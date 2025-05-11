using UnityEngine;

public class LavaGenerator : MonoBehaviour
{
    public LavaPoint[] Points;
    public LavaPoint[] SpawnLavaAtOnce(int XCount, int YCount, int ZCount)
    {
        Points = new LavaPoint[XCount * YCount * ZCount];
        for (int x = 0; x < XCount; x++)
        {
            for (int y = 0; y < YCount; y++)
            {
                for (int z = 0; z < ZCount; z++)
                {
                    LavaPoint Point = new LavaPoint
                    {
                        Color = Color.white,
                        active = 1
                    };

                    Point.Position = new Vector3((-XCount / 2 + x) * 0.15f, y * 0.15f + 0.5f, (-ZCount / 2 + z) * 0.15f);

                    Points[z + y * ZCount + x * ZCount * YCount] = Point;
                }
            }
        }
        return Points;
    }
    public LavaPoint[] SpawnLavaAtOnceRandom(int XCount, int YCount, int ZCount, float BoundsWidth, float BoundsHeight, float BoundsDepth)
    {
        for (int x = 0; x < XCount; x++)
        {
            for (int y = 0; y < YCount; y++)
            {
                for (int z = 0; z < ZCount; z++)
                {
                    LavaPoint Point = new LavaPoint
                    {
                        Color = Color.white,
                        active = 1
                    };

                    Point.Position = new Vector3(
                        UnityEngine.Random.Range(-BoundsWidth / 2, BoundsWidth / 2) * 0.1f,
                        UnityEngine.Random.Range(0, BoundsHeight) * 0.1f,
                        UnityEngine.Random.Range(-BoundsDepth / 2, BoundsDepth / 2) * 0.1f);

                    Points[z + y * ZCount + x * ZCount * YCount] = Point;
                }
            }
        }
        return Points;
    }
    public LavaPoint[] InitInactive(int XCount, int YCount, int ZCount)
    {

        Points = new LavaPoint[XCount * YCount * ZCount];
        for (int x = 0; x < XCount; x++)
        {
            for (int y = 0; y < YCount; y++)
            {
                for (int z = 0; z < ZCount; z++)
                {
                    LavaPoint Point = new LavaPoint
                    {
                        Color = Color.white,
                        active = 0
                    };

                    Point.Position = new Vector3(99, 99, 99);

                    Points[z + y * ZCount + x * ZCount * YCount] = Point;
                }
            }
        }
        return Points;

    }
}
