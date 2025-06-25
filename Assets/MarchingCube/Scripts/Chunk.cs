using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;

public class Chunk
{
    public Vector3 centre;
    public float size;
    public Mesh mesh;
    public bool hasSpawnedPlants = false;
    public bool hasSpawnedResources = false;

    public ComputeBuffer pointsBuffer;
    int numPointsPerAxis;
    public MeshFilter filter;
    MeshRenderer renderer;
    MeshCollider collider;
    public bool terra;
    public Vector3Int id;

    // Mesh processing
    Dictionary<int2, int> vertexIndexMap;
    List<Vector3> processedVertices;
    List<Vector3> processedNormals;
    List<int> processedTriangles;

    public float[,,] densityField; // Add this to store per-chunk density


    public Chunk(Vector3Int coord, float size, int numPointsPerAxis, GameObject meshHolder)
    {
        this.id = coord;
        this.size = size;
        this.numPointsPerAxis = numPointsPerAxis;

        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        int numPointsTotal = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        ComputeHelper.CreateStructuredBuffer<PointData>(ref pointsBuffer, numPointsTotal);

        // Mesh rendering and collision components
        filter = meshHolder.AddComponent<MeshFilter>();
        renderer = meshHolder.AddComponent<MeshRenderer>();
        filter.mesh = mesh;
        collider = renderer.gameObject.AddComponent<MeshCollider>();
        vertexIndexMap = new Dictionary<int2, int>();
        processedVertices = new List<Vector3>();
        processedNormals = new List<Vector3>();
        processedTriangles = new List<int>();
    }
    public void CreateMesh(VertexData[] vertexData, int numVertices, bool useFlatShading)
    {
        vertexIndexMap.Clear();
        processedVertices.Clear();
        processedNormals.Clear();
        processedTriangles.Clear();

        int triangleIndex = 0;

        // New: Create list for vertex colors
        List<Color> vertexColors = new List<Color>();

        for (int i = 0; i < numVertices; i++)
        {
            VertexData data = vertexData[i];

            int sharedVertexIndex;
            if (!useFlatShading && vertexIndexMap.TryGetValue(data.id, out sharedVertexIndex))
            {
                processedTriangles.Add(sharedVertexIndex);
            }
            else
            {
                if (!useFlatShading)
                {
                    vertexIndexMap.Add(data.id, triangleIndex);
                }
                processedVertices.Add(data.position);
                processedNormals.Add(data.normal);
                processedTriangles.Add(triangleIndex);

                // --------- Surface mask logic -----------
                // You can change the threshold (e.g., 0.7f) for more/less strict "top"
                bool isTopSurface = data.normal.y > 0.7f;
                // Color.green = (0,1,0,1); Color.white = (1,1,1,1)
                vertexColors.Add(isTopSurface ? Color.green : Color.white);
                // ----------------------------------------

                triangleIndex++;
            }
        }

        // Generate UVs based on vertex positions (planar mapping on XZ plane)
        List<Vector2> uvs = new List<Vector2>(processedVertices.Count);
        for (int i = 0; i < processedVertices.Count; i++)
        {
            Vector3 v = processedVertices[i];
            float u = (v.x / size) + 0.5f;
            float w = (v.z / size) + 0.5f;
            uvs.Add(new Vector2(u, w));
        }

        collider.sharedMesh = null;

        mesh.Clear();
        mesh.SetVertices(processedVertices);
        mesh.SetUVs(0, uvs); // Assign generated UVs
        mesh.SetTriangles(processedTriangles, 0, true);

        if (useFlatShading)
        {
            mesh.RecalculateNormals();
        }
        else
        {
            mesh.SetNormals(processedNormals);
        }

        // --------- Assign vertex colors! ----------
        mesh.SetColors(vertexColors);
        // ------------------------------------------

        // Only assign to collider if mesh has vertices
        if (mesh.vertexCount > 0)
        {
            collider.sharedMesh = mesh;
        }
        else
        {
            collider.sharedMesh = null;
        }
    }


    public struct PointData
    {
        public Vector3 position;
        public Vector3 normal;
        public float density;
    }

    public void AddCollider()
    {
        collider.sharedMesh = mesh;
    }

    public void SetMaterial(Material material)
    {
        renderer.material = material;
    }

    public void Release()
    {
        ComputeHelper.Release(pointsBuffer);
    }

    public void DrawBoundsGizmo(Color col)
    {
        Gizmos.color = col;
        // Calculate the world-space corner of this chunk
        Vector3 corner = (Vector3)id * size;
        Vector3 center = corner + Vector3.one * (size / 2f);
        Gizmos.DrawWireCube(center, Vector3.one * size);
        Gizmos.DrawSphere(corner, 0.2f);
    }

    public void SpawnResourcesVoxelAligned(
    GameObject rockPrefab, GameObject diamondPrefab, GameObject goldPrefab, GameObject copperPrefab, GameObject coalPrefab,
    float chunkSize, int numPointsPerAxis, Vector3 chunkCoord, float worldMinY, float worldMaxY, float isoLevel)
    {
        // Remove old resources
        foreach (Transform child in filter.transform)
            if (child.CompareTag("Resource"))
                GameObject.Destroy(child.gameObject);

        float voxelSize = chunkSize / (numPointsPerAxis - 1);

        for (int x = 0; x < numPointsPerAxis; x++)
        {
            for (int z = 0; z < numPointsPerAxis; z++)
            {
                // Find surface Y for this column
                int surfaceY = -1;
                for (int y = numPointsPerAxis - 2; y >= 0; y--)
                {
                    bool solid = densityField[x, y, z] < isoLevel;
                    bool aboveAir = densityField[x, y + 1, z] >= isoLevel;
                    if (solid && aboveAir)
                    {
                        surfaceY = y;
                        break;
                    }
                }
                if (surfaceY == -1) continue; // No surface found

                // Spawn resources below the surface
                for (int y = 0; y < surfaceY; y++)
                {
                    // World position using coordToWorld logic
                    Vector3 chunkOrigin = (chunkCoord * (numPointsPerAxis - 1)) * voxelSize;
                    Vector3 localCoord = new Vector3(x, y, z) - chunkCoord * (numPointsPerAxis - 1);
                    Vector3 worldPos = chunkOrigin + localCoord * voxelSize;

                    float yNorm = Mathf.InverseLerp(worldMinY, worldMaxY, worldPos.y);
                    float rand = UnityEngine.Random.value;

                    // Diamonds: bottom 10%
                    if (yNorm < 0.1f && rand < 0.01f && diamondPrefab != null)
                    {
                        var obj = GameObject.Instantiate(diamondPrefab, worldPos, Quaternion.identity, filter.transform);
                        obj.tag = "Resource";
                    }
                    else if (yNorm < 0.2f && rand < 0.02f && goldPrefab != null)
                    {
                        var obj = GameObject.Instantiate(goldPrefab, worldPos, Quaternion.identity, filter.transform);
                        obj.tag = "Resource";
                    }
                    else if (yNorm < 0.33f && rand < 0.03f && coalPrefab != null)
                    {
                        var obj = GameObject.Instantiate(coalPrefab, worldPos, Quaternion.identity, filter.transform);
                        obj.tag = "Resource";
                    }
                    else if (yNorm > 0.33f && yNorm < 0.66f && rand < 0.02f && copperPrefab != null)
                    {
                        var obj = GameObject.Instantiate(copperPrefab, worldPos, Quaternion.identity, filter.transform);
                        obj.tag = "Resource";
                    }
                    else if (rand < 0.01f && rockPrefab != null)
                    {
                        var obj = GameObject.Instantiate(rockPrefab, worldPos, Quaternion.identity, filter.transform);
                        obj.tag = "Resource";
                    }
                }
            }
        }
    }
}