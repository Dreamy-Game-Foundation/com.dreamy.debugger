namespace Dreamy.Debugger.Presentation.Services.Implementation
{
    using System;
    using Internal;
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.Service;
    using UI.Other;
    using UnityEngine;

    [Service(typeof (IDebugTriggerService))]
    public class DebugTriggerImpl : ServiceBase<IDebugTriggerService>, IDebugTriggerService
    {
        private PinAlignment _position;
        private TriggerRoot _trigger;
        private IConsoleService _consoleService;
        private bool _showErrorNotification;

        public bool IsEnabled
        {
            get { return _trigger != null && _trigger.CachedGameObject.activeSelf; }
            set
            {
                // Create trigger if it does not yet exist
                if (value && _trigger == null)
                {
                    CreateTrigger();
                }

                if (_trigger != null)
                {
                    _trigger.CachedGameObject.SetActive(value);
                }
            }
        }

        public bool ShowErrorNotification
        {
            get
            {
                return _showErrorNotification;
            }
            set
            {
                if (_showErrorNotification == value) return;

                _showErrorNotification = value;

                if (_trigger == null) return;

                if(_showErrorNotification)
                {
                    _consoleService = ServiceManager.GetService<IConsoleService>();
                    _consoleService.Error += OnError;
                }
                else
                {
                    _consoleService.Error -= OnError;
                    _consoleService = null;
                }
            }
        }

        public PinAlignment Position
        {
            get { return _position; }
            set
            {
                if (_trigger != null)
                {
                    SetTriggerPosition(_trigger.TriggerTransform, value);
                }

                _position = value;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(CachedGameObject);

            CachedTransform.SetParent(Hierarchy.Get("DreamyDebugger"), true);
            ShowErrorNotification = Settings.Instance.ErrorNotification;

            name = "Trigger";
        }

        private void OnError(IConsoleService console)
        {
            if (_trigger != null)
            {
                _trigger.ErrorNotifier.ShowErrorWarning();
            }
        }

        private void CreateTrigger()
        {
            var prefab = PresentationAssets.Load<TriggerRoot>(PanelPaths.TriggerPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("[DreamyDebugger] Error loading trigger prefab");
                return;
            }

            _trigger = ObjectFactory.Instantiate(prefab);
            _trigger.CachedTransform.SetParent(CachedTransform, true);

            SetTriggerPosition(_trigger.TriggerTransform, _position);

            switch (Settings.Instance.TriggerBehaviour)
            {
                case Settings.TriggerBehaviours.ConfigurableTap:
                {
                    _trigger.TripleTapButton.RequiredTapCount = Settings.Instance.RequiredTapCount;
                    _trigger.TripleTapButton.ResetTime=Settings.Instance.TapGapSeconds;
                    _trigger.TripleTapButton.SequenceTimeout=Settings.Instance.SequenceTimeoutSeconds;
                    _trigger.TripleTapButton.onClick.AddListener(OnTriggerButtonClick);
                    _trigger.TapHoldButton.gameObject.SetActive(false);
                    break;
                }
                case Settings.TriggerBehaviours.TripleTap:
                {
                    _trigger.TripleTapButton.onClick.AddListener(OnTriggerButtonClick);
                    _trigger.TapHoldButton.gameObject.SetActive(false);

                    break;
                }

                case Settings.TriggerBehaviours.TapAndHold:
                {
                    _trigger.TapHoldButton.onLongPress.AddListener(OnTriggerButtonClick);
                    _trigger.TripleTapButton.gameObject.SetActive(false);

                    break;
                }

                case Settings.TriggerBehaviours.DoubleTap:
                {
                    _trigger.TripleTapButton.RequiredTapCount = 2;
                    _trigger.TripleTapButton.onClick.AddListener(OnTriggerButtonClick);
                    _trigger.TapHoldButton.gameObject.SetActive(false);

                    break;
                }

                default:
                    throw new Exception("Unhandled TriggerBehaviour");
            }
            
            PresentationUtility.EnsureEventSystemExists();

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;

            if (_showErrorNotification)
            {
                _consoleService = ServiceManager.GetService<IConsoleService>();
                _consoleService.Error += OnError;
            }
        }

        protected override void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            if (_consoleService != null)
            {
                _consoleService.Error -= OnError;
            }

            base.OnDestroy();
        }

        private static void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene s1, UnityEngine.SceneManagement.Scene s2)
        {
            PresentationUtility.EnsureEventSystemExists();
        }
        
        private void OnTriggerButtonClick()
        {
            if (_trigger.ErrorNotifier.IsVisible)
            {
                // Open into console if there is an error.
                DreamyDebug.Instance.ShowDebugPanel(DefaultTabs.Console);
            }
            else
            {
                DreamyDebug.Instance.ShowDebugPanel();
            }
        }

        private static void SetTriggerPosition(RectTransform t, PinAlignment position)
        {
            var pivotX = 0f;
            var pivotY = 0f;

            var posX = 0f;
            var posY = 0f;

            if (position == PinAlignment.TopLeft || position == PinAlignment.TopRight || position == PinAlignment.TopCenter)
            {
                pivotY = 1f;
                posY = 1f;
            }
            else if (position == PinAlignment.BottomLeft || position == PinAlignment.BottomRight || position == PinAlignment.BottomCenter)
            {
                pivotY = 0f;
                posY = 0f;
            } else if (position == PinAlignment.CenterLeft || position == PinAlignment.CenterRight)
            {
                pivotY = 0.5f;
                posY = 0.5f;
            }

            if (position == PinAlignment.TopLeft || position == PinAlignment.BottomLeft || position == PinAlignment.CenterLeft)
            {
                pivotX = 0f;
                posX = 0f;
            }
            else if (position == PinAlignment.TopRight || position == PinAlignment.BottomRight || position == PinAlignment.CenterRight)
            {
                pivotX = 1f;
                posX = 1f;
            } else if (position == PinAlignment.TopCenter || position == PinAlignment.BottomCenter)
            {
                pivotX = 0.5f;
                posX = 0.5f;
            }

            t.pivot = new Vector2(pivotX, pivotY);
            t.anchorMax = t.anchorMin = new Vector2(posX, posY);
        }
    }
}