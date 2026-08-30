Shader "AnimalCafe/Phase7/FloorSurfaceTiled"
{
    Properties
    {
        _BaseMap ("Floor Surface", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _SurfaceRotationQuarterTurns ("Quarter Turns", Float) = 0
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
            float4 _BaseColor;
            float _SurfaceRotationQuarterTurns;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }
            float2 RotateQuarterTurns(float2 uv, float turns)
            {
                float quarter = fmod(round(turns), 4.0);
                if (quarter == 1.0) return float2(uv.y, 1.0 - uv.x);
                if (quarter == 2.0) return float2(1.0 - uv.x, 1.0 - uv.y);
                if (quarter == 3.0) return float2(1.0 - uv.y, uv.x);
                return uv;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                return tex2D(_BaseMap, RotateQuarterTurns(input.uv, _SurfaceRotationQuarterTurns)) * _BaseColor;
            }
            ENDCG
        }
    }
}
