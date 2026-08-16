using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EmergencyRoad
{
    public sealed class EmergencyWetnessRendererFeature : FullScreenPassRendererFeature
    {
        private static readonly int WetnessId = Shader.PropertyToID("_EmergencyWetness");

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Clear weather pays no depth/normal/full-screen rendering cost.
            if (Shader.GetGlobalFloat(WetnessId) <= .0001f) return;
            base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}
