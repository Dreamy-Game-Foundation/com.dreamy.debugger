using Dreamy.Debugger.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dreamy.Debugger.Tests
{
    public sealed class ReportSetupTests
    {
        [TestCase(" https://relay.example.test/ ", "https://relay.example.test/reports")]
        [TestCase("https://relay.example.test/base/reports/", "https://relay.example.test/base/reports")]
        [TestCase("http://127.0.0.1:8766", "http://127.0.0.1:8766/reports")]
        [TestCase("https://relay.example.test/health", "https://relay.example.test/health")]
        [TestCase("https://relay.example.test/?token=fixture", "https://relay.example.test/?token=fixture")]
        [TestCase("http://relay.example.test", "http://relay.example.test")]
        public void RelaySetupAcceptsBaseAddressWithoutRewritingInvalidInput(string input, string expected)
        {
            var normalize = typeof(DebuggerWindow).GetMethod("NormalizeRelayAddress", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(normalize.Invoke(null, new object[] { input }), Is.EqualTo(expected));
        }
        [TestCase(0, "https://discord.com/api/webhooks/123456789/fixture-webhook", "")]
        [TestCase(1, "123456789:fixture_token_with_enough_characters", "-123")]
        public void EmbeddedDestinationIsStagedOnlyInAndroidDevelopment(int provider, string credential, string chatId)
        {
            var path="Assets/DirectQaPresetFixture-"+Guid.NewGuid().ToString("N")+".asset";
            var configuration=ScriptableObject.CreateInstance<DebuggerConfiguration>();
            var settings=DebuggerProjectSettings.instance;
            var previous=settings.Configuration;
            AssetDatabase.CreateAsset(configuration,path);
            settings.Configuration=configuration;
            var staged=DebuggerStartupBuild.Stage+"/Resources/"+DirectQaBuildPreset.ResourcePath+".bytes";
            try
            {
                configuration.Mode=DebuggerMode.Qa;
                configuration.ReportingMode=ReportingMode.DirectQa;
                configuration.UploadsEnabled=true;
                DirectQaBuildSetup.SetPreset(configuration,new DirectQaBuildPreset{provider=provider,credential=credential,chatId=chatId});
                DebuggerStartupBuild.Prepare(true,BuildTarget.Android);
                Assert.That(File.Exists(staged),Is.False,"Editor credentials require a separate build opt-in");
                DirectQaBuildSetup.SetIncludeInAndroidBuild(configuration,true);
                DebuggerStartupBuild.Prepare(true,BuildTarget.Android);
                var payload=JsonUtility.FromJson<DirectQaBuildProfiles>(AssetDatabase.LoadAssetAtPath<TextAsset>(staged).text).Get(provider);
                Assert.That(payload.IsValid,Is.True);
                Assert.That(payload.credential==credential && payload.chatId==chatId && payload.provider==provider,Is.True);
                Assert.That(File.ReadAllText(path).Contains(credential),Is.False);

                DirectQaBuildSetup.SetPreset(configuration,null);
                Assert.That(DirectQaBuildSetup.IncludeInAndroidBuild(configuration),Is.False);
                DirectQaBuildSetup.SetPreset(configuration,new DirectQaBuildPreset{provider=provider,credential=credential,chatId=chatId});
                DirectQaBuildSetup.SetIncludeInAndroidBuild(configuration,true);
                DebuggerStartupBuild.Prepare(true,BuildTarget.StandaloneLinux64);
                Assert.That(File.Exists(staged),Is.False);
                configuration.Mode=DebuggerMode.Off;
                DebuggerStartupBuild.Prepare(false,BuildTarget.Android);
                Assert.That(Directory.Exists(DebuggerStartupBuild.Stage),Is.False);
                configuration.Mode=DebuggerMode.ReleaseDiagnostics;
                configuration.ReportingMode=ReportingMode.LocalOnly;
                DebuggerStartupBuild.Prepare(false,BuildTarget.Android);
                Assert.That(File.Exists(staged),Is.False);
            }
            finally
            {
                DebuggerStartupBuild.Cleanup();
                DirectQaBuildSetup.SetPreset(configuration,null);
                settings.Configuration=previous;
                AssetDatabase.DeleteAsset(path);
            }
        }

        [TestCase(false, DebuggerMode.Development)]
        [TestCase(false, DebuggerMode.Qa)]
        [TestCase(true, DebuggerMode.ReleaseDiagnostics)]
        public void DirectQaCannotEnterProductionOrReleaseDiagnostics(bool development, DebuggerMode mode)
        {
            var configuration=ScriptableObject.CreateInstance<DebuggerConfiguration>();
            try { configuration.ReportingMode=ReportingMode.DirectQa;configuration.Mode=mode;
                Assert.Throws<UnityEditor.Build.BuildFailedException>(()=>DebuggerBuildVerification.ValidateReportConfiguration(configuration,UnityEditor.BuildTarget.Android,development)); }
            finally { UnityEngine.Object.DestroyImmediate(configuration); }
        }
        [Test] public void DirectQaConfigurationStoresNoProviderCredentialsAndAllowsMobileWithoutRelay()
        {
            var configuration=ScriptableObject.CreateInstance<DebuggerConfiguration>();
            try { configuration.ReportingMode=ReportingMode.DirectQa;configuration.UploadsEnabled=true;
                Assert.DoesNotThrow(()=>DebuggerBuildVerification.ValidateReportConfiguration(configuration,UnityEditor.BuildTarget.Android,true));
                foreach(var field in typeof(DebuggerConfiguration).GetFields())Assert.That(field.Name.ToLowerInvariant(),Does.Not.Contain("webhook").And.Not.Contain("bottoken").And.Not.Contain("chatid")); }
            finally { UnityEngine.Object.DestroyImmediate(configuration); }
        }
        [Test]
        public void AutomaticMobileBuildRejectsLoopbackBeforeStaging()
        {
            var settings = DebuggerProjectSettings.instance;
            var previous = settings.Configuration;
            var configuration = ScriptableObject.CreateInstance<DebuggerConfiguration>();
            try
            {
                configuration.Mode = DebuggerMode.Development;
                configuration.UploadsEnabled = true;
                configuration.RelayUrl = "http://127.0.0.1:8766/reports";
                settings.Configuration = configuration;
                Assert.Throws<UnityEditor.Build.BuildFailedException>(() => DebuggerStartupBuild.Prepare(true, UnityEditor.BuildTarget.Android));
                Assert.That(System.IO.Directory.Exists(DebuggerStartupBuild.Stage), Is.False);
            }
            finally { settings.Configuration = previous; UnityEngine.Object.DestroyImmediate(configuration); }
        }

        [TestCase(UnityEditor.BuildTarget.Android, "http://127.0.0.1:8766/reports", true, false)]
        [TestCase(UnityEditor.BuildTarget.iOS, "https://localhost/reports", true, false)]
        [TestCase(UnityEditor.BuildTarget.Android, "", true, false)]
        [TestCase(UnityEditor.BuildTarget.Android, "https://qa.example.com/reports?key=test", true, false)]
        [TestCase(UnityEditor.BuildTarget.Android, "https://qa.example.com/reports", true, true)]
        [TestCase(UnityEditor.BuildTarget.iOS, "https://qa.example.com/reports", true, true)]
        [TestCase(UnityEditor.BuildTarget.Android, "http://127.0.0.1:8766/reports", false, true)]
        [TestCase(UnityEditor.BuildTarget.StandaloneWindows64, "http://127.0.0.1:8766/reports", true, true)]
        public void BuildGateRejectsEnabledMobileUploadsToUnusableEndpoint(UnityEditor.BuildTarget target, string url, bool uploads, bool valid)
        {
            var configuration = ScriptableObject.CreateInstance<DebuggerConfiguration>();
            try
            {
                configuration.Mode = DebuggerMode.Development;
                configuration.UploadsEnabled = uploads;
                configuration.RelayUrl = url;
                if (valid) Assert.DoesNotThrow(() => DebuggerBuildVerification.ValidateReportConfiguration(configuration, target));
                else Assert.Throws<UnityEditor.Build.BuildFailedException>(() => DebuggerBuildVerification.ValidateReportConfiguration(configuration, target));
            }
            finally { UnityEngine.Object.DestroyImmediate(configuration); }
        }

        [Test]
        public async Task ManagedRelayResolutionRetainsIngestKey()
        {
            var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            var port = ((IPEndPoint)socket.LocalEndpoint).Port;
            socket.Stop();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Start();
                var serve = Task.Run(async () =>
                {
                    var context = await listener.GetContextAsync();
                    var body = Encoding.UTF8.GetBytes("{\"service\":\"dreamy.debugger\",\"fakeDelivery\":false,\"supportsFullTest\":true,\"localTelegramProfile\":\"test-project\",\"ingestKeyRequired\":true}");
                    context.Response.ContentLength64 = body.Length;
                    await context.Response.OutputStream.WriteAsync(body, 0, body.Length);
                    context.Response.Close();
                });
                var resolve = typeof(DebuggerWindow).GetMethod("ResolveConfiguredTestAsync", BindingFlags.NonPublic | BindingFlags.Static);
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var task = (Task)resolve.Invoke(null, new object[] { new Uri("http://127.0.0.1:" + port + "/reports/telegram-test"), "limited-ingest-key", "Telegram", timeout.Token });
                    await task;
                    var result = task.GetType().GetProperty("Result").GetValue(task);
                    Assert.That(result.GetType().GetField("Item2").GetValue(result), Is.EqualTo("limited-ingest-key"));
                }
                await serve;
            }
        }

        [TestCase("https://qa.example.com/reports", true)]
        [TestCase("https://qa.example.com/base/reports/", true)]
        [TestCase("http://127.0.0.1:8766/reports", false)]
        [TestCase("https://localhost/reports", false)]
        [TestCase("https://[::1]/reports", false)]
        [TestCase("http://qa.example.com/reports", false)]
        [TestCase("https://user:password@qa.example.com/reports", false)]
        [TestCase("https://qa.example.com/reports?token=example", false)]
        [TestCase("https://qa.example.com/reports#example", false)]
        [TestCase("https://qa.example.com/health", false)]
        [TestCase("", false)]
        public void MobileSetupRejectsLoopbackAndCredentials(string url, bool expected)
        { Assert.That(ReportHttpsSetup.TryMobileRelay(url, out _), Is.EqualTo(expected)); }

        [Test] public void ReportsMenuAndTabUseTheSamePageIndex()
        {
            Assert.That(DebuggerWindow.PageLabels[4], Is.EqualTo("Reports"));
            Assert.That(DebuggerWindow.PageLabels[2], Is.EqualTo("Cheats"));
            Assert.That(DebuggerWindow.PageLabels[5], Is.EqualTo("Layout"));
        }
        [Test] public void InstallGuidanceMatchesHostAndDoesNotInstallAutomatically()
        {
            StringAssert.Contains("winget install", ReportHttpsSetup.InstallCommand(RuntimePlatform.WindowsEditor));
            StringAssert.Contains("brew install", ReportHttpsSetup.InstallCommand(RuntimePlatform.OSXEditor));
            StringAssert.StartsWith("https://developers.cloudflare.com/", ReportHttpsSetup.InstallCommand(RuntimePlatform.LinuxEditor));
        }
    }
}
