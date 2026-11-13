# Codex pre-review: The Long Defeat opening split, the release signal (#1128)

**Role:** reviewer (`.ai/roles/reviewer.md`). Read only: edit no file, run no build that deploys, start no game. Report findings; fix nothing.

**Scope:** the uncommitted working-tree change in this checkout (`E:\repos\taom-ld-split`, branch `feat/ld-defender-split`), base `4cafca4ab`. Read it with `git diff 4cafca4ab`. 16 files: 5 C# in `Main/`, 4 C# tests, `tools/tests/test_run_long_defeat.py`, `tools/ingame_checks/1079.toml`, and docs (`docs/features/long-defeat.md`, `docs/features/coop-interop.md`, `docs/reference/feature-map.md`, the RCA row 14 in `docs/reviews/rca-long-defeat-split-2026-11-12.md`, a lesson in `docs/reviews/lessons/adapters-taleworlds-api.md`).

## What the change does

The opening split (pushed on trunk in `725725c35`) takes four defender formations off AI control in each calm and orders them to places from the scene. It hands them back to the siege AI at the first contact of a wave. The maintainer decided: the split orders are opening orders only, and after the first contact the player and the AI command as before.

The pushed code took contact as "a siege lane's `SiegeLane.GetDefenseState()` rose above its value at the hold". This change reads `TeamAISiegeComponent.QuerySystem`'s `LeftCloseAttackerCount`, `MiddleCloseAttackerCount` and `RightCloseAttackerCount` instead (`LongDefeatBattlefieldAdapter.ReadCloseAttackers`). The domain rule `DefenderSplit.Contact` is unchanged (contact when any value rises above its value at the hold, or the list length changes). The rest renames `lanes` to `close` in code, logs and the check detail.

## Evidence already gathered (read it yourself before you rely on it)

- First game run, pushed build: `E:\Temp\long-defeat-runs\20261113-005049\` (its `taom_debug_*.log`). Lines 686 and 692: wave 1 starts and contact is logged in the same second; lines 3335 and 3343: the same in wave 2. The gate group stood 55 m to 64 m from its place at the release.
- Second game run, this change plus temporary debug lines (now removed): `E:\Temp\long-defeat-runs\20261113-012012\`. Contact at 219 s into the hold in wave 1 and 47 s in wave 2; both `split` checks pass. The `[DEBUG-g7c1]` lines show the gate group's path distance falling from 101.7 m to 3.4 m.
- Engine sources (v1.5.7, the installed DLLs' decompile): `C:\Users\mikew\.taom-src\v1.5.7-202611101605\TaleWorlds.MountAndBlade.SiegeLane.cs` (`DetermineLaneState`), `...SiegeQuerySystem.cs` (`LocateAttackers`, the `QueryData` refresh), `...TeamAISiegeComponent.cs` (the static `QuerySystem`). For any other engine type run `pwsh tools/taom-src.ps1 path <Type>`.

## Questions to answer (each with file:line evidence, marked observed or inferred)

1. Is the close attacker count a sound "first contact" signal for a Long Defeat wave? Name every condition that raises it, and say whether any of them happens before an attacker reaches a wall, a gate or a breach (spawn points, siege tower crews, an attacker on an inside face for a reason other than a breach, a ram at the gate).
2. Can the baseline read at a fresh split be stale? `QueryData` values refresh on a 5 s period. A fresh split happens in the calm right after a wave clears. Can the cached count at that moment still include the last wave's attackers, and what does that do to the next wave's contact?
3. Can `TeamAISiegeComponent.QuerySystem` be null, or a previous mission's object, at the hold or during the wave? What does `DefenderSplit.Contact` then do (an empty list against a three-item list counts as contact)?
4. Is any path left where the hold now never ends (no contact all wave), and is that path closed by the existing closers (`CloseSplit` at the clear and at the run's end, the stalemate charge)?
5. Do the renamed log and check words (`close`) still match every reader: `tools/ingame_checks/1079.toml`, `tools/run_long_defeat.py` (`SPLIT_MADE`, `owes_split`), `tools/ingame_verdict.py`, and the tests?
6. Are the engine claims in the changed docs and comments accurate: the doc's release paragraph and "In game" paragraph, RCA row 14, the lesson, and the multiplayer paragraph in `long-defeat.md` (`Module.AddMultiplayerGameMode`, `DefineGameNetworkMessageTypeForMod`, `MultiplayerMissions.OpenSiegeMission` having neither `MissionCombatantsLogic` nor `MissionSiegeEnginesLogic`)?
7. Anything else in the diff that is wrong: a dropped behaviour, a stale comment, a test that no longer tests what its name says.

## Output

Findings first, ranked by severity (CRITICAL, HIGH, MEDIUM, LOW), each with file:line, the evidence, and observed or inferred. Mark missing engine evidence UNVERIFIED. Then a one-line verdict. No fixes, no patches.
