/* HandTileAnimationSettings.cs */
using UnityEngine;

// This line allows you to create instances of this class as assets in your project.
[CreateAssetMenu(fileName = "HandTileAnimationSettings", menuName = "Hapi/Hand Tile Animation Settings")]
public class HandTileAnimationSettings : ScriptableObject
{
    [Header("Rotation Animation")]
    [Tooltip("How long the rotation animation takes in seconds.")]
    public float rotationDuration = 0.25f;
    
    [Tooltip("The curve of the rotation animation for easing.")]
    public AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}