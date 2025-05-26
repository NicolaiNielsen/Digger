using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private Camera cam;
    [SerializeField]
    private LayerMask mask;
    [SerializeField]
    private float distance = 3f;
    private PlayerUI playerUI;

    public WorldGenerator worldGenerator;
    private InputManager inputManager;

    void Start()
    {
        cam = GetComponent<PlayerLook>().cam;
        playerUI = GetComponent<PlayerUI>();
        inputManager = GetComponent<InputManager>();
    }

    void Update()
    {
        playerUI.UpdateText(string.Empty);

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * distance);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, distance, mask))
        {

            if (hit.collider.CompareTag("Diggable"))
            {
                // Update UI (optional)
                playerUI.UpdateText("Press E to dig");
                Vector3 hitPoint = hit.point;
                
                if (inputManager.onFoot.Dig.triggered)
                {
                    if (Physics.Raycast(ray, out hit))
                    {
                        Debug.Log("Hit point: " + hitPoint);
                        if (hit.transform.tag == "Diggable")
                            Debug.Log("TEst");
                            //worldGenerator.GetChunk(hit.point).RemoveTerrain(hit.point);

                    }

                }
            }
        }
        else
        {
            // Optional: show other interaction UI
            playerUI.UpdateText("No diggable surface");
        }
    }
}

