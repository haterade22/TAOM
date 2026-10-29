# Oaths of Service

## Overview

Oaths of Service lets a named companion (Gimli, Aragorn and any other entry in
`named_companion_config.json`) set terms for joining the player: a gold price, items, and flags that
other content can grant. The same config can give a companion extra conversation topics and make
them walk back to their home settlement while they are not in the player's party. Every part
is optional per companion; a companion with no block behaves exactly as before.

## Why This Exists

- **Vanilla behavior:** a wanderer is hired through one player line into `companion_hire`, answered by
  one NPC line at priority 100 (`LordConversationsCampaignBehavior`). The price comes from
  `CompanionHiringPriceCalculationModel`, there is no item cost, no way to lock the hire behind a deed,
  and nothing a companion says beyond their backstory lines. Every daily tick
  `CompanionsCampaignBehavior.SwapCompanions` also moves unrecruited wanderers between towns, so a
  named companion drifts away from the settlement he is written into.
- **TAOM requirement:** the lore companions should cost more than a tavern wanderer, can ask for an
  item (a hypothetical example: 2 x Mithril Ore; no such item exists today), can wait for a quest to
  grant a flag, and can answer questions that grow into trust. Data-driven, so a new oath needs no C#.
- **Without this feature:** Gimli costs what any wanderer costs, and an unrecruited companion wanders
  off wherever the world moves them.

## What the player sees

- **Hire terms line.** Talking to a companion with terms and asking to join, the companion says
  "I do not take the road for nothing. My price is 3000{GOLD_ICON}, 2 x Mithril Ore. Will you meet it?"
  (the gold part first, then each item as `count x name`; the Mithril Ore item is the hypothetical
  example above). The player answers "It is agreed. Take it, and ride with me." or "Not yet."
- **Greyed accept.** When gold or items are short, the accept line is shown greyed, with the hint
  "You still need: 500{GOLD_ICON}, 1 x Mithril Ore" listing only what is missing. If the companion
  limit is reached, the vanilla "Thinking again, I already have more companions than I can manage."
  line replaces it.
- **Locked.** If a required flag is not set, the companion answers with their `locked_string_id` line,
  or "Not yet. Prove yourself first, then ask me again." There is no price shown and nothing to accept.
- **Topics submenu.** On a companion who has topics visible right now, the `hero_main_options` menu
  gains "There is something I would ask you." It leads to "Ask, then." and one player line per visible
  topic, plus "Never mind." A topic that cannot be taken shows greyed with "You cannot afford that.",
  "Not so soon after the last time." or, on a co-op client, "Only the host can seal an oath in a shared
  campaign." A topic that takes items shows greyed with "You still need: ..." listing what is short.
  A gold reward shows the engine's usual gold notification. Items a topic gives arrive silently in the
  party inventory: there is no notification.
- **Stay-at-home return.** A companion with `stay_at_home` that is not in the player's party is
  moved back to their home settlement (`home_settlement` when set, else `spawn_settlement`) on the
  first hourly tick after each daily tick, unless the state matrix below skips them.

## Architecture

### Design Challenge

Vanilla's hire is one conversation token with a priority-100 reply, and Wanderer Allegiance already
pre-empts it at 110 for opposed-alignment refusals. The terms have to slot between the two without a
Harmony patch, and a failed hire must not eat the items.

### Solution Approach

Dialog lines on vanilla's `companion_hire` token, services over adapters, one ledger saved with the
campaign, no Harmony. The hire itself runs vanilla's own call order (pay gold, `AddCompanionAction`,
`AddHeroToPartyAction`) inside `NamedCompanionAdapter.HireIntoPlayerParty`. `TryPayAndHire` takes the
items first, then hires:

- If the hire returns false, every item taken is given back.
- If the hire throws, the items are given back only when the companion did not join
  (`IsRecruitedOrInParty` false). A throw in the last step leaves the companion in the party with the
  gold paid, so the items stay taken and the call reports the hire as done. Both cases are logged.

Hire price and gold rules:

- **Gold replaces the vanilla price.** `gold` absent means the vanilla price from
  `CompanionHiringPriceCalculationModel` applies. `gold: 0` together with at least one item means items
  only. `gold: 0` with no items reverts to the vanilla price (a free hire is not a supported term). A set
  value is the whole price, not an addition.
- **Exactly the gold held is enough.** The check is `Gold < price` for missing, and the adapter refuses
  only when `player.Gold < gold`, so a player holding exactly the price can pay. Vanilla needs strictly more.
- **Items count only unmodified stacks.** `GetItemCount` looks up the item with no modifier, so a
  "Fine" or otherwise modified copy does not count, and `RemoveItem` takes only from that stack.
- **Order of checks:** flags first (Locked), then gold, then items (both listed as missing together).
- **No empty price line.** If the terms would list no price and no items, the terms line does not
  answer (a warning is logged once) and vanilla hiring applies.

### Dialog lines

Priority 105 sits under Wanderer Allegiance's refusals (110) and above vanilla's hire reply (100).
The conversation manager sorts NPC lines by priority and takes the first one whose condition is true,
so Wanderer Allegiance's refusal always answers before a 105 line is tried. The Wanderer Allegiance
gate is therefore kept by priority alone: the oath lines do not re-ask it, and
`OathsOfServiceWiringTests.HireDialog_SitsAboveVanillaAndBelowWandererAllegiance_OnCompanionHire` pins
the two priorities. Each NPC line condition runs one `HireTermsService.Evaluate`.

| Id | From token | To | Text or token | Priority | Condition |
|---|---|---|---|---|---|
| `taom_oath_locked` | `companion_hire` | `lord_pretalk` | `{TAOM_OATH_LOCKED}` | 105 | terms exist, status Locked |
| `taom_oath_terms` | `companion_hire` | `taom_oath_hire_response` | `taom_oath_terms`, token `{TAOM_OATH_TERMS}` | 105 | terms exist, status Missing or Met, price or items to list |
| `taom_oath_full` | `taom_oath_hire_response` | `lord_pretalk` | vanilla key `afdN8ZU7` | default | companion limit reached |
| `taom_oath_accept` | `taom_oath_hire_response` | `hero_leave` | `taom_oath_accept`; greyed hint token `{TAOM_OATH_MISSING}` | 100 | limit not reached; clickable only when status Met and writes allowed |
| `taom_oath_decline` | `taom_oath_hire_response` | `lord_pretalk` | `taom_oath_decline` | 1 | always |
| `taom_oath_topics_open` | `hero_main_options` | `taom_oath_topics_npc` | `taom_oath_topics_open` | default | at least one topic visible |
| `taom_oath_topics_npc` | `taom_oath_topics_npc` | `taom_oath_topics` | `taom_oath_topics_npc` | default | always |
| `taom_oath_topic_<character>_<topic>` | `taom_oath_topics` | `taom_oath_topic_reply` | the topic's prompt text, localized when shown | 100 | that companion, topic visible |
| `taom_oath_topics_back` | `taom_oath_topics` | `lord_pretalk` | vanilla key `D33fIGQe` | 1 | always |
| `taom_oath_topic_reply` | `taom_oath_topic_reply` | `lord_pretalk` | `{TAOM_OATH_REPLY}` | default | reply text resolves |
| `taom_oath_topic_reply_fallback` | `taom_oath_topic_reply` | `lord_pretalk` | `taom_oath_reply_fallback` ("Another time, perhaps.") | 1 | always |

The fallback exists because the conversation manager has no exit when no NPC line matches the token:
without it a reply that cannot be built would stall the conversation.

A state failure is swallowed to "no terms", so vanilla hiring applies and an error is logged.

### Backstory intro

The first time the player speaks to a configured, enabled named companion they have not recruited, the
companion tells their story from the seven `<part>.<character_id>` strings in
`named_companions/named_companion_strings.xml` (`prebackstory`, `backstory_a` to `backstory_d`,
`response_1`, `response_2`). TAOM marks every companion as met at placement, which closes vanilla's
first-meeting chain (and that chain would throw on a null `Hero.Template`), so `OathIntroDialog` (#1061)
carries the story itself. It needs no config entry: any companion in `named_companion_config.json` with
`enabled` true gets it.

| Line | Input token | Output token | Speaker and text | Priority |
|---|---|---|---|---|
| `taom_nc_intro_a` | `lord_start` | `taom_nc_intro_pre` | NPC, `backstory_a` | 110 |
| `taom_nc_intro_pre` | `taom_nc_intro_pre` | `taom_nc_intro_b` | player, `prebackstory` | default |
| `taom_nc_intro_b` | `taom_nc_intro_b` | `taom_nc_intro_c` | NPC, `backstory_b` | default |
| `taom_nc_intro_c` | `taom_nc_intro_c` | `taom_nc_intro_reaction` | NPC, `backstory_c` | default |
| `taom_nc_intro_response_1`, `_2` | `taom_nc_intro_reaction` | `taom_nc_intro_d` | player, `response_1` or `response_2` | default |
| `taom_nc_intro_d` | `taom_nc_intro_d` | `hero_main_options` | NPC, `backstory_d` | default |

- **Entry:** vanilla's greeting (`start_default` or `start_default_under_24_hours`) always plays first and
  hands over to `lord_start`. There vanilla's replies (`lord_ask` to `hero_ask_4`, "So, then. What is
  it?") sit at the default priority 100, so the intro at 110 follows the greeting. The chain ends on
  `hero_main_options`, the normal menu.
- **Condition:** the conversation hero exists and is not a prisoner, `OathTopicService.IntroDue` is true
  (configured and enabled, not recruited by the same `Hero.Clan != null` test the topics use, not a co-op
  client, not shown before, and the Wanderer Allegiance verdict (#575) allows the hire), and all seven strings
  resolve for that character id. Otherwise the line does not match and vanilla's `lord_start` reply
  plays. The whole condition is in a try/catch that logs once.
- **Recorded:** when the opening line is spoken, under the ledger topic id `intro.backstory` in the same
  saved `_taom_oath_topicDays` dictionary as topics. The dot cannot appear in a config topic id
  (`^[a-z0-9_]{1,48}$`), so the record never hides or alters a real topic.
- **Unrecruited only:** a recruited companion never gets it.
- **Only when the hire is allowed:** every backstory ends with an offer to join, so a companion the
  Wanderer Allegiance gate would refuse (a Free companion to an Evil player) keeps it.
- **Co-op client:** skipped, and nothing is recorded (`CoopSessionPolicy.MayWriteSaveBackedState`).
- **Old saves:** the ledger has no `intro.backstory` record, so each unrecruited companion tells their
  story once on the first talk after the update. A companion who served the player before the update
  and was dismissed counts as unrecruited again (`RemoveCompanionAction` clears only `CompanionOf`),
  so they tell it once too; nothing in the engine records that a hero served before.

### Topics

A topic is visible when its `when` matches (recruited means the hero has a clan: `Hero.Clan` is `CompanionOf` or an own clan, so a companion the player promoted to lord counts as recruited, the same rule the daily anchor uses), it
is not `once` and used, every `requires_flags` entry is set, and the player's relation with the
companion is between `min_relation` and `max_relation` (both inclusive). A topic with `random_chance`
below 100 is also offered only when its daily roll passes: `OathDailyRoll.Percent(character_id, topic_id,
day)` is FNV-1a 32-bit over the UTF-8 bytes of `character|topic|day` (invariant culture), modulo 100, and
the topic shows when the roll is below the chance. Same inputs give the same roll, so the offer is stable
for the whole day (no flicker between renders, no re-roll by reopening the conversation) and differs from
day to day. `string.GetHashCode` is not used: it is not stable across runtimes.

`Blocker` checks, in order: co-op client, unknown topic, cooldown, purse, items (the party lacks some
`take_items`, counting unmodified stacks as the hire does). Choosing a topic applies, in order: a blocker
re-check; take the `take_items` (if one falls short, give back what was taken and return false); charge
gold (if the purse refuses, give the taken items back and return false); give the `give_items`; gold
reward to the main hero (through `ICareerPurseAdapter.GrantGold`, which shows the engine's gold
notification); relation change (signed, so a negative `relation_gain` lowers it); `sets_flags`; then the
day is recorded. Given items go straight into the party inventory without a notification. `once` means
"has a recorded day": `Complete` records the day every time, so one map serves both `once` and
`cooldown_days`. `Complete` returns false with nothing changed when the topic is blocked (co-op client,
cooldown still running, purse short, items missing).

Two engine effects shape the numbers a designer writes:

- **`min_relation` and `max_relation` read the effective relation,** which adds up to +-4 from
  personality traits (Honor +-2, Valor +-1, Mercy +-1; `DefaultDiplomacyModel.GetPersonalityEffects`).
  A `min_relation` of 5 can be met at a base relation of 1 by a character the companion likes.
- **A positive `relation_gain` is a base value.** `ChangeRelationAction` passes it through
  `GetEffectiveRelationChange`, which scales a result above zero by the player's Charm and Honor, so
  the relation that lands can differ from the number in the config. A negative `relation_gain` lands as
  written.

### Home anchor timing and state matrix

`CompanionsCampaignBehavior.SwapCompanions` is the force the anchor counters: it runs on the same
`DailyTickEvent` and moves a random unrecruited wanderer to another town. Listeners run newest first,
so TAOM's daily listener runs before SwapCompanions and a move made there would be undone the same day.
`OathsOfServiceCampaignBehavior` therefore only raises a flag on the daily tick, and the next
`HourlyTickEvent` runs `HomeAnchorService.AnchorAll` once and clears the flag. A companion swapped away
is back home within at most one hour of game time. The flag is not saved; a save made between the two
ticks loses that one run, and the next daily tick raises it again.

`AnchorAll` runs only on the co-op authority. For each enabled companion with `stay_at_home` and a
home (`home_settlement`, else `spawn_settlement`), it reads the current settlement first and moves them
home unless:

| State | Result |
|---|---|
| Already in the home settlement | skipped (checked first, nothing else is read) |
| Hero missing, dead, prisoner, fugitive, or otherwise not Active | skipped (one `HeroState` field, so `IsHeroActive` covers all of them; the adapter finds living heroes only) |
| Recruited (`CompanionOf` set) or in any party, including a captor party | skipped |
| Promoted to lord (own clan, `CompanionOf` null) | skipped (`IsRecruitedOrInParty` also tests `Hero.Clan`) |
| Player is in the hero's current settlement | skipped (the player could be watching) |
| Player is in the home settlement | skipped (same reason) |
| Otherwise | `ReturnHome`: leaves the current settlement if any, then enters home |

Each companion is handled in its own try, so a throw in a read or the move is logged and skips only
that companion. `Hero.Clan` is `CompanionOf ?? _clan`, and an unrecruited named companion has no clan
because `heroes.xml` uses `Faction.neutral`, which `Hero.Deserialize` skips.

### Culling

Vanilla's `TryKillCompanion` picks a random template from `_aliveCompanionTemplates` and kills the
living wanderer whose `Hero.Template` matches. A named companion is an XML character with no original
character, so `Hero.Template` is null and `IsTemplateKnown` rejects it: vanilla never picks one. The
oath config needs no extra protection against culling.

### Component Diagram

```
named_companion_config.json
        |
NamedCompanionConfigProvider --(parse, then OathConfigValidator.Validate)
        |
  HireTermsService   OathTopicService   HomeAnchorService     OathLedger (IOathFlagStore)
        \                 |                  /                      |
   NamedCompanionAdapter, PartyItemRosterAdapter, CareerPurseAdapter, ArmourItemCatalogAdapter
        |
OathsOfServiceDialogBehavior (OathHireDialog, OathTopicDialog, OathIntroDialog)   OathsOfServiceCampaignBehavior
```

## Configuration

### Config File: `Main/_Module/ModuleData/named_companions/named_companion_config.json`

Each companion entry gains the optional members below. `stay_at_home` sits on the entry itself.
`OathConfigValidator` runs once at load and mutates in place: a bad value reverts to its default or
the entry is skipped, each with a warning, then one summary warning. The provider reads the file in
two steps. A syntax error anywhere fails the whole load at error level (no companions). A wrong-typed
member is skipped with a warning without emptying the rest of the list, and one summary warning counts
the skipped members. A wrong-typed `enabled` disables that entry, so a companion whose switch cannot be
read fails closed.

| Field | Type | Default | Validation |
|---|---|---|---|
| `home_settlement` | string | absent (the spawn settlement) | must be a settlement of the loaded game, else cleared with a warning (spawn applies) |
| `stay_at_home` | bool | `false` | none |
| `hire_terms` | object | absent (vanilla hire) | dropped (vanilla hire) when it sets no gold, no items and no flags |
| `hire_terms.gold` | int | absent (vanilla price) | 0..1000000, else reverts to absent; 0 with no items also reverts (0 means items only) |
| `hire_terms.items` | list | empty | at most 6 entries; extras dropped |
| `hire_terms.items[].item_id` | string | none | non-empty, unique in the list; one id that does not exist in the loaded game drops the whole `hire_terms` block (vanilla hire applies) |
| `hire_terms.items[].count` | int | 1 | 1..999, else the entry is skipped |
| `hire_terms.requires_flags` | list of string | empty | each `^[a-z0-9_.]{1,64}$` after lower-casing, else skipped |
| `hire_terms.locked_string_id` | string | none | must resolve to a text id, else cleared (generic line applies) |
| `topics` | list | empty | see below |
| `topics[].id` | string | none | `^[a-z0-9_]{1,48}$`, unique across the whole file, else the topic is skipped |
| `topics[].prompt_string_id` | string | none | required, must resolve, else the topic is skipped |
| `topics[].reply_string_id` | string | none | required, must resolve, else the topic is skipped |
| `topics[].when` | string | `any` | `any`, `recruited` or `unrecruited` (case-insensitive), else the topic is skipped |
| `topics[].min_relation` | int | -100 | -100..100, else -100 |
| `topics[].max_relation` | int | 100 | -100..100, else 100; checked after that revert, a topic whose `min_relation` is above `max_relation` is skipped |
| `topics[].random_chance` | int | 100 | 1..100, else 100; a chance alone is not a state change (no `once` needed) |
| `topics[].give_items` | list | empty | same entry rules as `hire_terms.items` (at most 6, unique ids, empty id skipped); an id missing from the loaded game skips the whole topic |
| `topics[].give_items[].item_id`, `.count` | string, int | none, 1 | non-empty id; count 1..999, else the entry is skipped |
| `topics[].take_items` | list | empty | same rules as `give_items`; the topic is blocked while the party lacks them |
| `topics[].take_items[].item_id`, `.count` | string, int | none, 1 | same as `give_items` |
| `topics[].requires_flags` | list of string | empty | flag-name rule above |
| `topics[].sets_flags` | list of string | empty | flag-name rule above |
| `topics[].relation_gain` | int | 0 | -10..10, else 0; a negative value lowers the relation |
| `topics[].gold_cost` | int | 0 | 0..100000, else 0 |
| `topics[].gold_reward` | int | 0 | 0..10000, else 0 |
| `topics[].once` | bool | `false` | none |
| `topics[].cooldown_days` | int | 0 | 0..365, else 0 |

Text ids (`locked_string_id`, `prompt_string_id`, `reply_string_id`) must not contain a dot: the
engine splits a GameText id at the first dot, so such an id never resolves.
`NamedCompanionShippedDataTests` checks every shipped id against the string rows.

Cross-field rules: a topic with both `gold_cost` and `gold_reward` above 0 is skipped; a topic that
charges, pays, moves items (any `give_items` or `take_items` entry) or changes relation (either sign)
needs `once` or `cooldown_days` of at least 1, else it is skipped; a topic whose `min_relation` is above
its `max_relation` is skipped. A hire-terms item that does not exist (which drops the whole terms block),
a topic item that does not exist (which skips the whole topic), a topic whose prompt or reply text id is
unknown and a `home_settlement` that does not exist (cleared, the spawn settlement applies) are dropped
when the session launches (the game data is loaded by then), each with a warning. `NamedCompanionService`
(spawn on a new game, re-place on load) keeps using `spawn_settlement`, so a companion whose home differs
walks home on the first anchor run after a load. The id resolution runs in `OathsOfServiceDialogBehavior` just before the dialogs
register, because both dialogs read the resolved config.

### Current Values

| Companion | Settlement | Gold | Topics |
|---|---|---|---|
| `named_companion_gimli` | `town_E1` | 50000 | `gimli_first` (open to all, once, +2, sets `gimli.erebor_told`); `gimli_second` (relation 5+, needs that flag, once, +3, sets `gimli.erebor_trusted`) |
| `named_companion_aragorn` | `town_AN3` (Bree) | 75000 | `aragorn_first` (sets `aragorn.dunedain_told`); `aragorn_second` (needs it, sets `aragorn.dunedain_trusted`), same relation and once values |

Both set `stay_at_home: true`. Prompt and reply texts are in `named_companion_strings.xml`
(`taom_oath_<name>_<first|second>_<prompt|reply>`); the shared lines are in `taom_module_strings.xml`.

### Reload scope

The config provider caches its list for the process. A change to the JSON needs a full game restart,
not a load of another save.

### Turning it off

There is no MCM toggle. Remove a companion's `hire_terms`, `topics` and `stay_at_home` to turn the
feature off for that companion.

## Flag API for quests

`IOathFlagStore` (registered as a mapping onto `OathLedger`):

- `bool Has(string flag)`: true when set. The name is lower-cased; an invalid name is never set.
- `bool Set(string flag)`: sets it. Returns false when the name is not `^[a-z0-9_.]{1,64}$` after
  lower-casing, or this peer is a co-op client. Setting twice returns true.

Flags are global to the campaign, not per companion, and gate both hire terms and topics.

## Save data

`OathsOfServiceCampaignBehavior.SyncData` writes a `List<string>` and a `Dictionary<string, int>`, both
basic saveable containers the save system defines (`SaveableBasicTypeDefiner`):

| Key | Content |
|---|---|
| `_taom_oath_flagList` | the set flags |
| `_taom_oath_topicDays` | `character_id\|topic_id` to the day the topic was last used |

The ledger is a process singleton, so the behavior's constructor calls `ResetForNewSession()`. The
module runner builds the behavior once per campaign start, before any event and before a load's
`SyncData`, so a second campaign in the same process never inherits the first one's flags. A load
then imports over the reset; invalid saved flag names are dropped on import. The module owns save
data, so a fault fails closed. Tests that pin this:
`OathsOfServiceWiringTests.CampaignBehavior_ConstructorResetsTheLedger` (the constructor body),
`OathsOfServiceWiringTests.Module_BuildsAFreshBehaviorPerCampaign_OverOneSharedLedger` (a fresh behavior
per `Create`, one shared ledger, a flag set before `Create` gone after it) and
`OathsOfServiceCampaignBehaviorTests.SaveThenLoad_IntoAFreshBehavior_RestoresFlagsAndTopicDays` (the
round trip through a dictionary-backed store into a fresh behavior).

## Co-op

- A co-op client may not write: `TryPayAndHire`, `OathLedger.Set`, topic completion and the ledger
  marks all refuse through `CoopSessionPolicy.MayWriteSaveBackedState`. The accept line and topic
  lines show the greyed co-op hint.
- The home anchor runs on the authority only (`ICoopSessionProvider.IsAuthority`).
- BannerlordTogether: `IsAuthority` fails open there, so every peer may run the anchor. UNVERIFIED
  whether that mod's client-side suppression contains it. See [coop-interop.md](coop-interop.md).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/NamedCompanions/Oaths/HireTermsService.cs` | Evaluate terms, pay and hire with refund |
| `Main/Features/NamedCompanions/Oaths/HireEvaluation.cs` | Hire status, the evaluation result, missing terms |
| `Main/Features/NamedCompanions/Oaths/OathTopicService.cs` | Topic visibility, blockers, effects |
| `Main/Features/NamedCompanions/Oaths/OathDailyRoll.cs` | The deterministic daily roll behind `random_chance` |
| `Main/Features/NamedCompanions/Oaths/HomeAnchorService.cs` | Return home |
| `Main/Features/NamedCompanions/Oaths/OathLedger.cs` | Flags and topic days, the save shape |
| `Main/Features/NamedCompanions/Oaths/IOathFlagStore.cs`, `OathFlagName.cs` | Flag API and naming rule |
| `Main/Features/NamedCompanions/Oaths/OathConfigValidator.cs` | Load-time validation and id resolution |
| `Main/Features/NamedCompanions/Oaths/OathsOfServiceModule.cs` | Feature module, registrations, behaviors |
| `Main/Features/NamedCompanions/Oaths/Hooks/` | Dialog and campaign behaviors, text rendering |
| `Main/Features/NamedCompanions/Domain/OathDefinitions.cs` | Config shapes |
| `Main/Adapters/NamedCompanionAdapter.cs` | Hire price, hire, return home, relation, state reads |
| `Main/Adapters/CareerPurseAdapter.cs` | Charge and grant gold |
| `Main/_Module/ModuleData/named_companions/named_companion_config.json` | Per-companion oaths |

## Dependencies

- `INamedCompanionConfigProvider`, `INamedCompanionAdapter`: config and hero state.
- `IPartyItemRosterAdapter`, `ICareerPurseAdapter`, `IItemPoolAdapter`, `IArmourItemCatalogAdapter`: items, gold, id checks, item names.
- `ICoopSessionProvider`, `CoopSessionPolicy`: write and authority gates.

## Tests

`TAOM.Tests/Features/NamedCompanions/Oaths/`: `HireTermsServiceTests`, `HomeAnchorServiceTests`,
`OathConfigValidatorTests` (including the shipped-config check), `OathDailyRollTests`, `OathLedgerSessionResetTests`,
`OathLedgerTests`, `OathTextRendererTests`, `OathTopicServiceTests`, `OathsOfServiceCampaignBehaviorTests`,
`OathsOfServiceWiringTests`. In `TAOM.Tests/Features/NamedCompanions/`: `NamedCompanionConfigProviderTests`
(parse failures, the summary warning, `enabled` failing closed) and `NamedCompanionShippedDataTests`
(text ids against the string rows, spawn and home settlements against the live `TAOM_Map`, tagged `LiveInstall`).

## How to give another companion an oath

1. Add `hire_terms`, `topics` and optionally `stay_at_home` to the companion's entry in
   `named_companion_config.json`.
2. Add the prompt and reply strings to `named_companion_strings.xml` as `{=KEY}default` (no dot in the
   key), then register, translate and validate them ([TRANSLATOR_GUIDE.md](../localization/TRANSLATOR_GUIDE.md)).
3. Restart the game. Check the log for `NamedCompanion oaths` warnings.

## Known limits

- `{GOLD_ICON}` inside the terms line is built into a text variable. Verified in code: vanilla's
  `LordConversationsCampaignBehavior` sets the global text variable `GOLD_ICON` when the session launches
  (v1.5.5 decompile, `LordConversationsCampaignBehavior.cs` line 199), and
  `MBTextManager.SetTextVariable(string, string)` wraps the value in a `TextObject`, so the nested token
  resolves when the line is shown, as vanilla's `HIRING_COST_EXPLANATION` does. The visual check is owed in game.
- No MCM toggle, no per-save override.

## In-game checklist

1. Gimli: ask to join. The terms line shows 50000 plus the icon; with less gold the accept is greyed with "You still need".
2. Hold exactly 50000 gold: accept works, and the purse ends at 0.
3. Aragorn (in Bree): same at 75000.
4. With a Wanderer Allegiance refusal active for the pair (for example an Evil-aligned player and Aragorn), the refusal line still wins.
5. Topics: for an unrecruited and a recruited companion, the submenu appears, a `once` topic disappears after use, and the second topic appears only after the first. A gold reward shows the gold notification.
6. Leave Gimli unrecruited away from the player, advance a day: he returns to `town_E1` within the next hour unless you are in either settlement. Vanilla can swap a companion out while the player stays in the home town; the anchor skips that case on purpose, so he comes back once the player leaves.

7. Only unit tests cover these paths, because no shipped companion uses them (the samples are gold only):
   item terms on a topic (`give_items`, `take_items`, the greyed "You still need" hint, the rollback when
   a step fails), item terms on the hire (the greyed accept with an item, and the refund on a failed
   hire), `random_chance` (the daily roll and its stability within a day), `max_relation`, a
   negative `relation_gain`, and a `home_settlement` that differs from the spawn settlement. Add one to a
   test entry and check it in game before relying on it.

## Changelog

- 2026-10-29: the backstory intro (#1061): an unrecruited named companion tells their seven-string story once on the first talk.
- 2026-10-19: initial feature (#904).
- 2026-10-19: deep-review fixes. Aragorn moves to Bree (`town_AN3`). The anchor runs on the first hourly tick after the daily tick (SwapCompanions ran after it). A promoted companion (own clan) is never moved. An unknown hire item drops the whole terms block; `gold: 0` without items reverts to the vanilla price. A throw after the companion joined keeps the items. Gold rewards show the engine notification. Topic prompts localize when shown. A priority-1 fallback follows the reply line. The ledger saves a list and a dictionary under new keys. A syntax error in the config logs at error level; an unreadable `enabled` disables the entry.

- 2026-10-19: topics gain `max_relation`, `random_chance`, `give_items`, `take_items` and a signed `relation_gain` (-10..10); companions gain `home_settlement` (#904).

## GitHub Issue

- **Issue:** #904, Oaths of Service
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/named-companions.md](./named-companions.md)
- [docs/features/wanderer-allegiance.md](./wanderer-allegiance.md)
- [docs/modding/wanderers-and-named-companions.md](../modding/wanderers-and-named-companions.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
