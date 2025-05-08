using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [SerializeField] int xSize = 10;
    [SerializeField] int zSize = 10;

    private Mesh mesh;
    Vector3[] vertices;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        GenerateTerrain();
    }

    // Update is called once per frame
    void Update()
    {
        GenerateTerrain();
    }

    private void GenerateTerrain() {
        vertices = new Vector3[(xSize + 1) * (zSize + 1)];

        int i = 0;

        for(int z = 0; z <= zSize; z++) {
            for(int x = 0; x <= xSize; x++) {
                vertices[i] = new Vector3(x, 0 , z);
                i++;

            }
        }

        mesh.vertices = vertices;

    }

    private void OnDrawGizmos()
    {
        if (vertices == null)
            return;

        Gizmos.color = Color.red;
        foreach (Vector3 pos in vertices)
        {
            Gizmos.DrawSphere(pos, 0.2f);
        }
    }

}
