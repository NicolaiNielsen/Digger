using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    [Header("World Size")]
    [SerializeField] private int xSize = 10;
    [SerializeField] private int zSize = 10;
    [SerializeField] private int yHeight = 10;

    [Header("World Settings")]
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;

    public Material worldMaterial;

    private Container container;


    // Start is called before the first frame update
    void Start()
    {
        GameObject cont = new GameObject("Container");
        cont.transform.parent = transform;
        container = cont.AddComponent<Container>();
        container.Initialize(worldMaterial, Vector3.zero);

        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                for (int y = 0; y < yHeight; y++)
                {
                    Vector3 position = spawnPosition + new Vector3(x, y, z);
                    container[position] = new Voxel() { ID = 1 };
                }
            }
        }


        container.GenerateMesh();
        container.UploadMesh();
    }

    // Update is called once per frame
    void Update()
    {

    }

}
