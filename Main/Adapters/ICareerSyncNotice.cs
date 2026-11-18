namespace TAOM.Adapters;

/// <summary>The one line a co-op player sees when the co-op server refused a career change.</summary>
public interface ICareerSyncNotice
{
    void ShowRefused();
}
