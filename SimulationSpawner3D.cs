using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEditor.ShaderGraph.Internal;
using Unity.Mathematics;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
public struct HashEntry
{
    public uint hash;
    public uint index;
}
public class SimulationSpawner3D : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public ComputeShader ComputeShader;
    public ComputeShader DensityCacher;
    public ComputeShader PositionPredicter;
    public ComputeShader HashgridCalculator;
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

    ComputeBuffer LavaBuffer;
    void Start()
    {
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
        props = new MaterialPropertyBlock();
        GameObject HolySphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh = HolySphere.GetComponent<MeshFilter>().sharedMesh;
        HolySphere.SetActive(false);

        NumOfPossibleHashes = 1024;
        HashesBufferSize = Mathf.NextPowerOfTwo(Points.Length);
        Hashes = new HashEntry[HashesBufferSize];

    }
    void Update()
    {
        ComputeLava();
    }
    private void RenderLava()
    {
        Debug.Log(Points[0].Color);
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
        Debug.Log(Points[0].Color);
        // Render in batches of 1023 (Unity limitation)
        matrices.Clear();
        colors.Clear();
        foreach (var p in Points)
        {
            matrices.Add(Matrix4x4.TRS(p.Position, Quaternion.identity, Vector3.one * 0.1f));
            colors.Add(Color.white);
        }
        Debug.Log(colors.Count);
        for (int i = 0; i < Hashes.Length - 1; i++)
        {
            Debug.Log(Hashes[i].index);
            if ((int)Hashes[i].index > 0 && (int)Hashes[i].index < colors.Count)
            {
                Color color = colourMap.Evaluate(((float)Hashes[i].hash) / NumOfPossibleHashes);
                colors[(int)Hashes[i].index] = color;
            }

        }/*
        for (int i = 0; i < StartingIndizes.Length; i++)
        {
            Debug.Log(StartingIndizes[i]);
        }*/
        for (int i = 0; i < matrices.Count; i += 1023)
        {
            int count = Mathf.Min(1023, matrices.Count - i);
            props.Clear();
            props.SetVectorArray("_Color", colors.GetRange(i, count));
            Graphics.DrawMeshInstanced(Mesh, 0, Material, matrices.GetRange(i, count), props);
        }
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
    }
    private void ComputeLava()
    {
        int PositionSize = sizeof(float) * 3;
        int ColorSize = sizeof(float) * 4;
        int VelocitySize = sizeof(float) * 3;
        int TotalSize = PositionSize + ColorSize + VelocitySize;
        float[] Densities = new float[Points.Length];
        Vector3[] PredictedPositions = new Vector3[Points.Length];

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

        ComputeBuffer StartingIndizesBuffer = new ComputeBuffer(StartingIndizes.Length, sizeof(uint));
        StartingIndizesBuffer.SetData(StartingIndizes);
        HashgridCalculator.SetBuffer(0, "Hashes", HashesBuffer);
        HashgridCalculator.SetBuffer(1, "Hashes", HashesBuffer);
        HashgridCalculator.SetBuffer(2, "Hashes", HashesBuffer);
        HashgridCalculator.SetBuffer(2, "StartingIndizes", StartingIndizesBuffer);
        HashgridCalculator.SetInt("ParticleCount", Points.Length);
        HashgridCalculator.SetInt("NumOfPossibleHashes", NumOfPossibleHashes);
        HashgridCalculator.SetBuffer(0, "Points", LavaBuffer);
        HashgridCalculator.SetFloat("SmoothingRadius", SmoothingRadius);
        HashgridCalculator.Dispatch(0, 1024, 1, 1);
        //  HashgridCalculator.Dispatch(1, 1024, 1, 1);
        //    HashgridCalculator.Dispatch(2, 1024, 1, 1);
        //StartingIndizesBuffer.GetData(StartingIndizes);
        HashesBuffer.GetData(Hashes);
        RenderLavaByHash();
        //Cachses Densities to prevent expensive recalculation over and over again
        DensityCacher.SetBuffer(0, "Points", LavaBuffer);
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
        //RenderLava();
        RenderLavaByHash();
        LavaBuffer.Dispose();
        DensityBuffer.Dispose();
        PositionBuffer.Dispose();
    }
}
