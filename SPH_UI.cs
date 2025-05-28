using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SPH_UI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public SimulationSpawner3D Simulation;
    public LavaGenerator Spawner;
    public TMP_InputField ParticleCountText;
    public TMP_InputField MaxAge;
    public TMP_InputField TempExchange;
    public TMP_InputField ParticlesPerSecond;
    public TMP_InputField Density;
    public TMP_InputField Pressure;
    public TMP_InputField NearPressure;
    public TMP_InputField SmoothingRadius;
    public TMP_InputField Viscosity;
    public TMP_InputField SpawnPosX;
    public TMP_InputField SpawnPosY;
    public TMP_InputField SpawnPosZ;
    void Start()
    {
        ParticleCountText.text = Simulation.ParticlePerSecond.ToString();
        MaxAge.text = Simulation.MaxAge.ToString();
        TempExchange.text = Simulation.TemperatureExchangeSpeedModifier.ToString();
        ParticlesPerSecond.text = Simulation.ParticlePerSecond.ToString();
        Density.text = Simulation.TargetDensity.ToString();
        Pressure.text = Simulation.PressureMultiplier.ToString();
        NearPressure.text = Simulation.NearPressureMultiplier.ToString();
        SmoothingRadius.text = Simulation.SmoothingRadius.ToString();
        Viscosity.text = Simulation.Viscosity.ToString();
        SpawnPosX.text = Spawner.gameObject.transform.position.x.ToString();
        SpawnPosY.text = Spawner.gameObject.transform.position.y.ToString();
        SpawnPosZ.text = Spawner.gameObject.transform.position.z.ToString();
    }
    public void SetMaxAge()
    {
        float i = 0;
        bool isNum = float.TryParse(MaxAge.text, out i);
        if (isNum)
        {
            Simulation.MaxAge = i;
        }
    }
    public void SetTempExchange()
    {
        float i = 0;
        bool isNum = float.TryParse(TempExchange.text, out i);
        if (isNum)
        {
            Simulation.TemperatureExchangeSpeedModifier = i;
        }
    }
    public void SetParticlePerSecond()
    {
        int i = 0;
        bool isNum = int.TryParse(ParticlesPerSecond.text, out i);
        if (isNum)
        {
            Simulation.ParticlePerSecond = i;
        }
    }
    public void SetDensity()
    {
        float i = 0;
        bool isNum = float.TryParse(Density.text, out i);
        if (isNum)
        {
            Simulation.TargetDensity = i;
        }
    }
    public void SetPressure()
    {
        float i = 0;
        bool isNum = float.TryParse(Pressure.text, out i);
        if (isNum)
        {
            Simulation.PressureMultiplier = i;
        }
    }
    public void SetNearPressure()
    {
        float i = 0;
        bool isNum = float.TryParse(NearPressure.text, out i);
        if (isNum)
        {
            Simulation.NearPressureMultiplier = i;
        }
    }
    public void SetSmoothingRadius()
    {
        float i = 0;
        bool isNum = float.TryParse(SmoothingRadius.text, out i);
        if (isNum)
        {
            Simulation.SmoothingRadius = i;
        }
    }
    public void SetViscosity()
    {
        float i = 0;
        bool isNum = float.TryParse(Viscosity.text, out i);
        if (isNum)
        {
            Simulation.Viscosity = i;
        }
    }
    public void SetSpawnerPosition()
    {
        float x = 0;
        bool isNumX = float.TryParse(SpawnPosX.text, out x);
        float y = 0;
        bool isNumY = float.TryParse(SpawnPosY.text, out y);
        float z = 0;
        bool isNumZ = float.TryParse(SpawnPosZ.text, out z);

        if (isNumX && isNumY && isNumZ)
        {
            Spawner.transform.position = new Vector3(x, y, z);
        }
    }
    public void Reload()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }
}
