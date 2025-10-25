using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System;
public class PerformanceMeasurement : MonoBehaviour
{
    public SimulationSpawner3D refScript;
    private readonly List<int> FPSHistory = new();
    private readonly List<int> ParticleHistory = new();
    private int maxFPS;
    private int maxParticles;
    public UILineRenderer FPSLR;
    public TextMeshProUGUI FPSText;
    public UILineRenderer ParticleLR;
    public TextMeshProUGUI ParticleText;
    private float TimeSinceTick = 0f;
    private int tracker = 0;

    // Update is called once per frame
    void Update()
    {
        TimeSinceTick += Time.deltaTime;
        //  UpdateFPS();
        //  UpdateParticles();
        LogValues();
    }
    private void LogValues()
    {
        int curParticle = refScript.ParticleActivated;
        if (tracker == 0 && curParticle > 100)
        {
            tracker++;
            Debug.Log((int)(1.0f / Time.deltaTime) + " 100");
        }
        if (tracker == 1 && curParticle > 1000)
        {
            tracker++;
            Debug.Log((int)(1.0f / Time.deltaTime) + " 1000");
        }
        if (tracker == 2 && curParticle > 10000)
        {
            tracker++;
            Debug.Log((int)(1.0f / Time.deltaTime) + " 10000");
        }
        if (tracker == 3 && curParticle > 20000)
        {
            tracker++;
            Debug.Log((int)(1.0f / Time.deltaTime) + " 20000");
        }
        if (tracker == 4 && curParticle > 30000)
        {
            tracker++;
            Debug.Log((int)(1.0f / Time.deltaTime) + " 30000");
        }
        if (tracker == 5 && curParticle > 40000)
        {
            tracker++;
            Debug.Log((int)(1.0f / Time.deltaTime) + " 40000");
        }
        if (tracker == 6 && curParticle > 50000)
        {
            tracker++;
            Debug.Log((int)(1.0f / Time.deltaTime) + " 50000");
        }
    }
    private void UpdateFPS()
    {
        if (TimeSinceTick < 0.06f)
        {
            return;
        }
        int curFPS = (int)(1.0f / Time.deltaTime);
        FPSHistory.Add(curFPS);
        if (FPSHistory.Count > 1000)
        {
            FPSHistory.RemoveAt(0);
        }
        if (curFPS > maxFPS)
        {
            maxFPS = curFPS;
        }
        List<Vector2> points = new();
        float maxX = 2560;
        float maxY = 200;
        float width = maxX / (FPSHistory.Count - 1);
        float curX = 0;
        for (int i = 0; i < FPSHistory.Count; i++)
        {
            points.Add(new Vector2(curX, -maxY + (float)FPSHistory[i] / (float)maxFPS * maxY));
            curX += width;
        }
        FPSLR.points = points;
        FPSLR.SetAllDirty();
        FPSText.text = curFPS.ToString() + "FPS";
        FPSText.transform.gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector3(0, FPSLR.points[FPSLR.points.Count - 1].y, 0);
    }
    private void UpdateParticles()
    {
        if (TimeSinceTick < 0.06f)
        {
            return;
        }
        TimeSinceTick = 0;
        int curParticle = refScript.ParticleActivated;
        ParticleHistory.Add(curParticle);
        if (curParticle > maxParticles)
        {
            maxParticles = curParticle;
        }
        if (ParticleHistory.Count > 1000)
        {
            ParticleHistory.RemoveAt(0);
        }
        List<Vector2> points = new();
        float maxX = 2560;
        float maxY = 200;
        float width = maxX / (ParticleHistory.Count - 1);
        float curX = 0;
        for (int i = 0; i < ParticleHistory.Count; i++)
        {
            points.Add(new Vector2(curX, -maxY + (float)ParticleHistory[i] / (float)maxParticles * maxY));
            curX += width;
        }
        ParticleLR.points = points;
        ParticleLR.SetAllDirty();
        ParticleText.text = curParticle.ToString() + " Particles";
        ParticleText.transform.gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector3(-50, ParticleLR.points[ParticleLR.points.Count - 1].y, 0);
    }
}
