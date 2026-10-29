using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.WandererAllegiance;

namespace TAOM.Features.NamedCompanions.Oaths.Hooks;

/// <summary>
/// The backstory intro (#1061): the first time the player speaks to an unrecruited named companion, they tell
/// their story from the seven <c>&lt;part&gt;.&lt;character_id&gt;</c> strings in named_companion_strings.xml. TAOM
/// marks companions as met at placement, which closes vanilla's first-meeting chain, so this chain replaces it.
/// Vanilla's greeting (<c>start_default</c> or <c>start_default_under_24_hours</c>) plays first and hands over to
/// <c>lord_start</c>; the opening line sits there above the default priority 100 of vanilla's <c>lord_start</c>
/// replies (<c>lord_ask</c> to <c>hero_ask_4</c>), and the chain ends on <c>hero_main_options</c>. A companion the
/// Wanderer Allegiance gate (#575) would refuse keeps the story, which ends with an offer to join. The shown mark is a ledger record under <see cref="OathTopicService.IntroTopicId"/>.
/// </summary>
internal sealed class OathIntroDialog
{
    private const int Priority = 110;

    private static readonly string[][] Parts =
    {
        new[] { "prebackstory", "TAOM_NC_PREBACKSTORY" },
        new[] { "backstory_a", "TAOM_NC_BACKSTORY_A" },
        new[] { "backstory_b", "TAOM_NC_BACKSTORY_B" },
        new[] { "backstory_c", "TAOM_NC_BACKSTORY_C" },
        new[] { "backstory_d", "TAOM_NC_BACKSTORY_D" },
        new[] { "response_1", "TAOM_NC_RESPONSE_1" },
        new[] { "response_2", "TAOM_NC_RESPONSE_2" },
    };

    private readonly OathTopicService _topics;
    private readonly ICoopSessionProvider _coop;
    private readonly WandererAllegianceService _allegiance;
    private readonly IModLogger _logger;
    private bool _conditionFailureLogged;

    public OathIntroDialog(OathTopicService topics, ICoopSessionProvider coop, WandererAllegianceService allegiance,
        IModLogger logger)
    {
        _topics = topics;
        _coop = coop;
        _allegiance = allegiance;
        _logger = logger;
    }

    public void Register(CampaignGameStarter starter)
    {
        starter.AddDialogLine("taom_nc_intro_a", "lord_start", "taom_nc_intro_pre", "{=!}{TAOM_NC_BACKSTORY_A}",
            IntroCondition, RecordShown, Priority);
        starter.AddPlayerLine("taom_nc_intro_pre", "taom_nc_intro_pre", "taom_nc_intro_b", "{=!}{TAOM_NC_PREBACKSTORY}", null, null);
        starter.AddDialogLine("taom_nc_intro_b", "taom_nc_intro_b", "taom_nc_intro_c", "{=!}{TAOM_NC_BACKSTORY_B}", null, null);
        starter.AddDialogLine("taom_nc_intro_c", "taom_nc_intro_c", "taom_nc_intro_reaction", "{=!}{TAOM_NC_BACKSTORY_C}", null, null);
        starter.AddPlayerLine("taom_nc_intro_response_1", "taom_nc_intro_reaction", "taom_nc_intro_d", "{=!}{TAOM_NC_RESPONSE_1}", null, null);
        starter.AddPlayerLine("taom_nc_intro_response_2", "taom_nc_intro_reaction", "taom_nc_intro_d", "{=!}{TAOM_NC_RESPONSE_2}", null, null);
        starter.AddDialogLine("taom_nc_intro_d", "taom_nc_intro_d", "hero_main_options", "{=!}{TAOM_NC_BACKSTORY_D}", null, null);
    }

    // ConversationSentence.RunCondition has no catch, so nothing may escape this method.
    private bool IntroCondition()
    {
        try
        {
            var hero = Hero.OneToOneConversationHero;
            if (hero == null || hero.IsPrisoner) return false;
            // Hero.Clan is CompanionOf ?? own clan, so a companion promoted to lord counts as recruited, as in OathTopicDialog.
            // The same verdict the hire dialogue asks (WandererAllegianceDialogBehavior.Verdict), read live:
            // Player Switcher can change the player clan mid-session.
            var hireAllowed = _allegiance.Evaluate(hero.StringId, hero.Culture?.StringId, FactionSides.OfPlayer())
                == WandererHireVerdict.Allowed;
            if (!_topics.IntroDue(hero.StringId, hero.Clan != null, _coop.IsCoopClient, hireAllowed)) return false;

            // Look all seven up first: a half-set chain would leave the player on a line with no text.
            var texts = new TextObject[Parts.Length];
            for (var i = 0; i < Parts.Length; i++)
                if (!GameTexts.TryGetText(Parts[i][0], out texts[i], hero.StringId)) return false;

            for (var i = 0; i < Parts.Length; i++)
                MBTextManager.SetTextVariable(Parts[i][1], texts[i]);
            return true;
        }
        catch (Exception ex)
        {
            if (!_conditionFailureLogged)
            {
                _conditionFailureLogged = true;
                _logger.LogError($"[Oaths] intro condition failed: {ex}");
            }
            return false;
        }
    }

    private void RecordShown()
    {
        try
        {
            var hero = Hero.OneToOneConversationHero;
            if (hero != null) _topics.RecordIntro(hero.StringId, (int)CampaignTime.Now.ToDays, _coop.IsCoopClient);
        }
        catch (Exception ex) { _logger.LogError($"[Oaths] intro consequence failed: {ex}"); }
    }
}
