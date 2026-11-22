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
        private string query;
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
                    if (eligible && node.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
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
        public void Cancel() { Generation++; IsBuilding = false; owner = null; source = null; result.Clear(); matches = visible = null; }
    }
}
