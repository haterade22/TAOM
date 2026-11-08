# Codex pre-review: Fords of War pierce damage against cavalry (2026-11-07)

Your role is reviewer ([.ai/roles/reviewer.md](../../.ai/roles/reviewer.md)). This is a review only:

- Do not edit, create or delete any file. Do not stage, commit or stash.
- Do not run `dotnet build`, `dotnet test` or `build.ps1`. A Bannerlord match runs on this machine and needs the CPU. The orchestrator ran the Fords of War and damage model tests (filter `FordsOfWar|CustomBattleDamageModel`: 2,197 passed, 0 failed).
- Read files, `git diff`, and the installed engine's decompiled sources: `pwsh tools/taom-src.ps1 path <Type>` prints the file (cache `C:\Users\mikew\.taom-src\v1.5.6-202611041234`). Engine: Bannerlord v1.5.6.

## The change

This worktree, branch `feat/fords-spear-counter`, base `origin/bannerlord-1.5.x` at `610864ccd`. The change is uncommitted; `git diff` and `git status` show all of it.

| File | Change |
|---|---|
| `Main/Features/FordsOfWar/Domain/MountedPierceRule.cs` | new: `Factor(factor, pierce, victimMounted)` |
| `Main/Features/FordsOfWar/Domain/FordsMatchConfig.cs` | new optional record member `PierceVsMountedFactor = 2f` |
| `Main/Features/FordsOfWar/Config/FordsMatchConfigReader.cs` | reads `pierceVsMountedFactor`, range 0 to 10 |
| `Main/Features/FordsOfWar/Hooks/FordsUpgradeHooks.cs` | `Set` takes the factor; `Reduce` multiplies through `MountedPierce`; `Clear` resets it; a hit counter |
| `Main/Features/FordsOfWar/FordsMissionDirector.cs` | passes the factor to `Set` each tick; `mountedPierce=` on the shutdown log line |
| `Main/_Module/ModuleData/fords_of_war/match.json` | `"pierceVsMountedFactor": 2` |
| `TAOM.Tests/Features/FordsOfWar/...` | `MountedPierceRuleTests` (new), `FordsMatchConfigLoadTests`, `FordsMatchConfigDefaultsTests`, `FordsClientWiringTests` |
| `docs/features/fords-of-war.md`, `docs/features/fords-of-war/balance-log.md` | the rule and its round 4 entry |

Callers outside the diff, from the code graph: `TaomCustomBattleDamageModel.ApplyDamageReductions` (L53) and `ApplyDamageAmplifications` (L59); `FordsOfWarMissionLogic.StartMatch` and `OnEndMission` (they call `FordsMissionDirector.Shutdown`, which calls `FordsUpgradeHooks.Clear`); the `FordsMatchConfig` consumers (`FordsConfigCatalog`, `FordsMatch`, `FordsMatchPlan`, `FordsSetupChoices`, test fixtures that use `with`).

## The maintainer's decisions

- Mike, 2026-11-07: in Fords of War, cavalry and horses take extra pierce damage ("x2 or x3"), so that spearmen, pikemen and archers counter cavalry. He said it must apply to this game mode only.
- The factor is 2, the `match.json` key `pierceVsMountedFactor`. The orchestrator chose 2 under Mike's standing word that it decides Fords of War balance.

## Questions to answer with evidence

Answer each with file:line in TAOM and the quoted engine lines. A claim you could not check is UNVERIFIED.

1. **Mode isolation.** Can the factor reach a battle that is not a Fords of War match: a campaign battle, a plain Custom Battle, another Custom Game mode (Long Defeat), a Fords match after it ended or failed to start, a second match after the first? Trace `FordsUpgradeHooks.Set`, `Clear`, and every caller of `FordsUpgradeHooks.Reduce`.
2. **Damage type.** When `TaomCustomBattleDamageModel.ApplyDamageReductions` runs, is `AttackCollisionData.DamageType` the blow's real type, for melee and for missiles (arrows, bolts, javelins, the towers' arrows and rocks)? Does `TaomAgentApplyDamageModel.BluntByVanillaRule` match the engine's own blunt correction at that point? Quote `MissionCombatMechanicsHelper` and the base model.
3. **The mounted test**, `attack.IsVictimAgentMount || attack.DoesVictimHaveMountAgent`. Which victims does it include and which does it leave out: a horse with or without a rider, a rider, a creature mount (warg, spider, elk, mumak), a soldier on a creature's seat or howdah, a hero on a horse? Can one blow be multiplied twice (once on the rider, once on the mount)?
4. **Order in `FordsUpgradeHooks.Reduce`:** the own-tower-shot zero, the early return when no match is set, hero damage taken, the defence upgrades, then `LongDefeatCombatHooks.Reduce`. Where in the engine's damage pipeline does `ApplyDamageReductions` run (before or after armour), and does any later clamp change the effect of a factor of 2?
5. **Threads.** Blows can run off the main thread. Are the `volatile float` and the `Interlocked` counter correct here?
6. **Paths that skip the model.** Scripted damage (`CustomAttacksUtils.TakeDamage`: signature strikes, creature bites, troll brute force), tower damage (`DestructableComponent` and Patch131), horse charges. Name each, and say whether it reaches the factor and whether it should.
7. **Config.** The reader's range, the default, the shipped value, and `ShippedMatch_EqualsTheBuiltInDefault`. Does any consumer build `FordsMatchConfig` positionally and now get the default without noticing?
8. **Docs.** Does any sentence in the two changed docs state engine behaviour that the code or the engine contradicts?

## Output

Findings ranked CRITICAL, HIGH, MEDIUM, LOW. Each finding: file:line, the evidence quoted, a failure scenario, and a fix. Then the questions above that found nothing, each with one line of evidence. End with a verdict.
