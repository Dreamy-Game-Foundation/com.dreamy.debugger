using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Debugger.SceneInspection
{
    internal static class InspectorComponentCatalog
    {
        // Order matters for types that derive from Graphic, Renderer or Collider.
        public static string Icon(Component component)
        {
            switch (component)
            {
                case RectTransform _: return "rect";
                case Transform _: return "transform";
                case Camera _: return "camera";
                case Light _: return "light";
                case Rigidbody _: return "rigidbody";
                case Rigidbody2D _: return "rigidbody-2d";
                case BoxCollider _: return "box-collider";
                case SphereCollider _: return "sphere-collider";
                case CapsuleCollider _: case CapsuleCollider2D _: return "capsule-collider";
                case MeshCollider _: return "mesh-collider";
                case BoxCollider2D _: return "box-collider-2d";
                case CircleCollider2D _: return "circle-collider-2d";
                case Collider _: case Collider2D _: return "collider";
                case Canvas _: return "canvas";
                case CanvasScaler _: return "canvas-scaler";
                case LayoutGroup _: return "layout-group";
                case ContentSizeFitter _: return "content-fitter";
                case CanvasGroup _: return "canvas-group";
                case CanvasRenderer _: return "canvas-renderer";
                case GraphicRaycaster _: return "graphic-raycaster";
                case Image _: return "image";
                case RawImage _: return "raw-image";
                case SpriteRenderer _: return "sprite-renderer";
                case Text _: return "text";
                case ScrollRect _: return "scroll-rect";
                case Toggle _: return "toggle";
                case Slider _: return "slider";
                case InputField _: return "input-field";
                case Selectable _: return "button";
                case AudioSource _: return "audio";
                case AudioListener _: return "audio-listener";
                case Animator _: case Animation _: return "animator";
                case ParticleSystem _: case ParticleSystemRenderer _: return "particle";
                case MeshFilter _: return "mesh-filter";
                case Renderer _: return "mesh";
                case UnityEngine.EventSystems.EventSystem _: case UnityEngine.EventSystems.BaseRaycaster _: return "event";
                case MonoBehaviour script:
                    // Optional URP companions shown beside the native camera and light.
                    var typeName = script.GetType().FullName;
                    if (typeName == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData") return "camera";
                    if (typeName == "UnityEngine.Rendering.Universal.UniversalAdditionalLightData") return "light";
                    return "script";
                default: return "component";
            }
        }

        // Explicit native properties only. Many Unity getters allocate or have side effects.
        public static string[] Properties(Component component)
        {
            switch (component)
            {
                case Camera _: return new[] { "clearFlags", "backgroundColor", "orthographic", "fieldOfView", "orthographicSize", "nearClipPlane", "farClipPlane", "depth", "allowHDR", "allowMSAA" };
                case Light _: return new[] { "type", "color", "intensity", "range", "spotAngle", "shadows" };
                case Rigidbody _: return new[] { "mass", "linearDamping", "angularDamping", "useGravity", "isKinematic", "interpolation", "collisionDetectionMode", "constraints" };
                case Rigidbody2D _: return new[] { "bodyType", "mass", "linearDamping", "angularDamping", "gravityScale", "interpolation", "collisionDetectionMode", "constraints", "simulated" };
                case BoxCollider _: return new[] { "enabled", "isTrigger", "center", "size" };
                case SphereCollider _: return new[] { "enabled", "isTrigger", "center", "radius" };
                case CapsuleCollider _: return new[] { "enabled", "isTrigger", "center", "radius", "height", "direction" };
                case MeshCollider _: return new[] { "enabled", "isTrigger", "convex" };
                case Collider _: return new[] { "enabled", "isTrigger" };
                case BoxCollider2D _: return new[] { "isTrigger", "offset", "size", "edgeRadius" };
                case CircleCollider2D _: return new[] { "isTrigger", "offset", "radius" };
                case CapsuleCollider2D _: return new[] { "isTrigger", "offset", "size", "direction" };
                case Collider2D _: return new[] { "isTrigger", "offset" };
                case AudioSource _: return new[] { "volume", "pitch", "mute", "loop", "playOnAwake", "spatialBlend", "minDistance", "maxDistance", "priority" };
                case Animator _: return new[] { "speed", "applyRootMotion", "updateMode", "cullingMode" };
                case Image _: return new[] { "sprite", "color", "raycastTarget", "type", "preserveAspect", "fillAmount" };
                case RawImage _: return new[] { "texture", "color", "raycastTarget" };
                case Text _: return new[] { "text", "fontSize", "color", "alignment", "raycastTarget" };
                case Slider _: return new[] { "interactable", "wholeNumbers", "minValue", "maxValue", "value", "direction" };
                case Toggle _: return new[] { "interactable", "isOn" };
                case InputField _: return new[] { "interactable", "text", "readOnly", "characterLimit" };
                case Selectable _: return new[] { "interactable" };
                case ScrollRect _: return new[] { "horizontal", "vertical", "movementType", "inertia", "scrollSensitivity" };
                case CanvasGroup _: return new[] { "alpha", "interactable", "blocksRaycasts", "ignoreParentGroups" };
                case Canvas _: return new[] { "renderMode", "sortingOrder", "overrideSorting", "pixelPerfect" };
                case MeshFilter _: return new[] { "sharedMesh" };
                case SpriteRenderer _: return new[] { "enabled", "sprite", "color", "flipX", "flipY", "sortingOrder" };
                case Renderer _: return new[] { "enabled", "sortingOrder", "shadowCastingMode", "receiveShadows" };
                default: return System.Array.Empty<string>();
            }
        }
    }
}
