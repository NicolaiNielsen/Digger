using Unity.Mathematics;
using UnityEngine;

public static class GameData
{
    public static float surface = 0.5f;
    public static int ChunkWidth = 10;
    public static int ChunkHeight = 250;

    public static float TerrainHeight = 60f;
    public static float TerrainHeightRange = 3f;

    public static float GetTerrainHeight(float x, float z)
    {
        return (float)TerrainHeightRange * Mathf.PerlinNoise((float)x / 16f * 1.5f + 0.001f, (float)z / 16f * 1.5f + 0.001f) +TerrainHeight;
    }
}
