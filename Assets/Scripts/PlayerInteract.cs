using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private Camera cam;
    private LayerMask mask;
    [SerializeField]
    private float distance = 3f;
    [SerializeField]

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {   
        cam = GetComponent<PlayerLook>().cam;
    }

    // Update is called once per frame
    void Update()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * distance);
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo,distance, mask)) {

        }

    }
}
