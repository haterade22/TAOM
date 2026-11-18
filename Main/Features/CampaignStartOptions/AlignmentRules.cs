namespace TAOM.Features.CampaignStartOptions;

/// <summary>
/// What an Alignment rules preset fixes: the four master switches that decide how strictly the Free and
/// Evil sides stay apart. The sub-switches (apply to player, AI, parties, garrisons) are not part of a
/// preset and keep following MCM. This is the single place the two presets are defined.
/// </summary>
public sealed class AlignmentRules
{
    public AlignmentRules(bool recruitmentBlock, bool marriageBlock, bool desertion, bool allianceWithAnyone)
    {
        RecruitmentBlock = recruitmentBlock;
        MarriageBlock = marriageBlock;
        Desertion = desertion;
        AllianceWithAnyone = allianceWithAnyone;
    }

    /// <summary>Strict: every block on, and the player's kingdom may not ally across the Free/Evil line.</summary>
    public static readonly AlignmentRules Strict = new AlignmentRules(true, true, true, false);

    /// <summary>Relaxed: no alignment block, no alignment desertion, and the player may ally with anyone.</summary>
    public static readonly AlignmentRules Relaxed = new AlignmentRules(false, false, false, true);

    /// <summary>Recruitment Alignment Block (<c>EnableAlignmentRecruitment</c>).</summary>
    public bool RecruitmentBlock { get; }

    /// <summary>Marriage Alignment Block (<c>EnableMarriageAlignment</c>).</summary>
    public bool MarriageBlock { get; }

    /// <summary>Alignment Desertion (<c>EnableAlignmentDesertion</c>).</summary>
    public bool Desertion { get; }

    /// <summary>Allow Player Alliances With Anyone (<c>AllowPlayerAlliancesWithAnyone</c>).</summary>
    public bool AllianceWithAnyone { get; }

    /// <summary>True when the four switches are the same.</summary>
    public bool Matches(AlignmentRules other) =>
        RecruitmentBlock == other.RecruitmentBlock
        && MarriageBlock == other.MarriageBlock
        && Desertion == other.Desertion
        && AllianceWithAnyone == other.AllianceWithAnyone;

    /// <summary>True for a preset id this build knows (Standard, Strict or Relaxed).</summary>
    public static bool IsKnownPreset(string? preset) =>
        preset == CampaignStartOptionKeys.AlignmentStandardItem
        || preset == CampaignStartOptionKeys.AlignmentStrictItem
        || preset == CampaignStartOptionKeys.AlignmentRelaxedItem;

    /// <summary>The rules a preset pins; null for Standard and for an unknown id (follow MCM).</summary>
    public static AlignmentRules? ForPreset(string? preset) =>
        preset == CampaignStartOptionKeys.AlignmentStrictItem ? Strict
        : preset == CampaignStartOptionKeys.AlignmentRelaxedItem ? Relaxed
        : null;
}
