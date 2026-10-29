using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Tags;

namespace TAOM.Features.LiegeTitles;

/// <summary>
/// One conversation tag per TAOM-only kingdom culture, so a <c>str_liege_title.&lt;culture&gt;</c> row
/// in <c>taom_module_strings.xml</c> can carry <c>taom_culture_&lt;culture&gt;</c> as its only tag (#955).
/// The dialog lookup (<c>ConversationManager.FindMatchingTextOrNull</c>) scores a row by its tags and an
/// untagged row scores 0 for every culture, so a row without a tag of its own would win for all of them.
/// <c>ConversationManager.InitializeTags</c> finds these by reflection over the active game assemblies
/// and calls <c>Activator.CreateInstance</c> on every subclass it finds, an abstract base included, so there
/// is no shared base class: each class is concrete and parameterless, with a unique <c>StringId</c>.
/// </summary>
public sealed class TaomAbanissaCultureTag : ConversationTag
{
    public const string CultureId = "abanissa";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomAnduinValeCultureTag : ConversationTag
{
    public const string CultureId = "anduinvale";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomAngaladhCultureTag : ConversationTag
{
    public const string CultureId = "angaladh";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomDorwinionCultureTag : ConversationTag
{
    public const string CultureId = "dorwinion";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomSouthRhovanionCultureTag : ConversationTag
{
    public const string CultureId = "southrhovanion";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomArthedainCultureTag : ConversationTag
{
    public const string CultureId = "arthedain";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomBlueCraigCultureTag : ConversationTag
{
    public const string CultureId = "bluecraig";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomCardolanCultureTag : ConversationTag
{
    public const string CultureId = "cardolan";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomDolGuldurCultureTag : ConversationTag
{
    public const string CultureId = "dolguldur";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomEreborCultureTag : ConversationTag
{
    public const string CultureId = "erebor";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomEredLuinCultureTag : ConversationTag
{
    public const string CultureId = "eredluin";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomGondorCultureTag : ConversationTag
{
    public const string CultureId = "gondor";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomGoblinCultureTag : ConversationTag
{
    public const string CultureId = "goblin";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomGundabadCultureTag : ConversationTag
{
    public const string CultureId = "gundabad";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomIsengardCultureTag : ConversationTag
{
    public const string CultureId = "isengard";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomLindonCultureTag : ConversationTag
{
    public const string CultureId = "lindon";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomLothlorienCultureTag : ConversationTag
{
    public const string CultureId = "lothlorien";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomMirkwoodCultureTag : ConversationTag
{
    public const string CultureId = "mirkwood";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomMistyMountainOrcsCultureTag : ConversationTag
{
    public const string CultureId = "mistymountainorcs";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomMordorCultureTag : ConversationTag
{
    public const string CultureId = "mordor";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomRhudaurCultureTag : ConversationTag
{
    public const string CultureId = "rhudaur";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomRivendellCultureTag : ConversationTag
{
    public const string CultureId = "rivendell";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomShaghanaCultureTag : ConversationTag
{
    public const string CultureId = "shaghana";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}

public sealed class TaomUmbarCultureTag : ConversationTag
{
    public const string CultureId = "umbar";

    public override string StringId => "taom_culture_" + CultureId;

    public override bool IsApplicableTo(CharacterObject character) => character?.Culture?.StringId == CultureId;
}
