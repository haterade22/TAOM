namespace TAOM.Features.FiefManagement;

public class FiefManagementSettingsProvider : IFiefManagementSettingsProvider
{
    public bool EnableFiefManagement => TaomSettings.Instance?.EnableFiefManagement ?? true;
    public bool AllowRemoteBuildingQueue => TaomSettings.Instance?.AllowRemoteBuildingQueue ?? true;
    public bool IsDebugMode => TaomSettings.Instance?.FiefManagementDebug ?? false;
}
