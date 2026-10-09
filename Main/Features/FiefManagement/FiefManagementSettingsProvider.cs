namespace TAOM.Features.FiefManagement;

public class FiefManagementSettingsProvider : IFiefManagementSettingsProvider
{
    // Read every campaign frame: the Fiefs button's permission asks FiefHubOpener, which asks here.
    // Resolving TaomSettings.Instance walks MCM's settings containers, so the reference is cached on its
    // first non-null read and read THROUGH, never snapshotted: MCM edits its one registered instance in
    // place, so live edits still apply. Lazy, not in the constructor, so a resolve before MCM is up cannot
    // pin the fallbacks. Same contract as QuickActionsSettingsProvider and CampSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public FiefManagementSettingsProvider() { }
    internal FiefManagementSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool EnableFiefManagement => Settings?.EnableFiefManagement ?? true;
    public bool AllowRemoteBuildingQueue => Settings?.AllowRemoteBuildingQueue ?? true;
    public bool IsDebugMode => Settings?.FiefManagementDebug ?? false;
}
