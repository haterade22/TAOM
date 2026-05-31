namespace TAOM.Features.CastleRecruitment;

public interface ICastleRecruitmentSettingsProvider
{
    bool IsEnabled { get; }
    bool IsAiEnabled { get; }
    int NotablesPerCastle { get; }
}
