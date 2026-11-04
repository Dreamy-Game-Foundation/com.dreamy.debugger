using System.ComponentModel;
using UnityEngine.Serialization;

namespace Dreamy.Debugger.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using Dreamy.Debugger.Foundation;
    using UnityEngine;
#if UNITY_EDITOR

#endif

#pragma warning disable 0649

    public enum DefaultTabs
    {
        SystemInformation = 0,
        Options = 1,
        Console = 2,
        Profiler = 3,
        BugReporter = 4
    }

    public enum PinAlignment
    {
        TopLeft = 0,
        TopRight = 1,
        BottomLeft = 2,
        BottomRight = 3,
        CenterLeft = 4,
        CenterRight = 5,
        TopCenter = 6,
        BottomCenter = 7
    }

    public enum ConsoleAlignment
    {
        Top,
        Bottom
    }

    public class Settings : ScriptableObject
    {
        public enum ShortcutActions
        {
            None = 0,

            OpenSystemInfoTab = 1,
            OpenConsoleTab = 2,
            OpenOptionsTab = 3,
            OpenProfilerTab = 4,
            OpenBugReporterTab = 5,

            ClosePanel = 6,
            OpenPanel = 7,
            TogglePanel = 8,

            ShowBugReportPopover = 9,
            ToggleDockedConsole = 10,
            ToggleDockedProfiler = 11
        }

        public enum TriggerBehaviours
        {
            ConfigurableTap = 3,
            TripleTap,
            TapAndHold,
            DoubleTap
        }

        public enum TriggerEnableModes
        {
            Enabled,
            MobileOnly,
            Off,
            DevelopmentBuildsOnly
        }

        public enum UIModes
        {
            NewInputSystem,
            LegacyInputSystem
        }

        internal const string ResourcesName = "Settings";
        private static Settings _instance;

        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = GetOrCreateInstance();
                }

                return _instance;
            }
        }
        private static KeyboardShortcut[] GetDefaultKeyboardShortcuts()
        {
            return new[]
            {
                new KeyboardShortcut
                {
                    Control = true,
                    Shift = true,
                    Key = KeyCode.F1,
                    Action = ShortcutActions.OpenSystemInfoTab
                },
                new KeyboardShortcut
                {
                    Control = true,
                    Shift = true,
                    Key = KeyCode.F2,
                    Action = ShortcutActions.OpenConsoleTab
                },
                new KeyboardShortcut
                {
                    Control = true,
                    Shift = true,
                    Key = KeyCode.F3,
                    Action = ShortcutActions.OpenOptionsTab
                },
                new KeyboardShortcut
                {
                    Control = true,
                    Shift = true,
                    Key = KeyCode.F4,
                    Action = ShortcutActions.OpenProfilerTab
                }
            };
        }

        private void UpgradeKeyboardShortcuts()
        {
            if (_keyboardShortcuts == null || _keyboardShortcuts.Length == 0)
            {
                return; // Nothing to do
            }

            Debug.Log("[DreamyDebugger] Upgrading Settings format");

            var newShortcuts = new List<KeyboardShortcut>();

            for (var i = 0; i < _keyboardShortcuts.Length; i++)
            {
                var s = _keyboardShortcuts[i];

                newShortcuts.Add(new KeyboardShortcut
                {
                    Action = s.Action,
                    Key = s.Key,
                    Alt = _keyboardModifierAlt,
                    Shift = _keyboardModifierShift,
                    Control = _keyboardModifierControl
                });
            }

            _keyboardShortcuts = new KeyboardShortcut[0];
            _newKeyboardShortcuts = newShortcuts.ToArray();

#if UNITY_EDITOR

#endif
        }

        [Serializable]
        public sealed class KeyboardShortcut
        {
            [SerializeField] public ShortcutActions Action;

            [SerializeField] public bool Alt;

            [SerializeField] public bool Control;

            [SerializeField] public KeyCode Key;

            [SerializeField] public bool Shift;


        }

        public event PropertyChangedEventHandler PropertyChanged;

        [SerializeField, Range(1,20)] private int _requiredTapCount = 5;
        public float TapGapSeconds=.4f;
        public float SequenceTimeoutSeconds=2.5f;
        public int RequiredTapCount { get => _requiredTapCount; set => _requiredTapCount = Mathf.Clamp(value,1,20); }

        #region Settings

        public bool IsEnabled
        {
            get { return _isEnabled; }
set { _isEnabled = value; }
        }

        public UIModes UIInputMode
        {
            get { return _uiInputMode; }
set { _uiInputMode = value; }
        }

        public DefaultTabs DefaultTab
        {
            get { return _defaultTab; }
set { _defaultTab = value; }
        }

        /// <summary>
        /// Enable the triple-tap button.
        /// </summary>
        public TriggerEnableModes EnableTrigger
        {
            get { return _triggerEnableMode; }
set { _triggerEnableMode = value; }
        }

        /// <summary>
        /// Enable the triple-tap button.
        /// </summary>
        public TriggerBehaviours TriggerBehaviour
        {
            get { return _triggerBehaviour; }
set { _triggerBehaviour = value; }
        }

        /// <summary>
        /// Enable a notification when a new error is logged.
        /// </summary>
        public bool ErrorNotification
        {
            get { return _errorNotification; }
set { _errorNotification = value; }
        }

        public bool EnableKeyboardShortcuts
        {
            get { return _enableKeyboardShortcuts; }
set { _enableKeyboardShortcuts = value; }
        }

        public IList<KeyboardShortcut> KeyboardShortcuts
        {
            get { return _newKeyboardShortcuts; }
set { _newKeyboardShortcuts = value.ToArray(); }
        }

        public bool KeyboardEscapeClose
        {
            get { return _keyboardEscapeClose; }
set { _keyboardEscapeClose = value; }
        }

        public bool EnableBackgroundTransparency
        {
            get { return _enableBackgroundTransparency; }
set { _enableBackgroundTransparency = value; }
        }

        public float BackgroundTransparency
        {
            get { return _backgroundTransparency; }
set { _backgroundTransparency = value; }
        }

        public bool RequireCode
        {
            get { return _requireEntryCode; }
set { _requireEntryCode = value; }
        }

        public bool RequireEntryCodeEveryTime
        {
            get { return _requireEntryCodeEveryTime; }
set { _requireEntryCodeEveryTime = value; }
        }

        public IList<int> EntryCode
        {
            get { return new ReadOnlyCollection<int>(_entryCode); }
            set
            {
                if (value.Count < 4 || value.Count > 32)
                {
                    throw new Exception("Entry code must contain 4–32 digits");
                }
                if (value.Any(p => p > 9 || p < 0))
                {
                    throw new Exception("All digits in entry code must be >= 0 and <= 9");
                }

                _entryCode = value.ToArray();
            }
        }

        public bool UseDebugCamera
        {
            get { return _useDebugCamera; }
set { _useDebugCamera = value; }
        }

        public int DebugLayer
        {
            get { return _debugLayer; }
set { _debugLayer = value; }
        }

        public float DebugCameraDepth
        {
            get { return _debugCameraDepth; }
set { _debugCameraDepth = value; }
        }

        public bool CollapseDuplicateLogEntries
        {
            get { return _collapseDuplicateLogEntries; }
set { _collapseDuplicateLogEntries = value; }
        }

        public bool RichTextInConsole
        {
            get { return _richTextInConsole; }
set { _richTextInConsole = value; }
        }

        public string ApiKey
        {
            get { return _apiKey; }
#if UNITY_EDITOR
            set
            {
                _apiKey = value;
    
            }
#endif
        }

        public bool EnableBugReporter
        {
            get { return _enableBugReporter; }
set { _enableBugReporter = value; }
        }

        public bool EnableBugReportScreenshot
        {
            get { return _enableBugReportScreenshot; }
set { _enableBugReportScreenshot = value;  }
        }

        public IList<DefaultTabs> DisabledTabs
        {
            get { return _disabledTabs; }
set { _disabledTabs = value.ToArray(); }
        }

        /// <summary>
        /// Position for the triple-tap button
        /// </summary>
        public PinAlignment TriggerPosition
        {
            get { return _triggerPosition; }
#if UNITY_EDITOR
            set
            {
                var prevValue = _triggerPosition;
                _triggerPosition = value;
                if (OptionsAlignment == value)
                {
                    OptionsAlignment = prevValue;
                }
                if (ProfilerAlignment == value)
                {
                    ProfilerAlignment = prevValue;
                }
            }
#endif
        }

        public PinAlignment ProfilerAlignment
        {
            get { return _profilerAlignment; }
#if UNITY_EDITOR
            set
            {
                // Profiler only supports corners
                if (value == PinAlignment.CenterRight || value == PinAlignment.CenterLeft ||
                    value == PinAlignment.TopCenter || value == PinAlignment.BottomCenter)
                {
                    // Prefer bottom left
                    if (TriggerPosition != PinAlignment.BottomLeft && OptionsAlignment != PinAlignment.BottomLeft)
                    {
                        _profilerAlignment = PinAlignment.BottomLeft;
                        return;
                    }

                    // Find next available opening
                    for (var i = 0; i < 4; i++)
                    {
                        var pin = (PinAlignment) i;
                        if (_triggerPosition == pin || _optionsAlignment == pin)
                            continue;

                        _profilerAlignment = pin;
                        return;
                    }
                    
                    _profilerAlignment = PinAlignment.BottomLeft;
                    return;
                }

                var prevValue = _profilerAlignment;
                _profilerAlignment = value;

                if (TriggerPosition == value)
                {
                    TriggerPosition = prevValue;
                }
                if (OptionsAlignment == value)
                {
                    OptionsAlignment = prevValue;
                }
            }
#endif
        }

        public PinAlignment OptionsAlignment
        {
            get { return _optionsAlignment; }
#if UNITY_EDITOR
            set
            {
                // Options doesn't support CenterRight and CenterLeft
                if (value == PinAlignment.CenterRight || value == PinAlignment.CenterLeft)
                {                    
                    // Prefer bottom right
                    if (TriggerPosition != PinAlignment.BottomRight && ProfilerAlignment != PinAlignment.BottomRight)
                    {
                        _optionsAlignment = PinAlignment.BottomRight;
                        return;
                    }
                    for (var i = 0; i < 4; i++)
                    {
                        var pin = (PinAlignment) i;
                        if (_triggerPosition == pin || _profilerAlignment == pin)
                            continue;

                        _optionsAlignment = pin;
                        return;
                    }

                    _optionsAlignment = PinAlignment.BottomRight;
                    return;
                }

                var prevValue = _optionsAlignment;
                _optionsAlignment = value;

                if (TriggerPosition == value)
                {
                    TriggerPosition = prevValue;
                }

                if (ProfilerAlignment == value)
                {
                    ProfilerAlignment = prevValue;
                }
            }
#endif
        }

        public ConsoleAlignment ConsoleAlignment
        {
            get { return _consoleAlignment; }
            set { _consoleAlignment = value; }
        }

        public int MaximumConsoleEntries
        {
            get { return _maximumConsoleEntries; }
            set { _maximumConsoleEntries = value; }
        }

        public bool EnableEventSystemGeneration
        {
            get { return _enableEventSystemCreation; }
            set { _enableEventSystemCreation = value; }
        }

        public bool AutomaticallyShowCursor
        {
            get { return _automaticShowCursor; }
set { _automaticShowCursor = value; }
        }

        public float UIScale
        {
            get { return _uiScale; }
            set
            {
                if (value == _uiScale) return;
                _uiScale = value;
                OnPropertyChanged("UIScale");
            }
        }

        public bool UnloadOnClose
        {
            get { return _unloadOnClose; }
#if UNITY_EDITOR
            set
            {
                _unloadOnClose = value;
            }
#endif
        }

#if UNITY_EDITOR
        public bool DisableWelcomePopup
        {
            get { return _disableWelcomePopup; }
            set { _disableWelcomePopup = value; }
        }
#endif

#endregion

        #region Serialization

        [SerializeField] private bool _isEnabled = true;

        [SerializeField] private bool _disableWelcomePopup = false;

        [SerializeField] private UIModes _uiInputMode = UIModes.NewInputSystem;

        [SerializeField] private DefaultTabs _defaultTab = DefaultTabs.SystemInformation;

        [SerializeField] private TriggerEnableModes _triggerEnableMode = TriggerEnableModes.Enabled;

        [SerializeField] private TriggerBehaviours _triggerBehaviour = TriggerBehaviours.ConfigurableTap;

        [SerializeField] private bool _errorNotification = true;

        [SerializeField] private bool _enableKeyboardShortcuts = true;

        // Legacy keyboard shortcuts, should be upgraded
        [SerializeField] private KeyboardShortcut[] _keyboardShortcuts;

        // New keyboard shortcut array, containing upgraded shortcuts
        [SerializeField] private KeyboardShortcut[] _newKeyboardShortcuts = GetDefaultKeyboardShortcuts();

        [SerializeField] private bool _keyboardModifierControl = true;

        [SerializeField] private bool _keyboardModifierAlt = false;

        [SerializeField] private bool _keyboardModifierShift = true;

        [SerializeField] private bool _keyboardEscapeClose = true;

        [SerializeField] private bool _enableBackgroundTransparency = true;

        [SerializeField] private float _backgroundTransparency = 0.9f;

        [SerializeField] private bool _collapseDuplicateLogEntries = true;

        [SerializeField] private bool _richTextInConsole = true;

        [SerializeField] private bool _requireEntryCode = true;

        [SerializeField] private bool _requireEntryCodeEveryTime;

        [SerializeField] private int[] _entryCode = {0, 0, 0, 0};

        [SerializeField] private bool _useDebugCamera;

        [SerializeField] private int _debugLayer = 5;

        [SerializeField] [Range(-100, 100)] private float _debugCameraDepth = 100f;

        [SerializeField] private string _apiKey = "";

        [SerializeField] private bool _enableBugReporter;

        [SerializeField] private bool _enableBugReportScreenshot = true;

        [SerializeField] private DefaultTabs[] _disabledTabs = {};

        [SerializeField] private PinAlignment _profilerAlignment = PinAlignment.BottomLeft;

        [SerializeField] private PinAlignment _optionsAlignment = PinAlignment.BottomRight;

        [SerializeField] private ConsoleAlignment _consoleAlignment = ConsoleAlignment.Top;

        [SerializeField] private PinAlignment _triggerPosition = PinAlignment.TopLeft;

        [SerializeField] private int _maximumConsoleEntries = 1500;

        [SerializeField] private bool _enableEventSystemCreation = true;

        [SerializeField] private bool _automaticShowCursor = true;

        [SerializeField] private float _uiScale = 1;

        [SerializeField] private bool _unloadOnClose = false;

        #endregion

        #region Property Changed

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region Saving/Loading

        public static void ClearCache()
        {
            if (_instance != null)
            {
                Resources.UnloadAsset(_instance);
            }

            _instance = null;
        }

        internal static Settings GetInstance()
        {
            var instance = PresentationAssets.Load<Settings>("DreamyDebugger/" + ResourcesName);
            return instance;
        }

        private static Settings GetOrCreateInstance()
        {
            var authored = GetInstance();
            var instance = authored == null ? null : Instantiate(authored);

            if (instance == null)
            {
                Debug.Log("[DreamyDebugger] No DreamyDebugger settings object found - using defaults. (Open DreamyDebugger Settings window in the Unity Editor to create settings file)");

                // Create instance
                instance = CreateInstance<Settings>();

            } else
            {
                instance.UpgradeKeyboardShortcuts();
            }

            return instance;
        }

        #endregion
    }
}
