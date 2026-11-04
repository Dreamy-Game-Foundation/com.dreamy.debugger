#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR || UNITY_STANDALONE
#define COPY_TO_CLIPBOARD_SUPPORTED
#endif

using System;
using System.Runtime.CompilerServices;
using Dreamy.Debugger.Presentation.Services;
using Dreamy.Debugger.Foundation.Service;
using UnityEngine;

[assembly: InternalsVisibleTo("Dreamy.Debugger.Presentation.Editor")]

public static class DreamyDebug
{
    public const string Version = Dreamy.Debugger.Presentation.VersionInfo.Version;

    public static bool IsInitialized { get; private set; }

    public static IDebugService Instance
    {
        get { return ServiceManager.GetService<IDebugService>(); }
    }

    /// <summary>
    /// Action to be invoked whenever the user selects "copy" in the console window.
    /// If null, copy/paste will not be available.
    /// </summary>
    public static Action<ConsoleEntry> CopyConsoleItemCallback = GetDefaultCopyConsoleItemCallback();

    public static void Init()
    {
        IsInitialized = true;

        ServiceManager.RegisterAssembly<IDebugService>();

        // Initialize console if it hasn't already initialized.
        ServiceManager.GetService<IConsoleService>();

        // Load the debug service
        ServiceManager.GetService<IDebugService>();


    }

    public static Action<ConsoleEntry> GetDefaultCopyConsoleItemCallback()
    {
#if COPY_TO_CLIPBOARD_SUPPORTED
        return entry =>
        {
            GUIUtility.systemCopyBuffer =
                string.Format("{0}: {1}\n\r\n\r{2}", entry.LogType, entry.Message, entry.StackTrace);
        };
#else
        return null;
#endif
    }
}
