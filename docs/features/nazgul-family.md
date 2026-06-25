# Nazgûl Family Suppression

## Overview

The Ringwraiths — the Witch-King of Angmar and the seven named Nazgûl — never have a spouse,
parents, or children. They are undead servants of Sauron, bound to the Nine Rings; a wife and
heirs make no sense for them. This is enforced by a `MarriageModel` override (they can never marry,
so they can never have a spouse and therefore never have children) plus a defensive clear-on-load
for saves created before the feature existed.

## Why This Exists

Over campaign time Bannerlord's `RomanceCampaignBehavior` marries off eligible NPC lords and they
have children. The Nazgûl are `occupation="Lord"` Mordor heroes, so without intervention the engine
would happily wed a Ringwraith to some Mordor noblewoman and give the Lord of the Nazgûl a family.
The user's requirement: the Witch-King and all Nazgûl have **no father, mother, spouse or children**.

Two facts established during research make the fix small and targeted (decompile-verified, v1.4.6):

- **No predefined family reaches the engine.** `lords.xslt` rebuilds every lord with `<xsl:copy>` +
  explicit `<xsl:attribute>` only, dropping any vanilla `father`/`mother`/`spouse`. The wraiths start
  single, parentless.
- **TAOM's initial child generation already excludes them.** `initial_child_generation.json` lists
  `mordor` under `excluded_cultures`, and `HeroCreator.CreateChild` never sets a parent link anyway
  (the "parent" is only an appearance/culture template). So no initial children are generated for the
  wraith clan.

That leaves **runtime marriage** as the only family source — and every marriage path in the engine
funnels through `MarriageModel`. Blocking marriage is therefore the complete fix.

## Architecture

```
SubModule (registers model + behavior)
   ├── TaomMarriageModel : DefaultMarriageModel   ← blocks marriage for wraiths (no spouse ⇒ no children)
   │      └── INazgulRegistry                      ← "is this StringId a wraith?"
   └── NazgulFamilyBehavior : CampaignBehaviorBase ← clear-on-load (pre-feature saves only)
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

- **`NazgulFamilyBehavior`** runs once on `OnSessionLaunched`. For each wraith it nulls
  `Spouse` (the setter is two-way: it clears the partner's side too, matching the engine's own
  `KillCharacterAction.cs:147` cleanup), `Father`, `Mother`, and severs each child's parent link
  before clearing the wraith's `Children` list. In a **new campaign** the marriage block means the
  wraiths never form these links, so this is a no-op there — it exists only to retro-clean a save
  made before the feature shipped.

This follows ADR-002 (thin entry points: the model + behavior hold no logic beyond the wraith check
and the boundary mutations) and the testable decision (`INazgulRegistry`) is a pure service.

## Configuration

None. The wraith roster is a **fixed lore set**, not a tuning knob, so it is a compiled constant in
`NazgulRegistry` rather than a config file — there is nothing to misconfigure. There is no MCM toggle:
"the undead have no family" is a lore-correctness fix, always on.

The eight wraith lord StringIds (verified against `taom_lord_skill_sets.xml` — the lords whose
`skill_template` is `taom_witch_king_skills` / `taom_nazgul_skills` — and their display names in
`lords.xslt`):

| StringId | Name |
|----------|------|
| `lord_1_15` | Witch-King of Angmar |
| `lord_1_155` | Nazgûl, The Dark Marshall |
| `lord_1_16` | Nazgûl, The Knight of Umbar |
| `lord_1_28` | Nazgûl, The Betrayer |
| `lord_1_38` | Nazgûl, the Undying |
| `lord_1_48_1` | Nazgûl, the Tainted |
| `lord_1_48_2` | Nazgûl, the Shadow of Northmen |
| `lord_1_48_3` | Nazgûl, the Shadow of Umbar |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/NazgulFamily/INazgulRegistry.cs` | Interface — `IsWraith(stringId)` |
| `Main/Features/NazgulFamily/NazgulRegistry.cs` | The 8-id wraith roster (compiled constant) |
| `Main/Features/NazgulFamily/Models/TaomMarriageModel.cs` | `DefaultMarriageModel` override — wraiths unmarriageable |
| `Main/Features/NazgulFamily/NazgulFamilyBehavior.cs` | Clear-on-load for pre-feature saves |
| `Main/Features/NazgulFamily/NazgulFamilyIoC.cs` | Registers `INazgulRegistry` (singleton) |
| `Main/SubModule.cs` | Registers the model + behavior (near the age/pregnancy models) |
| `Main/IoC.cs` | Calls `NazgulFamilyIoC.RegisterNazgulFamilyFeature` |
| `TAOM.Tests/Features/NazgulFamily/NazgulRegistryTests.cs` | 17 cases — the 8 ids, non-wraiths, null/empty, prefix-no-match, case-insensitivity |

## Dependencies

- `DefaultMarriageModel` / `MarriageModel` (TaleWorlds) — the override base + the two gated methods.
- `Hero.AllAliveHeroes` / `DeadOrDisabledHeroes`, `Hero.Spouse/Father/Mother/Children` setters (v1.4.6).
- `IModLogger` (TAOM core) for the clear-on-load summary line.

## Tests

`NazgulRegistryTests` (17 cases) covers the pure decision: all 8 wraith ids → true; Sauron / Boromir /
Galadriel / a Dunland lord → false; `lord_1_48` (prefix of a wraith id) → false; null / empty → false;
case-insensitive match. The model + behavior are thin entry points (boundary mutation of sealed `Hero`),
covered via game per ADR-008; their correctness was independently decompile-verified — see below.

## Verification

An independent decompile pass (v1.4.6) confirmed COMPLETE on the four load-bearing questions:
1. `MarriageAction.ApplyInternal:11` hard-gates on `IsCoupleSuitableForMarriage` before assigning the
   spouse; all three upstream marriage paths (AI / player-courtship / lord-offer) are also gated.
2. No engine path assigns an initial spouse to a familyless predefined lord; initial children are
   doubly blocked (mordor excluded + `CreateChild` sets no parent link).
3. `IsCoupleSuitableForMarriage` (direct) + `IsSuitableForMarriage` (via `Hero.CanMarry`) are exactly
   the methods the marriage paths call.
4. The clear-on-load null-clearing is safe — two-way `Spouse` clear matches the engine's own
   `KillCharacterAction`; no clan/heir invariant is broken (heir/leader logic keys on clan membership,
   not family links). One benign, unreachable-in-practice residual: the ex-partner lands in the
   wraith's read-only `ExSpouses` list (a historical record, never read by marriage/heir logic).

## How-To

**Add or remove a wraith:** edit the `WraithIds` set in `NazgulRegistry.cs` (use the lord StringId)
and add a `[DataRow]` to `NazgulRegistryTests`. No other change is needed — the model + behavior read
the registry.

**Existing-save note:** a wraith that already married in a pre-feature save is retro-cleaned on the
next load. For a guaranteed-clean wraith roster, a new campaign is ideal (the marriage block means
the links never form), but the clear-on-load makes an old save correct too.

## Related

- [lord-perk-review.md](lord-perk-review.md) — the lord stats/SkillSet review tooling.
- `docs/reference/engine/campaign-object-graph.md` — Hero/Clan family relationships.
