using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GlitchRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class GlitchSettings
    {
        public bool isEnabled = true;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public Material material = null;
    }

    public GlitchSettings settings = new GlitchSettings();

    class GlitchRenderPass : ScriptableRenderPass
    {
        public Material glitchMaterial;
        private RTHandle source;
        private RTHandle tempTexture;

        public GlitchRenderPass(Material material)
        {
            this.glitchMaterial = material;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Unity 6 / URP 17+ uses cameraColorTargetHandle
            source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderingUtils.ReAllocateIfNeeded(ref tempTexture, cameraTextureDescriptor, name: "_TempGlitchTexture");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (glitchMaterial == null) return;
            if (glitchMaterial.GetFloat("_Intensity") <= 0f) return;

            CommandBuffer cmd = CommandBufferPool.Get("GlitchEffect");

            // Use Blitter for Unity 6 compatibility
            // Blit source -> temp (with effect)
            Blitter.BlitCameraTexture(cmd, source, tempTexture, glitchMaterial, 0);
            // Blit temp -> source
            Blitter.BlitCameraTexture(cmd, tempTexture, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            tempTexture?.Release();
        }
    }

    GlitchRenderPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new GlitchRenderPass(settings.material);
        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.isEnabled && settings.material != null)
        {
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass?.Dispose();
    }
}
