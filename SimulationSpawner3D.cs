using UnityEngine;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using UnityEngine.Experimental.Rendering;

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
public enum SpawnMode
{
    AtOnce,
    AtOnceRandom,
    Flow
}

public struct LavaPoint
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector4 Color;
    public int active;
};
public class SimulationSpawner3D : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public ComputeShader ComputeShader;
    public LavaGenerator LavaGenerator;
    ComputeBuffer argsBuffer;
    public MeshFilter LavaGeneratedMesh;
    public Gradient colourMap;
    public RenderMode Renderer;
    public SpawnMode SpawnMode;
    public Shader BilboardShader;
    public int XCount;
    public int YCount;
    public int ZCount;
    public float BoundsWidth = 14;
    public float BoundsHeight = 8;
    public float BoundsDepth = 14;
    public float TargetDensity;
    public float PressureMultiplier;
    public float NearPressureMultiplier;
    public float SmoothingRadius = 10f;
    private Mesh Mesh;
    public Material Material;
    public float Viscosity = 1f;
    public float isoLevel = 0.3f;

    [Range(0.05f, 1f)]
    public float voxelSize = 0.1f;

    [Header("SDF Settings")]
    public Vector3 SDF_scale = new Vector3(1f, 1f, 1f);
    public Vector3 SDF_Pos = new Vector3(0f, 0f, 0f);

    float[,,] densityField;
    ComputeBuffer LavaBuffer;
    Vector3[] PredictedPositions;
    private RenderTexture SDFTexture;
    private ComputeBuffer PositionBuffer;
    private ComputeBuffer DensityBuffer;
    public ComputeBuffer spatialKeys { get; private set; }
    public ComputeBuffer spatialOffsets { get; private set; }
    public ComputeBuffer sortedIndices { get; private set; }
    ComputeBuffer sortTarget_predictedPositionsBuffer;
    ComputeBuffer sortTarget_PointsBuffer;
    private LavaPoint[] Points;
    private MaterialPropertyBlock props;
    private List<Matrix4x4> matrices = new List<Matrix4x4>();
    private List<Vector4> colors = new List<Vector4>();
    private int HashesBufferSize;
    private HashEntry[] Hashes;
    private int NumOfPossibleHashes;
    private uint[] StartingIndizes;


    void Start()
    {
        LoadSDF();

        InitLava();

        PredictedPositions = new Vector3[Points.Length];
        props = new MaterialPropertyBlock();
        Mesh = GenerateQuadMesh();

        NumOfPossibleHashes = 4096;
        HashesBufferSize = Mathf.NextPowerOfTwo(Points.Length);
        Debug.Log("Hashbuffersize:" + HashesBufferSize);
        Hashes = new HashEntry[HashesBufferSize];

        InitBuffers();
        //ComputeLava(Mathf.Min(Time.deltaTime / 3f, 1f / 180f));
    }
    void Update()
    {
        //Run at atleast 60fps, slow down the simulation if framerate not reached to prevent explosion
        //Run 3 Simulation Steps per frame to improve Timestep size while not being slowed down by the render
        ComputeLava(Mathf.Min(Time.deltaTime / 3f, 1f / 180f), false);
        ComputeLava(Mathf.Min(Time.deltaTime / 3f, 1f / 180f), false);
        ComputeLava(Mathf.Min(Time.deltaTime / 3f, 1f / 180f), true);
        RenderLava();
    }

    void OnApplicationQuit()
    {
        DisposeBuffers();
    }

    private void InitLava()
    {
        switch (SpawnMode)
        {
            case SpawnMode.AtOnce:
                Points = LavaGenerator.SpawnLavaAtOnce(XCount, YCount, ZCount);
                break;
            case SpawnMode.AtOnceRandom:
                Points = LavaGenerator.SpawnLavaAtOnceRandom(XCount, YCount, ZCount, BoundsWidth, BoundsHeight, BoundsDepth);
                break;
            case SpawnMode.Flow:
                Points = LavaGenerator.InitInactive(XCount, YCount, ZCount);
                break;
        }

        densityField = new float[(int)(BoundsWidth / voxelSize), (int)(BoundsHeight / voxelSize), (int)(BoundsDepth / voxelSize)];
    }
    void InitBuffers()
    {
        int PositionSize = sizeof(float) * 3;
        int ColorSize = sizeof(float) * 4;
        int VelocitySize = sizeof(float) * 3;
        int TotalSize = PositionSize + ColorSize + VelocitySize + sizeof(int);

        Vector2[] Densities = new Vector2[Points.Length];
        DensityBuffer = new ComputeBuffer(Points.Length, sizeof(float) * 2);
        DensityBuffer.SetData(Densities);

        LavaBuffer = new ComputeBuffer(Points.Length, TotalSize);
        LavaBuffer.SetData(Points);

        sortTarget_PointsBuffer = new ComputeBuffer(Points.Length, TotalSize);
        sortTarget_predictedPositionsBuffer = new ComputeBuffer(Points.Length, sizeof(float) * 3);

        //Predict Positions 1 frame in the future, to improve reaction timing
        PositionBuffer = new ComputeBuffer(Points.Length, sizeof(float) * 3);
        PositionBuffer.SetData(PredictedPositions);

        spatialKeys = new ComputeBuffer(Points.Length, sizeof(uint));
        spatialOffsets = new ComputeBuffer(Points.Length, sizeof(uint));
        sortedIndices = new ComputeBuffer(Points.Length, sizeof(uint));
    }
    private void ComputeLava(float TimeStep, bool getData)
    {
        int CurrentKernel = 0;
        if (SpawnMode == SpawnMode.Flow)
        {
            CurrentKernel = ComputeShader.FindKernel("Activate");
            ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
            ComputeShader.SetFloat("TimePassed", TimeStep);
            ComputeShader.Dispatch(CurrentKernel, 1, 1, 1);
        }


        CurrentKernel = ComputeShader.FindKernel("PredictPositions");
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 8, 1, 1);

        ComputeShader.SetInt("ParticleCount", Points.Length);
        ComputeShader.SetFloat("SmoothingRadius", SmoothingRadius);
        CurrentKernel = ComputeShader.FindKernel("UpdateSpatialHash");
        ComputeShader.SetBuffer(CurrentKernel, "SpatialKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "SpatialOffsets", spatialOffsets);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SortedIndices", sortedIndices);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 8, 1, 1);


        CurrentKernel = ComputeShader.FindKernel("SortHashesNeu");
        ComputeShader.SetInt("numEntries", Points.Length);
        // Launch each step of the sorting algorithm (once the previous step is complete)
        // Number of steps = [log2(n) * (log2(n) + 1)] / 2
        // where n = nearest power of 2 that is greater or equal to the number of inputs
        int numStages = (int)Math.Log(Mathf.NextPowerOfTwo(Points.Length), 2); //POINTS LENGTH MUSS POW OF 2 SEIN???
        ComputeShader.SetBuffer(CurrentKernel, "SpatialKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "SortedIndices", sortedIndices);
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
                ComputeShader.Dispatch(CurrentKernel, Points.Length / 2, 1, 1);
            }
        }
        CurrentKernel = ComputeShader.FindKernel("InitializeOffsets");
        ComputeShader.SetBuffer(CurrentKernel, "SortedKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "Offsets", spatialOffsets);
        ComputeShader.SetInt("numInputs", Points.Length);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 8, 1, 1);

        CurrentKernel = ComputeShader.FindKernel("CalculateOffsets");
        ComputeShader.SetBuffer(CurrentKernel, "SortedKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "Offsets", spatialOffsets);
        ComputeShader.SetInt("numInputs", Points.Length);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 8, 1, 1);



        // Reorder kernel
        CurrentKernel = ComputeShader.FindKernel("Reorder");
        ComputeShader.SetBuffer(CurrentKernel, "SortTarget_Points", sortTarget_PointsBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SortTarget_PredictedPositions", sortTarget_predictedPositionsBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SortedIndices", sortedIndices);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 8, 1, 1);

        // Reorder copyback kernel
        CurrentKernel = ComputeShader.FindKernel("ReorderCopyBack");
        ComputeShader.SetBuffer(CurrentKernel, "SortTarget_Points", sortTarget_PointsBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SortTarget_PredictedPositions", sortTarget_predictedPositionsBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SortedIndices", sortedIndices);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 8, 1, 1);

        CurrentKernel = ComputeShader.FindKernel("DensityCache");
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "CachedDensities", DensityBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SpatialKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "SpatialOffsets", spatialOffsets);
        ComputeShader.SetFloat("TimePassed", TimeStep);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 8, 1, 1);

        //Calculate Forces and Movement
        CurrentKernel = ComputeShader.FindKernel("Simulation");
        ComputeShader.SetTexture(CurrentKernel, "SDFReadTexture", SDFTexture);
        ComputeShader.SetBuffer(CurrentKernel, "CachedDensities", DensityBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetFloat("BoundsHeight", BoundsHeight);
        ComputeShader.SetFloat("BoundsDepth", BoundsDepth);
        ComputeShader.SetFloat("BoundsWidth", BoundsWidth);
        ComputeShader.SetFloat("ViscosityMultiplier", Viscosity);
        ComputeShader.SetFloat("TargetDensity", TargetDensity);
        ComputeShader.SetFloat("PressureMultiplier", PressureMultiplier);
        ComputeShader.SetFloat("NearPressureMultiplier", NearPressureMultiplier);
        ComputeShader.SetFloats("SDF_OffSet", SDF_Pos.x, SDF_Pos.y, SDF_Pos.z);
        ComputeShader.SetFloats("SDF_Scale", SDF_scale.x, SDF_scale.y, SDF_scale.z);
        ComputeShader.SetBuffer(CurrentKernel, "SpatialKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "SpatialOffsets", spatialOffsets);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 8, 1, 1);

        if (getData)
        {
            LavaBuffer.GetData(Points);
            PositionBuffer.GetData(PredictedPositions);
        }
    }
    private void RenderLava()
    {
        //Render Results
        if (Renderer == RenderMode.AsMesh)
        {
            RenderLavaAsMesh();
        }
        else if (Renderer == RenderMode.ByVelocity || Renderer == RenderMode.ByDensity)
        {
            RenderLavaNormal();
        }
        else if (Renderer == RenderMode.ByHash)
        {
            RenderLavaByHash();
        }
    }
    private void DisposeBuffers()
    {
        //Free Up Memory
        LavaBuffer.Dispose();
        DensityBuffer.Dispose();
        PositionBuffer.Dispose();
        SDFTexture.Release();
        spatialKeys.Dispose();
        spatialOffsets.Dispose();
        sortedIndices.Dispose();
        argsBuffer.Release();
        sortTarget_PointsBuffer.Dispose();
        sortTarget_predictedPositionsBuffer.Dispose();
        //      HashesBuffer.Dispose();
        // StartingIndizesBuffer.Dispose();
    }
    //----------------------------------RENDERER------------------------------------------
    private void RenderLavaNormal()
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
        Mesh mesh = GenerateQuadMesh();
        argsBuffer = new ComputeBuffer(10, 4);
        CreateArgsBuffer(ref argsBuffer, mesh, Points.Length);


        Material mat = new Material(BilboardShader);
        Texture2D gradientTexture = Texture2D.blackTexture;
        TextureFromGradient(ref gradientTexture, 10, colourMap);
        mat.SetTexture("ColourMap", gradientTexture);


        mat.SetFloat("scale", 1 * 0.05f);
        mat.SetFloat("velocityMax", 10);
        mat.SetBuffer("Points", LavaBuffer);

        Vector3 s = transform.localScale;
        transform.localScale = Vector3.one;
        var localToWorld = transform.localToWorldMatrix;
        transform.localScale = s;

        mat.SetMatrix("localToWorld", localToWorld);
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuffer);
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
        // ComputeShader.SetBuffer(CurrentKernel, "Hashes", HashesBuffer);
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
        SDFTexture = new RenderTexture(30, 30, 0)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = 30,
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat,
            useMipMap = false
        };
        SDFTexture.Create();

        float[] SDFValues = new float[30 * 30 * 30];
        TextAsset mytxtData = Resources.Load<TextAsset>("Volcano_SDF30");
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
    //SOURCE: https://github.com/SebLague/Fluid-Planet/blob/main/Assets/Scripts/Rendering/MeshHelpers/QuadGenerator.cs------------------------
    public static Mesh GenerateQuadMesh()
    {
        int[] indices = new int[] { 0, 1, 2, 2, 1, 3 };

        Vector3[] vertices = new Vector3[]
        {
                new (-0.5f, 0.5f),
                new (0.5f, 0.5f),
                new (-0.5f, -0.5f),
                new (0.5f, -0.5f)
        };
        Vector2[] uvs = new Vector2[]
        {
                new (0, 1),
                new (1, 1),
                new (0, 0),
                new (1, 0)
        };

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0, true);
        mesh.SetUVs(0, uvs);
        return mesh;
    }
    public static void TextureFromGradient(ref Texture2D texture, int width, Gradient gradient, FilterMode filterMode = FilterMode.Bilinear)
    {
        if (texture == null)
        {
            texture = new Texture2D(width, 1);
        }
        else if (texture.width != width)
        {
            texture.Reinitialize(width, 1);
        }

        if (gradient == null)
        {
            gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new(Color.black, 0), new(Color.black, 1) },
                new GradientAlphaKey[] { new(1, 0), new(1, 1) }
            );
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = filterMode;

        Color[] cols = new Color[width];
        for (int i = 0; i < cols.Length; i++)
        {
            float t = i / (cols.Length - 1f);
            cols[i] = gradient.Evaluate(t);
        }

        texture.SetPixels(cols);
        texture.Apply();
    }
    public static void CreateArgsBuffer(ref ComputeBuffer argsBuffer, Mesh mesh, int numInstances)
    {
        const int stride = sizeof(uint);
        const int numArgs = 5;
        const int subMeshIndex = 0;
        uint[] argsBufferArray = new uint[5];

        bool createNewBuffer = argsBuffer == null || !argsBuffer.IsValid() || argsBuffer.count != argsBufferArray.Length || argsBuffer.stride != stride;
        if (createNewBuffer)
        {
            argsBuffer.Dispose();
            argsBuffer = new ComputeBuffer(numArgs, stride, ComputeBufferType.IndirectArguments);
        }

        lock (argsBufferArray)
        {
            argsBufferArray[0] = (uint)mesh.GetIndexCount(subMeshIndex);
            argsBufferArray[1] = (uint)numInstances;
            argsBufferArray[2] = (uint)mesh.GetIndexStart(subMeshIndex);
            argsBufferArray[3] = (uint)mesh.GetBaseVertex(subMeshIndex);
            argsBufferArray[4] = 0; // offset

            argsBuffer.SetData(argsBufferArray);
        }
    }
    ///////////////////////////////
}