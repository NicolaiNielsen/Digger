using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;

public class Marching : MonoBehaviour
{
    public bool smoothTerrain;
    public bool flatShaded;
    MeshFilter meshFilter;

    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    MeshCollider meshCollider;

    float surface = 0.5f;
    int width = 50;
    int height = 8;
    float[,,] map;
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
                    // Get a terrain height using regular old Perlin noise.
                    float thisHeight = (float)height * Mathf.PerlinNoise((float)x / 16f * 1.5f + 0.001f, (float)z / 16f * 1.5f + 0.001f);
                    // Set the value of this point in the terrainMap.
                    map[x, y, z] = (float)y - thisHeight;

                }
            }
        }
    }

    public void RemoveTerrain (Vector3 pos) {
        Debug.Log("AM I BEING CALLED");
        Vector3Int v3Int = new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
        Debug.Log(map[v3Int.x, v3Int.y, v3Int.z]);
        Debug.Log("After digging");
        map[v3Int.x, v3Int.y, v3Int.z] = 1f;
        Debug.Log(map[v3Int.x, v3Int.y, v3Int.z]);
        
        CreateMeshData();
    }

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        map = new float[width + 1, height + 1, width + 1];

        PopulateTerrainMap();
        CreateMeshData();
        transform.position = new Vector3(transform.position.x, -height / 2f, transform.position.z);
    }
    void ClearMeshData()
    {
        Debug.Log("Calling Clear Mesh data");
        vertices.Clear();
        triangles.Clear();
    }


    int VertForIndice (Vector3 vert) {

        // Loop through all the vertices currently in the vertices list.
        for (int i = 0; i < vertices.Count; i++) {

            // If we find a vert that matches ours, then simply return this index.
            if (vertices[i] == vert)
                return i;

        }

        // If we didn't find a match, add this vert to the list and return last index.
        vertices.Add(vert);
        return vertices.Count - 1;

    }

    void CreateMeshData() {
        Debug.Log("Calling Create mesh data");

        ClearMeshData();

        // Loop through each "cube" in our terrain.
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                for (int z = 0; z < width; z++) {

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

                Vector3 vert1 = position + MarchingCubeLookUpTable.CornerTable[MarchingCubeLookUpTable.EdgeIndexes[indices, 0]];
                Vector3 vert2 = position + MarchingCubeLookUpTable.CornerTable[MarchingCubeLookUpTable.EdgeIndexes[indices, 1]];
                Vector3 vertPosition;
                if (smoothTerrain)
                {
                    float vert1Sample = cube[MarchingCubeLookUpTable.EdgeIndexes[indices, 0]];
                    float vert2Sample = cube[MarchingCubeLookUpTable.EdgeIndexes[indices, 1]];
                    float difference = vert2Sample - vert1Sample;

                    if (difference == 0)
                    {
                        difference = surface;
                    }
                    else
                    {
                        difference = (surface - vert1Sample) / difference;
                    }

                    vertPosition = vert1 + ((vert2 - vert1) * difference);

                }
                else
                {
                    vertPosition = (vert1 + vert2) / 2f;
                }


                // Add to our vertices and triangles list and incremement the edgeIndex.
                if (flatShaded) {

                    vertices.Add(vertPosition);
                    triangles.Add(vertices.Count - 1);

                } else
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

    float SampleTerrain (Vector3Int point) {
        return map[point.x, point.y, point.z];
    }

}