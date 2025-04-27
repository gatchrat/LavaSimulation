using UnityEngine;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
public struct HashEntry
{
    public uint hash;
    public uint index;
}
public enum RenderMode
{
    ByHash,
    ByVelocity,
    ByDensity,
    AsMesh
}
public class SimulationSpawner3D : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public ComputeShader ComputeShader;

    public MeshFilter LavaGeneratedMesh;
    public Gradient colourMap;
    public RenderMode Renderer;
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
    private RenderTexture SDFTexture;
    private ComputeBuffer PositionBuffer;

    void Start()
    {
        LoadSDF();

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

        NumOfPossibleHashes = 4096;
        HashesBufferSize = Mathf.NextPowerOfTwo(Points.Length);
        Debug.Log("Hashbuffersize:" + HashesBufferSize);
        Hashes = new HashEntry[HashesBufferSize];
    }
    void Update()
    {
        ComputeLava(Mathf.Min(Time.deltaTime, 1f / 60f));//Run at atleast 60fps, slow down the simulation if framerate not reached to prevent explosion
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
    private void ComputeLava(float TimeStep)
    {
        int PositionSize = sizeof(float) * 3;
        int ColorSize = sizeof(float) * 4;
        int VelocitySize = sizeof(float) * 3;
        int TotalSize = PositionSize + ColorSize + VelocitySize;
        int CurrentKernel = 0;
        float[] Densities = new float[Points.Length];

        LavaBuffer = new ComputeBuffer(Points.Length, TotalSize);
        LavaBuffer.SetData(Points);

        //Predict Positions 1 frame in the future, to improve reaction timing
        PositionBuffer = new ComputeBuffer(Points.Length, sizeof(float) * 3);
        PositionBuffer.SetData(PredictedPositions);
        CurrentKernel = ComputeShader.FindKernel("PredictPositions");
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 10, 1, 1);

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
        CurrentKernel = ComputeShader.FindKernel("CalcHashes");
        ComputeShader.SetBuffer(CurrentKernel, "Hashes", HashesBuffer);
        ComputeShader.SetInt("ParticleCount", Points.Length);
        ComputeShader.SetInt("NumOfPossibleHashes", NumOfPossibleHashes);
        ComputeShader.SetInt("HashesBufferSize", HashesBufferSize);
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetFloat("SmoothingRadius", SmoothingRadius);
        ComputeShader.Dispatch(CurrentKernel, 1024, 1, 1);

        //---------https://github.com/SebLague/Fluid-Sim/blob/Episode-01/Assets/Scripts/Compute%20Helpers/GPU%20Sort/GPUSort.cs
        ComputeShader.SetInt("numEntries", Hashes.Length);
        // Launch each step of the sorting algorithm (once the previous step is complete)
        // Number of steps = [log2(n) * (log2(n) + 1)] / 2
        // where n = nearest power of 2 that is greater or equal to the number of inputs
        int numStages = (int)Math.Log(Hashes.Length, 2);
        CurrentKernel = ComputeShader.FindKernel("SortHashes");
        ComputeShader.SetBuffer(CurrentKernel, "Hashes", HashesBuffer);
        for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
        {
            for (int stepIndex = 0; stepIndex < stageIndex + 1; stepIndex++)
            {
                // Calculate some pattern stuff
                int groupWidth = 1 << (stageIndex - stepIndex);
                int groupHeight = 2 * groupWidth - 1;
                ComputeShader.SetInt("groupWidth", groupWidth);
                ComputeShader.SetInt("groupHeight", groupHeight);
                ComputeShader.SetInt("stepIndex", stepIndex);
                // Run the sorting step on the GPU
                ComputeShader.Dispatch(CurrentKernel, Hashes.Length / 2, 1, 1);
            }
        }
        //----------------------------------------------------------------------------------
        CurrentKernel = ComputeShader.FindKernel("CalcStartingIndizes");
        ComputeShader.SetBuffer(CurrentKernel, "Hashes", HashesBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "StartingIndizes", StartingIndizesBuffer);
        ComputeShader.Dispatch(CurrentKernel, 1024, 1, 1);
        StartingIndizesBuffer.GetData(StartingIndizes);
        HashesBuffer.GetData(Hashes);

        //Cachses Densities to prevent expensive recalculation over and over again
        ComputeBuffer DensityBuffer = new ComputeBuffer(Points.Length, sizeof(float));
        DensityBuffer.SetData(Densities);
        CurrentKernel = ComputeShader.FindKernel("DensityCache");
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "Hashes", HashesBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "StartingIndizes", StartingIndizesBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "CachedDensities", DensityBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetFloat("TimePassed", TimeStep);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 10, 1, 1);

        //Calculate Forces and Movement
        CurrentKernel = ComputeShader.FindKernel("Simulation");
        ComputeShader.SetTexture(CurrentKernel, "SDFTexture", SDFTexture);
        ComputeShader.SetBuffer(CurrentKernel, "Hashes", HashesBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "StartingIndizes", StartingIndizesBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "CachedDensities", DensityBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetFloat("MaxSpeed", MaxSpeed);
        ComputeShader.SetFloat("BoundsHeight", BoundsHeight);
        ComputeShader.SetFloat("BoundsDepth", BoundsDepth);
        ComputeShader.SetFloat("BoundsWidth", BoundsWidth);
        ComputeShader.SetFloat("ViscosityMultiplier", Viscosity);
        ComputeShader.SetFloat("TargetDensity", TargetDensity);
        ComputeShader.SetFloat("PressureMultiplier", PressureMultiplier);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 10, 1, 1);

        LavaBuffer.GetData(Points);
        PositionBuffer.GetData(PredictedPositions);

        //Render Results
        if (Renderer == RenderMode.AsMesh)
        {
            RenderLavaAsMesh();
        }
        else if (Renderer == RenderMode.ByVelocity || Renderer == RenderMode.ByDensity)
        {
            RenderLava();
        }
        else if (Renderer == RenderMode.ByHash)
        {
            RenderLavaByHash();
        }

        //Free Up Memory
        LavaBuffer.Dispose();
        DensityBuffer.Dispose();
        PositionBuffer.Dispose();
        HashesBuffer.Dispose();
        StartingIndizesBuffer.Dispose();
    }
    //----------------------------------RENDERER------------------------------------------
    private void RenderLava()
    {
        // Render in batches of 1023 (Unity limitation)
        matrices.Clear();
        colors.Clear();
        foreach (var p in Points)
        {
            matrices.Add(Matrix4x4.TRS(p.Position, Quaternion.identity, Vector3.one * 0.1f));
            Color color;
            if (Renderer == RenderMode.ByVelocity)
            {
                color = colourMap.Evaluate(p.Velocity.magnitude);
            }
            else
            {
                color = colourMap.Evaluate(p.Color.x / 100);
            }

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

        int width = densityField.GetLength(0);
        int height = densityField.GetLength(1);
        int depth = densityField.GetLength(2);

        RenderTexture densityTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = depth,
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        densityTexture.Create();

        ComputeBuffer DensityValuesBuffer = new ComputeBuffer(width * height * depth, sizeof(float));
        float[] DensityValues = new float[width * height * depth];
        DensityValuesBuffer.SetData(DensityValues);

        ComputeBuffer HashesBuffer = new ComputeBuffer(Hashes.Length, Marshal.SizeOf(typeof(HashEntry)));
        HashesBuffer.SetData(Hashes);

        ComputeBuffer StartingIndizesBuffer = new ComputeBuffer(StartingIndizes.Length, sizeof(uint));
        StartingIndizesBuffer.SetData(StartingIndizes);

        int CurrentKernel = ComputeShader.FindKernel("DensityField");
        ComputeShader.SetTexture(CurrentKernel, "DensityTexture", densityTexture);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "DensityValuesBuffer", DensityValuesBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "Hashes", HashesBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "StartingIndizes", StartingIndizesBuffer);
        ComputeShader.SetInt("FieldWidth", width);
        ComputeShader.SetInt("FieldHeight", height);
        ComputeShader.SetInt("FieldDepth", depth);
        ComputeShader.SetFloat("VoxelSize", voxelSize);

        int dispatchX = Mathf.CeilToInt(densityTexture.width / 8.0f);
        int dispatchY = Mathf.CeilToInt(densityTexture.height / 8.0f);
        int dispatchZ = Mathf.CeilToInt(densityTexture.volumeDepth / 8.0f);
        ComputeShader.Dispatch(CurrentKernel, dispatchX, dispatchY, dispatchZ);

        DensityValuesBuffer.GetData(DensityValues);
        PositionBuffer.GetData(PredictedPositions);

        DensityValuesBuffer.Dispose();
        StartingIndizesBuffer.Dispose();
        HashesBuffer.Dispose();

        //Marching Cubes 
        ComputeBuffer EdgeTableBuffer = new ComputeBuffer(256, sizeof(int));
        EdgeTableBuffer.SetData(MarchingCubesTables.edge_table);

        ComputeBuffer TriTableBuffer = new ComputeBuffer(256 * 16, sizeof(int));
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

        CurrentKernel = ComputeShader.FindKernel("March");
        ComputeShader.SetBuffer(CurrentKernel, "VertexCount", vertexCountBuffer);
        ComputeShader.SetTexture(CurrentKernel, "DensityTexture", densityTexture);
        ComputeShader.SetBuffer(CurrentKernel, "VertexBuffer", vertexBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "EdgeTable", EdgeTableBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "TriTable", TriTableBuffer);
        ComputeShader.SetInts("GridSize", numVoxelsX, numVoxelsY, numVoxelsZ);
        ComputeShader.SetFloat("IsoLevel", isoLevel);
        ComputeShader.Dispatch(CurrentKernel, dispatchX, dispatchY, dispatchZ);

        //Build Mesh
        // Allocate arrays
        uint[] vertexCountArray = new uint[1];
        vertexCountBuffer.GetData(vertexCountArray);
        vertexCountBuffer.Dispose();
        int vertexCount = (int)vertexCountArray[0];

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

        // Build mesh
        Mesh mesh = new Mesh();
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

    private void LoadSDF()
    {
        SDFTexture = new RenderTexture(30, 30, 0, RenderTextureFormat.RFloat)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = 30,
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        SDFTexture.Create();

        float[] SDFValues = new float[30 * 30 * 30];
        TextAsset mytxtData = Resources.Load<TextAsset>("EarthSDF_30");
        string txt = mytxtData.text;
        string[] Values = txt.Split(' ');
        Debug.Log("Loaded: " + Values.Length);
        //Last value is empty, first 3 show how many sample points are taken, second 3 show the bounds of the scanner
        for (int i = 6; i < Values.Length - 1; i++)
        {
            SDFValues[i - 6] = float.Parse(Values[i]);
        }

        ComputeBuffer SDFValueBuffer = new ComputeBuffer(SDFValues.Length, sizeof(float));
        SDFValueBuffer.SetData(SDFValues);
        int CurrentKernel = ComputeShader.FindKernel("LOADSDF");
        ComputeShader.SetBuffer(CurrentKernel, "SDFValues", SDFValueBuffer);
        ComputeShader.SetTexture(CurrentKernel, "SDFTexture", SDFTexture);
        ComputeShader.SetInts("SDFSize", 30, 30, 30);
        ComputeShader.Dispatch(CurrentKernel, 10, 10, 10);
    }
}