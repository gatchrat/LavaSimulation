using UnityEngine;
[ExecuteAlways]
public class CopyPos : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public SimulationSpawner3D refScript;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        this.transform.position = new Vector3(refScript.SDF_Pos.x, refScript.SDF_Pos.y, refScript.SDF_Pos.z);
        this.transform.localScale = new Vector3(refScript.SDF_scale.x * 0.33334f, refScript.SDF_scale.y * 0.33334f, refScript.SDF_scale.z * 0.33334f);
    }
}
