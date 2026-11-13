using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TAOM.Features.EncounterLines.Hooks;

namespace TAOM.Features.EncounterLines.Tags;

/// <summary>
/// One conversation tag per speaker group, so a row in <c>taom_encounter_lord_strings.xml</c> can carry
/// <c>taom_speaker_&lt;group&gt;</c> as its first tag (Patch149). The dialog lookup (<c>ConversationManager.FindMatchingTextOrNull</c>)
/// stops scoring a row at its first failing tag. <c>ConversationManager.InitializeTags</c> finds these by reflection
/// and calls <c>Activator.CreateInstance</c> on every subclass, an abstract base included, so there is no shared base
/// class: each class is concrete and parameterless, with a unique <c>StringId</c>. The answer comes from
/// <see cref="EncounterTagGate"/>.
/// </summary>
public sealed class TaomSpeakerWestTag : ConversationTag
{
    public const string Group = "west";

    public override string StringId => "taom_speaker_" + Group;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Speaker(character, Group);
}

public sealed class TaomSpeakerRohirrimTag : ConversationTag
{
    public const string Group = "rohirrim";

    public override string StringId => "taom_speaker_" + Group;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Speaker(character, Group);
}

public sealed class TaomSpeakerNorthmenTag : ConversationTag
{
    public const string Group = "northmen";

    public override string StringId => "taom_speaker_" + Group;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Speaker(character, Group);
}

public sealed class TaomSpeakerDunlendingsTag : ConversationTag
{
    public const string Group = "dunlendings";

    public override string StringId => "taom_speaker_" + Group;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Speaker(character, Group);
}

public sealed class TaomSpeakerSouthronsTag : ConversationTag
{
    public const string Group = "southrons";

    public override string StringId => "taom_speaker_" + Group;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Speaker(character, Group);
}

public sealed class TaomSpeakerEasterlingsTag : ConversationTag
{
    public const string Group = "easterlings";

    public override string StringId => "taom_speaker_" + Group;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Speaker(character, Group);
}

public sealed class TaomSpeakerElvesTag : ConversationTag
{
    public const string Group = "elves";

    public override string StringId => "taom_speaker_" + Group;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Speaker(character, Group);
}

public sealed class TaomSpeakerDwarvesTag : ConversationTag
{
    public const string Group = "dwarves";

    public override string StringId => "taom_speaker_" + Group;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Speaker(character, Group);
}

public sealed class TaomSpeakerOrcsTag : ConversationTag
{
    public const string Group = "orcs";

    public override string StringId => "taom_speaker_" + Group;

    public override bool IsApplicableTo(CharacterObject character) => EncounterTagGate.Speaker(character, Group);
}
