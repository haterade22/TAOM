using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Features.CompanionTactics.BattleActionBar;

/// <summary>
/// Tracks per-formation stance state during a battle. Display-only — stances do NOT
/// alter formation behavior in v1.3.15 (the underlying engine APIs are not exposed).
/// State is keyed by formation index (int) so the manager remains testable without
/// a sealed Formation reference.
/// </summary>
public interface ITroopStanceManager
{
    void SetStance(int formationIndex, TroopStance stance);
    TroopStance GetStance(int formationIndex);
    void ClearStance(int formationIndex);
    void ClearAllStances();
}
