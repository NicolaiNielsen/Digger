using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [SerializeField] int xSize = 10;
    [SerializeField] int ySize = 10;

    private Mesh mesh;
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
        Debug.Log("TEst");
    }
}
