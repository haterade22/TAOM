using TaleWorlds.SaveSystem;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// Registers <see cref="TaomSettlementClaimantDecision"/> with the TaleWorlds save system (#458).
/// Auto-discovered by the engine, so there is nothing to register by hand.
///
/// Required, not optional: for the PLAYER's kingdom <c>Kingdom.AddDecision</c> queues the decision
/// into <c>_unresolvedDecisions</c>, which is a <c>[SaveableField]</c>. An unregistered concrete
/// type in that list fails to resolve on load. AI kingdoms resolve their elections inline and never
/// persist one, so this only matters for the player's own kingdom.
///
/// BaseId 726900901 — next in TAOM's 7269xxx series after LotrIssue (726900801), CareerQuest
/// (726900701), FormationPreset (726900601), EquipPresets (726900501). The bases step by 100, so the
/// per-class localId MUST start at 101: that lands the global id at 726901002, clear of LotrIssue's
/// 726900902. A localId of 1 would yield 726900902 and collide at Module.Initialize.
/// <c>SaveDefinerCollisionGuard</c> reports any base-id clash with another mod at startup.
/// </summary>
public sealed class FiefGrantSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 726900901;

    public FiefGrantSaveableTypeDefiner() : base(SaveBaseId) { }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(TaomSettlementClaimantDecision), 101);
    }
}
