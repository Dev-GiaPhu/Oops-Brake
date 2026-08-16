using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EmergencyRoad
{
    public sealed class EmergencyWetnessRendererFeature : FullScreenPassRendererFeature
    {
        private static readonly int WetnessId = Shader.PropertyToID("_EmergencyWetness");
        private static readonly int WarmupId = Shader.PropertyToID("_EmergencyWetnessWarmup");

#if UNITY_EDITOR
        // Compile the pass while the scene is still being edited instead of waiting
        // for the first rain/night transition during Play Mode.
        private int editorWarmupRendersRemaining = 4;
#endif

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            bool warmupRequested = Shader.GetGlobalFloat(WarmupId) > .5f;
#if UNITY_EDITOR
            if (!Application.isPlaying && editorWarmupRendersRemaining > 0)
            {
                editorWarmupRendersRemaining--;
                warmupRequested = true;
            }
#endif

            // Clear weather still has zero continuing cost. The pass runs only for
            // a few startup renders so URP can prepare its Lit/Depth/Normal variants.
            if (!warmupRequested && Shader.GetGlobalFloat(WetnessId) <= .0001f) return;
            base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}
