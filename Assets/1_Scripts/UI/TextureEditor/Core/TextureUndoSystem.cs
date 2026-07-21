using System.Collections.Generic;
using UnityEngine;

namespace VacuumProtocol.UI.TextureEditor
{
    /// <summary>
    /// Description: Manages pixel buffer snapshot history stacks for Undo and Redo operations.
    /// Context: Used by TexturePainter to record canvas states after drawing strokes.
    /// Justification: Provides fast, memory-friendly snapshot restoration without texture allocation overhead.
    /// </summary>
    public class TextureUndoSystem
    {
        private readonly Stack<Color32[]> _undoStack = new Stack<Color32[]>();
        private readonly Stack<Color32[]> _redoStack = new Stack<Color32[]>();
        private readonly int _maxHistorySteps;

        /// <summary>
        /// Description: Returns true if undo steps are available.
        /// Context: UI button interactable evaluation.
        /// Justification: Used to enable/disable Undo button in UI.
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Description: Returns true if redo steps are available.
        /// Context: UI button interactable evaluation.
        /// Justification: Used to enable/disable Redo button in UI.
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Description: Constructor initializing maximum allowed history steps.
        /// Context: Instantiated by TexturePainter.
        /// Justification: Restricts memory footprint by discarding oldest steps when limit is reached.
        /// </summary>
        /// <param name="maxHistorySteps">Maximum number of undo states saved (default 20).</param>
        public TextureUndoSystem(int maxHistorySteps = 20)
        {
            _maxHistorySteps = maxHistorySteps;
        }

        /// <summary>
        /// Description: Pushes a copy of the current pixel state onto the undo stack.
        /// Context: Called at the start of a mouse stroke down event.
        /// Justification: Saves state so stroke can be reverted. Clears redo stack on new action.
        /// </summary>
        /// <param name="pixels">Current pixel buffer to copy.</param>
        public void PushState(Color32[] pixels)
        {
            if (pixels == null)
            {
                return;
            }

            // Create a deep copy of the pixel array
            Color32[] snapshot = new Color32[pixels.Length];
            System.Array.Copy(pixels, snapshot, pixels.Length);

            _undoStack.Push(snapshot);
            _redoStack.Clear();

            // Enforce max step limit by removing oldest bottom item if needed
            if (_undoStack.Count > _maxHistorySteps)
            {
                // Convert stack to list, remove bottom, rebuild stack
                List<Color32[]> list = new List<Color32[]>(_undoStack);
                list.RemoveAt(list.Count - 1);
                _undoStack.Clear();
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    _undoStack.Push(list[i]);
                }
            }
        }

        /// <summary>
        /// Description: Reverts to the previous pixel state and saves current state to redo stack.
        /// Context: Triggered by UI Undo button.
        /// Justification: Restores previous snapshot to canvas.
        /// </summary>
        /// <param name="currentPixels">Current canvas pixel array before undo.</param>
        /// <returns>Previous pixel array, or null if undo stack is empty.</returns>
        public Color32[] PerformUndo(Color32[] currentPixels)
        {
            if (!CanUndo)
            {
                return null;
            }

            // Push current state to redo stack
            Color32[] redoSnapshot = new Color32[currentPixels.Length];
            System.Array.Copy(currentPixels, redoSnapshot, currentPixels.Length);
            _redoStack.Push(redoSnapshot);

            // Pop previous state from undo stack
            return _undoStack.Pop();
        }

        /// <summary>
        /// Description: Re-applies the next pixel state and saves current state to undo stack.
        /// Context: Triggered by UI Redo button.
        /// Justification: Restores undone state back to canvas.
        /// </summary>
        /// <param name="currentPixels">Current canvas pixel array before redo.</param>
        /// <returns>Next pixel array, or null if redo stack is empty.</returns>
        public Color32[] PerformRedo(Color32[] currentPixels)
        {
            if (!CanRedo)
            {
                return null;
            }

            // Push current state to undo stack
            Color32[] undoSnapshot = new Color32[currentPixels.Length];
            System.Array.Copy(currentPixels, undoSnapshot, currentPixels.Length);
            _undoStack.Push(undoSnapshot);

            // Pop next state from redo stack
            return _redoStack.Pop();
        }

        /// <summary>
        /// Description: Clears all saved snapshots in undo and redo stacks.
        /// Context: Reset or texture resize initialization.
        /// Justification: Prevents state mismatch when canvas dimensions change.
        /// </summary>
        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
