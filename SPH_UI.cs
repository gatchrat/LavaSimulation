using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SPH_UI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public SimulationSpawner3D Simulation;
    public LavaGenerator Spawner;
    public GameObject MainCam;
    public TMP_InputField ParticleCountText;
    public TMP_InputField MaxAge;
    public TMP_InputField TempExchange;
    public TMP_Text ParticlesPerSecondText;
    public Slider ParticlesPerSecond;
    public TMP_Text DensityText;
    public Slider Density;
    public TMP_Text PressureText;
    public Slider Pressure;
    public TMP_Text NearPressureText;
    public Slider NearPressure;
    public TMP_Text SmoothingRadiusText;
    public Slider SmoothingRadius;
    public TMP_Text ViscosityText;
    public Slider Viscosity;
    public TMP_Text ParticleScaleText;
    public Slider ParticleScale;
    public TMP_Text VoxelText;
    public Slider VoxelSize;
    public TMP_InputField SpawnPosX;
    public TMP_InputField SpawnPosY;
    public TMP_InputField SpawnPosZ;
    private Boolean FreeCam = false;
    private Vector3 camPos;
    private Quaternion camRot;
    void Start()
    {
        ParticleCountText.text = Simulation.ParticleCount.ToString();
        MaxAge.text = Simulation.MaxAge.ToString();
        if (SceneManager.GetActiveScene() == SceneManager.GetSceneByName("SPH"))
        {
            MaxAge.text = "30";
        }
        SetMaxAge();
        TempExchange.text = Simulation.TemperatureExchangeSpeedModifier.ToString();
        ParticlesPerSecond.value = Simulation.ParticlePerSecond;
        SetParticlePerSecond();
        VoxelSize.value = Simulation.voxelSize;
        setVoxelSize();
        Density.value = Simulation.TargetDensity;
        SetDensity();
        Pressure.value = Simulation.PressureMultiplier;
        SetPressure();
        NearPressure.value = Simulation.NearPressureMultiplier;
        SetNearPressure();
        Viscosity.value = Simulation.Viscosity;
        SetViscosity();
        SmoothingRadius.value = Simulation.SmoothingRadius;
        SetSmoothingRadius();
        ParticleScale.value = Simulation.RenderScale;
        SetParticleRenderScale();
        SpawnPosX.text = Spawner.gameObject.transform.position.x.ToString();
        SpawnPosY.text = Spawner.gameObject.transform.position.y.ToString();
        SpawnPosZ.text = Spawner.gameObject.transform.position.z.ToString();
        MainCam.GetComponent<FreeCam>().enabled = FreeCam;
        camPos = MainCam.transform.position;
        camRot = MainCam.transform.rotation;
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
        Simulation.ParticlePerSecond = ParticlesPerSecond.value;
        ParticlesPerSecondText.text = "Amount/s " + ParticlesPerSecond.value;
    }
    public void SetDensity()
    {
        Simulation.TargetDensity = Density.value;
        DensityText.text = "Density " + Density.value;
    }
    public void SetPressure()
    {

        Simulation.PressureMultiplier = Pressure.value;
        PressureText.text = "Pressure " + Pressure.value;
    }
    public void SetNearPressure()
    {
        Simulation.NearPressureMultiplier = NearPressure.value;
        NearPressureText.text = "Near Pressure " + NearPressure.value;
    }
    public void SetSmoothingRadius()
    {
        Simulation.SmoothingRadius = SmoothingRadius.value;
        SmoothingRadiusText.text = "Smoothing Radius " + SmoothingRadius.value;
    }
    public void SetViscosity()
    {
        Simulation.Viscosity = Viscosity.value;
        ViscosityText.text = "Viscosity " + Viscosity.value;
    }
    public void SetParticleRenderScale()
    {
        Simulation.RenderScale = ParticleScale.value;
        ParticleScaleText.text = "Render Size " + ParticleScale.value;
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
        Debug.Log("Restart");
        int i;
        bool isNum = int.TryParse(ParticleCountText.text, out i);
        if (isNum)
        {
            Simulation.Restart(i);
        }
        else
        {
            Simulation.Restart(65536);
        }

    }
    public void ToggleFreeCam()
    {
        FreeCam = !FreeCam;
        MainCam.GetComponent<FreeCam>().enabled = FreeCam;
        if (!FreeCam)
        {
            MainCam.transform.position = camPos;
            MainCam.transform.rotation = camRot;
        }
    }
    public void TogglePause()
    {
        Simulation.Paused = !Simulation.Paused;
    }

    public void ResetValues()
    {
        Simulation.DisposeBuffers();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void setVoxelSize()
    {
        Simulation.SetVoxelSize(VoxelSize.value / 10);
        VoxelText.text = "Voxel Size " + VoxelSize.value;
    }
    public void ToggleSmoothing()
    {
        Simulation.Smoothed = !Simulation.Smoothed;
    }
    public void ToggleOldRenderer()
    {
        if (Simulation.RenderMode == RenderMode.CubeMarching)
        {
            Simulation.RenderMode = RenderMode.Particle;
        }
        else
        {
            Simulation.RenderMode = RenderMode.CubeMarching;
        }
    }
}
