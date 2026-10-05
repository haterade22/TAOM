# RCA: the Mouth of Sauron's gear, reviewed after it shipped (2026-10-04)

**Scope:** commit `799e189e` (Mike, "feat(mordor): add Mouth of Sauron equipment sets and strip upper mesh channels
tool"), which shipped in the v2.0.33 tester build and was tagged in v2.0.34 without a review, plus the follow-ups of
2026-10-04: Mike set the helm's `head_armor` from 55 to 50 in the live Armory, `armour_classes.xml` gained the helm as
`named`, and the Armory snapshot README gained an APPLIED EDIT block. Seven lenses (2, 3, 4, 5, 6, 7 and Tooling;
Opus 5.5 at max effort) in two waves, then one adversarial checker (workflow `wf_614275cb-eec`). 39 findings: the
checker confirmed 24, refuted 3, folded 11 as duplicates and left 1 unverified. No CRITICAL or HIGH; three MEDIUM.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| M1 | MED (MST-1) | `strip_upper_mesh_channels.py` refused only names with a head, eye or mouth token, so hands, arms and body meshes passed and lost their required channels (Saruman's own FBX holds `SK_hands_male_a` and `SK_full_body_saruman`, 26 channels each) | Tool guard | The guard was a denylist of what the tool must not touch, written for the two meshes in hand; the repo already marks the 26 hand-pose channels as required | An allowlist of the meshes it may edit (hair, beard, eyebrow, moustache), tested with real hand, arm and body names |
| M2 | MED (MST-2) | A run that strips nothing still re-exported the FBX, replaced the live file and reported success | Tool contract | Every sibling refuses a no-op (`add_face_morph_channels.py` "nothing to do"); this one was not written from them | Refuse before export, with a test |
| M3 | MED (MS7-3, MS4-7, MS5-6) | No gate resolves an `<EquipmentSet id>` to a defined roster; a misspelt id would null-dereference inside the NPCCharacters load, and the engine's empty catch would stop every character after it in the merged document from loading, with no crash at the typo (today: 365 references, 0 unresolved) | Missing gate | `REF_KINDS` sweeps only prefixed refs, and `_xml_files` reads `*.xml`, so the 778 references in `lords.xslt` are never read | FOLLOW-UP: a `BROKEN_ROSTER_REF` pass (Mike's word for an issue) |
| M4 | LOW (MS2-2, MS4-5) | Two docs contradicted the change: the armour handbook said no shipped item sets `covers_head`, and the Black Numenorean doc said the Mouth of Sauron binds the shared lord rosters | Doc drift | The commit touched data, and no doc that states the rule was grepped | Both corrected; the handbook names the one exception and its costs |
| M5 | LOW (MS4-4, MS7-2, MS2-4) | The snapshot README block (written 2026-10-04) said all five tpacs have cache entries (the material has none, like every Armory material), named no gate for the art, owed no in-game look, and left the item XML only outside version control | Doc accuracy | The block was written from the import notes, not from `check_rdc_entries.py` | Rewritten: cache wording, `check_rdc_entries.py --under Mordor/mouthofsauron` in the gate line, a "Not yet run" line, the item XML inline |
| M6 | LOW (MS7-1, MS4-3, MS5-1, MST-6) | Every release channel's Armory still holds the helm at 55 while the v2.0.34 notes say 50 | Delivery | The live edit was made in the dev install only; the channels hold the last packaged state | The README names it; the v2.0.34 Armory must be packaged from this install |
| M7 | LOW (MS6-2) | Nothing pinned the helm's 50 or its `named` class, so a reinstall from an older package would restore 55 silently | Unversioned data | The trap index asks for an in-repo gate with any live-Armory edit; the README's prose check was the only guard | `MouthOfSauronHelmDataTests` (live item, repo class row) |
| M8 | LOW (MST-3) | The strip tool's tests did not isolate its guards: `x.mouth` was not a known name, so the unknown-name guard passed that case whatever the face-part guard said | Test strength | The test used a placeholder name instead of a real one from the target FBX | Real head, eye and mouth names, and a missing `--fbx` case |
| M9 | LOW (MS4-6) | `799e189e` is a subject line with no body, so the v2.0.33 changelog entry says nothing a player can read | Changelog | A mixed commit made outside the review flow | The follow-up commit's body carries the player paragraph |
| M10 | LOW (MS5-2, MS7-5) | The morph doc pointed at Saruman's backups beside the FBX (the 2026-10-04 sweep moved them) and called a Kit re-import owed that the package's timestamps suggest happened | Doc drift | Written before the sweep; the re-import was not checked | Corrected; the compiled channel count stays unmeasured |
| M11 | LOW (MS4-1) | No GitHub issue for the helm or the strip tool, so the owed in-game look has nowhere to live | Documentation duty | Mixed commit, no issue step | #733, filed and closed with `triage-needs-ingame` (Mike) |
| M12 | INFO (MS2-3, MS5-9) | A saved campaign keeps the Mouth of Sauron's saved gear; only campaigns started on v2.0.33 or later dress him in the helm | Save semantics | Heroes are read from XML only for a new campaign | Recorded in the README; the in-game look runs on a new campaign |
| M13 | INFO (MS2-5) | The `Civilian` flag is not what lets his civilian set wear the helm; it only marks the item civilian in the inventory and tooltips | Design record | The intent was written from the flag's name | Recorded in the README |
| M14 | INFO (MS2-7) | The strip tool's docstring stated as settled what the morph doc tags Likely, including eyebrows that were never measured | Overclaim | Written from the CPU path alone | Docstring and `tools/README.md` reworded |
| M15 | INFO (MS5-8) | The roster section comment said the lord templates were "NOT yet assigned to any lord"; 25 characters use them | Comment drift | Older than this change; the new pair was added under it | Corrected |
| M16 | UNVERIFIED (MS2-1, MS4-2, MS6-1) | `covers_head` may freeze his hand-grip morphs: TAOM's research records it, and the native fix is parked | Engine behaviour | The design weighed hiding the head, not the face object's other jobs | In-game look decides; options for Mike if the grips freeze |

**Refuted by the checker:** MS5-4 (a succession re-dresses him from the ruler templates: real, deliberate for every
named lord, and a model to keep one helm fails the simplicity criterion), MS5-7 (a `lords.xslt` override instead of
the copied rosters: it would remove the `BROKEN_ITEM_REF` coverage the README relies on), MS6-3 (one shared target
helper for the strip and weight tools: a tiny win for new coupling).

**Follow-ups, not applied here (pre-existing code or outside the change):** M3 (the roster gate); MS2-6 (the morph
doc's description of `0x56EBA0`, written in `a728f35b`); MS5-5 (a failed armour-gate init lets every item onto culture stalls);
MS7-4 (`validate_xml_schemas.py` says NOT REGISTERED for files the engine does load); MST-4 (a stale "done" report can
answer the documented poll); the same allowlist for `transfer_upper_mesh_weights.py`, which copies the guard.

## Root cause: shipped without review

The gear reached trunk in a commit made outside the review flow, with no body, no issue and no in-game check on
record, and two releases carried it before anyone looked. The memory card for the helm still said "repo rosters
UNCOMMITTED, OWED: /deep-review" after the commit had landed, so nothing in the session's own records flagged that
reviewed-or-not had changed. The same happened to Tournament Rewards in `9e2a39f4`
([rca-tournament-rewards-2026-10-04.md](rca-tournament-rewards-2026-10-04.md)).

## Patterns

1. **Guards written for the case at hand** (M1, M2, M8): a denylist sized to the two meshes being stripped, no
   no-op check, and a test whose placeholder name let another guard answer for it.
2. **Records that state what a file said when it was written** (M4, M5, M6, M10, M15): a rule in the handbook, a
   roster note, a cache claim, a backup path and a section comment, each true once and none rechecked when the data
   moved.

## Why each lens caught or missed what it did

- **Engine compatibility (2)** found M16 by reading the native skin builder, and M4, M12 to M14; it does not judge
  tools.
- **Efficiency (3)** found nothing: no C# or C++ in scope.
- **Completeness (4)** found M11 and M9 and the README gaps in M5; it does not open native code.
- **Data flow (5)** found M6, M10 and the 1.4.x shop path (MS5-3); it traced the class row through the gate.
- **Design (6)** proposed M7's test and two refuted designs.
- **XML (7)** found M3 by tracing what the engine does with an unknown roster id, M5's art gate and M6.
- **Tooling** found M1, M2 and M8 by reading every Race Test FBX and the siblings the tool was copied from.

## Applied, pending, follow-up

- **Applied:** M4, M5, M6 (README), M7, M10, M12 to M15 (docs and comment), and the strip tool fixes M1, M2, M8, M14.
- **Decided by Mike (2026-10-04):** M11, issue #733 filed and closed with `triage-needs-ingame`; MS5-3, a blacklist
  row on `bannerlord-1.4.5` keeps the helm out of Mordor's new stock on the 1.4.8 line (a helm a saved game's
  town already stocks stays until it is bought: the 1.4.5 stock filter removes only foreign-culture items). M16 waits for the in-game look.
- **Follow-up:** the list above.
- **Convergence pass** (one `deep-reviewer` on the applied fixes): every applied finding resolved; it mutated the
  strip tool's guards in memory and each break failed a test. `MouthOfSauronHelmDataTests` fails against the 55
  backup ("Expected:<50>. Actual:<55>") and passes on the live install.

## Feedback to codify

- `lessons/build-tooling-workflow.md`: a mesh-editing tool's guard is an allowlist of what it may edit; a tool that
  changes nothing says so and writes nothing (M1, M2).
- `.claude/agents/deep-reviewer.md`: `audit_armory_refs.py` rewrites a tracked report unless given `--report -` (a
  reviewer in this run modified `docs/audits/armory-ref-audit.md`; restored).
