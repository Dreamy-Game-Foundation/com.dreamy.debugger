using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Dreamy.Debugger.Editor
{
    /// <summary>Window-owned mobile setup. Never installs software, sends reports or changes a build automatically.</summary>
    public sealed class ReportHttpsSetup : IDisposable
    {
        private readonly Action repaint;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly ConcurrentQueue<string> output = new ConcurrentQueue<string>();
        private Process tunnel;
        private Task<string> healthRequest;
        private bool localRequest, localReady, httpsReady, polling, disposed, expanded;
        private int port = 8766;
        private string executable = "cloudflared", httpsUrl = "", message = "", lastLine = "";
        private string lastKey;
        private string requestKey;
        private MessageType messageType = MessageType.Info;
        private static readonly Regex TunnelUrl = new Regex(@"https://[a-z0-9-]+\.trycloudflare\.com\b", RegexOptions.IgnoreCase);
        [Serializable] private sealed class Health
        {
            public string service;
            public bool fakeDelivery, supportsFullTest, ingestKeyRequired;
        }
        public ReportHttpsSetup(Action repaint)
        {
            this.repaint = repaint;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }
        public static bool TryMobileRelay(string text, out Uri uri)
        {
            return Uri.TryCreate(text?.Trim(), UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps &&
                !uri.IsLoopback && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 &&
                uri.AbsolutePath.TrimEnd('/').EndsWith("/reports", StringComparison.Ordinal) &&
                !ReportRedactor.ContainsProviderCredential(text);
        }
        public static string InstallCommand(RuntimePlatform platform)
        {
            if (platform == RuntimePlatform.WindowsEditor) return "winget install --id Cloudflare.cloudflared --exact";
            if (platform == RuntimePlatform.OSXEditor) return "brew install cloudflared";
            return "https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/";
        }
        public void Draw(SerializedObject configuration)
        {
            var currentKey = configuration.FindProperty("LimitedClientKey").stringValue;
            if (currentKey != lastKey) { lastKey = currentKey; localReady = httpsReady = false; }
            expanded = EditorGUILayout.Foldout(expanded, "Android / iOS HTTPS setup", true);
            if (!expanded) return;
            if (GUILayout.Button("Open complete mobile setup checklist"))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ReportHttpsSetup).Assembly);
                if (package != null) EditorUtility.OpenWithDefaultApp(Path.Combine(package.resolvedPath, "Documentation~", "MOBILE_REPORT_SETUP.md"));
            }
            EditorGUILayout.HelpBox("Localhost is blocked by the mobile runtime, including adb reverse. Use an existing HTTPS relay, or a free temporary Cloudflare Quick Tunnel for QA. Your PC, relay and tunnel must stay running. URL changes after restart; rebuild the app. Closing this window or reloading scripts stops its tunnel.", MessageType.Info);
            DirectQaSetupWizard.Step("1  Prepare the local relay",localReady);
            using (new EditorGUI.DisabledScope(tunnel != null || healthRequest != null))
            {
                var nextPort = Mathf.Clamp(EditorGUILayout.IntField("Local relay port", port), 1, 65535);
                if (nextPort != port) { port = nextPort; localReady = httpsReady = false; }
                if (GUILayout.Button("Prepare ingest key for local launchers")) PrepareKey(configuration);
                if (GUILayout.Button("Check local relay")) Check("http://127.0.0.1:" + port + "/reports", true, currentKey);
            }
            EditorGUILayout.HelpBox("Start or restart the saved Discord/Telegram relay after preparing its ingest key. Local launchers use this limited key; bot tokens/webhooks remain in the OS credential store. A relay must report live mode and ingest-key protection before a tunnel can start.", MessageType.None);
            DirectQaSetupWizard.Step("2  Install and start an HTTPS tunnel",tunnel!=null && !string.IsNullOrEmpty(httpsUrl));
            if(GUILayout.Button("Install cloudflared"))
            {
                var directory=Path.Combine(Application.dataPath,"../Library/DreamyDebugger/tools");
                if(TelegramRelaySettings.OpenTunnelInstaller(directory))
                {
                    if(Application.platform==RuntimePlatform.LinuxEditor)executable=Path.GetFullPath(Path.Combine(directory,"cloudflared"));
                    SetMessage("Finish the installation in the terminal, then start the QA tunnel.",MessageType.Info);
                }
                else SetMessage("Could not open an installation terminal. Use the official download instructions.",MessageType.Warning);
            }
            if (GUILayout.Button("Copy install command / download URL")) EditorGUIUtility.systemCopyBuffer = InstallCommand(Application.platform);
            if (GUILayout.Button("Open official download instructions")) Application.OpenURL("https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/");
            using (new EditorGUI.DisabledScope(tunnel != null || healthRequest != null))
            {
                executable = EditorGUILayout.TextField("cloudflared executable", executable);
                if (GUILayout.Button("Locate installed cloudflared"))
                {
                    var selected = EditorUtility.OpenFilePanel("Select cloudflared executable", "", Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
                    if (!string.IsNullOrEmpty(selected)) executable = selected;
                }
            }
            using (new EditorGUI.DisabledScope(!localReady || tunnel != null || healthRequest != null))
                if (GUILayout.Button("Start free QA tunnel")) Start();
            using (new EditorGUI.DisabledScope(tunnel == null))
                if (GUILayout.Button("Stop this window's tunnel")) Stop();
            DirectQaSetupWizard.Step("3  Verify and apply HTTPS",httpsReady);
            using (new EditorGUI.DisabledScope(healthRequest != null))
            {
                var next = EditorGUILayout.TextField("HTTPS relay URL", httpsUrl);
                if (next != httpsUrl) { httpsUrl = next; httpsReady = false; }
                using (new EditorGUI.DisabledScope(!TryMobileRelay(httpsUrl, out _)))
                    if (GUILayout.Button("Check HTTPS relay")) Check(httpsUrl, false, currentKey);
            }
            using (new EditorGUI.DisabledScope(!httpsReady || healthRequest != null || !TryMobileRelay(httpsUrl, out _)))
                if (GUILayout.Button("Apply HTTPS URL to selected configuration"))
                {
                    configuration.FindProperty("RelayUrl").stringValue = httpsUrl.Trim();
                    configuration.FindProperty("UploadsEnabled").boolValue = false;
                    configuration.ApplyModifiedProperties();
                    EditorUtility.SetDirty(configuration.targetObject); AssetDatabase.SaveAssetIfDirty(configuration.targetObject);
                    SetMessage("HTTPS URL saved; sending remains off. Send a full test to each destination below, match the Report ID/receipt, then enable sending and rebuild the QA app. Health checks do not prove destination delivery.", MessageType.Info);
                }
            if (healthRequest != null) EditorGUILayout.LabelField("Checking relay...");
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
        }
        private void PrepareKey(SerializedObject configuration)
        {
            var property = configuration.FindProperty("LimitedClientKey");
            var key = property.stringValue;
            if (string.IsNullOrWhiteSpace(key) || key.Length < 16) key = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            if (ReportRedactor.ContainsProviderCredential(key)) { SetMessage("Use a limited ingest key, never a Telegram token or Discord webhook.", MessageType.Error); return; }
            if (key.Length > 512 || Array.Exists(key.ToCharArray(), c => c < 33 || c > 126)) { SetMessage("Ingest key must contain 16–512 printable ASCII characters without spaces.", MessageType.Error); return; }
            try
            {
                var root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "DreamyDebugger");
                foreach (var provider in new[] { "TelegramRelay", "DiscordRelay" })
                {
                    var folder = Path.Combine(root, provider); Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, "ingest-key"), key);
                }
                property.stringValue = key; configuration.ApplyModifiedProperties();
                EditorUtility.SetDirty(configuration.targetObject); AssetDatabase.SaveAssetIfDirty(configuration.targetObject);
                localReady = httpsReady = false;
                SetMessage("Limited ingest key saved to this configuration and local launcher settings. Restart your relay, then Check local relay. Provider credentials were not changed.", MessageType.Info);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { SetMessage("Could not save local launcher settings. Check project write permissions.", MessageType.Error); }
        }
        private void Check(string url, bool local, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) { SetMessage("Prepare a limited ingest key first.", MessageType.Warning); return; }
            localRequest = local;
            requestKey = key;
            if (local) localReady = false; else httpsReady = false;
            var uri = new Uri(url.Trim());
            var health = new UriBuilder(uri) { Path = uri.AbsolutePath.TrimEnd('/').Substring(0, uri.AbsolutePath.TrimEnd('/').Length - "/reports".Length) + "/health" }.Uri;
            healthRequest = ReadHealth(health, key, lifetime.Token); Polling(true);
        }
        private static async Task<string> ReadHealth(Uri uri, string key, CancellationToken token)
        {
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = !uri.IsLoopback })
            using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 8192 })
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Dreamy-Key", key);
                using (var response = await client.GetAsync(uri, token))
                {
                    response.EnsureSuccessStatusCode(); return await response.Content.ReadAsStringAsync();
                }
            }
        }
        private void Start()
        {
            try
            {
                while (output.TryDequeue(out _)) { }
                lastLine = "";
                var start = new ProcessStartInfo(ResolveExecutable()) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { "tunnel", "--no-autoupdate", "--url", "http://127.0.0.1:" + port }) start.ArgumentList.Add(arg);
                tunnel = new Process { StartInfo = start };
                tunnel.OutputDataReceived += Receive; tunnel.ErrorDataReceived += Receive;
                if (!tunnel.Start()) throw new InvalidOperationException();
                tunnel.BeginOutputReadLine(); tunnel.BeginErrorReadLine(); httpsUrl = ""; httpsReady = false;
                SetMessage("Starting cloudflared. Waiting for its HTTPS URL...", MessageType.Info); Polling(true);
            }
            catch (Exception e) when (e is Win32Exception || e is InvalidOperationException || e is IOException)
            { Stop(); SetMessage("cloudflared could not start. Install it, then Locate installed cloudflared (Unity may have an older PATH).", MessageType.Error); }
        }
        private string ResolveExecutable()
        {
            var value=executable.Trim();
            if(value!="cloudflared")return value;
            if(Application.platform==RuntimePlatform.OSXEditor)
            {
                foreach(var path in new[]{"/opt/homebrew/bin/cloudflared","/usr/local/bin/cloudflared"})
                    if(File.Exists(path))return path;
            }
            if(Application.platform==RuntimePlatform.WindowsEditor)
            {
                var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WinGet","Links","cloudflared.exe");
                if(File.Exists(path))return path;
            }
            if(Application.platform==RuntimePlatform.LinuxEditor)
            {
                var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/DreamyDebugger/tools/cloudflared"));
                if(File.Exists(path))return path;
            }
            return value;
        }
        private void Receive(object sender, DataReceivedEventArgs args) { if (!string.IsNullOrEmpty(args.Data) && output.Count < 64) output.Enqueue(args.Data); }
        private void Poll()
        {
            if (disposed) return;
            var changed = false;
            while (output.TryDequeue(out var line))
            {
                lastLine = line.Length > 240 ? line.Substring(0, 240) : line;
                var match = TunnelUrl.Match(line);
                if (match.Success && httpsUrl.Length == 0)
                { httpsUrl = match.Value + "/reports"; SetMessage("Temporary URL found. Check HTTPS relay before applying it. Keep this window open during QA.", MessageType.Info); changed = true; }
            }
            if (healthRequest != null && healthRequest.IsCompleted)
            {
                try
                {
                    var health = JsonUtility.FromJson<Health>(healthRequest.GetAwaiter().GetResult());
                    var ready = requestKey == lastKey && health != null && health.service == "dreamy.debugger" && !health.fakeDelivery && health.supportsFullTest && health.ingestKeyRequired;
                    if (localRequest) localReady = ready; else httpsReady = ready;
                    SetMessage(ready ? "Relay reachable, live mode and ingest-key protection confirmed. Destination delivery still needs a full test." : "Relay is not ready: update/restart it in live mode with a limited ingest key, then check again.", ready ? MessageType.Info : MessageType.Warning);
                }
                catch (Exception) { SetMessage("Relay check failed. Check the URL, relay process, network/TLS and /health proxy route, then retry.", MessageType.Error); }
                healthRequest = null; changed = true;
            }
            if (tunnel != null && tunnel.HasExited)
            { tunnel.Dispose(); tunnel = null; httpsReady = localReady = false; SetMessage("Tunnel exited. " + lastLine + " Restart it, verify the new URL and rebuild the QA app.", MessageType.Warning); changed = true; }
            if (changed) repaint?.Invoke();
            if (tunnel == null && healthRequest == null) Polling(false);
        }
        private void Polling(bool enable)
        {
            if (enable == polling) return;
            polling = enable;
            if (enable) EditorApplication.update += Poll; else EditorApplication.update -= Poll;
        }
        private void SetMessage(string value, MessageType type) { message = value; messageType = type; repaint?.Invoke(); }
        private void Stop()
        {
            if (tunnel != null)
            {
                try { if (!tunnel.HasExited) tunnel.Kill(); } catch (Exception e) when (e is InvalidOperationException || e is Win32Exception) { }
                tunnel.Dispose(); tunnel = null;
            }
            httpsReady = false;
            SetMessage("This window's tunnel stopped. Restarting creates a new URL; verify, apply and rebuild before mobile testing.", MessageType.Info);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose; EditorApplication.quitting -= Dispose;
            Polling(false); lifetime.Cancel(); Stop(); lifetime.Dispose();
        }
    }
}
