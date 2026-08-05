# Prisoner Recruitment (Alignment Morale Waiver)

## Overview

Recruiting a prisoner into your party costs no morale when that prisoner is of **your own faction** or
**your own side of the war**. An Isengard captor recruiting a Mordor, Gundabad or Dunland prisoner
loses nothing — all serve Sauron. A Gondor captor recruiting Rohan or Dwarf prisoners likewise. Every
other case keeps vanilla behavior untouched.

Applies to the player, AI lords, and the party-screen cost label alike, through a single GameModel
override. MCM group: **World/Prisoner Recruitment** (master + per-player/AI gates, all default ON).

## Why This Exists

- **Vanilla behavior:** `DefaultPrisonerRecruitmentCalculationModel.GetPrisonerRecruitmentMoraleEffect`
  charges **−1 morale per troop recruited (−2 per bandit)**, multiplied by the count, regardless of who
  the prisoner is. The only relief is a perk: `Leadership.Presence` (same-culture only) or
  `Roguery.TwoFaced` (bandits only) zeroes it.
- **TAOM requirement:** In Middle-earth the War of the Ring has two sides. Absorbing troops who already
  fight for your cause is not a morale event — your men do not resent standing beside fellow servants
  of Sauron.
- **Without this feature:** An Evil player who beats a neighbouring Evil faction pays the same morale
  tax to absorb their orcs as they would to press-gang Gondorian knights.

## The Rules

Two rules, evaluated in order. Both must pass the settings gates first.

| # | Rule | Waives? |
|---|------|---------|
| 1 | **Own faction** — prisoner's culture StringId equals the recruiter's culture StringId (case-insensitive) | Yes, always |
| 2 | **Own side** — recruiter's side and prisoner's culture side are equal *and* not Neutral | Yes |
| — | Anything else (cross-side, Neutral↔Neutral, unknown ids) | No — vanilla applies |

**Recruiter side resolution is kingdom-first, culture-fallback.** The fallback is load-bearing, not
defensive: a kingdomless Isengard player has `Clan.Kingdom == null`, and a player-founded kingdom is
`new_kingdom` — both unclassified, so without the fallback the feature would never fire for its primary
user. (Same failure class as Codex #327 against WarOfTheRingMomentum.)

**Rule 1 is what covers the Neutral factions.** Khand (`battania`), Umbar, Shaghâna and Âbanissa are
`"neutral"` in `alignment.json`, so rule 2 deliberately never fires for them. Without rule 1 a Khand
player would lose morale recruiting Khand troops — same faction, penalty anyway.

### Known asymmetry (by design)

A Khand player waives Khand prisoners but **not** Umbar ones, even though both are Neutral. This is
intentional: Neutral means *unaffiliated*, not *allied*. Two Neutral factions share no cause, so they
get rule 1 and not rule 2. If this ever reads as a bug in play, the fix is a data decision — classify
those factions onto a real side in `alignment.json` — not a code change here. That would ripple into
Diplomacy, CaravanTrade, WarOfTheRingMomentum, AlignmentDesertion and Execution, which all read the
same file.

## Architecture

```
TaomPrisonerRecruitmentCalculationModel : DefaultPrisonerRecruitmentCalculationModel
        │  extracts ids, single delegate, base fall-through (gamemodels.md rule 4)
        ▼
IPrisonerRecruitmentMoraleService  ──►  IAlignmentService (Execution feature)
        │                                    └─ execution/alignment.json (shared, 5 other consumers)
        └──►  IPrisonerRecruitmentSettingsProvider ──► TaomSettings (MCM)
```

**No Harmony patch.** All three engine consumers resolve the model through `Campaign.Current.Models`:

| Consumer | Call site (v1.4.7) |
|---|---|
| AI prisoner recruitment | `RecruitPrisonersCampaignBehavior.ApplyPrisonerRecruitmentEffects` → `mobileParty.RecentEventsMorale += …` |
| Player party screen | `PartyScreenLogic` → `SetMoraleChangeAmount(…)` |
| UI cost label | `PartyCharacterVM.SetMoraleCost` → `RecruitMoraleCostText` |

One override therefore covers all three, and the displayed cost cannot desync from the applied one.

**No new alignment table.** Side data is reused from the Execution feature's `IAlignmentService`. This
feature is the mirror image of `AlignmentDesertion`: desertion sheds troops whose culture *opposes* the
owner's side; this waives the cost of taking on troops whose culture *shares* it.

**`AreEnemyAlignments` is deliberately not used** — its Neutral semantics are inverted (it treats
Neutral as enemy-of-everyone). Three other consumers route around it too.

### Why no JSON config file

Unlike the sibling alignment features, there is no config provider or JSON defaults layer. Every
setting here is a boolean — no range, ordering, or sign invariant exists for the
`Config Providers MUST Validate` rule to bite on. Compiled defaults live in the settings provider, MCM
on top. This is a deliberate omission, not an oversight.

### The −2 bandit cost is never waived

Be precise about what this claims. Vanilla keys the **−2** on `character.Occupation == Bandit` (a
per-**troop** field) but gates recruitability on `character.Culture.IsBandit` (a per-**culture** field).
Those are independent, and the guarantee is specifically: *no troop that costs −2 is ever waived.*

Three barriers hold it, none of them code in this feature:

1. All 8 `occupation="Bandit"` troops carry **dedicated bandit cultures**
   (`dunland_raiders_boss` → `Culture.dunland_raiders`, `gondor_soldiers_boss` →
   `Culture.gondor_soldiers`, …) — never a mainline culture. So rule 1 can't match them.
2. None of the 8 bandit culture ids appears in `alignment.json` → all resolve Neutral → rule 2 can't fire.
3. Vanilla's `IsPrisonerRecruitable` blocks `character.Culture.IsBandit` outright — those 8 aren't
   recruitable at all.

**Barriers 1 and 2 are facts about the shipped data, not invariants the type system enforces**, so both
are pinned by tests in `ShippedAlignmentConfigTests`, each deriving its id set from
`taom_spcultures.xml` rather than hardcoding a list that could itself go stale:

- `ShippedAlignmentConfig_BanditCultures_ResolveNeutral…` — barrier 2. Adding
  `"dunland_raiders": "evil"` would otherwise silently start waiving −2 for every Evil player.
- `ShippedTroops_EveryBanditOccupationTroop_CarriesABanditCulture` — barrier 1. A troop authored with
  `occupation="Bandit"` **and** a mainline culture would be recruitable (barrier 3 checks culture, not
  occupation) *and* waivable — zeroing a −2. Nothing else in the stack would catch it.

### What DOES waive: mainline troops in bandit parties

Bandit-hideout rosters are populated from mainline troops, not from the `occupation="Bandit"` boss.
The `dunland_raiders` culture draws `dunland_peasant` / `dunland_raider` / `dunland_clan_warrior`,
which carry `culture="Culture.empire"` (Dunland, **evil**) and `occupation="Soldier"`.

So an Isengard player who clears a Dunland-raider hideout **does** waive the cost on the captured
rank-and-file. This is correct and intended — those troops are Dunlendings, and Dunland is on Sauron's
side. Vanilla charges them −1, not −2 (their occupation isn't Bandit), and the −2 guarantee above is
untouched. "Captured from a hideout" and "same side, no morale cost" legitimately overlap.

## Files

| File | Role |
|---|---|
| `Main/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleService.cs` | The two rules + side resolution (pure, 100% tested) |
| `Main/Features/PrisonerRecruitment/PrisonerRecruitmentSettingsProvider.cs` | MCM over compiled defaults |
| `Main/Features/PrisonerRecruitment/Models/TaomPrisonerRecruitmentCalculationModel.cs` | The GameModel boundary |
| `Main/Features/PrisonerRecruitment/PrisonerRecruitmentIoC.cs` | Two `Reuse.Singleton` registrations |
| `Main/_Module/ModuleData/execution/alignment.json` | Shared side data (**owned by the Execution feature — not modified by this one**) |
| `TAOM.Tests/Features/PrisonerRecruitment/` | 32 service tests + 17 shipped-data pins |

Registered in `Main/IoC.cs` (beside the alignment family) and `Main/SubModule.cs` (`OnGameStart`).

## Culture ids (the trap)

The six XSLT-reskinned cultures keep their **vanilla** ids — `spcultures.xslt` renames `<name>`, never
`id`. Writing the lore name produces a dead key that silently resolves Neutral.

| Faction | Culture id | Side |
|---|---|---|
| Mordor | `mordor` | evil |
| Isengard | `isengard` | evil |
| Gundabad | `gundabad` | evil |
| Dol Guldur | `dolguldur` | evil |
| **Dunland** | **`empire`** | evil |
| **Rhûn** | **`khuzait`** | evil |
| **Harad** | **`aserai`** | evil |
| **Khand** | **`battania`** | neutral |
| **Rohan** | **`vlandia`** | free |
| **Dale** | **`sturgia`** | free |
| Gondor | `gondor` (kingdom `empire_w`) | free |

## Model registration ordering

TAOM subclasses `Default*` and registers with a plain `campaignStarter.AddModel(...)` in `OnGameStart`.
`GameModelsManager.GetGameModel<T>` scans registrations **backwards** — last-added wins, no chain walk.
`Campaign.Initialize` runs SandBox's registration, then `InitializeGameStarter` (where StoryMode would
register), then `OnGameStart` (TAOM), so TAOM's model always resolves. `TaomBanditDensityModel` is the
existing precedent for this exact shape against the same StoryMode conflict.

**Note:** `StoryModePrisonerRecruitmentCalculationModel` overrides all six methods of this model, and
its `GetConformityChangePerHour` zeroes conformity during the unfinished tutorial. TAOM winning the
slot would drop that behavior. This is inert in practice — StoryMode is not in TAOM's
`SubModule.xml` dependencies and `MainMenuCustomizerService` hides `StoryModeNewGame`, so no
`CampaignStoryMode` game is reachable.

## Testing

- **32 service tests** — the 3×3 side matrix, the own-faction rule (incl. the Neutral-faction case that
  is its reason to exist, and casing), kingdom-vs-culture precedence, both fallback cases, all three
  settings gates in both directions, and the null contract (garrisons/militia/caravans have no
  `LeaderHero`).
- **17 shipped-data pins** — bandit cultures resolve Neutral; every `occupation="Bandit"` troop carries
  a bandit culture; the seven Sauron-serving cultures resolve Evil; the Free peoples resolve Free.
- Mutation-verified: disabling rule 1 fails exactly the 3 own-faction tests; removing the Neutral guard
  fails exactly the 2 Neutral↔Neutral tests. The tests are not vacuous.
- The model class is thin → no direct test (`gamemodels.md` rule 8).

**In-game smoke (owed — unit tests cannot prove the model resolves at runtime):** as an Isengard
player, open the party screen on a captured Mordor/Gundabad/Dunland prisoner → cost reads `0`; a
Gondor/Rohan prisoner → reads `-1`; a bandit prisoner → not recruitable. Toggle the MCM master off →
the Mordor prisoner reverts to `-1`.

## Related

- [alignment-desertion.md](alignment-desertion.md) — the mirror-image feature (sheds opposed troops)
- [alignment-recruitment.md](alignment-recruitment.md) — blocks volunteer recruitment at enemy settlements
- [alignment-aware-execution.md](alignment-aware-execution.md) — owns `IAlignmentService` + `alignment.json`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
