using UnityEngine;

public class Gizmo : MonoBehaviour
{
    // Gizmo color
    public Color gizmoColor = Color.green;
    public SimulationSpawner3D refScript;

    private void OnDrawGizmos()
    {
        // Set Gizmo color
        Gizmos.color = gizmoColor;

        // Draw a cube at the object's position with its local scale
        Gizmos.DrawWireCube(new Vector3(0, refScript.BoundsHeight / 2, 0), new Vector3(refScript.BoundsWidth, refScript.BoundsHeight, refScript.BoundsDepth));
    }
}