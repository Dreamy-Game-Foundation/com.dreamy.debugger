using UnityEngine;
using UnityEngine.UI;
using Dreamy.Debugger.ThirdParty.Graphy;

namespace Dreamy.Debugger
{
    /// <summary>Graphy renderer fed by the shared Dreamy sampler; no second collector.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class GraphyHudGraph : MonoBehaviour
    {
        [SerializeField] private Shader graphShader;
        private const int Capacity = 128;
        private readonly float[] samples = new float[Capacity];
        private int cursor;
        private Material ownedMaterial;
        private G_GraphShader renderer;
        public bool IsAvailable => graphShader != null && graphShader.isSupported;
        public bool IsInitialized => renderer != null;

        public void Configure(DebuggerConfiguration config)
        {
            if (config == null) throw new System.ArgumentNullException(nameof(config));
            GetComponent<Image>().enabled = IsAvailable;
            if (!IsAvailable || !gameObject.activeInHierarchy) return;
            if (renderer == null)
            {
                var image = GetComponent<Image>();
                ownedMaterial = new Material(graphShader) { name = "Dreamy HUD Graph (instance)" };
                image.material = ownedMaterial;
                renderer = new G_GraphShader { Image = image, ArrayMaxSize = Capacity,
                    ShaderArrayValues = new float[Capacity] };
                renderer.InitializeShader();
                renderer.UpdateArrayValuesLength();
            }
            renderer.GoodColor = config.FpsGood;
            renderer.CautionColor = config.FpsCaution;
            renderer.CriticalColor = config.FpsCritical;
            renderer.UpdateColors();
        }

        public void Push(float fps, float average, DebuggerConfiguration config)
        {
            if (renderer == null && config != null) Configure(config);
            if (renderer == null || config == null || fps <= 0 || float.IsNaN(fps) || float.IsInfinity(fps)) return;
            samples[cursor] = fps;
            cursor = (cursor + 1) % Capacity;
            var maximum = Mathf.Max(config.GoodFps * 1.15f, 1);
            for (var i = 0; i < Capacity; i++) maximum = Mathf.Max(maximum, samples[i]);
            for (var i = 0; i < Capacity; i++) renderer.ShaderArrayValues[i] = samples[(cursor + i) % Capacity] / maximum;
            renderer.Average = average / maximum;
            renderer.GoodThreshold = config.GoodFps / maximum;
            renderer.CautionThreshold = config.CautionFps / maximum;
            renderer.UpdatePoints();
            renderer.UpdateAverage();
            renderer.UpdateThresholds();
        }

        private void OnDestroy()
        {
            if (ownedMaterial != null) Destroy(ownedMaterial);
        }
    }
}
