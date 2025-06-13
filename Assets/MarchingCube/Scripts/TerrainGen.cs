using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
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
	Vector3? lastTerraformPoint = null;
	float lastTerraformRadius = 1f;

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

	int totalVerts;

	// Stopwatches
	System.Diagnostics.Stopwatch timer_fetchVertexData;
	System.Diagnostics.Stopwatch timer_processVertexData;
	RenderTexture originalMap;

	public GameObject[] plantPrefabs;
	public GameObject stonePrefab, goldPrefab, diamondPrefab;
	public float spawnDistance = 50f; // Distance from player to trigger spawn

	public Transform player; // Assign in inspector or find at Start

	void Start()
	{
		//Initialize textures so it GPU optimized
		InitTextures();
		//Creates a buffer which is a memory block on the CPU
		CreateBuffers();
		//Creates a chunk 
		CreateChunks();

		var sw = System.Diagnostics.Stopwatch.StartNew();
		GenerateAllChunks();

		//Debug.Log("Generation Time: " + sw.ElapsedMilliseconds + " ms");
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

		totalVerts = 0;
		ComputeDensity();

		//Debug.Log(chunks.Length);
		for (int i = 0; i < chunks.Length; i++)
		{
			Debug.Log("Generating chunk" + chunks[i].id);
			GenerateChunk(chunks[i]);
		}
		//Debug.Log("Total verts " + totalVerts);

		// Print timers:
		//Debug.Log("Fetch vertex data: " + timer_fetchVertexData.ElapsedMilliseconds + " ms");
		//Debug.Log("Process vertex data: " + timer_processVertexData.ElapsedMilliseconds + " ms");
		//Debug.Log("Sum: " + (timer_fetchVertexData.ElapsedMilliseconds + timer_processVertexData.ElapsedMilliseconds));


	}

	void ComputeDensity()
	{
		int sizeX = rawDensityTexture.width;
		int sizeY = rawDensityTexture.height;
		int sizeZ = rawDensityTexture.volumeDepth;

		//Debug.Log(sizeX);
		//Debug.Log(sizeY);
		//Debug.Log(sizeZ);
		densityCompute.SetInts("textureSize", sizeX, sizeY, sizeZ);

		densityCompute.SetFloat("chunkSize", chunkSize);
		densityCompute.SetFloat("noiseHeightMultiplier", 0f);
		densityCompute.SetFloat("noiseScale", 0f);
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
		Debug.Log("Chunk.id: " + chunk.id);
		Debug.Log($"Chunk {chunk.id} * {numVoxelsPerAxis} = {chunkCoord}");
		Debug.Log("ChunkCoord: " + chunkCoord);
		meshCompute.SetVector("chunkCoord", chunkCoord);
		Debug.Log("numVoxelsPerAxis: " + numVoxelsPerAxis);
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
		timer_processVertexData.Stop();
	}

	void Update()
	{
		// TODO: move somewhere more sensible
		material.SetTexture("DensityTex", originalMap);
		//material.SetFloat("planetBoundsSize", boundsSize);

		// Proximity-based spawning
		foreach (var chunk in chunks)
		{
			if (chunk == null) continue;
			Vector3 chunkCenter = (Vector3)chunk.id * chunk.size + Vector3.one * (chunk.size / 2f);
			float dist = Vector3.Distance(player.position, chunkCenter);

			if (dist < spawnDistance)
			{
				if (!chunk.hasSpawnedPlants)
				{
					SpawnPlantsOnChunk(chunk, 10); // e.g. 10 plants per chunk
					chunk.hasSpawnedPlants = true;
				}
				if (!chunk.hasSpawnedResources)
				{
					SpawnResourcesInChunk(chunk, 5); // e.g. 5 resources per chunk
					chunk.hasSpawnedResources = true;
				}
			}
		}
	}

	// Spawns plants on the surface of a chunk
	void SpawnPlantsOnChunk(Chunk chunk, int count)
	{
		Vector3 chunkCoord = chunk.id * (numPointsPerAxis - 1);
		float voxelSize = chunkSize / (numPointsPerAxis - 1);
		Vector3 chunkOrigin = chunkCoord * voxelSize;

		for (int i = 0; i < count; i++)
		{
			// Pick a random X and Z in local voxel space
			int localX = UnityEngine.Random.Range(0, numPointsPerAxis - 1);
			int localZ = UnityEngine.Random.Range(0, numPointsPerAxis - 1);

			// Raycast from above the chunk to find the surface
			Vector3 rayOrigin = chunkOrigin + new Vector3(localX * voxelSize, chunkSize * numChunksY, localZ * voxelSize);

			if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, chunkSize * numChunksY * 2f, LayerMask.GetMask("Diggable")))
			{
				GameObject prefab = plantPrefabs[UnityEngine.Random.Range(0, plantPrefabs.Length)];
				Instantiate(prefab, hit.point, Quaternion.identity);
				Debug.Log($"[Plant] Spawned {prefab.name} at {hit.point} in chunk {chunk.id}");
			}
			else
			{
				Debug.Log($"[Plant] No surface hit for plant in chunk {chunk.id} at localXZ=({localX},{localZ})");
			}
		}
	}

	void SpawnResourcesInChunk(Chunk chunk, int count)
	{
		Vector3 chunkCoord = chunk.id * (numPointsPerAxis - 1);
		float voxelSize = chunkSize / (numPointsPerAxis - 1);
		Vector3 chunkOrigin = chunkCoord * voxelSize;

		for (int i = 0; i < count; i++)
		{
			int localX = UnityEngine.Random.Range(0, numPointsPerAxis - 1);
			int localY = UnityEngine.Random.Range(0, (numPointsPerAxis - 1) / 2); // Lower half
			int localZ = UnityEngine.Random.Range(0, numPointsPerAxis - 1);

			Vector3 pos = chunkOrigin + new Vector3(localX * voxelSize, localY * voxelSize, localZ * voxelSize);

			float r = UnityEngine.Random.value;
			GameObject prefab = stonePrefab;
			if (r > 0.98f) prefab = diamondPrefab;
			else if (r > 0.90f) prefab = goldPrefab;

			Instantiate(prefab, pos, Quaternion.identity);
			Debug.Log($"[Resource] Spawned {prefab.name} at {pos} in chunk {chunk.id}");
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
					Debug.Log($"Printing chunk: {{numChunksY: {y}, numChunksX: {x}, numChunksZ: {z}}}");
					//Debug.Log($"posX: {posX}");
					//Debug.Log($"posY: {posY}");
					//Debug.Log($"posZ: {posZ}");
					GameObject meshHolder = new GameObject($"Chunk ({x}, {y}, {z})");
					meshHolder.transform.parent = transform;
					meshHolder.layer = gameObject.layer;
					meshHolder.transform.tag = "Diggable";
					meshHolder.layer = LayerMask.NameToLayer("Interactable");
					//Passes ind the index of the cube
					//Pases in the center too
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
		Debug.Log($"Terraform called at {point} with weight {weight} and radius {radius}");

		// Get per-axis texture sizes
		int sizeX = rawDensityTexture.width;
		int sizeY = rawDensityTexture.height;
		int sizeZ = rawDensityTexture.volumeDepth;
		//Debug.Log($"Density texture size: {sizeX}x{sizeY}x{sizeZ}");

		// Calculate total world size based on chunk grid and chunkSize
		float worldSizeX = numChunksX * chunkSize;
		float worldSizeY = numChunksY * chunkSize;
		float worldSizeZ = numChunksZ * chunkSize;

		// Calculate world-to-texture scale for each axis
		float pixelWorldSizeX = worldSizeX / sizeX;
		float pixelWorldSizeY = worldSizeY / sizeY;
		float pixelWorldSizeZ = worldSizeZ / sizeZ;
		Debug.Log($"Pixel world size: {pixelWorldSizeX}, {pixelWorldSizeY}, {pixelWorldSizeZ}");

		// Convert world position to texture coordinates (0..size-1)
		float tx = Mathf.Clamp01(point.x / worldSizeX);
		float ty = Mathf.Clamp01(point.y / worldSizeY);
		float tz = Mathf.Clamp01(point.z / worldSizeZ);

		int editX = Mathf.RoundToInt(tx * (sizeX - 1));
		int editY = Mathf.RoundToInt(ty * (sizeY - 1));
		int editZ = Mathf.RoundToInt(tz * (sizeZ - 1));
		Debug.Log($"Edit voxel coords: {editX}, {editY}, {editZ}");

		// Add this debug visualization here:
		Vector3 editWorldPos = new Vector3(
			(tx * worldSizeX) - worldSizeX / 2f,
			(ty * worldSizeY) - worldSizeY / 2f,
			(tz * worldSizeZ) - worldSizeZ / 2f
		);
		Debug.DrawLine(point, editWorldPos, Color.red, 2f);

		// Calculate edit radius in texture space (average pixel size for spherical brush)
		float avgPixelWorldSize = (pixelWorldSizeX + pixelWorldSizeY + pixelWorldSizeZ) / 3f;
		int editRadius = Mathf.CeilToInt(radius / avgPixelWorldSize);
		Debug.Log($"Edit radius in voxels: {editRadius}");

		// Clamp weight for strong terraforming
		float minWeight = 10f;
		if (Mathf.Abs(weight) < minWeight)
		{
			weight = minWeight * Mathf.Sign(weight);
			Debug.Log($"Weight clamped to {weight}");
		}

		// Set edit compute shader parameters
		editCompute.SetFloat("weight", weight);
		editCompute.SetFloat("deltaTime", Time.deltaTime);
		editCompute.SetInts("brushCentre", editX, editY, editZ);
		editCompute.SetInt("brushRadius", editRadius);
		editCompute.SetInts("textureSize", sizeX, sizeY, sizeZ);

		Debug.Log("Dispatching editCompute...");
		ComputeHelper.Dispatch(editCompute, sizeX, sizeY, sizeZ);

		// Optional: Blur for smooth edits
		if (blurMap)
		{
			blurCompute.SetInts("textureSize", sizeX, sizeY, sizeZ);
			blurCompute.SetInts("brushCentre", editX, editY, editZ);
			blurCompute.SetInt("blurRadius", blurRadius);
			blurCompute.SetInt("brushRadius", editRadius);
			int kx = Mathf.Min(sizeX, (editRadius + blurRadius) * 2);
			int ky = Mathf.Min(sizeY, (editRadius + blurRadius) * 2);
			int kz = Mathf.Min(sizeZ, (editRadius + blurRadius) * 2);
			Debug.Log($"Dispatching blurCompute with size {kx}x{ky}x{kz}...");
			ComputeHelper.Dispatch(blurCompute, kx, ky, kz);
		}

		// Calculate world radius for affected chunks
		float worldRadius = (editRadius + 1 + (blurMap ? blurRadius : 0)) * avgPixelWorldSize;
		Debug.Log($"World radius for chunk update: {worldRadius}");

		// Regenerate affected chunks
		int affectedChunks = 0;
		for (int i = 0; i < chunks.Length; i++)
		{
			Chunk chunk = chunks[i];

			Vector3 chunkCenter = (Vector3)chunk.id * chunk.size + Vector3.one * (chunk.size / 2f);
			if (MathUtility.SphereIntersectsBox(point, worldRadius, chunkCenter, Vector3.one * chunk.size))
			{
				Debug.Log($"Chunk {i}: centre={chunk.id}, size={chunk.size}, point={point}, worldRadius={worldRadius}");
				Debug.Log($"Chunk {i} INTERSECTS!");
				chunk.terra = true;
				GenerateChunk(chunk);
				affectedChunks++;
			}
		}
		Debug.Log($"Chunks regenerated: {affectedChunks}");

		// At the end of Terraform, for debugging:
		//GenerateAllChunks();
		
		lastTerraformPoint = point;
		lastTerraformRadius = radius;
	}

	void Create3DTexture(ref RenderTexture texture, int width, int height, int depth, string name)
	{
		//
		var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
		if (texture == null || !texture.IsCreated() || texture.width != width || texture.height != height || texture.volumeDepth != depth || texture.graphicsFormat != format)
		{
			//Debug.Log ("Create tex: update noise: " + updateNoise);
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
		Debug.Log($"Created 3D Texture: {texture.width}x{texture.height}x{texture.volumeDepth}");
	}
// private void OnDrawGizmos()
// {
//     if (chunks == null) return;

//     // Draw chunk bounds
//     Color chunkBoundsColor = Color.cyan;
//     foreach (var chunk in chunks)
//     {
//         if (chunk == null) continue;
//         chunk.DrawBoundsGizmo(chunkBoundsColor);
//     }

//     // Draw last terraform area
//     if (lastTerraformPoint != null)
//     {
//         Gizmos.color = Color.red;
//         Gizmos.DrawWireSphere(lastTerraformPoint.Value, lastTerraformRadius);

//         // Optional: Draw voxel/texture edit box
//         // Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
//         // Gizmos.DrawWireCube(lastTerraformPoint.Value, Vector3.one * lastTerraformRadius * 2f);
//     }
// }


}