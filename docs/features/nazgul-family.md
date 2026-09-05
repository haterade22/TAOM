# Nazgûl Family Suppression

## Overview

The nine Ringwraiths — the Witch-King of Angmar, Khamûl the Easterling, and the seven other Nazgûl —
never have a spouse, parents, or children. They are undead servants of Sauron, bound to the Nine Rings;
a wife and heirs make no sense for them. This is enforced in two layers: a **data strip** in
`characters/heroes.xslt` removes the vanilla family seed so a new campaign starts them family-free,
and a `MarriageModel` override blocks any future runtime marriage (no spouse ⇒ no children). A
clear-on-load behaviour severs the family on saves created before the data strip.

## Why This Exists

The wraiths can acquire family two ways, and both are addressed:

- **Predefined family (the seed).** Vanilla `heroes.xml` wires the nine wraiths into a self-contained
  family graph at the **Hero** level: the Witch-King (`lord_1_15`) is married to `lord_1_16` with
  children `lord_1_155`/`28`/`38`; Khamûl (`lord_1_48`) is married to `lord_1_48_1` with children
  `lord_1_48_2`/`48_3`. TAOM's `heroes.xslt` transforms these Hero records, so **stripping
  `spouse`/`father`/`mother` there removes the family before the engine ever sees it** — the wraiths
  start single and parentless on every new campaign. (This is the data layer the original feature
  missed: it cited `lords.xslt`, which transforms `NPCCharacter` *definitions* that carry no family
  attributes — family is Hero-level, in `heroes.xml`.)
- **Runtime marriage.** Over campaign time `RomanceCampaignBehavior` marries off eligible NPC lords.
  The Nazgûl are `occupation="Lord"` heroes, so without intervention the engine would re-wed a Ringwraith.
  Every marriage path in the engine funnels through `MarriageModel`, so blocking it there is complete.

A third potential source — TAOM's initial child generation — is already a non-issue: it excludes both
wraith cultures (`mordor` for `lord_1_15`/`155`/`16`/`28`/`38`, `dolguldur` for the `lord_1_48*`
cluster) in `initial_child_generation.json`, and `HeroCreator.CreateChild` never sets a parent link
anyway (the "parent" is only an appearance/culture template).

## Architecture

```
characters/heroes.xslt   ← DATA STRIP: removes spouse/father/mother from the 9 wraith Hero records
SubModule (registers model + behavior)
   ├── TaomMarriageModel : DefaultMarriageModel   ← blocks FUTURE marriage (no spouse ⇒ no children)
   │      └── INazgulRegistry                      ← "is this StringId a wraith?"
   └── NazgulFamilyBehavior : CampaignBehaviorBase ← clear-on-load (pre-strip saves only)
          └── INazgulRegistry
```

- **`TaomMarriageModel`** overrides exactly the two methods the engine's marriage paths call:
  - `IsCoupleSuitableForMarriage(a, b)` → false if either is a wraith. This is the **hard chokepoint**:
    `MarriageAction.ApplyInternal` (MarriageAction.cs:11, v1.4.6) consults it and returns before
    `firstHero.Spouse = secondHero`, so **no** marriage path — AI-NPC, player courtship, or
    lord-offers-you-a-marriage — can wed a wraith.
  - `IsSuitableForMarriage(hero)` → false for a wraith. Reached via `Hero.CanMarry()` (Hero.cs:1942),
    the per-hero gate in the NPC marriage loop and the offer loop.

  Everything non-wraith falls through to vanilla `DefaultMarriageModel`. Clan-level members
  (`IsClanSuitableForMarriage`, `ShouldNpcMarriageBetweenClansBeAllowed`) are deliberately **not**
  overridden — wraiths share the Mordor clan with non-wraith lords, so the per-hero/per-couple gates
  are the correct granularity.

- **`characters/heroes.xslt`** is the primary mechanism. Each of the nine wraith Hero templates already
  rebuilds the Hero stripping `text` (and `faction` for the dolguldur cluster); the feature adds
  `spouse`/`father`/`mother` to that exclusion, matching the convention the other ~150 lord templates
  in the file already follow. After the strip, a new campaign seeds the wraiths with no family at all.

- **`NazgulFamilyBehavior`** runs once on `OnSessionLaunched` as the legacy-save fallback. For each
  wraith it nulls `Spouse` (the setter is two-way: it clears the partner's side too, matching the
  engine's own `KillCharacterAction.cs:147` cleanup) and clears its `ExSpouses` residual (via reflection
  on the private `_exSpouses`, mirroring the engine's full sever), removes the wraith from each ex-parent's
  `Children` (the `Father`/`Mother` setters are asymmetric on null), and severs each child's parent link
  before clearing the wraith's `Children`. Because all nine wraiths (incl. Khamûl, `lord_1_48`) are in
  the registry, the whole graph is wraith-internal — no non-wraith is widowed or left with a dangling
  child. On a **new campaign** the data strip means there is no family to clear, so this is a no-op there;
  it exists only to retro-clean a save made before the strip.

This follows ADR-002 (thin entry points: the model + behavior hold no logic beyond the wraith check
and the boundary mutations) and the testable decision (`INazgulRegistry`) is a pure service.

## Configuration

None. The wraith roster is a **fixed lore set**, not a tuning knob, so it is a compiled constant in
`NazgulRegistry` rather than a config file — there is nothing to misconfigure. There is no MCM toggle:
"the undead have no family" is a lore-correctness fix, always on.

The nine wraith lord StringIds (verified against the canonical Hero definitions in
`characters/heroes.xslt` + their display names). Note Khamûl (`lord_1_48`) carries a different
`skill_template` than the other eight (he is statted as an orc-warrior in `taom_lord_skill_sets.xml`),
so a skill_template-only scope would miss him — he is canonically the second of the Nine regardless:

| StringId | Name | Culture |
|----------|------|---------|
| `lord_1_15` | Witch-King of Angmar | mordor |
| `lord_1_155` | Nazgûl, The Dark Marshall | mordor |
| `lord_1_16` | Nazgûl, The Knight of Umbar | mordor |
| `lord_1_28` | Nazgûl, The Betrayer | mordor |
| `lord_1_38` | Nazgûl, the Undying | mordor |
| `lord_1_48` | Khamûl the Easterling (second of the Nine) | dolguldur |
| `lord_1_48_1` | Nazgûl, the Tainted | dolguldur |
| `lord_1_48_2` | Nazgûl, the Shadow of Northmen | dolguldur |
| `lord_1_48_3` | Nazgûl, the Shadow of Umbar | dolguldur |

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/characters/heroes.xslt` | **Data strip** — removes `spouse`/`father`/`mother` from the 9 wraith Hero templates |
| `Main/Features/NazgulFamily/INazgulRegistry.cs` | Interface — `IsWraith(stringId)` |
| `Main/Features/NazgulFamily/NazgulRegistry.cs` | The 9-id wraith roster (compiled constant) |
| `Main/Features/NazgulFamily/Models/TaomMarriageModel.cs` | `DefaultMarriageModel` override — wraiths unmarriageable |
| `Main/Features/NazgulFamily/NazgulFamilyBehavior.cs` | Clear-on-load for pre-strip saves |
| `Main/Features/NazgulFamily/NazgulFamilyIoC.cs` | Registers `INazgulRegistry` (singleton) |
| `Main/SubModule.cs` | Registers the model + behavior (near the age/pregnancy models) |
| `Main/IoC.cs` | Calls `NazgulFamilyIoC.RegisterNazgulFamilyFeature` |
| `TAOM.Tests/Features/NazgulFamily/NazgulRegistryTests.cs` | 19 cases — the 9 ids, non-wraiths, null/empty, prefix/superstring-no-match, case-insensitivity |

## Dependencies

- `DefaultMarriageModel` / `MarriageModel` (TaleWorlds) — the override base + the two gated methods.
- `Hero.AllAliveHeroes` / `DeadOrDisabledHeroes`, `Hero.Spouse/Father/Mother/Children` setters (v1.4.6).
- `IModLogger` (TAOM core) for the clear-on-load summary line.

## Tests

`NazgulRegistryTests` (19 cases) covers the pure decision: all 9 wraith ids → true; Sauron / Boromir /
Galadriel / a Dunland lord → false; `lord_1_4` (prefix of `lord_1_48`) and `lord_1_485` (superstring)
→ false (exact-set match, not substring); null / empty → false; case-insensitive match. The model +
behavior are thin entry points (boundary mutation of sealed `Hero`), covered via game per ADR-008;
the heroes.xslt strip + the marriage chokepoints were independently decompile-verified — see below.

## Verification

The two-layer design was decompile-verified (v1.4.6) and deep-reviewed:
1. **Data strip:** vanilla `heroes.xml` seeds the nine wraiths with a self-contained family graph;
   `heroes.xslt` now strips `spouse`/`father`/`mother` from all nine Hero templates, so the engine
   loads them family-free on a new campaign. (The original feature wrongly attributed the strip to
   `lords.xslt`, which transforms `NPCCharacter` defs that carry no family — caught in deep review.)
2. **Marriage block:** `MarriageAction.ApplyInternal:11` hard-gates on `IsCoupleSuitableForMarriage`
   before assigning the spouse, and `Hero.CanMarry():1942` gates on `IsSuitableForMarriage` — the two
   overridden methods cover every AI / player-courtship / lord-offer path.
3. **Child-gen** is a non-issue for both wraith cultures (`mordor` + `dolguldur` excluded; `CreateChild`
   sets no parent link).
4. **Clear-on-load** (legacy saves): two-way `Spouse` clear matches the engine's own
   `KillCharacterAction`; the `ExSpouses` residual the setter leaves is removed via reflection (mirroring
   the engine's full sever) so the encyclopedia shows no ex-spouse; the asymmetric `Father`/`Mother`
   setters are compensated by removing the wraith from the ex-parent's `Children`. All nine wraiths
   (incl. Khamûl) are registered, so the graph is wraith-internal — no non-wraith is widowed. No
   clan/heir invariant is broken (heir/leader logic keys on clan membership, not family links).

The deep review ran 7 dimensions plus a critic pass; the critic caught both the
lords-vs-heroes-xslt premise inversion (item 1 above) and Khamûl's initial omission from the
wraith registry.

## How-To

**Add or remove a wraith:** add the lord StringId to the `WraithIds` set in `NazgulRegistry.cs`, a
`[DataRow]` to `NazgulRegistryTests`, and a `heroes.xslt` Hero template that strips `spouse`/`father`/
`mother` (if the lord has predefined family). The model + behavior read the registry; the strip removes
the data seed.

**Existing-save note:** the `heroes.xslt` strip only affects NEW campaigns (XSLT runs at data load).
A save created before the strip keeps its serialized wraith family until the next load, where
`NazgulFamilyBehavior` severs it (spouse + ex-spouses + parents + children). For a guaranteed-clean
roster from turn one, start a new campaign.

## Related

- [lord-perk-review.md](lord-perk-review.md) — the lord stats/SkillSet review tooling.
- `docs/reference/engine/campaign-object-graph.md` — Hero/Clan family relationships.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/dread-aura.md](./dread-aura.md)
- [docs/modding/lords-and-heroes.md](../modding/lords-and-heroes.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
