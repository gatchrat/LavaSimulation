using UnityEngine;
using System;
using UnityEngine.Experimental.Rendering;

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
    public float age;
};
public class SimulationSpawner3D : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public ComputeShader ComputeShader;
    public String SDFFileName;
    public LavaGenerator LavaGenerator;
    ComputeBuffer argsBuffer;
    public Gradient colourMap;
    public SpawnMode SpawnMode;
    public Shader BilboardShader;
    public int ParticleCount;
    public float MaxAge;
    public float TemperatureExchangeSpeedModifier = 0f;
    public float ParticlePerSecond = 180;
    public float BoundsWidth = 14;
    public float BoundsHeight = 8;
    public float BoundsDepth = 14;
    public float TargetDensity;
    public float PressureMultiplier;
    public float NearPressureMultiplier;
    public float SmoothingRadius = 10f;
    private Mesh Mesh;
    public float Viscosity = 1f;

    [Header("SDF Settings")]
    public Vector3 SDF_scale = new Vector3(1f, 1f, 1f);
    public Vector3 SDF_Pos = new Vector3(0f, 0f, 0f);
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
    private int SDFValueCount;
    private float SDFSize;
    private bool Restarting = false;
    Mesh mesh;
    private float TimePassedOverall = 0f;
    private int ParticleActivated = 0;
    public Boolean Paused = false;

    void Start()
    {
        InitStuff();
    }
    void Update()
    {
        //Run at atleast 60fps, slow down the simulation if framerate not reached to prevent explosion
        //Run 3 Simulation Steps per frame to improve Timestep size while not being slowed down by the render
        if (!Paused)
        {
            ComputeLava(Mathf.Min(Time.deltaTime / 3f, 1f / 180f));
            ComputeLava(Mathf.Min(Time.deltaTime / 3f, 1f / 180f));
            ComputeLava(Mathf.Min(Time.deltaTime / 3f, 1f / 180f));
        }
        RenderLava();
    }

    void OnApplicationQuit()
    {
        DisposeBuffers();
    }
    void InitStuff()
    {
        LoadSDF();
        InitLava();
        PredictedPositions = new Vector3[Points.Length];
        InitBuffers();

    }
    public void Restart(int Count)
    {
        Restarting = true;
        ParticleCount = Count;
    }
    private void Restart()
    {
        DisposeBuffers();
        InitStuff();
        TimePassedOverall = 0f;
        ParticleActivated = 0;
    }

    private void InitLava()
    {
        switch (SpawnMode)
        {
            case SpawnMode.AtOnce:
                Points = LavaGenerator.SpawnLavaAtOnce(ParticleCount);
                break;
            case SpawnMode.AtOnceRandom:
                Points = LavaGenerator.SpawnLavaAtOnceRandom(ParticleCount, BoundsWidth, BoundsHeight, BoundsDepth);
                break;
            case SpawnMode.Flow:
                Points = LavaGenerator.InitInactive(ParticleCount);
                break;
        }
    }
    void InitBuffers()
    {
        int PositionSize = sizeof(float) * 3;
        int ColorSize = sizeof(float) * 4;
        int VelocitySize = sizeof(float) * 3;
        int AktiveSize = sizeof(int);
        int AgeSize = sizeof(float);
        int TotalSize = PositionSize + ColorSize + VelocitySize + AktiveSize + AgeSize;

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

        //AssignBuffers
        int CurrentKernel;
        if (SpawnMode == SpawnMode.Flow)
        {
            CurrentKernel = ComputeShader.FindKernel("Activate");
            ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        }
        CurrentKernel = ComputeShader.FindKernel("PredictPositions");
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);

        CurrentKernel = ComputeShader.FindKernel("UpdateSpatialHash");
        ComputeShader.SetBuffer(CurrentKernel, "SpatialKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "SpatialOffsets", spatialOffsets);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SortedIndices", sortedIndices);

        CurrentKernel = ComputeShader.FindKernel("SortHashesNeu");
        ComputeShader.SetBuffer(CurrentKernel, "SpatialKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "SortedIndices", sortedIndices);

        CurrentKernel = ComputeShader.FindKernel("CalculateOffsets");
        ComputeShader.SetBuffer(CurrentKernel, "SortedKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "Offsets", spatialOffsets);

        CurrentKernel = ComputeShader.FindKernel("Reorder");
        ComputeShader.SetBuffer(CurrentKernel, "SortTarget_Points", sortTarget_PointsBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SortTarget_PredictedPositions", sortTarget_predictedPositionsBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SortedIndices", sortedIndices);

        CurrentKernel = ComputeShader.FindKernel("ReorderCopyBack");
        ComputeShader.SetBuffer(CurrentKernel, "SortTarget_Points", sortTarget_PointsBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SortTarget_PredictedPositions", sortTarget_predictedPositionsBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SortedIndices", sortedIndices);

        CurrentKernel = ComputeShader.FindKernel("DensityCache");
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "CachedDensities", DensityBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SpatialKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "SpatialOffsets", spatialOffsets);

        CurrentKernel = ComputeShader.FindKernel("Simulation");
        ComputeShader.SetTexture(CurrentKernel, "SDFReadTexture", SDFTexture);
        ComputeShader.SetBuffer(CurrentKernel, "CachedDensities", DensityBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "PredictedPosition", PositionBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "Points", LavaBuffer);
        ComputeShader.SetBuffer(CurrentKernel, "SpatialKeys", spatialKeys);
        ComputeShader.SetBuffer(CurrentKernel, "SpatialOffsets", spatialOffsets);

        mesh = GenerateQuadMesh();
        CreateArgsBuffer(mesh, Points.Length);
    }
    private void ComputeLava(float TimeStep)
    {
        int CurrentKernel;
        if (SpawnMode == SpawnMode.Flow)
        {
            CurrentKernel = ComputeShader.FindKernel("Activate");
            TimePassedOverall += TimeStep;
            int ParticleToActivate = (int)((TimePassedOverall * ParticlePerSecond) - ParticleActivated);
            ComputeShader.SetInt("ActiveParticles", ParticleActivated);
            ParticleActivated += ParticleToActivate;
            ComputeShader.SetFloat("TimePassed", TimeStep);
            ComputeShader.SetInt("ParticleToActivate", ParticleToActivate);
            ComputeShader.Dispatch(CurrentKernel, 1, 1, 1);
        }
        else
        {
            ParticleActivated = Points.Length;
        }

        CurrentKernel = ComputeShader.FindKernel("PredictPositions");
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 256, 1, 1);

        ComputeShader.SetInt("ParticleCount", Points.Length);
        ComputeShader.SetFloat("SmoothingRadius", SmoothingRadius);

        CurrentKernel = ComputeShader.FindKernel("UpdateSpatialHash");
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 256, 1, 1);
        //SpatialKeys[id.x] = key;

        CurrentKernel = ComputeShader.FindKernel("SortHashesNeu");
        ComputeShader.SetInt("numEntries", Math.Min(ParticleActivated, Points.Length));
        //Sorts the hash values, but also sorts the index array, so we keep track of witch point has which hash
        // Launch each step of the sorting algorithm (once the previous step is complete)
        // Number of steps = [log2(n) * (log2(n) + 1)] / 2
        // where n = nearest power of 2 that is greater or equal to the number of inputs
        int numStages = (int)Math.Log(Mathf.NextPowerOfTwo(Math.Min(ParticleActivated, Points.Length)), 2);
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
                ComputeShader.Dispatch(CurrentKernel, Math.Min(ParticleActivated, Points.Length) / 2, 1, 1);
            }
        }
        // Saves for each occuring hash value, where in the array the hash starts, end is found by walking each time
        //Offsets[key] = index
        CurrentKernel = ComputeShader.FindKernel("CalculateOffsets");
        ComputeShader.SetInt("numInputs", Math.Min(ParticleActivated, Points.Length));
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 256, 1, 1);

        // Generates a sorted array of points by going over the hash indexes
        CurrentKernel = ComputeShader.FindKernel("Reorder");
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 256, 1, 1);

        // Overwrite the point array with the ordered one
        CurrentKernel = ComputeShader.FindKernel("ReorderCopyBack");
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 256, 1, 1);

        CurrentKernel = ComputeShader.FindKernel("DensityCache");
        ComputeShader.SetFloat("TimePassed", TimeStep);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 256, 1, 1);

        //Actual Simulation Step
        CurrentKernel = ComputeShader.FindKernel("Simulation");
        ComputeShader.SetFloat("BoundsHeight", BoundsHeight);
        ComputeShader.SetFloat("MaxAge", MaxAge);
        ComputeShader.SetFloat("BoundsDepth", BoundsDepth);
        ComputeShader.SetFloat("BoundsWidth", BoundsWidth);
        ComputeShader.SetFloat("ViscosityMultiplier", Viscosity);
        ComputeShader.SetFloat("TargetDensity", TargetDensity);
        ComputeShader.SetFloat("PressureMultiplier", PressureMultiplier);
        ComputeShader.SetFloat("NearPressureMultiplier", NearPressureMultiplier);
        ComputeShader.SetFloats("SDF_OffSet", SDF_Pos.x, SDF_Pos.y, SDF_Pos.z);
        ComputeShader.SetFloats("SDF_Scale", SDF_scale.x, SDF_scale.y, SDF_scale.z);
        ComputeShader.SetInts("SDFValueCount", SDFValueCount, SDFValueCount, SDFValueCount);
        ComputeShader.SetFloats("SDFSize", SDFSize, SDFSize, SDFSize);
        ComputeShader.SetFloat("TemperatureExchangeSpeedModifier", TemperatureExchangeSpeedModifier);
        Vector3 Pos = LavaGenerator.gameObject.transform.position;
        ComputeShader.SetFloats("Spawnpoint", Pos.x, Pos.y, Pos.z);
        ComputeShader.Dispatch(CurrentKernel, Points.Length / 256, 1, 1);
    }
    private void DisposeBuffers()
    {
        //Free Up Memory, otherwise free memory leaks :)
        LavaBuffer.Dispose();
        DensityBuffer.Dispose();
        PositionBuffer.Dispose();
        SDFTexture.Release();
        spatialKeys.Dispose();
        spatialOffsets.Dispose();
        sortedIndices.Dispose();
        argsBuffer.Dispose();
        sortTarget_PointsBuffer.Dispose();
        sortTarget_predictedPositionsBuffer.Dispose();
    }
    //----------------------------------RENDERER------------------------------------------
    private void RenderLava()
    {
        //Restart at the end of the update loop since we dont know when the restart is triggered and the buffers may be in active use
        if (Restarting)
        {
            Restarting = false;
            Restart();
            return;
        }
        LavaBuffer.GetData(Points);
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10000);


        Material mat = new Material(BilboardShader);
        Texture2D gradientTexture = Texture2D.blackTexture;
        TextureFromGradient(ref gradientTexture, 10, colourMap);
        mat.SetTexture("ColourMap", gradientTexture);


        mat.SetFloat("scale", 1 * 0.1f);
        mat.SetFloat("velocityMax", 3);
        mat.SetBuffer("Points", LavaBuffer);

        Vector3 s = transform.localScale;
        transform.localScale = Vector3.one;
        var localToWorld = transform.localToWorldMatrix;
        transform.localScale = s;

        mat.SetMatrix("localToWorld", localToWorld);
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuffer);
    }
    private void LoadSDF()
    {
        TextAsset mytxtData = Resources.Load<TextAsset>(SDFFileName);
        string txt = mytxtData.text;
        string[] Values = txt.Split(' ');
        SDFValueCount = int.Parse(Values[0]);
        Debug.Log(SDFValueCount);
        SDFSize = float.Parse(Values[3]);

        SDFTexture = new RenderTexture(SDFValueCount, SDFValueCount, 0)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = SDFValueCount,
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat,
            useMipMap = false
        };
        SDFTexture.Create();

        float[] SDFValues = new float[SDFValueCount * SDFValueCount * SDFValueCount];

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
        ComputeShader.SetInts("SDFValueCount", SDFValueCount, SDFValueCount, SDFValueCount);
        ComputeShader.SetFloats("SDFSize", SDFSize, SDFSize, SDFSize);
        ComputeShader.Dispatch(CurrentKernel, SDFValueCount / 8, SDFValueCount / 8, SDFValueCount / 8);
        SDFValueBuffer.Dispose();
        Debug.Log("SDF Loaded");
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
    public void CreateArgsBuffer(Mesh mesh, int numInstances)
    {
        const int stride = sizeof(uint);
        const int numArgs = 5;
        const int subMeshIndex = 0;
        uint[] argsBufferArray = new uint[5];
        argsBuffer = new ComputeBuffer(numArgs, stride, ComputeBufferType.IndirectArguments);


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