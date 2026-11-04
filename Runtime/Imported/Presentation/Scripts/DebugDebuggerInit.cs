using System;

namespace Dreamy.Debugger.Presentation
{
    using Dreamy.Debugger.Foundation;
    using UnityEngine;

    /// <summary>
    /// Add this component somewhere in your scene to automatically load Dreamy.Debugger.Presentation when the scene is loaded.
    /// By default, Dreamy.Debugger.Presentation will defer loading any UI except the corner-trigger until the user requests it.
    /// It is recommended to add this to the very first scene in your game. This will ensure the console log
    /// will hold useful information about your game initialization.
    /// </summary>
    [AddComponentMenu("")]
    [Obsolete("No longer required, use Automatic initialization mode or call DreamyDebug.Init() manually.")]
    public class DebugDebuggerInit : ComponentBehaviour
    {
    }
}
