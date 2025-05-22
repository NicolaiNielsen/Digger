using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{

    public int WorldSizeChunks = 10;

    Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject loadingScreen;
    void Start()
    {

        Generate();
    }

void Generate()
{
    loadingScreen.SetActive(true);

    for (int x = 0; x < WorldSizeChunks; x++)
    {
        for (int z = 0; z < WorldSizeChunks; z++)
        {
            Vector3Int chunkPosition = new Vector3Int(x * GameData.ChunkWidth, 0, z * GameData.ChunkWidth);
            Debug.Log($"Generating chunk at {chunkPosition}");
            Debug.Log($"World Size Chunks: {WorldSizeChunks}");
            // Create and store chunk
                Chunk newChunk = new Chunk(chunkPosition);
            chunks.Add(chunkPosition, newChunk);

            // Parent the chunk's GameObject
            newChunk.chunkObject.transform.parent = transform;
        }
    }

    loadingScreen.SetActive(false);

    // Debug all chunk positions
    foreach (var kvp in chunks)
    {
        Vector3Int chunkCoord = kvp.Key;
        Chunk chunk = kvp.Value;

        Debug.Log($"Chunk at grid {chunkCoord} - World Position: {chunk.chunkObject.transform.position}");
    }
}


public Chunk GetChunk(Vector3 position)
    {
        float chunkWorldSizeX = GameData.ChunkWidth * Chunk.voxelSize;
        float chunkWorldSizeY = GameData.ChunkHeight * Chunk.voxelSize;
        float chunkWorldSizeZ = GameData.ChunkWidth * Chunk.voxelSize;  // assuming square in x and z

        int chunkX = Mathf.FloorToInt(position.x / chunkWorldSizeX);
        int chunkY = Mathf.FloorToInt(position.y / chunkWorldSizeY);
        int chunkZ = Mathf.FloorToInt(position.z / chunkWorldSizeZ);

        Vector3Int chunkCoord = new Vector3Int(
            chunkX * GameData.ChunkWidth,
            chunkY * GameData.ChunkHeight,
            chunkZ * GameData.ChunkWidth
        );

        if (chunks.TryGetValue(chunkCoord, out Chunk chunk))
        {

            Debug.Log("Found chunk at " + chunkCoord);
            return chunk;
        }
        else
        {
            
        }

        Debug.LogWarning("Chunk not found at " + chunkCoord);
        return null;
    }



}
