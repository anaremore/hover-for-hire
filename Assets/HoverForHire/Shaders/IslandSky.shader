Shader "Hover for Hire/Island Sky"
{
    Properties { _SunDirection("Sun direction",Vector)=(0,1,0,0) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _SunDirection; CBUFFER_END
            struct A { float4 position:POSITION; }; struct V { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
            V Vert(A i) { V o; o.position=TransformObjectToHClip(i.position.xyz); o.direction=i.position.xyz; return o; }
            float Hash(float2 p) { p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
            float Noise(float2 p) { float2 a=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y); }
            float Fbm(float2 p) { return Noise(p)*.52+Noise(p*2.03)*.26+Noise(p*4.1)*.13+Noise(p*8.3)*.065; }
            half4 Frag(V i):SV_Target
            {
                float3 d=normalize(i.direction); float h=saturate(d.y);
                half3 color=lerp(half3(.60,.68,.69),half3(.13,.32,.53),pow(h,.45));
                float sun=saturate(dot(d,normalize(_SunDirection.xyz)));
                color+=half3(1,.63,.29)*pow(sun,12)*.25+half3(1,.77,.43)*pow(sun,700)*1.5+half3(4,3,1.8)*smoothstep(.9996,.99985,sun);
                float2 p=d.xz/max(.035,d.y)*1.4+float2(_Time.y*.0012,0);
                float clouds=Fbm(p); float cloud=smoothstep(.51,.73,clouds)*smoothstep(.13,.32,d.y);
                half3 cloudColor=lerp(half3(.42,.49,.55),half3(.94,.88,.74),saturate((clouds-.48)*4+sun*.25));
                color=lerp(color,cloudColor,cloud*.92); return half4(color,1);
            }
            ENDHLSL
        }
    }
}
