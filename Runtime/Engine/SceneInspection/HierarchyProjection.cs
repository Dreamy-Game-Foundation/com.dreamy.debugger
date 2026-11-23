using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Dreamy.Debugger.SceneInspection
{
    /// <summary>Cancelable, budgeted name search/flattening over a stable session snapshot.</summary>
    public sealed class HierarchyProjection
    {
        private IReadOnlyList<SceneInspectionSession.Node> source;
        private SceneInspectionSession owner;
        private bool[] matches, visible;
        private string query, term;
        private string filter;
        private readonly List<UnityEngine.Component> components = new List<UnityEngine.Component>();
        private int? scene;
        private bool activeOnly;
        private int cursor, phase;
        private readonly List<SceneInspectionSession.Node> result = new List<SceneInspectionSession.Node>();
        public bool IsBuilding { get; private set; }
        public IReadOnlyList<SceneInspectionSession.Node> Result => result;
        public int Generation { get; private set; }
        public int SearchedCount => cursor;
        public void Begin(SceneInspectionSession session, string text, int? sceneHandle, bool active)
        {
            Generation++; owner = session; source = session.Nodes; query = text ?? ""; scene = sceneHandle; activeOnly = active;
            var colon = query.IndexOf(':');
            filter = colon > 0 ? query.Substring(0, colon).ToLowerInvariant() : "";
            term = colon > 0 ? query.Substring(colon + 1).Trim() : query;
            matches = new bool[source.Count]; visible = new bool[source.Count]; result.Clear(); cursor = 0;
            phase = query.Length == 0 ? 1 : 0; IsBuilding = true;
        }
        /// <returns>True only when this generation's complete result is ready.</returns>
        public bool Tick(double milliseconds)
        {
            if (!IsBuilding) return false;
            var clock = Stopwatch.StartNew(); var steps = 0;
            while (steps++ < 512 && clock.Elapsed.TotalMilliseconds < Math.Max(.1, milliseconds))
            {
                if (cursor >= source.Count)
                {
                    if (phase == 0) { phase = 1; cursor = 0; continue; }
                    IsBuilding = false; return true;
                }
                var node = source[cursor];
                var eligible = node.Target != null && (!scene.HasValue || scene.Value == node.SceneHandle) && (!activeOnly || node.Active);
                if (phase == 0)
                {
                    if (eligible && Matches(node))
                        for (var parent = cursor; parent >= 0 && !matches[parent]; parent = source[parent].Parent) matches[parent] = true;
                }
                else
                {
                    visible[cursor] = node.Target != null && (query.Length > 0 ? matches[cursor] :
                        eligible && (node.Parent < 0 || visible[node.Parent] && owner.IsExpanded(source[node.Parent])));
                    if (visible[cursor]) result.Add(node);
                }
                cursor++;
            }
            return false;
        }
        private bool Matches(SceneInspectionSession.Node node)
        {
            switch (filter)
            {
                case "tag": return Contains(node.Target.tag, term);
                case "layer":
                    return int.TryParse(term, out var index) ? node.Target.layer == index :
                        Contains(UnityEngine.LayerMask.LayerToName(node.Target.layer), term);
                case "type":
                case "component":
                    node.Target.GetComponents(components);
                    foreach (var component in components)
                        if (component != null && Contains(component.GetType().FullName, term))
                        { components.Clear(); return true; }
                    components.Clear();
                    return false;
                default: return Contains(node.Name, query);
            }
        }
        private static bool Contains(string value, string text) =>
            value != null && value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

        public void Cancel() { Generation++; IsBuilding = false; owner = null; source = null; result.Clear(); matches = visible = null; }
    }
}
