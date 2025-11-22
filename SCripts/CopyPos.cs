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
        this.transform.position = new Vector3(refScript.SDF_Pos.x + 3.09f, refScript.SDF_Pos.y + 4.67f, refScript.SDF_Pos.z + 2.41f);
        this.transform.localScale = new Vector3(refScript.SDF_scale.x, refScript.SDF_scale.y, refScript.SDF_scale.z);
    }
}
