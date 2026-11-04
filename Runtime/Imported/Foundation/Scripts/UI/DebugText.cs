namespace Dreamy.Debugger.Foundation.UI
{
    using System;
    using Internal;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Adds a LayoutDirty callback to the default Text component.
    /// </summary>
    [AddComponentMenu(ComponentMenuPaths.DebugText)]
    public class DebugText : Text
    {
        public event Action<DebugText> LayoutDirty;

        public override void SetLayoutDirty()
        {
            base.SetLayoutDirty();

            if (LayoutDirty != null)
            {
                LayoutDirty(this);
            }
        }
    }
}
