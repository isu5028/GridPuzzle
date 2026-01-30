Shader "Hidden/Glitch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 1)) = 0
        _TimeX ("Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            ZTest Always ZWrite Off Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Intensity;
            float _TimeX;

            float rand(float2 n) {
                return frac(sin(dot(n, float2(12.9898, 4.1414))) * 43758.5453);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _TimeX;

                // Jitter
                float jitter = rand(float2(t, uv.y));
                float shake = (jitter - 0.5) * _Intensity * 0.1;

                // Drift
                float drift = sin(uv.y * 10.0 + t * 5.0) * _Intensity * 0.05;

                // Combine
                float offset = shake + drift;

                // Chromatic Aberration
                fixed4 r = tex2D(_MainTex, uv + float2(offset + _Intensity * 0.02, 0));
                fixed4 g = tex2D(_MainTex, uv + float2(offset, 0));
                fixed4 b = tex2D(_MainTex, uv + float2(offset - _Intensity * 0.02, 0));

                fixed4 col = fixed4(r.r, g.g, b.b, 1.0);

                // Scanlines / Noise (Optional)
                if(rand(uv + t) > 0.95) col.rgb += _Intensity * 0.5;

                return col;
            }
            ENDCG
        }
    }
}
