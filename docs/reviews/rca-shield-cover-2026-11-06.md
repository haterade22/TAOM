# RCA: shared shield cover rule and troll act_none guard (#1088, #1089, 2026-11-06)

## Top-line

`/deep-review` at tier 1 (Codex at `ultra`; Lens 5 Data flow, Lens 2 Engine compatibility) on the fixes for the two
follow-ups of [rca-dwarf-attacks-2026-11-06.md](rca-dwarf-attacks-2026-11-06.md): the troll's Brute Force played
`act_none` when the Armory lost its action (#1088), and four scripted blows counted a raised shield whichever way it
faced (#1089). No CRITICAL or HIGH, so no second wave. One MEDIUM and six LOW findings, all fixed before the commit.
Codex found no defect; it and Lens 2 left the native shield geometry UNVERIFIED.

## Findings

| # | Sev | Finding | Found by | Fix and proof |
|---|---|---|---|---|
| S1 | MED | The new rule's comment, its test header and three feature-doc changelog lines said every caller "halves" a blocked blow. Only the dwarf attacks and the troll ring halve; the elephant-like attacks keep a quarter (`WarRamConfig`, `ElephantConfig`, `MumakilConfig`, `ElkConfig`, `AnimaliaConfig`) and the signature rings `shieldBlockedMultiplier` (0.25 shipped). The commit body becomes the changelog. | Lens 5, Lens 2 | Each line names its own multiplier; the #1089 body corrected. |
| S2 | LOW | `ShieldCover.Blocks` took `shieldRaised`, but the only caller returns early when no shield is raised and passes `true`; its test could not see the boundary check go. | Lens 5 | The parameter is gone: `ShieldCover.Faces` is the geometry, the boundary owns the raised check. |
| S3 | LOW | The degenerate test rows failed through 0/0 = NaN, not through the minimum-length check, so deleting that check stayed green; one finiteness check was redundant. | Lens 5 | Rows at 1e-5 for the look and the attacker; the redundant check removed. |
| S4 | LOW | `ITrollBruteForceService.IsOffCooldown` lost its last caller through the interface. | Lens 5 | Removed from the interface; the class keeps it for `IsReady` and its tests. |
| S5 | LOW | Comments claimed that playing `act_none` "clears the channel"; native code was not traced that far (it was traced to the anim system's dispatch). | Lens 2 | Reworded to what is proven: a missing action resolves to `act_none` (-1) and plays nothing, and the busy test reads an idle agent's empty channel as busy. Also in the dwarf files. |
| S6 | LOW | `external-mod-couplings.md` pointed at the old line of the 8-parameter `TakeDamage`. | Lens 2 | Line numbers updated (Armory_TAOM finds it by parameter types, so only the doc was stale). |

## Root-cause pattern

S1 and S5 are the same slip: a sentence written from the case in hand (the dwarf's 0.5, the dwarf review's reading of
`act_none`) and copied onto code with other values or unproven engine behaviour. When a rule moves to a shared place,
re-read each consumer's numbers before writing the shared comment.

## Why each reviewer missed what it missed

- **Codex** checked the geometry and the float gates and confirmed the signature strike primary victim is untouched;
  it did not compare the comments with the five configs.
- **Lens 2** read the engine and found the coupling drift and the unproven claim; it flagged S1 as outside its lens.
- **Lens 5** traced each consumer's multiplier and found S1 to S4.

## Deferred

- UNVERIFIED: how native code decides that a melee blow hit a shield (`AttackCollisionData._attackBlockedWithShield`
  is filled natively). The front-half rule never refuses a block against an attacker straight ahead; for an attacker
  60 to 90 degrees to the side it may grant a block that a posed shield would not. Tightening is one constant,
  `ShieldCover.FacingDot`, and the maintainer's call.
- UNVERIFIED: a rider's shield pose in game (the data says it is an `actt_defend_shield` action on channel 1).
- UNVERIFIED: whether Armory_TAOM transpiles `BruteForceRing.Deliver`, `SignatureStrikeRunner.Run` or
  `ElephantLikeAttackTaskBase.Hit` on the removed inline IL; a prefix or postfix still binds. Its source is not on
  this machine.
- FOLLOW-UP (no issue: pre-existing, UNVERIFIED native behaviour; Mike to decide): the troll and the dwarf check per
  action set that a clip exists, but only log when it does not; whether native refuses such an action or clears the
  channel is not traced (`python tools/native_decompile.py` on the anim dispatch, or a game run without the binding).
