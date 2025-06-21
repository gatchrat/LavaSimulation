using UnityEngine;
using UnityEngine.InputSystem;

public class HideCanvas : MonoBehaviour
{
    public Canvas UI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            UI.enabled = !UI.enabled;
        }
    }
}
