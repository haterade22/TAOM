namespace TAOM.Features.HeroRace;

/// <summary>
/// Production wire-up of <see cref="IHeroRaceSettingsProvider"/> over the MCM
/// <c>TaomSettings</c> singleton.
///
/// <para>Defaults match the slider defaults in TaomSettings so a missing MCM instance behaves
/// exactly as the previous compile-time constants did, rather than silently disabling the
/// feature.</para>
///
/// <para>The instance is NOT cached at construction: unlike the nameplate provider this is read
/// once per <c>FaceGen.GetBaseMonsterFromRace</c> call, not thousands of times a second, and
/// reading through the live singleton means a settings change applies without a restart.</para>
/// </summary>
public class HeroRaceSettingsProvider : IHeroRaceSettingsProvider
{
    public bool EyeHeightEnabled => TaomSettings.Instance?.EnableDwarfEyeHeight ?? true;

    public float DwarfEyeHeightAdjuster =>
        TaomSettings.Instance?.DwarfEyeHeightAdjuster ?? EyeHeightAdjustment.DefaultAdjuster;
}
