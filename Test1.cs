using System.Data.Common;
using UnityEngine;
using System.Collections.Generic;

struct LavaPoint
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector4 Color;
};
public class Test1 : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public ComputeShader ComputeShader;
    public ComputeShader DensityCacher;
    public int Count;
    public float SmoothingRadius = 10f;
    private Mesh Mesh;
    public Material Material;
    private LavaPoint[] Points;
    private GameObject[] Objects;
    private GameObject[] SmoothRadio;
    private MaterialPropertyBlock props;
    private List<Matrix4x4> matrices = new List<Matrix4x4>();
    private List<Vector4> colors = new List<Vector4>();

    ComputeBuffer LavaBuffer;
    void Start()
    {
        Points = new LavaPoint[Count * Count];
        Objects = new GameObject[Count * Count];
        SmoothRadio = new GameObject[Count * Count];
        for (int x = 0; x < Count; x++)
        {
            for (int y = 0; y < Count; y++)
            {
                InitLava(x, y);
            }
        }
        props = new MaterialPropertyBlock();
        Mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<MeshFilter>().sharedMesh;
    }
    void Update()
    {
        ComputeLava();
    }
    private void RenderLava()
    {
        Debug.Log(Points[0].Velocity);
        // Render in batches of 1023 (Unity limitation)
        matrices.Clear();
        colors.Clear();
        foreach (var p in Points)
        {
            matrices.Add(Matrix4x4.TRS(p.Position, Quaternion.identity, Vector3.one * 1));
            colors.Add(p.Color);
        }
        for (int i = 0; i < matrices.Count; i += 1023)
        {
            int count = Mathf.Min(1023, matrices.Count - i);
            props.Clear();
            props.SetVectorArray("_Color", colors.GetRange(i, count));
            Graphics.DrawMeshInstanced(Mesh, 0, Material, matrices.GetRange(i, count), props);
        }
    }

    private void InitLava(int x, int y)
    {

        LavaPoint Point = new LavaPoint
        {
            Position = new Vector3(Random.Range(-70, 70), Random.Range(0, 80), 0),
            Color = Color.white
        };
        Points[x * Count + y] = Point;
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

        //Cachses Densities to prevent expensive recalculation over and over again
        DensityCacher.SetBuffer(0, "Points", LavaBuffer);
        DensityCacher.SetBuffer(0, "CachedDensities", DensityBuffer);
        DensityCacher.SetFloat("TimePassed", Time.deltaTime);
        DensityCacher.SetFloat("SmoothingRadius", SmoothingRadius);
        DensityCacher.Dispatch(0, Points.Length / 10, 1, 1);

        ComputeShader.SetBuffer(0, "CachedDensities", DensityBuffer);
        ComputeShader.SetBuffer(0, "Points", LavaBuffer);
        ComputeShader.SetFloat("TimePassed", Time.deltaTime);
        ComputeShader.SetFloat("SmoothingRadius", SmoothingRadius);
        ComputeShader.Dispatch(0, Points.Length / 10, 1, 1);

        LavaBuffer.GetData(Points);
        RenderLava();
        LavaBuffer.Dispose();
        DensityBuffer.Dispose();
    }
}
