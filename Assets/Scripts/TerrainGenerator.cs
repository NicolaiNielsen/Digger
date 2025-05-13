using UnityEngine;

public class VoxelTerrainGenerator : MonoBehaviour
{
    [SerializeField] int width = 10;
    [SerializeField] int depth = 10;
    [SerializeField] int height = 5;

    [SerializeField] float x_position = 20f;
    [SerializeField] float z_position = 20f;

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;

    private void Start()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        GenerateVoxelTerrain();
    }

    private void Update()
    {
        // Optionally update terrain dynamically in each frame (e.g., for procedural changes)
        // GenerateVoxelTerrain();
    }

    private void GenerateVoxelTerrain()
    {
        int numCubes = width * depth * height;
        vertices = new Vector3[numCubes * 24]; // 6 faces per cube, each face has 4 vertices (2 triangles per face)
        triangles = new int[numCubes * 36]; // 6 triangles per cube, each with 3 vertices

        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Position of the cube in 3D space
                    Vector3 position = new Vector3(x + x_position, y, z + z_position);

                    // Add vertices for each of the 6 faces of the cube
                    AddCube(position, ref vertexIndex, ref triangleIndex);
                }
            }
        }

        // Assign the vertices and triangles to the mesh
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
    }

    private void AddCube(Vector3 position, ref int vertexIndex, ref int triangleIndex)
    {
        // Define the 8 vertices for a cube (8 corners)
        Vector3[] cubeVertices = new Vector3[8]
        {
            position + new Vector3(0, 0, 0), // Front bottom left
            position + new Vector3(1, 0, 0), // Front bottom right
            position + new Vector3(1, 0, 1), // Back bottom right
            position + new Vector3(0, 0, 1), // Back bottom left
            position + new Vector3(0, 1, 0), // Front top left
            position + new Vector3(1, 1, 0), // Front top right
            position + new Vector3(1, 1, 1), // Back top right
            position + new Vector3(0, 1, 1), // Back top left
        };

        // Add the vertices to the mesh
        for (int i = 0; i < 8; i++)
        {
            vertices[vertexIndex] = cubeVertices[i];
            vertexIndex++;
        }

        // Define the triangles for each of the 6 faces of the cube (2 triangles per face)
        int[] cubeTriangles = new int[36]
        {
            // Front face
            0, 4, 1, 1, 4, 5,
            // Back face
            2, 6, 3, 3, 6, 7,
            // Left face
            0, 3, 4, 4, 3, 7,
            // Right face
            1, 5, 2, 2, 5, 6,
            // Top face
            4, 7, 5, 5, 7, 6,
            // Bottom face
            0, 1, 2, 2, 3, 0
        };

        // Adjust the triangle indices for the current set of vertices
        for (int i = 0; i < cubeTriangles.Length; i++)
        {
            triangles[triangleIndex] = cubeTriangles[i] + (vertexIndex - 8);
            triangleIndex++;
        }
    }
}
