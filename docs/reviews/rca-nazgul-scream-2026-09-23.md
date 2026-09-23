# RCA: the Nine's SCREAM, SignatureStrikes with N signatures (#645), 2026-09-23

## Top-line

#645 turned SignatureStrikes from one hard-wired signature into a `signatures` list and gave the
Nine a SCREAM with a generated sound. The seven-lens `/deep-review` found no engine incompatibility
(27 members verified), no data-flow gap in the signature-index contract or the cooldown stamps, no
performance cost worth a change, and one design simplification. It found one MED in new code (an
engine float into a native call with no finiteness gate), one MED in docs (a sentence the change
made false), a set of LOW doc, comment and placement misses, and a REPEAT of a lesson written for
this very test file one week earlier. A convergence pass over the fixes found one more LOW, a
citation the finding-3 fix left half-updated. All were fixed in the same session.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED (data flow) | `StrikeSoundPlayer` passed `attacker.GetEyeGlobalPosition()` straight into native `Mission.MakeSound` with no finiteness check, ahead of the runner's own centre gate. `CustomAttacksUtils.IsBlowGeometrySafe` keeps the same values out of `MakeSound`. What a non-finite position does inside native is unproven: the spider AV that guard was written for was later traced to `HandleBlowAux` (`rca-spider-dismount-on-hit-2026-06-15.md`), so this gate is a defence, not a crash fix. | Engine float into a native sink | The runner's existing centre gate was kept, but the NEW native call was written as a plain call: the NaN rule in `csharp-architecture.md` names decision gates, casts and new inputs to existing gates, not a float handed to native. | Gated (`none (non-finite position)`); lesson in `adapters-taleworlds-api.md`; see "Feedback memories" for the rule. |
| 2 | MED (completeness) | `combat-mechanics.md:40` said a signature overhead always floors the struck agent; the Nine's overhead is a Scream with `knockDown: false`. The model's own comment said a sweep was the only knock-back true; two domain comments said "hero id or race" and "from the impact". | Doc drift inside the changed semantics | The sweep updated the knock-back half of the same line and missed its knock-down parenthetical; no grep for code comments describing signature semantics. | Fixed; the misc lesson from #644 (grep for what a change invalidates, not only its wording) applies. |
| 3 | LOW (engine) | The doc's morale sentence gave `0.5 * tier + 1` for everyone. A campaign hero uses `1.5 * (0.5 * (level / 4 + 1) + 1)`, and a Custom Battle character is a `BasicCharacterObject` whose resistance is 1 (`BasicCharacterObject.cs:268-271`), so there the full 25 applies. | Unverified engine claim | Carried from the plan's research of the campaign-troop path without reading `GetMoraleResistance` on both classes. | Corrected with citations; `evidence-over-claims.md` §C. |
| 4 | LOW (engine) | The doc said Native supports `.ogg` and `.wav` only; Native's header lists them, and TAOM ships `.mp3` variations elsewhere. | Overstated claim | Paraphrase hardened into "only". | Reworded to what the header says. |
| 5 | LOW (data flow) | The MCM hint promised the struck-foe stagger everywhere; `TaomCombatMechanicsModel` is campaign-only. | Player-facing text drift | The #605 hint had the same limit for the knockdown and the new text copied its shape. | Hint states the Custom Battle caveat. |
| 6 | LOW (XML) | The scream's `module_sound` sat inside the Troll section of `module_sounds.xml`. | Placement | Inserted after the entry used as the shape reference instead of reading the file's section layout. | Own `LOTR/Mordor/Nazgul` section. |
| 7 | LOW (completeness) | The regex in `ShippedSignatureStrikesConfigTests` became two literal dash characters when the class was rewritten with the Write tool, which decodes `\uXXXX`. | Tool gotcha, REPEAT | The lesson exists (`build-tooling-workflow.md`, from `rca-signature-strikes-2026-09-16.md` finding 8, the same file). The dash scan this session covered markdown and JSON, not the rewritten `.cs`. | Escapes restored by a byte-level script; the lesson gains the wholesale-rewrite trigger. |
| 8 | LOW (completeness) | No cross-links from `nazgul-family.md` / `dread-aura.md`; the issue's file table named a helper the plan proposed and the build dropped; `StrikeSoundPlayer` had no direct test. | Completeness | The plan's file list went into the issue before implementation and was not re-read after the design changed. | Links added, issue corrected, two engine-free tests. |
| 9 | Design (applied) | The service parsed direction and kind fail-closed but defaulted an unparseable origin to `Impact`. | Silent default route | Written for convenience in the service; the provider already reverts, so no shipped config could reach it. | Origin parsed fail-closed; RED test `Evaluate_ProfileWithUnknownOrigin_ReturnsNull`. |
| 10 | LOW (engine, convergence pass) | The morale sentence corrected for finding 3 still cited only `SandboxBattleMoraleModel.cs:95-98` for the division; a Custom Battle runs `CustomBattleMoraleModel` (`:72-75`, through `TaomCustomBattleMoraleModel`, `SubModule.cs:1264`), and both floor the divisor at 1. No number changed. | Incomplete fix | The fix split the resistance values by game mode but kept the one-mode citation beside them: finding 2's shape again, half of a sentence updated. | Both models cited and the floor stated. |

Unverified and owed to the smoke, not findings: that native answers -1 for an unregistered sound
name (the engine's own null id; the smoke misspells a name on purpose), the first `.ogg` a TAOM
module sound has ever played, and whether the nazghul voice set carries the `Yell` the fallback
uses.

## Root-cause pattern

Findings 1, 2, 5 and 10 share a shape: **the change's NEW surface inherited nothing from the rules
its OLD surface already obeyed.** The ring centre was gated, the new sound call was not; the
knock-back sentence was updated, the knock-down half of the same line was not; the effect was
described, the campaign-only caveat was not; the resistance values were split by game mode, the
citation beside them was not. Each time the fix was to apply to the new line what the adjacent old
line already did.

## Why each lens missed or caught these

- Standards: passed; its rules are structural.
- Engine: caught 3 and 4 by reading the engine behind each doc claim; verified every new member.
- Data flow: caught 1 and 5, tracing the eye position to its native sink.
- XML: caught 6 and ran every gate on the new sound.
- Efficiency: found no cost worth a change (the per-hit path is one reference-keyed probe).
- Completeness: caught 2, 7 and 8.
- Design: proposed 9 and confirmed the rest optimal (the sound path, the double name parse, the
  hero-set idiom, the test growth).
- Convergence (the standards lens over the fix hunks only): caught 10 by re-reading the engine
  behind each corrected sentence, and confirmed the origin change cannot drop a row the provider
  emits.

## Feedback memories to codify

- `docs/reviews/lessons/adapters-taleworlds-api.md`: an engine float handed straight into a native
  call is a gate too.
- `docs/reviews/lessons/build-tooling-workflow.md`: the existing `\uXXXX` lesson now records its
  second occurrence and the wholesale-rewrite trigger.
- Recommended, not done here: `csharp-architecture.md` "Engine-Float Decision Gates" names four
  categories and says to widen the scope when a new one appears. A float into a native sink is a
  fifth; it was caught in review rather than shipped, so the rule change is left for the next
  harness pass rather than made inside a feature commit.
