using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TAOM.Features.EncounterLines.Hooks;

namespace TAOM.Features.EncounterLines.Tags;

/// <summary>
/// One conversation tag per lord bucket (<c>taom_player_&lt;bucket&gt;</c>), the second tag of every row in
/// <c>taom_encounter_lord_strings.xml</c>, and <see cref="TaomAltLineTag"/>, which splits the two surrender demands so the
/// <c>_2</c> row is not lost to a score tie (Patch149). Same shape as <see cref="TaomSpeakerWestTag"/>: concrete,
/// parameterless, no shared base, answered by <see cref="EncounterTagGate"/>.
/// </summary>
public sealed class TaomPlayerLiegeTag : ConversationTag
{
    public const string Bucket = EncounterBuckets.Liege;

    public override string StringId => "taom_player_" + Bucket;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Bucket(character, Bucket);
}

public sealed class TaomPlayerAllyTag : ConversationTag
{
    public const string Bucket = EncounterBuckets.Ally;

    public override string StringId => "taom_player_" + Bucket;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Bucket(character, Bucket);
}

public sealed class TaomPlayerRivalTag : ConversationTag
{
    public const string Bucket = EncounterBuckets.Rival;

    public override string StringId => "taom_player_" + Bucket;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Bucket(character, Bucket);
}

public sealed class TaomPlayerFoeTag : ConversationTag
{
    public const string Bucket = EncounterBuckets.Foe;

    public override string StringId => "taom_player_" + Bucket;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Bucket(character, Bucket);
}

public sealed class TaomPlayerKinTag : ConversationTag
{
    public const string Bucket = EncounterBuckets.Kin;

    public override string StringId => "taom_player_" + Bucket;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Bucket(character, Bucket);
}

/// <summary>
/// True on the days a lord's surrender demand is the <c>_2</c> row: <c>_1</c> rows carry it at weight -1 and <c>_2</c>
/// rows at weight 1, so exactly one of the pair passes (<see cref="EncounterBuckets.IsAltLine"/>).
/// </summary>
public sealed class TaomAltLineTag : ConversationTag
{
    public const string Id = "taom_alt_line";

    public override string StringId => Id;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.AltLine(character);
}
