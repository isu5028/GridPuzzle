Shader "Custom/DitherDissolve"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _DitherPattern ("Dither Pattern (optional)", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _PatternScale ("Block Size (Pixels)", Float) = 1
        _DitherAspect ("Dither Aspect Compensation", Float) = 1
        _UseScreenSpaceDither ("Use Screen Space Dither", Float) = 0
        _UseTexture ("Use Texture Pattern", Float) = 0

        [Header(Sweep Settings)]
        _SweepDirection ("Sweep Direction (0=BottomLeft, 1=BottomRight, 2=TopRight, 3=TopLeft)", Float) = 0
        _SweepSpread ("Sweep Spread", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _DitherPattern;
            float4 _MainTex_ST;
            float _DissolveAmount;
            float _PatternScale;
            float _DitherAspect;
            float _UseScreenSpaceDither;
            float _UseTexture;
            float _SweepDirection;
            float _SweepSpread;

            // 8x8 Bayer Matrix (0-63を0-1に正規化)
            static const float BayerMatrix8x8[64] = {
                0/64.0,  32/64.0,  8/64.0, 40/64.0,  2/64.0, 34/64.0, 10/64.0, 42/64.0,
                48/64.0, 16/64.0, 56/64.0, 24/64.0, 50/64.0, 18/64.0, 58/64.0, 26/64.0,
                12/64.0, 44/64.0,  4/64.0, 36/64.0, 14/64.0, 46/64.0,  6/64.0, 38/64.0,
                60/64.0, 28/64.0, 52/64.0, 20/64.0, 62/64.0, 30/64.0, 54/64.0, 22/64.0,
                3/64.0,  35/64.0, 11/64.0, 43/64.0,  1/64.0, 33/64.0,  9/64.0, 41/64.0,
                51/64.0, 19/64.0, 59/64.0, 27/64.0, 49/64.0, 17/64.0, 57/64.0, 25/64.0,
                15/64.0, 47/64.0,  7/64.0, 39/64.0, 13/64.0, 45/64.0,  5/64.0, 37/64.0,
                63/64.0, 31/64.0, 55/64.0, 23/64.0, 61/64.0, 29/64.0, 53/64.0, 21/64.0
            };

            // GetBayerValueはfrag内に統合しました


            // スイープ方向のグラデーションを計算（アスペクト比補正付き）
            float GetSweepGradient(float2 uv, float aspect)
            {
                // アスペクト比に基づいて座標系をスケーリング
                // 1ユニットの移動が物理的に同じ距離になるようにする
                float2 scale = float2(1, 1);
                if (aspect > 1.0)
                {
                    scale.x = aspect;
                }
                else
                {
                    scale.y = 1.0 / aspect;
                }

                float2 pos = uv * scale;
                float gradient = 0;
                int direction = (int)_SweepDirection;

                // 物理的な距離計算に基づいたグラデーション
                if (direction == 0) // 左下から右上
                {
                    gradient = (pos.x + pos.y);
                }
                else if (direction == 1) // 右下から左上
                {
                    gradient = (scale.x - pos.x) + pos.y;
                }
                else if (direction == 2) // 右上から左下
                {
                    gradient = (scale.x - pos.x) + (scale.y - pos.y);
                }
                else // 左上から右下
                {
                    gradient = pos.x + (scale.y - pos.y);
                }

                // 全体の移動距離で正規化して 0-1 に収める
                return gradient / (scale.x + scale.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // アスペクト比 (Width / Height)
                float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;

                // ブロックサイズ（1つの正方形のピクセル数）
                float blockSize = max(1.0, _PatternScale);
                float ditherAspect = max(0.0001, _DitherAspect);

                // 画像のピクセル座標に変換（テクスチャ空間）
                float2 texturePixelPos = i.uv * _MainTex_TexelSize.zw;
                // 非等方スケール時でもディザセルを正方形に保つため、X軸のみ補正
                texturePixelPos.x *= ditherAspect;

                // ディザ座標を決定
                // - テクスチャ空間: スプライト拡大時にディザも拡大される
                // - スクリーン空間: スプライト拡大の影響を受けず、画面ピクセル基準で一定
                float2 ditherPos = texturePixelPos;
                bool useScreenSpaceDither = (_UseScreenSpaceDither > 0.5);
                if (useScreenSpaceDither)
                {
                    float invW = 1.0 / max(0.0001, i.screenPos.w);
                    float2 screenUV = i.screenPos.xy * invW;
                    ditherPos = screenUV * _ScreenParams.xy;
                }

                // 座標をブロック単位で丸める（量子化）
                // これにより、blockSize x blockSize の範囲内で全ての計算結果が同一になり、
                // 中途半端に削れた形が出ず、綺麗な正方形だけで構成されるようになります。
                float2 blockPos = floor(ditherPos / blockSize) * blockSize;

                // スイープ方向はUVベースを維持（テクスチャ空間時のみ量子化座標を使用）
                float2 sweepUV = i.uv;
                if (!useScreenSpaceDither)
                {
                    sweepUV = blockPos / _MainTex_TexelSize.zw;
                }

                float threshold;
                if (_UseTexture > 0.5)
                {
                    // テクスチャ使用時（64x64単位で繰り返し）
                    float2 patternUV = frac(blockPos / (blockSize * 64.0));
                    threshold = tex2D(_DitherPattern, patternUV).r;
                }
                else
                {
                    // Bayer Matrixを使用
                    // ブロックのインデックスから 0-63 の位置を決定
                    float2 blockIndex = floor(blockPos / blockSize);
                    float2 bayerPos = frac(blockIndex / 8.0) * 8.0;
                    int2 bayerIndex = int2(floor(bayerPos));
                    threshold = BayerMatrix8x8[bayerIndex.y * 8 + bayerIndex.x];
                }

                float sweepGradient = GetSweepGradient(sweepUV, aspect);

                // ディゾルブ進行度を計算（量子化されているので一定の範囲が同時に変化する）
                float localDissolve = _DissolveAmount * (1.0 + _SweepSpread) - sweepGradient * _SweepSpread;

                // ディゾルブ処理
                clip(threshold - localDissolve);

                return col;
            }
            ENDCG
        }
    }
}
