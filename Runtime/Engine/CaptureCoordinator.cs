using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
namespace Dreamy.Debugger {
    public sealed class CaptureResult {
        public byte[] Jpeg; public string Reason; public DateTime CapturedUtc;
        public static CaptureResult WithoutImage(string reason)=>new CaptureResult{Reason=reason};
    }
    public sealed class CaptureCoordinator : MonoBehaviour {
        private const int CaptureBufferBudget = 24 * 1024 * 1024;
        private const int JpegBudget = 512 * 1024;
        private static readonly int[] JpegQualities = { 92, 82, 70 };
        private sealed class Operation {
            public readonly TaskCompletionSource<CaptureResult> Completion=new TaskCompletionSource<CaptureResult>();
            public CancellationTokenSource Cancellation;
            public Coroutine Coroutine;
            public bool Readback, Encoding, Finished;
        }
        private Operation active;
        private bool lowMemory;
        public Func<bool> SensitiveScreen;
        public bool IsSensitiveScreen { get { try{return SensitiveScreen?.Invoke()==true;}catch{return true;} } }
        public bool IsBusy=>active!=null;
        private void OnEnable()=>Application.lowMemory+=OnLowMemory;
        private void OnDisable(){Application.lowMemory-=OnLowMemory;Abort(active,"capture disabled");}
        private void OnDestroy()=>Abort(active,"capture destroyed");
        private void OnLowMemory(){lowMemory=true;Abort(active,"low memory");}
        private void Finish(Operation op){
            if(op==null || op.Finished)return;
            op.Finished=true;if(ReferenceEquals(active,op))active=null;op.Cancellation.Dispose();
        }
        private void Abort(Operation op,string reason){
            if(op==null || op.Finished)return;
            op.Cancellation.Cancel();
            op.Completion.TrySetResult(CaptureResult.WithoutImage(reason));
            // A submitted GPU request/encoder retains its resources and single-flight slot until completion.
            if(op.Readback || op.Encoding)return;
            if(op.Coroutine!=null)StopCoroutine(op.Coroutine);
            Finish(op);
        }
        public async Task<CaptureResult> CaptureAsync(int maxEdge,CancellationToken token){
            if(active!=null)return CaptureResult.WithoutImage("capture busy");
            if(!isActiveAndEnabled)return CaptureResult.WithoutImage("capture disabled");
            if(lowMemory)return CaptureResult.WithoutImage("low memory");
            if(Time.unscaledDeltaTime>.05f)return CaptureResult.WithoutImage("frame-time budget");
            if(IsSensitiveScreen)return CaptureResult.WithoutImage("sensitive screen");
            if(!SystemInfo.supportsAsyncGPUReadback)return CaptureResult.WithoutImage("GPU readback unsupported");
            if(!Application.isFocused || Screen.width<=0 || Screen.height<=0)return CaptureResult.WithoutImage("app not rendering");
            maxEdge=Mathf.Clamp(maxEdge,240,1440);
            if(token.IsCancellationRequested)return CaptureResult.WithoutImage("canceled");
            var op=new Operation{Cancellation=CancellationTokenSource.CreateLinkedTokenSource(token)};active=op;
            op.Coroutine=StartCoroutine(CaptureFrame(maxEdge,op));
            using(var timer=CancellationTokenSource.CreateLinkedTokenSource(token)){
                var timeout=Task.Delay(3000,timer.Token);
                if(await Task.WhenAny(op.Completion.Task,timeout)!=op.Completion.Task)Abort(op,token.IsCancellationRequested?"canceled":"capture timeout");
                timer.Cancel();
                return await op.Completion.Task;
            }
        }
        private IEnumerator CaptureFrame(int maxEdge,Operation op){
            yield return new WaitForEndOfFrame();
            if(op.Finished || op.Completion.Task.IsCompleted)yield break;
            if(op.Cancellation.IsCancellationRequested || !isActiveAndEnabled || !Application.isFocused || IsSensitiveScreen){Abort(op,IsSensitiveScreen?"sensitive screen":"canceled");yield break;}
            var sourceWidth=Screen.width;var sourceHeight=Screen.height;
            if(!TryGetCaptureSize(sourceWidth,sourceHeight,maxEdge,out var width,out var height)){
                Abort(op,"capture memory budget");yield break;
            }
            RenderTexture source=null,scaled=null;
            try{
                source=RenderTexture.GetTemporary(sourceWidth,sourceHeight,0,RenderTextureFormat.ARGB32);
                scaled=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(source);
                // The screenshot readback starts at the top on Vulkan and other top-origin backends.
                // Unity's JPEG encoder expects bottom-first rows. Correct the orientation in the downscale pass.
                if (SystemInfo.graphicsUVStartsAtTop)
                    Graphics.Blit(source, scaled, new Vector2(1, -1), new Vector2(0, 1));
                else
                    Graphics.Blit(source, scaled);
                var retainedSource=source;var retainedScaled=scaled;var at=DateTime.UtcNow;op.Readback=true;
                AsyncGPUReadback.Request(retainedScaled,0,TextureFormat.RGBA32,request=>{
                    byte[] bytes=null;string reason=null;
                    try{
                        if(request.hasError)reason="GPU readback failed";
                        else if(!op.Completion.Task.IsCompleted && !op.Cancellation.IsCancellationRequested && !IsSensitiveScreen)bytes=request.GetData<byte>().ToArray();
                        else reason=IsSensitiveScreen?"sensitive screen":"canceled";
                    }catch(Exception error){reason=error.GetType().Name;}
                    finally{RenderTexture.ReleaseTemporary(retainedSource);RenderTexture.ReleaseTemporary(retainedScaled);op.Readback=false;}
                    if(bytes==null){op.Completion.TrySetResult(CaptureResult.WithoutImage(reason??"capture unavailable"));Finish(op);return;}
                    op.Encoding=true;Encode(bytes,width,height,at,op);
                });
                source=scaled=null;
            }catch(Exception error){
                if(source!=null)RenderTexture.ReleaseTemporary(source);if(scaled!=null)RenderTexture.ReleaseTemporary(scaled);
                op.Readback=false;op.Completion.TrySetResult(CaptureResult.WithoutImage(error.GetType().Name));Finish(op);
            }
        }
        private async void Encode(byte[] bytes,int width,int height,DateTime at,Operation op){
            var token=op.Cancellation.Token;
            try{
                var jpeg=await Task.Run(()=>EncodeWithinBudget(bytes,width,height,token),token);
                op.Completion.TrySetResult(token.IsCancellationRequested?CaptureResult.WithoutImage("canceled"):IsSensitiveScreen?CaptureResult.WithoutImage("sensitive screen"):jpeg==null?CaptureResult.WithoutImage("encoded image cap"):new CaptureResult{Jpeg=jpeg,Reason="captured",CapturedUtc=at});
            }catch(OperationCanceledException){op.Completion.TrySetResult(CaptureResult.WithoutImage("canceled"));}
            catch(Exception error){op.Completion.TrySetResult(CaptureResult.WithoutImage(error.GetType().Name));}
            finally{op.Encoding=false;Finish(op);}
        }

        private static bool TryGetCaptureSize(int sourceWidth,int sourceHeight,int maxEdge,out int width,out int height)
        {
            width=height=0;
            if(sourceWidth<=0 || sourceHeight<=0)return false;
            var sourcePixels=(long)sourceWidth*sourceHeight;
            // Account for the full-size render texture, the scaled texture and its copied readback.
            var scaledPixels=(CaptureBufferBudget-sourcePixels*4)/8;
            if(scaledPixels<=0)return false;
            var scale=Math.Min(1d,Math.Min(maxEdge/(double)Math.Max(sourceWidth,sourceHeight),Math.Sqrt(scaledPixels/(double)sourcePixels)));
            width=(int)Math.Floor(sourceWidth*scale);
            height=(int)Math.Floor(sourceHeight*scale);
            return width>0 && height>0;
        }

        private static byte[] EncodeWithinBudget(byte[] bytes,int width,int height,CancellationToken token)
        {
            foreach(var quality in JpegQualities)
            {
                token.ThrowIfCancellationRequested();
                var encoded=ImageConversion.EncodeArrayToJPG(bytes,GraphicsFormat.R8G8B8A8_UNorm,(uint)width,(uint)height,0,quality);
                if(encoded!=null && encoded.Length>0 && encoded.Length<=JpegBudget)return encoded;
            }
            return null;
        }
    }
}
