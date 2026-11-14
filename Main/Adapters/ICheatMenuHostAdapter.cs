namespace TAOM.Adapters;

/// <summary>Where the cheat menu opens: the free campaign map only (the shared map-menu gate), then its root game menu.</summary>
public interface ICheatMenuHostAdapter
{
    bool IsMapClearForMenu { get; }

    void OpenMenu();
}
