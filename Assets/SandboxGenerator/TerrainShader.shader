Shader "Custom/EasyGrassDirt"
{
    Properties
    {
        _GrassTex ("Grass Texture", 2D) = "white" {}
        _DirtTex ("Dirt Texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.3
        _TextureScale ("Texture Scale", Float) = 6.0
        _SurfaceEpsilon ("Surface Band", Float) = 0.015
        [NoScaleOffset]_DensityTex ("Density 3D Tex", 3D) = "" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _GrassTex, _DirtTex;
        sampler3D _DensityTex;
        float _Glossiness, _TextureScale, _SurfaceEpsilon;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        // Simple triplanar function (no stretching)
        float4 TriplanarBlend(sampler2D tex, float3 pos, float3 normal, float scale)
        {
            float3 blend = pow(abs(normalize(normal)), 8);
            blend /= (blend.x + blend.y + blend.z);

            float2 xz = pos.yz / scale;
            float2 yz = pos.xz / scale;
            float2 xy = pos.xy / scale;

            float4 tx = tex2D(tex, xz);
            float4 ty = tex2D(tex, yz);
            float4 tz = tex2D(tex, xy);

            return tx * blend.x + ty * blend.y + tz * blend.z;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // 1. Get density at this point (world space to 0-1)
            float3 t = (IN.worldPos) * 0.5 / _TextureScale + 0.5;
            float density = tex3D(_DensityTex, t);

            // 2. Only "surface": density ~ 0 (tweakable band)
            float surfaceWidth = _SurfaceEpsilon; // try 0.01, 0.02, 0.005, etc.
            float isSurface = smoothstep(-surfaceWidth, surfaceWidth, -density);

            // 3. Only very flat upward faces
            float up = saturate(dot(normalize(IN.worldNormal), float3(0,1,0)));
            float isUp = smoothstep(0.97, 1.0, up); // Only nearly flat up

            // 4. Combine
            float grassMask = isSurface * isUp;

            // 5. Triplanar sample
            float4 grassCol = TriplanarBlend(_GrassTex, IN.worldPos, IN.worldNormal, _TextureScale);
            float4 dirtCol  = TriplanarBlend(_DirtTex, IN.worldPos, IN.worldNormal, _TextureScale);

            // 6. Blend
            float4 finalCol = lerp(dirtCol, grassCol, grassMask);

            o.Albedo = finalCol.rgb;
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
