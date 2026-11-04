using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
namespace Dreamy.Debugger.Editor {
    public static class DebuggerVisualVerification {
        public static Task<CaptureResult> CaptureJob;
        public static double CaptureDispatchMilliseconds;
        public static void StartCapture(){
            var runtime=DebuggerRuntime.Instance;if(runtime==null)throw new InvalidOperationException("Run the production demo first");
            var clock=System.Diagnostics.Stopwatch.StartNew();CaptureJob=runtime.GetComponent<CaptureCoordinator>().CaptureAsync(960,CancellationToken.None);clock.Stop();CaptureDispatchMilliseconds=clock.Elapsed.TotalMilliseconds;
        }
        public static string SaveCapture(string directory){
            if(CaptureJob==null || !CaptureJob.IsCompleted)return "pending";
            var result=CaptureJob.GetAwaiter().GetResult();Directory.CreateDirectory(directory);
            if(result.Jpeg!=null)File.WriteAllBytes(Path.Combine(directory,"async-screenshot.jpg"),result.Jpeg);
            File.WriteAllText(Path.Combine(directory,"capture-receipt.json"),JsonUtility.ToJson(new Receipt{reason=result.Reason,bytes=result.Jpeg?.Length??0,dispatchMilliseconds=CaptureDispatchMilliseconds,utc=result.CapturedUtc.ToString("O"),environment="Unity Editor Linux, not mobile device evidence"},true));
            return result.Reason;
        }
        [Serializable]private sealed class Receipt {public string reason,utc,environment;public int bytes;public double dispatchMilliseconds;}
    }
}
