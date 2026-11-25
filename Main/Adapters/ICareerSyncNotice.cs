namespace TAOM.Adapters;

/// <summary>The one line a co-op player sees when the co-op server refused a career change.</summary>
public interface ICareerSyncNotice
{
    /// <summary>The server refused the change and keeps the career it has saved for this hero.</summary>
    void ShowRefused();

    /// <summary>The server refused the career and has none saved for this hero, so it is lost at the next join.</summary>
    void ShowRefusedUnsaved();
}
