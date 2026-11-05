using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dreamy.Debugger.Editor
{
    // Editor-only destination settings. No token and no runtime/build dependency.
    public sealed class TelegramRelaySettings : ScriptableObject
    {
        public string ChatId = "";
        public const string AssetPath = "Assets/Editor/DreamyDebugger/TelegramRelaySettings.asset";

        public static TelegramRelaySettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TelegramRelaySettings>(AssetPath);
            if (settings != null) return settings;
            if (!AssetDatabase.IsValidFolder("Assets/Editor")) AssetDatabase.CreateFolder("Assets", "Editor");
            if (!AssetDatabase.IsValidFolder("Assets/Editor/DreamyDebugger")) AssetDatabase.CreateFolder("Assets/Editor", "DreamyDebugger");
            settings = CreateInstance<TelegramRelaySettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            return settings;
        }

        public static void DrawSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TelegramRelaySettings>(AssetPath);
            if (settings == null)
            {
                if (GUILayout.Button("Create Telegram destination settings (Editor only)")) Selection.activeObject = GetOrCreate();
                return;
            }
            var serialized = new SerializedObject(settings);
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("ChatId"), new GUIContent("Telegram chat ID"));
            if (serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(settings);
            EditorGUILayout.HelpBox("Chat ID is the destination, not the bot token. Use the discovery command after sending /start to your bot. Token is entered privately in the terminal.", MessageType.Info);
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.dreamy.debugger/package.json");
            if (package == null) return;
            var script = Path.Combine(package.resolvedPath, "Tools~/relay/telegram_setup.py");
            // Quoted for a POSIX shell; no credentials enter the command or clipboard.
            var command = "python3 " + Quote(script);
            if (GUILayout.Button("Copy command to discover chat IDs")) EditorGUIUtility.systemCopyBuffer = command + " --discover-chat";
            if (GUILayout.Button("Copy local relay command using this settings asset")) EditorGUIUtility.systemCopyBuffer = command + " --settings " + Quote(Path.GetFullPath(AssetPath));
            if (GUILayout.Button("Select Telegram settings asset")) Selection.activeObject = settings;
        }

        private static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";
    }
}
