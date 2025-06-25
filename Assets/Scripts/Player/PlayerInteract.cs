using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private Camera cam;
    [SerializeField]
    private LayerMask mask;
    [SerializeField]
    private PlayerUI playerUI;
    private InputManager inputManager;

    public event System.Action onTerrainModified;

	public float terraformRadius;
    public float terraformSpeedNear;
    public float terraformSpeedFar;

    TerrainGen TerrainGen;

    bool isTerraforming;
    Vector3 lastTerraformPointLocal;
    Vector3 hitPoint;

    void Start()
    {
        cam = GetComponent<PlayerLook>().cam;
        playerUI = GetComponent<PlayerUI>();
        inputManager = GetComponent<InputManager>();
        TerrainGen = FindFirstObjectByType<TerrainGen>();

    }

    void Update()
    {
        playerUI.UpdateText(string.Empty);

        RaycastHit hit;

        bool wasTerraformingLastFrame = isTerraforming;
        isTerraforming = false;

        int numIterations = 5;

        float digDistance = 3f; // Match this to your resource pickup raycast distance

        for (int i = 0; i < numIterations; i++)
		{
			float rayRadius = terraformRadius * Mathf.Lerp(0.01f, 1, i / (numIterations - 1f));
			if (Physics.SphereCast(cam.transform.position, rayRadius, cam.transform.forward, out hit, digDistance, mask))
			{
				Debug.Log("WE HITTING SOMETHING?");
				lastTerraformPointLocal = MathUtility.WorldToLocalVector(cam.transform.rotation, hit.point);
				Terraform(hit.point);
				break;
			}
		}
    }

    void Terraform(Vector3 terraformPoint)
	{
		Debug.Log("terraformPoint");
		hitPoint = terraformPoint;

		const float dstNear = 10;
		const float dstFar = 60;

		float dstFromCam = (terraformPoint - cam.transform.position).magnitude;
		float weight01 = Mathf.InverseLerp(dstNear, dstFar, dstFromCam);
		float weight = Mathf.Lerp(terraformSpeedNear, terraformSpeedFar, weight01);

		// // Add terrain
		// if (Input.GetMouseButton(0))
		// {
		// 	isTerraforming = true;
		// 	TerrainGen.Terraform(terraformPoint, -weight, terraformRadius);
		// 	//firstPersonController.NotifyTerrainChanged(terraformPoint, terraformRadius);
		// }
		// Subtract terrain
		if (inputManager.onFoot.Dig.IsPressed())
		{
			isTerraforming = true;
			Debug.Log("Terraform radius: " + terraformRadius);
			TerrainGen.Terraform(terraformPoint, -weight, terraformRadius);
			Debug.Log(inputManager.onFoot.Dig.triggered);
			Debug.Log("WE calling onfoot dig Triggerd?");
		}

		if (isTerraforming)
		{
			onTerrainModified?.Invoke();
		}
	}
}

