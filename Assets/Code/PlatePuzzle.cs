using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Combines several PressurePlates into one puzzle.
///  - AllPressed: every plate must be pressed at the same time (e.g. two crates on two plates).
///  - Sequence:   plates must be pressed in the exact order of the list (path stepping).
///                A wrong step resets the whole sequence.
/// For Sequence puzzles, set the plates to Latching so correct steps stay visibly pressed.
/// </summary>
public class PlatePuzzle : MonoBehaviour
{
    public enum PuzzleMode { AllPressed, Sequence }

    [SerializeField] private PuzzleMode mode = PuzzleMode.AllPressed;

    [Tooltip("AllPressed: every plate here must be pressed. Sequence: plates must be pressed in this order, top to bottom.")]
    [SerializeField] private PressurePlate[] plates;

    [Tooltip("AllPressed only: stay solved even if a plate is released later.")]
    [SerializeField] private bool permanent = false;

    [Header("Events")]
    public UnityEvent onSolved;
    [Tooltip("AllPressed only: fires when a plate is released after the puzzle was solved.")]
    public UnityEvent onUnsolved;
    [Tooltip("Sequence only: fires when the wrong plate is stepped on.")]
    public UnityEvent onWrongStep;

    public bool IsSolved { get; private set; }

    private int progress;

    private void OnEnable()
    {
        foreach (PressurePlate plate in plates)
        {
            if (plate == null) continue;
            plate.Pressed += OnPlatePressed;
            plate.Released += OnPlateReleased;
        }
    }

    private void OnDisable()
    {
        foreach (PressurePlate plate in plates)
        {
            if (plate == null) continue;
            plate.Pressed -= OnPlatePressed;
            plate.Released -= OnPlateReleased;
        }
    }

    private void OnPlatePressed(PressurePlate plate)
    {
        if (mode == PuzzleMode.AllPressed)
        {
            EvaluateAll();
            return;
        }

        // Sequence
        if (IsSolved || plates.Length == 0) return;

        if (plate == plates[progress])
        {
            progress++;
            if (progress >= plates.Length)
            {
                IsSolved = true;
                onSolved.Invoke();
            }
        }
        else
        {
            progress = 0;
            onWrongStep.Invoke();
            ResetPlates();
        }
    }

    private void OnPlateReleased(PressurePlate plate)
    {
        if (mode == PuzzleMode.AllPressed) EvaluateAll();
    }

    private void EvaluateAll()
    {
        if (IsSolved && permanent) return;

        bool all = plates.Length > 0;
        foreach (PressurePlate p in plates)
        {
            if (p == null || !p.IsPressed)
            {
                all = false;
                break;
            }
        }

        if (all && !IsSolved)
        {
            IsSolved = true;
            onSolved.Invoke();
        }
        else if (!all && IsSolved)
        {
            IsSolved = false;
            onUnsolved.Invoke();
        }
    }

    /// <summary>Resets progress and releases every plate. Hook this to a reset button if you want one.</summary>
    public void ResetPuzzle()
    {
        progress = 0;
        IsSolved = false;
        ResetPlates();
    }

    private void ResetPlates()
    {
        foreach (PressurePlate p in plates)
            if (p != null) p.ResetPlate();
    }
}
