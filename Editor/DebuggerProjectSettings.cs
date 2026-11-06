using Dreamy.Debugger.Presentation;
using UnityEditor;
using UnityEngine;

namespace Dreamy.Debugger.Editor
{
    [FilePath("ProjectSettings/DreamyDebuggerSettings.asset",FilePathAttribute.Location.ProjectFolder)]
    public sealed class DebuggerProjectSettings : ScriptableSingleton<DebuggerProjectSettings>
    {
        public DebuggerLoadingMode Loading=DebuggerLoadingMode.Automatic;
        public DebuggerConfiguration Configuration;
        public void SaveSettings()=>Save(true);
    }
    [InitializeOnLoad]
    public static class DebuggerEditorStartup
    {
        private static DebuggerStartupAssets payload;
        private static DebuggerConfiguration defaults;
        static DebuggerEditorStartup(){DebuggerBootstrap.AssetProvider=Resolve;}
        public static DebuggerStartupAssets Resolve()
        {
            if(payload==null){payload=ScriptableObject.CreateInstance<DebuggerStartupAssets>();payload.hideFlags=HideFlags.HideAndDontSave;}
            if(defaults==null){defaults=ScriptableObject.CreateInstance<DebuggerConfiguration>();defaults.hideFlags=HideFlags.HideAndDontSave;}
            payload.Loading=DebuggerProjectSettings.instance.Loading;
            payload.Configuration=DebuggerProjectSettings.instance.Configuration ?? defaults;
            payload.Catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(DebuggerAssetBuilder.CatalogPath);
            return payload;
        }
    }
}
