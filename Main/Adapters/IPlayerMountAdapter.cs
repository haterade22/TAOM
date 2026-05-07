using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Adapters;

/// <summary>
/// Reads and mutates the player main hero's mount + harness equipment slots.
/// Returns opaque <see cref="IMountSnapshot"/> tokens so services never see <c>EquipmentElement</c> directly (ADR-007).
/// </summary>
public interface IPlayerMountAdapter
{
    bool HasMount();
    IMountSnapshot Capture();
    void Clear();
    void Restore(IMountSnapshot snapshot);
}
