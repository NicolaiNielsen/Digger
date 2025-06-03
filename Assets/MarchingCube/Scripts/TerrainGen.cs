using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class TerrainGen : MonoBehaviour
{

	[Header("Init Settings")]
	public int numChunks = 1;
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

	int totalVerts;

	// Stopwatches
	System.Diagnostics.Stopwatch timer_fetchVertexData;
	System.Diagnostics.Stopwatch timer_processVertexData;
	RenderTexture originalMap;

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

		Debug.Log("Generation Time: " + sw.ElapsedMilliseconds + " ms");
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


		for (int i = 0; i < chunks.Length; i++)
		{
			GenerateChunk(chunks[i]);
		}
		Debug.Log("Total verts " + totalVerts);

		// Print timers:
		Debug.Log("Fetch vertex data: " + timer_fetchVertexData.ElapsedMilliseconds + " ms");
		Debug.Log("Process vertex data: " + timer_processVertexData.ElapsedMilliseconds + " ms");
		Debug.Log("Sum: " + (timer_fetchVertexData.ElapsedMilliseconds + timer_processVertexData.ElapsedMilliseconds));


	}

	void ComputeDensity()
	{
			int sizeX = rawDensityTexture.width;
			int sizeY = rawDensityTexture.height;
			int sizeZ = rawDensityTexture.volumeDepth;
			densityCompute.SetInts("textureSize", sizeX, sizeY, sizeZ);

			densityCompute.SetFloat("chunkSize", chunkSize);
			densityCompute.SetFloat("noiseHeightMultiplier", 0f);
			densityCompute.SetFloat("noiseScale", 0f);

			ComputeHelper.Dispatch(densityCompute, sizeX, sizeY, sizeZ);

			ProcessDensityMap();	
		}

	void ProcessDensityMap()
	{
		if (blurMap)
		{
			int size = rawDensityTexture.width;
			blurCompute.SetInts("brushCentre", 0, 0, 0);
			blurCompute.SetInt("blurRadius", blurRadius);
			blurCompute.SetInt("textureSize", rawDensityTexture.width);
			ComputeHelper.Dispatch(blurCompute, size, size, size);
		}
	}

	void GenerateChunk(Chunk chunk)
	{
		// Marching cubes
		int numVoxelsPerAxis = numPointsPerAxis - 1;
		int marchKernel = 0;
		meshCompute.SetInt("textureSize", processedDensityTexture.width);
		meshCompute.SetInt("numPointsPerAxis", numPointsPerAxis);
		meshCompute.SetFloat("isoLevel", isoLevel);
		meshCompute.SetFloat("planetSize", boundsSize);
		triangleBuffer.SetCounterValue(0);
		meshCompute.SetBuffer(marchKernel, "triangles", triangleBuffer);

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
		timer_processVertexData.Stop();
	}

	void Update()
	{

		// TODO: move somewhere more sensible
		material.SetTexture("DensityTex", originalMap);
		material.SetFloat("planetBoundsSize", boundsSize);
	}



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
					float posX = (-(numChunksX - 1f) / 2 + x) * chunkSize;
					float posY = (-(numChunksY - 1f) / 2 + y) * chunkSize;
					float posZ = (-(numChunksZ - 1f) / 2 + z) * chunkSize;
					Vector3 centre = new Vector3(posX, posY, posZ);

					GameObject meshHolder = new GameObject($"Chunk ({x}, {y}, {z})");
					meshHolder.transform.parent = transform;
					meshHolder.layer = gameObject.layer;
					meshHolder.transform.tag = "Diggable";
					meshHolder.layer = LayerMask.NameToLayer("Interactable");

					Chunk chunk = new Chunk(coord, centre, chunkSize, numPointsPerAxis, meshHolder);
					chunk.SetMaterial(material);
					chunks[i] = chunk;
					i++;
				}
			}
		}
	}


	public void Terraform(Vector3 point, float weight, float radius)
	{
		int editTextureSize = rawDensityTexture.width;
		float editPixelWorldSize = boundsSize / editTextureSize;
		int editRadius = Mathf.CeilToInt(radius / editPixelWorldSize);

		float tx = Mathf.Clamp01((point.x + boundsSize / 2) / boundsSize);
		float ty = Mathf.Clamp01((point.y + boundsSize / 2) / boundsSize);
		float tz = Mathf.Clamp01((point.z + boundsSize / 2) / boundsSize);

		int editX = Mathf.RoundToInt(tx * (editTextureSize - 1));
		int editY = Mathf.RoundToInt(ty * (editTextureSize - 1));
		int editZ = Mathf.RoundToInt(tz * (editTextureSize - 1));

		// Clamp weight to ensure strong terraforming
		float minWeight = 10f; // Try 10, 20, or even higher for stronger effect
		if (Mathf.Abs(weight) < minWeight)
			weight = minWeight * Mathf.Sign(weight);

		editCompute.SetFloat("weight", weight);
		editCompute.SetFloat("deltaTime", Time.deltaTime);
		editCompute.SetInts("brushCentre", editX, editY, editZ);
		editCompute.SetInt("brushRadius", editRadius);

		editCompute.SetInt("size", editTextureSize);
		ComputeHelper.Dispatch(editCompute, editTextureSize, editTextureSize, editTextureSize);

		int size = rawDensityTexture.width;

		if (blurMap)
		{
			blurCompute.SetInt("textureSize", rawDensityTexture.width);
			blurCompute.SetInts("brushCentre", editX - blurRadius - editRadius, editY - blurRadius - editRadius, editZ - blurRadius - editRadius);
			blurCompute.SetInt("blurRadius", blurRadius);
			blurCompute.SetInt("brushRadius", editRadius);
			int k = (editRadius + blurRadius) * 2;
			ComputeHelper.Dispatch(blurCompute, k, k, k);
		}

		float worldRadius = (editRadius + 1 + ((blurMap) ? blurRadius : 0)) * editPixelWorldSize;
		for (int i = 0; i < chunks.Length; i++)
		{
			Chunk chunk = chunks[i];
			if (MathUtility.SphereIntersectsBox(point, worldRadius, chunk.centre, Vector3.one * chunk.size))
			{
				chunk.terra = true;
				GenerateChunk(chunk);
			}
		}
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
	private void OnDrawGizmos()
    {
        if (chunks == null) return;

        // Choose a color for chunk bounds gizmos
        Color chunkBoundsColor = Color.cyan;

        foreach (var chunk in chunks)
        {
            if (chunk == null) continue;

            // Draw each chunk's bounds gizmo
            chunk.DrawBoundsGizmo(chunkBoundsColor);
        }
    }


}