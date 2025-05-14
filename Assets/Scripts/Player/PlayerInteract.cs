using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerInteract : MonoBehaviour
{
    private Camera cam;
    [SerializeField]
    private LayerMask mask;
    [SerializeField]
    private float distance = 3f;
    private PlayerUI playerUI;

    private InputManager inputManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {   
        cam = GetComponent<PlayerLook>().cam;
        playerUI = GetComponent<PlayerUI>();
        inputManager = GetComponent<InputManager>();
    }

    // Update is called once per frame
void Update()
{
    playerUI.UpdateText(string.Empty);

    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
    Debug.DrawRay(ray.origin, ray.direction * distance);
    RaycastHit hitInfo;
    
    if (Physics.Raycast(ray, out hitInfo, distance, mask))
    {
        Interactable interactable = hitInfo.collider.GetComponent<Interactable>();
        if (interactable != null)
        {
            playerUI.UpdateText(interactable.promptMessage);

            // Handle Dig
            if (hitInfo.collider.CompareTag("Diggable"))
            {
                if (inputManager.onFoot.Dig.triggered)
                {
                    interactable.BaseInteract();
                }
            }
            // Handle General Interaction
            else
            {
                if (inputManager.onFoot.Interact.triggered)
                {
                    interactable.BaseInteract();
                }
            }
        }
    }
}

}
