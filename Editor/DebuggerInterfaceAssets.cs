using System.Collections.Generic;
using System.Linq;
using Dreamy.Debugger.Presentation;
using UnityEditor;
using UnityEngine;
namespace Dreamy.Debugger.Editor
{
    public static class DebuggerInterfaceAssets
    {
        private const string Root="Packages/com.dreamy.debugger/Runtime/Branding/Interface/";
        public static void Install()
        {
            DebuggerMaterialAssets.Install();
        }
    }
}
