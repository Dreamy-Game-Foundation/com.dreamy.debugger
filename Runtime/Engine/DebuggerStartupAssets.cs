using UnityEngine;
using Dreamy.Debugger.Presentation;
namespace Dreamy.Debugger
{
    public sealed class DebuggerStartupAssets : ScriptableObject
    {
        public DebuggerLoadingMode Loading = DebuggerLoadingMode.Automatic;
        public DebuggerConfiguration Configuration;
        public DebuggerAssetCatalog Catalog;
    }
}
