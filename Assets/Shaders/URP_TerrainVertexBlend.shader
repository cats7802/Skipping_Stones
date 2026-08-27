Shader "Custom/URP_TerrainVertexBlend"
{
    Properties
    {
        [Header(Layer 0 Base Grass)]
        _BaseTex ("Grass (Base) Texture", 2D) = "white" {}
        _BaseColor ("Grass Tint", Color) = (1, 1, 1, 1)

        [Header(Layer 1 Red Channel Dirt)]
        _DirtTex ("Dirt (R Channel) Texture", 2D) = "white" {}
        _DirtColor ("Dirt Tint", Color) = (1, 1, 1, 1)

        [Header(Layer 2 Green Channel Rock)]
        _RockTex ("Rock (G Channel) Texture", 2D) = "white" {}
        _RockColor ("Rock Tint", Color) = (1, 1, 1, 1)

        [Header(Layer 3 Blue Channel Sand)]
        _SandTex ("Sand (B Channel) Texture", 2D) = "white" {}
        _SandColor ("Sand Tint", Color) = (1, 1, 1, 1)

        [Header(Lighting and Smoothness)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD3;
                float4 color : COLOR;
            };

            TEXTURE2D(_BaseTex); SAMPLER(sampler_BaseTex);
            TEXTURE2D(_DirtTex); SAMPLER(sampler_DirtTex);
            TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);
            TEXTURE2D(_SandTex); SAMPLER(sampler_SandTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTex_ST;
                float4 _DirtTex_ST;
                float4 _RockTex_ST;
                float4 _SandTex_ST;
                float4 _BaseColor;
                float4 _DirtColor;
                float4 _RockColor;
                float4 _SandColor;
                float _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uvBase = TRANSFORM_TEX(input.uv, _BaseTex);
                float2 uvDirt = TRANSFORM_TEX(input.uv, _DirtTex);
                float2 uvRock = TRANSFORM_TEX(input.uv, _RockTex);
                float2 uvSand = TRANSFORM_TEX(input.uv, _SandTex);

                half4 colBase = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, uvBase) * _BaseColor;
                half4 colDirt = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, uvDirt) * _DirtColor;
                half4 colRock = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uvRock) * _RockColor;
                half4 colSand = SAMPLE_TEXTURE2D(_SandTex, sampler_SandTex, uvSand) * _SandColor;

                // 버텍스 컬러 RGB 채널 가중치
                float wDirt = saturate(input.color.r);
                float wRock = saturate(input.color.g);
                float wSand = saturate(input.color.b);
                float wBase = saturate(1.0 - (wDirt + wRock + wSand));

                // 정규화 가중치 계산 (4개 레이어의 합 = 1.0)
                float totalWeight = wBase + wDirt + wRock + wSand;
                if (totalWeight > 0.001)
                {
                    wBase /= totalWeight;
                    wDirt /= totalWeight;
                    wRock /= totalWeight;
                    wSand /= totalWeight;
                }
                else
                {
                    wBase = 1.0; wDirt = 0.0; wRock = 0.0; wSand = 0.0;
                }

                // 4개 레이어 완벽한 정규화 부드러운 블렌딩
                half4 finalAlbedo = (colBase * wBase) + (colDirt * wDirt) + (colRock * wRock) + (colSand * wSand);

                // URP 기본 디렉셔널 라이팅 적용
                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = (mainLight.color * NdotL) + half3(0.25, 0.28, 0.35); // Ambient

                return half4(finalAlbedo.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
