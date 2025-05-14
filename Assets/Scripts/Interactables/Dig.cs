using UnityEngine;

public class Dig : Interactable
{
    protected override void Interact()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 3f, LayerMask.GetMask("Interactable"))) // Only Interactable layer
        {
            Container container = hit.collider.GetComponent<Container>();
            if (container != null)
            {
                // Get local hit position in container space
                Vector3 localHit = hit.point - container.transform.position;

                // Offset slightly into the block, based on the surface normal
                Vector3 adjustedPos = localHit + (0.5f * -hit.normal); // "Dig into" the block you hit

                Vector3Int voxelPos = Vector3Int.FloorToInt(adjustedPos);

                Debug.Log($"Trying to dig voxel at {voxelPos}");

                if (container[voxelPos].isSolid)
                {
                    container[voxelPos] = new Voxel() { ID = 0 }; // Mark as empty
                    container.GenerateMesh();
                    container.UploadMesh();
                }
            }
        }
    }
}
