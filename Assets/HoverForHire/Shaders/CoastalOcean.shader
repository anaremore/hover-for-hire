Shader "Hover for Hire/Coastal Ocean"
{
    Properties { _CoastHeight("Coast height",2D)="black" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_CoastHeight); SAMPLER(sampler_CoastHeight);
            struct A { float4 vertex:POSITION; };
            struct V { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; half fog:TEXCOORD1; };
            V Vert(A i) { V o; o.world=TransformObjectToWorld(i.vertex.xyz); o.vertex=TransformWorldToHClip(o.world); o.fog=ComputeFogFactor(o.vertex.z); return o; }
            float Wave(float2 p,float t) { return sin(p.x*.58+p.y*.37+t*1.3)*.24+sin(p.x*1.24-p.y*.79-t*1.6)*.095+sin(p.y*3.1+p.x*.6+t*2.2)*.025; }
            half4 Frag(V i):SV_Target
            {
                float2 p=i.world.xz; float t=_Time.y;
                float dx=Wave(p+float2(.15,0),t)-Wave(p-float2(.15,0),t);
                float dz=Wave(p+float2(0,.15),t)-Wave(p-float2(0,.15),t);
                float distanceToEye=distance(_WorldSpaceCameraPos,i.world);
                float waveFade=rcp(1+pow(distanceToEye*.018,1.6));
                half3 n=normalize(half3(-dx*waveFade,1,-dz*waveFade)); half3 view=GetWorldSpaceNormalizeViewDir(i.world);
                float2 uv=p/float2(2400,2200)+.5;
                float land=SAMPLE_TEXTURE2D(_CoastHeight,sampler_CoastHeight,uv).r*240-40;
                float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                float depth=lerp(100,max(.1,-3.5-land),inside);
                half3 water=lerp(half3(.025,.28,.26),half3(.016,.075,.12),saturate(depth/24));
                half fresnel=pow(1-saturate(dot(n,view)),4);
                half3 reflected=lerp(half3(.28,.44,.52),half3(.57,.64,.66),saturate(view.y));
                Light sun=GetMainLight(); half3 halfway=normalize(sun.direction+view);
                half glint=pow(saturate(dot(n,halfway)),230)*3.5+pow(saturate(dot(n,halfway)),25)*.08;
                half foam=(1-smoothstep(.25,2.8,depth))*(.4+.6*smoothstep(.35,.75,sin(depth*2.8-t*1.8+Wave(p*.35,t))*.5+.5));
                half3 color=lerp(water,reflected,.15+fresnel*.65)+sun.color*glint;
                // The ocean is one huge quad: evaluate fog per pixel, not at its distant corners.
                color=lerp(color,half3(.66,.77,.72),foam*.7);
                return half4(MixFog(color,ComputeFogFactor(TransformWorldToHClip(i.world).z)),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
