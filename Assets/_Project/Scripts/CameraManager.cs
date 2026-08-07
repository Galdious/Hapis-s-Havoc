using UnityEngine;
using Unity.Cinemachine; // CORRECT NAMESPACE for Cinemachine 3.x

public class CameraManager : MonoBehaviour
{
    // Singleton for easy access
    public static CameraManager Instance { get; private set; }

    [SerializeField] private CinemachineCamera editorCamera; // CORRECT CLASS NAME
    [SerializeField] private CinemachineCamera playerCamera; // CORRECT CLASS NAME
    [SerializeField] private CinemachineCamera endlessCamera;


    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    /// <summary>
    /// The vCam a mode renders through. Exposed because BoardFramingDriver must set the LENS on
    /// the camera that will actually render, and picking by Priority is unreliable: the driver
    /// can run before SwitchTo*View has assigned priorities, and would then size the wrong vCam.
    /// Selecting by mode is order-independent.
    /// </summary>
    public CinemachineCamera CameraFor(OperatingMode mode)
    {
        switch (mode)
        {
            case OperatingMode.Endless: return endlessCamera;
            case OperatingMode.Playing: return playerCamera;
            default:                    return editorCamera;
        }
    }

    public void SwitchToEditorView()
    {
        if (editorCamera == null || playerCamera == null) return;

        // Give the editor camera higher priority
        editorCamera.Priority = 20;
        playerCamera.Priority = 10;
        endlessCamera.Priority = 10;
    }

    public void SwitchToPlayerView()
    {
        if (editorCamera == null || playerCamera == null) return;

        // Give the player camera higher priority
        playerCamera.Priority = 20;
        editorCamera.Priority = 10;
        endlessCamera.Priority = 10;
    }
    public void SwitchToEndlessView()
    {
        if (endlessCamera == null)
        {
            Debug.LogError("[CameraManager] Cannot switch to Endless View, the camera reference is missing!");
            return;
        }

        // Give the endless camera the highest priority to make it active.
        endlessCamera.Priority = 20;
        playerCamera.Priority = 10;
        editorCamera.Priority = 10;
    }
    

}