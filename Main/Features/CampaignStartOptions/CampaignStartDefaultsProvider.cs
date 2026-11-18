using TAOM.Features.AiPartySize;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.Diplomacy;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.HostilePeace;
using TAOM.Features.MarriageAlignment;
using TAOM.Features.TroopWeight;
using TAOM.Features.WarOfTheRingMomentum;

namespace TAOM.Features.CampaignStartOptions;

/// <summary>
/// The MCM-else-JSON answer for each start option, read live (MCM can change between two campaigns). It
/// never consults the campaign, so the main-menu screen, the pin and the "this campaign differs from MCM"
/// check all see "what a new campaign gets". Each value comes from its own feature's provider.
/// </summary>
public sealed class CampaignStartDefaultsProvider : ICampaignStartDefaults
{
    private readonly IWarOfTheRingService _wotr;
    private readonly ITroopWeightSettingsProvider _troopWeight;
    private readonly IMomentumSettingsProvider _momentum;
    private readonly IEyeOfSauronSettings _eye;
    private readonly IAiPartyScalingSettingsProvider _aiScaling;
    private readonly IRecruitmentAlignmentSettingsProvider _recruitment;
    private readonly IMarriageAlignmentSettingsProvider _marriage;
    private readonly IAlignmentDesertionSettingsProvider _desertion;
    private readonly IHostilePeaceSettingsProvider _hostilePeace;

    public CampaignStartDefaultsProvider(
        IWarOfTheRingService wotr,
        ITroopWeightSettingsProvider troopWeight,
        IMomentumSettingsProvider momentum,
        IEyeOfSauronSettings eye,
        IAiPartyScalingSettingsProvider aiScaling,
        IRecruitmentAlignmentSettingsProvider recruitment,
        IMarriageAlignmentSettingsProvider marriage,
        IAlignmentDesertionSettingsProvider desertion,
        IHostilePeaceSettingsProvider hostilePeace)
    {
        _wotr = wotr;
        _troopWeight = troopWeight;
        _momentum = momentum;
        _eye = eye;
        _aiScaling = aiScaling;
        _recruitment = recruitment;
        _marriage = marriage;
        _desertion = desertion;
        _hostilePeace = hostilePeace;
    }

    public StartDefaults Current()
    {
        var wotr = _wotr.NewCampaignEnabled;
        var (day1, day2) = _wotr.NewCampaignPhaseDays;

        // Clamp once, here: the screen, the pin, the mismatch check and `list` all compare the same valid pair, so an
        // inverted MCM pair cannot make an unchanged Standard campaign look different.
        (day1, day2) = CampaignStartOptionsResolver.ClampPair(day1, day2);

        return new StartDefaults(
            wotr, day1, day2,
            _troopWeight.NewCampaignDefault,
            _momentum.VictoryNewCampaignDefault,
            _eye.EnabledNewCampaignDefault,
            _aiScaling.NewCampaignDefault,
            new AlignmentRules(
                _recruitment.NewCampaignDefault,
                _marriage.NewCampaignDefault,
                _desertion.NewCampaignDefault,
                _hostilePeace.AllowPlayerAlliancesNewCampaignDefault));
    }
}
