using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;

public class TerrainGen : MonoBehaviour
{

	[Header("Init Settings")]
	public Vector3Int startPositon;
	public int numPointsPerAxis = 2;
	public float boundsSize = 1;
	public float chunkSize = 1;
	public int numChunksX = 2;
	public int numChunksY = 2;
	public int numChunksZ = 2;
	public float isoLevel = 0f;
	public bool useFlatShading;

	public float noiseScale;
	public float noiseHeightMultiplier;
	public bool blurMap;
	public int blurRadius = 3;

	[Header("References")]
	public ComputeShader meshCompute;
	public ComputeShader densityCompute;
	public ComputeShader blurCompute;
	public ComputeShader editCompute;
	public Material material;

	// Private
	ComputeBuffer triangleBuffer;
	ComputeBuffer triCountBuffer;
	[HideInInspector] public RenderTexture rawDensityTexture;
	[HideInInspector] public RenderTexture processedDensityTexture;
	Chunk[] chunks;

	VertexData[] vertexDataArray;

	// Stopwatches	
	System.Diagnostics.Stopwatch timer_fetchVertexData;
	System.Diagnostics.Stopwatch timer_processVertexData;
	RenderTexture originalMap;

	public GameObject stonePrefab, goldPrefab, diamondPrefab, coalPrefab, copperPrefab, rubyPrefab;
	public float spawnDistance = 10f; // Distance from player to trigger spawn

	public Transform player; // Assign in inspector or find at Start

	void Start()
	{
		InitTextures();
		CreateBuffers();
		CreateChunks();

		var sw = System.Diagnostics.Stopwatch.StartNew();
		GenerateAllChunks();

		ComputeHelper.CreateRenderTexture3D(ref originalMap, processedDensityTexture);
		if (processedDensityTexture == null)
		{
			Debug.LogError("processedDensityTexture is null!");
		}
		if (originalMap == null)
		{
			Debug.LogError("originalMap is null!");
		}


		if (processedDensityTexture != null && originalMap != null)
		{
			ComputeHelper.CopyRenderTexture3D(processedDensityTexture, originalMap);
		}
	}

	void InitTextures()
	{
		//Calculates how many points we need along one axis, we create cubes with even level of detail so no need for x,y,z
		//Creates a 3DTexture with rawDesinityTexture which is size x size x size in size
		//one cube is 2*2*2 = 8 size
		int sizeX = numChunksX * (numPointsPerAxis - 1) + 1;
		int sizeY = numChunksY * (numPointsPerAxis - 1) + 1;
		int sizeZ = numChunksZ * (numPointsPerAxis - 1) + 1;
		//creates something along the lines new float[,,] density = new float[2, 2, 2];
		//A GPU optimized 3D array to store points
		Create3DTexture(ref rawDensityTexture, sizeX, sizeY, sizeZ, "Raw Density Texture");
		Create3DTexture(ref processedDensityTexture, sizeX, sizeY, sizeZ, "Processed Density Texture");
		//Supposed to smooth the edges
		if (!blurMap)
		{
			processedDensityTexture = rawDensityTexture;
		}

		// Set textures on compute shaders
		densityCompute.SetTexture(0, "DensityTexture", rawDensityTexture);
		editCompute.SetTexture(0, "EditTexture", rawDensityTexture);
		blurCompute.SetTexture(0, "Source", rawDensityTexture);
		blurCompute.SetTexture(0, "Result", processedDensityTexture);
		meshCompute.SetTexture(0, "DensityTexture", (blurCompute) ? processedDensityTexture : rawDensityTexture);
	}

	void GenerateAllChunks()
	{
		// Create timers:
		timer_fetchVertexData = new System.Diagnostics.Stopwatch();
		timer_processVertexData = new System.Diagnostics.Stopwatch();

		ComputeDensity();

		for (int i = 0; i < chunks.Length; i++)
		{
			GenerateChunk(chunks[i]);
		}
	}

	void ComputeDensity()
	{
		int sizeX = rawDensityTexture.width;
		int sizeY = rawDensityTexture.height;
		int sizeZ = rawDensityTexture.volumeDepth;
		densityCompute.SetInts("textureSize", sizeX, sizeY, sizeZ);
		densityCompute.SetFloat("chunkSize", chunkSize);
		densityCompute.SetFloat("noiseScale", 0.05f);         // ← lower = bigger bumps
densityCompute.SetFloat("heightMultiplier", 1.0f);
		//Assume we are dipatching
		ComputeHelper.Dispatch(densityCompute, sizeX, sizeY, sizeZ);
		ProcessDensityMap();
	}

	void ProcessDensityMap()
	{
		if (blurMap)
		{
			int sizeX = rawDensityTexture.width;
			int sizeY = rawDensityTexture.height;
			int sizeZ = rawDensityTexture.volumeDepth;

			// Blur the entire texture: center at (0,0,0), large brush radius
			blurCompute.SetInts("brushCentre", 0, 0, 0);
			blurCompute.SetInt("brushRadius", Mathf.Max(sizeX, sizeY, sizeZ)); // Large enough to cover all
			blurCompute.SetInt("blurRadius", blurRadius);
			blurCompute.SetInts("textureSize", sizeX, sizeY, sizeZ);

			ComputeHelper.Dispatch(blurCompute, sizeX, sizeY, sizeZ);
		}
	}

	void GenerateChunk(Chunk chunk)
	{
		// Marching cubes
		// Opensource contirubtion
		int numVoxelsPerAxis = numPointsPerAxis - 1;
		int marchKernel = 0;

		meshCompute.SetInts("textureSize", rawDensityTexture.width, rawDensityTexture.height, rawDensityTexture.volumeDepth);
		meshCompute.SetInt("numPointsPerAxis", numPointsPerAxis);
		meshCompute.SetFloat("isoLevel", isoLevel);
		meshCompute.SetFloat("chunkSize", chunkSize);
		triangleBuffer.SetCounterValue(0);
		meshCompute.SetBuffer(marchKernel, "triangles", triangleBuffer);
		//example chunk.id = chunk.id is )
		//0,0,0 * numPointsPerAxis - 1
		//0,0,1 * 36
		Vector3 chunkCoord = (Vector3)chunk.id * (numPointsPerAxis - 1);
		meshCompute.SetVector("chunkCoord", chunkCoord);
		ComputeHelper.Dispatch(meshCompute, numVoxelsPerAxis, numVoxelsPerAxis, numVoxelsPerAxis, marchKernel);

		// Create mesh
		int[] vertexCountData = new int[1];
		triCountBuffer.SetData(vertexCountData);
		ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);

		timer_fetchVertexData.Start();
		triCountBuffer.GetData(vertexCountData);

		int numVertices = vertexCountData[0] * 3;

		// Fetch vertex data from GPU

		triangleBuffer.GetData(vertexDataArray, 0, 0, numVertices);

		timer_fetchVertexData.Stop();

		//CreateMesh(vertices);
		timer_processVertexData.Start();
		chunk.CreateMesh(vertexDataArray, numVertices, useFlatShading);

		if (!chunk.hasSpawnedResources)
		{//initial spawning of chunk
			SpawnResourcesInChunk(chunk, 10);
		}

		chunk.hasSpawnedResources = true;
		timer_processVertexData.Stop();
	}

	void Update()
	{
		// TODO: move somewhere more sensible
		material.SetTexture("DensityTex", originalMap);
	}


	void SpawnResourcesInChunk(Chunk chunk, int count)
	{
		float totalTerrainHeight = numChunksY * chunkSize;
		float chunkWorldY = chunk.id.y * chunkSize;
		float chunkYNorm = chunkWorldY / totalTerrainHeight;

		// Fun resource bands: top to bottom
		// 0.0-0.1: Stone
		// 0.1-0.2: Copper
		// 0.2-0.4: Coal
		// 0.4-0.7: Gold
		// 0.7-0.9: Ruby
		// 0.9-1.0: Diamond
		GameObject prefab = null;
		string resourceName = "";
		if (chunkYNorm >= 0.9f && diamondPrefab != null) {
			prefab = diamondPrefab;
			resourceName = "Diamond";
		} else if (chunkYNorm >= 0.7f && rubyPrefab != null) {
			prefab = rubyPrefab;
			resourceName = "Ruby";
		} else if (chunkYNorm >= 0.4f && goldPrefab != null) {
			prefab = goldPrefab;
			resourceName = "Gold";
		} else if (chunkYNorm >= 0.2f && coalPrefab != null) {
			prefab = coalPrefab;
			resourceName = "Coal";
		} else if (chunkYNorm >= 0.1f && copperPrefab != null) {
			prefab = copperPrefab;
			resourceName = "Copper";
		} else if (stonePrefab != null) {
			prefab = stonePrefab;
			resourceName = "Stone";
		}

		if (prefab == null) return;

		Vector3 chunkCoord = chunk.id * (numPointsPerAxis - 1);
		float voxelSize = chunkSize / (numPointsPerAxis - 1);
		Vector3 chunkOrigin = chunkCoord * voxelSize;

		for (int i = 0; i < count; i++)
		{
			int localX = UnityEngine.Random.Range(0, numPointsPerAxis - 1);
			int localY = UnityEngine.Random.Range(0, numPointsPerAxis - 1);
			int localZ = UnityEngine.Random.Range(0, numPointsPerAxis - 1);

			Vector3 pos = chunkOrigin + new Vector3(localX * voxelSize, localY * voxelSize, localZ * voxelSize);

			var obj = Instantiate(prefab, pos, Quaternion.identity, chunk.filter.transform);
			obj.tag = "Resource";
			Debug.Log($"[Resource] Spawned {resourceName} at {pos} in chunk {chunk.id}");
		}
	}


	//Test
	//Test commitg
	void CreateBuffers()
	{
		//Creates block of memeory on the GPU
		int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
		int numVoxelsPerAxis = numPointsPerAxis - 1;
		int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
		int maxTriangleCount = numVoxels * 5;
		int maxVertexCount = maxTriangleCount * 3;

		triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		triangleBuffer = new ComputeBuffer(maxVertexCount, ComputeHelper.GetStride<VertexData>(), ComputeBufferType.Append);
		vertexDataArray = new VertexData[maxVertexCount];
	}

	void ReleaseBuffers()
	{
		ComputeHelper.Release(triangleBuffer, triCountBuffer);
	}

	void OnDestroy()
	{
		ReleaseBuffers();
		foreach (Chunk chunk in chunks)
		{
			chunk.Release();
		}
	}

	void CreateChunks()
	{
		chunks = new Chunk[numChunksX * numChunksY * numChunksZ];
		//float chunkSize = (boundsSize) / numChunks;
		int i = 0;

		for (int y = 0; y < numChunksY; y++)
		{
			for (int x = 0; x < numChunksX; x++)
			{
				for (int z = 0; z < numChunksZ; z++)
				{
					Vector3Int coord = new Vector3Int(x, y, z);
					GameObject meshHolder = new GameObject($"Chunk ({x}, {y}, {z})");
					meshHolder.transform.parent = transform;
					meshHolder.layer = gameObject.layer;
					meshHolder.transform.tag = "Diggable";
					meshHolder.layer = LayerMask.NameToLayer("Interactable");
					Chunk chunk = new Chunk(coord, chunkSize, numPointsPerAxis, meshHolder);
					chunk.SetMaterial(material);
					chunks[i] = chunk;
					i++;
				}
			}
		}
	}


	public void Terraform(Vector3 point, float weight, float radius)
	{

		// Get per-axis texture sizes
		int sizeX = rawDensityTexture.width;
		int sizeY = rawDensityTexture.height;
		int sizeZ = rawDensityTexture.volumeDepth;

		// Calculate total world size based on chunk grid and chunkSize
		float worldSizeX = numChunksX * chunkSize;
		float worldSizeY = numChunksY * chunkSize;
		float worldSizeZ = numChunksZ * chunkSize;

		// Calculate world-to-texture scale for each axis
		float pixelWorldSizeX = worldSizeX / sizeX;
		float pixelWorldSizeY = worldSizeY / sizeY;
		float pixelWorldSizeZ = worldSizeZ / sizeZ;

		// Convert world position to texture coordinates (0..size-1)
		float tx = Mathf.Clamp01(point.x / worldSizeX);
		float ty = Mathf.Clamp01(point.y / worldSizeY);
		float tz = Mathf.Clamp01(point.z / worldSizeZ);

		int editX = Mathf.RoundToInt(tx * (sizeX - 1));
		int editY = Mathf.RoundToInt(ty * (sizeY - 1));
		int editZ = Mathf.RoundToInt(tz * (sizeZ - 1));

		Vector3 editWorldPos = new Vector3(
			(tx * worldSizeX) - worldSizeX / 2f,
			(ty * worldSizeY) - worldSizeY / 2f,
			(tz * worldSizeZ) - worldSizeZ / 2f
		);
		Debug.DrawLine(point, editWorldPos, Color.red, 2f);

		// Calculate edit radius in texture space (average pixel size for spherical brush)
		float avgPixelWorldSize = (pixelWorldSizeX + pixelWorldSizeY + pixelWorldSizeZ) / 3f;
		int editRadius = Mathf.CeilToInt(radius / avgPixelWorldSize);

		// Clamp weight for strong terraforming
		float minWeight = 10f;
		if (Mathf.Abs(weight) < minWeight)
		{
			weight = minWeight * Mathf.Sign(weight);
		}

		// Set edit compute shader parameters
		editCompute.SetFloat("weight", weight);
		editCompute.SetFloat("deltaTime", Time.deltaTime);
		editCompute.SetInts("brushCentre", editX, editY, editZ);
		editCompute.SetInt("brushRadius", editRadius);
		editCompute.SetInts("textureSize", sizeX, sizeY, sizeZ);

		ComputeHelper.Dispatch(editCompute, sizeX, sizeY, sizeZ);

		// Optional: Blur for smooth edits
		if (blurMap)
		{
			blurCompute.SetInts("textureSize", sizeX, sizeY, sizeZ);
			blurCompute.SetInts("brushCentre", editX, editY, editZ);
			blurCompute.SetInt("blurRadius", blurRadius);
			blurCompute.SetInt("brushRadius", editRadius);
			ComputeHelper.Dispatch(blurCompute, sizeX, sizeY, sizeZ);
		}

		// Calculate world radius for affected chunks
		float worldRadius = (editRadius + 1 + (blurMap ? blurRadius : 0)) * avgPixelWorldSize;

		// Regenerate affected chunks
		int affectedChunks = 0;
		for (int i = 0; i < chunks.Length; i++)
		{
			Chunk chunk = chunks[i];

			Vector3 chunkCenter = (Vector3)chunk.id * chunk.size + Vector3.one * (chunk.size / 2f);
			if (MathUtility.SphereIntersectsBox(point, worldRadius, chunkCenter, Vector3.one * chunk.size))
			{
				chunk.terra = true;
				GenerateChunk(chunk);
				affectedChunks++;
			}
		}
		TryPickupResourceAtCursor();
		// Pickup resources near the dig point, using the same radius as dig
        //TryPickupNearbyResources(point, radius * 0.7f);
	}

		void Create3DTexture(ref RenderTexture texture, int width, int height, int depth, string name)
		{
			//
			var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
			if (texture == null || !texture.IsCreated() || texture.width != width || texture.height != height || texture.volumeDepth != depth || texture.graphicsFormat != format)
			{
				if (texture != null)
				{
					texture.Release();
				}
				const int numBitsInDepthBuffer = 0;
				texture = new RenderTexture(width, height, numBitsInDepthBuffer);
				texture.graphicsFormat = format;
				texture.volumeDepth = depth;
				texture.enableRandomWrite = true;
				texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;


				texture.Create();
			}
			texture.wrapMode = TextureWrapMode.Repeat;
			texture.filterMode = FilterMode.Bilinear;
			texture.name = name;
		}

	// Simple inventory system
    private Dictionary<string, int> inventory = new Dictionary<string, int>();

    void AddToInventory(string resourceType, int amount)
    {
        if (inventory.ContainsKey(resourceType))
            inventory[resourceType] += amount;
        else
            inventory[resourceType] = amount;
        Debug.Log($"Added {amount} {resourceType} to inventory. Total: {inventory[resourceType]}");
    }

    // Only pick up resources if the dig directly hits them (precise)
    void TryPickupNearbyResources(Vector3 digPosition, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(digPosition, 0.05f); // very small radius
        foreach (var hit in hits)
        {
            ResourcePickup pickup = hit.GetComponent<ResourcePickup>();
            if (pickup != null)
            {
                AddToInventory(pickup.resourceType, 1);
                Destroy(pickup.gameObject);
                Debug.Log($"Picked up {pickup.resourceType}!");
            }
        }
    }

    void TryPickupResourceAtCursor()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            ResourcePickup pickup = hit.collider.GetComponent<ResourcePickup>();
            if (pickup != null)
            {
                AddToInventory(pickup.resourceType, 1);
                Destroy(pickup.gameObject);
                Debug.Log($"Picked up {pickup.resourceType}!");
            }
        }
    }
}


