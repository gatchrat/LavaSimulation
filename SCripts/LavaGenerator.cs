using System;
using UnityEngine;

public class LavaGenerator : MonoBehaviour
{
    public LavaPoint[] Points;
    public LavaPoint[] SpawnLavaAtOnce(int Count)
    {
        Points = new LavaPoint[Count * Count * Count];
        for (int x = 0; x < Count; x++)
        {
            for (int y = 0; y < Count; y++)
            {
                for (int z = 0; z < Count; z++)
                {
                    LavaPoint Point = new LavaPoint
                    {
                        Color = Color.white,
                        active = 1,
                        age = 0f
                    };

                    Point.Position = new Vector3((-Count / 2 + x) * 0.15f, y * 0.15f + 0.5f, (-Count / 2 + z) * 0.15f);

                    Points[z + y * Count + x * Count * Count] = Point;
                }
            }
        }
        return Points;
    }
    public LavaPoint[] SpawnLavaAtOnceRandom(int Count, float BoundsWidth, float BoundsHeight, float BoundsDepth)
    {
        for (int x = 0; x < Count; x++)
        {
            for (int y = 0; y < Count; y++)
            {
                for (int z = 0; z < Count; z++)
                {
                    LavaPoint Point = new LavaPoint
                    {
                        Color = Color.white,
                        active = 1,
                        age = 0f
                    };

                    Point.Position = new Vector3(
                        UnityEngine.Random.Range(-BoundsWidth / 2, BoundsWidth / 2) * 0.1f,
                        UnityEngine.Random.Range(0, BoundsHeight) * 0.1f,
                        UnityEngine.Random.Range(-BoundsDepth / 2, BoundsDepth / 2) * 0.1f);

                    Points[z + y * Count + x * Count * Count] = Point;
                }
            }
        }
        return Points;
    }
    public LavaPoint[] InitInactive(int Count)
    {
        Points = new LavaPoint[Count];
        for (int z = 0; z < Count; z++)
        {
            LavaPoint Point = new LavaPoint
            {
                Color = Color.white,
                active = 0,
                age = 0f
            };

            Point.Position = new Vector3((-Count / 2) * 0.15f, 0, (-Count / 2 + z) * 0.15f) + new Vector3(99, 99, 99);

            Points[z] = Point;
        }
        return Points;

    }
}
