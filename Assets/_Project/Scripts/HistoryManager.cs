/*
 *  HistoryManager.cs
 *  ---------------------------------------------------------------
 *  Manages the Undo/Redo history of the game.
 *  - Uses a Stack to store snapshots of the game state.
 *  - Provides methods to save the current state and undo the last state.
 */

using System.Collections.Generic;
using UnityEngine;

public class HistoryManager : MonoBehaviour
{
    // Singleton pattern for easy access from any script.
    public static HistoryManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private LevelEditorManager editorManager;

    [Header("Settings")]
    [Tooltip("The maximum number of undo steps to store.")]
    [SerializeField] private int maxHistorySteps = 20;

    [Header("UI")]
    [SerializeField] private UnityEngine.UI.Button undoButton;

    // A Stack is the perfect data structure for Undo (Last-In, First-Out).
    private Stack<GameStateSnapshot> historyStack = new Stack<GameStateSnapshot>();

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
        // Add this listener
        if (undoButton != null)
        {
            undoButton.onClick.AddListener(UndoLastState);
        }
        UpdateUndoButton();
    }




    /// <summary>
    /// Clears the entire undo history. Should be called when a new level is loaded.
    /// </summary>
    public void ClearHistory()
    {
        historyStack.Clear();
        UpdateUndoButton();
        Debug.Log("[HistoryManager] Undo history cleared.");
    }

    /// <summary>
    /// Takes a snapshot of the current game state and pushes it onto the undo stack.
    /// </summary>
public void SaveState(GameStateSnapshot snapshotToSave = null)
{
    if (!IsReady)
    {
        Debug.LogWarning("[HistoryManager] Not ready to save state (editorManager not assigned). Skipping save.");
        return;
    }

    GameStateSnapshot snapshot = snapshotToSave ?? editorManager.CreateCurrentStateSnapshot();
    
    historyStack.Push(snapshot);
    UpdateUndoButton(); // Ensure button state is updated after saving

    Debug.Log($"[HistoryManager] State saved. History now contains {historyStack.Count} steps.");

    // Optional: Enforce a maximum number of undo steps to save memory.
    if (historyStack.Count > maxHistorySteps)
    {
        // For now, we'll just let it grow, but a List (instead of Stack) would be better for this.
        // We can optimize this if performance becomes an issue.
    }
}

    /// <summary>
    /// Restores the game to the most recently saved state.
    /// </summary>
    public void UndoLastState()
    {
        if (historyStack.Count > 1)
        {
            Debug.Log("[HistoryManager] Undoing last action.");

            // Pop the CURRENT state off, which we are throwing away
            historyStack.Pop();
            // Peek at the PREVIOUS state, which we are restoring
            GameStateSnapshot previousState = historyStack.Peek();

            editorManager.StartCoroutine("ReconstructLevelFromDataCoroutine", previousState);
            UpdateUndoButton(); // Update button state after undoing
        }
        else
        {
            Debug.LogWarning("[HistoryManager] Cannot undo: No history available.");
        }
    }
    


public void UpdateUndoButton()
{
    if (undoButton != null)
    {
        // The button is interactable if there is a state to go back to.
        undoButton.interactable = historyStack.Count > 1;
    }
}










}