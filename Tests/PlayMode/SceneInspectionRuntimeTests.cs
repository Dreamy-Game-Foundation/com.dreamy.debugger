using System.Collections;
using System.Collections.Generic;
using Dreamy.Debugger.SceneInspection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Dreamy.Debugger.Tests
{
    public sealed class SceneInspectionRuntimeTests
    {
        [UnityTest] public IEnumerator IndexTracksInactiveRenameReparentDestroyAndDdolUsingPublicSceneApi()
        {
            var scene = SceneManager.CreateScene("Inspection fixture");
            var root = new GameObject("Fixture root"); SceneManager.MoveGameObjectToScene(root, scene);
            var child = new GameObject("Needle inactive"); child.transform.SetParent(root.transform); child.SetActive(false);
            var persistent = new GameObject("Inspection DDOL fixture"); Object.DontDestroyOnLoad(persistent);
            var persistentGame = new GameObject("DDOL game object"); Object.DontDestroyOnLoad(persistentGame);
            var selected = child;
            using (var session = new SceneInspectionSession(persistent.scene, () => true, () => true, go => go == persistent, .1f, 5))
            {
                double tick = 0;
                for (var i = 0; i < 100; i++) { session.Tick(tick += .2); if (!session.IsScanning) break; yield return null; }
                var rows = new List<int>(); session.Flatten(rows, "needle", scene.handle, false);
                Assert.That(rows.Count, Is.EqualTo(2), "Search preserves ancestor context for inactive children");
                Assert.That(session.Nodes[rows[1]].Target, Is.EqualTo(child));
                session.Flatten(rows, "DDOL game object", persistent.scene.handle, false); Assert.That(rows.Count, Is.EqualTo(1));
                session.Select(child); Assert.That(session.CanEdit, Is.True);
                child.name = "Renamed"; child.transform.SetParent(null); SceneManager.MoveGameObjectToScene(child, scene);
                session.Refresh();
                for (var i = 0; i < 100; i++) { session.Tick(tick += .2); if (!session.IsScanning) break; yield return null; }
                session.Flatten(rows, "renamed", scene.handle, false); Assert.That(rows.Count, Is.EqualTo(1));
                Assert.That(session.Selected, Is.EqualTo(selected));
                session.Suspend(); Assert.That(session.CanEdit, Is.True);
                Object.Destroy(child); yield return null; session.Tick(tick += .2); Assert.That(session.Selected, Is.Null);
            }
            Object.Destroy(persistent); Object.Destroy(persistentGame); yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator AccessExpiryAndPauseDoNotKeepEditingOrScanAfterSuspend()
        {
            var root = new GameObject("Policy fixture"); var unlocked = true;
            using (var session = new SceneInspectionSession(default(Scene), () => unlocked, () => unlocked, go => false))
            {
                Assert.That(session.CanEdit, Is.True);
                unlocked = false; session.Tick(1); Assert.That(session.CanEdit, Is.False); Assert.That(session.IsScanning, Is.False);
                unlocked = true; Assert.That(session.CanEdit, Is.True, "Authorized sessions edit immediately after unlock");
                session.Paused = true; var revision = session.Revision; session.Tick(2); Assert.That(session.Revision, Is.EqualTo(revision));
            }
            Object.Destroy(root); yield return null;
        }
    }
}
