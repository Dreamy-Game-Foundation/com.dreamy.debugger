using UnityEngine;
using UnityEngine.EventSystems;

namespace Dreamy.Debugger.SceneInspection
{
    public sealed class SceneSplitHandle : MonoBehaviour, IDragHandler
    {
        public SceneTabController Owner;
        public void OnDrag(PointerEventData data)
        {
            var rect = (RectTransform)Owner.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var point) && rect.rect.width > 0)
                Owner.SplitRatio = Mathf.Clamp((point.x - rect.rect.xMin) / rect.rect.width, 240f / rect.rect.width, Mathf.Min(.5f, 1f - 420f / rect.rect.width));
        }
    }
}
