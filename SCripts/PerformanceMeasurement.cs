using UnityEngine;
using System.Collections.Generic;
using TMPro;
public class PerformanceMeasurement : MonoBehaviour
{
    public SimulationSpawner3D refScript;
    private readonly List<int> FPSHistory = new();
    private readonly List<int> ParticleHistory = new();
    private int maxFPS;
    public UILineRenderer FPSLR;
    public TextMeshProUGUI FPSText;
    public UILineRenderer ParticleLR;
    public TextMeshProUGUI ParticleText;
    private float TimeSinceTick = 0f;

    // Update is called once per frame
    void Update()
    {
        TimeSinceTick += Time.deltaTime;
        UpdateFPS();
        UpdateParticles();
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
        float maxX = 1920;
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
        if (ParticleHistory.Count > 1000)
        {
            ParticleHistory.RemoveAt(0);
        }
        List<Vector2> points = new();
        float maxX = 1920;
        float maxY = 200;
        float width = maxX / (ParticleHistory.Count - 1);
        float curX = 0;
        for (int i = 0; i < ParticleHistory.Count; i++)
        {
            points.Add(new Vector2(curX, -maxY + (float)ParticleHistory[i] / (float)curParticle * maxY));
            curX += width;
        }
        ParticleLR.points = points;
        ParticleLR.SetAllDirty();
        ParticleText.text = curParticle.ToString() + " Particles";
        ParticleText.transform.gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector3(-50, ParticleLR.points[ParticleLR.points.Count - 1].y, 0);
    }
}
