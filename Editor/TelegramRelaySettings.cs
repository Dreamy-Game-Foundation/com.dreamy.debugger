using System;
using System.ComponentModel;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Reporting;
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

        private static readonly LocalRelayControls Telegram = new LocalRelayControls("Telegram", 8765);
        private static readonly LocalRelayControls Discord = new LocalRelayControls("Discord", 8766);
        private static string ProjectDirectory => Path.GetDirectoryName(Application.dataPath);

        [InitializeOnLoadMethod]
        private static void ScheduleRelayStartup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode) return;
                var script = FindScript();
                if (script == null) return;
                Telegram.StartIfEnabled(script, TelegramSettingsArguments());
                Discord.StartIfEnabled(script, Array.Empty<string>());
            };
        }

        private static string FindScript()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.dreamy.debugger/package.json");
            return package == null ? null : Path.Combine(package.resolvedPath, "Tools~/relay/telegram_setup.py");
        }

        private static string[] TelegramSettingsArguments()
        {
            var path = Path.Combine(ProjectDirectory, AssetPath);
            return File.Exists(path) ? new[] { "--settings", path } : Array.Empty<string>();
        }

        internal static string GetLocalRelayUrl(string destination)
            => (destination == "Discord" ? Discord : Telegram).RelayUrl;

        internal static string ReadLocalIngestKey(string destination)
        {
            var path = Path.Combine(ProjectDirectory, "Library", "DreamyDebugger", destination + "Relay", "ingest-key");
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 1024) return "";
                var key = File.ReadAllText(path).Trim();
                return key.Length >= 16 && key.Length <= 512 && key.All(c => c >= 33 && c <= 126) &&
                    !ReportRedactor.ContainsProviderCredential(key) ? key : "";
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { return ""; }
        }

        internal static Task<DirectQaBuildPreset> ReadSavedProfileAsync(int provider, CancellationToken token)
        {
            var script = FindScript();
            if (script == null) return Task.FromResult<DirectQaBuildPreset>(null);
            return provider == 0
                ? Discord.ReadSavedProfileAsync(script, Array.Empty<string>(), token)
                : Telegram.ReadSavedProfileAsync(script, TelegramSettingsArguments(), token);
        }

        internal static bool OpenPythonInstaller()
        {
            return LocalRelayControls.OpenInstallTerminal(
                "if (Get-Command winget -ErrorAction SilentlyContinue) { winget install --id Python.Python.3.12 --exact } else { Start-Process 'https://www.python.org/downloads/windows/' }",
                "export PATH=\"/opt/homebrew/bin:/usr/local/bin:$PATH\"; if command -v brew >/dev/null; then brew install python; else open https://www.python.org/downloads/macos/; fi",
                "if command -v apt-get >/dev/null; then sudo apt-get install python3 python3-venv; " +
                "elif command -v dnf >/dev/null; then sudo dnf install python3 python3-pip; " +
                "elif command -v pacman >/dev/null; then sudo pacman -S --needed python python-pip; " +
                "elif command -v zypper >/dev/null; then sudo zypper install python3 python3-pip; " +
                "else printf '%s\\n' 'Install Python 3.9+ with venv using your distribution package manager.'; fi");
        }

        internal static bool OpenTunnelInstaller(string linuxDirectory)
        {
            return LocalRelayControls.OpenInstallTerminal(
                "if (Get-Command winget -ErrorAction SilentlyContinue) { winget install --id Cloudflare.cloudflared --exact } else { Start-Process 'https://developers.cloudflare.com/tunnel/downloads/' }",
                "export PATH=\"/opt/homebrew/bin:/usr/local/bin:$PATH\"; if command -v brew >/dev/null; then brew install cloudflared; else open https://developers.cloudflare.com/tunnel/downloads/; fi",
                "case $(uname -m) in x86_64) arch=amd64;; aarch64|arm64) arch=arm64;; *) printf '%s\\n' 'See Cloudflare downloads for your architecture.'; exit 1;; esac; " +
                "url=\"https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-$arch\"; " +
                "dir=$1; mkdir -p \"$dir\" || exit 1; " +
                "if command -v curl >/dev/null; then curl -fsSL \"$url\" -o \"$dir/cloudflared.download\"; " +
                "elif command -v wget >/dev/null; then wget -q \"$url\" -O \"$dir/cloudflared.download\"; " +
                "else printf '%s\\n' 'Install curl or wget, then retry.'; exit 1; fi && " +
                "chmod u+x \"$dir/cloudflared.download\" && mv \"$dir/cloudflared.download\" \"$dir/cloudflared\" && printf '%s\\n' 'cloudflared installed for this project.'",
                linuxDirectory);
        }

        public static void DrawSettings(Action<string, string> testLocalRelay, bool testRunning)
        {
            const string tab = "Dreamy.LocalRelay.ShowDiscord";
            var selected = GUILayout.Toolbar(SessionState.GetBool(tab, false) ? 1 : 0, new[] { "Telegram", "Discord" });
            SessionState.SetBool(tab, selected == 1);
            var destination = selected == 1 ? "Discord" : "Telegram";
            DrawSettings(destination);
            using (new EditorGUI.DisabledScope(testRunning))
                if (GUILayout.Button("Send " + destination + " test report"))
                    testLocalRelay(GetLocalRelayUrl(destination), destination);
        }

        internal static void DrawSettings(string destination)
        {
            var script = FindScript();
            if (script == null) return;
            if (destination == "Discord")
            {
                Discord.Draw(script, Array.Empty<string>(), null);
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<TelegramRelaySettings>(AssetPath);
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty("ChatId"), new GUIContent("Telegram chat ID"));
                if (serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(settings);
            }
            Telegram.Draw(script, TelegramSettingsArguments(), settings);
        }

        private sealed class LocalRelayControls
        {
            private readonly string provider;
            private readonly int defaultPort;
            private string launchError, relayStatus;
            private MessageType relayStatusType;
            private Process credentialProcess;
            private Task<string> credentialOutput, credentialErrors;
            private double credentialDeadline;

            public LocalRelayControls(string provider, int defaultPort)
            {
                this.provider = provider;
                this.defaultPort = defaultPort;
            }

            private bool IsDiscord => provider == "Discord";
            private string PreferenceKey(string name) => "Dreamy." + provider + "." + Hash128.Compute(Application.dataPath) + "." + name;
            private int Port => EditorPrefs.GetInt(PreferenceKey("Port"), defaultPort);

            private string[] ProjectArguments() => new[]
            {
                "--project-dir", ProjectDirectory, "--port", Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--destination", IsDiscord ? "discord" : "telegram"
            };

            private string CredentialPython
            {
                get
                {
                    var environment = Path.Combine(ProjectDirectory, "Library", "DreamyDebugger", provider + "Relay", "credential-env");
                    return Application.platform == RuntimePlatform.WindowsEditor
                        ? Path.Combine(environment, "Scripts", "python.exe")
                        : Path.Combine(environment, "bin", "python");
                }
            }

            public async Task<DirectQaBuildPreset> ReadSavedProfileAsync(string script, string[] setupArgs, CancellationToken token)
            {
                var python = CredentialPython;
                if (!File.Exists(python) || token.IsCancellationRequested) return null;
                var info = new ProcessStartInfo(python)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var arg in new[] { script, "--read-saved-profile" }.Concat(ProjectArguments()).Concat(setupArgs))
                    info.ArgumentList.Add(arg);
                try
                {
                    using var process = Process.Start(info);
                    if (process == null) return null;
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var output = process.StandardOutput.ReadToEndAsync();
                    var errors = process.StandardError.ReadToEndAsync();
                    var read = Task.WhenAll(output, errors);
                    try
                    {
                        if (await Task.WhenAny(read, Task.Delay(8000, timeout.Token)) != read || token.IsCancellationRequested)
                            return null;
                        await read;
                        if (!process.HasExited && !process.WaitForExit(100)) return null;
                        if (process.ExitCode != 0 || output.Result.Length > 4096) return null;
                        var preset = JsonUtility.FromJson<DirectQaBuildPreset>(output.Result);
                        return preset?.IsValid == true && preset.provider == (IsDiscord ? 0 : 1) ? preset : null;
                    }
                    finally
                    {
                        timeout.Cancel();
                        if (!process.HasExited) process.Kill();
                    }
                }
                catch (Exception error) when (error is Win32Exception || error is InvalidOperationException ||
                    error is IOException || error is ArgumentException) { return null; }
            }

            public void StartIfEnabled(string script, string[] setupArgs)
            {
                if (!EditorPrefs.GetBool(PreferenceKey("AutoStart"), false)) return;
                var sessionKey = PreferenceKey("AutoStartAttempted");
                if (SessionState.GetBool(sessionKey, false)) return;
                SessionState.SetBool(sessionKey, true);
                RunSavedCommand(script, "--start-saved", setupArgs);
            }

            public string RelayUrl => "http://127.0.0.1:" + Port + "/reports";

            public void Draw(string script, string[] setupArgs, TelegramRelaySettings settings)
            {
                var rememberKey = PreferenceKey("Remember");
                var autoStartKey = PreferenceKey("AutoStart");
                var remember = EditorPrefs.GetBool(rememberKey, false);
                var nextRemember = EditorGUILayout.ToggleLeft(IsDiscord ? "Remember webhook on this computer" : "Remember bot token on this computer", remember);
                if (nextRemember != remember) EditorPrefs.SetBool(rememberKey, nextRemember);
                var launchArgs = IsDiscord || nextRemember ? setupArgs.Concat(ProjectArguments()).ToArray() : setupArgs;
                if (nextRemember) launchArgs = launchArgs.Append("--remember-token").ToArray();
                if (GUILayout.Button("Start " + provider + " Relay in Terminal")) OpenTerminal(script, launchArgs);
                if (!IsDiscord && GUILayout.Button("Find Chat ID in Terminal")) OpenTerminal(script, "--discover-chat");
                using (new EditorGUI.DisabledScope(credentialProcess != null))
                {
                    if (GUILayout.Button("Start Saved " + provider + " Relay")) RunSavedCommand(script, "--start-saved", setupArgs);
                    if (GUILayout.Button("Stop saved relay")) RunSavedCommand(script, "--stop-saved", Array.Empty<string>());
                }
                if (!string.IsNullOrEmpty(launchError)) EditorGUILayout.HelpBox(launchError, MessageType.Warning);
                if (!string.IsNullOrEmpty(relayStatus)) EditorGUILayout.HelpBox(relayStatus, relayStatusType);

                var advancedKey = "Dreamy." + provider + "Advanced";
                var showManual = EditorGUILayout.Foldout(SessionState.GetBool(advancedKey, false), "Manual commands and saved settings");
                SessionState.SetBool(advancedKey, showManual);
                if (!showManual) return;

                var autoStart = EditorPrefs.GetBool(autoStartKey, false);
                var nextAutoStart = EditorGUILayout.ToggleLeft("Start saved relay when Unity opens", autoStart);
                if (nextAutoStart != autoStart) EditorPrefs.SetBool(autoStartKey, nextAutoStart);
                if (nextRemember)
                    EditorGUILayout.HelpBox("First setup installs credential support under Library. The verified credential is saved in the system credential store.", MessageType.Info);

                if (GUILayout.Button("Install Python for local relay"))
                    launchError=OpenPythonInstaller()?null:"Could not open an installation terminal.";
                using (new EditorGUI.DisabledScope(credentialProcess != null))
                {
                    if (GUILayout.Button(IsDiscord ? "Forget Saved Webhook" : "Forget Saved Bot Token"))
                    {
                        EditorPrefs.SetBool(autoStartKey, false);
                        EditorPrefs.SetBool(rememberKey, false);
                        RunSavedCommand(script, "--forget-token", Array.Empty<string>());
                    }
                }
                var nextPort = Mathf.Clamp(EditorGUILayout.DelayedIntField("Saved relay port", Port), 1, 65535);
                if (nextPort != Port) EditorPrefs.SetInt(PreferenceKey("Port"), nextPort);
                if (GUILayout.Button("Copy setup command")) EditorGUIUtility.systemCopyBuffer = CopyCommand(script, launchArgs);
                if (IsDiscord) return;
                if (GUILayout.Button("Copy chat discovery command")) EditorGUIUtility.systemCopyBuffer = CopyCommand(script, new[] { "--discover-chat" });
                if (settings == null)
                {
                    if (GUILayout.Button("Create Telegram destination settings (Editor only)")) Selection.activeObject = GetOrCreate();
                }
                else if (GUILayout.Button("Select Telegram settings asset")) Selection.activeObject = settings;
            }

            private void RunSavedCommand(string script, string command, string[] setupArgs)
            {
                if (credentialProcess != null) return;
                var python = CredentialPython;
                if (!File.Exists(python) || !File.Exists(script))
                {
                    SetRelayStatus("Saved relay setup is unavailable. Enable the remember option and start setup in the terminal.", MessageType.Warning);
                    return;
                }
                var info = new ProcessStartInfo(python)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var arg in new[] { script, command }.Concat(ProjectArguments()).Concat(setupArgs))
                    info.ArgumentList.Add(arg);
                try
                {
                    credentialProcess = Process.Start(info);
                    if (credentialProcess == null) throw new InvalidOperationException();
                    credentialProcess.StandardInput.Close();
                    credentialOutput = credentialProcess.StandardOutput.ReadToEndAsync();
                    credentialErrors = credentialProcess.StandardError.ReadToEndAsync();
                    credentialDeadline = EditorApplication.timeSinceStartup + 25;
                    EditorApplication.update += PollSavedCommand;
                    AssemblyReloadEvents.beforeAssemblyReload += CancelSavedCommand;
                    EditorApplication.quitting += CancelSavedCommand;
                    SetRelayStatus(command == "--forget-token" ? "Removing the saved credential…" : "Checking local " + provider + " relay…", MessageType.Info);
                }
                catch (Exception error) when (error is Win32Exception || error is InvalidOperationException || error is IOException)
                {
                    CancelSavedCommand();
                    SetRelayStatus("Could not start the credential helper. Run relay setup again.", MessageType.Warning);
                }
            }

            private void PollSavedCommand()
            {
                if (credentialProcess.HasExited && credentialOutput.IsCompleted && credentialErrors.IsCompleted)
                {
                    var output = credentialOutput.Status == TaskStatus.RanToCompletion ? credentialOutput.Result.Trim() : "";
                    var success = credentialProcess.ExitCode == 0;
                    // The helper returns user-facing summaries on stdout. Tracebacks and OS errors stay out of UI.
                    if (string.IsNullOrEmpty(output))
                        output = success ? "Relay command finished." : "Saved relay command failed. Check the local setup and credential store.";
                    CancelSavedCommand();
                    SetRelayStatus(output, success ? MessageType.Info : MessageType.Warning);
                }
                else if (EditorApplication.timeSinceStartup >= credentialDeadline)
                {
                    CancelSavedCommand();
                    SetRelayStatus("The credential helper timed out. Unlock the system credential store and try again.", MessageType.Warning);
                }
            }

            private void CancelSavedCommand()
            {
                EditorApplication.update -= PollSavedCommand;
                AssemblyReloadEvents.beforeAssemblyReload -= CancelSavedCommand;
                EditorApplication.quitting -= CancelSavedCommand;
                if (credentialProcess == null) return;
                try { if (!credentialProcess.HasExited) credentialProcess.Kill(); }
                catch (Exception error) when (error is InvalidOperationException || error is Win32Exception) { }
                credentialProcess.Dispose();
                credentialProcess = null;
                credentialOutput = credentialErrors = null;
            }

            private void SetRelayStatus(string status, MessageType type)
            {
                relayStatus = status;
                relayStatusType = type;
                foreach (var window in Resources.FindObjectsOfTypeAll<DebuggerWindow>()) window.Repaint();
            }

            private void OpenTerminal(string script, params string[] setupArgs)
            {
                launchError = null;
                if (!File.Exists(script))
                {
                    launchError = "Relay setup script was not found in this package.";
                    return;
                }

                setupArgs = new[] { "--prompt-token" }.Concat(setupArgs).ToArray();
                try
                {
                    if (Application.platform == RuntimePlatform.WindowsEditor)
                    {
                        var pythonArguments = string.Join(" ", new[] { script }.Concat(setupArgs).Select(QuotePowerShell));
                        var command = "if (Get-Command py -ErrorAction SilentlyContinue) { py -3 " + pythonArguments +
                                      " } elseif (Get-Command python -ErrorAction SilentlyContinue) { python " + pythonArguments +
                                      " } else { Write-Host 'Install Python 3, then open Telegram setup again.' }";
                        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
                        StartProcess("powershell.exe", "-NoLogo", "-NoProfile", "-NoExit", "-EncodedCommand", encoded);
                        return;
                    }

                    const string launchScript = "export PATH=\"/opt/homebrew/bin:/usr/local/bin:$PATH\"; if command -v python3 >/dev/null 2>&1; then python3 \"$@\"; " +
                                                "else printf '%s\n' 'Install Python 3, then open Telegram setup again.'; fi; " +
                                                "printf '\nPress Enter to close...'; read -r unused";
                    var shellArgs = new[] { "/bin/sh", "-c", launchScript, "dreamy-telegram", script }.Concat(setupArgs).ToArray();
                    if (OpenPosixTerminal(shellArgs))return;

                    launchError = "Unity could not find a terminal. Use Copy setup command under Manual commands and saved settings.";
                }
                catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException || ex is IOException)
                {
                    launchError = "Unity could not open the terminal. Use Copy setup command under Manual commands and saved settings.";
                }
            }

            internal static bool OpenInstallTerminal(string windows,string mac,string linux,params string[] args)
            {
                try
                {
                    if(Application.platform==RuntimePlatform.WindowsEditor)
                    {
                        var encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes(windows));
                        StartProcess("powershell.exe","-NoLogo","-NoProfile","-NoExit","-EncodedCommand",encoded);
                        return true;
                    }
                    var command=Application.platform==RuntimePlatform.OSXEditor?mac:linux;
                    var script="( "+command+" ); result=$?; printf '\\nExit code: %s\\nPress Enter to close...' \"$result\"; read -r unused";
                    return OpenPosixTerminal(new[]{"/bin/sh","-c",script,"dreamy-install"}.Concat(args).ToArray());
                }
                catch(Exception ex) when(ex is Win32Exception || ex is InvalidOperationException || ex is IOException)
                { return false; }
            }

            private static bool OpenPosixTerminal(string[] shellArgs)
            {
                if(Application.platform==RuntimePlatform.OSXEditor)
                {
                    var shellCommand=string.Join(" ",shellArgs.Select(QuotePosix));
                    StartProcess("/usr/bin/osascript","-e","on run argv","-e","tell application \"Terminal\"",
                        "-e","activate","-e","do script item 1 of argv","-e","end tell","-e","end run",shellCommand);
                    return true;
                }
                if(Application.platform!=RuntimePlatform.LinuxEditor)return false;
                foreach(var terminal in new[]{"xdg-terminal-exec","gnome-terminal","x-terminal-emulator","konsole","xterm","kitty","alacritty"})
                {
                    var arguments=terminal=="xdg-terminal-exec"?shellArgs:
                        new[]{terminal=="gnome-terminal"?"--":"-e"}.Concat(shellArgs).ToArray();
                    try{StartProcess(terminal,arguments);return true;}
                    catch(Win32Exception ex) when(ex.NativeErrorCode==2 || ex.NativeErrorCode==13){}
                }
                return false;
            }

            private static void StartProcess(string executable, params string[] args)
            {
                var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
                foreach (var arg in args) startInfo.ArgumentList.Add(arg);
                using var process = Process.Start(startInfo);
                if (process == null) throw new InvalidOperationException("Terminal process was not launched.");
            }

            private static string CopyCommand(string script, string[] args)
            {
                var windows = Application.platform == RuntimePlatform.WindowsEditor;
                return (windows ? "py -3 " : "python3 ") +
                       string.Join(" ", new[] { script }.Concat(args).Select(value => windows ? QuotePowerShell(value) : QuotePosix(value)));
            }

            private static string QuotePowerShell(string value) => "'" + value.Replace("'", "''") + "'";
            private static string QuotePosix(string value) => "'" + value.Replace("'", "'\\''") + "'";
        }
    }
}
