Shader "Custom/RhythmRingWaterRipple"
{
    Properties
    {
        _Color ("Ring Color", Color) = (0.1, 0.85, 1.0, 1.0)
        _NormalMap ("Water Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Float) = 0.08
        _NormalSpeed ("Normal Speed", Float) = 0.6
        _DistortionStrength ("Distortion Strength", Float) = 0.12
        _EmissionIntensity ("Glow Intensity", Float) = 2.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+50" 
            "RenderPipeline"="UniversalPipeline" 
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "RhythmRingPass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _NormalScale;
                float _NormalSpeed;
                float _DistortionStrength;
                float _EmissionIntensity;
            CBUFFER_END

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 물결 노멀에 맞춰 월드 좌표 기반 UV 계산
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.worldPos = worldPos;

                float time = _Time.y * _NormalSpeed;
                float2 normalUV = worldPos.xz * _NormalScale + float2(time * 0.05, time * 0.03);
                
                // 노멀 맵에서 물결 방향 샘플링하여 버텍스 살짝 오프셋
                float4 normalSample = SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, normalUV, 0);
                float3 unpackNorm = normalSample.rgb * 2.0 - 1.0;
                
                worldPos.xz += unpackNorm.xy * _DistortionStrength * 0.35;
                worldPos.y += unpackNorm.z * 0.02;

                output.positionCS = TransformWorldToHClip(worldPos);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _NormalSpeed;
                float2 normalUV = input.worldPos.xz * _NormalScale + float2(time * 0.05, time * 0.03);
                float4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV);
                float3 unpackNorm = normalSample.rgb * 2.0 - 1.0;

                // 물결 곡면에 따라 빛나는 글로우 강조
                float rippleGlow = 1.0 + unpackNorm.z * 0.45;
                float4 finalColor = input.color * _EmissionIntensity * rippleGlow;
                return finalColor;
            }
            ENDHLSL
        }
    }
}
