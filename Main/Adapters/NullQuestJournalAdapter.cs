namespace TAOM.Adapters;

/// <summary>The quest log is absent (a dedicated server, or a client where TAOM.Client did not load): the fast open never binds,
/// so the quest log builds every quest at open as vanilla does.</summary>
public sealed class NullQuestJournalAdapter : IQuestJournalAdapter
{
    public bool IsBound => false;

    public string? Bind() => "the quest log adapter lives in TAOM.Client, which is not loaded";

    public void Rebuild(object item) => throw new System.InvalidOperationException("NullQuestJournalAdapter never binds");

    public bool AnyUnexamined(object item) => false;
}
