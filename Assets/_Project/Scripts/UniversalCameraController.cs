/*
 * UniversalCameraController.cs
 * ---------------------------------------------------------------
 * Universal camera control using Unity Input System for cross-platform support
 * Works on desktop (mouse), mobile (touch), and web (both)
 */

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Collections.Generic;

public class UniversalCameraController : MonoBehaviour
{
    public enum PanMode { Free, ZAxisOnly, None }

    [System.Serializable]
    public class PanSettings
    {
        public PanMode panMode = PanMode.Free;
        public float panSpeed = 1.2f;
        [Range(0f, 10f)] public float friction = 5f;
        public Vector2 clampPadding = new Vector2(5f, 5f);
        public bool returnToOrigin = false; // For Play mode
        public float returnDelay = 1f;
    }

    [Header("Target References")]
    [SerializeField] private Transform cameraProxy;

    [Header("Mode Configurations")]
    [SerializeField] private PanSettings editorSettings = new PanSettings 
    { 
        panMode = PanMode.Free, 
        panSpeed = 2f, 
        friction = 8f, 
        clampPadding = new Vector2(5f, 5f) 
    };
    
    [SerializeField] private PanSettings playerSettings = new PanSettings 
    { 
        panMode = PanMode.Free, 
        panSpeed = 1.5f, 
        friction = 3f, 
        clampPadding = new Vector2(3f, 3f),
        returnToOrigin = true,
        returnDelay = 1f
    };
    
    [SerializeField] private PanSettings endlessSettings = new PanSettings 
    { 
        panMode = PanMode.ZAxisOnly, 
        panSpeed = 1f, 
        friction = 5f, 
        clampPadding = new Vector2(0f, 10f) // Only Z padding for endless
    };

    [Header("Input Detection")]
    [SerializeField] private float dragThreshold = 15f;

    // Public properties
    public bool IsPlayerControllingCamera { get; private set; }
    
    // Private field for endless camera offset
    private Vector3 endlessCameraOffset;

    // Private state
    private PanSettings currentSettings;
    private Vector3 velocity;
    private bool isDragging = false;
    private bool isDragClaimedByUI = false;
    private Vector2 inputStartPosition;
    private Vector2 lastInputPosition;
    private GameObject tappedObject;
    
    // Mode-specific state
    private Vector3 playModeStartPosition;

    /// <summary>
    /// The proxy position that frames the board, supplied by BoardFramingDriver. Null until
    /// framing has run.
    ///
    /// SwitchMode used to zero the proxy and THEN capture playModeStartPosition from it, so Play
    /// mode's rubberband returned to WORLD ORIGIN instead of the framed pose. Holding the value
    /// here rather than pushing it into SwitchMode makes the ORDER irrelevant: whenever
    /// SwitchMode runs, it uses the latest framed value if one exists.
    ///
    /// returnToOrigin / returnDelay / friction are untouched - only the TARGET changes.
    /// </summary>
    private Vector3? framedRestingPosition;

    /// <summary>Called by BoardFramingDriver once the board is framed.</summary>
    public void SetFramedRestingPosition(Vector3 restingProxyPosition)
    {
        framedRestingPosition = restingProxyPosition;
        playModeStartPosition = restingProxyPosition;
        if (cameraProxy != null) cameraProxy.position = restingProxyPosition;
    }
    private Vector3 endlessBaseTarget;
    private float lastPlayerInputTime;
    
    // Cross-platform input state
    private bool inputActive = false;
    private bool usingTouch = false;
    
    // Clamping
    private Vector3 minClamp;
    private Vector3 maxClamp;
    
    // References
    private GridManager gridManager;
    private Camera mainCamera;
    private GameManager gameManager;
    private EndlessModeManager endlessManager;
    private BoatController playerBoat;

    void Start()
    {
        // Enable enhanced touch support for mobile
        EnhancedTouchSupport.Enable();
        
        mainCamera = Camera.main;
        gridManager = FindFirstObjectByType<GridManager>();
        gameManager = FindFirstObjectByType<GameManager>();
        endlessManager = FindFirstObjectByType<EndlessModeManager>();
        
        // Find camera proxy if not assigned
        if (cameraProxy == null)
        {
            if (endlessManager != null)
            {
                // Look for camera proxy in endless manager
                Transform[] children = endlessManager.GetComponentsInChildren<Transform>();
                foreach (var child in children)
                {
                    if (child.name.ToLower().Contains("cameraproxy"))
                    {
                        cameraProxy = child;
                        break;
                    }
                }
            }
            
            // Fallback to main camera
            if (cameraProxy == null)
            {
                cameraProxy = mainCamera.transform;
            }
        }
        
        if (gridManager == null || gameManager == null) 
        {
            enabled = false;
            return;
        }
        
        SwitchMode(gameManager.currentMode);
    }

    void OnDestroy()
    {
        // Disable enhanced touch when destroyed
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        // Check for mode changes
        if (currentSettings != GetSettingsForMode(gameManager.currentMode)) 
        {
            SwitchMode(gameManager.currentMode);
        }
        
        HandleInput();
        ApplyMovement();
    }

    private void HandleInput()
    {
        // Universal input handling for mouse and touch
        bool inputPressed = GetInputPressed();
        Vector2 inputPosition = GetInputPosition();
        
        if (inputPressed && !inputActive)
        {
            StartInput(inputPosition);
        }
        else if (inputPressed && inputActive)
        {
            UpdateInput(inputPosition);
        }
        else if (!inputPressed && inputActive)
        {
            EndInput(inputPosition);
        }
    }

    private bool GetInputPressed()
    {
        // Check for touch input first (prioritize on mobile devices)
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            usingTouch = true;
            return true;
        }
        
        // Check for mouse input (desktop/web)
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            usingTouch = false;
            return true;
        }
        
        return false;
    }
    
    private Vector2 GetInputPosition()
    {
        if (usingTouch && Touchscreen.current != null)
        {
            return Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (!usingTouch && Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
        
        return Vector2.zero;
    }
    
    private Vector2 GetInputDelta()
    {
        if (usingTouch && Touchscreen.current != null)
        {
            return Touchscreen.current.primaryTouch.delta.ReadValue();
        }
        else if (!usingTouch && Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue();
        }
        
        return Vector2.zero;
    }

    private void StartInput(Vector2 inputPosition)
    {
        inputActive = true;
        isDragging = false;
        isDragClaimedByUI = false;
        tappedObject = null;
        velocity = Vector3.zero;
        lastPlayerInputTime = Time.time;
        
        inputStartPosition = inputPosition;
        lastInputPosition = inputPosition;

        // Check for UI elements that should claim the drag
        // Create pointer event data with correct pointer ID for touch vs mouse
        PointerEventData eventData = new PointerEventData(EventSystem.current) 
        { 
            position = inputPosition,
            pointerId = usingTouch ? 0 : -1
        };
        
        // Perform raycast to check what's under the input
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        // Check if any result is a draggable hand tile
        foreach (var result in results)
        {
            if (result.gameObject.GetComponentInParent<PlayableHandTile>() != null)
            {
                isDragClaimedByUI = true;
                return; // Exit immediately - don't check for other objects
            }
        }
        
        // Also check with the standard UI detection as backup
        bool overUI = false;
        if (usingTouch)
        {
            overUI = EventSystem.current.IsPointerOverGameObject(0); // Primary touch
        }
        else
        {
            overUI = EventSystem.current.IsPointerOverGameObject(); // Mouse
        }
        
        // If we're over any UI element, don't proceed with camera or object detection
        if (overUI)
        {
            isDragClaimedByUI = true;
            return;
        }
        
        // Check for clickable objects in the 3D world
        Ray ray = mainCamera.ScreenPointToRay(inputPosition);
        if (Physics.Raycast(ray, out RaycastHit hit)) 
        {
            if (hit.collider.GetComponentInParent<IPointerClickHandler>() != null) 
            {
                tappedObject = hit.collider.gameObject;
            }
        }
    }

    private void UpdateInput(Vector2 inputPosition)
    {
        if (isDragClaimedByUI || currentSettings.panMode == PanMode.None) 
            return;

        // Check if we should start dragging
        if (!isDragging && Vector2.Distance(inputPosition, inputStartPosition) > dragThreshold)
        {
            isDragging = true;
            tappedObject = null; // Cancel any tap
            lastPlayerInputTime = Time.time;
        }

        if (isDragging)
        {
            Vector2 inputDelta = GetInputDelta();
            ApplyPanDelta(inputDelta);
        }
        
        lastInputPosition = inputPosition;
    }

    private void EndInput(Vector2 inputPosition)
    {
        // Handle tap if we didn't drag
        if (!isDragging && tappedObject != null)
        {
            var pointerEventData = new PointerEventData(EventSystem.current) 
            { 
                position = inputPosition,
                pointerId = usingTouch ? 0 : -1,
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(tappedObject, pointerEventData, ExecuteEvents.pointerClickHandler);
        }
        
        inputActive = false;
        isDragging = false;
    }

    private void ApplyPanDelta(Vector2 inputDelta)
    {
        float panMultiplier = currentSettings.panSpeed * 0.01f;
        
        float moveX = 0f;
        float moveZ = -inputDelta.y * panMultiplier;
        
        // Apply X movement based on pan mode
        if (currentSettings.panMode == PanMode.Free)
        {
            moveX = -inputDelta.x * panMultiplier;
        }
        
        Vector3 displacement = new Vector3(moveX, 0, moveZ);
        
        // Apply movement based on mode
        if (gameManager.currentMode == OperatingMode.Endless)
        {
            ApplyEndlessPan(displacement);
        }
        else
        {
            // Direct movement for Editor/Play modes
            cameraProxy.position = ApplyClamping(cameraProxy.position + displacement);
            velocity = displacement / Time.deltaTime;
        }
    }

    private void ApplyEndlessPan(Vector3 displacement)
    {
        // In endless mode, we modify the offset that gets applied to the boat-following logic
        endlessCameraOffset += displacement;
        
        // Clamp the offset to reasonable bounds
        if (playerBoat != null)
        {
            float maxOffset = currentSettings.clampPadding.y;
            endlessCameraOffset.z = Mathf.Clamp(endlessCameraOffset.z, -maxOffset, maxOffset);
        }
        
        velocity = displacement / Time.deltaTime;
    }

    private void ApplyMovement()
    {
        IsPlayerControllingCamera = isDragging || velocity.magnitude > 0.01f;

        if (gameManager.currentMode == OperatingMode.Endless)
        {
            ApplyEndlessMovement();
        }
        else
        {
            ApplyStandardMovement();
        }
    }

    private void ApplyStandardMovement()
    {
        if (IsPlayerControllingCamera)
        {
            if (!isDragging) // Apply inertia
            {
                cameraProxy.position = ApplyClamping(cameraProxy.position + velocity * Time.deltaTime);
                velocity = Vector3.Lerp(velocity, Vector3.zero, currentSettings.friction * Time.deltaTime);
            }
        }
        else // Player is idle
        {
            if (currentSettings.returnToOrigin && gameManager.currentMode == OperatingMode.Playing)
            {
                // Return to start position after delay
                if (Time.time - lastPlayerInputTime > currentSettings.returnDelay)
                {
                    cameraProxy.position = Vector3.Lerp(
                        cameraProxy.position, 
                        playModeStartPosition, 
                        Time.deltaTime * currentSettings.friction
                    );
                }
            }
        }
    }

    private void ApplyEndlessMovement()
    {
        if (!isDragging && velocity.magnitude > 0.01f)
        {
            // Apply inertia to the offset
            endlessCameraOffset += velocity * Time.deltaTime;
            velocity = Vector3.Lerp(velocity, Vector3.zero, currentSettings.friction * Time.deltaTime);
            
            // Clamp offset
            if (playerBoat != null)
            {
                float maxOffset = currentSettings.clampPadding.y;
                endlessCameraOffset.z = Mathf.Clamp(endlessCameraOffset.z, -maxOffset, maxOffset);
            }
        }
        
        // The actual camera movement happens in EndlessModeManager.LateUpdate()
        // using our GetEndlessCameraOffset() method
    }

    private void SwitchMode(OperatingMode mode)
    {
        currentSettings = GetSettingsForMode(mode);
        CalculateClampingBounds();
        velocity = Vector3.zero;

        // Reset camera proxy to origin when switching to Editor or Play mode
        if (mode != OperatingMode.Endless && cameraProxy != null)
        {
            // The FRAMED resting position, not the world origin - see framedRestingPosition.
            cameraProxy.position = framedRestingPosition ?? Vector3.zero;
        }

        if (mode == OperatingMode.Playing)
        {
            playModeStartPosition = cameraProxy.position;
        }
        else if (mode == OperatingMode.Endless)
        {
            endlessCameraOffset = Vector3.zero;
            playerBoat = FindFirstObjectByType<BoatController>();
        }
        
        lastPlayerInputTime = Time.time;
    }
    
    private PanSettings GetSettingsForMode(OperatingMode mode)
    {
        switch (mode) 
        {
            case OperatingMode.Editor: return editorSettings;
            case OperatingMode.Playing: return playerSettings;
            case OperatingMode.Endless: return endlessSettings;
            default: return editorSettings;
        }
    }

    private void CalculateClampingBounds()
    {
        if (gridManager == null || gridManager.cols == 0 || gridManager.rows == 0) 
            return;
            
        Vector3 gridBottomLeft = gridManager.GetWorldPosition(0, 0);
        Vector3 gridTopRight = gridManager.GetWorldPosition(gridManager.cols - 1, gridManager.rows - 1);
        
        minClamp = new Vector3(
            gridBottomLeft.x - currentSettings.clampPadding.x, 
            0, 
            gridBottomLeft.z - currentSettings.clampPadding.y
        );
        maxClamp = new Vector3(
            gridTopRight.x + currentSettings.clampPadding.x, 
            0, 
            gridTopRight.z + currentSettings.clampPadding.y
        );
    }
    
    private Vector3 ApplyClamping(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, minClamp.x, maxClamp.x);
        position.z = Mathf.Clamp(position.z, minClamp.z, maxClamp.z);
        return position;
    }

    // Public methods for EndlessModeManager integration
    public Vector3 GetEndlessCameraOffset()
    {
        return endlessCameraOffset;
    }
    
    public void ResetEndlessOffset()
    {
        endlessCameraOffset = Vector3.zero;
        velocity = Vector3.zero;
    }

    public void SetEndlessBaseTarget(Vector3 target)
    {
        endlessBaseTarget = target;
    }

    // Public method for other managers to call when grid changes
    public void OnGridChanged()
    {
        CalculateClampingBounds();
    }
}