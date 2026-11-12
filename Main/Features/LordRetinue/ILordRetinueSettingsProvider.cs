namespace TAOM.Features.LordRetinue;

public interface ILordRetinueSettingsProvider
{
    bool IsEnabled { get; }

    int MaxExtraHeroes { get; }
}
