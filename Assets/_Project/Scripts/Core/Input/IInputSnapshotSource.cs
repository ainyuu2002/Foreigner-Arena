using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// Alternate held-input source. CharacterMotor remains the owner of InputBuffer and CommandParser.
    /// </summary>
    public interface IInputSnapshotSource
    {
        bool IsInputActive { get; }
        Vector2 Move { get; }
        InputSnapshot ReadSnapshot(int frame);
    }
}
