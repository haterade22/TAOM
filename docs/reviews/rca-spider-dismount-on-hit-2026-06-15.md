# RCA — Spider rider dismount-on-hit native crash (2026-06-15)

## Top-line

A campaign battle with many spiders CTD'd ~1 min in: native `AccessViolation` reading `0x3` inside
`Agent.HandleBlowAux`. Root cause: the engine's native mounted-**dismount** path for the non-vanilla
spider mount, triggered by a real-melee `CanDismount` hit on a *surviving* mounted Spider Rider — the
same fault class Patch47 routes around on *death*, but on the non-lethal path Patch47 doesn't cover.
Fixed by `Patch48_SpiderHitDismountGuard` (strip `CanDismount` for spider riders). **Confirmed in-game
2026-06-15: no crash.**

## Findings

| # | Sev | Bug | Why missed (first pass) | Preventive action |
|---|-----|-----|-------------------------|-------------------|
| 1 | HIGH (CTD) | Non-lethal `CanDismount` melee hit on a mounted Spider Rider AVs in native `HandleBlowAux` (`0x3`). | Initial report had a **truncated** stack (`TickMissionAux → Mission.Tick`) + a "[Spider][diag] bite flood before the crash" — over-fit to a NaN-from-synthetic-blow hypothesis. The synthetic-blow guard (`CustomAttacksUtils.IsBlowGeometrySafe`) was built on that wrong diagnosis. | Get the FULL stack + the Blow/victim state under the debugger BEFORE committing to a hypothesis on a native AV. The full frame chain (`MeleeHitCallback → … → HandleBlowAux`) + the victim being a mounted rider + the `CanDismount` flag named the real path immediately. |
| 2 | (process) | The NaN guard didn't fix the crash but was framed as the fix. | A truncated native stack + a temporal correlation ("bite flood, then crash") was treated as causation. | Recorded honestly in spider.md + CHANGELOG: the NaN guard is valid defensive hardening, NOT the dismount-crash fix. |

## Root-cause detail (debugger-proven)

Stack: `Mission.MeleeHitCallback → Mission.RegisterBlow → Agent.RegisterBlow → Agent.HandleBlow →
Agent.HandleBlowAux` → native AV `0x3`. Victim = the goblin **Spider Rider** (`IsHuman`, `HasMount=true`,
`MountAgent`=spider, **Health 12** — surviving). Blow: finite geometry, `DamageType=Pierce`,
`InflictedDamage=58`, **`BlowFlag=CanDismount`**. So: a real enemy weapon landed a dismountable hit on the
mounted rider; native `HandleBlowAux` attempted the dismount and deref'd a corrupt pointer in the spider
mount's non-vanilla native structure.

Confirmations:
- **Rider animations are NOT the cause.** `as_goblin_warrior` is an empty override with
  `base_set="as_human_warrior"` → inherits the full human death/fall/dismount surface (13 verbs). The rider
  dies/falls fine on foot.
- **Same family as Patch47.** Patch47's RCA (mounted-*death* AV) equalized every spider data surface and
  concluded the fault is "internal to the engine's mounted-death path for non-vanilla mounts" — unfixable by
  data, route around it. This is that path reached via a non-lethal `CanDismount` hit instead of `Die`.
- **Not our bite path.** `MeleeHitCallback` is the vanilla native melee callback; our synthetic bite calls
  the reflected `Mission.RegisterBlow` directly. The "bite flood" before the crash was normal spider combat,
  coincident with — not causing — the rider being hit.

## Fix

`Patch48_SpiderHitDismountGuard` — Harmony prefix on `Agent.HandleBlowAux` strips `BlowFlags.CanDismount`
when the victim's mount is the spider Monster, so the native dismount never fires. The rider stays on the
locked mount (correct design — `CanAgentRideMount=false`/`MountDifficulty=999` already lock it); the blow's
damage still applies. Spider-only (matches Patch47). Lethal hits remain covered by Patch47's pre-`Die`
dismount. Verified in-game (no crash).

## Root-cause pattern (the generalizable lesson)

**A rideable non-vanilla creature mount needs TWO dismount guards, not one.** The engine's native
mounted-dismount path is broken for non-vanilla mounts and is reached on BOTH:
1. **death** — the rider dies seated (`Agent.Die`) → Patch47 hard-dismounts first (rider dies on-foot).
2. **a non-lethal `CanDismount` hit** — `Agent.HandleBlowAux` → Patch48 strips `CanDismount` (rider stays mounted).

Patch47 alone is insufficient: it only covers death. Any future ridden creature mount (and the **elephant
mahout right now**, which shares the architecture and has the same latent fault — just rarely melee-reached
atop the elephant) needs both. Codified in [[feedback_creature_mount_dismount_guards_death_and_hit]].

## Why the directional-attack deep-review didn't catch this

It didn't run on this code — Patch48 didn't exist yet, and the crash is a *pre-existing* spider-mount native
fault (the old single-bite spider had it too), not a property of the directional-attack C# the review covered.
A static C#/data review cannot find a native dismount AV; only an in-game `CanDismount` hit on a mounted
rider surfaces it. The in-game battle-test is the irreplaceable gate for creature-mount native faults
(consistent with the whole spider/elephant 1.4.6 campaign).

## Verification

- `dotnet build Main/TAOM.csproj` — succeeds. Combat suites green (105). Full suite 3169 green.
- **In-game (2026-06-15): campaign battle, many spiders, enemies meleeing riders — no `0x3` crash** (user-confirmed).
- The Harmony patch is verified by APPLYING it in-game (the crash stopping proves both apply + fix), per `harmony-patches.md`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/spider.md](../features/spider.md)

<!-- backlinks-end -->
