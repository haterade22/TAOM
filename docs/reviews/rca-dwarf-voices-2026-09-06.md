# RCA: dwarf battle voices, and the gate written to stop them recurring

**Date:** 2026-09-06
**Issue:** #548
**Scope:** `lotr_dwarf_voice_def.xml`, `module_sounds.xml`, `tools/audit_voice_clip_lengths.py`,
`tools/voice-clip-baseline.txt`, `docs/features/kingdom-voices.md`
**Review:** `/deep-review`, 6 agents (standards, engine claims, data flow, tooling correctness,
completeness, efficiency)

## Top line

Players reported dwarves shouting a full spoken line every 2 to 4 seconds in battle. The cause was
not a rate anyone could turn down: Bannerlord has no voice frequency, cooldown or probability knob,
so how often a clip plays is decided entirely by which `VoiceType` slot it is bound to. TAOM had
7.93 s and 8.77 s sound-set compilations, each holding several complete spoken lines, bound to
`Grunt`, `Pain` and `Focus`, and 5.4 to 11.8 s warcries on `Yell`. `Grunt` has zero managed call
sites anywhere in the shipping client, so native fires it per melee exertion. Where vanilla plays a
short exertion, a dwarf delivered a speech.

The data fix is two attribute changes and eleven deleted `<variation>` lines. The more important
outcome is that the review, and a follow-up sweep after it closed, found the **gate written to
prevent recurrence was itself unsound**: four demonstrated false PASSes on exactly the defect class it exists to catch. A validator that reports
success on the bug it was written for is worse than no validator, because it converts an open
question into a false assurance.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | The original defect: long compilations on native-fired slots | Data | No check related clip length to slot. Category caps (4 s / 8 s) are far too loose: a 3.9 s clip is legal `mission_voice` and still unbearable on `Grunt` | New gate enforces a 2.0 s bar on `Grunt`/`Stun`/`Pain` |
| 2 | HIGH | Gate did not validate `type=` against the 62 declared voice types, so `type="grunt"` escaped every length rule AND was a dead binding | Tooling | I wrote the rule as a set membership test and never asked what happens to a value outside the set. Voice-type identity is an exact string resolved through `GetVoiceTypeIndex` | `VOICE_TYPES` set; an unknown type is a finding, never normalised |
| 3 | HIGH | A baseline entry pinned `(slot, path)` but not duration, so re-cutting a baselined clip to any length rode the old exemption | Tooling | I modelled the baseline as a suppression list rather than a ratchet. The duration was already written in a trailing comment and simply not compared | Entries are now `Slot\|path@seconds`; the pin is mandatory and drift beyond 0.05 s resurfaces the finding |
| 4 | HIGH | A run that inspected nothing reported success. File discovery was a glob, so a renamed voice definition silently checked less (379 bindings to 53, no signal) | Tooling | Same class as `UPGRADE_INDEX_EMPTY`, a rule this repo already wrote down after the identical mistake elsewhere. I did not consult it when writing a new gate | Discovery is driven off `project.mbproj`; a registered-but-missing file exits 2; a zero-measurement run exits 2 |
| 5 | MED | An absent or misspelled `sound_category` silently skipped the cap check, while Native states such a sound is never played | Tooling | I keyed the cap off a dict `.get()` and treated a miss as "nothing to check" instead of "the engine will drop this" | All 21 Native categories transcribed; missing or unknown is a finding |
| 6 | MED | Docstring asserted "every mp3 in this tree is CBR". 38 files carry a Xing/Info frame, and a VBR clip can measure SHORT, which is the unsafe direction | Tooling | Asserted from the files I had looked at (the dwarf set) and generalised to a tree I had not surveyed | Xing/Info frame count is parsed; a VBR clip is measured exactly |
| 7 | MED | Docstring asserted Python's `wave` "misreports IMA ADPCM by roughly 10x". It does not; it raises `unknown format: 17` | Tooling | **I took this from a research subagent's report and wrote it into a code comment without running it.** The conclusion it justified was right; the stated reason was invented | Comment corrected from a run I performed. See lesson below |
| 8 | MED | Doc claimed `Charge` is fired by `OrderController.PlayOrderGestures` only. A second call site exists in `AgentVisuals.MakeRandomVoiceForFacegen` | Research | I verified the combat call site myself and stopped there, then wrote "only" | Doc names both; the design conclusion (one agent per battle order) is unchanged and was independently confirmed |
| 9 | LOW | Three doc counts went stale, two of them because of this change: distinct dwarf audio paths 48 to 47, and the worked example still named a deleted variation | Docs | Edited the data without re-deriving the counts the same doc publishes | Corrected. The 231-to-250 `module_sound` count was already wrong and is fixed while here |
| 11 | HIGH | The hardened gate STILL passed half the original bug: re-binding the 5.4 to 7.1 s warcries to `Yell` exited 0. `SLOT_MAX` covered only `Grunt`/`Stun`/`Pain`, and `Yell`'s `mission_voice_shout` category allows 8 s | Tooling | Found during the follow-up doc sweep, AFTER the review closed. I fixed the two slots the bug arrived on and set one shared constant for them, never asking which OTHER slots the engine fires often. `Yell` was named as frequently-fired in my own slot table on the same page | The bar is now per slot (`Yell` 3.5 s, calibrated against the dwarf Battlecries at 3.09 s and `uruk_01` at 1.71 s). It also surfaced three previously invisible `uruk_hai_01` Yell violations |
| 10 | LOW | Gate was wired into nothing: no CI step, no test, no `tools/README.md` row, while CHANGELOG and feature doc both claimed it enforced the fix | Process | I treated "the tool exists and passes" as done. Two docs asserted enforcement that did not exist | CI step in `validate-xml`; 23 unit tests; README row |

## Root-cause pattern: I stated things I had not run

Findings 6, 7 and 8 are one failure wearing three hats. In each case I wrote a confident, specific,
checkable claim into a durable artifact (a code comment, a doc table) without executing the check
that would settle it. Finding 7 is the sharpest: a research subagent reported the "10x" figure, I
found the surrounding argument persuasive, and I promoted its detail into a code comment as though
it were my own measurement. `evidence-over-claims.md` §A.4 exists for exactly this and names it:
*"a confident subagent report is a claim, not evidence, and relaying it unverified is the same
failure as trusting an agent's ✅ done."*

The tell is uniform across all three: each claim was **one command away** from being settled.
`python -c "import wave; wave.open(f)"` takes two seconds. Scanning 93 files for a Xing header takes
ten. Grepping the whole decompile for a second call site takes one. In every case I had the tools
open and did not spend the seconds, because the claim already felt settled.

Findings 2 through 5 and 11 share a different pattern worth naming separately: **I wrote the gate to
detect the bug I had just fixed, rather than the class it belongs to.** Every one of those holes appears the
moment you ask "what input passes this check while still being broken?" rather than "does this check
catch my bug?" The tooling-correctness agent found four of them by asking the first question.

Finding 11 is the sharpest instance and it escaped the review entirely. The reported bug arrived on
two slots, `Grunt` and `Yell`. I fixed both in the data, then wrote a length rule covering only the
wordless slots, so the gate could not catch the `Yell` half of the very bug it was written for. My
own slot table on the same doc page lists `Yell` as frequently fired; I had written that sentence and
still did not apply it. The lesson is narrower than "test adversarially": **when a defect arrives on
N inputs, the gate must be proven against all N**, by re-applying each and watching it fail. I proved
the `Grunt` half and assumed the rest.

## Why each agent caught or missed

| Agent | Result |
|-------|--------|
| Standards | Caught the missing README row and missing test. Correctly reported the C# rules as not applicable rather than manufacturing findings against an XML changeset |
| Engine claims | Refuted finding 8 and added two precision corrections (the `Victory` timer widens to 6 to 12 s after the first cheer; the managed `Pain` call is a narrow shield-penetration branch). Correctly returned UNVERIFIABLE on mp3 support rather than guessing |
| Data flow | Highest value. Caught the doc count drift, the stale line sheet, the mp3-only regression risk, and the `LOTRAOM` merge hazard. This is the agent whose scope crosses files, which is where all three doc defects lived |
| Tooling correctness | Found all three HIGH false PASSes with working reproductions. None of the other five could have: they review C#-shaped concerns, and this changeset's risk was concentrated in a Python gate |
| Completeness | Independently confirmed the CI, test and README gaps, and confirmed the CHANGELOG entry was insertion-only against a shared file |
| Efficiency | Correctly reported no issues and returned UNVERIFIED on the FMOD channel question instead of asserting a cost it had not measured |

The structural lesson: this changeset's risk was almost entirely in a **validation script**, and five
of the six core agents are calibrated for C# feature code. The one agent aimed at the script found
every HIGH. The `/deep-review` skill already provides for this (Step 2c triggers a tooling-correctness
agent for new `tools/**/*.py`), and it fired correctly here.

## What the tests now pin

`tools/tests/test_audit_voice_clip_lengths.py`, 23 cases. The three that matter most are named for
the false PASSes they close: `unknown_voice_type_is_a_finding`, `baseline_pin_blocks_a_regrown_clip`,
`missing_registered_file_exits_two`. Writing them immediately paid for itself by catching a
fourth defect I had introduced while fixing the third: my coverage floor returned exit 2 before
findings were printed, so a file whose every binding was a finding reported as "bad input" and hid
them. Findings now take precedence over the floor.

## Still open

- **mp3 support is unverified**, and this change removed the last `.wav` from `Grunts`,
  `focus_sounds` and `Victories`. The practical argument that it works is strong: TAOM already ships
  85 to 93 mp3 references including nearly every dwarf order bark, and `dwarf_stun` was mp3-only
  before this change, so if mp3 were silent most dwarf barks would already be missing and the
  reported symptom was too much noise, not too little. It is still an inference, and the in-game
  check settles it.
- **`Grunts` and `focus_sounds` are down to one variation each**, and `Grunts` feeds the engine's
  highest-frequency slot. This is the honest state of the asset library: the old "variety" was two
  eight-second speeches. Stage 2 (splitting the compilations) is what fixes it properly.
- **`LOTRAOM` is installed alongside `TAOM`** with pre-fix voice definitions, and
  `CreateProcessedVoiceDefinitionsXMLForNative` merges same-named definitions by appending. If both
  load, the fix is silently undone. `Main/_Module/SubModule.xml` declares no `<IncompatibleModules>`.
- **`uruk_hai_01` has the same defect**, baselined with a stated reason rather than fixed, so the
  in-game A/B on the dwarves stays attributable.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
