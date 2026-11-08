# Codex fix re-review: Fords of War pierce damage against cavalry (2026-11-07)

Your role is reviewer ([.ai/roles/reviewer.md](../../.ai/roles/reviewer.md)). This is a review only:

- Do not edit, create or delete any file. Do not stage, commit or stash.
- Do not run `dotnet build`, `dotnet test` or `build.ps1`. A Bannerlord match runs on this machine and needs the CPU. The orchestrator ran the filtered tests after the fix (FordsOfWar, the damage models, the scripted attacks, the architecture gates: 3,330 passed, 0 failed, 5 skipped).
- Read files, `git diff`, and the installed engine's decompiled sources: `pwsh tools/taom-src.ps1 path <Type>` prints the file (cache `C:\Users\mikew\.taom-src\v1.5.6-202611041234`). Look up one type at a time; never decompile every module DLL. Engine: Bannerlord v1.5.6.

## What you reviewed before

Your pre-review (`docs/reviews/raw/codex-prereview-fords-pierce-cavalry-2026-11-07.md`, prompt beside it) found:

- **MEDIUM:** scripted pierce blows (`CustomAttacksUtils.TakeDamage`: warg and spider bites, signature strike splash, creature attacks) write `InflictedDamage` with `DamageCalculated = true` and never reach the damage model, so the rule missed them.
- **LOW:** the factor resets at mission teardown, about 25 s after the result, not at the result.

Two Claude lenses also found that a shield-blocked pierce blow was doubled (the engine runs the model on a blocked blow, where the damage is the shield's).

## The fix (this worktree, branch `feat/fords-spear-counter`, uncommitted; `git diff` and `git status` show all of it)

- **Shield blocks:** `FordsPierceHooks.Factor` returns 1 when `collision.AttackBlockedWithShield`.
- **Scripted blows (Mike, 2026-11-07: "those things need to reach the damage model", scope "Fords of War only"):** `CustomAttacksUtils.TakeDamage` now sets `blow.InflictedDamage = FordsScriptedBlows.Damage(attacker, victim, damage, damageType, in attackCollisionDataForDebugPurpose)` after it builds the collision data. Outside a match (`!FordsUpgradeHooks.IsLive`) it returns the damage unchanged. In a match it builds `new AttackInformation(attacker, victim, WeakGameEntity.Invalid, in collision, in MissionWeapon.Invalid)`, calls `MissionGameModels.Current.AgentApplyDamageModel.CalculateDamage(in attack, in collision, damage)`, then `FordsPierceHooks.ScriptedDamage(modelled, damageType, victim)` applies the pierce rule with the blow's own type and rounds with `Math.Round` (half to even).
- **The split (ADR-002, hooks classes under 150 lines):** the rule moved from `FordsUpgradeHooks` into `FordsPierceHooks`; the scripted path is `FordsScriptedBlows`. `FordsUpgradeHooks.Set` forwards the factor, `Clear` calls `FordsPierceHooks.Clear()`, `Reduce` multiplies by `FordsPierceHooks.Factor(in attack, in collision)` after its `_west == null` return. The shutdown log reads `mountedPierce={FordsPierceHooks.Hits}`.
- **The LOW:** documented, not changed (the docs say the factors stay set until teardown).

## Questions (answer each with file:line evidence; missing engine evidence is UNVERIFIED)

1. **Double application.** On a scripted pierce blow, `CalculateDamage` runs `FordsUpgradeHooks.Reduce`, which calls `FordsPierceHooks.Factor` with the scripted collision. The design relies on `BluntByVanillaRule` (empty weapon) making that call return 1, so only `ScriptedDamage` applies the factor. Confirm or refute from the collision data `GetAttackCollisionDataForDebugPurpose` produces and from `BluntByVanillaRule`.
2. **The constructed `AttackInformation`.** Read the v1.5.6 constructor. With `MissionWeapon.Invalid`, `WeakGameEntity.Invalid` and the debug collision data (bone index, victim mount flags, missile flags): can it throw, read a null, or fill a field the TAOM model chain (`TaomCustomBattleDamageModel`, `TaomAgentApplyDamageModel`, `RaceAbilityHooks`, `CreatureBanditDamage`, `LongDefeatCombatHooks`, `FordsUpgradeHooks.Amplify` and `Reduce`) reads wrongly?
3. **What else the model does to a scripted blow.** List each step of the model chain that changes the damage for an empty-weapon blunt-read blow in a Fords match (army upgrades, hero factors, blunt-specific scaling, `IsDamageIgnored`, general modifiers). Is any change unintended? For example, does any step treat the blow as a fall, a kick or a bash and scale it, or zero it?
4. **Side effects.** Does calling the model from `TakeDamage` double count telemetry or counters, or re-enter anything (the blow is registered right after)? `TakeDamage` runs on the main thread.
5. **Mode isolation.** Can `FordsScriptedBlows.Damage` change a scripted blow in a campaign battle, a plain Custom Battle or after a match ends? `IsLive` is `_west != null`.
6. **Shield blocks.** Is `AttackBlockedWithShield` the right flag for every blocked pierce blow, missiles included? Does a blocked blow still keep the hero and armour-upgrade factors (intended: yes, unchanged)?
7. **Anything else** in the fix diff that is wrong: the tests, the docs (`docs/features/fords-of-war.md` "Pierce against cavalry" and "Scripted blows", `balance-log.md` round 4 entry), rounding, the counter.

## Output

Per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), location, failure scenario, evidence. Then a one-line verdict per question above. Confirm or refute both earlier findings as fixed.
