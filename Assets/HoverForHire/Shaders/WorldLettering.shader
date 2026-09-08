Shader "Hover for Hire/World Lettering"
{
    Properties { _BaseMap("Font atlas",2D)="white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; half fog:TEXCOORD1; };
            V Vert(A i) { V o; o.vertex=TransformObjectToHClip(i.vertex.xyz);o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(o.vertex.z);return o; }
            half4 Frag(V i):SV_Target
            {
                // Dynamic font atlases store coverage in alpha; their RGB is not glyph color.
                return half4(MixFog(i.color.rgb,i.fog),SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*i.color.a);
            }
            ENDHLSL
        }
    }
}
