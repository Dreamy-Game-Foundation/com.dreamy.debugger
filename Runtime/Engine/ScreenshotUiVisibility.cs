using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dreamy.Debugger
{
    public enum ScreenshotGameplayUi { Keep, RegisteredTargets, AllCanvases }

    /// <summary>Shared, nestable suppression for store captures and diagnostic captures.</summary>
    internal sealed class ScreenshotUiVisibility : IDisposable
    {
        private sealed class State
        {
            public float Alpha;
            public bool Interactable, BlocksRaycasts;
        }

        private sealed class Lease : IDisposable
        {
            private ScreenshotUiVisibility owner;
            internal readonly ScreenshotGameplayUi Mode;
            internal readonly bool AllowHoldFeedback;
            internal Lease(ScreenshotUiVisibility owner, ScreenshotGameplayUi mode, bool allowHoldFeedback) { this.owner = owner; Mode = mode; AllowHoldFeedback = allowHoldFeedback; }
            public void Dispose()
            {
                if (owner == null) return;
                var previous = owner;
                owner = null;
                previous.leases.Remove(this);
                previous.Refresh();
            }
        }

        private readonly Func<IEnumerable<GameObject>> debuggerRoots;
        private readonly Func<GameObject> holdFeedbackRoot;
        private readonly List<Lease> leases = new List<Lease>();
        private readonly Dictionary<CanvasGroup, State> states = new Dictionary<CanvasGroup, State>();
        private readonly HashSet<CanvasGroup> addedGroups = new HashSet<CanvasGroup>();
        private bool refreshing, disposed;
        internal bool IsSuppressed => leases.Count > 0;
        internal int ActiveLeaseCount => leases.Count;

        internal ScreenshotUiVisibility(Func<IEnumerable<GameObject>> debuggerRoots, Func<GameObject> holdFeedbackRoot = null)
        {
            this.debuggerRoots = debuggerRoots;
            this.holdFeedbackRoot = holdFeedbackRoot;
            Canvas.willRenderCanvases += Refresh;
        }

        internal IDisposable Acquire(ScreenshotGameplayUi mode = ScreenshotGameplayUi.Keep, bool allowHoldFeedback = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ScreenshotUiVisibility));
            var lease = new Lease(this, mode, allowHoldFeedback);
            leases.Add(lease);
            Refresh();
            return lease;
        }

        internal void Refresh()
        {
            if (disposed || refreshing || leases.Count == 0 && states.Count == 0) return;
            refreshing = true;
            try
            {
                var wanted = new HashSet<CanvasGroup>();
                var feedback = holdFeedbackRoot?.Invoke();
                var allowFeedback = feedback != null && leases.Count > 0 && leases.All(lease => lease.AllowHoldFeedback);
                if (leases.Count > 0)
                {
                    foreach (var root in debuggerRoots()) Collect(root, wanted);
                    if (leases.Any(lease => lease.Mode == ScreenshotGameplayUi.AllCanvases))
                    {
                        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            Collect(canvas.gameObject, wanted);
                    }
                    else if (leases.Any(lease => lease.Mode == ScreenshotGameplayUi.RegisteredTargets))
                    {
                        foreach (var target in UnityEngine.Object.FindObjectsByType<ScreenshotHideTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (target.enabled && target.Selected) Collect(target.gameObject, wanted);
                    }
                }

                // Exempt only this owned, non-interactive overlay in a manual session.
                // A diagnostic or PNG lease always wins and suppresses it normally.
                if (allowFeedback) wanted.RemoveWhere(group => group != null && (group.gameObject == feedback || group.transform.IsChildOf(feedback.transform)));

                foreach (var group in states.Keys.Where(group => group == null || !wanted.Contains(group)).ToArray())
                {
                    Restore(group, states[group]);
                    states.Remove(group);
                }
                foreach (var group in wanted)
                {
                    if (!states.ContainsKey(group))
                        states.Add(group, new State { Alpha = group.alpha, Interactable = group.interactable, BlocksRaycasts = group.blocksRaycasts });
                    group.alpha = 0;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                }
            }
            finally { refreshing = false; }
        }

        private void Collect(GameObject root, HashSet<CanvasGroup> wanted)
        {
            if (root == null) return;
            EnsureGroup(root);
            // A nested override-sorting Canvas can form its own rendering boundary.
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) EnsureGroup(canvas.gameObject);
            // Mask children too: ignoreParentGroups must not let a nested popup escape.
            foreach (var child in root.GetComponentsInChildren<CanvasGroup>(true)) wanted.Add(child);
        }

        private void EnsureGroup(GameObject root)
        {
            var group = root.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = root.AddComponent<CanvasGroup>();
                addedGroups.Add(group);
            }
        }

        private static void Restore(CanvasGroup group, State state)
        {
            if (group == null) return;
            group.alpha = state.Alpha;
            group.interactable = state.Interactable;
            group.blocksRaycasts = state.BlocksRaycasts;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Canvas.willRenderCanvases -= Refresh;
            foreach (var pair in states) Restore(pair.Key, pair.Value);
            states.Clear();
            leases.Clear();
            // Added masks stay neutral between captures; destroy only when their owner shuts down.
            foreach (var group in addedGroups) if (group != null) UnityEngine.Object.Destroy(group);
            addedGroups.Clear();
        }
    }
}
