using UnityEngine;
using Unity.Cinemachine; // CORRECT NAMESPACE for Cinemachine 3.x

public class CameraManager : MonoBehaviour
{
    // Singleton for easy access
    public static CameraManager Instance { get; private set; }

    [SerializeField] private CinemachineCamera editorCamera; // CORRECT CLASS NAME
    [SerializeField] private CinemachineCamera playerCamera; // CORRECT CLASS NAME

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

    public void SwitchToEditorView()
    {
        if (editorCamera == null || playerCamera == null) return;
        
        // Give the editor camera higher priority
        editorCamera.Priority = 20;
        playerCamera.Priority = 10;
    }

    public void SwitchToPlayerView()
    {
        if (editorCamera == null || playerCamera == null) return;
        
        // Give the player camera higher priority
        playerCamera.Priority = 20;
        editorCamera.Priority = 10;
    }
}