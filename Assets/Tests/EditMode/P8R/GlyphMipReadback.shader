Shader "Hidden/AnimalCafe/Tests/GlyphMipReadback"
{
    Properties
    {
        _MainTex ("Glyph", 2D) = "white" {}
        _MipLevel ("Mip level", Float) = 0
    }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _MipLevel;
            // Fixed display-size mip, independent of the default blit's derivative policy.
            // 测试明确指定显示尺寸对应的 mip，不依赖默认 blit 的隐式 LOD。
            fixed4 frag(v2f_img input) : SV_Target
            {
                return tex2Dlod(_MainTex, float4(input.uv, 0, _MipLevel));
            }
            ENDCG
        }
    }
    Fallback Off
}
