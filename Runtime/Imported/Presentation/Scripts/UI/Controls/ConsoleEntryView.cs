namespace Dreamy.Debugger.Presentation.UI.Controls
{
    using System;
    using Services;
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.UI;
    using Dreamy.Debugger.Foundation.UI.Layout;
    using UnityEngine;
    using UnityEngine.UI;

    [RequireComponent(typeof (RectTransform))]
    public class ConsoleEntryView : ComponentBehaviour, IVirtualView
    {
        public const string ConsoleBlobInfo = "Console_Info_Blob";
        public const string ConsoleBlobWarning = "Console_Warning_Blob";
        public const string ConsoleBlobError = "Console_Error_Blob";
        private int _count;
        private bool _hasCount;
        private ConsoleEntry _prevData;
        private RectTransform _rectTransform;

        public Text Timestamp;
        [RequiredField] public Text Count;

        [RequiredField] public CanvasGroup CountContainer;

        [RequiredField] public StyleComponent ImageStyle;

        [RequiredField] public Text Message;

        [RequiredField] public Text StackTrace;

        public void SetDataContext(object data)
        {
            var msg = data as ConsoleEntry;

            if (msg == null)
            {
                throw new Exception("Data should be a ConsoleEntry");
            }

            // Always check for updates on "Count", as it can change
            if (msg.Count > 1)
            {
                if (!_hasCount)
                {
                    CountContainer.alpha = 1f;
                    _hasCount = true;
                }

                if (msg.Count != _count)
                {
                    Count.text = Internal.PresentationUtility.GetNumberString(msg.Count, 999, "999+");
                    _count = msg.Count;
                }
            }
            else if (_hasCount)
            {
                CountContainer.alpha = 0f;
                _hasCount = false;
            }

            // Only update everything else if data context has changed, not just for an update
            if (msg == _prevData)
            {
                return;
            }

            _prevData = msg;

            if(Timestamp!=null)Timestamp.text=msg.TimestampUtc==default?"—":msg.TimestampUtc.ToLocalTime().ToString("HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture);
            Message.text = msg.MessagePreview;
            StackTrace.text = msg.StackTracePreview;

            if (string.IsNullOrEmpty(StackTrace.text) || !StackTrace.gameObject.activeSelf)
            {
                Message.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 2,
                    _rectTransform.rect.height - 4);
            }
            else
            {
                Message.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 12,
                    _rectTransform.rect.height - 14);
            }

            switch (msg.LogType)
            {
                case LogType.Log:
                    ImageStyle.StyleKey = ConsoleBlobInfo;
                    break;

                case LogType.Warning:
                    ImageStyle.StyleKey = ConsoleBlobWarning;
                    break;

                case LogType.Exception:
                case LogType.Assert:
                case LogType.Error:
                    ImageStyle.StyleKey = ConsoleBlobError;
                    break;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            _rectTransform = CachedTransform as RectTransform;
            CountContainer.alpha = 0f;

            Message.supportRichText = Settings.Instance.RichTextInConsole;
        }
    }
}
