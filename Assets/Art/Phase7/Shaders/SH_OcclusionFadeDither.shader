Shader "AnimalCafe/Phase7/OcclusionFadeDither"
{
    Properties { _BaseMap ("Base Map", 2D) = "white" {} _BaseColor ("Color", Color) = (1,1,1,1) _FadeOpacity ("Fade", Range(0,1)) = 1 }
    SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Blend SrcAlpha OneMinusSrcAlpha
        Pass { CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _BaseMap; float4 _BaseColor; float _FadeOpacity;
        struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; }; struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
        v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
        fixed4 frag(v2f i):SV_Target { fixed4 appearance=tex2D(_BaseMap,i.uv)*_BaseColor; appearance.a*=_FadeOpacity; return appearance; }
        ENDCG }
    }
}
