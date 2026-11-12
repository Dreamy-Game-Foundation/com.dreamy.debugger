using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace Dreamy.Debugger
{
    public sealed partial class ScreenshotMode
    {
        public int StoredScreenshotCount { get; private set; }
        public long StoredScreenshotBytes { get; private set; }
        public bool IsStorageBusy { get; private set; }
        public string StorageError { get; private set; }
        private static readonly Regex ScreenshotName = new Regex(@"^Screenshot-[0-9]{8}-[0-9]{6}-[0-9]{3}-[0-9a-f]{8}\.png$", RegexOptions.CultureInvariant);

        public Task RefreshStorageAsync() => UpdateStorageAsync(false);
        public Task DeleteStoredScreenshotsAsync() => UpdateStorageAsync(true);

        private async Task UpdateStorageAsync(bool delete)
        {
            if (IsStorageBusy || IsBusy || !CanUse) return;
            IsStorageBusy = true;
            StorageError = null;
            var directory = OutputDirectory;
            try
            {
                var result = await Task.Run(() => ProcessStorage(directory, delete));
                if (this == null) return;
                StoredScreenshotCount = (int)result[0];
                StoredScreenshotBytes = result[1];
                StorageError = result[2] == 0 ? null : "Some screenshots could not be read or deleted. Try again.";
                if (delete)
                {
                    if (LastCapture != null && !File.Exists(LastCapture.Path))
                    {
                        HideWorkflowResult();
                        if (Preview != null) Destroy(Preview);
                        Preview = null;
                        LastCapture = null;
                    }
                    Status = StorageError ?? "Local screenshots deleted. Gallery copies are kept.";
                }
            }
            catch (Exception error) { if (this != null) StorageError = "Could not access screenshots: " + error.Message; }
            finally { if (this != null) IsStorageBusy = false; }
        }

        // Top-level, generated screenshot names only. Never traverse or follow file links.
        private static long[] ProcessStorage(string directory, bool delete)
        {
            long count = 0, bytes = 0, errors = 0;
            if (!Directory.Exists(directory)) return new long[] { 0, 0, 0 };
            foreach (var path in Directory.EnumerateFiles(directory, "Screenshot-*.png", SearchOption.TopDirectoryOnly))
            {
                if (!ScreenshotName.IsMatch(Path.GetFileName(path))) continue;
                try
                {
                    var info = new FileInfo(path);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if (delete) File.Delete(path);
                    if (File.Exists(path)) { count++; bytes += new FileInfo(path).Length; }
                }
                catch (IOException) { errors++; if (File.Exists(path)) { count++; try { bytes += new FileInfo(path).Length; } catch (IOException) { } } }
                catch (UnauthorizedAccessException) { errors++; }
            }
            return new[] { count, bytes, errors };
        }
    }
}
