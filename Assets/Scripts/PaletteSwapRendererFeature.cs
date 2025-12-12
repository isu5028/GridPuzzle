using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PaletteSwapRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();

    class PaletteSwapPass : ScriptableRenderPass
    {
        private Material material;
        private const string k_PassName = "Palette Swap Pass";

        private class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle destination;
        }

        public PaletteSwapPass(Material material)
        {
            this.material = material;
            requiresIntermediateTexture = true; // Required for BlitCameraTexture logic to ensure we have a valid copy target if needed
        }

        // RenderGraph implementation (Unity 6 / URP 17+)
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                return;

            if (resourceData.isActiveTargetBackBuffer || material == null)
                return;

            TextureHandle source = resourceData.activeColorTexture;

            // Create a temporary texture for the effect
            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_PaletteSwapTempTexture", false);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName, out var passData))
            {
                passData.material = material;
                passData.source = source;
                passData.destination = tempTexture;

                // We are reading from source and writing to tempTexture
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write); // Index 0 fixed

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // For raster passes, we simply draw a full screen triangle
                    // The shader fallback or Blit logic handles the sampling
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Copy back temp -> source
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Palette Swap Copy Back", out var passData))
            {
                passData.source = tempTexture;
                passData.destination = source;

                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write); // Index 0 fixed

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        // Legacy Execute left empty or threw exception if strict, but safe to leave empty if RecordRenderGraph is called
        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {}
    }

    PaletteSwapPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new PaletteSwapPass(settings.material);
        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null) return;
        renderer.EnqueuePass(m_ScriptablePass);
    }
}
