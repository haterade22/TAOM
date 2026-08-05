# RCA — BannerBearers deep review (2026-07-16)

**Feature:** `Main/Features/BannerBearers/` — formations raise their faction standard via the engine's native `BannerBearerLogic`. Issue [#351](https://github.com/haterade22/TAOM/issues/351).
**Review:** `/deep-review BannerBearers` — 5 parallel agents (Standards, API Compatibility, Efficiency, Completeness, Data Flow). No Codex pass.
**Outcome:** deep-review 2 CRITICAL, 2 HIGH, 1 MED, 1 LOW confirmed + 1 CRITICAL disputed-and-dismissed; 1 further HIGH found by the author between passes; Codex 2 MED + 1 LOW. **All confirmed findings fixed in-session.** Final state: build succeeded, **61 BannerBearers tests + full suite green**, `validate_moduledata.py` PASS.

## Top-line

The feature was architecturally sound — the API-compatibility agent verified all 43 engine bindings with zero incompatibilities, and the highest-risk invariant (the bearer-freeze guard) was independently traced through two decompile sources and confirmed sufficient. **Every confirmed defect was a data-flow gap, not an engine-binding mistake.** Both CRITICALs were invisible to the type system, invisible to the engine (which fails silently on each), and invisible to 4 of the 5 agents.

The single most valuable finding — six culture keys that match nothing — came from a hypothesis the orchestrator formed *before* dispatching and passed into the Data Flow agent's brief as an explicit "investigate this thoroughly" item. It would very likely have shipped otherwise.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | CRITICAL | `CultureBanners` keyed 6 factions on their LOTR **display name** (`rohan`, `dale`, `khand`, `dunland`, `harad`, `rhun`). None is a real `StringId` — `spcultures.xslt` overrides `<name>` but never `id`, so Rohirrim is `vlandia`, Dunlendings `empire`, Haradrim `aserai`, Easterlings `khuzait`, Barding `sturgia`, Variag `battania`. All six silently fell through to `DefaultBannerItemId` and flew a generic Gondor standard. | Data flow / M1 "parsed-but-unresolvable" | The orchestrator audited culture coverage with a regex over `taom_spcultures.xml` **only**, saw 22 ids, noticed Rohan was absent, hypothesised "XSLT-transformed" — and then wrote the config from the LOTR *names* without ever confirming what id the XSLT produces. The dictionary is `Dictionary<string,string>`: a dead key is not a type error, not a parse error, and not an engine error. It renders a banner, just the wrong one. **See "the answer was already in the repo" below — this was avoidable without any research at all.** | 3 regression tests (below) + a `_comment_CultureBanners_ids` note in the shipped JSON + the doc note. Lesson appended to `lessons/data-content-cultures.md`. |
| 7 | HIGH | `DefaultBannerItemId` shipped as `"standard_of_duty_t1"`, so **every unmapped culture flew the Gondorian Standard of Duty.** 38 cultures are registered at runtime; the config maps 28. The unmapped 10 — `looters`, `sea_raiders`, `forest_bandits`, `desert_bandits`, `mountain_bandits`, `steppe_bandits`, `nord`, `vakken`, `darshi`, `neutral_culture` — are vanilla leftovers still carrying **~99 live references in TAOM's own ModuleData**. Every vanilla-culture bandit warband in the game would have raised a Gondor standard. | Data flow / fail-open default | Found by the orchestrator **after** the 5-agent review, while writing the Codex prompt — by asking "how many cultures exist?" instead of "are my keys right?". All 5 agents missed it, and so did the culture-id fix: correcting six keys says nothing about the cultures that were never keyed. The default was chosen for coverage ("everything gets a banner") without asking *what else is in the set*. | `DefaultBannerItemId` now ships `""` — fail closed. Only explicitly-mapped cultures field standards. 2 regression tests: the default must stay empty, and the 10 leftovers must not be mapped. Lesson appended to `lessons/data-content-cultures.md`. |
| 2 | CRITICAL | Master-toggle leak: `GetDesiredNumberOfBannerBearersForFormation` returned `0` when `Enabled=false` instead of deferring to `base` (vanilla's `1`). The model stays registered and answers the engine for **every** formation — including ones vanilla's hero-captain path or the player's Order-of-Battle screen banners independently of TAOM. Disabled, TAOM would leave the banner item in place and suppress its bearer: **worse than vanilla, not equal to it.** | Data flow / master-toggle fold | The orchestrator folded `Enabled` at the **service** layer (`GetDesiredBearerCount` returns 0) and reasoned that satisfied "off = vanilla". It does for the *assignment* path, which TAOM owns — but not for the *policy* path, which the engine drives for formations TAOM never touched. The blind spot: treating "our feature does nothing" as identical to "the engine behaves as it would without us". | All 4 overrides now defer to `base` on `!IsEnabled`. Lesson appended to `lessons/gamemodels-services.md`. |
| 3 | HIGH | Master-toggle leak: `GetMinimumFormationTroopCountToBearBanners` returned TAOM's tuned `4` regardless of `Enabled`, changing the threshold used by the **engine's own** `CanFormationDeployBannerBearers` (vanilla `2`) for every formation even with the feature off. | Data flow / master-toggle fold | Same root cause as #2. Additionally: this override *looked* like the rule's legal shape (a) "a single expression", so it read as obviously-fine and got no scrutiny. | Same fix. Covered by the same lesson. |
| 4 | HIGH | ADR-002 breach: `GetBannerBearerReplacementWeapon`'s private helper `FindOwnOneHandedWeapon` ran a `for` loop with an `if` **inside the GameModel**. `.claude/rules/gamemodels.md` is explicit and binary: a body containing `if`/`foreach`/`switch` is a violation "even if the model is under 20 lines. 'It's only a few lines' is not a carve-out." | Standards | **The orchestrator deviated from its own approved plan.** The plan said "fix in XML first, C# as backstop"; mid-implementation it reasoned a C# fallback reading the troop's own sidearm was "per-troop accurate and maintenance-free" and skipped the XML. That traded 8 XML blocks for a rule breach. The agent's *proposed* fix (move the loop into `BannerBearerService`) was itself wrong — it would have put the sealed `BasicCharacterObject`/`ItemObject` into a service, breaching ADR-007. | Deleted the override + helper entirely. Added `<banner_bearer_replacement_weapons>` to the 8 `is_bandit` cultures that lacked it. Invariant now pinned by a build-time test instead of runtime C#. Lesson appended to `lessons/build-tooling-workflow.md`. |
| 5 | MED | Master-toggle leak: `CanAgentBecomeBannerBearer`'s race gate applied when disabled, narrowing the eligible-bearer set below vanilla's. | Data flow / master-toggle fold | Same as #2. | Same fix. |
| 6 | LOW | Master-toggle leak: the unarmed-bearer backstop applied when disabled. | Data flow / master-toggle fold | Same as #2. | Dissolved by fix #4 — the backstop no longer exists. |
| — | ~~CRITICAL~~ | **DISPUTED — not a bug.** Standards agent reported `FindOwnOneHandedWeapon` returning non-nullable `ItemObject` while returning `null` as a CRITICAL nullable-contract violation. | — | `Main/TAOM.csproj:9` sets `<NoWarn>$(NoWarn);8600;8601;8602;8603;8604;8618;8625</NoWarn>` — CS8603 is deliberately suppressed project-wide for `Main`. A forced clean rebuild emits **zero** CS86xx warnings and zero warnings naming BannerBearers. The base engine signature is non-nullable-oblivious `ItemObject` and vanilla itself returns null from it. The agent inferred the rule from the `Nullable` PackageReference without reading `NoWarn`. | None. Recorded here so the same false positive isn't re-litigated next review. |

## Root-cause pattern: **the master-toggle fold is a per-override property, not a per-feature one**

Findings 2, 3, 5 and 6 are one bug repeated four times. The orchestrator folded `Enabled` once, at the service layer, and treated the feature as toggled. But a `GameModel` is not a feature — it is a **policy oracle the engine consults regardless of what our feature is doing**. Folding the toggle where *our* code branches is insufficient; it must fold at every point the *engine* asks a question, and the fold must be `return base.X(...)` (restore vanilla), never `return 0` / `return <our default>` (impose a different answer).

This is the exact class named in `.claude/rules/csharp-architecture.md` and in the deep-review Agent 5 prompt, sourced from CombatMechanics `GetHorseChargePenetration` (2026-07-02): *"a single unconditionally-read config value breaks the promise."* It recurred here despite the rule existing, because the rule's worked example is about a config value read without folding — while this instance folds the config correctly and still leaks, by **returning a toggle-aware 0 instead of deferring to vanilla**. "Folded the toggle" and "restored vanilla" are different properties; only the second is what "off = vanilla" promises.

**Generalised rule (new):** for any `GameModel` override, the disabled path must be `return base.<Method>(...)`. If an override cannot express its disabled path as a base call, the override does not belong in that slot.

## The answer was already in the repo — in FOUR live places — and the rule that owned it excluded the file

This is the single most important finding of the review, and it is not about banners.

TAOM documents "the XSLT cultures keep their vanilla ids" in **four live locations**:

| Location | What it says |
|---|---|
| `.claude/rules/xml-data.md` | *"**Common mistake:** Writing lore names for XSLT cultures. `rohan` is WRONG — use `vlandia`. `dunland` is WRONG — use `empire`. `harad`/`rhun`/`dale`/`khand` are WRONG…"* |
| `.claude/rules/moduledata-validation.md` | same id table |
| `.claude/skills/review-codex/SKILL.md` | *"NOTE: `rohan` is NOT a valid ID. Rohan uses `vlandia`."* |
| `AGENTS.md` (Codex catalog) | *"Config ID mismatches: keys like `rohan` (should be `vlandia`)…"* |

**All six wrong names, named explicitly, as a known recurring mistake.** The bug still shipped. It needed no research whatsoever — only reading a table that already existed.

The mechanism is exact and worth stating precisely. `xml-data.md` has a section titled **"Config ID Cross-Reference (MANDATORY)"** whose first line is:

> *"After writing ANY XML/**JSON** config containing culture, kingdom, or settlement IDs, cross-reference EVERY ID against this table before moving on."*

…while its frontmatter read:

```yaml
paths:
  - "Main/_Module/ModuleData/**/*.xml"
  - "Main/_Module/ModuleData/characters/**"
  - "Main/_Module/ModuleData/factionmap/**"
```

The rule's **prose** claims JSON. The rule's **glob** matches `*.xml`. The file authored was `Main/_Module/ModuleData/banner_bearers/banner_bearers_config.json`. **The rule written to prevent this exact mistake could not fire on the file that made it** — and it had been failing this way for every JSON config in the repo: 58 of TAOM's 59 ModuleData JSON files sat outside the trigger (only `factionmap/*.json` matched).

**The failure is not missing knowledge. It is knowledge with no trigger.** Documenting the fact a fifth time would have changed nothing. Two things actually change the outcome:

1. **Fixed the trigger** — added `Main/_Module/ModuleData/**/*.json` to `xml-data.md`'s `paths:`, so the MANDATORY section now loads for the 59 JSON configs it was always written for, and left a note in the rule explaining why. **Generalised rule: prose scope and glob scope must agree — if a rule says it governs a file type, its `paths:` must match that file type.** Worth auditing the other paths-scoped rules for the same drift.
2. **The regression test** — fires on every build regardless of what the author knew or which rule loaded. Documentation asks a human to remember; a test does not.

## Root-cause pattern: **display name ≠ StringId, and a dead dictionary key is silent**

Finding 1 is the M1 "parsed-but-unresolvable" trap surfacing in a `Dictionary<string,string>` rather than a single enum-ish string field. Every existing guard missed it because they all check the *value* side:

- The config provider validates **ranges** — keys are strings, nothing to range-check.
- `ShippedBannerBearerConfigTests` validated every banner **id** against vanilla's `banners.xml` — and passed, because the values were all real. Nobody validated the **keys**.
- `validate_moduledata.py` validates ModuleData XML cross-refs — this is a JSON file, out of its scope.
- The engine silently renders the fallback banner. No log, no throw.

The `vanilla-data-comparison.md` rule already warns that TAOM mirrors/renames vanilla data and that stale refs bite — but it is `paths:`-scoped to `**/settlements.xml`, `**/spcultures.xml`, `**/*.xslt` etc. **The file being authored here was a JSON config in a new feature folder, so the rule never loaded.** The knowledge existed; the trigger didn't fire.

**Generalised rule (new):** when a config maps ids of engine/ModuleData entities, a test must assert every **key** resolves against the real entity set — not just that the values do. Ship the key-side test with the config, always.

## Why each agent missed these

| Agent | Missed | Why |
|---|---|---|
| **1 — Standards** (haiku) | #1, #2, #3, #5, #6 | Correctly scoped to ADRs and file-local rules. Caught #4 (the real ADR-002 breach) — its highest-value catch — but a dead dictionary key and a toggle-fold gap are semantic, cross-file properties outside its checklist. It also produced the one false positive (nullable), by inferring project config from a PackageReference instead of reading `NoWarn`. |
| **2 — API Compatibility** (sonnet) | #1, #2, #3, #5, #6 | Working exactly as designed and did it well: 43/43 bindings verified, and it independently confirmed the two riskiest design bets (virtual dispatch of `CanAgentBecomeBannerBearer` from vanilla's priority method; spawn-before-`OnTeamDeployed` ordering). Signature correctness says nothing about whether the *values* we pass are meaningful. |
| **3 — Efficiency** (haiku) | #1, #2, #3, #5, #6 | Out of scope by construction. Notably it *did* flag `FormationIndex` vs `PhysicalClass` as a semantic concern — the only agent besides #5 to look at meaning rather than mechanics. |
| **4 — Completeness** (haiku) | #1, #2, #3, #5, #6 | Verified the doc's Configuration table matched the shipped JSON **field-for-field and value-for-value — and passed**, because the config and the doc were *consistently wrong together*. A doc-vs-config cross-check cannot catch a defect present in both. It did correctly catch the missing GitHub issue and 6 missing upper-bound validation tests. |
| **5 — Data Flow** (sonnet) | — | Caught #1, #2, #3, #5, #6 — every remaining defect. Confirms the skill's own claim that this is the highest-value agent. |

**The meta-finding:** 4 of 5 agents passed the feature. A 5-agent review that drops the Data Flow agent, or runs it on a thin brief, would have shipped both CRITICALs. Worth noting that the Data Flow agent's brief for this run explicitly listed the culture-id question as item 9 with "this is the single most likely data-flow gap — investigate it thoroughly." **The orchestrator's pre-dispatch hypothesis is what aimed the agent at the bug.** Generic briefs find generic bugs.

## Lessons to codify

Appended to the per-category lesson files (index: `docs/reviews/LESSONS-LEARNED.md`):

1. **`lessons/gamemodels-services.md`** — *A GameModel override's disabled path must be `return base.<Method>(...)`, never a computed "off" value.* Folding a master toggle inside the service is not the same as restoring vanilla: the engine consults the model for entities our feature never touched, and an override answering `0` while "disabled" actively suppresses vanilla mechanics. Second instance of the master-toggle-fold class after CombatMechanics `GetHorseChargePenetration` (2026-07-02) — the first leaked by *not* reading the toggle, this one leaked by reading it and answering wrong.
2. **`lessons/data-content-cultures.md`** — *TAOM's re-skinned cultures keep their vanilla StringIds.* `spcultures.xslt` overrides `<name>`, never `id`: Rohirrim = `vlandia`, Dunlendings = `empire`, Haradrim = `aserai`, Easterlings/Rhûn = `khuzait`, Barding/Dale = `sturgia`, Variag/Khand = `battania`. Any config keyed on a culture must use the StringId, and must ship a test asserting every key resolves against the real culture set — a dead key is silent at every layer.
3. **`lessons/build-tooling-workflow.md`** — *When an approved plan says "fix it in data", fixing it in code instead is a rule breach waiting to happen.* The mid-implementation "the C# fallback is more accurate" reasoning traded 8 XML blocks for an ADR-002 violation, a nullable question, two perf findings and a master-toggle leak — all of which dissolved when the plan's original data fix was applied. Deviating from an approved plan is a decision that deserves the same scrutiny as the plan itself.
4. **`lessons/testing-qa.md`** — *A doc-vs-config consistency check cannot catch a defect present in both.* The Completeness agent verified all 11 config fields matched the doc exactly and passed while six keys were dead. Pin config **keys** against the real entity set, not against the documentation.

## Codex adversarial pass (2026-07-16, gpt-5.5 / xhigh)

Run after the fixes above, against the corrected state. **Verdict: SHIP WITH FIXES — CRITICAL 0, HIGH 0, MEDIUM 2, LOW 1.** Raw: `docs/reviews/raw/codex-adversarial-bannerbearers-2026-07-16.md`; prompt: `docs/reviews/codex-adversarial-bannerbearers-2026-07-16.prompt.md`.

All six Known Suspects came back favourable, independently confirming the fixes and the riskiest design bet:

| Suspect | Codex verdict |
|---|---|
| S1 freeze guard sufficiency | **Confirmed sufficient** for field/caravan/siege/sally-out. Hideout/arena/tournament builders don't add `BannerBearerLogic` at all, so the guard is moot there. |
| S2 master-toggle fold | **Confirmed fixed** — all three overrides defer to `base` when disabled. |
| S3 threshold stability vs exact-equality edges | **Confirmed stable** — `Lazy<T>` + `Reuse.Singleton`, no reload path. |
| S4 fail-closed default | **Confirmed** — empty default and missing keys return null; leftovers unmapped; all ids valid. |
| S5 `FormationIndex` vs `PhysicalClass` | **Agrees** with `FormationIndex` — mission slot identity; `PhysicalClass` would drift with casualties. |
| S6 N>1 bearers | **Engine-supported** up to 6; the real risk is the MixedFormations interaction, not a native crash. |

### Codex findings (all 3 verified against source before implementing, all fixed)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| C1 | MED | **Culture came from `formation.GetFirstUnit()`** — which is literally `Arrangement.GetAllUnits()[0]`, not a semantic culture owner. A mixed-culture formation (allied army, mercenary-heavy player party) flies whichever standard happened to be arranged into slot 0. | Data flow / wrong-source | The orchestrator needed *a* culture per formation and took the cheapest one that compiled. "First unit" reads as a reasonable proxy and is right for the common single-culture case, so nothing about it looks wrong in isolation — it only misbehaves in mixed formations, which no test could construct (no live `Formation`). The 5 agents all accepted it; the API agent even verified `GetFirstUnit()`'s null behaviour without questioning its *semantics*. | Majority-culture vote in the service (`ResolveMajorityCultureId`), with an ordinal tie-break so the winner never depends on arrangement order. 6 new unit tests. Lesson: `lessons/adapters-taleworlds-api.md`. |
| C2 | MED | **MixedFormations `Patch30` overrode banner-bearer placement.** It Prefixes `Formation.GetOrderPositionOfUnit` and returns `false` for every unit in a field battle, so the engine's bearer slotting (`SwitchUnitLocations` into the `RelativeFormationPosition[6]` banner positions) is ignored and standards scatter through the ranks. No crash. | Cross-feature interaction | **This was a known unknown, not an unknown unknown** — the feature doc and the plan both listed "MixedFormations + banners → arrangement thrash" as the top untested interaction, and the deep-review Data Flow agent was explicitly briefed on it. It still wasn't *resolved*, because tracing it needs reading a DIFFERENT feature's patch and reasoning about which one wins — cross-feature work that per-feature review scopes structurally exclude. Codex, given the whole repo and no scope, found it immediately. | 2-line fall-through in `Patch30`: `if (unit?.Banner != null) return true;` — placed before the IoC resolve to keep the ~40,000×/frame path cheap (`Agent.Banner` is `Equipment?.GetBanner()`, one array index, no loop). Lesson: `lessons/harmony-il.md`. |
| C3 | LOW | **`ExcludedRaces` typos fail open** — an unknown race name never matches, so the race it was meant to bar stays eligible to carry a banner. | Config validation / M1 | Known and *documented* at the time — `BannerBearerConfigProvider`'s header comment says so explicitly ("An unknown race name in ExcludedRaces simply never matches (permissive)") — and consciously deferred because the race registry isn't populated at config-load time. Documenting a gap is not closing it. | Validate on first use (`IsRaceAllowed`), where `IRaceManager` is live: `IsValidRaceName` per entry, warn once. 2 new tests. |

### What Codex added over the 5 agents

Codex found **zero** of the bugs the internal review found (they were already fixed) and **two** it structurally could not have: both C1 and C2 are about whether a *choice* is semantically right, not whether code is correct. C2 in particular is the argument for a whole-repo reviewer — the interaction was flagged as a risk in three places and still needed an agent with no scope boundary to actually resolve it.

The disagreement rate was zero: every Codex finding verified true on first read. That is unusually clean and worth noting against the ~95% baseline in `feedback_audit_findings_not_always_correct.md` — it likely reflects the prompt naming the six real uncertainties rather than asking for a generic sweep.

## Verification after fixes

- `dotnet build Main/TAOM.csproj` — succeeded, zero errors.
- `dotnet test TAOM.Tests` — **0 failed**. 61 BannerBearers tests (+3 culture-key pins, +2 fail-closed-default pins, +6 majority-culture, +2 excluded-race-name warning). MixedFormations' own 41 tests re-run green after the Patch30 change. (A whole-suite total is deliberately not quoted: this tree also carries an unrelated in-flight feature's untracked tests, so the number moves for reasons that have nothing to do with this changeset.)
- `python tools/validate_moduledata.py` — **PASS**, 5,867 items / 38 cultures; all 32 newly-referenced weapon ids resolve.
- `pwsh [xml]$x = Get-Content -Raw taom_spcultures.xml` — **PARSE OK** (mandatory XML smoke test; the BOM state was read and preserved by the edit script).
- `python tools/lint_docs.py --fail-on-drift` — CLAUDE.md budget 0, config-example drift 0.

## Still owed

- ~~**GitHub issue** (Completeness finding) — must exist before the closing commit.~~ Created: [#351](https://github.com/haterade22/TAOM/issues/351).
- 6 upper-bound config validation tests (`> 100` / `> 1000` paths) — Completeness finding, LOW.
- In-game verification, per `docs/features/banner-bearers.md`. The freeze guard is traced-sufficient but unproven in a live battle; the MixedFormations arrangement interaction remains untested.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/banner-bearers.md](../features/banner-bearers.md)
- [docs/reviews/rca-banner-bearers-reinforcement-av-2026-07-25.md](./rca-banner-bearers-reinforcement-av-2026-07-25.md)

<!-- backlinks-end -->
