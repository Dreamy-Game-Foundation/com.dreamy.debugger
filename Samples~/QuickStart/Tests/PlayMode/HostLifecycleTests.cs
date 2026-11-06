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
        [UnitySetUp] public IEnumerator SetUp(){if(DebuggerRuntime.Instance!=null)UnityEngine.Object.Destroy(DebuggerRuntime.Instance.gameObject);yield return null;yield return null;}

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
            yield return null;
        }
        private ProjectCheatHost Create() { gameObject = new GameObject("Dreamy Debugger lifecycle test"); return gameObject.AddComponent<ProjectCheatHost>(); }
        private void Unlock(ProjectCheatHost host)
        {
            for (var i=0;i<5;i++) host.ForwardTap();
            Assert.That(host.SubmitPin("0000"),Is.EqualTo(UnlockResult.Unlocked));
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
