Shader "AnimalCafe/Phase7/WallSurfaceLayered"
{
    Properties
    {
        _BaseMap ("Base Surface", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _WainscotingMap ("Wainscoting", 2D) = "white" {}
        _WainscotingColor ("Wainscoting Color", Color) = (1,1,1,1)
        _WainscotingEnabled ("Wainscoting Enabled", Float) = 0
        _WainscotingCutoff ("Wainscoting Cutoff", Range(0,1)) = 0
        _WallpaperTiling ("Wallpaper Tiling", Vector) = (1,1,0,0)
        _WallpaperReliefStrength ("Wallpaper Relief Strength", Range(0,0.08)) = 0.04
        _SelectionHighlight ("Selection Highlight", Range(0,1)) = 0
        _SelectionColor ("Selection Color", Color) = (0.25,0.85,0.45,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_TexelSize;
            sampler2D _WainscotingMap;
            float4 _BaseColor;
            float4 _WainscotingColor;
            float _WainscotingEnabled;
            float _WainscotingCutoff;
            float4 _WallpaperTiling;
            float _WallpaperReliefStrength;
            float _SelectionHighlight;
            float4 _SelectionColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 baseUv = input.uv * _WallpaperTiling.xy;
                fixed4 baseSample = tex2D(_BaseMap, baseUv);
                fixed4 baseColor = baseSample * _BaseColor;
                // Sample two neighbouring texels as a tiny height cue. Uniform Paint
                // remains unchanged; patterned Wallpaper receives only a restrained
                // light/dark response and the mesh/collider stay completely flat.
                float baseLuma = dot(baseSample.rgb, float3(0.299, 0.587, 0.114));
                float rightLuma = dot(tex2D(_BaseMap, baseUv + float2(_BaseMap_TexelSize.x, 0)).rgb, float3(0.299, 0.587, 0.114));
                float upperLuma = dot(tex2D(_BaseMap, baseUv + float2(0, _BaseMap_TexelSize.y)).rgb, float3(0.299, 0.587, 0.114));
                float wallpaperRelief = clamp(1.0 + ((baseLuma - rightLuma) + (baseLuma - upperLuma)) * _WallpaperReliefStrength, 0.96, 1.04);
                baseColor.rgb *= wallpaperRelief;
                fixed4 surfaceColor = baseColor;
                // Canonical wall meshes author V upward as UV decreases, so normalize
                // visual height before applying the lower-wall cutoff.
                float wallUvY = 1.0 - input.uv.y;
                if (_WainscotingEnabled > 0.5 && wallUvY <= _WainscotingCutoff)
                {
                    float2 wainscotingUv = float2(input.uv.x * _WallpaperTiling.x, wallUvY / max(_WainscotingCutoff, 0.0001));
                    fixed4 panelColor = tex2D(_WainscotingMap, wainscotingUv) * _WainscotingColor;
                    // A tiny luminance-derived light response suggests panel relief while
                    // remaining one flat render surface (no displaced vertices/colliders).
                    float panelLuma = dot(panelColor.rgb, float3(0.299, 0.587, 0.114));
                    float wainscotingShade = lerp(0.94, 1.04, saturate(panelLuma));
                    surfaceColor = fixed4(panelColor.rgb * wainscotingShade, panelColor.a);
                }

                return lerp(surfaceColor, surfaceColor * 0.72 + _SelectionColor * 0.28, _SelectionHighlight);
            }
            ENDCG
        }
    }
}
