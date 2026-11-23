using System;
using System.Collections.Generic;

namespace Dreamy.Debugger.SceneInspection
{
    internal sealed class InspectionHistory
    {
        private const int Limit = 64;
        private readonly List<Change> undo = new List<Change>();
        private readonly List<Change> redo = new List<Change>();

        private sealed class Change
        {
            public InspectionMember Member;
            public object Before, After;
        }

        public bool CanUndo => undo.Count != 0;
        public bool CanRedo => redo.Count != 0;

        public bool Write(InspectionMember member, object before, object draft, Func<bool> allowed, out string error)
        {
            if (!member.TryCommit(before, draft, allowed, out error)) return false;
            var readable = member.TryRead(out var actual, out _);
            if (readable && Equals(before, actual)) return true;
            redo.Clear();
            if (!member.HasStableHistory || !readable) return true;
            if (undo.Count == Limit) undo.RemoveAt(0);
            undo.Add(new Change { Member = member, Before = before, After = actual });
            return true;
        }

        public bool Move(bool backwards, Func<bool> allowed, out string error)
        {
            var source = backwards ? undo : redo;
            var destination = backwards ? redo : undo;
            error = null;
            if (source.Count == 0) return false;
            var change = source[source.Count - 1];
            if (!change.Member.TryCommit(backwards ? change.After : change.Before,
                backwards ? change.Before : change.After, allowed, out error)) return false;
            source.RemoveAt(source.Count - 1);
            if (!change.Member.TryRead(out var actual, out _)) return true;
            if (backwards) change.Before = actual;
            else change.After = actual;
            destination.Add(change);
            return true;
        }

        public void Clear() { undo.Clear(); redo.Clear(); }
    }
}
