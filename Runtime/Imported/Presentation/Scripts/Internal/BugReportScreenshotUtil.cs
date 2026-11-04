using System.Collections;
namespace Dreamy.Debugger.Presentation.Internal {
    public static class BugReportScreenshotUtil {
        public static byte[] ScreenshotData;
        public static IEnumerator ScreenshotCaptureCo(){
            ScreenshotData=null;if(ScreenshotBridge.Capture==null)yield break;
            var task=ScreenshotBridge.Capture();while(!task.IsCompleted)yield return null;
            if(task.Status==System.Threading.Tasks.TaskStatus.RanToCompletion)ScreenshotData=task.Result;
        }
    }
}
