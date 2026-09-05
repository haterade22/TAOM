# RCA: Dwarven War Ram review (2026-08-28)

Feature: the Dwarven war ram, issue [#515](https://github.com/haterade22/TAOM/issues/515).
Review: `/deep-review` (6 agents) plus a `/review-codex` adversarial pass.

**Three P1s were caught before shipping (one deferred by decision), and two of them are the same
mistake made twice: an engine action was chosen by what it LOOKS like instead of by what it is TYPED,
and engine-driven actions were assumed inert because our own code never fires them. The second
instance happened while FIXING the first, which is why the preventive action below is written as a
rule rather than a note.**

The feature is otherwise clean. Standards, efficiency, tooling and 9 of 11 data-flow chains passed
with no findings, and 7 of the 8 engine claims the design rests on were confirmed against the
installed v1.4.8 DLLs.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | **P1** | The behaviour tree's attack clip was `act_horse_rear`, typed **`actt_rear`**. The `horse` monster_usage set the ram inherits declares `rear_action="act_horse_rear"`, so the engine fires it itself on a damaged mount; and `Agent.Mount` reads `mountAgent.GetCurrentActionType(0) == ActionCodeType.Rear` and **refuses the mount while it is true**. The BT forces the action onto channel 0 every 6s, so the ram would go briefly **unmountable in combat**, on the one TAOM mount deliberately built to be player-rideable | Animation / engine dispatch | The clip was chosen for how it reads (a ram rears and slams) and verified only for **existence and binding**: it resolves in `as_horse`, it is a real clip, it is not `act_none`. The action's TYPE was read and even written into the code comment (`typed actt_rear`) without asking what the engine does with that type | Verify an action's **type semantics**, not just that it resolves. See the rule added below |
| 2 | MED | The two "unused" profile slots pointed at `act_horse_strike_front` / `act_horse_strike_back`, documented as having no consequence. `ElephantLikeCombatProfile.IsAttack` ORs across **all four** slots, and those two actions are engine-driven via the inherited horse `monster_usage_strikes` table, so an engine-driven strike silently satisfied the ram's "am I already attacking" gate and suppressed its own attack for that tick | Cross-file data flow | Reasoned about which slots the BT **fires** and stopped there. Never traced who else **reads** them. The comment said "never fires for this creature", which was true and irrelevant | Same rule: a slot that is never written can still be read |
| 3 | MED | The `MOUNTED_DWARF` war-ram allowlist relaxes `default_group="HorseArcher"` as well as `Cavalry`, with no test and no current consumer | Validation / test coverage | The brief that produced the allowlist named `Cavalry` explicitly and left `HorseArcher` to be inferred from `_MOUNTED_GROUPS`. The implementer made a defensible call and documented the `Cavalry` half only | Two tests added: the positive (`HorseArcher` + ram is clean) and the negative (`HorseArcher` without a ram still errors) |
| 4 | LOW | `docs/features/no-mount-cultures.md` still asserted Erebor is a no-mount culture; `docs/reference/feature-map.md` had no war-ram row despite CLAUDE.md requiring one per feature | Documentation | Both were listed in the plan's follow-through section and then not executed. Writing the new docs displaced updating the old ones | Both fixed. No systemic rule; this is ordinary follow-through |
| 5 | INFO | TAOM's `SubModule.xml` never declares `<DependedModule Id="LOTRLOME_Armory"/>` although `troops_erebor.xml` references Armory-defined items | Cross-module | **Pre-existing.** `troops_erebor.xml` already referenced Armory dwarf armour before #515; load order has always been accidental | Recorded, not fixed. Widening #515 into a module-manifest change is scope creep; worth its own issue |
| 6 | P1 (deferred) | No mount-lock means a dismounted dwarf can take an ordinary horse and enemy cavalry can take a riderless ram, contradicting the "dwarves ride rams and nothing else" framing | Runtime authorization | The no-mount-lock decision was argued only from "the player should be able to ride his own ram" and never from the reciprocal direction | **Deferred by explicit decision.** Recorded here and as a CHANGELOG known limitation. Closing it needs target-specific authorization on both `Agent.CheckSkillForMounting` and the AI `CanAgentRideMount` path |
| 7 | **P1** | The REPLACEMENT clip `act_horse_strike_front` was also wrong: `actt_mount_strike` is `ActionCodeType.MountStrike = 52`, inside the `StrikeBegin=48..StrikeEnd=52` band that `Agent.IsInBeingStruckAction` reads as BEING STRUCK, and the clip is named `horse_hit_from_front` | Animation / engine dispatch | Fixing finding 1 reused the very method that caused it: the replacement was verified for its type NAME and that it resolved, not for what the engine DOES with that type. One mistake, made twice | Now `act_horse_kick` (`actt_kick`, `ActionCodeType.Kick = 28`), the horse rig's only genuinely offensive action |
| 8 | P2 | Party-template minimum sums drifted up (+4 on the culture default, +1 per clan), so expected roster size rose slightly even though the max sums held at 2000 | Data | "Max sums unchanged" was checked and asserted; minimums were not, because the sizing doc emphasises the max ceiling | Ram stacks moved to min 0; all eight sums now match HEAD exactly |
| 9 | P3 | `Main/IoC.cs` and `Main/SubModule.cs` were written with doubled carriage returns, turning two one-line changes into 233 and 1709-line rewrites | Tooling | An ad-hoc edit helper decoded with `utf-8-sig`, which preserves CRLF, then wrote with a CRLF newline setting, doubling every carriage return | Repaired. When scripting an edit, either normalise to LF on read or write with `newline=''`, never both |

## Root-cause pattern: existence-checking an engine symbol instead of type-checking it

Findings 1 and 2 are one failure wearing two hats.

TAOM already has strong machinery for "does this action exist?": `ActionIndexCache` resolves eagerly,
`AnyUnresolved()` catches drift to `act_none`, and the mission behavior logs it at startup. That
machinery answers **"is this name real?"** and the feature used it correctly. Both bugs live entirely
outside what it can see.

The question neither the machinery nor I asked is: **who else drives this action, and what does the
engine do when it is active?** For `act_horse_rear` the answers were "the engine does, on every
damaged mount" and "it blocks mounting". For `act_horse_strike_*` they were "the engine does, via the
inherited strikes table" and "it satisfies our own busy-check".

**Inheriting `monster_usage="horse"` is what made this reachable, and it is the same property that
made the feature cheap.** Every other TAOM creature mount has a bespoke usage set, so its
`act_<creature>_*` actions are fired by TAOM and nothing else; picking one for a BT attack is safe by
construction. The war ram is the first mount whose action vocabulary is **shared with the engine**,
so for the first time "our code does not fire this" stopped implying "nothing fires this".

The reusable form: **a reskin inherits the donor's behaviour, not just its animations.** Cheapness and
coupling are the same coin.

## Why each review agent missed or caught these

| Agent | Finding 1 | Finding 2 | Note |
|---|---|---|---|
| Standards | missed | missed | Correctly out of scope: ADR compliance cannot see action-type semantics. It verified the file was well-formed, which it was |
| **API compatibility** | **CAUGHT** | n/a | The only pass that decompiled `Agent.Mount` and connected `ActionCodeType.Rear` to the `actt_rear` typing. It was explicitly briefed to attack the clip choice, which is why it went looking |
| Efficiency | missed | missed | Verified the caches resolve eagerly and cost nothing per tick. Both true, and orthogonal |
| Completeness | missed | missed | Found the two doc gaps (#4). Test-coverage view cannot see a wrong-but-resolving constant |
| **Data flow** | missed | **CAUGHT** | Traced who READS the config constants rather than who writes them, which is exactly what finding 2 needed. It reached the wrong conclusion about the clips being hit-reactions, but the mechanical finding was right and actionable |
| Tooling | n/a | n/a | Caught #3 independently. Scoped to Python and XML I/O |

**The orchestrator's own error is worth recording:** I read `Agent.Mount` earlier in the same session,
while establishing that `CanRide` gates mounting, and saw the `ActionCodeType.Rear` check in the
quoted source. I was looking for the `CanRide` clause and did not register the adjacent `Rear` clause.
Reading the right function is not the same as reading it for the right question.

## Preventive action

Added to `docs/reviews/lessons/animation-skeleton.md`:

> ### Verify an engine action's TYPE and its other drivers, not just that it resolves
>
> `ActionIndexCache` + `AnyUnresolved()` answer "is this name real". They cannot answer "what does the
> engine do when this action is active" or "who else fires it". Before binding any action to a
> behaviour tree, establish three things: its `action_types.xml` **type**; whether the creature's
> `monster_usage` set names it in a **verb slot** or table (which means the engine fires it too); and
> whether the engine **branches on that type** anywhere (`ActionCodeType`, `AgentActionFlag`).
>
> **Why missed:** the war ram's attack was chosen for how it reads and verified only for existence.
> `act_horse_rear` is typed `actt_rear`, is the inherited `horse` usage set's own `rear_action`, and is
> checked by `Agent.Mount` as `ActionCodeType.Rear` to refuse mounting. Separately, two profile slots
> our tree never fires were still read by `IsAttack`, and the engine drove them via the inherited
> strikes table.
>
> **Prevent:** this risk is specific to a mount that **inherits a vanilla `monster_usage`**. A creature
> with a bespoke usage set owns its whole `act_<creature>_*` vocabulary, so nothing else fires it; a
> reskin shares the vocabulary with the engine. When reviewing a reskin, grep the inherited usage set
> for every action the feature binds, and treat any hit as engine-driven. Prefer `actt_mount_strike`
> for attacks; never a type that appears in a verb slot.
>
> **Source:** issue #515, `docs/reviews/rca-war-ram-2026-08-28.md`.

No new feedback memory: this is a subsystem rule, not a workflow one, and the lessons file is the
canonical place for it.

## What was verified and held

Recorded so a future reader does not re-litigate the design:

- `base_monster="horse"` inherits `Flags`, `Weight`, `HitPoints`, `ActionSetCode`, `MonsterUsage`,
  `NumPaces`, `FamilyType`, every bone index, both capsule blocks and all twelve rein fields, copied
  before any child attribute can override. `<Flags>` is only reassigned if the node carries a `<Flags>`
  **child element**, which the ram's self-closing element does not.
- `CanRide` gates `CheckSkillForMounting` only. The spawn path checks `HorseComponent.IsRideable` with
  no flag reference, so AI spawn was never blocked.
- ~~`SetInitialAgentScale(0.01f * BodyLength)` fires only on the mount agent.~~ **REFUTED by the Codex
  pass, corrected 2026-08-28.** `EquipmentIndex.ArmorItemEndSlot` and `EquipmentIndex.Horse` are the
  same value (10), the scale block in `BuildAgent` has no `IsMount` guard, and `BuildAgent` runs for
  the rider as well as the mount with the Horse item still in the rider's spawn equipment, so any
  `body_length` other than 100 scales the RIDER too. The ram ships at 100 (identity), so it is
  unaffected. `BodyLength` has three managed readers and none of the others has a side effect.
  This entry is left struck through rather than deleted: it was published as verified in three docs
  before the refutation, and finding 7 below is about exactly this failure mode.
- Harness fit compares `HorseComponent.Monster.FamilyType`, and `SPInventoryVM` is its only managed
  enforcement.
- `as_dwarf_warrior` carries 203 `act_horse_*`/`act_ride*` rows, set-for-set identical to vanilla
  `as_human_warrior`. Of the 80 action references in the `horse` usage set, the 4 absent from
  `as_dwarf_warrior` are equally absent from `as_human_warrior`, so they are mount-side-only by design.
- `as_horse_map` and `as_horse_town_and_village` both exist and key off the literal `as_horse` id, so
  the campaign-map and settlement variants come for free and the elephant "Crash #4" class does not
  apply.
- Patch47's absence is an inference by analogy, not a proof. The spider hand-authored its Monster
  fields to match vanilla; the ram uses the engine's own `base_monster` copy, and vanilla `horse_2`
  is a shipped precedent for exactly that. **A ridden-death in-game test is still owed.**

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/mumakil.md](../features/mumakil.md)
- [docs/features/war-ram.md](../features/war-ram.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
