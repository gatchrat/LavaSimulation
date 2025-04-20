using UnityEngine;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using UnityEngine.UI;
using Unity.Mathematics;
public struct HashEntry
{
    public uint hash;
    public uint index;
}
public class SimulationSpawner3D : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public ComputeShader ComputeShader;
    public ComputeShader marchingCubesShader;
    public MeshFilter LavaGeneratedMesh;
    public ComputeShader DensityCacher;
    public ComputeShader PositionPredicter;
    public ComputeShader HashgridCalculator;
    public ComputeShader DensityFieldCalculator;
    public Gradient colourMap;
    public int XCount;
    public int YCount;
    public int ZCount;
    public float BoundsWidth = 14;
    public float BoundsHeight = 8;
    public float BoundsDepth = 14;
    public float TargetDensity;
    public float PressureMultiplier;
    public float SmoothingRadius = 10f;
    public float MaxSpeed = 1f;
    private Mesh Mesh;
    public Material Material;
    public Boolean RandomSpawns = false;
    public float Viscosity = 1f;
    private LavaPoint[] Points;
    private MaterialPropertyBlock props;
    private List<Matrix4x4> matrices = new List<Matrix4x4>();
    private List<Vector4> colors = new List<Vector4>();
    private int HashesBufferSize;
    private HashEntry[] Hashes;
    private int NumOfPossibleHashes;
    private uint[] StartingIndizes;
    public float isoLevel = 0.3f;

    [Range(0.05f, 1f)]
    public float voxelSize = 0.1f;
    float[,,] densityField;
    ComputeBuffer LavaBuffer;
    Vector3[] PredictedPositions;
    private float BoundsWidthAtStart;
    private float BoundsDepthAtStart;
    void Start()
    {
        BoundsWidthAtStart = BoundsWidth;
        BoundsDepthAtStart = BoundsDepth;
        Points = new LavaPoint[XCount * YCount * ZCount];
        for (int x = 0; x < XCount; x++)
        {
            for (int y = 0; y < YCount; y++)
            {
                for (int z = 0; z < ZCount; z++)
                {
                    InitLava(x, y, z);
                }
            }
        }
        PredictedPositions = new Vector3[Points.Length];
        props = new MaterialPropertyBlock();
        GameObject HolySphere = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh = HolySphere.GetComponent<MeshFilter>().sharedMesh;
        HolySphere.SetActive(false);

        NumOfPossibleHashes = 1024;
        HashesBufferSize = Mathf.NextPowerOfTwo(Points.Length);
        Debug.Log("Hashbuffersize:" + HashesBufferSize);
        Hashes = new HashEntry[HashesBufferSize];
        // ComputeLava();
    }
    void Update()
    {
        ComputeLava();
        //RenderLava();
    }
    private void RenderLava()
    {
        //        Debug.Log(Points[0].Color);
        // Render in batches of 1023 (Unity limitation)
        matrices.Clear();
        colors.Clear();
        foreach (var p in Points)
        {
            matrices.Add(Matrix4x4.TRS(p.Position, Quaternion.identity, Vector3.one * 0.1f));
            Color color = colourMap.Evaluate(p.Velocity.magnitude);
            colors.Add(color);
        }
        for (int i = 0; i < matrices.Count; i += 1023)
        {
            int count = Mathf.Min(1023, matrices.Count - i);
            props.Clear();
            props.SetVectorArray("_Color", colors.GetRange(i, count));
            Graphics.DrawMeshInstanced(Mesh, 0, Material, matrices.GetRange(i, count), props);
        }
    }
    private void RenderLavaByHash()
    {
        // Render in batches of 1023 (Unity limitation)
        matrices.Clear();
        colors.Clear();
        foreach (var p in Points)
        {
            matrices.Add(Matrix4x4.TRS(p.Position, Quaternion.identity, Vector3.one * 0.1f));
            colors.Add(Color.white);
        }
        for (int i = 0; i < Hashes.Length - 1; i++)
        {
            if ((int)Hashes[i].index > 0 && (int)Hashes[i].index < colors.Count)
            {
                Color color = colourMap.Evaluate(((float)Hashes[i].hash) / NumOfPossibleHashes);
                colors[(int)Hashes[i].index] = color;
            }

        }

        for (int i = 0; i < matrices.Count; i += 1023)
        {
            int count = Mathf.Min(1023, matrices.Count - i);
            props.Clear();
            props.SetVectorArray("_Color", colors.GetRange(i, count));
            Graphics.DrawMeshInstanced(Mesh, 0, Material, matrices.GetRange(i, count), props);
        }
    }
    private void RenderLavaAsMesh()
    {
        ComputeBuffer PositionBuffer = new ComputeBuffer(Points.Length, sizeof(float) * 3);
        PositionBuffer.SetData(PredictedPositions);

        int width = densityField.GetLength(0);
        int height = densityField.GetLength(1);
        int depth = densityField.GetLength(2);

        RenderTexture densityTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
        densityTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        densityTexture.volumeDepth = depth;
        densityTexture.enableRandomWrite = true;
        densityTexture.wrapMode = TextureWrapMode.Clamp;
        densityTexture.filterMode = FilterMode.Bilinear;
        densityTexture.Create();

        ComputeBuffer DensityValuesBuffer = new ComputeBuffer(width * height * depth, sizeof(float));
        float[] DensityValues = new float[width * height * depth];
        DensityValuesBuffer.SetData(DensityValues);

        ComputeBuffer HashesBuffer = new ComputeBuffer(Hashes.Length, Marshal.SizeOf(typeof(HashEntry)));
        HashesBuffer.SetData(Hashes);

        ComputeBuffer StartingIndizesBuffer = new ComputeBuffer(StartingIndizes.Length, sizeof(uint));
        StartingIndizesBuffer.SetData(StartingIndizes);

        DensityFieldCalculator.SetTexture(0, "DensityTexture", densityTexture);
        DensityFieldCalculator.SetBuffer(0, "PredictedPosition", PositionBuffer);
        DensityFieldCalculator.SetBuffer(0, "DensityValuesBuffer", DensityValuesBuffer);
        DensityFieldCalculator.SetBuffer(0, "Hashes", HashesBuffer);
        DensityFieldCalculator.SetBuffer(0, "StartingIndizes", StartingIndizesBuffer);
        DensityFieldCalculator.SetFloat("SmoothingRadius", SmoothingRadius);
        DensityFieldCalculator.SetInt("NumOfPossibleHashes", NumOfPossibleHashes);
        DensityFieldCalculator.SetInt("ParticleCount", Points.Length);
        DensityFieldCalculator.SetInt("FieldWidth", width);
        DensityFieldCalculator.SetInt("FieldHeight", height);
        DensityFieldCalculator.SetInt("FieldDepth", depth);
        DensityFieldCalculator.SetFloat("VoxelSize", voxelSize);

        int dispatchX = Mathf.CeilToInt(densityTexture.width / 8.0f);
        int dispatchY = Mathf.CeilToInt(densityTexture.height / 8.0f);
        int dispatchZ = Mathf.CeilToInt(densityTexture.volumeDepth / 8.0f);
        DensityFieldCalculator.Dispatch(0, dispatchX, dispatchY, dispatchZ);

        DensityValuesBuffer.GetData(DensityValues);
        PositionBuffer.GetData(PredictedPositions);
        DensityValuesBuffer.Dispose();
        StartingIndizesBuffer.Dispose();
        HashesBuffer.Dispose();
        /*

                float min = float.MaxValue;
                float max = float.MinValue;
                int numOfValues = 0;

                // Loop through each slice of the 3D texture
                for (int z = 0; z < densityTexture.volumeDepth; z++)
                {
                    for (int y = 0; y < densityTexture.height; y++)
                    {
                        for (int x = 0; x < densityTexture.width; x++)
                        {
                            if (DensityValues[x + y * width + z * width * height] > 0)
                            {
                                numOfValues++;

                            }
                            min = Mathf.Min(min, DensityValues[x + y * width + z * width * height]);
                            max = Mathf.Max(max, DensityValues[x + y * width + z * width * height]);
                        }
                    }
                }
                int numOfPositions = 0;
                foreach (var Position in PredictedPositions)
                {
                    if (Position.magnitude > 0)
                    {
                        numOfPositions++;
                    }
                }

                Debug.Log("Min Value: " + min);
                Debug.Log("Max Value: " + max);
                Debug.Log("FilledValues" + numOfValues);
                Debug.Log("Width:" + width);
                Debug.Log("Height:" + height);
                Debug.Log("Depth" + depth);
                Debug.Log("ParticleCount" + Points.Length);
                Debug.Log("FilledPositions" + numOfPositions);
                Debug.Log("Hash of Particle 0 " + CalcHash(Points[0].Position + new Vector3(10, 0, 10)));
                Debug.Log("Startindex Hash of Particle 0 " + StartingIndizes[CalcHash(Points[0].Position + new Vector3(10, 0, 10))]);
                Debug.Log("Startindex of next Hash" + StartingIndizes[CalcHash(Points[0].Position + new Vector3(10, 0, 10)) + 1]);
                Debug.Log("Hash of Particle 100 " + CalcHash(Points[100].Position + new Vector3(10, 0, 10)));
                Debug.Log("Startindex Hash of Particle 100 " + StartingIndizes[CalcHash(Points[100].Position + new Vector3(10, 0, 10))]);

                for (int i = 0; i < Hashes.Length; i++)
                {
                    if (CalcHash(Points[0].Position + new Vector3(10, 0, 10)) == Hashes[i].hash)
                    {
                        Debug.Log("Found hash for Particle 0 at:" + i);
                    }
                }*/
        //Marching Cubes 
        ComputeBuffer EdgeTableBuffer = new ComputeBuffer(256, sizeof(int));
        ComputeBuffer TriTableBuffer = new ComputeBuffer(256 * 16, sizeof(int));
        EdgeTableBuffer.SetData(MarchingCubesTables.edge_table);
        int[] triTable = new int[256 * 16];
        for (int i = 0; i < 256; i++)
        {
            for (int j = 0; j < 16; j++)
            {
                triTable[i * 16 + j] = MarchingCubesTables.TriTable[i, j];
            }
        }
        TriTableBuffer.SetData(triTable);
        int numVoxelsX = width - 1;
        int numVoxelsY = height - 1;
        int numVoxelsZ = depth - 1;
        ComputeBuffer vertexBuffer = new ComputeBuffer(width * height * depth * 30, sizeof(float) * 3);
        ComputeBuffer vertexCountBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        uint[] initialCount = new uint[] { 0 };
        vertexCountBuffer.SetData(initialCount);

        vertexBuffer.SetCounterValue(0);
        marchingCubesShader.SetFloat("BoundsHeight", BoundsHeight);
        marchingCubesShader.SetFloat("BoundsDepth", BoundsDepthAtStart);
        marchingCubesShader.SetFloat("BoundsWidth", BoundsWidthAtStart);
        marchingCubesShader.SetFloat("VoxelSize", voxelSize);
        marchingCubesShader.SetBuffer(0, "VertexCount", vertexCountBuffer);
        marchingCubesShader.SetTexture(0, "DensityTexture", densityTexture);
        marchingCubesShader.SetBuffer(0, "VertexBuffer", vertexBuffer);
        marchingCubesShader.SetBuffer(0, "EdgeTable", EdgeTableBuffer);
        marchingCubesShader.SetBuffer(0, "TriTable", TriTableBuffer);
        marchingCubesShader.SetInts("GridSize", numVoxelsX, numVoxelsY, numVoxelsZ);
        marchingCubesShader.SetFloat("IsoLevel", isoLevel);
        marchingCubesShader.Dispatch(0, dispatchX, dispatchY, dispatchZ);


        //Build Mesh
        // Allocate arrays
        uint[] vertexCountArray = new uint[1];
        vertexCountBuffer.GetData(vertexCountArray);
        vertexCountBuffer.Dispose();
        int vertexCount = (int)vertexCountArray[0];

        // Debug.Log("Total vertices generated: " + vertexCount);

        // Allocate arrays for mesh creation
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[vertexCount];

        // Read the vertex data - only read up to the actual count
        vertexBuffer.GetData(vertices, 0, 0, vertexCount);
        vertexBuffer.Dispose();

        // Generate triangle indices
        for (int i = 0; i < vertexCount; i++)
        {
            triangles[i] = i;
        }
        /* min = float.MaxValue;
         max = float.MinValue;



         for (int x = 0; x < vertices.Length; x++)
         {

             min = Mathf.Min(min, vertices[x].magnitude);
             max = Mathf.Max(max, vertices[x].magnitude);
         }*/

        // Build mesh
        Mesh mesh = new Mesh();
        /*Debug.Log("Vertices:" + vertices.Length);
        Debug.Log("MaxVerticesMagnitude:" + max);
        Debug.Log("MinVerticesMagnitude:" + min);
        Debug.Log("Triangules:" + triangles.Length);
        for (int i = 0; i < vertices.Length; i += 3)
        {
            Debug.Log(VertMapper(vertices[i] - new Vector3(5, 0, 4)) + ',' + VertMapper(vertices[i + 1] - new Vector3(5, 0, 4)) + ',' + VertMapper(vertices[i + 2] - new Vector3(5, 0, 4)));

        }

*/
        if (vertexCount > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        LavaGeneratedMesh.mesh = mesh;
        PositionBuffer.Dispose();
        TriTableBuffer.Dispose();
        EdgeTableBuffer.Dispose();

    }
    private String VertMapper(Vector3 vert)
    {
        if (vert == new Vector3(0.85f, 1, 0.9f))
        {
            return ("Links");
        }
        if (vert == new Vector3(0.95f, 1, 0.9f))
        {
            return ("Rechts");
        }
        if (vert == new Vector3(0.9f, 1.05f, 0.9f))
        {
            return ("Oben");
        }
        if (vert == new Vector3(0.9f, 0.95f, 0.9f))
        {
            return ("Unten");
        }
        if (vert == new Vector3(0.9f, 1, 0.85f))
        {
            return ("Vorne");
        }
        if (vert == new Vector3(0.9f, 1, 0.95f))
        {
            return ("Hinten");
        }
        return "kp";
    }
    private void InitLava(int x, int y, int z)
    {

        LavaPoint Point = new LavaPoint
        {
            Color = Color.white
        };
        if (!RandomSpawns)
        {
            Point.Position = new Vector3((-XCount / 2 + x) * 0.2f, y * 0.2f, (-ZCount / 2 + z) * 0.2f);
        }
        else
        {
            Point.Position = new Vector3(
                UnityEngine.Random.Range(-BoundsWidth / 2, BoundsWidth / 2) * 0.1f,
             UnityEngine.Random.Range(0, BoundsHeight) * 0.1f,
             UnityEngine.Random.Range(-BoundsDepth / 2, BoundsDepth / 2) * 0.1f);
        }
        Points[z + y * ZCount + x * ZCount * YCount] = Point;
        densityField = new float[(int)(BoundsWidth / voxelSize), (int)(BoundsHeight / voxelSize), (int)(BoundsDepth / voxelSize)];
    }
    private void ComputeLava()
    {
        int PositionSize = sizeof(float) * 3;
        int ColorSize = sizeof(float) * 4;
        int VelocitySize = sizeof(float) * 3;
        int TotalSize = PositionSize + ColorSize + VelocitySize;
        float[] Densities = new float[Points.Length];


        LavaBuffer = new ComputeBuffer(Points.Length, TotalSize);
        LavaBuffer.SetData(Points);
        ComputeBuffer DensityBuffer = new ComputeBuffer(Points.Length, sizeof(float));
        DensityBuffer.SetData(Densities);


        //Predict Positions 1 frame in the future, to improve reaction timing
        ComputeBuffer PositionBuffer = new ComputeBuffer(Points.Length, sizeof(float) * 3);
        PositionBuffer.SetData(PredictedPositions);
        PositionPredicter.SetBuffer(0, "Points", LavaBuffer);
        PositionPredicter.SetBuffer(0, "PredictedPosition", PositionBuffer);
        PositionPredicter.Dispatch(0, Points.Length / 10, 1, 1);

        //Generate Position Hashes for performant Neighbor search

        for (int i = 0; i < HashesBufferSize; i++)
        {
            Hashes[i] = new HashEntry { hash = 0xFFFFFFFF, index = uint.MaxValue };
        }
        ComputeBuffer HashesBuffer = new ComputeBuffer(Hashes.Length, Marshal.SizeOf(typeof(HashEntry)));
        HashesBuffer.SetData(Hashes);
        StartingIndizes = new uint[NumOfPossibleHashes];
        for (int i = 0; i < StartingIndizes.Length; i++)
            StartingIndizes[i] = 0xFFFFFFFF; // -1 as unsigned

        ComputeBuffer StartingIndizesBuffer = new ComputeBuffer(StartingIndizes.Length, sizeof(uint));
        StartingIndizesBuffer.SetData(StartingIndizes);

        HashgridCalculator.SetBuffer(0, "Hashes", HashesBuffer);
        HashgridCalculator.SetBuffer(1, "Hashes", HashesBuffer);
        HashgridCalculator.SetBuffer(2, "Hashes", HashesBuffer);
        HashgridCalculator.SetBuffer(2, "StartingIndizes", StartingIndizesBuffer);
        HashgridCalculator.SetInt("ParticleCount", Points.Length);
        HashgridCalculator.SetInt("NumOfPossibleHashes", NumOfPossibleHashes);
        HashgridCalculator.SetInt("HashesBufferSize", HashesBufferSize);
        HashgridCalculator.SetBuffer(0, "Points", LavaBuffer);
        HashgridCalculator.SetFloat("SmoothingRadius", SmoothingRadius);
        HashgridCalculator.Dispatch(0, 1024, 1, 1);


        //https://github.com/SebLague/Fluid-Sim/blob/Episode-01/Assets/Scripts/Compute%20Helpers/GPU%20Sort/GPUSort.cs
        HashgridCalculator.SetInt("numEntries", Hashes.Length);
        // Launch each step of the sorting algorithm (once the previous step is complete)
        // Number of steps = [log2(n) * (log2(n) + 1)] / 2
        // where n = nearest power of 2 that is greater or equal to the number of inputs
        int numStages = (int)Math.Log(Hashes.Length, 2);

        for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
        {
            for (int stepIndex = 0; stepIndex < stageIndex + 1; stepIndex++)
            {
                // Calculate some pattern stuff
                int groupWidth = 1 << (stageIndex - stepIndex);
                int groupHeight = 2 * groupWidth - 1;
                HashgridCalculator.SetInt("groupWidth", groupWidth);
                HashgridCalculator.SetInt("groupHeight", groupHeight);
                HashgridCalculator.SetInt("stepIndex", stepIndex);
                // Run the sorting step on the GPU
                HashgridCalculator.Dispatch(1, Hashes.Length / 2, 1, 1);
            }
        }
        //----------------------------------------------------------------------------------





        HashgridCalculator.Dispatch(2, 1024, 1, 1);
        StartingIndizesBuffer.GetData(StartingIndizes);
        HashesBuffer.GetData(Hashes);
        //Cachses Densities to prevent expensive recalculation over and over again
        DensityCacher.SetBuffer(0, "Points", LavaBuffer);
        DensityCacher.SetBuffer(0, "Hashes", HashesBuffer);
        DensityCacher.SetBuffer(0, "StartingIndizes", StartingIndizesBuffer);
        DensityCacher.SetInt("NumOfPossibleHashes", NumOfPossibleHashes);
        DensityCacher.SetBuffer(0, "CachedDensities", DensityBuffer);
        DensityCacher.SetBuffer(0, "PredictedPosition", PositionBuffer);
        DensityCacher.SetFloat("TimePassed", Time.deltaTime);
        DensityCacher.SetFloat("SmoothingRadius", SmoothingRadius);
        DensityCacher.SetInt("ParticleCount", Points.Length);
        DensityCacher.Dispatch(0, Points.Length / 10, 1, 1);

        ComputeShader.SetBuffer(0, "CachedDensities", DensityBuffer);
        ComputeShader.SetBuffer(0, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(0, "Points", LavaBuffer);
        ComputeShader.SetInt("ParticleCount", Points.Length);
        ComputeShader.SetFloat("MaxSpeed", MaxSpeed);
        ComputeShader.SetFloat("BoundsHeight", BoundsHeight);
        ComputeShader.SetFloat("BoundsDepth", BoundsDepth);
        ComputeShader.SetFloat("BoundsWidth", BoundsWidth);
        ComputeShader.SetFloat("ViscosityMultiplier", Viscosity);
        ComputeShader.SetFloat("TimePassed", Time.deltaTime);
        ComputeShader.SetFloat("TargetDensity", TargetDensity);
        ComputeShader.SetFloat("PressureMultiplier", PressureMultiplier);
        ComputeShader.SetFloat("SmoothingRadius", SmoothingRadius);
        ComputeShader.Dispatch(0, Points.Length / 10, 1, 1);

        LavaBuffer.GetData(Points);
        PositionBuffer.GetData(PredictedPositions);
        //RenderLavaAsMesh();
        RenderLava();
        //RenderLavaByHash();
        LavaBuffer.Dispose();
        DensityBuffer.Dispose();
        PositionBuffer.Dispose();
        HashesBuffer.Dispose();
        StartingIndizesBuffer.Dispose();
    }
    uint CalcHash(Vector3 Position)
    {
        const uint P1 = 73856093;
        const uint P2 = 19349663;
        const uint P3 = 83492791;

        int xi = (int)Math.Floor(Position.x / SmoothingRadius);
        int yi = (int)Math.Floor(Position.y / SmoothingRadius);
        int zi = (int)Math.Floor(Position.z / SmoothingRadius);

        // Multiply as signed, then cast to unsigned to match HLSL's behavior
        uint xPart = (uint)(xi * (int)P1);
        uint yPart = (uint)(yi * (int)P2);
        uint zPart = (uint)(zi * (int)P3);


        return (xPart ^ yPart ^ zPart) % (uint)NumOfPossibleHashes;
    }
}