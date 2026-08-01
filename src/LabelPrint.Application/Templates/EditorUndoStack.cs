namespace LabelPrint.Application.Templates;

/// <summary>
/// Undo/redo stack for template editor state snapshots (JSON strings).
/// </summary>
public sealed class EditorUndoStack
{
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>Records the state before a mutation; clears the redo branch.</summary>
    public void Push(string snapshotBeforeChange)
    {
        _undo.Push(snapshotBeforeChange);
        _redo.Clear();
    }

    /// <summary>Pops the previous snapshot to restore; pushes current onto redo.</summary>
    public string? Undo(string currentSnapshot)
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        _redo.Push(currentSnapshot);
        return _undo.Pop();
    }

    /// <summary>Re-applies a snapshot from redo; pushes current onto undo.</summary>
    public string? Redo(string currentSnapshot)
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        _undo.Push(currentSnapshot);
        return _redo.Pop();
    }
}
