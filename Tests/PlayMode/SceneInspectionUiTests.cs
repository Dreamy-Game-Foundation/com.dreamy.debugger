#if UNITY_EDITOR
using System.Collections;
using Dreamy.Debugger.Access;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.SceneInspection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dreamy.Debugger.Tests
{
    public sealed class SceneInspectionUiTests
    {
        public enum Choice { First, Second }
        public sealed class Values : ScriptableObject { public long Number = 7; public bool Flag; public Choice Mode; }
        [UnityTest] public IEnumerator TypedChoicesRefreshWithoutSyntheticWritesAndRecheckAccess()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<InspectorRowView>("Packages/com.dreamy.debugger/Runtime/Prefabs/SceneInspection/InspectorValueRow.prefab");
            var row = Object.Instantiate(prefab); var values = ScriptableObject.CreateInstance<Values>(); var editable = true;
            try
            {
                var entry = new InspectorRowView.Entry { Label = "Flag", Member = InspectionMember.Field(values, typeof(Values).GetField("Flag")) };
                row.Bind(entry, () => editable); Assert.That(row.Boolean.isOn, Is.False);
                values.Flag = true; row.Refresh(); Assert.That(row.Boolean.isOn, Is.True); Assert.That(entry.Error, Is.Null);
                row.Boolean.isOn = false; Assert.That(values.Flag, Is.False);
                editable = false; row.Boolean.isOn = true; Assert.That(values.Flag, Is.False, "Late UI events cannot bypass access");
                editable = true;
                entry = new InspectorRowView.Entry { Label = "Mode", Member = InspectionMember.Field(values, typeof(Values).GetField("Mode")) };
                row.Bind(entry, () => editable); row.Enumeration.value = 1; Assert.That(values.Mode, Is.EqualTo(Choice.Second));
                values.Mode = (Choice)99; row.Refresh();
                Assert.That(row.Enumeration.options[row.Enumeration.value].text, Does.Contain("Unknown"));
                Assert.That(values.Mode, Is.EqualTo((Choice)99));
            }
            finally { Object.Destroy(row.gameObject); Object.Destroy(values); }
            yield return null;
        }
        [UnityTest] public IEnumerator DraftBlurConflictApplyAndRevocationPreserveAuthoritativeValue()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<InspectorRowView>("Packages/com.dreamy.debugger/Runtime/Prefabs/SceneInspection/InspectorValueRow.prefab");
            var row = Object.Instantiate(prefab); var values = ScriptableObject.CreateInstance<Values>();
            var editable = true; var previous = InputBridge.Pressed; InputBridge.Pressed = key => false;
            try
            {
                var entry = new InspectorRowView.Entry { Label = "Number", Member = InspectionMember.Field(values, typeof(Values).GetField("Number")) };
                row.Bind(entry, () => editable);
                row.Value.text = "99"; row.Value.onEndEdit.Invoke(row.Value.text);
                Assert.That(values.Number, Is.EqualTo(7)); Assert.That(entry.Drafting, Is.True, "Blur does not commit");
                values.Number = 8; row.Refresh(); Assert.That(row.Value.text, Is.EqualTo("99"), "Refresh must preserve draft");
                row.Apply.onClick.Invoke(); Assert.That(values.Number, Is.EqualTo(8)); StringAssert.Contains("changed", entry.Error);
                row.Cancel.onClick.Invoke(); Assert.That(row.Value.text, Is.EqualTo("8"));
                row.Value.text = long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                row.Apply.onClick.Invoke(); Assert.That(values.Number, Is.EqualTo(long.MaxValue));
                row.Value.text = "12"; editable = false; row.Refresh();
                Assert.That(entry.Drafting, Is.False); Assert.That(values.Number, Is.EqualTo(long.MaxValue));
                editable = true; row.Refresh(); row.Value.text = "15";
                InputBridge.Pressed = key => key == KeyCode.Return; row.Value.onEndEdit.Invoke(row.Value.text);
                Assert.That(values.Number, Is.EqualTo(15), "Desktop Enter explicitly commits");
            }
            finally { InputBridge.Pressed = previous; Object.Destroy(row.gameObject); Object.Destroy(values); }
            yield return null;
        }
        [UnityTest] public IEnumerator SceneTabOpensOncePoolsRowsAndRespectsRelock()
        {
            var previous = DebuggerBootstrap.AssetProvider;
            var assets = ScriptableObject.CreateInstance<DebuggerStartupAssets>();
            var config = ScriptableObject.CreateInstance<DebuggerConfiguration>(); config.PersistRuntimePreferences = false;
            assets.Configuration = config;
            assets.Catalog = AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>("Packages/com.dreamy.debugger/Runtime/Prefabs/DebuggerAssets.asset");
            DebuggerBootstrap.AssetProvider = () => assets;
            if (DebuggerRuntime.Instance != null) Object.Destroy(DebuggerRuntime.Instance.gameObject);
            yield return null; yield return null;
            var runtime = DebuggerBootstrap.Initialize();
            var fixture = new GameObject("Runtime hierarchy fixture");
            for (var i = 0; i < 120; i++) { var child = new GameObject("Child " + i); child.transform.SetParent(fixture.transform); }
            try
            {
                Assert.That(runtime.Access.RequestPrompt(), Is.True);
                Assert.That(runtime.Access.SubmitPin("0000"), Is.EqualTo(UnlockResult.Unlocked));
                runtime.Open(DefaultTabs.Scene);
                for (var i = 0; i < 20; i++) yield return null;
                var views = Object.FindObjectsByType<SceneTabController>(FindObjectsSortMode.None);
                Assert.That(views.Length, Is.EqualTo(1)); var view = views[0];
                Assert.That(view.gameObject.activeInHierarchy, Is.True);
                view.Search.text = "Child";
                for (var i = 0; i < 20; i++) yield return null;
                Assert.That(view.HierarchyScroll.content.childCount, Is.LessThan(50), "UI pool should follow viewport size, not object count");
                Assert.That(view.EditLabel.text, Is.EqualTo("Read only")); Assert.That(view.EditButton.interactable, Is.False);
                var row = view.HierarchyScroll.content.GetComponentInChildren<HierarchyRowView>(); Assert.That(row, Is.Not.Null);
                var camera = view.GetComponentInParent<Canvas>().worldCamera;
                var point = RectTransformUtility.WorldToScreenPoint(camera, ((RectTransform)row.Select.transform).TransformPoint(new Vector3(100, -20)));
                var pointer = new PointerEventData(EventSystem.current) { position = point };
                var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits.Exists(hit => hit.gameObject.GetComponentInParent<HierarchyRowView>() != null), Is.True, "Hierarchy row must receive pointer raycasts");
                ExecuteEvents.Execute(row.Select.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                yield return null;
                Assert.That(view.Header.text, Does.Contain("Runtime hierarchy fixture"));
                yield return CaptureResponsiveViews(view);
                runtime.Relock(); yield return null;
                Assert.That(DreamyDebug.Instance.IsDebugPanelVisible, Is.False);
                Assert.That(view.Status.text, Is.EqualTo("Scene inspection locked"));
            }
            finally
            {
                Object.Destroy(fixture); if (runtime != null) Object.Destroy(runtime.gameObject);
                DebuggerBootstrap.AssetProvider = previous; Object.Destroy(assets); Object.Destroy(config);
            }
            yield return null; yield return null;
        }

        private static IEnumerator CaptureResponsiveViews(SceneTabController view)
        {
            // Real runtime UI in the package's public world-space mode. These are camera renders,
            // not Device Simulator/Game View or physical-device evidence.
            var surface = DreamyDebug.Instance.EnableWorldSpaceMode();
            surface.position = Vector3.zero; surface.localScale = Vector3.one;
            var cameraObject = new GameObject("Runtime inspection capture camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Dreamy.Debugger.Foundation.UI.DebuggerPalette.Background;
            var folder = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "SceneInspectionEvidence");
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(390, 844), new Vector2Int(1024, 768) })
                {
                    surface.sizeDelta = size; camera.orthographicSize = size.y / 2f; camera.aspect = (float)size.x / size.y;
                    var render = new RenderTexture(size.x, size.y, 24); camera.targetTexture = render;
                    yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    camera.Render();
                    var old = RenderTexture.active; RenderTexture.active = render;
                    var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); pixels.Apply(); RenderTexture.active = old;
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "runtime-world-space-" + size.x + "x" + size.y + ".png"), pixels.EncodeToPNG());
                    Object.Destroy(pixels); camera.targetTexture = null; render.Release(); Object.Destroy(render);
                    Assert.That(view.InspectorScroll.content.childCount, Is.LessThan(30));
                }
            }
            finally { Object.Destroy(cameraObject); }
        }
    }
}
#endif
