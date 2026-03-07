Shader "Custom/RetroOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        _Sharpness ("Sharpness", Range(0, 1)) = 0.2

        _EdgeThreshold ("Edge Threshold", Range(0.001, 1)) = 0.08
        _EdgeStrength ("Edge Strength", Range(0, 2)) = 1.0
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)

        _UseLumaForEdges ("Use Luma For Edges", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _Sharpness;
            float _EdgeThreshold;
            float _EdgeStrength;
            float4 _OutlineColor;
            float _UseLumaForEdges;

            v2f Vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Luma(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            float EdgeMetric(float3 center, float3 n, float3 e, float3 s, float3 w)
            {
                // Color-based edge metric
                float colorDiff =
                    length(center - n) +
                    length(center - e) +
                    length(center - s) +
                    length(center - w);

                // Optional luma-based edge metric, often more stable on stylized images
                float lc = Luma(center);
                float ln = Luma(n);
                float le = Luma(e);
                float ls = Luma(s);
                float lw = Luma(w);

                float lumaDiff =
                    abs(lc - ln) +
                    abs(lc - le) +
                    abs(lc - ls) +
                    abs(lc - lw);

                return lerp(colorDiff, lumaDiff, _UseLumaForEdges);
            }

            float4 Frag(v2f i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;

                float4 c = tex2D(_MainTex, i.uv);
                float4 n = tex2D(_MainTex, i.uv + float2(0,  texel.y));
                float4 e = tex2D(_MainTex, i.uv + float2(texel.x, 0));
                float4 s = tex2D(_MainTex, i.uv + float2(0, -texel.y));
                float4 w = tex2D(_MainTex, i.uv + float2(-texel.x, 0));

                // 1) Sharpness: same idea as your reference shader
                float neighbor = -_Sharpness;
                float center   = 1.0 + 4.0 * _Sharpness;

                float3 sharpened =
                    n.rgb * neighbor +
                    e.rgb * neighbor +
                    c.rgb * center +
                    s.rgb * neighbor +
                    w.rgb * neighbor;

                sharpened = saturate(sharpened);

                // 2) Cheap outlines from color/luma discontinuity
                float edge = EdgeMetric(c.rgb, n.rgb, e.rgb, s.rgb, w.rgb);

                // Threshold and scale
                edge = saturate((edge - _EdgeThreshold) / max(1e-5, _EdgeThreshold));
                edge *= _EdgeStrength;
                edge = saturate(edge);

                // 3) Blend outline over sharpened result
                float3 finalColor = lerp(sharpened, _OutlineColor.rgb, edge);

                return float4(finalColor, c.a);
            }
            ENDHLSL
        }
    }
}