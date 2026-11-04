using System.Text;
namespace Dreamy.Debugger {
    internal static class AndroidExitEvidence {
        public static string Collect(long after,out long newest){
            newest=after;
#if UNITY_ANDROID && !UNITY_EDITOR
            try{
                using(var version=new UnityEngine.AndroidJavaClass("android.os.Build$VERSION"))if(version.GetStatic<int>("SDK_INT")<30)return "";
                using(var player=new UnityEngine.AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<UnityEngine.AndroidJavaObject>("currentActivity"))
                using(var manager=activity.Call<UnityEngine.AndroidJavaObject>("getSystemService","activity"))
                using(var list=manager.Call<UnityEngine.AndroidJavaObject>("getHistoricalProcessExitReasons",UnityEngine.Application.identifier,0,3)){
                    var text=new StringBuilder();
                    for(var i=0;i<list.Call<int>("size");i++)using(var entry=list.Call<UnityEngine.AndroidJavaObject>("get",i)){
                        var reason=entry.Call<int>("getReason");var timestamp=entry.Call<long>("getTimestamp");
                        if(timestamp<=after || (reason!=3 && reason!=4 && reason!=5 && reason!=6))continue;
                        newest=System.Math.Max(newest,timestamp);text.Append("\nreason=").Append(reason).Append(" at=").Append(timestamp).Append(" importance=").Append(entry.Call<int>("getImportance"));
                    }
                    return text.Length==0?"":"Android OS previous process exit evidence (not a screenshot/crash dump):"+text;
                }
            }catch(System.Exception){return "";}
#else
            return "";
#endif
        }
    }
}
