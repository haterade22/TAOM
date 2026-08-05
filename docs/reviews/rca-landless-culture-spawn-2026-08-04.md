# RCA — a landless culture kills the daily clan tick (2026-08-04)

**Symptom:** hard CTD on an ordinary daily clan tick — no mission active, no TAOM frame anywhere on
the stack, no TAOM patch on frames 0-11 by the crash report's own Harmony correlation.
**Bundle:** `E:/LOTRAOMAssets/taom_crash_20260804_081845_099f650c`, signature `099f650c` — TAOM
v2.0.16, Bannerlord v1.4.7.117484, campaign Summer 2 / 1084, player level 3.
**Issue:** [#374](https://github.com/haterade22/TAOM/issues/374) · **Feature doc:**
[`lord-spawn-guard.md`](../features/lord-spawn-guard.md).

The feature doc says what the fix *is*. This document is how the defect got in, why it survived
every existing check, the two wrong answers the investigation produced on the way, and the near-miss
that would have replaced one bug with two.

## Symptom and stack

```
System.InvalidOperationException: Sequence contains no matching element
  System.Linq.Enumerable.First
  HeroSpawnCampaignBehavior.SpawnLordParty(Hero, Boolean)
  ConsiderSpawningLordParties(Clan, Boolean)
  TrySpawnHeroesAndParties
  CampaignEvents.DailyTickClan
  CampaignPeriodicEventManager.PeriodicDailyTick
  Campaign.Tick
```

Every frame is vanilla. The throw is `HeroSpawnCampaignBehavior.SpawnLordParty` (v1.4.7,
lines 252-260):

```csharp
Settlement settlement = SettlementHelper.GetBestSettlementToSpawnAround(hero);
if (settlement == null || settlement.MapFaction != hero.MapFaction)
    settlement = hero.MapFaction.InitialHomeSettlement;
if (settlement == null)
    settlement = Settlement.All.First(x => x.Culture == hero.Culture);   // First, not FirstOrDefault
```

The last line is safe in Calradia because every Calradian culture owns land. It is out of
`Campaign.Tick`, past every managed backstop.

## Root cause: a chain of two independent faults, neither fatal alone

| | Fault | Owner | Alone |
|---|---|---|---|
| 1 | The hero's map faction has no `InitialHomeSettlement` | any mod that creates a clan at runtime and never calls `SetInitialHomeSettlement` | **harmless** — vanilla falls to the `First()`, which finds a settlement because the culture owns land |
| 2 | The hero's culture owns zero settlements | TAOM's map data | **harmless** — the `First()` is never reached, because the faction anchor is non-null |

Both at once is the crash. That structure is why it sat latent for as long as it did, and why the
fix has to be two-sided.

**Fault 1 is unbounded.** `GetBestSettlementToSpawnAround` weights own-faction settlements 10,000x
higher, so the `MapFaction` mismatch branch only fires for a landless faction to begin with.
**Vanilla ships no clan without `initial_home_settlement`.** This RCA originally claimed three
(`guardians`, `chosen_of_the_sky`, `freemen`); that was a comment-blind grep — all three are inside
XML comment spans in SandBox `spclans.xml` (95 live `<Faction>` elements by `ElementTree`, zero
missing the attribute; 98 by regex over raw text). The one non-mod path that does exist is a
settlement rename that misses `spclans.xslt`, which re-points 26 vanilla clans whose original ids
are absent from TAOM_Map. Otherwise that leaves clans created at runtime. `Warlord` v1.1.6.1 (`com.bloc.warlord`, 31
Harmony patches, ships `ExtraCharacters.xml`) is the only clan-creating third-party mod in the
reporter's load order. **That attribution is unconfirmed** — Warlord is not installed on the dev
machine and was never decompiled. The fix does not depend on it, and no fix TAOM can write bounds
this fault.

**Fault 2 is TAOM's.** `<game>/Modules/TAOM_Map/ModuleData/settlements.xslt` contains
`<xsl:template match="Settlement"/>` — an unconditional empty template that deletes all 494 vanilla
settlements. TAOM_Map replaces them with 988 of its own, covering 27 of the 38 defined cultures.
Eleven cultures own nothing. Six of those (`looters`, `sea_raiders`, and the four `*_bandits`) are
unreachable — `GetBestAvailableCommander` filters on `Occupation.Lord` and bandit heroes are
`Occupation.Bandit`. Five can sit on a `Lord`: `battania`, `darshi`, `nord`, `vakken`,
`neutral_culture`.

**`battania` is TAOM's Variag culture, and it was authored complete except for land.** A 7,297-char
template in `Main/_Module/ModuleData/spcultures.xslt`, `{=TAOM_battania_culture}Variag`, 26
`notable_templates`, 68 `NPCCharacter.*_khand` bindings, a kingdom, `clan_battania_1`..`_8`,
`wolfskins`, `player_faction`, 41 surviving vanilla `Lord` characters (`main_hero` among them) and
18 TAOM-authored ones in
`characters/lords.xml`. The settlements were never migrated with it: all 27 K-series settlements
stayed `Culture.khuzait` (TAOM's `{=aom_khuzait_name}Easterlings`) while Variag clans owned ten of
them — `town_K1` Sturlurtsa Khand (`clan_battania_1`), `town_K2` Lermsakun (`_4`), `town_K3` Ardivar
(`_2`), `town_K4` Yanik Anli (`_3`), plus castles `K1`/`K6` (`_6`), `K5`/`K7` (`_7`), `K3` (`_5`),
`K2` (`_8`). `castle_K4` belongs to `clan_khuzait_1` and is a genuine Easterling holding.

So Khand produced Easterling notables, volunteer pools, guards and marketplace stock, and the
authored Variag culture owned nothing.

**`CultureConversion` could never have repaired it.** `CultureConversionBehavior` registers
`OnSettlementOwnerChangedEvent` + `DailyTickEvent`, and `CultureConversionService.RunDailyChecks`
only drains records already in the store. The store is seeded exclusively by `OnSettlementConquered`
— the owner-change path. A settlement whose culture never matched its owner *from day 1* is never
enqueued. The crash log confirms it: 90+ in-game days, zero conversion lines.

## Why it was not caught earlier

Four independent reasons, none of which is "nobody looked":

- **Nothing validated that a culture used by a lord owns land.** Not a check that existed and
  under-fired — a check that did not exist. `tools/taom_schema.py` had no settlement registry at all
  before this fix; `Registries` carried no `settled_cultures` field, so the question was unaskable.
- **The Variag culture was authored complete, so every check that did exist passed.** All 68
  `*_khand` `NPCCharacter` bindings resolve against the full `NPCCharacter` registry. The culture XSLT is
  well-formed and its transformed attributes resolve. Reference-integrity validation is exactly the
  wrong instrument for this defect: nothing is *dangling*. The culture points at everything it needs
  to point at. It is only missing a relationship that lives in a different file.
- **The two halves live in different modules, and one of them is a live file with no repo copy that
  matters.** The culture side is `Main/_Module/ModuleData/spcultures.xslt`; the settlement side is
  `<game>/Modules/TAOM_Map/ModuleData/settlements.xml`, whose repo shadow
  (`Main/_Module/ModuleData/settlements.xml`) is stale by design. A repo-only validator physically
  cannot see the shipped settlement list. `build_settled_cultures` has to walk the installed game
  modules to answer the question, which is why nobody wrote it as an afterthought.
- **The defect needs a second, third-party-supplied fault to become observable.** Fault 2 was
  present the day the Variag culture was authored and produced no crash, no warning, and no log
  line — only wrong-flavoured notables in Khand, which reads as a content nit rather than a fatal
  precondition. It became a CTD only when a mod in one player's load order supplied fault 1.

The generalisable shape: **a latent data defect whose only symptom is cosmetic until an external
actor supplies the second half.** The Khand mistag was filed mentally as a content nit for as long
as it existed; it was a fatal precondition the whole time.

## The investigation's own missteps

Both are method errors worth keeping, because both produced a *confident wrong answer* rather than
an obvious failure.

### (a) The XSLT attribute-exclusion trap — two wrong answers in opposite directions

Determining which factions lack `initial_home_settlement` requires modelling what
`spclans.xslt` / `spkingdoms.xslt` actually emit. Several templates drop the inherited attribute via
`@*[local-name() != 'initial_home_settlement']` and then re-add it with `<xsl:attribute>`.

- **Pass 1** read the vanilla XML and reported **23 dangling refs**, including the entire
  `clan_battania_*` set — the exact clans at the centre of the real bug, which made the wrong answer
  look like a discovery.
- **Pass 2** modelled the XSLT but ignored the attribute-exclusion filter and reported **zero**.
- Only honouring both halves — the exclusion *and* the re-add — gives the true answer: every TAOM
  clan and kingdom carries a valid `initial_home_settlement` once resolved.

Two passes, two answers, both wrong, and the first was wrong in a way that confirmed the hypothesis
under test. **An XSLT question is not answered by reading either the input or the stylesheet — only
by the transform's output.** `/xslt-check` exists for this; running the transform through lxml is
what settled it, and is what confirmed the Variag attributes at the end.

### (b) `[CultureMarketplace] town_K1 (battania)` — a diagnostic label naming a different field than it reads

This line appears in the reporter's debug log and reads as proof that conversion had already flipped
`town_K1` to `battania` — i.e. that the settlement culture was fine and the bug was elsewhere. It is
not. The diagnostic is fed by `ITownRosterAdapter.GetCurrentCultureId(Settlement)`, whose body is:

```csharp
return settlement?.OwnerClan?.Culture?.StringId;   // Main/Adapters/TownRosterAdapter.cs:19-22
```

That is the **owner's** culture, not the settlement's. The label reads like the settlement's because
of the method name — and `ICultureConversionAdapter.GetCurrentCultureId(string settlementId)`, a
same-named method on a sibling adapter, *does* return `Settlement.Find(id)?.Culture?.StringId`. Two
adapters, one method name, two different fields. The log line was in fact evidence *for* the bug
(owner culture ≠ settlement culture) and was read as evidence against it.

**A log line is a claim about the field its author intended, not the field its name suggests. Read
the getter before reasoning from the output.** Same class as
`rca-validator-silent-scope-2026-08-03` finding 4: a plausible artefact consistent with the symptom
reads as confirmation of it.

## The near-miss: retagging alone would have shipped two new bugs

Retagging 26 settlements to `Culture.battania` is a one-attribute edit per row. It would also have
**activated bindings that were dormant only because the culture was landless**:

| Binding | Vanilla value, dormant | Would have produced | Now |
|---|---|---|---|
| `basic_troop` | `battanian_volunteer` | Calradian Battanian recruits | `loke_rim_initiate` |
| `melee_militia_troop` | `battanian_militia_spearman` | Calradian Battanian militia in Sturlurtsa Khand | `rhun_militia_spearman` |
| `ranged_militia_troop` | `battanian_militia_archer` | same | `rhun_militia_archer` |
| `melee_elite_militia_troop` | `battanian_militia_veteran_spearman` | same | `rhun_militia_veteran_spearman` |
| `ranged_elite_militia_troop` | `battanian_militia_veteran_archer` | same | `rhun_militia_veteran_archer` |
| `default_party_template` | `kingdom_hero_party_battania_template` | Calradian party composition | `kingdom_hero_party_rhun_template` |

TAOM redefines the `battanian_*` ids **nowhere**. The second latent bug was in the volunteer
cascade: it ends at `ResolvePool(context.CultureId, CultureMap)`, the K-series settlements have no
per-settlement pools, and `CultureMap` had no `battania` entry — so the retag would have silently
dropped Khand's volunteers to null. Both were caught before the retag went in, and both are fixed by
aliasing the Rhun roster, which is what those settlements produced *before* the retag. Behaviour is
preserved; only the culture label is corrected. (`vassal_reward_party_template` was already
`vassal_reward_troops_mordor` — a pre-existing authoring choice, deliberately untouched.)

**Generalise it: activating dormant data is its own category of change.** A field that has never
been read is unconstrained by every test and every play session that came before, because none of
them executed it. The moment a data edit makes it live, its *entire* dependency fan-out becomes new
code with zero coverage. The audit belongs to the activation, not to the edit that triggers it —
enumerate every binding the newly-live key feeds, and check each against what the system produced
beforehand. Here it was six troop bindings plus one recruitment map; two of the seven would have
been player-visible within a day of a new campaign.

A pleasant side effect of the `CultureMap["battania"]` entry: `HasCulturePool` gates
`CultureConversion`, so a fief taken by a Variag clan could never convert to Variag before, and now
can. The pre-existing test `HasCulturePool_PlayableCultureWithoutTroopSet_ReturnsFalse_KnownGap`
documented that gap and was retired.

## What changed

**1. `Patch65_LandlessCultureSpawnGuard`** — makes the engine path survivable regardless of fault 1.
A **prefix** repairs the precondition rather than reimplementing the method: it gives the faction an
`InitialHomeSettlement`, so vanilla takes the branch *above* the throwing line and every downstream
side effect stays vanilla. `Clan.InitialHomeSettlement` is `[SaveableProperty(114)]`, so the repair
persists — one write per broken faction, not a per-tick patch-up — and it applies to existing saves.
The anchor order is deterministic, no RNG. A **finalizer** scoped to `InvalidOperationException`
only is the backstop, not the mechanism. Details in the feature doc.

**2. The Khand retag** — 26 of 27 K-series settlements moved to `Culture.battania` in the live
`TAOM_Map/ModuleData/settlements.xml` (battania 0 → 26, khuzait 136 → 110, total still 988, distinct
cultures 27 → 28), plus the six troop-binding repoints and `CultureMap["battania"]` that the
activation required. The script `tools/oneoff/retag_khand_to_variag.py` is dry-run by default, uses
an explicit id allowlist rather than a `town_K*` prefix sweep (which would have taken `castle_K4`),
asserts the current culture is khuzait before writing, refuses to write on any surprise, and is
idempotent.

**3. `LANDLESS_CULTURE` in `tools/taom_schema.py`** — severity ERROR, pass 4b. Any culture used by
an `NPCCharacter` with `occupation="Lord"`, a `<Faction>` or a `<Kingdom>` in TAOM's ModuleData must
own at least one settlement or appear in `_LANDLESS_BY_DESIGN` with a stated reason. `build_settled_cultures`
walks `Native`, `SandBoxCore`, `SandBox`, `CustomBattle`, `TAOM_Map` in load order and **honours the
unconditional strip-XSLT**, resetting the accumulated set when a module deletes everything before
it. Without that it would count vanilla's 494 deleted settlements, report every culture as landed,
and pass while the game crashed — the failure mode `rca-validator-silent-scope-2026-08-03`
recorded the day before.

### What now makes the class impossible

The precondition and the data defect are covered by different mechanisms on purpose. Patch65 covers
fault 1, which TAOM does not own and cannot bound — any mod, any future engine change. The validator
covers fault 2 in TAOM's own ModuleData, where it is a data defect that should never reach a build.
Vanilla-inherited factions are Patch65's problem, not a validator finding; scope stays matched to
the validator's documented contract.

**Evidence the check bites:** 18 `LANDLESS_CULTURE` errors before the retag — the 18 TAOM-authored
Variag lords in `characters/lords.xml` — and `PASS: no validation issues found.` after.

## Lessons to codify

**For `docs/reviews/lessons/data-content-cultures.md`:**

### A culture is not authored until something on the map carries it
The Variag culture had a name, 26 notable templates, 68 resolving NPC bindings, a kingdom, 8 clans
and 18 TAOM-authored lords (plus 41 vanilla `battania` `Lord` characters), and owned zero settlements. Every reference-integrity check passed, because nothing
was dangling — the missing piece was a *relationship* in another module's file, not a broken id.
- **Why missed:** the culture side and the settlement side live in different modules, and the
  settlement side's repo copy is a deliberate stale shadow, so no repo-only validator could see it.
- **Prevent:** `LANDLESS_CULTURE` (ERROR) in `tools/taom_schema.py`. When authoring or re-theming a
  culture, the settlement retag is part of the culture, not a follow-up.
- **Source:** `docs/reviews/rca-landless-culture-spawn-2026-08-04.md`

### Activating dormant data is a change in its own right, with its own audit
Retagging 26 settlements to `Culture.battania` woke six troop bindings and one recruitment-pool
lookup that had never executed, because nothing carried the culture. Left alone the retag would have
put Calradian Battanian militia in Sturlurtsa Khand and dropped Khand's volunteers to null.
- **Why missed by construction:** a field that has never been read is unconstrained by every test
  and every play session to date — none of them ran it. Activation turns the whole fan-out into
  uncovered new code.
- **Prevent:** when a data edit makes a previously-unused key live, enumerate every binding it feeds
  and diff each against what the system produced beforehand. Preserving prior behaviour is the
  default; changing it is a separate, deliberate edit.
- **Source:** same RCA.

**For `docs/reviews/lessons/xslt-moduledata.md`:**

### Answer XSLT questions from the transform output, never from the input or the stylesheet
"Which factions lack `initial_home_settlement`?" got two confident wrong answers before the right
one: 23 dangling refs (read the vanilla XML, ignored the rewrites) and zero (modelled the rewrites,
ignored `@*[local-name() != 'initial_home_settlement']`). The first wrong answer named the exact
clans under investigation, so it read as a discovery.
- **Prevent:** run the transform (lxml, or `/xslt-check`) and read the emitted attributes. A
  template that excludes an attribute and re-adds it is invisible to both halves read separately.
- **Source:** same RCA.

**For `docs/reviews/lessons/misc.md`:**

### A log line names the field its author intended, not the field its name suggests
`[CultureMarketplace] town_K1 (battania)` was read as "the settlement's culture is battania" and
sent the investigation down a wrong branch. It is fed by
`ITownRosterAdapter.GetCurrentCultureId(Settlement)` → `settlement?.OwnerClan?.Culture?.StringId` —
the *owner's* culture. `ICultureConversionAdapter.GetCurrentCultureId(string)` is a same-named
method on a sibling adapter that returns the settlement's own culture.
- **Prevent:** read the getter before reasoning from a diagnostic, especially when the line is the
  only evidence contradicting a hypothesis. Sibling adapters sharing a method name with different
  semantics is a live hazard in this codebase.
- **Source:** same RCA.

## Verification

| Gate | Result |
|---|---|
| `LordSpawnGuardServiceTests` (16) | written RED first — 11 failed against the stub |
| `Patch65LandlessCultureSpawnGuardBindingTests` (2) | pins the private-by-name target, the `MobileParty` return type the `ref __result` finalizer needs, the `hero` parameter name Harmony binds by, and both `InitialHomeSettlement` setters |
| `HarmonyPatchBindingTests` | 61/61 against the installed v1.4.7 engine |
| `./build.ps1 -RunTests` | **4912 passed, 0 failed, 2 skipped** (4914 total) |
| `python tools/validate_moduledata.py` | 18 `LANDLESS_CULTURE` errors before the retag, `PASS` after |
| XSLT | executed against vanilla `spcultures.xml` via lxml; transformed Variag attributes resolve |

**Save-compat:** the retag applies to **existing saves as well as new campaigns** — the reverse of
what this RCA first stated. `Settlement.Culture` is a bare `public CultureObject Culture;`
(v1.4.7 `Settlement.cs:70`) with no `[SaveableField]`, absent from `AutoGeneratedSaveManager`'s
Settlement member set, and re-read from XML on every load (`Settlement.cs:961`). See the deep-review
section below. The Patch65 anchor repair also applies to existing saves and *does* persist —
`Clan.InitialHomeSettlement` is `[SaveableProperty(114)]`.

## Owed follow-ups

- **In-game smoke — not run.** New campaign; Khand towns showing Variag notables and Rhun
  volunteers; ~30 days of map fast-forward so `DailyTickClan` sweeps every clan. Reproducing the
  *original* crash needs a clan with a null `InitialHomeSettlement` — Warlord, or a console command
  that calls `Clan.CreateClan` without `SetInitialHomeSettlement`.
- **`darshi` / `nord` / `vakken`** (`ghilman`, `skolderbrotva`, `forest_people`) are vanilla minor
  factions TAOM inherits and never re-cultured. All three keep a valid `initial_home_settlement`, so
  vanilla never reaches the `First()`, and all three sit in `_LANDLESS_BY_DESIGN`. Patch65 covers
  them if a mod re-parents their lords. They carry no TAOM content at all — worth their own issue.
- **The Warlord attribution stays unconfirmed** until someone installs it and decompiles its clan
  creation. Nothing in the fix depends on the answer.
- **Khand has no roster of its own.** The Rhun aliasing is behaviour-preserving, not final; a Variag
  troop tree would replace the six bindings in `spcultures.xslt` and the `CultureMap` entry.

---

# Deep review, 2026-08-04 (post-fix)

`/deep-review` ran 7 dimension finders over the changeset, then put every finding to three
independent adversarial refuters (correctness / reachability / severity lenses). **32 raw findings →
14 survived → 12 after merging cross-dimension duplicates.** Two entered as HIGH and were downgraded
to MEDIUM by unanimous refuter vote; three MEDIUMs went to LOW. Every finding below was re-verified
by hand before any fix was applied — two of the review's own numbers were wrong on first pass (see
"What the review got wrong").

Nothing threatened the crash fix itself. Patch65's prefix, finalizer, IoC wiring, apply timing and
adapter boundary all came through clean, and the compatibility dimension re-verified every
TaleWorlds signature against the installed v1.4.7 DLLs. **All four MEDIUMs were in the data half of
the change, and in the claims made about it.**

## Findings fixed

| # | Sev | Finding | Fix |
|---|-----|---------|-----|
| M1 | MED | The retag enumerated its id set from the `village_K*` **naming convention** instead of from the settlement graph, so 18 `castle_village_K*` settlements bound to the six retagged castles stayed `khuzait`. Every Khand fief group ended up split across two cultures: a Variag castle whose villages spawn Easterling headmen and draw the Easterling villager party template. | Extended `TARGET_IDS` with the 18 ids and re-ran. Verified: village-culture == bound-parent-culture was **0 mismatches / 607** before the migration, **18** after the first pass, **0** again now. |
| M2 | MED | The battania culture template bound six attributes and let `<xsl:apply-templates select="@*"/>` inherit the rest from vanilla — leaving **7 engine-read attributes** on Calradian entities: `elite_basic_troop` (`battanian_highborn_youth`), `villager_party_template`, `militia_party_template`, `rebels_party_template`, and all three `settlement_patrol_template_level_*`. | Bound all 7 to the khuzait/Rhun equivalents, restoring exact pre-retag parity. Deliberately did **not** bind `caravan_party_template` / `elite_caravan_party_template`: `CultureObject.Deserialize` (v1.4.7) reads only the plural child elements, so those attributes are inert markup. Verified by transforming with lxml and diffing the emitted element — only `id`, `name`, `text` and two cosmetic attributes still match `/battania/i`. |
| M3 | MED | **The save-compat claim was inverted.** Docstring, CHANGELOG and feature doc all said `Settlement.Culture` is persisted and the retag was new-campaign-only. It is a bare `public` field with no `[SaveableField]`, absent from `AutoGeneratedSaveManager`'s Settlement member set, and re-read from XML on every load. | Corrected in four places, and the existing-save load added to the owed manual tests. That path is the one every current player is on, and the wrong claim had scoped it out of the test plan entirely. |
| M4/L4 | LOW | Two guard tests were vacuous: with no anchor candidate stubbed, `FindAnchor` returns null regardless, so `DidNotReceive()` held whether the guard existed or not. | Stubbed `GetHeroHomeSettlementId`; each test now reddens if its named guard is deleted. |
| L1 | LOW | The finalizer's exception filter — the entire safety contract — had no test. A refactor widening the type check would swallow every engine fault out of `SpawnLordParty` with a green suite. | Added `Patch65FinalizerTests` (suppress / propagate / no-exception), mirroring `Patch62MovieReleaseAvGuardTests`. |
| L5 | LOW | The claim that vanilla ships three clans without `initial_home_settlement` (`guardians`, `chosen_of_the_sky`, `freemen`) is **false** — all three sit inside XML comment spans. 95 live `<Faction>` elements by parser, zero missing the attribute; 98 by raw-text regex. | Corrected in five places. |
| L6 | LOW | The apply-batch comment justified placement with "it fires on the daily clan tick", naming only one of the two listeners that reach the target. | Comment now names both, and warns against re-batching on the weaker bound. |
| — | LOW | The Patch65 binding-test doc-comment claimed a renamed target fails silently. | It fails **loudly**: `PatchClassProcessor.PatchWithAttributes` throws `ArgumentException` on a null original and `ReportException` rethrows it as `HarmonyException`. Corrected — the old wording would send crash triage hunting a silent no-op that cannot happen. |

## What the review got wrong

Two of its numbers did not survive independent verification, which is why verify-before-fix is not
optional:

- **M1's count.** A refuter reproduced 18/607. My own first re-check said **12** — my regex spanned
  element boundaries. Parsing the settlement graph properly (`./Components/Village[@bound]`, which is
  nested under `<Components>` and not a direct child) confirmed the review's 18. The lesson cuts both
  ways: the review was right, and what made it right was parsing rather than grepping.
- **M2's scope.** Filed as one missing attribute, with `villager_party_template` rated `[Likely]`.
  The full transform-and-diff found **7**, all engine-read. The review under-counted.

## RCA — why these got through

**M2 is a REPEAT OFFENDER and the lesson was already written down.**
`docs/reviews/lessons/xslt-moduledata.md` already carries "XSLT passthrough silently inherits vanilla
attributes you didn't intend to keep", from Codex Review #227 (Dale culture, 2026-05-26, P1) — same
file, same mechanism — and its *Prevent* clause prescribes exactly the enumeration that would have
caught this. It even flags Rohan as a repeat-offender risk. This is the **third** instance
(Dale → Rohan → Khand). **Why it did not fire:** the work was framed as "point Khand's troops at the
Rhun roster", not as "author a culture override", so neither the rule nor `/new-culture` was
consulted. The rule's trigger was scoped to *authoring* a culture; the bug arrived via *editing* one.

**M1 is a new invariant but a known shape.** `lessons/data-content-cultures.md` already warns that
verifying a property across existing data proves "a coincidence with good hygiene, not an invariant".
Village-culture ↔ bound-parent-culture held 607/607 across the whole map and nothing enforced it. The
id set was derived from a naming convention rather than from `bound=`.

**M3: the correct fact was already in the repo.** `SettlementConversionRecord.cs` and
`ICultureConversionService.cs` both state that `Settlement.Culture` is not engine-saved — the entire
CultureConversion re-apply-on-load mechanism exists *because* of it. The docstring asserted the
opposite from memory. `evidence-over-claims.md` §C did not fire because a `Save-compat:` line reads
as boilerplate rather than as a factual assertion about engine behaviour.

**L5 and the CRLF corruption share one root: trusting a text-level read of a structured file.** The
three-minor-factions claim came from a grep that counted commented-out `<Faction>` elements as live.
The CRLF bug came from `write_text` on a file whose line endings only matter at the byte level.
`tools/taom_schema.py:_read_stripped` exists precisely to prevent the first and `tools/README.md`'s
XML I/O convention precisely to prevent the second — both were already in the repo; neither was
applied to a one-off script or to an investigative grep.

**M4/L4/L1 are the third shipping of a test-that-cannot-fail** (skillspector 2026-06-22,
`rca-validator-silent-scope-2026-08-03`, now here). The existing lesson's banned-pattern list is
syntactic, so a syntactically-normal `DidNotReceive()` reads as compliant; the vacuity here came from
the *arrange*, not from the assertion.

## Lessons appended

- `lessons/xslt-moduledata.md` — the BIND/PASSTHROUGH/N-A enumeration applies to **any edit** of a
  `Culture[@id=…]` template, not only to authoring a new one; plus the mechanical form (lxml
  transform, diff against an already-re-themed sibling). And: any grep over ModuleData that becomes a
  factual claim must be re-run with comment spans stripped, or via a parser.
- `lessons/data-content-cultures.md` — derive a settlement id set from the relationship graph
  (`bound=`, `owner=`), never from an id-prefix convention.
- `lessons/state-lifecycle-save.md` — before writing any `Save-compat:` claim, grep the repo for an
  existing statement about that field and confirm against **both** `[SaveableField]` and
  `AutoGeneratedSaveManager`'s member list; a bare public field may or may not be saved.
- `lessons/testing-qa.md` — the arrange-side form of the vacuous-assertion ban: name the one
  production line whose deletion reddens each guard test.

## Deferred, recorded here

`_unanchorable` second-faction test · battania volunteer-pool value test · offset-preserving comment
blanking in the retag script · Khand name lists (`male_names` / `clan_names`; Dale has the identical
gap) · a `castle_village` culture-invariant check in `validate_moduledata.py` · `lords.xslt`'s 396
`<xsl:attribute name="culture">` lords being invisible to `LANDLESS_CULTURE` (redundant today, since
the `.xml` lord path caught all 18 real cases) · `Patch65`'s statics not reset on
`OnSubModuleUnloaded` · a repo-wide issue for the unguarded `OnGameInitializationFinished`
`PatchCategory` batch.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/lord-spawn-guard.md](../features/lord-spawn-guard.md)

<!-- backlinks-end -->
