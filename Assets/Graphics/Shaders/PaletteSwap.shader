Shader "Hidden/PaletteSwap"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off

        Pass
        {
            Name "PaletteSwap"
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _TargetColor0;
            float4 _TargetColor1;
            float4 _TargetColor2;
            float4 _TargetColor3;

            // 明るさの境界線（微調整可能）
            // 0.25, 0.50, 0.75 あたりが標準的
            static const float Threshold1 = 0.1;
            static const float Threshold2 = 0.4;
            static const float Threshold3 = 0.9;

            half4 Frag (Varyings input) : SV_Target
            {
                // WebGL対策：PointClampでパキッと取る
                float4 c = SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, input.texcoord);

                // 透明な部分は透明のまま返す
                if (c.a < 0.01) return c;

                float luma = c.r;

                // 明るさに応じてターゲット色を返す
                float4 finalColor = _TargetColor3;

                if (luma >= Threshold3)
                {
                    finalColor = _TargetColor0;
                }
                else if (luma >= Threshold2)
                {
                    finalColor = _TargetColor1;
                }
                else if (luma >= Threshold1)
                {
                    finalColor = _TargetColor2;
                }

                // アルファ値を元の画像から復元
                finalColor.a = c.a;
                return finalColor;
            }
            ENDHLSL
        }
    }
}