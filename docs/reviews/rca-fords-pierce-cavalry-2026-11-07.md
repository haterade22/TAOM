# RCA: Fords of War, pierce damage against cavalry (/deep-review, 2026-11-07)

## Top-line

The change: in a Fords of War match, a horse or a rider takes `pierceVsMountedFactor` (2) times a pierce blow, so that
spearmen, pikemen and archers counter cavalry (Mike, 2026-11-07: "Cavalry and Horses suffer extra piercing damage (x2 or
x3)", for this game mode only). Worktree `E:/repos/taom-fords-spear`, branch `feat/fords-spear-counter`, base `610864ccd`.

- **Tier 1** (a GameModel seam, engine calls on the blow path). First wave: Lens 5 Data flow and Lens 2 Engine
  compatibility at `max`, Codex at `ultra` (the output header names gpt-6-astra). No CRITICAL or HIGH, so no second wave:
  Lens 1 Standards, 3 Efficiency, 4 Completeness and 6 Design are SKIPPED BY TIER, Lens 7 XML and the Tooling lens are NOT IN
  SCOPE (no XML, no script), and Step 4 is N/A.
- **Result:** 0 CRITICAL, 0 HIGH, 3 MEDIUM, 6 LOW, 0 API incompatibilities, 3 UNVERIFIED native internals. Every confirmed
  finding is fixed or documented below. Codex found no defect in mode isolation, the damage type, the order of the factors,
  threads or the config.
- **Proof:** the filtered tests after the fix (FordsOfWar, the damage models, the scripted attacks, the architecture gates):
  3,330 passed, 0 failed, 5 skipped. The fix re-review is under "Fix re-review". Nothing ran in game: round 4 of the round
  robin measures the rule.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Scripted pierce blows never met the rule: `CustomAttacksUtils.TakeDamage` (warg and spider bites, a signature strike's splash, the dwarf, troll and elephant-like attacks) writes `InflictedDamage` with `DamageCalculated = true`, and the engine registers that blow without its damage model. The army upgrades and hero factors missed them too, before this change. (Codex) | Engine assumption | The rule was placed where the modelled blows pass, and no one listed the other writers of `InflictedDamage`. | `FordsScriptedBlows.Damage` sends a scripted blow through the registered model in a Fords match only (Mike: "those things need to reach the damage model", scope "Fords of War only"); `FordsPierceHooks.ScriptedDamage` applies the rule with the blow's own type, because the empty weapon reads as blunt. A wiring test pins the single write of the damage and its place before `RegisterBlow`. Lesson in `gamemodels-services.md`. |
| 2 | MED | A pierce blow blocked by a shield was doubled: the engine runs the model on a blocked blow, where the damage is the shield's (`Mission.GetAttackCollisionResults`, helper L214 to 216). (Lens 5, Lens 2 F1) | Engine assumption | The rule read `DamageType` and the victim only; the blocked case uses the same seam with other damage. | `FordsPierceHooks.Factor` returns 1 on `AttackBlockedWithShield`; the wiring test pins it. The hero and armour-upgrade factors still scale shield damage, as before. Same lesson. |
| 3 | MED | The doc's list of who the rule hits named only Rohan's riders; every mount counts, the beasts too (wargs, spiders, elk, camels, war rams, the war elephant and the mumak), ridden or riderless, while howdah and seat crew do not. (Lens 2 F2) | Undocumented behaviour | The list was written from the case in hand. | The doc and the balance log name every mount kind. Mike confirmed that the mumak and the war elephant count (2026-11-07: "that is fine"). |
| 4 | LOW | The factor resets at mission teardown, about 25 s after the result, not at the result. (Codex, Lens 5) | Lifecycle | `Clear` runs from `Shutdown`, as for the other factors. | Documented; no blow in the result window changes the result. |
| 5 | LOW | The balance log said "19 of the 25" cavalry troops; the script read only the first roster of each troop. (Codex, UNVERIFIED) | Unverified count | A count written from a partial read. | Sentence removed. |
| 6 | LOW | The docs omitted crossbow bolts, the engine's reactions to a bigger blow (shrug-off, dismount, rearing), and which blows the blunt rule leaves out; one said "hilt hit". (Lens 5, Lens 2 F4, F5) | Doc accuracy | Written before the engine paths were read. | Rewritten from the engine lines the lenses quoted. |
| 7 | LOW | Two comments that list what shares `BluntByVanillaRule`, and the lists of what the Custom Battle model adds, did not name the new rule. (Lens 2 F6, Lens 5) | Stale cross-reference | A shared helper's comment lists its callers. | Updated in `TaomAgentApplyDamageModel`, `TaomCustomBattleDamageModel`, `combat-mechanics.md` and `gamemodel-registry.md`. |
| 8 | LOW | The first fix grew `FordsUpgradeHooks` to 175 lines; the ADR-002 gate failed. | Gate | Two features in one hooks class. | Split into `FordsPierceHooks` (the rule) and `FordsScriptedBlows` (the scripted path); `FordsUpgradeHooks` is 132 lines. |

**Process note:** one Lens 2 lookup started `ilspycmd` over every module DLL while a match played; the lens stopped the
processes itself. The fix re-review brief limits lookups to one type at a time.

## Fix re-review

Codex at `high` (gpt-6-astra, prompt `docs/reviews/codex-rereview-fords-pierce-cavalry-2026-11-07.prompt.md`) and Lens 2
at `high` read the fix diff. Both confirm findings 1 and 2 fixed in source and finding 4 documented. They found no engine
incompatibility (Lens 2: 14 verified, 1 UNVERIFIED: that native sets `AttackBlockedWithShield` on every shield contact).
They refute a double application: the scripted collision carries an empty weapon, so `BluntByVanillaRule` makes the model's
pierce factor 1 and only `FordsPierceHooks.ScriptedDamage` applies it. With bone index -1 the engine's `AttackInformation`
never reads bone data, and the engine builds the same empty-weapon attack itself for charge and fall damage.

| # | Sev | Finding | Fix |
|---|---|---|---|
| R1 | LOW | The balance log's undo (set the factor to 1) left scripted blows going through the model. (Codex) | The undo names the second step: revert the `FordsScriptedBlows.Damage` call. |
| R2 | LOW | The rounding test (37.5 to 38) could not tell half to even from away from zero. (Codex) | 62.5 to 62 added. |
| R3 | LOW | `MountedPierceRule`'s summary said a blocked blow is unchanged; a scripted blow that a shield only reduces is still doubled, because it lands on the soldier. (Lens 2 F-A) | Wording: the rule skips a blow the engine stops with a shield. Kept the behaviour, which fits "the rule is for the soldier and the horse". |
| R4 | LOW | `CreatureBanditDamage`'s summary said scripted blows never reach it. (Lens 2 F-B) | "outside a Fords of War match". No Fords data file names a creature-bandit or bandit-troll id, so its blunt factor cannot meet a scripted bite in a match. |
| R5 | LOW | The doc named only some scripted attacks. (Lens 2 F-C) | It names all six callers of `TakeDamage`; the dwarf and troll blows are blunt, so they get the upgrades and hero factors only. |

Balance note (Codex): a signature strike's splash takes the main hit's modelled damage and goes through the model again, so
the hero factor reaches both; the balance log's "Sauron's splash x3" already says so.
