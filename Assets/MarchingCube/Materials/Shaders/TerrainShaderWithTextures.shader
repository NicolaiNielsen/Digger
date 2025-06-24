Shader "Custom/Terrain_TriplanarTextureNormal_URP"
{
    Properties
    {
        _GrassTex ("Grass Texture", 2D) = "white" {}
        _GrassNormal ("Grass Normal", 2D) = "bump" {}
        _RockTex ("Rock Texture", 2D) = "white" {}
        _RockNormal ("Rock Normal", 2D) = "bump" {}
        _GrassTint ("Grass Tint", Color) = (1.4,1.4,1.4,1)      // Brighter default!
        _RockTint ("Rock Tint", Color) = (1.2,1.2,1.2,1)        // Brighter default!
        _GrassBoost ("Grass Brightness", Float) = 1.2           // You can tweak in inspector
        _RockBoost ("Rock Brightness", Float) = 1.1             // You can tweak in inspector
        _HeightGrass ("Grass Height World Y", Float) = 0.5
        _HeightBlend ("Grass Blend Range", Float) = 0.25
        _TriplanarGrassScale ("Triplanar Grass Scale", Float) = 30
        _TriplanarRockScale ("Triplanar Rock Scale", Float) = 50
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
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 worldPos     : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _HeightGrass;
                float _HeightBlend;
                float _TriplanarGrassScale;
                float _TriplanarRockScale;
                float4 _GrassTint;
                float4 _RockTint;
                float _GrassBoost;
                float _RockBoost;
            CBUFFER_END

            TEXTURE2D(_GrassTex);     SAMPLER(sampler_GrassTex);
            TEXTURE2D(_GrassNormal);  SAMPLER(sampler_GrassNormal);
            TEXTURE2D(_RockTex);      SAMPLER(sampler_RockTex);
            TEXTURE2D(_RockNormal);   SAMPLER(sampler_RockNormal);

            float4 triplanarSample(Texture2D tex, SamplerState samp, float3 worldPos, float3 worldNormal, float scale)
            {
                float3 scaledPos = worldPos / scale;
                float4 colX = tex.Sample(samp, scaledPos.zy);
                float4 colY = tex.Sample(samp, scaledPos.xz);
                float4 colZ = tex.Sample(samp, scaledPos.xy);

                float3 blendWeight = pow(abs(worldNormal), 2);
                blendWeight /= max(blendWeight.x + blendWeight.y + blendWeight.z, 1e-5);

                return colX * blendWeight.x + colY * blendWeight.y + colZ * blendWeight.z;
            }

            float3 triplanarNormal(Texture2D normalTex, SamplerState normalSamp, float3 worldPos, float3 worldNormal, float scale)
            {
                float3 scaledPos = worldPos / scale;
                float3 nX = normalTex.Sample(normalSamp, scaledPos.zy).xyz * 2 - 1;
                float3 nY = normalTex.Sample(normalSamp, scaledPos.xz).xyz * 2 - 1;
                float3 nZ = normalTex.Sample(normalSamp, scaledPos.xy).xyz * 2 - 1;

                float3 blendWeight = pow(abs(worldNormal), 2);
                blendWeight /= max(blendWeight.x + blendWeight.y + blendWeight.z, 1e-5);

                float3 n = normalize(nX * blendWeight.x + nY * blendWeight.y + nZ * blendWeight.z);
                return n;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Sample textures
                float4 grassCol = triplanarSample(_GrassTex, sampler_GrassTex, IN.worldPos, IN.worldNormal, _TriplanarGrassScale);
                float4 rockCol  = triplanarSample(_RockTex, sampler_RockTex, IN.worldPos, IN.worldNormal, _TriplanarRockScale);

                // Apply tint & brightness boost
                grassCol.rgb *= _GrassTint.rgb * _GrassBoost;
                rockCol.rgb  *= _RockTint.rgb * _RockBoost;

                float3 grassNorm = triplanarNormal(_GrassNormal, sampler_GrassNormal, IN.worldPos, IN.worldNormal, _TriplanarGrassScale);
                float3 rockNorm  = triplanarNormal(_RockNormal, sampler_RockNormal, IN.worldPos, IN.worldNormal, _TriplanarRockScale);

                float heightT = saturate((_HeightGrass - IN.worldPos.y) / _HeightBlend);

                float4 baseCol = lerp(grassCol, rockCol, heightT);
                float3 baseNorm = normalize(lerp(grassNorm, rockNorm, heightT));

                // Lighting: more ambient to avoid darkness!
                float3 lightDir = normalize(float3(0.4, 0.8, 0.3));
                float NdotL = saturate(dot(baseNorm, lightDir));
                float3 litCol = baseCol.rgb * (0.6 + 0.7 * NdotL); // More ambient light!

                return float4(litCol, 1);
            }
            ENDHLSL
        }
    }
}
