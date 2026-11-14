using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms;

/// <summary>Turns titles and hero ids into display text.</summary>
internal static class TitleNames
{
    /// <summary>The title's display name: its prefix rule, culture rank name or rank name, with the base name filled in.</summary>
    public static string Of(TitleDefinition title, RealmsConfig config, TitleCatalog? catalog)
    {
        var text = new TextObject(config.RankNames.Template(title, catalog));
        text.SetTextVariable("NAME", new TextObject(title.Name));
        return text.ToString();
    }

    public static string Of(TitleDefinition title, RealmsCampaign behavior) =>
        Of(title, behavior.Config, behavior.Service.Catalog);

    /// <summary>The hero's name as the titles show it: "(vacant)" without a hero, else the game's name.</summary>
    public static string Hero(string? heroId)
    {
        if (heroId == null)
            return new TextObject("{=realms_vacant}(vacant)").ToString();
        return Find(heroId)?.Name?.ToString() ?? heroId;
    }

    /// <summary>
    /// The holder by right as they are known (<see cref="StyleOf"/>, "King Aragorn" on every title he holds, "Prince
    /// Imrahil" on the Duchy of Belfalas too); the plain name when no title or office of theirs gives a style, "(vacant)"
    /// without a holder. Every holder line shows a hero the same way, whichever of their titles it belongs to.
    /// </summary>
    public static string Styled(TitleDefinition title, string? heroId, RealmsCampaign behavior)
    {
        var hero = heroId != null ? Find(heroId) : null;
        return (hero != null ? StyleOf(hero, behavior) : null)?.ToString() ?? Hero(heroId);
    }

    /// <summary>The style alone, without the place or epithet ("King Aragorn", "Steward Denethor"); see <see cref="KnownAs"/>.</summary>
    public static TextObject? StyleOf(Hero hero, RealmsCampaign behavior) => Known(hero, behavior)?.Style;

    /// <summary>
    /// The hero as known by their title (<see cref="KnownBy"/>): style, bare name and that title's base name, then the
    /// epithet ("Lord Forlong of Lossarnach", "Lord Cerdic of Dunland, the Anvil", "Prince Imrahil of Dol Amroth").
    /// An office with a holder style beats every title of its rank or lower ("Steward Denethor of Gondor").
    /// Null when they hold no title, that title gives no style, or titles are off.
    /// </summary>
    public static TextObject? KnownAs(Hero hero, RealmsCampaign behavior)
    {
        var known = Known(hero, behavior);
        return known != null ? Compose(hero, known.Value.Style, known.Value.Place) : null;
    }

    /// <summary>
    /// The title the hero is known by: the one they chose on its page, else their highest title held by right,
    /// unless a lower one keeps its own style over it (a prefix's <c>known_over</c>, Dol Amroth's Prince). Null when they
    /// hold none or titles are off.
    /// </summary>
    public static TitleDefinition? KnownBy(Hero hero, RealmsCampaign behavior)
    {
        if (behavior.IsDisabled || !behavior.Service.IsInitialized || hero.StringId == null)
            return null;
        return behavior.Service.KnownByTitleOf(hero.StringId, hero.Culture?.StringId);
    }

    private static (TextObject Style, TextObject? Place)? Known(Hero hero, RealmsCampaign behavior)
    {
        if (behavior.IsDisabled || !behavior.Service.IsInitialized || hero.StringId == null)
            return null;
        var bare = Parts(hero).Bare;
        if (bare == null)
            return null;
        var highest = behavior.Service.PrimaryTitleOf(hero.StringId);
        var office = behavior.Service.OfficesOf(hero.StringId).FirstOrDefault(o => (hero.IsFemale ? o.HolderFemale ?? o.HolderMale : o.HolderMale) != null);
        if (office != null && (highest == null || office.Rank >= highest.Rank))
        {
            var style = new TextObject(hero.IsFemale ? office.HolderFemale ?? office.HolderMale : office.HolderMale);
            style.SetTextVariable("NAME", bare);
            return (style, office.Of != null ? new TextObject(office.Of) : null);
        }

        var title = KnownBy(hero, behavior);
        var holder = title != null ? StyledText(title, hero, behavior) : null;
        if (title == null || holder == null)
            return null;
        return (holder, new TextObject(title.Name));
    }

    /// <summary>"{HOLDER} of {PLACE}, {EPITHET}", leaving out the parts the hero or style does not have.</summary>
    private static TextObject Compose(Hero hero, TextObject holder, TextObject? place)
    {
        var epithet = Parts(hero).Epithet;
        var text = new TextObject(place != null
            ? epithet != null ? "{=realms_known_as_epithet}{HOLDER} of {TITLE}, {EPITHET}" : "{=realms_known_as}{HOLDER} of {TITLE}"
            : epithet != null ? "{=realms_known_as_style_epithet}{HOLDER}, {EPITHET}" : "{=realms_known_as_style}{HOLDER}");
        text.SetTextVariable("HOLDER", holder);
        if (place != null)
            text.SetTextVariable("TITLE", place);
        if (epithet != null)
            text.SetTextVariable("EPITHET", epithet);
        return text;
    }

    /// <summary>
    /// The name a holder style is built on and the epithet that follows it: the game's name split at its first ", "
    /// ("Cerdic, the Anvil": "Cerdic" and "the Anvil"), else the whole name with no epithet.
    /// </summary>
    private static (TextObject? Bare, TextObject? Epithet) Parts(Hero hero)
    {
        var name = hero.Name?.ToString();
        var comma = name?.IndexOf(", ", System.StringComparison.Ordinal) ?? -1;
        if (name == null || comma <= 0 || comma + 2 >= name.Length)
            return (hero.Name, null);
        return (new TextObject(name.Substring(0, comma)), new TextObject(name.Substring(comma + 2)));
    }

    private static TextObject? StyledText(TitleDefinition title, Hero hero, RealmsCampaign behavior)
    {
        var name = Parts(hero).Bare;
        if (name == null)
            return null;
        // Styled the way the hero's own people style the holder of a title of this rank.
        var template = behavior.Config.RankNames.HolderTemplate(title, behavior.Service.Catalog, hero.Culture?.StringId, hero.IsFemale);
        if (template == null)
            return null;
        var text = new TextObject(template);
        text.SetTextVariable("NAME", name);
        return text;
    }

    private static Hero? Find(string heroId) =>
        TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager.Find<Hero>(heroId);
}
