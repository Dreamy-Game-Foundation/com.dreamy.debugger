using System;
using System.Collections;
using Dreamy.Debugger.Access;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dreamy.Debugger.Sample.Tests
{
    public sealed class HostLifecycleTests
    {
        private GameObject gameObject;
        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var host in UnityEngine.Object.FindObjectsByType<ProjectCheatHost>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(host.gameObject);
            if (DebuggerRuntime.Instance != null) UnityEngine.Object.Destroy(DebuggerRuntime.Instance.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
            foreach (var host in UnityEngine.Object.FindObjectsByType<ProjectCheatHost>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(host.gameObject);
            if (DebuggerRuntime.Instance != null) UnityEngine.Object.Destroy(DebuggerRuntime.Instance.gameObject);
            yield return null;
        }
        private ProjectCheatHost Create() { gameObject = new GameObject("Dreamy Debugger lifecycle test"); return gameObject.AddComponent<ProjectCheatHost>(); }
        private void Unlock(ProjectCheatHost host)
        {
            for (var i=0;i<5;i++) host.ForwardTap();
            Assert.That(host.SubmitPin("0000"),Is.EqualTo(UnlockResult.Unlocked));
        }
        private static void InstallSample()
        {
            typeof(ProjectCheatHost).GetMethod("InstallSample",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        }

        [UnityTest] public IEnumerator ImportedSampleWaitsForRuntimeAndRebindsAfterRestart()
        {
            InstallSample(); InstallSample();
            var hosts = UnityEngine.Object.FindObjectsByType<ProjectCheatHost>(FindObjectsSortMode.None);
            Assert.That(hosts.Length, Is.EqualTo(1));
            var host = hosts[0];
            Assert.That(host.Registry, Is.Null);
            gameObject = new GameObject("Late sample runtime");
            var runtime = gameObject.AddComponent<DebuggerRuntime>();
            yield return null;
            Assert.That(host.Registry, Is.SameAs(runtime.Cheats));
            Assert.That(runtime.Cheats.Snapshot().Count, Is.EqualTo(8));
            var old = runtime.Cheats;
            runtime.enabled = false;
            yield return null;
            Assert.That(old.Snapshot().Count, Is.Zero);
            runtime.enabled = true;
            yield return null;
            Assert.That(host.Registry, Is.SameAs(runtime.Cheats));
            Assert.That(host.Registry, Is.Not.SameAs(old));
            Assert.That(runtime.Cheats.Snapshot().Count, Is.EqualTo(8));
            host.enabled = false;
            Assert.That(runtime.Cheats.Snapshot().Count, Is.Zero);
        }

        [UnityTest] public IEnumerator AuthoredInactiveHostPreventsAutomaticDuplicate()
        {
            gameObject = new GameObject("Authored sample host");
            gameObject.SetActive(false);
            gameObject.AddComponent<ProjectCheatHost>();
            InstallSample();
            Assert.That(UnityEngine.Object.FindObjectsByType<ProjectCheatHost>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator DisableDisposesOptionsAndRevokesOldRegistry()
        {
            var host=Create(); Unlock(host); var old=host.Registry;
            old.Execute("economy.grant", true); Assert.That(host.Cheats.Coins,Is.EqualTo(100));
            host.enabled=false;
            Assert.That(old.Snapshot().Count,Is.Zero);
            Assert.Throws<UnauthorizedAccessException>(()=>old.Execute("economy.grant", true));
            Assert.That(host.ForwardTap(),Is.False);
            host.enabled=true; Unlock(host);
            Assert.That(host.Registry.Snapshot().Count,Is.EqualTo(8));
            Assert.That(old.Snapshot().Count,Is.Zero);
            Assert.That(host.Cheats.Coins,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator DestroyRevokesRetainedRegistryAndRemovesOptions()
        {
            var host=Create(); Unlock(host); var registry=host.Registry;
            UnityEngine.Object.Destroy(gameObject); yield return null;
            Assert.That(registry.Snapshot().Count,Is.Zero);
            Assert.Throws<UnauthorizedAccessException>(()=>registry.Execute("economy.grant", true));
        }
        [UnityTest] public IEnumerator UnscaledAccessWorksWhileGameIsPaused()
        {
            var host=Create(); var previous=Time.timeScale;
            try { Time.timeScale=0; Unlock(host); Assert.That(host.Access.IsUnlocked,Is.True); }
            finally { Time.timeScale=previous; }
            yield return null;
        }
    }
}
