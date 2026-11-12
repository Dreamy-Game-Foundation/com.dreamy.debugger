using UnityEngine;

namespace Dreamy.Debugger
{
    /// <summary>Opt this UI subtree into Screenshot Mode without disabling its gameplay scripts.</summary>
    [AddComponentMenu("Dreamy/Debugger/Screenshot Hide Target")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenshotHideTarget : MonoBehaviour
    {
        [SerializeField] private string groupName;
        public string DisplayName => string.IsNullOrWhiteSpace(groupName) ? gameObject.name : groupName;
        [System.NonSerialized] public bool Selected = true;
    }
}
