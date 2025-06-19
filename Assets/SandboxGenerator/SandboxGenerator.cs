using UnityEngine;

public class SandboxGenerator : MonoBehaviour
{

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        RenderTexture densityMap = null;
        Create3DTexture(ref densityMap, 10, 10, 10, "densityMap");
    }

    // Update is called once per frame
    void Update()
    {

    }

    void Create3DTexture(ref RenderTexture texture, int width, int height, int depth, string name)
    {
        // 📦 Formatet vi vil bruge til hver voxel: ét flydende tal (float)
        var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;

        // 🔁 Tjek om vi allerede har en texture, og om den er korrekt størrelse og format
        // Hvis ikke, så laver vi en ny
        if (texture == null || !texture.IsCreated() ||
            texture.width != width || texture.height != height || texture.volumeDepth != depth ||
            texture.graphicsFormat != format)
        {
            // 🧹 Hvis der allerede findes en gammel texture, så slet den fra GPU-hukommelsen
            if (texture != null)
            {
                texture.Release();
            }

            // 🛠️ Lav en ny RenderTexture (3D texture) som GPU'en kan skrive i
            texture = new RenderTexture(width, height, 0); // 0 = ingen depth buffer
            texture.graphicsFormat = format;                // R32_SFloat = 1 float per voxel
            texture.volumeDepth = depth;                    // Z-dimension (dybde)
            texture.enableRandomWrite = true;               // Gør det muligt for compute shaders at skrive
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D; // 3D texture

            // 🚀 Allokerer og sender den til GPU’en
            texture.Create();
        }

        // 🎨 Indstillinger for hvordan teksturen skal bruges (ikke så vigtige her)
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.name = name;

        // ✅ Debug: Fortæl i konsollen at vi lavede en 3D texture
        Debug.Log($"✅ Lavede 3D Texture: {texture.width}x{texture.height}x{texture.volumeDepth}");
    }
}