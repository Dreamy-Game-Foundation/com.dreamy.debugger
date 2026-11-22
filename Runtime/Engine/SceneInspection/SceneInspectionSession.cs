using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dreamy.Debugger.SceneInspection
{
    /// <summary>Session-owned, budgeted scene snapshot. No scanning occurs until Tick is called.</summary>
    public sealed class SceneInspectionSession : IDisposable
    {
        public sealed class Node
        {
            public GameObject Target;
            public int Parent, Depth, SceneHandle;
            public string Name, SceneName, Path;
            public bool Active, HasChildren;
        }

        private readonly Func<bool> canRead, canWrite;
        private readonly Func<GameObject, bool> excluded;
        private readonly Scene persistentScene;
        private readonly float interval, budget;
        private readonly List<GameObject> roots = new List<GameObject>();
        private readonly Stack<Node> pending = new Stack<Node>();
        private List<Node> building = new List<Node>(), nodes = new List<Node>();
        private readonly HashSet<int> visited = new HashSet<int>();
        private readonly Dictionary<int, GameObject> expanded = new Dictionary<int, GameObject>();
        private double nextScan;
        private bool disposed, editing;
        private GameObject selected;
        public IReadOnlyList<Node> Nodes => nodes;
        public GameObject Selected => selected != null ? selected : null;
        public int Revision { get; private set; }
        public bool IsScanning => pending.Count != 0;
        public bool Paused { get; set; }
        public bool CanRead => !disposed && canRead();
        public bool CanEdit => CanRead && editing && canWrite();

        public SceneInspectionSession(Scene persistentScene, Func<bool> canRead, Func<bool> canWrite,
            Func<GameObject, bool> excluded, float interval = .25f, float budgetMilliseconds = 1)
        {
            this.persistentScene = persistentScene;
            this.canRead = canRead; this.canWrite = canWrite; this.excluded = excluded;
            this.interval = Mathf.Max(.1f, interval); budget = Mathf.Clamp(budgetMilliseconds, .1f, 5);
            SceneManager.sceneLoaded += Loaded; SceneManager.sceneUnloaded += Unloaded;
        }

        public void SetEditing(bool value) { editing = value && CanRead && canWrite(); }
        public void Select(GameObject value) { selected = value; Revision++; }
        public bool IsExpanded(Node node) => node.Target != null && expanded.TryGetValue(node.Target.GetInstanceID(), out var value) && value != null && value == node.Target;
        public void Toggle(Node node)
        {
            if (node.Target == null) return;
            var id = node.Target.GetInstanceID();
            if (!expanded.Remove(id)) expanded[id] = node.Target;
            Revision++;
        }
        public void Reveal(GameObject target)
        {
            if (target == null) return;
            for (var parent = target.transform.parent; parent != null; parent = parent.parent)
                expanded[parent.gameObject.GetInstanceID()] = parent.gameObject;
            Select(target);
        }
        public void Refresh() { nextScan = 0; }
        public void Suspend()
        {
            pending.Clear(); building.Clear(); visited.Clear(); editing = false; nextScan = 0;
        }
        public void Tick(double time)
        {
            if (!CanRead) { Suspend(); return; }
            if (!canWrite()) editing = false;
            if (selected == null && !ReferenceEquals(selected, null)) { selected = null; Revision++; }
            if (Paused) return;
            if (pending.Count == 0)
            {
                if (time < nextScan) return;
                BeginScan(); nextScan = time + interval;
            }
            var clock = Stopwatch.StartNew();
            var steps = 0;
            while (pending.Count > 0 && steps++ < 512 && clock.Elapsed.TotalMilliseconds < budget)
            {
                var node = pending.Pop(); var go = node.Target;
                if (go == null || excluded(go) || !visited.Add(go.GetInstanceID())) continue;
                var parent = building.Count;
                node.Name = go.name; node.Active = go.activeInHierarchy;
                node.Path = node.Parent < 0 ? node.Name : building[node.Parent].Path + "/" + node.Name;
                var transform = go.transform; node.HasChildren = transform.childCount > 0;
                building.Add(node);
                for (var i = transform.childCount - 1; i >= 0; i--)
                    pending.Push(new Node { Target = transform.GetChild(i).gameObject, Parent = parent,
                        Depth = node.Depth + 1, SceneHandle = node.SceneHandle, SceneName = node.SceneName });
            }
            if (pending.Count != 0) return;
            var previous = nodes; nodes = building; building = previous; building.Clear(); visited.Clear();
            var stale = new List<int>();
            foreach (var pair in expanded) if (pair.Value == null) stale.Add(pair.Key);
            foreach (var id in stale) expanded.Remove(id);
            Revision++;
        }
        private void BeginScan()
        {
            building.Clear(); visited.Clear();
            if (persistentScene.IsValid() && persistentScene.isLoaded) AddScene(persistentScene);
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--) AddScene(SceneManager.GetSceneAt(i));
        }
        private void AddScene(Scene scene)
        {
            if (!scene.isLoaded) return;
            roots.Clear(); scene.GetRootGameObjects(roots);
            for (var i = roots.Count - 1; i >= 0; i--)
                pending.Push(new Node { Target = roots[i], Parent = -1, SceneHandle = scene.handle, SceneName = scene.name });
            roots.Clear();
        }
        /// <summary>Returns snapshot indices; search also keeps ancestor context.</summary>
        public void Flatten(List<int> result, string query, int? sceneHandle, bool activeOnly)
        {
            result.Clear(); query = query ?? "";
            var searching = query.Length != 0;
            var matches = new bool[nodes.Count];
            if (searching)
                for (var i = 0; i < nodes.Count; i++)
                    if (Eligible(nodes[i], sceneHandle, activeOnly) && nodes[i].Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        for (var p = i; p >= 0; p = nodes[p].Parent) matches[p] = true;
            var visible = new bool[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                visible[i] = node.Target != null && (searching ? matches[i] :
                    Eligible(node, sceneHandle, activeOnly) && (node.Parent < 0 || visible[node.Parent] && IsExpanded(nodes[node.Parent])));
                if (visible[i]) result.Add(i);
            }
        }
        private static bool Eligible(Node node, int? scene, bool active) => node.Target != null && (!scene.HasValue || scene.Value == node.SceneHandle) && (!active || node.Active);
        private void Loaded(Scene scene, LoadSceneMode mode) { Suspend(); }
        private void Unloaded(Scene scene)
        {
            Suspend(); nodes.Clear();
            if (selected == null) selected = null;
            var stale = new List<int>(); foreach (var pair in expanded) if (pair.Value == null) stale.Add(pair.Key);
            foreach (var id in stale) expanded.Remove(id);
            Revision++;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; SceneManager.sceneLoaded -= Loaded; SceneManager.sceneUnloaded -= Unloaded;
            Suspend(); nodes.Clear(); roots.Clear(); expanded.Clear(); selected = null;
        }
    }
}
