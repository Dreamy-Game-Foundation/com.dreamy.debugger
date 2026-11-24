using System;
using System.IO;
using UnityEngine;

namespace Dreamy.Debugger
{
    /// <summary>Explicit user actions only. Android 10+ uses app-owned MediaStore images.</summary>
    public static class ScreenshotExport
    {
        public static bool CanOpenFolder => Application.isEditor || Application.platform == RuntimePlatform.LinuxPlayer ||
            Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer;
        public static bool CanSaveToGallery
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!gallerySupported.HasValue)
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION")) gallerySupported = version.GetStatic<int>("SDK_INT") >= 29;
                return gallerySupported.Value;
#else
                return false;
#endif
            }
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool? gallerySupported;
        private static string exportedPath, exportedUri;
#endif

        public static void OpenFolder(string path)
        {
            if (!CanOpenFolder) throw new NotSupportedException("Open folder is available in the Editor and desktop players.");
            Application.OpenURL(new Uri(Path.GetFullPath(path)).AbsoluteUri);
        }

        public static string SaveToGallery(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The screenshot is no longer available.");
            if (!CanSaveToGallery) throw new NotSupportedException("Gallery export requires Android 10 or later.");
#if UNITY_ANDROID && !UNITY_EDITOR
            if (path == exportedPath && !string.IsNullOrEmpty(exportedUri)) return exportedUri;
            var contentUri = CopyToMediaStore(path, "android.provider.MediaStore$Images$Media", "image/png", "Pictures/DreamyDebugger");
            exportedPath = path;
            return exportedUri = contentUri;
#else
            throw new NotSupportedException("Gallery export requires Android 10 or later.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        internal static void ShareReportArchive(string archive)
        {
            if (!CanSaveToGallery) throw new NotSupportedException("Sharing reports requires Android 10 or later.");
            var uri = CopyToMediaStore(archive, "android.provider.MediaStore$Downloads", "application/zip", "Download/DreamyDebugger");
            OpenShareSheet(uri, "application/zip", "Share report");
        }

        private static string CopyToMediaStore(string path, string collectionName, string mimeType, string relativePath)
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (var media = new AndroidJavaClass(collectionName))
            using (var collection = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            using (var values = new AndroidJavaObject("android.content.ContentValues"))
            {
                values.Call("put", "_display_name", Path.GetFileName(path));
                values.Call("put", "mime_type", mimeType);
                values.Call("put", "relative_path", relativePath);
                using (var pending = new AndroidJavaObject("java.lang.Integer", 1)) values.Call("put", "is_pending", pending);
                using (var uri = resolver.Call<AndroidJavaObject>("insert", collection, values))
                {
                    if (uri == null) throw new IOException("Android could not create the export.");
                    try
                    {
                        using (var stream = resolver.Call<AndroidJavaObject>("openOutputStream", uri, "w"))
                        {
                            if (stream == null) throw new IOException("Android could not open the export.");
                            var bytes = File.ReadAllBytes(path);
                            var signed = new sbyte[bytes.Length]; Buffer.BlockCopy(bytes, 0, signed, 0, bytes.Length);
                            stream.Call("write", signed);
                            stream.Call("flush");
                            stream.Call("close");
                        }
                        values.Call("clear");
                        using (var ready = new AndroidJavaObject("java.lang.Integer", 0)) values.Call("put", "is_pending", ready);
                        if (resolver.Call<int>("update", uri, values, null, null) != 1) throw new IOException("Android could not publish the export.");
                        return uri.Call<string>("toString");
                    }
                    catch { resolver.Call<int>("delete", uri, null, null); throw; }
                }
            }
        }
#endif

        public static void Share(string path)
        {
            var contentUri = SaveToGallery(path);
#if UNITY_ANDROID && !UNITY_EDITOR
            OpenShareSheet(contentUri, "image/png", "Share screenshot");
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private static void OpenShareSheet(string contentUri, string mimeType, string title)
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var uriType = new AndroidJavaClass("android.net.Uri"))
            using (var uri = uriType.CallStatic<AndroidJavaObject>("parse", contentUri))
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
            using (var clipType = new AndroidJavaClass("android.content.ClipData"))
            using (var clip = clipType.CallStatic<AndroidJavaObject>("newRawUri", title, uri))
            using (var intentType = new AndroidJavaClass("android.content.Intent"))
            {
                using (intent.Call<AndroidJavaObject>("setType", mimeType)) { }
                using (intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri)) { }
                using (intent.Call<AndroidJavaObject>("setClipData", clip)) { }
                using (intent.Call<AndroidJavaObject>("addFlags", 1)) { }
                using (var chooser = intentType.CallStatic<AndroidJavaObject>("createChooser", intent, title))
                    activity.Call("startActivity", chooser);
            }
        }
#endif
    }
}
