// DrawSnapGizmos.cs
// Shows a little cube so you can see snap-point positions in Scene view.
// Now includes a public Color, so you can change it per object in the Inspector.

using UnityEngine;

#if UNITY_EDITOR           // Ensures the script runs only in the Editor
[ExecuteAlways]            // Updates even when not in Play Mode
public class DrawSnapGizmos : MonoBehaviour
{
    [Header("Gizmo Settings")]
    [Tooltip("Colour shown in the Scene view")]
    public Color gizmoColor = Color.yellow;   // Editable in Inspector

    [Tooltip("Size of the cube gizmo (world units)")]
    [Range(0.02f, 0.3f)]
    public float gizmoSize = 0.1f;            // Also editable

    private void OnDrawGizmos()
    {
        // Use whatever colour the user picked
        Gizmos.color = gizmoColor;

        // Draw a cube at this object's position
        Gizmos.DrawCube(transform.position, Vector3.one * gizmoSize);
    }
}
#endif