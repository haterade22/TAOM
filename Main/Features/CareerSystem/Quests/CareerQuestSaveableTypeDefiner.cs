using TaleWorlds.SaveSystem;

namespace TAOM.Features.CareerSystem.Quests;

/// <summary>
/// Registers <see cref="CareerQuest"/> with the TaleWorlds save system. Auto-discovered by the
/// engine (no manual registration). BaseId 726900701 — TAOM-unique, next in the 7269007xx series
/// after FormationPreset (726900601) and EquipPresets (726900501). The quest's [SaveableField]
/// members are string / List&lt;int&gt; / List&lt;JournalLog&gt; — all basic or already-registered
/// types, so only the class definition is needed (no enum/struct definitions).
/// </summary>
/// <remarks>
/// The engine global type id is <c>_saveBaseId + localId</c> (SaveableTypeDefiner.AddClassDefinition,
/// verified 1.4.5). TAOM's definer bases step by 100, so the per-class localId MUST start at 101 (not 1)
/// — that lands the id in the base+100 century block and keeps it clear of the previous definer's range.
/// FormationPreset (726900601+101 = 726900702) and EquipPresets (726900501+101/102) both follow this.
/// Using localId 1 here produced 726900702, colliding with FormationPreset → "An item with the same key
/// has already been added" at Module.Initialize. localId 101 → 726900802 (collision-free).
/// </remarks>
public sealed class CareerQuestSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 726900701;

    public CareerQuestSaveableTypeDefiner() : base(SaveBaseId) { }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(CareerQuest), 101);
    }
}
