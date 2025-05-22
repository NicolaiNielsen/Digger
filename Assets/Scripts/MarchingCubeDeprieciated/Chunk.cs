using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Chunk
{
    MeshFilter meshFilter;
    public GameObject chunkObject;

    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    MeshCollider meshCollider;
    MeshRenderer meshRenderer;
    Vector3 chunkPosition;

    int width
    {
        get { return GameData.ChunkWidth; }
    }

    int height
    {
        get { return GameData.ChunkHeight; }
    }

    float surface
    {
        get { return GameData.surface; }
    }

    float[,,] map;
    public static float voxelSize = 0.25f; // smaller = more detail


    Vector3 VertexInterp(Vector3 p1, Vector3 p2, float valp1, float valp2)
{
    if (Mathf.Abs(surface - valp1) < 0.00001f)
        return p1;
    if (Mathf.Abs(surface - valp2) < 0.00001f)
        return p2;
    if (Mathf.Abs(valp1 - valp2) < 0.00001f)
        return p1;

    float t = (surface - valp1) / (valp2 - valp1);
    return p1 + t * (p2 - p1);
}


    public Chunk(Vector3 _position)
    {
        chunkObject = new GameObject();
        chunkObject.name = string.Format("Chunk {0} {1} {2}", _position.x, _position.y, _position.z);
        chunkPosition = _position;
        chunkObject.transform.position = (Vector3)chunkPosition * voxelSize;

        meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshCollider = chunkObject.AddComponent<MeshCollider>();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        chunkObject.layer = LayerMask.NameToLayer("Interactable");
        chunkObject.transform.tag = "Diggable";
        meshRenderer.material = Resources.Load<Material>("Materials/Terrain");
        map = new float[width + 1, height + 1, width + 1];
        PopulateTerrainMap();
        CreateMeshData();
    }
    void ClearMeshData()
    {
        Debug.Log("Calling Clear Mesh data");
        vertices.Clear();
        triangles.Clear();
    }
    void PopulateTerrainMap()
    {

        // The data points for terrain are stored at the corners of our "cubes", so the terrainMap needs to be 1 larger
        // than the width/height of our mesh.
        for (int x = 0; x < width + 1; x++)
        {
            for (int z = 0; z < width + 1; z++)
            {
                for (int y = 0; y < height + 1; y++)
                {
                    float thisHeight;
                    float worldX = (x + chunkPosition.x) * voxelSize;
                    float worldZ = (z + chunkPosition.z) * voxelSize;

                    thisHeight = GameData.GetTerrainHeight(worldX, worldZ);
                    // Set the value of this point in the terrainMap.
                    map[x, y, z] = (float)y - thisHeight;
                    Debug.Log("Setting map value at " + x + " " + y + " " + z + " to " + map[x, y, z]);

                }
            }
        }
    }

public void RemoveTerrain(Vector3 worldPos, float digRadius = 0.2f)
{
    Debug.Log($"Trying to remove terrain at worldPos {worldPos}");

    // Convert to local space relative to chunk origin
    Vector3 localPos = worldPos - chunkObject.transform.position;

    // Convert to voxel indices
    Vector3Int centerVoxel = new Vector3Int(
        Mathf.RoundToInt(localPos.x / voxelSize),
        Mathf.RoundToInt(localPos.y / voxelSize),
        Mathf.RoundToInt(localPos.z / voxelSize)
    );

    Debug.Log($"Local position: {localPos}, Center voxel: {centerVoxel}");
    float digStrength = 0.2f; // Example strength value
    // Convert digRadius to voxel radius
    int voxelRadius = Mathf.CeilToInt(digRadius / voxelSize);

    for (int x = centerVoxel.x - voxelRadius; x <= centerVoxel.x + voxelRadius; x++)
    {
        for (int y = centerVoxel.y - voxelRadius; y <= centerVoxel.y + voxelRadius; y++)
        {
            for (int z = centerVoxel.z - voxelRadius; z <= centerVoxel.z + voxelRadius; z++)
            {
                // Bounds check
                if (x < 0 || x >= map.GetLength(0) ||
                    y < 0 || y >= map.GetLength(1) ||
                    z < 0 || z >= map.GetLength(2))
                {
                    continue;
                }

                // Check distance from center (optional for spherical dig)
                Vector3 offset = new Vector3(x, y, z) - centerVoxel;
                float distance = offset.magnitude * voxelSize;

                if (distance <= digRadius)
                {
                    map[x, y, z] += digStrength; // e.g. digStrength = 0.2f
                    map[x, y, z] = Mathf.Clamp(map[x, y, z], -1f, 1f); // Optional clamp
                    map[x, y, z] = 1f; // 1 = empty / air
                    Debug.Log($"Removing voxel at {x}, {y}, {z} (local)");
                }
            }
        }
    }

    CreateMeshData(); // Rebuild terrain mesh
}


    int VertForIndice(Vector3 vert)
    {

        // Loop through all the vertices currently in the vertices list.
        for (int i = 0; i < vertices.Count; i++)
        {

            // If we find a vert that matches ours, then simply return this index.
            if (vertices[i] == vert)
                return i;

        }

        // If we didn't find a match, add this vert to the list and return last index.
        vertices.Add(vert);
        return vertices.Count - 1;

    }

    void CreateMeshData()
    {
        Debug.Log("Calling Create mesh data");

        ClearMeshData();

        // Loop through each "cube" in our terrain.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {

                    // Pass the value into our MarchCube function.
                    MarchCube(new Vector3Int(x, y, z));

                }
            }
        }
        Debug.Log("Calling Create mesh data");
        BuildMesh();

    }
    //Gets the cube configuration based on the 8 points in the cube
    int GetCubeConfiguration(float[] cube)
    {
        int configurationIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (cube[i] > surface)
            {
                configurationIndex |= 1 << i;
            }
        }
        return configurationIndex;
    }

    void MarchCube(Vector3Int position)
    {
        float[] cube = new float[8];

        for (int i = 0; i < 8; i++)
        {
            cube[i] = SampleTerrain(position + MarchingCubeLookUpTable.CornerTable[i]);
        }

        int configIndex = GetCubeConfiguration(cube);

        if (configIndex == 0 || configIndex == 255)
        {
            return;
        }

        int edgeIndex = 0;

        for (int i = 0; i < 5; i++)
        {
            for (int p = 0; p < 3; p++)
            {
                int indices = MarchingCubeLookUpTable.TriangleTable[configIndex, edgeIndex];

                if (indices == -1)
                {
                    return;
                }

                Vector3Int cornerA = position + MarchingCubeLookUpTable.CornerTable[MarchingCubeLookUpTable.EdgeIndexes[indices, 0]];
                Vector3Int cornerB = position + MarchingCubeLookUpTable.CornerTable[MarchingCubeLookUpTable.EdgeIndexes[indices, 1]];

                Vector3 vert1 = (Vector3)cornerA * voxelSize;
                Vector3 vert2 = (Vector3)cornerB * voxelSize;
                Vector3 vertPosition;

                float vert1Sample = cube[MarchingCubeLookUpTable.EdgeIndexes[indices, 0]];
                float vert2Sample = cube[MarchingCubeLookUpTable.EdgeIndexes[indices, 1]];
                float difference = vert2Sample - vert1Sample;

                vertPosition = VertexInterp(vert1, vert2, vert1Sample, vert2Sample);

                triangles.Add(VertForIndice(vertPosition));

                edgeIndex++;
            }
        }
    }

    void BuildMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;
        Debug.Log("Calling build mesh");
    }

    float SampleTerrain(Vector3Int point)
    {
        return map[point.x, point.y, point.z];
    }

}