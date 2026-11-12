using TAOM.Core.Validation;

namespace TAOM.Features.Realms;

/// <summary>
/// The "World/Realms" MCM group, clamped. The bounds match the slider ranges on <see cref="TaomSettings"/>; they exist for
/// a hand-edited MCM json, which MCM deserializes verbatim. The defaults live here so the property initializers and the
/// fallback before MCM is up cannot drift apart. The settings reference is taken lazily and read through, so every
/// change applies at once.
/// </summary>
public sealed class RealmsSettingsProvider : IRealmsSettingsProvider
{
    public const int DefaultPercent = 100;
    public const int MinCostPercent = 10;
    public const int MaxCostPercent = 500;
    public const int MinPaymentPercent = 0;
    public const int MaxPaymentPercent = 300;

    /// <summary>Tribute is a share of fief income, so its range is far below the payments'.</summary>
    public const int DefaultTributePercent = 5;
    public const int MinTributePercent = 1;
    public const int MaxTributePercent = 25;

    private TaomSettings? _settings;

    public RealmsSettingsProvider()
    {
    }

    internal RealmsSettingsProvider(TaomSettings? settings) => _settings = settings;

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public int ClaimPricePercent => Cost(Settings?.RealmsClaimPricePercent);
    public int InfluenceCostPercent => Cost(Settings?.RealmsInfluenceCostPercent);
    public bool AiClaims => Settings?.RealmsAiClaims ?? true;
    public bool TributeReceive => Settings?.RealmsTributeReceive ?? true;
    public int TributeReceivePercent => Tribute(Settings?.RealmsTributeReceivePercent);
    public bool TributePay => Settings?.RealmsTributePay ?? true;
    public int TributePayPercent => Tribute(Settings?.RealmsTributePayPercent);
    public int OfficeInfluencePercent => Payment(Settings?.RealmsOfficeInfluencePercent);
    public bool Resentment => Settings?.RealmsResentment ?? true;
    public bool LiegePull => Settings?.RealmsLiegePull ?? true;
    public bool LiegeArmyDiscount => Settings?.RealmsLiegeArmyDiscount ?? true;
    public bool FamilyGrants => Settings?.RealmsFamilyGrants ?? true;
    public bool TitleStyles => Settings?.RealmsTitleStyles ?? true;

    internal static int Cost(int? value) => SettingClamp.Clamp(value, DefaultPercent, MinCostPercent, MaxCostPercent);

    internal static int Payment(int? value) => SettingClamp.Clamp(value, DefaultPercent, MinPaymentPercent, MaxPaymentPercent);

    internal static int Tribute(int? value) => SettingClamp.Clamp(value, DefaultTributePercent, MinTributePercent, MaxTributePercent);
}
