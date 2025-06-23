Shader "Custom/Terrain_Triplanar_URP"
{
    Properties
    {
        _RockInnerShallow ("Rock Inner Shallow", Color) = (0.72,0.55,0.45,1)
        _RockInnerDeep ("Rock Inner Deep", Color) = (0.32,0.23,0.18,1)
        _RockLight ("Rock Light", Color) = (0.7,0.7,0.7,1)
        _RockDark ("Rock Dark", Color) = (0.3,0.3,0.3,1)
        _GrassLight ("Grass Light", Color) = (0.4,0.7,0.3,1)
        _GrassDark ("Grass Dark", Color) = (0.2,0.4,0.2,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _DensityTex ("Density Texture", 3D) = "" {}
        _HeightGrass ("Grass Height World Y", Float) = 0.5
        _HeightBlend ("Grass Blend Range", Float) = 0.25
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Test ("Test", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Name "Forward"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _RockInnerShallow;
                float4 _RockInnerDeep;
                float4 _RockLight;
                float4 _RockDark;
                float4 _GrassLight;
                float4 _GrassDark;
                float _HeightGrass;
                float _HeightBlend;
                float _Glossiness;
                float _Test;
            CBUFFER_END

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);   SAMPLER(sampler_NoiseTex);
            TEXTURE3D(_DensityTex); SAMPLER(sampler_DensityTex);

            // Triplanar sampling
            float4 triplanarOffset(float3 worldPos, float3 normal, float3 scale, Texture2D tex, SamplerState samp, float2 offset)
            {
                float3 scaledPos = worldPos / scale;
                float4 colX = tex.Sample(samp, scaledPos.zy + offset);
                float4 colY = tex.Sample(samp, scaledPos.xz + offset);
                float4 colZ = tex.Sample(samp, scaledPos.xy + offset);

                float3 blendWeight = pow(abs(normal), 2);
                blendWeight /= (blendWeight.x + blendWeight.y + blendWeight.z);
                return colX * blendWeight.x + colY * blendWeight.y + colZ * blendWeight.z;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Triplanar noise for blending
                float4 noise = triplanarOffset(IN.worldPos, IN.worldNormal, 30, _NoiseTex, sampler_NoiseTex, 0);
                float4 noise2 = triplanarOffset(IN.worldPos, IN.worldNormal, 50, _NoiseTex, sampler_NoiseTex, 0);

                // World Y blending for grass/dirt/rock
                float heightT = saturate((_HeightGrass - IN.worldPos.y) / _HeightBlend);

                // Optionally use 3D density if you assign a texture
                float density = 0;
                #ifdef _DensityTex
                    density = _DensityTex.SampleLevel(sampler_DensityTex, IN.worldPos, 0).r;
                #endif

                float4 grassCol = lerp(_GrassLight, _GrassDark, noise.r);
                float4 rockCol = lerp(_RockLight, _RockDark, noise2.r);

                // Deeper (optionally using density), fade to inner rock color
                float threshold = 0.005;
                float4 baseCol = lerp(grassCol, rockCol, heightT);

                // If you want density-based inner color blending, use this:
                if (density < -threshold)
                {
                    float rockDepthT = saturate(abs(density + threshold) * 20);
                    baseCol = lerp(_RockInnerShallow, _RockInnerDeep, rockDepthT);
                }

                return float4(baseCol.rgb, 1);
            }
            ENDHLSL
        }
    }
}
