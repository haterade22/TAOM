# Party-template sizing: what `max_value` actually controls

> Written 2026-08-14 during the evil-culture balance pass. Every engine claim below was read out of
> the v1.4.8 decompile under `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\` and is
> cited with a file and line. Read this before retuning `Main/_Module/ModuleData/taom_partyTemplates.xml`,
> because the obvious reading of `max_value` is wrong and the numbers below will be retuned again
> after the in-game smoke test.

**The short version:** a template's max sum is the ceiling on the roster a party is handed

> **Bandit and caravan templates are retuned by a different tool.** They sit outside this tool's `kingdom_hero_party_*` scope and are solved for a troop-POWER budget rather than a headcount sum, because the AI compares `EstimatedStrength` and the raider cultures differ by tier: `python tools/rebalance_template_power.py`. See [caravan-bandit-parity.md](../features/caravan-bandit-parity.md).
**at spawn**, not the size that party settles at. Steady state belongs to `PartySizeLimit`, which is
a completely separate model, and the two have never been reconciled.

## What the engine reads out of a template

`PartyTemplateObject.Deserialize` reads exactly two integers per stack, `min_value` and `max_value`
(`TaleWorlds.CampaignSystem.Party/PartyTemplateObject.cs:39`), plus the troop id. There is no
per-template "size" field.

The class exposes two sums:

| Method | Line | Returns |
|---|---|---|
| `GetUpperTroopLimit()` | `PartyTemplateObject.cs:62-70` | plain `sum(stack.MaxValue)` |
| `GetLowerTroopLimit()` | `PartyTemplateObject.cs:72-80` | plain `sum(stack.MinValue)` |

**Neither has a single caller anywhere in the Campaign assembly.** A recursive grep of
`E:\Decompiled_Bannerlord\Campaign\` finds only the two definitions. They are informational.

> The only shipping code that does call them is the NavalDLC module, and only for parties TAOM does
> not author. Three call sites, all in the decompiled aggregate `_modules_build/NavalDLC__NavalDLC.cs`:
> its `PartySizeLimitModel` decorator returns `DefaultPartyTemplate.GetUpperTroopLimit()` as the size
> limit for a **bandit clan whose party carries naval capability** (`:102720`) and
> `SettlementPatrolPartyTemplateNaval.GetUpperTroopLimit()` for a **naval patrol** (`:102732`); the
> Act 3 storyline boss corsair is filled to its own template's upper limit (`:13901`). Kingdom lord
> parties fall through to the base model. If TAOM ever ships naval bandit clans, the max sum stops
> being informational for them and becomes a literal party-size cap.

## The spawn formula

`DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty`
(`TaleWorlds.CampaignSystem.GameComponents/DefaultPartySizeLimitModel.cs:427-464`) builds the roster
a party receives when it is created. Per stack, at `:442`:

```
count = RoundRandomized(min + (max - min) * r)
```

One ratio `r` is drawn for the whole party, then applied to every stack. Callers:
`MobileParty.cs:2645` (the general path) and `PatrolPartiesCampaignBehavior.cs:113`.

### Where `r` comes from

`GetInitialPartySizeRatioForMobileParty` (`DefaultPartySizeLimitModel.cs:390-413`) branches:

| Party kind | Ratio |
|---|---|
| Bandit with ship hulls | one of two bands, `[0, 0.33]` or `[0.66, 1]`, 40/60 split (`:395-398`) |
| Bandit, land | `(0.4 + 0.8 * PlayerProgress) * RandomFloatRanged(0.2, 0.8)` (`:399-402`) |
| Player-owned caravan | `1f` (`:404-407`) |
| Patrol party | `1f` (`:408-411`) |
| **Everything else, including every kingdom lord party** | `party.RandomFloat()` (`:412`) |

`RandomFloat()` on a party is `RandomValue / 2.1474836e9` (`RandomOwnerExtensions.cs:53-56`), and
`PartyBase.RandomValue` is `MBRandom.RandomInt(1, int.MaxValue)` assigned once at construction
(`PartyBase.cs:253`). So for a lord party the ratio is approximately uniform on `(0, 1)`, fixed for
that party's lifetime, and **completely independent of the template**.

**Consequence:** the expected spawn roster is the midpoint of the min sum and the max sum, and
raising the max sum raises it linearly. Mordor's culture template carries 52 stacks summing to
min 47 (39 when this was written; commit `2fcbef10` added 13 Black Numenorean stacks and re-ran the
tool, which re-absorbed them while holding the 3500 target). At the old max sum of 1446 the expected spawn roster was about 747. At the new 3500 it is
about 1773.

## `PartySizeLimit` is the steady-state cap, and it is a different model

Nothing above caps the party. Recruitment, army joins and the party's long-run size are governed by
`PartySizeLimit`, which `PartyBase` caches off `PartySizeLimitModel.GetPartyMemberSizeLimit`
(`PartyBase.cs:343-355`, model call at `:351`).

A party spawned above that limit is not merely held there, it is actively drained.
`DesertionCampaignBehavior` runs on the daily party tick for every lord party
(`DesertionCampaignBehavior.cs:12`, `:19-25`), and
`DefaultPartyDesertionModel.GetTroopsToDesertDueToWageAndPartySize` (`:50-76`) sheds at least
`max(1, overflow / 4)` men, where `overflow` is the roster minus whoever is already deserting for
morale minus `PartySizeLimit`. Unpaid wages can push the count higher, but the size term alone is a
flat quarter of the excess per day and carries no morale condition, so the overflow decays
geometrically: 1,500 men over the limit is under 100 after ten campaign days. The over-limit morale
penalty (`DefaultPartyMoraleModel.GetPartySizeMoraleEffect:132-142`, `-sqrt(overflow)`) drives a
second, morale-based desertion on top of that.

TAOM overrides that model with **`TaomPartySizeModel`** (`Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs`,
registered at `Main/SubModule.cs:827`). It layers three things onto the same `ExplainedNumber`:

1. **Culture party-size feats** via `ICulturalFeatsService.ApplyPartySizeFeats` (factor-based, `AddFactor`).
2. **Career passives** via `ICareerPassiveService.ApplyFlat` (flat counts, not factors).
3. **The TroopWeight elite tax** via `ITroopWeightService.ApplyPartySizeWeightPenalty`, which
   subtracts the party's weight surplus (`TroopWeightService.ComputeSizePenalty`,
   `Main/Features/TroopWeight/TroopWeightService.cs:181`). Since 2026-09-06 the UI shows that tax as
   capacity *used* (`19 / 20`) rather than as a shrunken limit. The enforced number is unchanged.

Item 3 is why a small culture bonus can read as absent in play: evil rosters are precisely the
weight-2.0 ones, so the tax can subtract more than a small percentage adds. That interaction is the
reason the evil-culture party-size feats now carry a 20% floor. See
[cultural-feats.md](../features/cultural-feats.md) "Evil-culture party-size floor" and
[troop-weight-system.md](../features/troop-weight-system.md).

## The new-game top-up, and the second thing the template controls

On a **new campaign only**, `HeroSpawnCampaignBehavior.SpawnLordParty`
(`TaleWorlds.CampaignSystem.CampaignBehaviors/HeroSpawnCampaignBehavior.cs:246-278`) can add more men
on top of the spawn roster. The block at `:262-276`:

```
count  = (int)((PartySizeLimit - MemberRoster.TotalManCount) * RandomFloatRanged(0.75f, 0.9f))  // :264
weight = (stack.MinValue + stack.MaxValue) / 2f                                                 // :269
```

then draws `count` troops with `MBRandom.ChooseWeighted` over those per-stack weights. The draw is a
plain `for (i = 0; i < count; i++)`, so a negative `count` simply does not run.

**These targets push most lords past that gate.** The roster the lord is already holding at that
point came from the template: `LordPartyComponent.InitializationArgs.InitializeLordPartyProperties`
(`LordPartyComponent.cs:38-39`) hands `Clan.DefaultPartyTemplate` to
`InitializeMobilePartyAroundPosition`, which routes to `FindAppropriateInitialRosterForMobileParty`
(`MobileParty.cs:2645`). So the top-up fires only for the lords whose ratio draw happened to land the
roster under their own limit, which is `r < (limit - minSum) / spread`. For Mordor that is
`(limit - 47) / 3453`: against a limit near 200 it is about 4% of lords, down from about 11% at the
old spread of 1399. For the other 96% the template alone sets starting size and the top-up adds
nothing. The bigger the target, the more completely the template takes over.

On the parties where it does fire, `(min + max) / 2` per stack is what decides **which** troops get
added, so editing a stack's `max_value` shifts that stack's share of the composition, not just the
count. Widening one stack far past its siblings makes that troop the bulk of the men it adds.

`MobilePartyHelper.FillPartyManuallyAfterCreation` (`Helpers/MobilePartyHelper.cs:294-349`) runs the
same ratio math backwards from a desired man count, and uses the same `(max + min) / 2` weighting to
trim or pad to that exact number. Its only callers in the Campaign assembly are four issue
behaviours, each passing its own count (`EscortMerchantCaravanIssueBehavior.cs:690`,
`ExtortionByDesertersIssueBehavior.cs:969` and `:1036`, `SmugglersIssueBehavior.cs:778`, the last
scaling off the player's party rather than a constant). It is not part of the lord-party path.

## Current per-culture max-sum targets

Set by `tools/rebalance_party_template_maxes.py` (`CULTURE_TARGETS`, lines 48-76) and applied to
`Main/_Module/ModuleData/taom_partyTemplates.xml` on 2026-08-14. **193 templates** are in scope.
"Old max" below is the culture-level template's max sum at `HEAD` before the 2026-08-14 pass; "min"
is its min sum, which the tool never touches.

**The Target column was retargeted on 2026-09-01** from 4500 / 3500 / 2000 / 1500 / 1000 to the
values shown. Those larger numbers were sized for the 10x AI party size cap that shipped alongside
them; that cap is neutral by default now, so a 3500 template made a Mordor lord spawn about 2,400 men
and lose all but ~150 on the first daily tick. The band ordering is preserved and compressed, because
a 4.5:1 spread is meaningless once every culture is trimmed to the same 40-203 cap.

**Use the mean, not the worked example.** At `r = 0.69`, the ratio behind the 2423-man Mordor lord
that triggered this pass, the culture-default templates land near goblin 237, orc 184, dwarf 156,
men 142, elf 121. That is one point on a distribution, not a typical value: `r` is
`party.RandomFloat()`, uniform and drawn once per party. The number to use for balance work is the
mean across every template in a band, which is lower: elf 78-104, men 102-128, dwarf 113-161,
orc 132-176, goblin 162-187. An individual lord can land anywhere from 3 to 320.

| Culture | Target | Templates in scope | Culture template: stacks | min | Old max |
|---|---|---|---|---|---|
| goblin | 320 | 11 | 12 | 54 | 600 |
| bluecraig | 320 | 1 | 12 | 54 | 600 |
| mordor | 260 | 16 | 52 | 47 | 1446 |
| isengard | 260 | 12 | 19 | 54 | 950 |
| gundabad | 260 | 6 | 15 | 58 | 750 |
| dolguldur | 260 | 7 | 31 | 93 | 1550 |
| mistymountainorcs | 260 | 6 | 12 | 54 | 600 |
| erebor | 220 | 8 | 47 | 102 | 2350 |
| gondor | 200 | 15 | 14 | 10 | 700 |
| rohan | 200 | 23 | 10 | 37 | 500 |
| dale | 200 | 10 | 21 | 57 | 1050 |
| dunland | 200 | 10 | 10 | 33 | 500 |
| rhun | 200 | 20 | 8 | 30 | 400 |
| harad | 200 | 10 | 6 | 26 | 300 |
| umbar | 200 | 6 | (no culture template) | | |
| shaghana | 200 | 9 | (no culture template) | | |
| abanissa | 200 | 8 | (no culture template) | | |
| rivendell | 150 | 6 | 16 | 59 | 800 |
| lothlorien | 150 | 1 | (no culture template) | | |
| mirkwood | 150 | 7 | 13 | 55 | 650 |
| lindon | 150 | 1 | 16 | 59 | 800 |

"Templates in scope" is the tool's own classification, which keys off the id prefix, so Blue Craig's
five `goblin_bluecraig_N` clan templates count under **goblin** and Lindon's two `rivendell_lindon_N`
count under **rivendell** (see the matcher trap at the end of this doc). **Reading the 2026-08-14 pass**, whose targets were 4500 / 3500 / 2000 / 1500 / 1000 and are now
history: `kingdom_hero_party_erebor_template` was the only template in the whole file whose max sum
went **down** (2350 to 2000), the biggest jump was goblin and Blue Craig at 600 to 4500, and Rhûn and
Harad started from the two smallest templates in the set, 400 and 300. The 2026-09-01 retarget then
brought every one of them into the band in the table above.

`khand` is present in `CULTURE_TARGETS` and matches nothing: no `kingdom_hero_party_khand_*`
template exists. Leaving the key costs nothing and documents the intent if one is ever authored.

### The 2026-09-04 floor bug

The retarget above shipped with a defect worth knowing before you run this tool again. `retarget()`
scaled every stack's spread with no floor, so a thin stack (5 of Mordor's old 3500) rounded to
`min 0 / max 0` at the new 260 target. That zeroed 45 stacks and deleted six Black Numenorean troop
types (`mordor_num_knight`, `_warden`, `_marksman`, `_temple_knight`, `_temple_guard`, `_shadowbow`)
from 14 of Mordor's 16 lord templates.

It was unrecoverable by the tool. A 0/0 stack cannot spawn from either path, and the next retarget
scales from a spread that is already zero, so no future target restores it. Only git history held
those numbers.

**Every gate passed while it was broken:** the tool reported success, the XML parsed,
`validate_moduledata.py` returned 0 errors, and the full suite passed, because nothing asserted that
a stack which could previously spawn a troop still can.

Fixed in `bb01b9a4` (v2.0.28). `retarget()` now floors any stack that had a real spread at `min + 1`,
the drift-absorption pass respects that floor, and a stack authored at `max == min` is still left
pinned. The templates were regenerated from the pre-retarget file rather than patched forward, since
the lost spreads could not be scaled back up. `tools/tests/test_rebalance_party_template_maxes.py`
pins the floor and its first test fails against the pre-fix formula.

Mordor is the exposure because it carries 52 stacks against the same budget every other culture
spends on 12 to 27. If you add a culture with comparable stack variety, check for zeroed stacks after
a retarget rather than trusting the sum.

### Cultures that share another culture's templates

`Clan.DefaultPartyTemplate` returns the clan's own `_defaultPartyTemplate` when it has one and
otherwise falls back to `Culture.DefaultPartyTemplate` (`TaleWorlds.CampaignSystem/Clan.cs:112-122`).
Vanilla kingdom clans do not carry the attribute at all (only bandit and minor-faction clans do, 25
of them in `SandBox/ModuleData/spclans.xml`), so a TAOM kingdom clan uses whatever `spclans.xslt`
binds for it, or the culture's template if that block binds nothing.

Verified against `Main/_Module/ModuleData/taom_spcultures.xml` and `spcultures.xslt`:

| Culture | `default_party_template` | Source |
|---|---|---|
| lothlorien | `kingdom_hero_party_rivendell_template` | `taom_spcultures.xml` |
| umbar | `kingdom_hero_party_harad_template` | `taom_spcultures.xml` |
| shaghana | `kingdom_hero_party_harad_template` | `taom_spcultures.xml` |
| abanissa | `kingdom_hero_party_harad_template` | `taom_spcultures.xml` |
| battania (Khand / Variag) | `kingdom_hero_party_rhun_template` | `spcultures.xslt:1349` |

Khand is the case to watch, and it shares far more than the one attribute the table lists. Across
the fourteen troop and party-template attributes the two blocks set (`spcultures.xslt:1323-1370` for
`battania`, `:923-939` for `khuzait`), thirteen are identical strings: the same `basic_troop`
(`loke_rim_initiate`), the same `elite_basic_troop` (`loke_rim_cavalry`), the same four
`rhun_militia_*` troops, and the same villager, militia, rebels and three patrol templates. Only
`vassal_reward_party_template` differs, and it does not point at Rhûn either (Khand's is
`vassal_reward_troops_mordor`). Khand has no distinct roster, so retuning Rhûn retunes Khand.

The four sharers above still have per-clan templates of their own (6 umbar, 9 shaghana, 8 abanissa,
1 lothlorien), which is why they appear in the target table with no culture-level row.

The vanilla-id mapping for the XSLT blocks, since the file names them by vanilla id: `empire` is
Dunland, `aserai` is Harad, `vlandia` is Rohan, `khuzait` is Rhûn, `sturgia` is Dale, `battania` is
Khand.

## How to retune

```bash
python tools/rebalance_party_template_maxes.py            # dry-run, the default
python tools/rebalance_party_template_maxes.py --apply    # writes taom_partyTemplates.xml
```

Edit `CULTURE_TARGETS` at the top of the script, dry-run, read the report, then `--apply`.

**Method:** `min_value` is never touched. Each stack's spread (`max - min`) is scaled by one factor
per template so the template's max sum lands exactly on the culture target, then rounding drift is
absorbed by the widest stacks. Holding min fixed is what guarantees `max >= min` for every stack,
which the engine relies on: a max below its min makes `(max - min) * r` negative and the stack fills
below its floor.

**Idempotent.** The target is absolute, not a multiplier, so re-running against an already-retargeted
file is a no-op. Re-run on 2026-08-14 after the apply: 193 templates, **0 stacks changed**.

**Scope** is `kingdom_hero_party_<culture>[_<clan>_N]_template`, matched by
`^kingdom_hero_party_(?!mercenary_|outlaw_)(.+)_template$` (script line 79). That includes the
per-clan variants bound by `spclans.xslt` and `characters/clans.xml`, which is what most named lords
actually spawn from. All 177 distinct clan-bound `kingdom_hero_party_*` references across those two
files fall inside the 193, checked on 2026-08-14. Mercenary and outlaw templates are deliberately
excluded: they belong to minor factions, not kingdom lords.

**One trap in the matcher.** The culture key is taken from the template id **prefix**, not from the
owning clan's culture. `kingdom_hero_party_goblin_bluecraig_N_template` classifies as `goblin`, and
`kingdom_hero_party_rivendell_lindon_N_template` classifies as `rivendell`. That is harmless today
only because each of those pairs shares a target (4500 and 1000). Give Blue Craig or Lindon a
different number from its prefix culture and those clan templates will silently follow the prefix.
The script prints a `WARNING: ... matched no culture key and were SKIPPED` block for ids that match
nothing, so a genuinely unrecognised template is loud; a mis-attributed one is not.

After any apply, run `python tools/validate_moduledata.py` (the `BROKEN_TROOP_REF` and
`BROKEN_PARTY_TEMPLATE_REF` checks cover this file).

## Open questions

**UPDATE 2026-09-01: resolved from the other end.** The AI Party Size numeric defaults reset to
neutral, so the cap is the vanilla one this section describes as the problem. Rather than raise the
cap to meet the templates, the templates were brought down to meet the cap (see the retarget note
above). Spawn and cap are now in the same range, which is what the answer below was reaching for.
Read it as design history; the numbers in it are the 2026-08-14 ones.

**ANSWERED 2026-08-18: the cap has now been raised.** This section predicted the behaviour
correctly and play confirmed it, with one correction to the mechanism. The collapse was not mainly
the quarter-of-overflow desertion described below, which takes about ten campaign days; it was
TAOM's own TroopWeight shed, which runs off `DailyTickPartyEvent` for every party not in a map
event and removes the WHOLE overflow in a single tick. Players saw the drop within a couple of
ticks, not a couple of weeks.

Raising the cap alone would not have been enough. Two further mechanisms ignore `PartySizeLimit`
entirely and run on morale instead: vanilla's morale desertion (up to 14.87% a day below morale 10)
and the garrison dump. A large party starves automatically (a flat -30 morale) and cannot pay its
wages (-20), so both stay armed no matter how high the cap goes. The fix therefore carries food and
wage relief alongside the cap, and re-derives startup gold, whose `K` turns out to be the assumed
party size. See [../features/ai-party-size.md](../features/ai-party-size.md) and issue #461.

**A 3500-man cap would be more than ten times what vanilla builds a lord party from.** Vanilla starts
at `BaseMobilePartySize = 20` (`DefaultPartySizeLimitModel.cs:27`) and adds 15 per clan tier plus 25
per tier for the party leader (`:45`, `:47`) on top of leadership, renown and perk bonuses, which
puts a high-tier lord in the low hundreds. Raising `PartySizeLimit` to match these targets is the
obvious follow-up and is deliberately not done: two armies at that scale meeting on a field battle
is a load TAOM has never measured, and neither the map-event nor the mission path has been profiled
at it. Attempting it wants its own issue and its own control battles, plus a look at whether the
engine's formation code (see [formations-and-team-ai.md](engine/formations-and-team-ai.md)) behaves
at those counts.

**Whether the TroopWeight elite tax should apply at all to a culture whose entire roster is heavy**
is unanswered. Today an orc culture is taxed for fielding orcs, which is the only thing it can
field. The 20% feat floor is a workaround for the symptom, not an answer to that question.

**Composition drift is real and untested.** The retarget scales every stack's spread by one factor
but leaves `min_value` alone, and the spawn formula fills a stack to `min + spread * r`. Only the
spread half grew, so each stack's share of the roster shifts toward the wide stacks and away from
the ones whose min carried most of their count. Mordor's factor is `(3500 - 47) / (1446 - 47)`,
about 2.47, so a `min="5" max="5"` stack still contributes 5 while a `min="1" max="100"` stack now
contributes about `1 + 99 * 2.47 * r`. Nobody has compared a spawned Mordor party's troop mix before
and after. Note that this arithmetic describes the pre-Black-Numenorean file: commit `2fcbef10` took
Mordor from 39 stacks to 52 and re-ran the tool, so the per-stack spreads have moved even though the
min sum (47) and the 3500 target have not.

## Related

- [cultural-feats.md](../features/cultural-feats.md): the culture party-size feats and the
  evil-culture floor that share `TaomPartySizeModel` with this
- [troop-weight-system.md](../features/troop-weight-system.md): the elite tax
- [culture-playability-wiring.md](../features/culture-playability-wiring.md): the eight engine-read
  party-template attributes on a culture, and why an XSLT block silently inherits Calradia for
  anything it never names
- [engine/campaign-object-graph.md](engine/campaign-object-graph.md): `Clan` / `MobileParty` /
  `PartyBase` relationships
- `tools/raise_party_template_maxes.py`: the completed 2026-07-02 one-off that raised `max_value` to
  50 on every kingdom and bandit template stack still below it, leaving the one-per-hideout boss
  stack alone. Superseded for culture-level retargeting by `rebalance_party_template_maxes.py`; kept
  for its history

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/black-numenorean.md](../features/black-numenorean.md)
- [docs/features/caravan-bandit-parity.md](../features/caravan-bandit-parity.md)
- [docs/features/culture-playability-wiring.md](../features/culture-playability-wiring.md)
- [docs/features/startup-resources.md](../features/startup-resources.md)
- [docs/features/troop-weight-system.md](../features/troop-weight-system.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/migration/templates/troops-and-parties.md](../migration/templates/troops-and-parties.md)
- [docs/modding/balance-levers.md](../modding/balance-levers.md)
- [docs/modding/cultures.md](../modding/cultures.md)
- [docs/modding/party-templates.md](../modding/party-templates.md)
- [docs/modding/recipe-add-a-culture.md](../modding/recipe-add-a-culture.md)
- [docs/modding/recipe-new-mod-from-zero.md](../modding/recipe-new-mod-from-zero.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)
- [docs/reference/feature-map.md](./feature-map.md)
- [docs/reviews/lessons/campaign-mechanics.md](../reviews/lessons/campaign-mechanics.md)

<!-- backlinks-end -->
