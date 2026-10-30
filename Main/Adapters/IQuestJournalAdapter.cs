namespace TAOM.Adapters;

/// <summary>
/// The Quest log's journal items, as the quest-log fast open reaches them (docs/features/shadowfax-screens.md). Items are
/// handed over as opaque objects (a <c>QuestItemVM</c> built from a finished quest's journal entry), so the defer service
/// holds no TaleWorlds type. The implementation names <c>QuestItemVM</c>, a client-only type, so it lives in TAOM.Client
/// (ADR-013): core registers <see cref="NullQuestJournalAdapter"/> and the client composition replaces it.
/// </summary>
public interface IQuestJournalAdapter
{
    /// <summary>
    /// True when <see cref="Bind"/> found every member and every guard patch is attached right now. The patches are checked
    /// again on every read (a cheap Harmony lookup, once per open), so a patch removed after the install stops deferral.
    /// </summary>
    bool IsBound { get; }

    /// <summary>
    /// Resolves the private <c>QuestItemVM.PopulateQuestLog</c> and checks that the four patches that keep a deferred item
    /// correct are attached. Returns null when bound, else why not; never throws.
    /// </summary>
    string? Bind();

    /// <summary>
    /// Builds a deferred item's stages the way its constructor would have: <c>PopulateQuestLog(entry, false)</c> for each
    /// journal entry through reflection (so other patches on it still run), then <c>UpdateIsUpdated</c> and
    /// <c>RefreshValues</c>. Throws if the item or the binding is wrong.
    /// </summary>
    void Rebuild(object item);

    /// <summary>True when any of the item's journal entries is still in the campaign's unexamined quest logs.</summary>
    bool AnyUnexamined(object item);
}
