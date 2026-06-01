using System.Collections.Generic;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

// Pure formatter. The collision-mesh tokens (bo=, shieldBo=, holsterBo=) are the
// diagnostic payload: a `<null>` on the last slot logged before a hang names the
// missing `bo_` collision mesh the engine stalled on while equipping the agent.
public sealed class EquipmentDumpFormatter : IEquipmentDumpFormatter
{
    public IReadOnlyList<string> Format(EquipmentSnapshot snapshot)
    {
        var lines = new List<string>();
        if (snapshot?.Slots == null) return lines;

        foreach (var slot in snapshot.Slots)
        {
            if (slot == null) continue;
            lines.Add(
                $"slot={slot.SlotName} id={slot.ItemId} " +
                $"bo={Tok(slot.BodyName)} shieldBo={Tok(slot.CollisionBodyName)} " +
                $"holsterBo={Tok(slot.HolsterBodyName)} mesh={Tok(slot.MultiMeshName)} " +
                $"kind={slot.ComponentKind}");
        }
        return lines;
    }

    private static string Tok(string? value) => string.IsNullOrEmpty(value) ? "<null>" : value!;
}
