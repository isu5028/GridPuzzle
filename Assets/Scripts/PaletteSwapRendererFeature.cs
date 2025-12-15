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
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                return;

            if (resourceData.isActiveTargetBackBuffer || material == null)
                return;

            TextureHandle source = resourceData.activeColorTexture;

            // ディスクリプタ設定（WebGL向けにMSAAを1に強制）
            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;

            TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_PaletteSwapTempTexture", false);

            // Pass 1: 適用 (Source -> Temp)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName, out var passData))
            {
                passData.material = material;
                passData.source = source;
                passData.destination = tempTexture;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // 【重要】明示的に _MainTex をセットする
                    data.material.SetTexture("_MainTex", data.source);

                    // シェーダーのプロパティがグローバル変数を受け取れるよう、キーワード等を更新
                    // (ShaderGraphによっては不要ですが、念のため)

                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Pass 2: 書き戻し (Temp -> Source)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Palette Swap Copy Back", out var passData))
            {
                passData.source = tempTexture;
                passData.destination = source;

                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        // Legacy execute is empty
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