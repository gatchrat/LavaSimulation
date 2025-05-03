using UnityEngine;

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
        this.transform.position = new Vector3(refScript.SDF_XPos, refScript.SDF_YPos, refScript.SDF_ZPos);
    }
}
