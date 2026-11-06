using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dreamy.Debugger.Preferences;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.Services;
using Dreamy.Debugger.Presentation.Profiler;
using Dreamy.Debugger.Foundation.Service;
using UnityEngine;

namespace Dreamy.Debugger
{
    public sealed partial class DebuggerRuntime
    {
        public RuntimePreferences Preferences {get;private set;}
        /// <summary>Set before activating the runtime. Useful for an isolated test or a host-owned store.</summary>
        public IPreferenceStorage PreferenceStorage {get;set;}
        private DebuggerConfiguration preferenceDefaults;
        private HudMode desiredHudMode;
        private bool applyingPreferences;
        private sealed class SessionPreferences : IPreferenceStorage
        {
            public Task<string[]> ReadAsync()=>Task.FromResult(Array.Empty<string>());
            public Task WriteAsync(string json)=>Task.CompletedTask;
        }
        public static string PreferenceScope(string applicationId,string channel,string editorProject,int revision)
        {
            using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(applicationId+"|"+channel+"|"+editorProject+"|"+revision))).Replace("-", "").ToLowerInvariant().Substring(0,24);
        }
        private void InitializePreferences(Dictionary<string,string> seed)
        {
            preferenceDefaults=Instantiate(runtimeConfig);
            var scope=PreferenceScope(Application.identifier,Debug.isDebugBuild?"development":"release",Application.isEditor?Application.dataPath:"",runtimeConfig.PreferenceRevision);
            var storage=PreferenceStorage ?? (runtimeConfig.PersistRuntimePreferences
                ? (IPreferenceStorage)new PreferenceFileStore(Path.Combine(Application.persistentDataPath,"DreamyDebugger","Preferences",scope+".json"))
                : new SessionPreferences());
            Preferences=new RuntimePreferences(storage);
            if(runtimeConfig.PersistRuntimePreferences)Preferences.Seed(seed);
            Preferences.Changed+=ApplyPreferences;
            PreferenceBridge.Read=(key,fallback)=>Preferences.Get(key,fallback);
            PreferenceBridge.Write=(key,value)=>{if(!applyingPreferences)Preferences?.Set(key,value);};
        }
        private void ApplyPreferences(string key)
        {
            if(stopping || Preferences==null || preferenceDefaults==null)return;
            applyingPreferences=true;
            try
            {
                if(key==null || key.StartsWith("hud.") || key.StartsWith("fps."))
                {
                    var p=Preferences;var d=preferenceDefaults;
                    desiredHudMode=p.GetEnum("hud.mode",p.IsReady?d.InitialHud:HudMode.Off);
                    sessionHudPosition=p.GetEnum("hud.position",d.InitialHudPosition);
                    runtimeConfig.HudScale=p.GetNumber("hud.scale",d.HudScale,.5f,2);
                    runtimeConfig.HudMemory=p.GetBool("hud.memory",d.HudMemory);
                    runtimeConfig.HudAudio=p.GetBool("hud.audio",d.HudAudio);
                    runtimeConfig.HudDragEnabled=p.GetBool("hud.drag",d.HudDragEnabled);
                    runtimeConfig.GoodFps=(int)p.GetNumber("fps.good",d.GoodFps,2,1000);
                    runtimeConfig.CautionFps=(int)p.GetNumber("fps.caution",d.CautionFps,1,runtimeConfig.GoodFps-1);
                    runtimeConfig.FpsGood=ReadColor("fps.good-color",d.FpsGood);
                    runtimeConfig.FpsCaution=ReadColor("fps.caution-color",d.FpsCaution);
                    runtimeConfig.FpsCritical=ReadColor("fps.critical-color",d.FpsCritical);
                    runtimeConfig.LowFpsWarning=p.GetBool("fps.warning",d.LowFpsWarning);
                    runtimeConfig.LowFpsThreshold=p.GetNumber("fps.threshold",d.LowFpsThreshold,1,240);
                    runtimeConfig.LowFpsSustain=p.GetNumber("fps.sustain",d.LowFpsSustain,.1f,120);
                    runtimeConfig.LowFpsCooldown=p.GetNumber("fps.cooldown",d.LowFpsCooldown,1,3600);
                    fpsRule=null;
                    ApplyHud(desiredHudMode);
                    if(Hud!=null){if(key==null || key!="hud.mode" && key!="hud.position" && key!="hud.x" && key!="hud.y")Hud.Reconfigure(runtimeConfig);Hud.SetPosition(sessionHudPosition,false);if(p.Has("hud.x") && p.Has("hud.y"))Hud.SetNormalizedPosition(new Vector2(p.GetNumber("hud.x",.5f,0,1),p.GetNumber("hud.y",.5f,0,1)));}
                }
                if(hasPresentation && (key==null || key.StartsWith("console.")))
                {
                    var filter=ServiceManager.GetService<IConsoleFilterState>();
                    filter.SetConsoleFilterState(LogType.Log,Preferences.GetBool("console.log",true));
                    filter.SetConsoleFilterState(LogType.Warning,Preferences.GetBool("console.warning",true));
                    filter.SetConsoleFilterState(LogType.Error,Preferences.GetBool("console.error",true));
                    var console=ServiceManager.GetService<IConsoleService>() as Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService;if(console!=null)console.CollapseEnabled=Preferences.GetBool("console.collapse",Settings.Instance.CollapseDuplicateLogEntries);
                }
                if(hasPresentation && (key==null || key.StartsWith("category.") || key.StartsWith("pin.") || key.StartsWith("console.")))PreferenceBridge.NotifyRestored();
            }
            finally{applyingPreferences=false;}
        }
        private Color ReadColor(string key,Color fallback)=>ColorUtility.TryParseHtmlString(Preferences.Get(key,""),out var value)?value:fallback;
        public void SetFpsColor(string key,string html){if(key!="fps.good-color" && key!="fps.caution-color" && key!="fps.critical-color")return;if(ColorUtility.TryParseHtmlString(html,out var color))Preferences?.Set(key,"#"+ColorUtility.ToHtmlStringRGBA(color));}
        public void SetPreference(string key,bool value)=>Preferences?.Set(key,value);
        public void SetPreference(string key,float value)=>Preferences?.Set(key,value);
        public void SetHud(HudMode mode){if(Enum.IsDefined(typeof(HudMode),mode))Preferences?.Set("hud.mode",mode.ToString());}
        public void SetHudPosition(HudPosition position){if(Enum.IsDefined(typeof(HudPosition),position))Preferences?.Set("hud.position",position.ToString());}
        public void SetHudScale(float value){if(!float.IsNaN(value) && !float.IsInfinity(value))Preferences?.Set("hud.scale",Mathf.Clamp(value,.5f,2));}
        public void ResetHudPosition(){Preferences?.Set("hud.x",.5f);Preferences?.Set("hud.y",.5f);SetHudPosition(preferenceDefaults.InitialHudPosition);}
        public void ResetLocalPreferences()=>Preferences?.Reset();
        public Task FlushPreferencesAsync()=>Preferences?.FlushAsync() ?? Task.CompletedTask;
        private void ApplyHud(HudMode mode)
        {
            if(!hasPresentation)return;
            if(Hud==null && mode!=HudMode.Off)
            {
                var prefab=PresentationAssets.Load<GameObject>("DreamyDebugger/PerformanceHud");
                if(prefab!=null)
                {
                    Hud=Instantiate(prefab,transform).GetComponent<PerformanceHud>();Hud.Configure(runtimeConfig);
                    Hud.ModeChanged=SetHud;Hud.PositionChanged=SetHudPosition;
                    Hud.DragCompleted=point=>{Preferences?.Set("hud.x",point.x);Preferences?.Set("hud.y",point.y);SetHudPosition(HudPosition.Free);};
                }
            }
            ProfilerServiceImpl.CollectForHud=mode!=HudMode.Off;
            if(Hud!=null){Hud.SetMode(mode,false);if(captureOverlayUsers>0)Hud.gameObject.SetActive(false);}
        }
    }
}
