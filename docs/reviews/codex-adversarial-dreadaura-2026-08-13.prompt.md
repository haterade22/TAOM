# Adversarial review: TAOM DreadAura (focused)

Bannerlord v1.4.8 mod feature, new and uncommitted. Be adversarial: assume bugs exist, find them.

IMPORTANT BUDGET RULE: keep total context small. Do NOT paste large decompiled bodies. Do NOT recursively list or read `E:\Decompiled_Bannerlord\`. Read at most 6 TAOM files plus at most 3 targeted decompiled methods, and quote at most 15 lines from any one of them. A previous run of this review died by exhausting the context window on decompilation. Prefer a short, sharp answer over a thorough one.

## What the feature does

Nazgul and Sauron drain morale from nearby enemy agents in battle. A `MissionLogic` ticks per frame; on a staggered per-source pulse it calls `Mission.GetNearbyEnemyAgents(pos, radius, team, buffer)` and then `agent.ChangeMorale(-drain)` on eligible targets. It adds NO GameModel override: it CALLS `BattleMoraleModel.CalculateMoraleChangeToCharacter` for tier/hero resistance.

Already found and fixed by an internal review, OUT OF SCOPE: (a) unbounded elapsed-time catch-up on resume, (b) the master toggle gating source registration, (c) MCM radius/rate snapshotted rather than read live. A FOURTH instance of that root-cause family is in scope.

## Read these files only

Main/Features/DreadAura/DreadAuraService.cs
Main/Features/DreadAura/Hooks/DreadPulseRunner.cs
Main/Features/DreadAura/Hooks/DreadPulseScheduler.cs
Main/Features/DreadAura/Hooks/DreadAgentGate.cs
Main/Features/DreadAura/Hooks/DreadMissionGate.cs
Main/Features/DreadAura/Hooks/DreadSourceTracker.cs

## Answer exactly these five questions, briefly

Q1 THREAD SAFETY. `CommonAIComponent.OnTickParallel` mutates `_morale` on a parallel job. `DreadPulseRunner` writes the same field via `ChangeMorale` from `OnMissionTick` on the main thread. Is that a real data race in practice? Does vanilla's own `AgentMoraleInteractionLogic` do the same thing from a main-thread event, making this precedented rather than novel? Answer with a verdict and one paragraph.

Q2 CAMPAIGN BLEED. `BattleEndLogic.OnEndMission` marks agents with `GetMorale() < 0.01f` as routed via `IAgentOriginBase.SetRouted`. Decompile just enough to say: what does SetRouted do to the post-battle roster? Can this feature cause troop LOSS in a party that the player cannot see or contest? Verdict plus the specific mechanism.

Q3 MISSION TYPES. `DreadMissionGate.IsEligible` requires: Campaign.Current non-null, !GameNetwork.IsSessionActive, CombatType == Combat, and (IsFieldBattle || IsSiegeBattle || IsSallyOutBattle). Name any mission type where this is WRONG in either direction: a battle where the aura should run but will not, or a non-battle where it will run. Specifically consider the pre-battle Deployment phase and modded tournaments. Is `Mission.Mode` needed as well?

Q4 THE ONE THING. Read the six files and name the single most likely remaining bug, with file, line, and a concrete failure scenario (inputs and state producing wrong output or a crash). If you genuinely find nothing, say "no further findings" rather than inventing one.

Q5 BALANCE. The design claims a tier-3 human routs after about 28 s of continuous exposure at 12 m and an elven hero never does, because the engine regenerates morale at +0.4/sec up to half a troop's starting morale and the elven hero's effective drain lands at 0.33/sec. Sanity-check that arithmetic from `DreadAuraService.ComputeDrain`. Then judge whether one Nazgul trivially wins a 200v200 field battle once vanilla's own morale contagion and the 30%-retreating formation-flee threshold compound on top.

## Output format

For each of Q1 to Q5: a heading, a one-word verdict where applicable, then at most one short paragraph. Then a FINDINGS table: file, line, severity P1/P2/P3, failure scenario, minimal fix. No preamble, no summary of what you read.
