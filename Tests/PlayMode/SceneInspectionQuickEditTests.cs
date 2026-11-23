#if UNITY_EDITOR
using System.Collections;
using Dreamy.Debugger.SceneInspection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Dreamy.Debugger.Tests
{
    public sealed class SceneInspectionQuickEditTests
    {
        public sealed class Values : ScriptableObject { public float Speed = 2; public Vector3 Position = new Vector3(1,2,3); }

        [UnityTest]
        public IEnumerator AxisDraftsAndLabelDragRespectConflictAndAccess()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<InspectorRowView>("Packages/com.dreamy.debugger/Runtime/Prefabs/SceneInspection/InspectorValueRow.prefab");
            var row = Object.Instantiate(prefab); var values = ScriptableObject.CreateInstance<Values>();
            var editable = true;
            var eventObject = EventSystem.current == null ? new GameObject("Quick edit EventSystem", typeof(EventSystem)) : null;
            var eventSystem = eventObject != null ? eventObject.GetComponent<EventSystem>() : EventSystem.current;
            try
            {
                var vector = new InspectorRowView.Entry { Label="Position", Member=InspectionMember.Field(values, typeof(Values).GetField("Position")) };
                row.Bind(vector, () => editable);
                row.AxisInputs[0].text = "1.25"; row.Apply.onClick.Invoke();
                Assert.That(values.Position, Is.EqualTo(new Vector3(1.25f,2,3)));
                row.AxisInputs[0].text = "7"; values.Position = new Vector3(1.25f,8,3); row.Apply.onClick.Invoke();
                Assert.That(values.Position.y, Is.EqualTo(8)); StringAssert.Contains("changed", vector.Error);
                row.CancelDraft();
                var scalar = new InspectorRowView.Entry { Label="Speed", Member=InspectionMember.Field(values, typeof(Values).GetField("Speed")) };
                row.Bind(scalar, () => editable);
                var pointer = new PointerEventData(eventSystem) { pressPosition=Vector2.zero, position=new Vector2(20,0), delta=new Vector2(20,0) };
                var handle = row.LabelHandle.gameObject;
                ExecuteEvents.Execute(handle, pointer, ExecuteEvents.initializePotentialDrag);
                ExecuteEvents.Execute(handle, pointer, ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute(handle, pointer, ExecuteEvents.dragHandler);
                Assert.That(values.Speed, Is.EqualTo(2), "Gesture previews before release");
                ExecuteEvents.Execute(handle, pointer, ExecuteEvents.endDragHandler);
                Assert.That(values.Speed, Is.EqualTo(2.4f).Within(.00001f));
                ExecuteEvents.Execute(handle, pointer, ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute(handle, pointer, ExecuteEvents.dragHandler);
                editable=false; row.Refresh();
                ExecuteEvents.Execute(handle, pointer, ExecuteEvents.endDragHandler);
                Assert.That(values.Speed, Is.EqualTo(2.4f).Within(.00001f));
                Assert.That(scalar.Drafting, Is.False);
            }
            finally { Object.Destroy(row.gameObject); Object.Destroy(values); if (eventObject != null) Object.Destroy(eventObject); }
            yield return null;
        }

        [Test]
        public void IntegerScrubbingPreservesLargeValuesAndRejectsOverflow()
        {
            Assert.That(InspectionNumbers.TryOffset(9007199254740993L, -1, 6, out var result, out _), Is.True);
            Assert.That(result, Is.EqualTo(9007199254740994L));
            Assert.That(InspectionNumbers.TryOffset(long.MaxValue, -1, 6, out _, out _), Is.False);
            Assert.That(InspectionNumbers.TryOffset(0UL, -1, -6, out _, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator TouchNumberSheetAppliesOneDraftAndRetainsOtherAxes()
        {
            const string root = "Packages/com.dreamy.debugger/Runtime/Prefabs/SceneInspection/";
            var tab = AssetDatabase.LoadAssetAtPath<SceneTabController>(root + "SceneTab.prefab");
            var editor = Object.Instantiate(tab.NumberEditor);
            var row = Object.Instantiate(tab.InspectorTemplate);
            var values = ScriptableObject.CreateInstance<Values>();
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 1, button = PointerEventData.InputButton.Left };
            var allowed = true;
            try
            {
                row.TouchEditor = editor;
                row.Bind(new InspectorRowView.Entry
                {
                    Label = "Position",
                    Member = InspectionMember.Field(values, typeof(Values).GetField(nameof(Values.Position)))
                }, () => allowed);
                var hit = row.AxisInputs[0].gameObject;
                ExecuteEvents.Execute(hit, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(editor.gameObject.activeSelf, Is.True);
                editor.Number.text = "-2.5";
                Assert.That(editor.Title.text, Is.EqualTo("Position · X"));
                Assert.That(editor.Axes[1].gameObject.activeSelf, Is.False);
                Assert.That(values.Position, Is.EqualTo(new Vector3(1, 2, 3)));
                editor.Apply.onClick.Invoke();
                Assert.That(values.Position.x, Is.EqualTo(-2.5f));
                Assert.That(values.Position.y, Is.EqualTo(2f));
                ExecuteEvents.Execute(row.AxisInputs[1].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                editor.Plus.onClick.Invoke();
                editor.Apply.onClick.Invoke();
                Assert.That(values.Position.y, Is.EqualTo(2.1f).Within(.00001f));
                Assert.That(values.Position.z, Is.EqualTo(3));
                ExecuteEvents.Execute(hit, pointer, ExecuteEvents.pointerClickHandler);
                editor.Number.text = "7";
                allowed = false;
                yield return null;
                Assert.That(editor.gameObject.activeSelf, Is.False);
                Assert.That(values.Position.x, Is.EqualTo(-2.5f));
            }
            finally { Object.Destroy(editor.gameObject); Object.Destroy(row.gameObject); Object.Destroy(values); }
        }

        [Test]
        public void TagAndLayerWritesAreScopedAndRecheckAccessAndConflict()
        {
            var parent = new GameObject("Quick edit target"); var child = new GameObject("Child"); child.transform.SetParent(parent.transform);
            try
            {
                var tag = InspectionMember.Adapter(parent,"tag",true); var layer = InspectionMember.Adapter(parent,"layer",true);
                Assert.That(tag.TryCommit("Untagged","Player",()=>true,out _), Is.True);
                Assert.That(parent.tag, Is.EqualTo("Player")); Assert.That(child.tag, Is.EqualTo("Untagged"));
                Assert.That(layer.TryCommit(0,2,()=>false,out _), Is.False);
                Assert.That(layer.TryCommit(0,2,()=>true,out _), Is.True);
                Assert.That(parent.layer, Is.EqualTo(2)); Assert.That(child.layer, Is.EqualTo(0));
                Assert.That(layer.TryCommit(0,5,()=>true,out _), Is.False);
                Assert.That(parent.layer, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
#endif
