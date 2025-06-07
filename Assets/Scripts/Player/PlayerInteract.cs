using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private Camera cam;
    [SerializeField]
    private LayerMask mask;
    [SerializeField]
    private float distance = 3f;
    private PlayerUI playerUI;
    private InputManager inputManager;

    public event System.Action onTerrainModified;

    public float terraformRadius = 1;
    public float terraformSpeedNear = 0.1f;
    public float terraformSpeedFar = 0.25f;

    TerrainGen TerrainGen;

    bool isTerraforming;
    Vector3 lastTerraformPointLocal;

    bool hasHit;
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
        hasHit = false;

        bool wasTerraformingLastFrame = isTerraforming;
        isTerraforming = false;

        int numIterations = 5;
        bool rayHitTerrain = false;

        for (int i = 0; i < numIterations; i++)
		{
			float rayRadius = terraformRadius * Mathf.Lerp(0.01f, 1, i / (numIterations - 1f));
			if (Physics.SphereCast(cam.transform.position, rayRadius, cam.transform.forward, out hit, 1000, mask))
			{
				Debug.Log("WE HITTING SOMETHING?");
				lastTerraformPointLocal = MathUtility.WorldToLocalVector(cam.transform.rotation, hit.point);
				Terraform(hit.point);
				rayHitTerrain = true;
				break;
			}
		}
    }

    void Terraform(Vector3 terraformPoint)
	{
		Debug.Log("terraformPoint");
		hasHit = true;
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
		if (inputManager.onFoot.Dig.triggered)
		{
			isTerraforming = true;
			Debug.Log("Terraform radius: " + terraformRadius);
			TerrainGen.Terraform(terraformPoint, weight, terraformRadius);
			Debug.Log(inputManager.onFoot.Dig.triggered);
			Debug.Log("WE calling onfoot dig Triggerd?");
		}

		if (isTerraforming)
		{
			onTerrainModified?.Invoke();
		}
	}
}

