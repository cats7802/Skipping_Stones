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

        Blend SrcAlpha OneMinusSrcAlpha
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
                
                // 🌟 수면의 실제 월드 절대 좌표(worldPos.xz)를 기준으로 물결 노멀 샘플링
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.worldPos = worldPos;

                float time = _Time.y * _NormalSpeed;
                float2 normalUV = worldPos.xz * _NormalScale + float2(time * 0.05, time * 0.03);
                
                // 실제 물 셰이더의 노멀 텍스처에서 물결 굴곡 오프셋 샘플링
                float4 normalSample = SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, normalUV, 0);
                float3 unpackNorm = normalSample.rgb * 2.0 - 1.0;
                
                // 잔잔한 수면 물결 굴곡 왜곡 적용
                worldPos.xz += unpackNorm.xy * _DistortionStrength * 0.25;
                worldPos.y += unpackNorm.z * _DistortionStrength * 0.05;

                output.positionCS = TransformWorldToHClip(worldPos);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 🌟 색상 탈색(과다 노출) 원천 차단: 버텍스/머티리얼의 쨍한 루비 레드 & 오렌지 컬러 100% 보존
                return input.color;
            }
            ENDHLSL
        }
    }
}
