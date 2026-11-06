
#pragma warning disable 169
#pragma warning disable 649

namespace Dreamy.Debugger.Presentation.UI.Controls
{
    using System;
    using System.Collections;
    using Internal;
    using Services;
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.UI.Layout;
    using UnityEngine;
    using UnityEngine.UI;

    public class ConsoleLogControl : ComponentBehaviour
    {
        [RequiredField] [SerializeField] private VirtualVerticalLayoutGroup _consoleScrollLayoutGroup;

        [RequiredField] [SerializeField] private ScrollRect _consoleScrollRect;

        public int UnseenCount { get; private set; }
        public Text NewEntriesText;
        private float? preserveOffset;
        public void JumpToLatest() { UnseenCount=0;preserveOffset=null;_scrollPosition=Vector2.zero; }
        public void SelectEntry(ConsoleEntry entry) { _consoleScrollLayoutGroup.SelectedItem=entry; }
        public ConsoleEntry SelectedEntry => _consoleScrollLayoutGroup.SelectedItem as ConsoleEntry;
        public ScrollRect ScrollView => _consoleScrollRect;
        private void LateUpdate(){
            if(preserveOffset.HasValue){var p=_consoleScrollRect.content.anchoredPosition;p.y=Mathf.Clamp(preserveOffset.Value,0,Mathf.Max(0,_consoleScrollRect.content.rect.height-(_consoleScrollRect.viewport!=null?_consoleScrollRect.viewport.rect.height:((RectTransform)_consoleScrollRect.transform).rect.height)));_consoleScrollRect.content.anchoredPosition=p;preserveOffset=null;}
            if(_consoleScrollRect.verticalNormalizedPosition<.01f)UnseenCount=0;
            if(NewEntriesText!=null)NewEntriesText.text=UnseenCount>0?UnseenCount+" new":"Latest";
        }
        private bool _isDirty;
        private Vector2? _scrollPosition;
        private bool _showErrors = true;
        private bool _showInfo = true;
        private bool _showWarnings = true;
        public Action<ConsoleEntry> SelectedItemChanged;
        private string _filter;

        public bool ShowErrors
        {
            get { return _showErrors; }
            set
            {
                _showErrors = value;
                SetIsDirty();
            }
        }

        public bool ShowWarnings
        {
            get { return _showWarnings; }
            set
            {
                _showWarnings = value;
                SetIsDirty();
            }
        }

        public bool ShowInfo
        {
            get { return _showInfo; }
            set
            {
                _showInfo = value;
                SetIsDirty();
            }
        }

        public bool EnableSelection
        {
            get { return _consoleScrollLayoutGroup.EnableSelection; }
            set { _consoleScrollLayoutGroup.EnableSelection = value; }
        }

        public string Filter
        {
            get { return _filter; }
            set {
                if (_filter != value)
                {
                    _filter = value;
                    _isDirty = true;
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();

            _consoleScrollLayoutGroup.SelectedItemChanged.AddListener(OnSelectedItemChanged);
            Service.Console.Updated += ConsoleOnUpdated;
        }

        protected override void Start()
        {
            base.Start();
            SetIsDirty();
            StartCoroutine(ScrollToBottom());
        }

        IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            _scrollPosition = new Vector2(0,0);
        }

        protected override void OnDestroy()
        {
            if (Service.Console != null)
            {
                Service.Console.Updated -= ConsoleOnUpdated;
            }

            base.OnDestroy();
        }

        private void OnSelectedItemChanged(object arg0)
        {
            var entry = arg0 as ConsoleEntry;

            if (SelectedItemChanged != null)
            {
                SelectedItemChanged(entry);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (_scrollPosition.HasValue)
            {
                _consoleScrollRect.normalizedPosition = _scrollPosition.Value;
                _scrollPosition = null;
            }

            if (_isDirty)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_consoleScrollRect.normalizedPosition.y < 0.01f)
            {
                _scrollPosition = _consoleScrollRect.normalizedPosition;
            }

            else preserveOffset=_consoleScrollRect.content.anchoredPosition.y;
            var selected=_consoleScrollLayoutGroup.SelectedItem;
            var selectedVisible=false;
            _consoleScrollLayoutGroup.ClearItems();

            var entries = Service.Console.Entries;

            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];

                if ((e.LogType == LogType.Error || e.LogType == LogType.Exception || e.LogType == LogType.Assert) &&
                    !ShowErrors)
                {
                    if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                    continue;
                }

                if (e.LogType == LogType.Warning && !ShowWarnings)
                {
                    if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                    continue;
                }

                if (e.LogType == LogType.Log && !ShowInfo)
                {
                    if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                    continue;
                }

                if (!string.IsNullOrEmpty(Filter))
                {
                    if (e.Message.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                        continue;
                    }
                }

                _consoleScrollLayoutGroup.AddItem(e);
                if(ReferenceEquals(e,selected))selectedVisible=true;
            }

            if(selected!=null && !selectedVisible)_consoleScrollLayoutGroup.SelectedItem=null;
            _isDirty = false;
        }

        private void SetIsDirty()
        {
            _isDirty = true;
        }

        private void ConsoleOnUpdated(IConsoleService console)
        {
            if(_consoleScrollRect.verticalNormalizedPosition>=.01f)UnseenCount++;
            if(console.Entries.Count==0)UnseenCount=0;
            SetIsDirty();
        }
    }
}
