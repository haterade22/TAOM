using TaleWorlds.SaveSystem;
using TAOM.Features.LotrIssues.Templates;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Registers the LOTR issue/quest template classes with the TaleWorlds save system. Auto-discovered by
/// the engine (no manual registration). BaseId 726900801 — TAOM-unique, next in the 7269008xx series
/// after CareerQuest (726900701), FormationPreset (726900601), EquipPresets (726900501).
/// </summary>
/// <remarks>
/// The engine global type id is <c>_saveBaseId + localId</c>. TAOM's definer bases step by 100, so the
/// per-class localId MUST start at 101 — that lands the id in the base+100 century block (726900902+),
/// clear of CareerQuest's 726900802. localId 1 would yield 726900802 and collide → "An item with the
/// same key has already been added" at Module.Initialize.
///
/// One (Issue, Quest) pair per template. Wave 0 ships T1 (DeliverGoods) at 101/102. Each later wave
/// appends its pair at the next free localId (103/104, …) — additive, never renumbered, so older saves
/// keep loading.
/// </remarks>
public sealed class LotrIssueSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 726900801;

    public LotrIssueSaveableTypeDefiner() : base(SaveBaseId) { }

    protected override void DefineClassTypes()
    {
        // T1 — DeliverGoods (Wave 0)
        AddClassDefinition(typeof(DeliverGoodsLotrIssue), 101);
        AddClassDefinition(typeof(DeliverGoodsLotrIssueQuest), 102);
        // DeliverPersonnel — bandit-prisoner delivery (Wave 1)
        AddClassDefinition(typeof(DeliverPersonnelLotrIssue), 103);
        AddClassDefinition(typeof(DeliverPersonnelLotrIssueQuest), 104);
        // Combat — defeat-raids / capture-lords (Wave 2)
        AddClassDefinition(typeof(CombatLotrIssue), 105);
        AddClassDefinition(typeof(CombatLotrIssueQuest), 106);
    }
}
