#if UNITY_EDITOR
using System.Collections;
using System.Linq;
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
        [System.Flags] public enum Channels { Red = 1, Green = 2, Blue = 4 }
        public sealed class PresentationFixture : MonoBehaviour
        {
            public Color Tint = Color.red;
            public Channels Mask = (Channels)9;
            public Rigidbody Reference;
        }
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
            config.AllowSceneEditing = true;
            config.HierarchyMemberPresentation = new[]
            {
                new DebuggerConfiguration.MemberPresentation
                {
                    TypeName = typeof(PresentationFixture).FullName,
                    MemberName = nameof(PresentationFixture.Tint),
                    Label = "Preview tint",
                    Group = "Appearance",
                    Drawer = DebuggerConfiguration.HierarchyDrawer.Picker
                }
            };
            assets.Configuration = config;
            assets.Catalog = AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>("Packages/com.dreamy.debugger/Runtime/Prefabs/DebuggerAssets.asset");
            DebuggerBootstrap.AssetProvider = () => assets;
            if (DebuggerRuntime.Instance != null) Object.Destroy(DebuggerRuntime.Instance.gameObject);
            yield return null; yield return null;
            var runtime = DebuggerBootstrap.Initialize();
            var fixture = new GameObject("Runtime hierarchy fixture");
            var body = fixture.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var properties = fixture.AddComponent<PresentationFixture>();
            fixture.layer = 5;
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
                Assert.That(view.Menu.gameObject.activeSelf, Is.False);
                Assert.That(view.MemberSearch.gameObject.activeSelf, Is.False);
                view.MenuButton.onClick.Invoke();
                Assert.That(view.Menu.gameObject.activeSelf, Is.True);
                view.MenuDismiss.onClick.Invoke();
                Assert.That(view.Menu.gameObject.activeSelf, Is.False);
                var row = view.HierarchyScroll.content.GetComponentInChildren<HierarchyRowView>(); Assert.That(row, Is.Not.Null);
                var camera = view.GetComponentInParent<Canvas>().worldCamera;
                var point = RectTransformUtility.WorldToScreenPoint(camera, ((RectTransform)row.Select.transform).TransformPoint(new Vector3(100, -20)));
                var pointer = new PointerEventData(EventSystem.current) { position = point };
                var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits.Exists(hit => hit.gameObject.GetComponentInParent<HierarchyRowView>() != null), Is.True, "Hierarchy row must receive pointer raycasts");
                ExecuteEvents.Execute(row.Select.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                yield return null;
                Assert.That(view.Header.text, Does.Contain("Runtime hierarchy fixture"));
                view.Search.text = "type:Rigidbody";
                for (var i = 0; i < 4; i++) yield return null;
                Assert.That(view.HierarchyScroll.content.GetComponentsInChildren<HierarchyRowView>().Length, Is.EqualTo(1));
                view.Search.text = "layer:UI";
                for (var i = 0; i < 4; i++) yield return null;
                Assert.That(view.HierarchyScroll.content.GetComponentsInChildren<HierarchyRowView>().Length, Is.EqualTo(1));

                Assert.That(view.ObjectTag.interactable, Is.True, "Editing is enabled after unlock");
                view.ObjectTag.onClick.Invoke();
                Assert.That(view.Picker.gameObject.activeSelf, Is.True);
                view.Picker.Search.text = "Player";
                view.Picker.Rows[0].onClick.Invoke();
                Assert.That(fixture.tag, Is.EqualTo("Untagged"), "Selection remains a draft before Apply");
                view.Picker.Apply.onClick.Invoke();
                Assert.That(fixture.tag, Is.EqualTo("Player"));
                view.Search.text = "tag:Player";
                for (var i = 0; i < 4; i++) yield return null;
                Assert.That(view.HierarchyScroll.content.GetComponentsInChildren<HierarchyRowView>().Length, Is.EqualTo(1));

                view.MemberSearch.text = "Mass";
                yield return null;
                var mass = Row(view, "Mass");
                Assert.That(mass.PinButton.gameObject.activeSelf, Is.False);
                Assert.That(mass.CopyButton.gameObject.activeSelf, Is.False);
                view.MenuButton.onClick.Invoke();
                view.TogglePropertyActions.onClick.Invoke();
                yield return null;
                Assert.That(mass.PinButton.gameObject.activeSelf, Is.True);
                mass.PinButton.onClick.Invoke();
                view.MemberSearch.text = "";
                yield return null;
                Assert.That(view.InspectorScroll.content.GetComponentInChildren<InspectorRowView>().Caption.text, Is.EqualTo("Mass"));
                mass = Row(view, "Mass");
                mass.Value.text = "2.125";
                yield return null;
                Assert.That(view.DraftBar.gameObject.activeSelf, Is.True);
                Assert.That(mass.Apply.gameObject.activeSelf, Is.False);
                view.ApplyButton.onClick.Invoke();
                Assert.That(body.mass, Is.EqualTo(2.125f));
                yield return null;
                Row(view, "Mass").CopyButton.onClick.Invoke();
                Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo("2.125"));
                view.UndoButton.onClick.Invoke();
                Assert.That(body.mass, Is.EqualTo(1f));
                view.RedoButton.onClick.Invoke();
                Assert.That(body.mass, Is.EqualTo(2.125f));
                body.mass = 3;
                view.UndoButton.onClick.Invoke();
                yield return null;
                Assert.That(body.mass, Is.EqualTo(3f));
                Assert.That(view.Status.text, Does.Contain("changed"));
                Row(view, "Mass").PinButton.onClick.Invoke();

                view.MemberSearch.text = "Mass";
                yield return null;
                view.CompareButton.onClick.Invoke();
                var other = new GameObject("Comparison target");
                other.transform.SetParent(fixture.transform);
                var otherBody = other.AddComponent<Rigidbody>();
                otherBody.isKinematic = true;
                Assert.That(view.Reveal(other), Is.True);
                yield return null;
                Assert.That(Row(view, "Mass").Hint.text, Does.Contain("Runtime hierarchy fixture").And.EndsWith("3"));
                otherBody.mass = 3;
                view.RefreshButton.onClick.Invoke();
                yield return null;
                Assert.That(Row(view, "Mass").Hint.gameObject.activeSelf, Is.False);
                view.CompareButton.onClick.Invoke();
                Assert.That(view.Reveal(fixture), Is.True);
                yield return null;

                view.MemberSearch.text = "Preview tint";
                yield return null;
                Assert.That(Row(view, "Appearance"), Is.Not.Null);
                var tint = Row(view, "Preview tint");
                Assert.That(tint.Value.readOnly, Is.True);
                tint.PickerButton.onClick.Invoke();
                view.Picker.ChannelSliders[1].value = .5f;
                view.Picker.Channels[3].text = "0.75";
                Assert.That(properties.Tint, Is.EqualTo(Color.red));
                view.Picker.Apply.onClick.Invoke();
                Assert.That(properties.Tint, Is.EqualTo(new Color(1, .5f, 0, .75f)));
                view.MemberSearch.text = "Mask";
                yield return null;
                Row(view, "Mask").PickerButton.onClick.Invoke();
                view.Picker.Search.text = "Green";
                view.Picker.Rows[0].onClick.Invoke();
                view.Picker.Apply.onClick.Invoke();
                Assert.That(properties.Mask, Is.EqualTo((Channels)11), "Undeclared bits survive toggling a known flag");

                view.MemberSearch.text = "Reference";
                yield return null;
                Assert.That(Row(view, "Reference").Action.interactable, Is.False);
                Row(view, "Reference").PickerButton.onClick.Invoke();
                view.Picker.Search.text = "Comparison target";
                view.Picker.Rows[0].onClick.Invoke();
                view.Picker.Apply.onClick.Invoke();
                Assert.That(properties.Reference, Is.SameAs(otherBody));
                yield return null;
                Row(view, "Reference").Action.onClick.Invoke();
                yield return null;
                Assert.That(view.Header.text, Is.EqualTo("Comparison target"));
                Assert.That(view.Reveal(fixture), Is.True);
                yield return null;

                view.MemberSearch.text = "";
                view.MenuButton.onClick.Invoke();
                view.TogglePropertyActions.onClick.Invoke();
                yield return null;
                Assert.That(Row(view, "Position").CopyButton.gameObject.activeSelf, Is.False);
                yield return CaptureResponsiveViews(view);
                view.ObjectTag.onClick.Invoke();
                Assert.That(view.Picker.gameObject.activeSelf, Is.True);
                runtime.Relock(); yield return null;
                Assert.That(view.Picker.gameObject.activeSelf, Is.False);
                Assert.That(DreamyDebug.Instance.IsDebugPanelVisible, Is.False);
                Assert.That(view.Status.text, Is.EqualTo("Scene inspection locked"));
                Assert.That(runtime.Access.RequestPrompt(), Is.True);
                Assert.That(runtime.Access.SubmitPin("0000"), Is.EqualTo(UnlockResult.Unlocked));
                runtime.Open(DefaultTabs.Scene);
                yield return null;
                Assert.That(view.UndoButton.interactable, Is.False);
                Assert.That(view.RedoButton.interactable, Is.False);
            }
            finally
            {
                Object.Destroy(fixture); if (runtime != null) Object.Destroy(runtime.gameObject);
                DebuggerBootstrap.AssetProvider = previous; Object.Destroy(assets); Object.Destroy(config);
            }
            yield return null; yield return null;
        }

        private static InspectorRowView Row(SceneTabController view, string caption) =>
            view.InspectorScroll.content.GetComponentsInChildren<InspectorRowView>().First(item => item.Caption.text == caption);

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
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dreamy-hierarchy-v2-evidence");
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(390, 844), new Vector2Int(844, 390), new Vector2Int(1024, 768), new Vector2Int(768, 1024) })
                {
                    surface.sizeDelta = size; camera.orthographicSize = size.y / 2f; camera.aspect = (float)size.x / size.y;
                    var render = new RenderTexture(size.x, size.y, 24); camera.targetTexture = render;
                    yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    if (size == new Vector2Int(390, 844))
                    {
                        SaveRender(camera, render, folder, "simple-inspector");
                        var position = System.Linq.Enumerable.First(view.InspectorScroll.content.GetComponentsInChildren<InspectorRowView>(),
                            row => row.Caption.text == "Position");
                        var touch = new PointerEventData(EventSystem.current) { pointerId = 1, button = PointerEventData.InputButton.Left };
                        ExecuteEvents.Execute(position.AxisInputs[0].gameObject, touch, ExecuteEvents.pointerClickHandler);
                        Assert.That(view.NumberEditor.gameObject.activeSelf, Is.True);
                        yield return null;
                    }
                    // Keep the same draft through the portrait to low-landscape transition.
                    if (size == new Vector2Int(844, 390)) Assert.That(view.NumberEditor.gameObject.activeSelf, Is.True);
                    var sheet = view.NumberEditor.gameObject.activeSelf;
                    if (size == new Vector2Int(390, 844) || size == new Vector2Int(844, 390))
                    {
                        Assert.That(view.HierarchyPane.gameObject.activeSelf, Is.False);
                        Assert.That(view.BackButton.gameObject.activeSelf, Is.True);
                        Assert.That(view.NumberEditor.Axes.All(axis => !axis.gameObject.activeSelf), Is.True);
                    }
                    Canvas.ForceUpdateCanvases();
                    SaveRender(camera, render, folder, sheet ? "touch-editor" : "runtime-world-space");
                    if (size == new Vector2Int(844, 390)) view.NumberEditor.Dismiss();
                    camera.targetTexture = null; render.Release(); Object.Destroy(render);
                    Assert.That(view.InspectorScroll.content.childCount, Is.LessThan(30));
                }
                view.MemberSearch.text = "Preview tint";
                yield return null;
                Row(view, "Preview tint").PickerButton.onClick.Invoke();
                Assert.That(view.Picker.gameObject.activeSelf, Is.True);
                foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(844, 390) })
                {
                    surface.sizeDelta = size; camera.orthographicSize = size.y / 2f; camera.aspect = (float)size.x / size.y;
                    var render = new RenderTexture(size.x, size.y, 24); camera.targetTexture = render;
                    yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    var apply = (RectTransform)view.Picker.Apply.transform;
                    var corners = new Vector3[4];
                    apply.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = camera.WorldToViewportPoint(corner);
                        Assert.That(point.x, Is.InRange(0f, 1f));
                        Assert.That(point.y, Is.InRange(0f, 1f));
                    }
                    SaveRender(camera, render, folder, "color-picker");
                    camera.targetTexture = null; render.Release(); Object.Destroy(render);
                }
                view.Picker.Dismiss();
            }
            finally { Object.Destroy(cameraObject); }
        }
        private static void SaveRender(Camera camera, RenderTexture render, string folder, string view)
        {
            camera.Render();
            var old = RenderTexture.active; RenderTexture.active = render;
            var pixels = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); pixels.Apply(); RenderTexture.active = old;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, view + "-" + render.width + "x" + render.height + ".png"), pixels.EncodeToPNG());
            Object.Destroy(pixels);
        }
    }
}
#endif
