# Codex pre-review: Arachnophobia Mode (2026-11-09)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify,
create or delete any file, do not run a build that deploys (`./build.ps1`), and do not commit. Read
`AGENTS.md`, `.ai/policy.md`, `.ai/review-reference.md`, `.claude/rules/harmony-patches.md` and
`.claude/rules/troops.md` first. Every claim needs a source location you read in this run; mark
anything you cannot prove UNVERIFIED. Print `git rev-parse HEAD` at the top of your report.

## The change

Uncommitted in this worktree (`E:/repos/taom-arachnophobia`, branch `feat/arachnophobia-mode`, base
`fc7cd73c4`), issue #1110. Read untracked files directly; use `git diff -- <file>` for the others.

| File | In scope |
|---|---|
| `Main/Features/Arachnophobia/*.cs` | new, every file |
| `Main/Adapters/ITroopMountCatalogAdapter.cs`, `Main/Adapters/TroopMountCatalogAdapter.cs` | new |
| `Main/Features/CreatureBandits/CreatureBandParties.cs` | `DisbandAll` |
| `Main/Features/CreatureBandits/CreatureBanditRules.cs` | `IsBroodMountMonster`, `CanDisbandBrood` |
| `Main/Features/CreatureBandits/CreatureBroodSpawnBehavior.cs` | the block before `ReturnStraysToPatrol` |
| `Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs` | the `hasCreatureItem` line |
| `Main/Features/EliteEmissary/EliteEmissaryService.cs` | the constructor and `GetCultureOffers` |
| `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | the constructor, `GetVolunteerTroopId`, `GetCulturePoolTroopIds` |
| `Main/Features/TaomSettings.cs` | `ArachnophobiaMode` |
| `Main/SubModule.cs`, `Main/IoC.cs` | the Arachnophobia lines |
| `Main/_Module/ModuleData/taom_module_strings.xml`, `Languages/*/std_taom_module_strings_*.xml` | the two `taom_arachnophobia_mcm_mode*` rows |
| `TAOM.Tests/Features/Arachnophobia/*.cs`, `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentHiddenTroopTests.cs` | new |
| `TAOM.Tests/**` others in `git diff` | constructor arguments, setting counts, reflection rows, new test methods |
| `tools/ingame_checks/1110.toml` | new |
| `docs/features/arachnophobia-mode.md` and the doc rows in `git diff` | claims to test |

What the maintainer decided (approved plan, `docs/features/arachnophobia-mode.md` "What Stays"):
the mode is off by default; troop records stay, because saves refer to ids; spider troops already in
a save stay as warg riders and are not converted; Fords of War keeps its offer for now; lore text
and cobwebs stay. Everything else, comments and the feature doc included, is a claim to test.

Engine signatures and bodies: the installed v1.5.6 DLLs are the authority (`pwsh tools/taom-src.ps1
path <Type>`, or the `ilspy` MCP server). Do not paste decompiled engine bodies into your report
beyond the short quote that proves a claim.

## Known suspects (give each a verdict with evidence)

1. **Hook timing.** `ApplyForGame` runs in `SubModule.OnGameInitializationFinished`. For a new
   campaign, a loaded save and a Custom Battle: which objects derived from troop equipment or
   upgrade targets exist before that hook, and does any stay stale after it?
2. **The reload claim.** The code assumes each game rebuilds `NPCCharacter` equipment and upgrade
   targets from XML. Is that true for a second game in one process (campaign to main menu to
   campaign, campaign to Custom Battle)? What does `ReadRiders` find on the second game?
3. **Heroes.** `CharacterObject.IsHero` is `_heroObject != null`. Is it reliable at this hook, in a
   new campaign and on load? Can a write through `BattleEquipments` reach a hero's saved gear by any
   other route, such as a shared `Equipment` or `MBEquipmentRoster`?
4. **The written objects.** Are the `Equipment` objects from `BattleEquipments` the same objects
   that a troop spawn, the party icon, the troop preview, loot and simulation read? Which path can
   still show or award a spider item after the swap?
5. **`UpgradeTargets` consumers.** List every engine and TAOM reader that indexes the array or
   caches its length or contents. Is a shorter or empty array safe for each, and before the first
   UI opens? What about `UpgradeRequiresItemFromCategory` on a troop that lost its mounted target?
6. **The disband.** Is `DestroyPartyAction.Apply(null, party)` safe inside a `DailyTickEvent`
   listener? Test a brood in a player encounter, targeted by another party or quest, with a hero
   prisoner, with `IsCurrentlyUsedByAQuest`, and in a `SiegeEvent`. Is the prisoner guard correct?
   What does the engine do with a brood that holds prisoners for many days?
7. **The Patch93 fall-through.** A brood troop on a warg now takes the vanilla spawn. Trace it into a
   field battle. Which TAOM branches keyed on `CreatureBanditRules.IsCreatureTroop`, or on the
   broods' clan, assume a riderless creature, and can any throw or stall the battle on a husk rider?
   Does a battle started before the first daily tick end normally?
8. **Completeness of the routes.** Which path can still put a hidden troop in play or in a list?
   Check culture fields (`BasicTroop`, `EliteBasicTroop`, militia), party templates in all three
   modules, prisoner recruitment, AI recruitment, garrison conversion, `LordKitDonorAdapter`,
   Enlistment, tournaments, the troop tree UI and the map icon.
9. **The switch changed during a session.** On to off, off to on. `_hidden` and the equipment change
   only at a game start, and the brood tick reads the setting each day. What does each mixed state do?
10. **Failure paths.** A missing item, a missing setter, or an exception part way through the swap:
    is each resulting state safe and reported? Is `riders=0` a true failure in every game type the
    hook runs in (Campaign, story mode, Custom Battle, multiplayer, editor)?
11. **Co-op and the dedicated server.** `orientation.md` has a co-op trap line. Is the disband
    correctly gated? Is `TaomSettings.Instance` non-null on a dedicated server? What differs on a
    client that has the same setting as the server?
12. **The manifest.** Do the regexes in `tools/ingame_checks/1110.toml` match the exact lines the
    code writes?
13. **The tests.** Apply the two-edit test: which test would stay green if its behaviour broke?

## Report format

Findings by severity (CRITICAL, HIGH, MEDIUM, LOW), each with file:line, the failing state, evidence
and a suggested fix. Then one verdict per suspect above. End with what you could not verify.
