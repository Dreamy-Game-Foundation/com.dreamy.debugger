using System;
using System.Collections.Generic;
using System.Linq;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.SceneInspection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreamy.Debugger.Tests
{
    public sealed class SceneInspectionTests
    {
        [Serializable] public struct Leaf { public long Number; }
        [Serializable] public struct Nested { public Leaf Leaf; }
        public sealed class Fixture : ScriptableObject
        {
            public Nested Nested;
            public List<Nested> Items = new List<Nested> { new Nested { Leaf = new Leaf { Number = 7 } } };
            public Nested[] Array = { new Nested { Leaf = new Leaf { Number = 9 } } };
            [SerializeField] private int serialized = 3;
            [NonSerialized] public int Transient;
            [HideInInspector] public int Hidden;
            public readonly int Readonly = 4;
            public static int Static;
            public int ThrowingProperty => throw new InvalidOperationException("Must not invoke");
            public int Serialized => serialized;
        }
        [Flags] public enum Flags : ulong { A = 1, High = 1UL << 63 }
        private Fixture fixture;
        [SetUp] public void SetUp() { fixture = ScriptableObject.CreateInstance<Fixture>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(fixture); }
        private InspectionMember Member(string name) => InspectionMember.Field(fixture, typeof(Fixture).GetField(name));
        private static InspectionMember Number(InspectionMember member) => member.Child(typeof(Nested).GetField("Leaf")).Child(typeof(Leaf).GetField("Number"));

        [Test] public void WritesNestedStructBackToOwner()
        {
            var member = Number(Member("Nested"));
            Assert.That(member.TryCommit(0L, long.MaxValue, () => true, out var error), Is.True, error);
            Assert.That(fixture.Nested.Leaf.Number, Is.EqualTo(long.MaxValue));
        }
        [TestCase(false)] [TestCase(true)] public void WritesCollectionStructBackToItsSlot(bool array)
        {
            var member = Number(Member(array ? "Array" : "Items").Element(0, typeof(Nested)));
            Assert.That(member.TryRead(out var baseline, out _), Is.True);
            Assert.That(member.TryCommit(baseline, 1234567890123456789L, () => true, out var error), Is.True, error);
            Assert.That(array ? fixture.Array[0].Leaf.Number : fixture.Items[0].Leaf.Number, Is.EqualTo(1234567890123456789L));
        }
        [Test] public void RejectsConflictAndExpiredAccessWithoutWriting()
        {
            var member = Number(Member("Nested"));
            Assert.That(member.TryCommit(1L, 10L, () => true, out var conflict), Is.False);
            StringAssert.Contains("changed", conflict);
            Assert.That(member.TryCommit(0L, 10L, () => false, out _), Is.False);
            Assert.That(fixture.Nested.Leaf.Number, Is.Zero);
        }
        [Test] public void RejectsReadonlyAndDestroyedOwners()
        {
            Assert.That(Member("Readonly").TryCommit(4, 5, () => true, out _), Is.False);
            var member = Member("Nested"); Object.DestroyImmediate(fixture);
            Assert.That(member.TryRead(out _, out var error), Is.False); StringAssert.Contains("destroyed", error);
        }
        [Test] public void EligibilityDoesNotInvokePropertiesOrExposeHiddenFields()
        {
            var names = new InspectionDescriptors().Fields(typeof(Fixture)).Select(f => f.Name).ToArray();
            CollectionAssert.Contains(names, "serialized"); CollectionAssert.Contains(names, "Readonly");
            CollectionAssert.DoesNotContain(names, "Transient"); CollectionAssert.DoesNotContain(names, "Hidden");
            CollectionAssert.DoesNotContain(names, "Static"); CollectionAssert.DoesNotContain(names, "ThrowingProperty");
            Assert.That(fixture.Serialized, Is.EqualTo(3));
        }
        [TestCase("9223372036854775807", typeof(long))]
        [TestCase("18446744073709551615", typeof(ulong))]
        [TestCase("1.2345678901234567", typeof(double))]
        [TestCase("79228162514264337593543950335", typeof(decimal))]
        public void NumericPrecisionSurvivesRoundTrip(string input, Type type)
        {
            Assert.That(InspectionValues.TryParse(input, type, out var value, out var error), Is.True, error);
            Assert.That(InspectionValues.Format(value), Is.EqualTo(input));
        }
        [TestCase("9223372036854775808", typeof(long))]
        [TestCase("NaN", typeof(float))]
        [TestCase("Infinity", typeof(double))]
        [TestCase("1,NaN,2", typeof(Vector3))]
        [TestCase("2147483648,0", typeof(Vector2Int))]
        public void InvalidNumbersNeverCommit(string input, Type type) => Assert.That(InspectionValues.TryParse(input, type, out _, out _), Is.False);
        [Test] public void FlagsPreserveHighUnsignedBits()
        {
            Assert.That(InspectionValues.TryParse("A, High", typeof(Flags), out var value, out _), Is.True);
            Assert.That(value, Is.EqualTo(Flags.A | Flags.High));
        }
        [Test] public void BudgetedSearchCancelsOldQueryAndKeepsAncestorContext()
        {
            var root = new GameObject("Projection fixture");
            try
            {
                for (var i = 0; i < 2000; i++) new GameObject(i == 1999 ? "Second match" : "First " + i).transform.SetParent(root.transform);
                using (var session = new SceneInspectionSession(default(UnityEngine.SceneManagement.Scene), () => true, () => false,
                    go => go != root && !go.transform.IsChildOf(root.transform), .1f, 5))
                {
                    for (var i = 0; i < 100; i++) { session.Tick(i); if (!session.IsScanning) break; }
                    var projection = new HierarchyProjection(); projection.Begin(session, "First", null, false);
                    projection.Tick(.1); Assert.That(projection.IsBuilding, Is.True);
                    var generation = projection.Generation; projection.Begin(session, "Second", null, false);
                    Assert.That(projection.Generation, Is.GreaterThan(generation));
                    for (var i = 0; i < 100 && projection.IsBuilding; i++) projection.Tick(5);
                    Assert.That(projection.IsBuilding, Is.False); Assert.That(projection.Result.Count, Is.EqualTo(2));
                    Assert.That(projection.Result[0].Target, Is.EqualTo(root)); Assert.That(projection.Result[1].Name, Is.EqualTo("Second match"));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test] public void CatalogShipsBoundSceneAndRowPrefabsWithoutChangingOldEnumValues()
        {
            Assert.That((int)DefaultTabs.Screenshot, Is.EqualTo(5)); Assert.That((int)DefaultTabs.Scene, Is.EqualTo(6));
            var catalog = AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>("Packages/com.dreamy.debugger/Runtime/Prefabs/DebuggerAssets.asset");
            Assert.That(catalog.Entries.Count(e => e.Key == "DreamyDebugger/UI/Prefabs/Tabs/Scene"), Is.EqualTo(1));
            var prefab = catalog.Entries.Single(e => e.Key == "DreamyDebugger/UI/Prefabs/Tabs/Scene").Asset as GameObject;
            var view = prefab.GetComponent<SceneTabController>(); Assert.That(view, Is.Not.Null);
            Assert.That(view.HierarchyTemplate, Is.Not.Null); Assert.That(view.InspectorTemplate, Is.Not.Null);
            Assert.That(view.Search.textComponent.font, Is.Not.Null); Assert.That(view.InspectorTemplate.Value.textComponent.font, Is.Not.Null);
            Assert.That(view.HierarchyScroll.viewport, Is.Not.Null); Assert.That(view.InspectorScroll.content, Is.Not.Null);
        }
    }
}
