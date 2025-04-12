using System.Data.Common;
using UnityEngine;

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
    public int Count;
    public float SmoothingRadius = 5f;
    public Mesh Mesh;
    public Material Material;
    private LavaPoint[] Points;
    private GameObject[] Objects;
    void Start()
    {
        Points = new LavaPoint[Count * Count];
        Objects = new GameObject[Count * Count];
        for (int x = 0; x < Count; x++)
        {
            for (int y = 0; y < Count; y++)
            {
                InitLava(x, y);
            }
        }
    }
    void Update()
    {
        ComputeLava();
    }
    private void RenderLava()
    {
        for (int i = 0; i < Objects.Length; i++)
        {
            Objects[i].transform.position = Points[i].Position;
            Color Color = Points[i].Color;
            Objects[i].GetComponent<MeshRenderer>().material.SetColor("_BaseColor", Color);
        }
    }

    private void InitLava(int x, int y)
    {
        GameObject Cube = new GameObject("Cube " + x * Count + y, typeof(MeshFilter), typeof(MeshRenderer));
        Cube.GetComponent<MeshFilter>().mesh = Mesh;
        Cube.GetComponent<MeshRenderer>().material = new Material(Material);
        Cube.transform.position = new Vector3(Random.Range(-70, 70), Random.Range(0, 80), 0);

        Color Color = Random.ColorHSV();
        Cube.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", Color);

        Objects[x * Count + y] = Cube;

        LavaPoint Point = new LavaPoint
        {
            Position = Cube.transform.position,
            Color = Color
        };
        Points[x * Count + y] = Point;
    }
    private void ComputeLava()
    {
        int PositionSize = sizeof(float) * 3;
        int ColorSize = sizeof(float) * 4;
        int VelocitySize = sizeof(float) * 3;
        int TotalSize = PositionSize + ColorSize + VelocitySize;

        ComputeBuffer LavaBuffer = new ComputeBuffer(Points.Length, TotalSize);
        LavaBuffer.SetData(Points);

        ComputeShader.SetBuffer(0, "Points", LavaBuffer);
        ComputeShader.SetFloat("TimePassed", Time.deltaTime);
        ComputeShader.SetFloat("SmoothingRadius", SmoothingRadius);
        ComputeShader.Dispatch(0, Points.Length / 10, 1, 1);

        LavaBuffer.GetData(Points);
        RenderLava();
        LavaBuffer.Dispose();
    }
}
