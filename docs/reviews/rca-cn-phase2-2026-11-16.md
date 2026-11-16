# RCA: the Chinese team's data and prefab gaps (#1385 Phase 2)

Findings from the review of #1385 Phase 2: the keyed prefab labels and `FrontEndEditableTextWidget`, the
siege defense texts under `taom_siege_defense_<kingdom>_<field>`, the special resource names and tiers
under `taom_res_*`, the keys on `taom_wotr_strings.xml`, the team's Chinese for 114 reworded vanilla keys,
and the CNs and CNt rows and caches. The change adds an optional constructor parameter that DryIoc fills
and changes a value a co-op reply carries, so it ran as tier 1: the data flow, engine compatibility and
XML lenses at `max`. Codex did not run: it has had no usage since 2026-11-15, and the maintainer said to
skip it. No lens reported a CRITICAL or HIGH, so no second wave ran. Each row was read again against the
files before it was fixed.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | XML F1: 19 of the team's `wording` rows (11 CNs, 8 CNt) were byte for byte SandBox's own row for that key. Each replaced vanilla's text with the same text, would hide a later TaleWorlds fix to it, and made the `wording` reason untrue | Redundant data | `cn_vanilla_rewords.py` compared the team's text with our English and the placeholder rule, never with vanilla's Chinese row | The 19 rows, their `kept-vanilla-key-rows.txt` lines and their cache entries are deleted; `VanillaKeyOverrideTests.KeptRows_NoneRepeatsVanillasOwnRow` reads vanilla's rows of each kept language and fails on a repeat |
| 2 | LOW | Data flow F1: a co-op guest's emissary result toast named the resource in the host's language. The host sends `ResourceDisplayName` in `CoopCompatTradeReply.ResourceName`, and the name is now localized on each machine. MED if a dedicated server runs in English (UNVERIFIED) | Localized value sent over the network | Before this change both machines held the same English name. The brief asked whether the text crosses the network; the builder did not trace the reply | The guest shows its own name and uses the host's only as the paid-in-gold signal (`EliteEmissaryInquiryPresenter.GuestResultResourceName`); `EliteEmissaryGuestResultNameTests` |
| 3 | LOW | Engine compatibility F1, data flow F4: a comment in `SiegeDefenseService` said an unset variable shows the engine's "variable has not been set" text. That text is for dotted names only (`TextProcessingContext.cs:81`); a plain unset name shows nothing or a global of that name, and vanilla sets SETTLEMENT and ATTACKER | Engine claim not read from the source | The comment was written from the error text's name | The comment now states the engine's lookup: the object's own variables (case sensitive), then the globals (case insensitive), then "" |
| 4 | LOW | Engine compatibility F2: the `GameTextRowsAreKeyedTests` summary said a language row under an unkeyed id never applies. `TryGetText` keys the copy, but keeps the text cached at load, so the row applies after a language change in game | Engine claim not read from the source | Same as row 3 | The summary now says the player sees English until the language changes in game |
| 5 | LOW | Engine compatibility F3, data flow F2: resource names and tier texts resolve once, at first load, so after a language change in a running game they stay in the old language until restart | Cache with no language check | The doc said "resolves them once" and did not name the cost | `special-resources.md` names the limit ("until the game restarts"). A reload on language change was not added: the first load happens in the campaign, and the switch mid-game is rare |
| 6 | LOW | XML F2: the 7 prefab rows went in as one block, where their neighbours alternate a row and a blank line | Byte shape | `cn_prefab_rows.py` inserted the rows together | A blank line after each row. No gate: the shape has no effect on the engine, and TRANSLATOR_GUIDE "Line endings and encoding" states the rule |

## Not applied

- Data flow F5: `SiegeDefenseService` passes the settlement id, not its name, as `{settlement}` to the reward message. No English or Chinese reward row uses `{settlement}`, `siege-defense.md` limits the reward message to `{influence}` and `{relation}`, and the importer rejects an added token. FOLLOW-UP no issue: latent, and it predates this change.
- Data flow F3: `SpecialResourceEarningService.cs:264` and `SpecialResourceToasts.cs:22` build English toasts around the now localized names, so a Chinese player sees a Chinese name in an English line. The literals predate this change; Phase 3 of #1385 keys them.

## Why each agent missed these

- Rows 1 and 2 needed a comparison outside the change: vanilla's own Chinese rows, and the other machine in a co-op trade. The builder checked the team's text against our English and traced each name to its readers on one machine.
- Rows 3 and 4 were engine claims written from memory. The engine compatibility lens read the source for each.

## Feedback memories to codify

None. The lesson goes to `docs/reviews/lessons/localization-ui.md`.
