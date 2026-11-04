namespace Dreamy.Debugger.Presentation.UI.Other
{
    using System;
    using Controls;
    using Dreamy.Debugger.Foundation;
    using UnityEngine;
    using UnityEngine.Serialization;

    public class DebugTab : ComponentBehaviour
    {
        /// <summary>
        /// Content that will be added to the content area of the header
        /// </summary>
        public RectTransform HeaderExtraContent;

        [Obsolete] [HideInInspector] public Sprite Icon;

        /// <summary>
        /// Content that will be added to the content area of the tab button
        /// </summary>
        public RectTransform IconExtraContent;

        public string IconStyleKey = "Icon_Stompy";
        public int SortIndex;

        [HideInInspector] public DebugTabButton TabButton;

        public string Title
        {
            get { return _title; }
        }

        public string LongTitle
        {
            get { return !string.IsNullOrEmpty(_longTitle) ? _longTitle : _title; }
        }

        public string Key
        {
            get { return _key; }
        }
#pragma warning disable 649

        [SerializeField] [FormerlySerializedAs("Title")] private string _title;

        [SerializeField] private string _longTitle;

        [SerializeField] private string _key;

#pragma warning restore 649
    }
}
