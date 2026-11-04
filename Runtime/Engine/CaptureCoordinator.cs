using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
namespace Dreamy.Debugger {
    public sealed class CaptureResult {
        public byte[] Jpeg;public string Reason;public DateTime CapturedUtc;
        public static CaptureResult WithoutImage(string reason)=>new CaptureResult{Reason=reason};
    }
    public sealed class CaptureCoordinator : MonoBehaviour {
        private bool busy,lowMemory;
        /// <summary>Host can suppress sensitive screens. No unredacted frame is persisted before encoding.</summary>
        public Func<bool> SensitiveScreen;
        private void OnEnable()=>Application.lowMemory+=OnLowMemory;
        private void OnDisable()=>Application.lowMemory-=OnLowMemory;
        private void OnLowMemory(){lowMemory=true;}
        public async Task<CaptureResult> CaptureAsync(int maxEdge,CancellationToken token){
            if(busy)return CaptureResult.WithoutImage("capture busy");
            if(lowMemory)return CaptureResult.WithoutImage("low memory");
            if(Time.unscaledDeltaTime>.05f)return CaptureResult.WithoutImage("frame-time budget");
            try{if(SensitiveScreen?.Invoke()==true)return CaptureResult.WithoutImage("sensitive screen");}
            catch(Exception){return CaptureResult.WithoutImage("capture policy error");}
            if(!SystemInfo.supportsAsyncGPUReadback)return CaptureResult.WithoutImage("GPU readback unsupported");
            if(!Application.isFocused || Screen.width<=0 || Screen.height<=0)return CaptureResult.WithoutImage("app not rendering");
            var estimate=(long)Screen.width*Screen.height*4+Math.Min(960,maxEdge)*Math.Min(960,maxEdge)*8L;
            if(estimate>24*1024*1024)return CaptureResult.WithoutImage("capture memory budget");
            if(token.IsCancellationRequested)return CaptureResult.WithoutImage("canceled");
            busy=true;
            var completion=new TaskCompletionSource<CaptureResult>();
            StartCoroutine(CaptureFrame(Mathf.Clamp(maxEdge,240,960),token,completion));
            var timeout=Task.Delay(3000,token);
            if(await Task.WhenAny(completion.Task,timeout)!=completion.Task){completion.TrySetResult(CaptureResult.WithoutImage(token.IsCancellationRequested?"canceled":"capture timeout"));}
            return await completion.Task;
        }
        private IEnumerator CaptureFrame(int maxEdge,CancellationToken token,TaskCompletionSource<CaptureResult> completion){
            yield return new WaitForEndOfFrame();
            if(completion.Task.IsCompleted || token.IsCancellationRequested){busy=false;completion.TrySetResult(CaptureResult.WithoutImage("canceled"));yield break;}
            RenderTexture source=null,scaled=null;
            try{
                source=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);
                var scale=Mathf.Min(1f,maxEdge/(float)Mathf.Max(Screen.width,Screen.height));
                var width=Mathf.Max(1,Mathf.RoundToInt(Screen.width*scale));var height=Mathf.Max(1,Mathf.RoundToInt(Screen.height*scale));
                scaled=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(source);Graphics.Blit(source,scaled);
                var retainedSource=source;var retainedScaled=scaled;
                var at=DateTime.UtcNow;
                AsyncGPUReadback.Request(retainedScaled,0,TextureFormat.RGBA32,request=>{
                    byte[] bytes=null;string reason=null;
                    try{if(request.hasError)reason="GPU readback failed";else if(!completion.Task.IsCompleted && !token.IsCancellationRequested)bytes=request.GetData<byte>().ToArray();else reason="canceled";}
                    catch(Exception error){reason=error.GetType().Name;}
                    finally{RenderTexture.ReleaseTemporary(retainedSource);RenderTexture.ReleaseTemporary(retainedScaled);}
                    if(bytes==null){if(this!=null)busy=false;completion.TrySetResult(CaptureResult.WithoutImage(reason??"capture unavailable"));return;}
                    Encode(bytes,width,height,at,token,completion);
                });
                source=scaled=null;
            }catch(Exception error){if(source!=null)RenderTexture.ReleaseTemporary(source);if(scaled!=null)RenderTexture.ReleaseTemporary(scaled);busy=false;completion.TrySetResult(CaptureResult.WithoutImage(error.GetType().Name));}
        }
        private async void Encode(byte[] bytes,int width,int height,DateTime at,CancellationToken token,TaskCompletionSource<CaptureResult> completion){
            try{
                var jpeg=await Task.Run(()=>{token.ThrowIfCancellationRequested();return ImageConversion.EncodeArrayToJPG(bytes,GraphicsFormat.R8G8B8A8_UNorm,(uint)width,(uint)height,0,70);},token);
                completion.TrySetResult(token.IsCancellationRequested?CaptureResult.WithoutImage("canceled"):jpeg.Length>512*1024?CaptureResult.WithoutImage("encoded image cap"):new CaptureResult{Jpeg=jpeg,Reason="captured",CapturedUtc=at});
            }catch(OperationCanceledException){completion.TrySetResult(CaptureResult.WithoutImage("canceled"));}
            catch(Exception error){completion.TrySetResult(CaptureResult.WithoutImage(error.GetType().Name));}
            finally{if(this!=null)busy=false;}
        }
    }
}
