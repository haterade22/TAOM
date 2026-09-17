# RCA: career perk wiring audit and its fixes (#613), 2026-09-17

## Top-line

After #611 (the cavalry mount bonuses that multiplied a property nobody read), every career
passive consumer and every ability buff field was walked with one question: where does the ENGINE
read this? Three defects fell out (typed resistance masks that were not typed, an ammo refill
that skipped thrown weapons, and no diagnostics for the mission-side perks at all), plus a fourth
that got its own issue (#614: the 49 stealth pips ride a hook that is the player spotting others).
The fixes were built test-first and then put through the five-agent review, which found two more
instances of the same class inside the fix itself. Both were fixed the same session. CareerSystem,
DevConsole, CombatMechanics and binding filters: 793 green; full suite below.

## Findings from the audit (fixed under #613)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Four "+N% blunt resistance" pips and one "+8% cut resistance" pip authored `attack_type_mask="Blunt"` / `"Cut"`; the `[Flags]` enum had only Melee / Ranged and `ParseEnum` fell back to `All` in silence, so the pips resisted every hit. | Config validation, unknown string takes the widest reading | The rule ("validate any string field the consumer branches on") was written for the `type` attribute of the same element and applied there; the sibling attribute two tokens later was left on the generic `ParseEnum`. A test even pinned the fallback as intentional. | `AttackTypeMask` gained the kind axis; masks parse by name (a digit string and an unknown name fail); unknown warns and lands on `None`, which matches no hit; the shipped-XML test refuses `None` and pins the five kinded pip ids. |
| 2 | MED | The Ammo refill gated on `MissionWeapon.IsAnyAmmo()`, which is "consumable and not a weapon" (arrows, bolts); a javelin stack carries weapon flags and was skipped. Nine of the twenty Ammo pips sit on three javelineer careers ("never out of javelins"). | Engine predicate narrower than its name | `IsAnyAmmo` reads as "any ammunition"; nobody opened `WeaponComponentData.IsAmmo` (`:99-107`). | `IsAnyConsumable()`; IL pin. |
| 3 | MED | No mission-side perk left any evidence: the only in-game proof was a tooltip "Career" line on the campaign ExplainedNumbers. That is how #611's phantom lived three months. | Observability | Diagnostics were never in the feature's definition of done. | `taom.career_perks` and the `[CareerPerks]` log lines; the feature doc's "Testing a perk" section names what each type leaves, including the types that leave nothing. |

## Findings from the review of the fix

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 4 | HIGH (compat + data flow) | The new hit mask read `AttackCollisionData.DamageType` raw. Vanilla's own damage math corrects it in a local it never writes back (`MissionCombatMechanicsHelper.GetAttackCollisionResults:200`): a bare-hand hit, a hit off the weapon's attach bone, a kick or bash, fall damage and a horse charge are Blunt whatever the struct says. A "blunt resistance" pip would have missed a trample. | Engine value corrected in a local the model never sees | The field's name and type matched the need; the correction sits one call earlier in the same chain and nobody read the consumer of the raw field before trusting it. Same shape as #611: a value that exists is not the value the engine uses. | `AttackTypeMaskMatch.ForHit(…, bluntByRule)`; the model computes the rule with vanilla's own public helper (`IsCollisionBoneDifferentThanWeaponAttachBone`) and the same four flags. |
| 5 | HIGH (data flow, UNVERIFIED native, pre-existing since 2026-06-26) | The refill pushed an amount above an unchanged `ModifiedMaxAmount` through the native `Agent.SetWeaponAmountInSlot` after build. The engine treats the max as the cap everywhere else (a pickup merges only up to it, `Agent.cs:3496`), and vanilla's own extra-ammo perks never do this: `InitializeMissionEquipment` calls `MissionEquipment.SetAmountOfSlot(slot, amount, addOverflowToMaxAmount: true)` before build, raising the max with the amount (`SandboxAgentStatCalculateModel.cs:209`). A full stack, the normal spawn state, may never have taken the refill. | Wrong mechanism for a capped engine value | The June wiring chose the first setter that compiled; it was shipped "Not-tested: requires a live game" and the smoke never happened. The #613 fix inherited the mechanism and changed only the gate. | The refill now rides `TaomAgentStatCalculateModel.InitializeMissionEquipment` through `CareerAmmoApplier` and `SetAmountOfSlot(…, true)`, vanilla's seam and vanilla's call; the old `OnAgentBuild` refill is gone (the binding test refuses its return, so it cannot apply twice). |
| 6 | MED | The `[CareerPerks]` dedupe dictionaries on the singleton stat service were never cleared, so a second battle with unchanged values would have logged nothing at spawn, the exact re-test loop the doc recommends. | Singleton state without a session reset | The dedupe was designed for one battle. | `ResetDiagnostics()` from `CareerPerkMissionBehavior.OnEndMission`, beside the ability and buff clears. |
| 7 | LOW | The report probed only the six kinded hit masks, so a kind-only pip's zero against a kindless hit was invisible. | Tool blind spot | Same oversight as 4. | Plain Melee and Ranged added to the probe list. |
| 8 | LOW | Per-hit `terms +=` allocates two or three short strings on the DEBUG lane for every hit a passive touched. | Efficiency | Accepted as is: a `StringBuilder` allocates too, and the line exists only when a passive applied. | None. |
| 9 | Note | `CareerPerkMissionBehavior` was 228 lines before this change and is 188 after (the refill left); `TaomAgentStatCalculateModel` 128. Both under the ADR-002 ceiling now. | Line count | | |

## Root-cause pattern

Findings 1, 4 and 5 are one shape, and it is #611's shape again: **the value TAOM reached for existed
and had the right name, and was not the value the engine uses.** `attack_type_mask` was parsed, but
by a parser that widened on failure. `DamageType` was on the struct, but vanilla corrects it in a
local. `SetWeaponAmountInSlot` set the amount, but the engine caps at a max it left alone. In each
case the fix was to find the engine's OWN consumer or producer of that value and use its path.

## Why each agent missed or caught these

| Agent | 4 (DamageType) | 5 (ammo max) | 6 (dedupe) |
|---|---|---|---|
| Standards | Missed, correct scope. | Missed. | Missed. |
| Compatibility | **Caught**: read the consumer chain one call earlier. | Missed: verified `SetWeaponAmountInSlot` exists. | Missed. |
| Efficiency | Missed. | Missed. | Missed. |
| Completeness | Missed. | Missed. | Missed. |
| Data flow | **Caught** independently. | **Caught**: traced `BoostAmmo` for a full stack and compared every engine call site. | **Caught**: asked whether the singleton's dictionaries are ever cleared. |

Two independent catches of 4 and one of 5 and 6, all by the agents that read engine bodies rather
than signatures. The compatibility prompt's "open the caller" instruction (from #606) is what found 4.

## Lessons appended

- `lessons/adapters-taleworlds-api.md`: an engine struct field with the right name may be
  corrected in a local one call earlier; read the engine's consumer before trusting the field
  (`AttackCollisionData.DamageType`), and set a capped engine value through the engine's own
  producer (`SetAmountOfSlot(…, true)`, not `SetWeaponAmountInSlot` above the max).
- `lessons/testing-qa.md`: a diagnostic's own definition of done includes the second run; a
  singleton dedupe resets at the session boundary.

## Verification

- `--filter "FullyQualifiedName~CareerSystem|FullyQualifiedName~DevConsole|FullyQualifiedName~GameModelOverrideBinding|FullyQualifiedName~CombatMechanics|FullyQualifiedName~MissionBehaviorLifecycle"`: 793 passed, 0 failed.
- Full suite: quoted in the CHANGELOG entry at commit time.
- Owed in game: `taom.career_perks` in a campaign and in a battle; a blunt-resistance career under a
  mace, a sword and a horse; a javelineer's stack at spawn reading above its printed max.
