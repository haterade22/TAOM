# RCA: Custom Battle ran none of TAOM's Combat Mechanics damage rules (#788, 2026-10-09)

## Top-line

In Custom Battle, TAOM's Combat Mechanics damage rules never ran: skill crush-through, monster auto-crush, orc
shield-crush, creature cleave and slice-through, creature stagger immunity with the per-race stagger threshold,
weight-driven charge knockdown, missile shield-penetration flags and shield damage, plus Signature Strikes knockdown and
knock-back and the MCM horse charge penetration slider. The campaign registers them in `TaomCombatMechanicsModel`, on
the `CampaignGameStarter` only. The campaign model cannot simply be registered in Custom Battle: its base,
`SandboxAgentApplyDamageModel`, reads `DefaultSkillEffects.MountedWeaponDamagePenalty`, which goes through
`Campaign.Current`, so the first mounted hit outside a campaign throws (`SandboxAgentApplyDamageModel.cs:641-644`).

The fix (the maintainer's choice) is one hand-wired twin, `TaomCustomBattleDamageModel : CustomAgentApplyDamageModel`,
registered in `SubModule.RegisterCustomBattleModels`. It replaces `TaomCustomBattleCreatureDamageModel`, which the
CreatureBandits module declared, and makes the same hook calls in the same order as the campaign model, except two
campaign-only parts: the career passives (they read heroes) and the Refuge reduction (it reads a party). A
`ModuleRunner` fault in CreatureBandits can no longer drop the Custom Battle damage model for the session.

The deep review ran six lenses in two waves (Standards, Engine compatibility, Data flow; Efficiency, Completeness,
Design). It found no CRITICAL or HIGH defect and no engine incompatibility (the damage-path types are byte-identical
on v1.5.4 and the installed v1.5.5). Every finding was checked against the code or the v1.5.5 decompile before it was
fixed.

## History

Combat Mechanics shipped campaign-only in `62bfc87b4` (2026-07-02, #320). The Custom Battle damage slot first got a
TAOM model in `d469914ab` (2026-09-28, #692), and that model was extended three times without Combat Mechanics
(`a498e357e`, `70320bbe1` for #730, `92839bc52` for #735). No recorded decision made Combat Mechanics campaign-only
on purpose. `rca-custom-battle-bannerless-factions-2026-10-02.md` listed race combat rules absent in Custom Battle as
worth an issue; none was filed until #788.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | (bug) | #788: Custom Battle ran none of the Combat Mechanics damage rules | A campaign-only GameModel left Custom Battle on vanilla | REPEAT: the 2026-09-26 lesson ("ask for every model rule whether it must hold in Custom Battle") relies on judgment, and each later change to the Custom Battle slot added only its own feature | The twin, plus a parity gate (row 2) that fails when the campaign model gains a rule the twin lacks |
| 2 | MED | The new parity test compared only a hand-listed override set and five hand-listed hook types, so a 14th campaign override or a new hook type would pass silently | A guard narrower than the class of bug | Written from the twin's present shape | `CallSequence` counts every call into TAOM's assembly, minus a named `CampaignOnly` set; `Source_OverridesExactlyWhatTheCampaignModelOverrides` runs without the game. Both proved red-first |
| 3 | LOW | Rule text said "base first" for all 13 overrides; five ask the hooks first | Doc claim | Copied a summary, not the code | Reworded to "same base and hook calls in the same order" |
| 4 | LOW | Engine statements in the new docs: the synthetic creature blows "route through" the deciders (they ask no damage-model method), an "unmeasured" knockdown-resistance gap (the formula is identical), what reads the flat penetration values, what re-enters the stagger threshold, a charge step on knock-back | Doc claims from intent | Written before the engine read | Each corrected against the v1.5.5 decompile |
| 5 | LOW | `race-abilities.md` said Custom Battle reads resistance on every charge; the culture-doctrine and Race Abilities Custom Battle baselines did not say they predate #788 | Mode-dependent docs outside the type-name sweep | The doc sweep searched the deleted type's name, not the mode name | Corrected; the lesson's Prevent line adds the sweep by mode name |
| 6 | LOW | Comments and catalogue rows named only the campaign model; the crush path was described as read-only though it draws `MBRandom` on a multi-threaded callback | Stale text | Not part of the original edit list | Corrected |
| 7 | LOW | Two redundant tests; no container test for `SignatureStrikesIoC`, which Custom Battle start now resolves unguarded | Test hygiene | | Deleted; `SignatureStrikesContainerWiringTests` added, proved red-first |

## Root-cause pattern

**A mode gap fixed by judgment comes back.** This class has shipped before: #610's charge multiplier (RCA finding 2),
the banner bearers in Custom Battle (#687, lesson `gamemodels-services.md`), and the bannerless-factions RCA. Each
prevention was a rule to remember. This change adds a gate instead: the twin's parity with the campaign model is
pinned by IL and by source, and `FeatureModulesTests` stops a module from shadowing a hand-wired slot.

## Why each lens caught what it caught

- **Standards** found the rule wording (3), a stale ordinal and a duplicate test (7).
- **Engine compatibility** verified 15 usages, found the doc statements (4), and confirmed the v1.5.4 and v1.5.5
  damage-path types are identical.
- **Data flow** traced Custom Battle start to the slot and found the narrow parity guard (2).
- **Efficiency** found no cost above the campaign's, and the unsynchronized `MBRandom` draw (6).
- **Completeness** found the mode-dependent docs (5) and the missing container test (7).
- **Design** showed that a namespace-based classifier would still miss direct service calls, and gave the
  assembly-based one (2).

## Not applied, and owed

- **The Custom Battle stat twin** lacks the elephant, spider and mumakil mount lock that the campaign stat model
  applies; in-game impact unverified. A separate question for the maintainer, not part of #788.
- **Retiring the Custom Battle module path** (`FeatureModuleHooks`): no module declares a Custom Battle model now. The
  maintainer's decision.
- **The `MBRandom` draw on the multi-threaded defend callback**: the campaign's long-standing behaviour, now also in
  Custom Battle; consequence unverified.
- **In-game checks**: the feel of crush, cleave, stagger and charge knockdown in Custom Battle, and new baselines for
  the culture-doctrine A/B, the Signature Strikes smoke and the Race Abilities runs.
- **`/engine-bump`**: built and tested on the installed v1.5.5 against the v1.5.4 pin (maintainer's choice);
  `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` fails until the bump.

## Lessons

`docs/reviews/lessons/gamemodels-services.md`: the #788 entry (a Custom Battle twin mirrors the campaign model's rules
by gate, not by memory; sweep docs by mode name).
