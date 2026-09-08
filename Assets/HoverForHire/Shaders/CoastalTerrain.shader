Shader "Hover for Hire/Coastal Terrain"
{
    Properties
    {
        _Grass("Grass", 2D) = "white" {}
        _Rock("Rock", 2D) = "white" {}
        _Sand("Sand", 2D) = "white" {}
        _GrassNormal("Grass normal", 2D) = "bump" {}
        _RockNormal("Rock normal", 2D) = "bump" {}
        _SandNormal("Sand normal", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Terrain" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Grass); SAMPLER(sampler_Grass);
            TEXTURE2D(_Rock); SAMPLER(sampler_Rock);
            TEXTURE2D(_Sand); SAMPLER(sampler_Sand);
            TEXTURE2D(_GrassNormal); SAMPLER(sampler_GrassNormal);
            TEXTURE2D(_RockNormal); SAMPLER(sampler_RockNormal);
            TEXTURE2D(_SandNormal); SAMPLER(sampler_SandNormal);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half3 weights:TEXCOORD2; half fog:TEXCOORD3; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionWS=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS); o.weights=i.color.rgb; o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 p=i.positionWS; half3 n=normalize(i.normalWS);
                half3 w=max(i.weights,0); w/=max(dot(w,1),.001);
                float2 uv=p.xz/24;
                half3 grass=lerp(SAMPLE_TEXTURE2D(_Grass,sampler_Grass,uv).rgb,SAMPLE_TEXTURE2D(_Grass,sampler_Grass,float2(-uv.y,uv.x)*.73+.37).rgb,.45);
                half3 sand=SAMPLE_TEXTURE2D(_Sand,sampler_Sand,p.xz/18).rgb;
                half3 tri=pow(abs(n),4); tri/=max(dot(tri,1),.001);
                half3 rock=SAMPLE_TEXTURE2D(_Rock,sampler_Rock,p.zy/28).rgb*tri.x+SAMPLE_TEXTURE2D(_Rock,sampler_Rock,p.xz/28).rgb*tri.y+SAMPLE_TEXTURE2D(_Rock,sampler_Rock,p.xy/28).rgb*tri.z;
                half macro=SAMPLE_TEXTURE2D(_Grass,sampler_Grass,p.xz/290).g;
                half3 albedo=(grass*half3(.63,.98,.50)*w.r+rock*half3(.88,.90,.90)*w.g+sand*half3(.90,.89,.82)*w.b)*lerp(.91,1.09,macro);
                half3 detail=UnpackNormal(SAMPLE_TEXTURE2D(_GrassNormal,sampler_GrassNormal,uv))*w.r+UnpackNormal(SAMPLE_TEXTURE2D(_RockNormal,sampler_RockNormal,p.xz/28))*w.g+UnpackNormal(SAMPLE_TEXTURE2D(_SandNormal,sampler_SandNormal,p.xz/18))*w.b;
                n=normalize(n+half3(detail.x,0,detail.y)*.35);
                InputData input=(InputData)0; input.positionWS=p; input.normalWS=n; input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(p); input.shadowCoord=TransformWorldToShadowCoord(p); input.bakedGI=SampleSH(n); input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS); input.shadowMask=1;
                SurfaceData surface=(SurfaceData)0; surface.albedo=albedo; surface.alpha=1; surface.smoothness=.08; surface.occlusion=1;
                half4 color=UniversalFragmentPBR(input,surface); color.rgb=MixFog(color.rgb,i.fog); return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
