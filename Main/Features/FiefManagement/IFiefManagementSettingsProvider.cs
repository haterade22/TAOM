namespace TAOM.Features.FiefManagement;

public interface IFiefManagementSettingsProvider
{
    bool EnableFiefManagement { get; }
    bool AllowRemoteBuildingQueue { get; }
    bool IsDebugMode { get; }
}
