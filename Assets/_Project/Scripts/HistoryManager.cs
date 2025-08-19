/*
 *  HistoryManager.cs
 *  ---------------------------------------------------------------
 *  Manages the Undo/Redo history of the game.
 *  - Uses a Stack to store snapshots of the game state.
 *  - Provides methods to save the current state and undo the last state.
 */
using System.Collections;   
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HistoryManager : MonoBehaviour
{
    // Singleton pattern for easy access from any script.
    public static HistoryManager Instance { get; private set; }
    public bool hasUsedUndo { get; private set; } = false;

    [Header("Scene References")]
    [SerializeField] private LevelEditorManager editorManager;

    [Header("Settings")]
    [Tooltip("The maximum number of undo steps to store.")]
    [SerializeField] private int maxHistorySteps = 20;



    // A Stack is the perfect data structure for Undo (Last-In, First-Out).
    private Stack<GameStateSnapshot> historyStack = new Stack<GameStateSnapshot>();

    private bool isUndoing = false;

    public bool IsReady => editorManager != null;

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


    private void Start()
    {
        UIManager.Instance.UpdateAllUndoButtons();
    }





    /// Clears the entire undo history. Should be called when a new level is loaded.

    public void ClearHistory()
    {
        historyStack.Clear();
        UIManager.Instance.UpdateAllUndoButtons();
        Debug.Log("[HistoryManager] Undo history cleared.");
    }


    /// Takes a snapshot of the current game state and pushes it onto the undo stack.

public void SaveState(GameStateSnapshot snapshotToSave = null)
{
            if (isUndoing)
        {
            // If we are in the middle of an undo operation, do not save a new state.
            // This prevents the restored state from being immediately pushed back onto the stack.
            return;
        }

    if (!IsReady)
        {
            Debug.LogWarning("[HistoryManager] Not ready to save state (editorManager not assigned). Skipping save.");
            return;
        }

    GameStateSnapshot snapshot = snapshotToSave ?? editorManager.CreateCurrentStateSnapshot();
    
    if (historyStack.Count > 0)
    {
        // Get the state currently at the top of the stack.
        GameStateSnapshot lastState = historyStack.Peek();
        
        // Convert both the last state and the new state to JSON strings to compare them.
        string lastStateJson = JsonUtility.ToJson(lastState);
        string newStateJson = JsonUtility.ToJson(snapshot);

        // If the new state is identical to the last one, don't save it.
        if (lastStateJson == newStateJson)
        {
            Debug.Log("[HistoryManager] State is identical to the previous one. Skipping save.");
            return;
        }
    }


    historyStack.Push(snapshot);
    UIManager.Instance.UpdateAllUndoButtons();

    Debug.Log($"[HistoryManager] State saved. History now contains {historyStack.Count} steps.");
}

    public void Undo()
    {
        // This is a simple wrapper to start the coroutine from the UI button.
        if (!isUndoing) // Prevent clicking Undo multiple times while one is in progress
        {
            StartCoroutine(UndoLastStateCoroutine());
        }
    }


    private IEnumerator UndoLastStateCoroutine()
    {
        yield return ScreenFader.Instance.FadeOut();

        if (historyStack.Count > 1)
        {
            hasUsedUndo = true; 
            isUndoing = true; // Set the flag to true
            Debug.Log("[HistoryManager] Undoing last action.");

            // Pop the CURRENT state off, which we are throwing away
            historyStack.Pop();
            // Peek at the PREVIOUS state, which we are restoring
            GameStateSnapshot previousState = historyStack.Peek();

            LevelInfo currentLevelInfo = GameManager.Instance.currentLevelInfo;


            // We now wait for the reconstruction to fully complete.
            // This calls the now-public method in LevelEditorManager.
            yield return StartCoroutine(editorManager.ReconstructLevelFromDataCoroutine(
                editorManager.currentLoadedLevelData,
                currentLevelInfo, // <<< Pass the info
                previousState,
                true
            ));



            UIManager.Instance.UpdateAllUndoButtons(); // Update button state after undoing
        }
        else
        {
            Debug.LogWarning("[HistoryManager] Cannot undo: No history available.");
        }

        // IMPORTANT: Clear the flag after the entire operation is complete.
        isUndoing = false;
        

        yield return ScreenFader.Instance.FadeIn();
    }




    public void UpdateUndoButton(Button editorButton, Button playerButton)
    {
        bool isInteractable = historyStack.Count > 1;
        if (editorButton != null)
        {
            editorButton.interactable = isInteractable;
        }
        if (playerButton != null)
        {
            playerButton.interactable = isInteractable;
        }
    }


    public void ResetUndoFlag()
    {
        hasUsedUndo = false;
        Debug.Log("[HistoryManager] Undo flag has been reset for the new level.");
    }









}