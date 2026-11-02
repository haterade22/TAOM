using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>Boundary implementation of <see cref="IHeroCharacterStateAdapter"/>.</summary>
public sealed class HeroCharacterStateAdapter : IHeroCharacterStateAdapter
{
    private static readonly FieldInfo OriginCharacterField =
        AccessTools.Field(typeof(CharacterObject), "_originCharacter");

    private readonly Hero _hero;

    public HeroCharacterStateAdapter(Hero hero) => _hero = hero;

    public string HeroId => _hero.StringId ?? string.Empty;

    // The stale fields first: Coop's culture getter postfix writes on a read, so culture alone reads a hollow parent
    // as healthy on a co-op host. Through the base type on purpose: CharacterObject.Culture is a new member that
    // answers with HeroObject.Culture on a hero, so it never shows the raw value that FillFrom copies.
    public bool IsCharacterHollow =>
        _hero.CharacterObject is CharacterObject character
        && (StaleCharacterAdapter.IsStale(character) || ((BasicCharacterObject)character).Culture == null);

    public string DescribeState()
    {
        var character = _hero.CharacterObject;
        if (character == null) return $"hero={HeroId} character=null";

        var twin = FindRegisteredTwin(character);
        return $"hero={HeroId} character={character.StringId} original={character.IsOriginalCharacter} " +
            $"origin={character.OriginalCharacter?.StringId ?? "none"} ready={character.IsReady} " +
            $"registeredSameInstance={ReferenceEquals(twin, character)} " +
            $"registeredCulture={((BasicCharacterObject)twin)?.Culture?.StringId ?? "none"} " +
            $"skillsNull={character.GetDefaultCharacterSkills() == null} " +
            $"bodyNull={character.BodyPropertyRange == null} basicNameNull={StaleCharacterAdapter.IsBasicNameNull(character)} " +
            $"race={character.Race.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
            $"heroCulture={_hero.Culture?.StringId ?? "none"} clanCulture={_hero.Clan?.Culture?.StringId ?? "none"} " +
            $"alive={_hero.IsAlive}";
    }

    public bool TryRepairCharacter()
    {
        var character = _hero.CharacterObject;
        var twin = FindRegisteredTwin(character);
        if (IsHealthyOrigin(twin, character) && TryAdoptOrigin(character, twin)) return true;

        var raw = (BasicCharacterObject)character;
        var culture = raw.Culture ?? _hero.Culture ?? _hero.Clan?.Culture;
        if (culture == null || !StaleCharacterAdapter.BindingsResolved) return false;

        raw.Culture = culture;
        StaleCharacterAdapter.FillNullFields(character);
        return true;
    }

    // The registered character of the same id, built from XML. A hollow hero keeps an object the object manager no
    // longer holds, so XML made this twin instead (MBObjectBase.OnBeforeLoad, MBObjectManager.GetPresumedObject). A
    // generated hero's root is its origin, so its id leads to the same twin.
    private static CharacterObject FindRegisteredTwin(CharacterObject character)
    {
        var id = character.OriginalCharacter?.StringId ?? character.StringId;
        return string.IsNullOrEmpty(id) ? null : MBObjectManager.Instance?.GetObject<CharacterObject>(id);
    }

    private static bool IsHealthyOrigin(CharacterObject twin, CharacterObject character) =>
        twin != null && !ReferenceEquals(twin, character)
        && ((BasicCharacterObject)twin).Culture != null && twin.BodyPropertyRange != null;

    // The engine's own shape for a generated hero. _originCharacter is saved, so for a hero in the hero lists
    // SandBoxManager repeats this copy on every later load. A child's origin is then this twin too
    // (CharacterObject.CreateFrom), so the child also keeps its race and body range after a load.
    internal static bool TryAdoptOrigin(CharacterObject hollow, CharacterObject origin)
    {
        if (OriginCharacterField == null) return false;

        OriginCharacterField.SetValue(hollow, origin);
        hollow.InitializeHeroCharacterOnAfterLoad();
        return true;
    }
}
