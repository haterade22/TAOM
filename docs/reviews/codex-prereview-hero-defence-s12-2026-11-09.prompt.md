# Codex pre-review: Hero Defence Slices 1 and 2 (2026-11-09)

You are an adversarial reviewer. Review only. Do not edit, create or delete any file. Do not run `dotnet build`, `dotnet test` or `./build.ps1`: an unattended game test is running on this machine, and the full C# suite already passed on this tree (38,180 passed, 0 failed, 9 skipped). Reading files, grep, git read commands and short Python one-liners are fine. Return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to `docs/reviews/raw/`; do not write that path yourself.

Feature: Hero Defence is a planned third campaign-free mission mode for TAOM (a Bannerlord total conversion), a tower defence where the towers are named heroes. This change is its pure core only: a run state machine (phases, lives, gold, score, heroes who fall and return) and a wave generator (culture rotation, level bands, class mix, leaders, powers), plus JSON config readers, shipped JSON data, a roster generator script and tests. Nothing is registered in IoC or SubModule yet, and no engine type is touched. Issue #1106.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
1. The plan, the source of the rules: `C:\Users\mikew\.claude\plans\i-have-a-new-dapper-crayon.md` (sections "Decisions", "How it plays", "Waves", "Economy", "Verification"). The maintainer approved it.
2. `AGENTS.md` and `.ai/review-reference.md` for the project rules.
3. The data: `Main/_Module/ModuleData/hero_defence/waves.json`, `cultures.json`, `rosters.json`.

FILES (all new, untracked, in this worktree):
Domain: `Main/Features/HeroDefence/Domain/` (CreepWavePlan.cs, CreepWavePlan.Troops.cs, DefenceRules.cs, DefenceTypes.cs, HeroDefenceRun.cs, HeroDefenceRun.Shop.cs, HeroDefenceRun.Wave.cs, PlacementRules.cs, WaveCatalog.cs)
Config: `Main/Features/HeroDefence/Config/` (HeroDefenceConfigCatalog.cs, HeroDefenceConfigScope.cs, HeroDefenceCulturesReader.cs, HeroDefenceRosterReader.cs, HeroDefenceWavesReader.cs)
Data: `Main/_Module/ModuleData/hero_defence/{waves,cultures,rosters}.json`
Script: `tools/generate_hero_defence_rosters.py`, `tools/tests/test_generate_hero_defence_rosters.py`
Tests: `TAOM.Tests/Features/HeroDefence/**`
Shared types it reuses unchanged: `Vec3f` and `AgentKey` (`Main/Features/FordsOfWar/Domain/`), `RunRandom` and `EndlessScale` (`Main/Features/LongDefeat/Domain/`).

KNOWN SUSPECTS (CONFIRM or DISPUTE each, with file:line evidence):
S1. `CreepWavePlan.Brief` adds the infantry gap bonus (`gap * GapHealthPerLevel`, `gap * GapDamagePerLevel`) to the whole wave's factors. The plan says the bonus is for clamped infantry. Is a wave-wide factor a defect for ranged or mounted creeps that are in band, or an acceptable simplification?
S2. Opening cultures (waves 1 and 2) skip `HasEntered`. If a pinned culture has no infantry at or below the band, `ClassCounts` still gives infantry its weight and `CreepsOf` returns no creeps for it, so the wave is smaller than the count formula with no warning. Can the shipped data or a config edit reach this? Is there a test?
S3. A brief with zero packs: `TickWave` sees `QueueDone && _alive.Count == 0` on its first tick and pays the full clear bonus. Which inputs can produce a zero-pack wave, and does any reader or test stop them?
S4. `ReleaseNextPack` picks the first slot whose live count is 0, and a slot counts creeps only after `CreepSpawned`. State the contract this puts on the future engine director (register a whole batch in the tick that applies the effects). Is that contract written down anywhere a later slice will see it?
S5. The rotation bag (`Pick`, `Candidates`): never the same culture twice in a row, prefer a different signature, refill when empty, late cultures enter by band. Find a seed or catalog where a culture repeats, a signature repeats while another was possible, or a culture never appears. Check that `Brief(w)` gives the same result whatever order the waves are asked in.
S6. The readers claim "never throw, a bad value reverts with one warning". Try NaN, Infinity, wrong types, nulls, duplicate ids, an unknown signature, an unknown troop id, an empty file and a missing file against each reader.

REQUIRED SECTIONS:
1. PLAN CONFORMANCE: every numbered rule of the plan's "How it plays", "Waves" and "Economy" sections that this code implements, with pass or fail and evidence. Name each plan rule the code does not implement yet and say whether that is in scope for these slices (Slice 1: run state machine; Slice 2: wave generator, rosters, rotation, bands, gaps, growth, leaders, powers).
2. DETERMINISM AND ARITHMETIC: seeds, stream ids, rounding, largest-remainder counts, integer overflow, float NaN paths.
3. CONFIG CROSS-REFERENCE: every culture id in `cultures.json` against the troop files under `Main/_Module/ModuleData/troops/` and the cheatsheet; every leader id exists and is not a buyable hero named in the plan's hero table; every power id in the pool.
4. GENERATOR: does `tools/generate_hero_defence_rosters.py` apply the plan's filters (the tree's own culture, occupation Soldier, no settlement militia, an upgrade edge in or out) and does the drift test fail on a stale `rosters.json`?
5. TESTS: assertions that are vacuous, tests that pass for the wrong reason, missing NaN and invalid-value cases.
6. FINDINGS: each with severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, a concrete failing input, and a fix. Mark each claim observed (you ran or read it) or inferred.

QUALITY GATES: cite file:line for every claim. A claim you could not check is UNVERIFIED, never a guess. Do not report style or naming unless it causes a defect.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.
