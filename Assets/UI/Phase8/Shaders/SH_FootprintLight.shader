Shader "AnimalCafe/Phase8/FootprintLight"
{
    Properties
    {
        _BaseColor ("Validity Tint", Color) = (0.18, 0.72, 0.32, 1)
        _TintBrightness ("Tint Brightness", Range(1, 6)) = 6
        _TintSaturation ("Tint Saturation", Range(1, 3)) = 3
        _LightIntensity ("Signal Light Intensity", Range(1, 2)) = 1.5
        _FootprintOpacity ("Light Opacity", Range(0, 1)) = 0.45
        _EdgeSoftness ("Inward Edge Softness", Range(0.001, 0.45)) = 0.12
        [HideInInspector] _ZWrite ("Z Write", Float) = 0
        [HideInInspector] _ZTest ("Z Test", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Cull Off
        Pass
        {
            Name "FootprintLight"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _TintBrightness;
                half _TintSaturation;
                half _LightIntensity;
                half _FootprintOpacity;
                half _EdgeSoftness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Fade inside the same quad. No larger mesh, Light, Bloom or theme-alpha dependency.
                // 软边只向真实占用边界内部衰减，Theme 的 alpha=1 不会让投影变成实心板。
                half distanceToEdge = min(min(input.uv.x, 1 - input.uv.x), min(input.uv.y, 1 - input.uv.y));
                half edge = smoothstep(0, max(_EdgeSoftness, 0.001h), distanceToEdge);
                // Vivid footprint color only: keep the shared Theme and model tint unchanged.
                // 只提亮投影的 RGB；不靠增加 alpha 遮住底面，也不按颜色猜测 valid/invalid。
                half luminance = dot(_BaseColor.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 vividTint = lerp(luminance.xxx, _BaseColor.rgb, _TintSaturation);
                // Permit a modest signal-light RGB peak above 1; alpha still controls the same blend.
                // 只增强投影自身的发光值，不加场景灯或 Bloom；显示端仍可能截断亮底的主色通道。
                half3 lightTint = saturate(vividTint * _TintBrightness) * clamp(_LightIntensity, 1.0h, 2.0h);
                half opacity = saturate(_FootprintOpacity) * edge;
                // Premultiply before the LDR target clamps source RGB; keep C*alpha + background*(1-alpha).
                // 预乘让发光值在 LDR 中也能显示；柔边与底面的混合比例不变。
                return half4(lightTint * opacity, opacity);
            }
            ENDHLSL
        }
    }
}
