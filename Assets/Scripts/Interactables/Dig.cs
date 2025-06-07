using UnityEngine;

public class Dig : Interactable
{
    public WorldGenerator world;
    protected override void Interact()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 3f, LayerMask.GetMask("Interactable")))
        {
            Chunk cube = hit.collider.GetComponent<Chunk>();
            if (cube != null)
            {
                world.GetChunk(hit.transform.position).RemoveTerrain(hit.point);
            }
        }
    }
}
