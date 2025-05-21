Shader "Terrain/CustomURPTerrain"
{
    Properties
    {
        _MainTex("Ground Texture", 2D) = "white" {}
        _WallTex("Wall Texture", 2D) = "white" {}
        _TexScale("Texture Scale", Float) = 1
        _YWeight("Top Surface Emphasis", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _WallTex;
            float _TexScale;
            float _YWeight;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.worldPos = worldPos;
                OUT.worldNormal = normalize(TransformObjectToWorldNormal(IN.normalOS));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 scaledWorldPos = IN.worldPos / _TexScale;

                float3 pWeight = abs(IN.worldNormal);
                pWeight.y *= _YWeight; // Boost influence of top surfaces
                pWeight /= (pWeight.x + pWeight.y + pWeight.z + 1e-5); // Normalize weights

                float3 xP = tex2D(_WallTex, scaledWorldPos.yz).rgb * pWeight.x;
                float3 yP = tex2D(_MainTex, scaledWorldPos.xz).rgb * pWeight.y;
                float3 zP = tex2D(_WallTex, scaledWorldPos.xy).rgb * pWeight.z;

                float3 albedo = xP + yP + zP;
                return float4(albedo, 1.0);
            }
            ENDHLSL
        }
    }
}
