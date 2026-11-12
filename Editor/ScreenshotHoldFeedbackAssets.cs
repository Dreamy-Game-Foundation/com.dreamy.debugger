using System.Linq;
using Dreamy.Debugger.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

namespace Dreamy.Debugger.Editor
{
    public static class ScreenshotHoldFeedbackAssets
    {
        public const string PrefabPath = "Packages/com.dreamy.debugger/Runtime/Prefabs/ScreenshotHoldFeedback.prefab";
        [MenuItem("Tools/Dreamy Debugger/Author Restore Hold Feedback")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode before authoring feedback.");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Restore hold feedback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(RestoreHoldFeedbackView));
                SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 32767;
                var group = root.GetComponent<CanvasGroup>(); group.ignoreParentGroups = true; group.interactable = group.blocksRaycasts = false;
                var view = root.GetComponent<RestoreHoldFeedbackView>(); view.Scaler = root.GetComponent<CanvasScaler>();
                view.Scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                var safe = Node(root.transform, "Safe area"); safe.gameObject.AddComponent<SafeArea>();
                safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
                var card = Node(safe, "Hold progress", typeof(Image)); view.Card = card;
                card.anchorMin = card.anchorMax = new Vector2(0, 1); card.pivot = new Vector2(0, 1);
                card.anchoredPosition = new Vector2(8, -8); card.sizeDelta = new Vector2(300, 72);
                var fill = card.GetComponent<Image>(); fill.color = new Color(Theme.Surface.r, Theme.Surface.g, Theme.Surface.b, .97f);
                fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.dreamy.debugger/Runtime/Branding/Material/rounded-fill.png");
                fill.type = Image.Type.Sliced; fill.raycastTarget = false;
                var ring = Node(card, "Progress ring", typeof(RestoreHoldRingGraphic)); Fixed(ring, 9, 11, 50, 50);
                view.Ring = ring.GetComponent<RestoreHoldRingGraphic>(); view.Ring.color = Theme.Accent; view.Ring.raycastTarget = false;
                var percent = Label(card, "Percent", "0%", 12, Theme.Ivory); Fixed(percent.rectTransform, 9, 11, 50, 50);
                percent.alignment = TextAnchor.MiddleCenter; view.Percent = percent;
                var title = Label(card, "Title", "Hold to restore UI", 16, Theme.Ivory); Across(title.rectTransform, 9, 27); view.Title = title;
                var subtitle = Label(card, "Hint", "Release to cancel", 12, Theme.Muted); Across(subtitle.rectTransform, 36, 25); view.Hint = subtitle;
                // No GraphicRaycaster or interactive controls: gameplay input always passes through.
                root.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var catalog = AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(DebuggerAssetBuilder.CatalogPath);
            var entries = catalog.Entries.Where(entry => entry.Key != "DreamyDebugger/ScreenshotHoldFeedback").ToList();
            entries.Add(new CatalogEntry { Key = "DreamyDebugger/ScreenshotHoldFeedback", Asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) });
            catalog.Entries = entries.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
        }
        private static RectTransform Node(Transform parent, string name, params System.Type[] types)
        {
            var go = new GameObject(name, new[] { typeof(RectTransform) }.Concat(types).ToArray()); go.layer = 5; go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        private static void Fixed(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top); rect.sizeDelta = new Vector2(width, height);
        }
        private static void Across(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(70, -top - height); rect.offsetMax = new Vector2(-6, -top);
        }
        private static Text Label(Transform parent, string name, string text, int size, Color color)
        {
            var rect = Node(parent, name, typeof(Text)); var label = rect.GetComponent<Text>();
            label.text = text; label.color = color; label.fontSize = size; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            label.font = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dreamy.debugger/Runtime/Branding/Material/Barlow-Regular.ttf");
            return label;
        }
    }
}
