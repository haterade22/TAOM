using System.Collections.Generic;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

// Pure formatter: turns an EquipmentSnapshot into human-readable per-slot log lines.
// No TaleWorlds types, no IO — fully unit-testable.
public interface IEquipmentDumpFormatter
{
    IReadOnlyList<string> Format(EquipmentSnapshot snapshot);
}
