using UnityEngine;

public class Dig : Interactable
{
    protected override void Interact()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 3f, LayerMask.GetMask("Interactable")))
        {
            Marching cube = hit.collider.GetComponent<Marching>();
            if (cube != null)
            {
                // Convert hit point from world space to local space of the Marching object
                Vector3 localHit = cube.transform.InverseTransformPoint(hit.point);

                Debug.Log($"Hit local point: {localHit}");

                // Call RemoveTerrain with the local position so voxel indices match
                cube.RemoveTerrain(localHit);

                Debug.Log($"Trying to dig marchingcube at local pos {localHit} on {cube.name}");
            }
        }
    }
}
