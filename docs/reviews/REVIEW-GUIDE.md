# Codex Adversarial Review Guide

How to write effective prompts, what to verify, and what we've learned.

## Process

The dispatch and verification process is the `/review-codex` skill (`.claude/skills/review-codex/SKILL.md`).

## Artifact retention (2026-07-12)

Raw Codex stdout transcripts (2 to 4 MB each) land in **`docs/reviews/raw/`, which is gitignored**; they stay on disk for local reference but never enter git. The **committed** durable record per review is: the `*.prompt.md` (reusable dispatch artifact), the `rca-*.md` (incident analysis), the `REVIEW-LOG.md` entry (scoring history), and any `lessons/<category>.md` entries (cross-feature rules). The `/review-codex` and `/deep-review` skills write to `raw/` automatically. Historical raw transcripts (through 2026-07-11) were untracked and moved to `raw/` in the same change; links from older RCAs point at `raw/...` and resolve on-disk but not in a fresh clone; the committed distillate is the durable record.

---

## Advanced Pattern: Known Suspects

For features where you've already done internal review (e.g., `/deep-review`), add a "Known Suspects" section to the Codex prompt. This forces Codex to CONFIRM or DISPUTE specific hypotheses with evidence, rather than finding its own surface-level issues.

Format in the prompt:
```
=== KNOWN SUSPECTS (confirm or dispute each with evidence) ===
1. [TITLE]: [hypothesis]. Read [specific file] to confirm.
2. [TITLE]: [hypothesis]. Read [specific file] to confirm.
```

Format in the expected output:
```
## KNOWN SUSPECTS VERDICT
1. [TITLE]: CONFIRMED -- [file:line evidence] or DISPUTED -- [counter-evidence]
```

Quality gate: add "Section N skips any suspect or says 'could not verify'" to enforce engagement.

This pattern produced the highest-quality Codex output in our review history because it forces deep reading of specific code paths instead of surface scanning.

## Prompt Template (v6)

```
Adversarial review of {FeatureName}.

{1-2 sentences: what the feature does, its risk profile, what's already good}

TAOM ID CHEATSHEET: paste the block from .claude/skills/review-codex/references/prompt-fixed.md, the only copy.

READ FIRST (required context):
- docs/features/{feature-name}.md
- {any config files: JSON, XML}

FILES (service — business logic):
{list service files}

FILES (entry points — thin, delegate to service):
{list GameModel and Harmony patch files}

FILES (config):
{list JSON/XML config files}

FILES (tests):
{list test files with count and coverage note}

=== REQUIRED SECTIONS (missing section = incomplete review) ===

SECTION 1: VANILLA CODE
Read these files from E:\Decompiled_Bannerlord\ and paste the relevant
methods into your output as ``` code blocks:
  - Find {VanillaClass}.cs in {Modules|Campaign|Core}/ — paste {MethodName}()
  - Find {VanillaClass2}.cs — paste {MethodName2}()

This section MUST contain ``` code blocks with decompiled C#. Prose
descriptions of vanilla behavior are NOT sufficient — prior reviews
described vanilla behavior without reading the code and produced false
positives. If you cannot find the file, say so explicitly.

SECTION 2: VANILLA ANALYSIS
Using the code from Section 1, answer:
  a) {specific question about vanilla behavior}
  b) {specific question about how TAOM interacts with vanilla}
Reference specific line numbers from the code you pasted.

SECTION 3: {FEATURE-SPECIFIC DEEP ANALYSIS}
{Concrete scenarios with expected outputs, math walkthroughs, or
 IL verification — whatever is the highest-risk area for this feature}
  a) {specific scenario with numbers — show the formula step by step}
  b) {specific scenario with numbers}
  c) {edge case}

SECTION 4: CONFIG CROSS-REFERENCE (required for any config-driven feature)
a) List every string ID key in the config file(s)
b) Cross-reference each against the source-of-truth file. Specify the file:
-- Culture IDs: check against taom_spcultures.xml + spcultures.xslt
-- Kingdom IDs: check against TAOM_spkingdoms.xml
-- Settlement IDs: check against Main/_Module/ModuleData/settlements.xml
-- Troop IDs: check against troops/troops_{culture}.xml
Do NOT claim "config looks valid" without showing which file you checked.
c) Check for DEAD CONFIG -- values that exist in config but are never read at runtime. Search for the config key in the C# codebase. If no code loads or uses a config field, it is dead.

SECTION 5: FINDINGS OR OBSERVATIONS
If bugs found — each finding MUST include:
  - TAOM code (file:line)
  - Vanilla code (quote from Section 1)
  - Evidence of divergence
  - Severity: CRITICAL / HIGH / MEDIUM / LOW

If approve verdict — you MUST still provide an OBSERVATIONS subsection
listing things worth noting even if not bugs (e.g., high multiplier
magnitudes, silent filtering, exception swallowing, design tradeoffs).
An approve with zero observations suggests shallow analysis.

=== QUALITY GATES ===

Your review is INCOMPLETE if:
  - Section 1 contains no ``` code blocks with decompiled C#
  - Section 4 claims validity without showing cross-reference evidence
  - All findings are the same severity (vary your calibration)
  - Section 5 has no observations on an approve verdict
  - A finding claims "this is wrong" without checking feature docs for
    design intent (Wave 1 produced a false positive from misreading
    kingdom mapping — always check docs/features/ before flagging)

Lessons from prior reviews:
SUCCESSES to repeat:
- Config ID cross-reference caught "rohan"/"dol_guldur" mismatches (BattleBalance)
- Vanilla reimplementation diff caught fertility formula drift (RaceAge)
- Garrison wage gate found by comparing TAOM vs vanilla IsGarrison (TroopProgression)
FAILURES to avoid:
- Codex assumed empire=Rohan (it is Dunland). Use the ID cheatsheet.
- Codex skipped transpiler IL verification despite being focus #1 (BannerColor)
- Codex flagged characterObject.IsMounted as bug -- vanilla uses same check (CulturalFeats)

DO NOT flag architecture/pattern compliance {if feature is well-architected}.

Return the full report as your final message; never write the output path.
```

---

## Prompt Design Principles

### What makes Codex find real bugs vs. surface noise

| Principle | Why | Example |
|-----------|-----|---------|
| **Point to E:\Decompiled_Bannerlord\** | Codex has pre-decompiled source but won't use it unless told | "Find DefaultPartySpeedCalculatingModel.cs in Modules/" |
| **Require vanilla code in output** | Forces Codex to actually read vanilla, not guess | "Include the decompiled C# in your output" |
| **Give concrete scenarios** | Forces math walkthrough, catches formula bugs | "Mordor army besieging town_EW3, position 0 of 4, show the multiplier" |
| **Name the required sections** | Codex skips hard sections silently; named sections make gaps visible | "REQUIRED SECTIONS (missing = incomplete)" |
| **Reference feature docs** | Codex needs design intent to distinguish bugs from features | "READ FIRST: docs/features/army-targeting.md" |
| **Say what's already good** | Prevents Codex from filling the review with easy pattern violations | "100% test coverage, proper service/adapter separation" |
| **Include prior failure examples** | Concrete failures are stronger than abstract "DO NOT" rules | "Codex called X a bug but vanilla uses the same pattern" |
| **Vary severity explicitly** | Codex defaults to everything-is-HIGH | "If everything is HIGH, your calibration is off" |
| **Require verification artifacts** | Codex describes instead of showing; prose is unfalsifiable | "Your output MUST contain ``` code blocks with decompiled C#" |
| **Separate "show" from "analyze"** | Codex skips showing code if it can jump to conclusions | "Step A: paste the code. Step B: answer questions about it." |
| **Require observations on approve** | Clean verdicts need evidence of depth, not just absence of findings | "OBSERVATIONS section required even for approve verdicts" |

### What wastes Codex's time

| Anti-pattern | Why it fails |
|-------------|-------------|
| "Check for ADR violations" on a well-architected feature | Codex finds pattern violations and inflates them to CRITICAL |
| "Decompile X" without pointing to E:\Decompiled_Bannerlord\ | Codex often skips decompilation entirely |
| Generic focus areas ("null handling", "thread safety") | Gets generic answers; Codex checks superficially |
| No feature documentation reference | Codex can't distinguish design intent from bugs |
| Same severity guidance as AGENTS.md defaults | AGENTS.md rates ADR violations as CRITICAL; prompt needs to override for mature features |
| "Include decompiled code" without structural enforcement | Codex ignored this instruction in 3/3 reviews — words alone don't work |
| No config cross-reference file path | "Validate config" without "against settlements.xml" lets Codex claim validity without checking |

---

## Codex Failure Patterns (observed)

Track these to prevent repeats. Each entry: what went wrong, which review, how to prevent.

### FP-1: False positive from not reading vanilla code
**Review:** CulturalFeats (2026-04-05)
**What happened:** Codex called `characterObject.IsMounted` in TaomPartyTroopUpgradeModel a bug, claiming it should be `upgradeTarget.IsMounted`. Vanilla Bannerlord's `KhuzaitRecruitUpgradeFeat` uses the exact same `characterObject.IsMounted` check.
**Prevention:** Require "VANILLA REFERENCE" section with specific decompiled file paths. Require decompiled code in output.

### FP-2: Skipped hardest analysis silently
**Review:** BannerColorPersistence (2026-04-05)
**What happened:** Prompt's #1 focus area was transpiler IL verification. Codex skipped it entirely — no mention, no acknowledgment, no "I couldn't verify this." Found 3 surface-level scope concerns instead.
**Prevention:** Use "REQUIRED SECTIONS (missing = incomplete)" format. Name each section explicitly.

### FP-3: ADR violations inflated to CRITICAL/no-ship
**Review:** CulturalFeats (2026-04-05)
**What happened:** CulturalFeats GameModels have feat-check logic directly in models (no service extraction). Codex rated this CRITICAL/no-ship. The "logic" is `if (hasFeat) addBonus` — one-liners in <55-line files. This is tech debt, not a ship-blocker.
**Prevention:** Tell Codex what's already good. "This feature is well-architected" prevents pattern-compliance padding. Override AGENTS.md severity for mature features.

### FP-4: Design intent questions presented as confirmed bugs
**Review:** BannerColorPersistence (2026-04-05)
**What happened:** Codex flagged global clan color scope as a "regression." In TAOM's LOTR setting, global scope is likely intentional — each faction keeps its iconic colors regardless of kingdom. Codex didn't consider this.
**Prevention:** Reference feature docs so Codex understands design intent. Add "READ FIRST" section.

### FP-5: Uniform severity (everything HIGH)
**Review:** Both reviews (2026-04-05)
**What happened:** CulturalFeats: 1 CRITICAL + 2 HIGH + 1 MEDIUM. BannerColorPersistence: 3 HIGH. No LOW findings in either. Real bugs ranged from LOW (null guard) to HIGH (unconditional forest speed).
**Prevention:** Explicit instruction: "If everything is HIGH, your calibration is off. Vary severity."

### FP-6: Approve without evidence
**Review:** ArmyTargeting (2026-04-05)
**What happened:** Codex issued a correct "approve" verdict but produced no decompiled vanilla code (despite explicit instruction), claimed config IDs were valid without cross-referencing settlements.xml, and described vanilla behavior in prose instead of showing code. The verdict was right but indistinguishable from a rubber stamp.
**Prevention:** Require "VERIFICATION ARTIFACTS" — specific code blocks that must appear in the output. For approve verdicts, require an "OBSERVATIONS" section (things worth noting even if not bugs). An approve with zero decompiled code is incomplete regardless of verdict.

### SUCCESS-1: Config ID cross-reference catches silent failures
**Reviews:** BattleBalance, Diplomacy (2026-04-05)
**What worked:** Codex cross-referenced config keys against actual culture/kingdom StringIds and found mismatches: `"rohan"` should be `"vlandia"`, `"dol_guldur"` should be `"dolguldur"`, missing kingdoms in alignment.json. These are silent failures — the feature runs without errors but the config values never match at runtime.
**Why it works:** Explicit "cross-reference X against Y file" instructions in the prompt force Codex to actually check IDs. Without this instruction, Codex just says "config looks valid."

### SUCCESS-2: Vanilla reimplementation diff catches formula drift
**Review:** RaceAge (2026-04-05)
**What worked:** TaomPregnancyModel fully reimplements vanilla. Codex compared the math and found the human config values (200/195) produce ~56% higher fertility than vanilla's curve. This led to discovering that the config was intentional (Dunedain) but docs/tests were stale.
**Why it works:** "Walk through scenario X with actual numbers" forces formula comparison.

### SUCCESS-3: Lifecycle/state analysis catches cross-mission bugs
**Reviews:** Infrastructure, AdvancedCombat+Warg (2026-04-05/06)
**What worked:** Codex traced singleton lifetimes and found MissionAdapterFactory caches Agent references past mission boundaries (stale data). Also found shader abort latch stays armed after success, and FirstAttack flag never consumed.
**Why it works:** "Check for stale cached references" and "trace the lifecycle" prompts force Codex to think about state beyond the happy path.

### SUCCESS-4: Dead code detection finds unused features
**Reviews:** Wave4B (2026-04-05)
**What worked:** Codex confirmed BattleScenes is truly dead (commented out in SubModule.cs), found child gender param unused, and found startup resource retry was unsafe.
**Why it works:** Explicit "DEAD CODE DETECTION" section requirement makes Codex search for unreachable paths.

### MISS-1: Load-path mutation without entity state enumeration
**Review:** NamedCompanions (2026-04-08)
**What happened:** `EnsureCompanionsPlaced()` (OnGameLoaded) teleported recruited companions out of the player's party because `IsPlacedInSettlement()` returned false for heroes traveling on the map. The implementation only considered the happy-path state (unrecruited, in settlement) and two negative states (dead, disabled) -- but missed the most common real-world state (recruited, in player party).
**Root cause:** Copied the "run same logic on load" pattern from BannerInjectionBehavior without checking whether the operation was idempotent. Banner color injection is harmless when repeated; hero teleportation is destructive.
**Prevention:**
1. Added "Entity State Matrix" requirement to `csharp-architecture.md` -- any OnGameLoaded behavior that mutates hero state must enumerate all possible states before writing the code
2. Added "Skip-Guard Exhaustion" requirement to `tests.md` -- every guard clause needs a test for every entity state that should be skipped
3. Added "idempotent vs destructive" check to architecture rules -- before copying a pattern, ask if the operation has side effects

### FP-7: Wrong kingdom mapping assumption
**Review:** Diplomacy (2026-04-05)
**What happened:** Codex assumed `empire`=Rohan and `vlandia`=Arthedain, then flagged phase-1 war pairs as wrong. Actual mapping: `empire`=Dunland, `vlandia`=Rohan. The config was correct all along. False positive from not having the TAOM kingdom→LOTR mapping.
**Prevention:** Include "TAOM KINGDOM MAPPING" reference block in every prompt. Prevents ID-to-faction confusion.

### SUCCESS-5: User-editable JSON treated as untrusted input
**Review:** RevoltTuning (2026-04-20)
**What worked:** Codex flagged that `RevoltTuningConfigProvider.LoadConfig()` logged "Loaded" success for any parseable file, with no semantic validation. A user edit like `{"rebellionStartLoyaltyThreshold":100}` or a sign-flipped `1.0` penalty would silently ship broken gameplay. Tests only covered syntax failures (missing file, malformed JSON), not semantic failures.
**Why it works:** Codex reasons about the full trust chain — if the feature advertises "edit this JSON to retune," that file is untrusted input at a system boundary and needs validation, not just parsing.
**Prevention:** Before shipping any feature with user-editable config, add a `Validate` pass that checks semantic constraints (ranges, sign, ordering) and falls back to defaults with a warning. Tests must cover semantically-invalid-but-parseable values, not just parse failures.

### SUCCESS-6: Doc claims cross-checked against DryIoc lifecycle
**Review:** RevoltTuning (2026-04-20)
**What worked:** Feature doc promised JSON edits take effect "on next game load." Codex traced the actual lifecycle (`Reuse.Singleton` + `OnSubModuleLoad` + `Lazy<T>` capture + ctor-side snapshot in the model) and showed the config loads once per Bannerlord process, not per campaign. The docs were wrong, not the code.
**Why it works:** Codex compares documented behavior against the IoC registration lifetime and the points where the config is first resolved.
**Prevention:** When writing how-to-retune docs for any JSON/XML-backed feature, state the actual reload scope explicitly (application restart vs new campaign vs per-tick). Don't copy plan language without verifying.

### SUCCESS-7: Every troop an item-assigning tool touched, tabulated against the item's usage flags
**Review:** Ranged range ladders, review 108 (2026-09-13)
**What worked:** The roster tool cloned each kingdom's donor bow and handed the clone to every troop in a band by class and speed. Asked which mounted troop received which `item_usage`, Codex listed all 16 mounted ranged troops with the usage and whether Native permits it mounted, and found seven horse archers holding a `long_bow` clone (`requires_no_mount`: the bow spawns on their back and is never drawn). Six Claude agents briefed on class and speed had passed them. Asked for EVERY reader of `NotMerchandise`, it read the loot model and corrected a doc claim.
**Why it works:** a clone copies every attribute, and the tool reads two of them; the reviewer enumerates the ones it does not read against the troops that now carry them.
**Prevention:** when a tool assigns an existing item to troops, the prompt names the item attributes the tool does NOT set (`item_usage`, flags, `culture`, `difficulty`, `is_merchandise`) and asks what each does to the troops that now carry it, mounted, shielded and by race. Data guard: `ranged_ladder.mount_barred_usages` + `RANGED_MOUNT_USAGE`.

### SUCCESS-8: Compiles the production allocator into a harness and prints the collision it claims
**Review:** Creature handles and threads, review 109 (2026-09-13)
**What worked:** Handed a slot map whose eviction had just been added, Codex compiled `LayoutPositioner` and its model types into a PowerShell `Add-Type` harness, removed one index the way the new code does, called the real allocator, and printed `first ranged=(1, -5); first replacement=(1, -5); collision=True; replacement after 100 reuses=(10, 4)`. Two deep-review passes and the shipped test had read the eviction and not the next allocation.
**Why it works:** an allocator has two sides; a fix that edits one is judged by driving the other, with the production code, not by reading it.
**Prevention:** when a change evicts from a store that also allocates, the prompt asks what the next allocation returns after N evictions at a fixed population, and the test asserts the invariant (unique, in class, inside the footprint) over turnover, not the single step.

### SUCCESS-9: Opens the caller of every override the thread map lists
**Review:** Creature handles and threads, review 109 (2026-09-13)
**What worked:** The review's thread map, built from `Mission.OnTick` and the native callbacks, called the tree logic main-thread-only. Codex opened the callers of the thirteen `MissionBehavior` overrides and found `Mission.OnAgentPanicked` is a plain managed call from `CommonAIComponent.OnTick`, inside the asynchronous agent tick; `OnAgentFleeing`, its twin, comes from `MissionAgentPanicHandler.OnPreMissionTick` on the main thread.
**Why it works:** a callback's thread is its caller's thread, and a managed `Mission.OnX` is not an `[MBCallback]` just because it sits beside them.
**Prevention:** every thread claim about a `MissionBehavior` override names the caller's file and line; an override whose caller is an agent, team or formation tick, or unknown native, defers itself (`DeferredCallbackQueue`) instead of touching main-thread state.

### SUCCESS-10: Checks the prompt's own premises against the files before answering them
**Review:** Gondor volunteer pools, review 110 (2026-09-13)
**What worked:** Two Known Suspects were built on wrong premises. KS3 described Morlad's pool as bowman-only when the file holds bowman 50 / scout 50, and KS7 said the clan and culture pools fire only for settlements with no pool, when a converted fief resolves its settlement culture's `CultureMap` entry before the standard cascade (`VolunteerRecruitmentService.cs:92`). Codex reopened the JSON and the cascade, corrected both, then answered the corrected question: a Markov estimate of in-slot maturation from the real roots, and the conversion outcome per case.
**Why it works:** the prompt writer built the changeset and carries its assumptions; a suspect that restates an assumption cannot catch its own error. Reading the file the suspect is about, first, does.
**Prevention:** every Known Suspect names the file and line it is about, and the reviewer is told to refute the suspect's premise as well as its conclusion. The template's "Try to refute every claim below" already says so; keep it, and treat a disputed premise as a finding about the prompt, not noise.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/010-knowledge-base-architecture.md](../adrs/010-knowledge-base-architecture.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/research/karpathy-autoresearch.md](../research/karpathy-autoresearch.md)

<!-- backlinks-end -->
