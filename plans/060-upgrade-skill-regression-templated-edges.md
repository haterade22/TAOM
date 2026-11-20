# Plan 060: judge templated upgrade edges on their SkillSet in the gate and both skill writers

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat db5680c8a..HEAD -- tools/sync_lord_inline_skills.py tools/taom_schema.py tools/validate_moduledata.py tools/ranged_ladder.py tools/rebalance_ranged_ladders.py tools/rebalance_troops.py tools/tests/test_sync_lord_inline_skills.py tools/tests/test_upgrade_skill_monotonicity.py tools/tests/test_ranged_ladder.py docs/reference/moduledata-validator-codes.md docs/features/moduledata-validation.md docs/features/ranged-ladders.md docs/features/troop-skill-balance.md`.
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition.

## Status

- **Priority**: P3
- **Effort**: M
- **Risk**: LOW (tools only; 0 shipped upgrade edges have a templated side today, so the shipped data
  must give the same validator result before and after)
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `db5680c8a`, 2026-11-19
- **Baseline at that commit**: dotnet build exit 0; C# 43028 passed, 3 failed, 9 skipped. Failing:
  `EveryKey_IsSeededInAllTwelveLanguageFiles`, `EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `NoTranslatedString_MixesWritingSystems` (language-file tests; the translation XMLs belong to the
  project lead and are not this plan's). Python suite: 10549 ran, 0 failed, 13 skipped.
- **Issue**: #626 (the maintainer approved on 2026-11-19 that this plan edits `/tools/`)

## Why this matters

`UPGRADE_SKILL_REGRESSION` is the error gate that stops a troop promotion from lowering a combat
skill. It skips every upgrade edge whose source or target names a `skill_template`, because it reads
only the inline `<skills>` rows. On the installed 1.5.x engine a templated character's skills are
computable (the SkillSet's values with the inline rows laid over them), so the skip is an unjudged
hole, not a necessity, and the two tools that write skills (`ranged_ladder.planned_skill_edits`,
`rebalance_troops`) have the same hole: one skips the edge, the other reads a templated character as
its inline rows alone. Nothing ships through the hole today (0 of 913 indexed edges has a templated
side), but the next villager or troop that gains a template would pass every gate unjudged. After this
plan the gate and both writers resolve a template through one shared function, and a writer refuses to
write an inline row that would contradict a SkillSet.

## Current state

All excerpts are from the planned-at commit `db5680c8a`.

### The engine rule (installed v1.5.7, decompiled with `pwsh tools/taom-src.ps1 path <Type>`)

`TaleWorlds.Core.BasicCharacterObject.Deserialize(MBObjectManager objectManager, XmlNode node)`,
lines 337 to 364 of the decompile:

```csharp
MBCharacterSkills mBCharacterSkills = objectManager.ReadObjectReferenceFromXml("skill_template", typeof(MBCharacterSkills), node) as MBCharacterSkills;
if (mBCharacterSkills != null)
{
    DefaultCharacterSkills = mBCharacterSkills;
}
else
{
    DefaultCharacterSkills = MBObjectManager.Instance.CreateObject<MBCharacterSkills>(base.StringId);
}
...
foreach (XmlNode childNode in node.ChildNodes)
{
    if (childNode.Name == "Skills" || childNode.Name == "skills")
    {
        if (mBCharacterSkills != null)
        {
            DefaultCharacterSkills = MBObjectManager.Instance.CreateObject<MBCharacterSkills>(base.StringId);
            foreach (SkillObject property in mBCharacterSkills.Skills.GetProperties())
            {
                DefaultCharacterSkills.Skills.SetPropertyValue(property, mBCharacterSkills.Skills.GetPropertyValue(property));
            }
        }
        DefaultCharacterSkills.Init(objectManager, childNode);
    }
```

`MBCharacterSkills.Init` calls `Skills.Deserialize(objectManager, node)`, and
`PropertyOwner<T>.Deserialize` (lines 71 to 88) only calls `SetPropertyValue` for each listed row; it
never clears. So the effective skills are: a copy of the SkillSet, each inline row overriding its one
skill; a skill neither lists reads as 0. `CharacterObject.GetSkillValue` (line 797) returns
`base.GetSkillValue(skill)` for a non-hero, which reads `DefaultCharacterSkills`.

A template id that names no SkillSet: `MBObjectManager.ReadObjectReferenceFromXml` (line 1513) returns
`GetPresumedObject(text, text2)` (line 713), which creates and registers a presumed, empty object when
the type auto-creates; `Game.cs:321` registers the type as
`objectManager.RegisterType<MBCharacterSkills>("SkillSet", "SkillSets", 52u)`, and `RegisterType`'s
`autoCreateInstance` defaults to `true` (MBObjectManager.cs:376). So a dangling template gives an
empty SkillSet: the inline rows alone. Do not re-derive any of this; if a decompile you happen to read
says otherwise, STOP.

### The shared resolver's home: `tools/sync_lord_inline_skills.py`

It already loads every SkillSet the engine would see, and `validate_moduledata.py`'s
`SKILL_TEMPLATE_MISMATCH` calls it:

- `skill_set_files(game, module_data)` (:78 to :84): the `*skill_sets*.xml` files of the vanilla
  modules `("Native", "SandBoxCore", "SandBox", "StoryMode")` (`VANILLA_MODULES`, :53), then the repo
  module's, in engine load order. On this machine that is 5 files: Native, SandBoxCore, SandBox
  `*_skill_sets.xml`, then `taom_lord_skill_sets.xml` and `taom_wanderer_skill_sets.xml`.
- `load_skill_sets_from(files)` (:87 to :108) merges same-id sets the way `MBObjectManager` does and
  returns `dict[str, dict[str, int]]`, keyed by the bare SkillSet id (no `SkillSet.` prefix).
- `load_skill_sets(game, module_data=MODULE_DATA)` (:244 to :245) is
  `load_skill_sets_from(skill_set_files(game, module_data))`. `game` is the install ROOT (the folder
  that holds `Modules`), not the Modules folder.
- `_scan` (:168) reads a template's set id as `set_id = template.split(".", 1)[-1]`, and judges a row
  against `sets[set_id].get(sm.group(2), 0)` (:173). The new function uses the same split.
- Imports at the top (:38 to :48): `argparse, glob, os, re, sys`, `NamedTuple`, and
  `from _gamedir import game_dir`. It imports nothing else from `tools/`, so `taom_schema`,
  `ranged_ladder` and `rebalance_troops` can import it without a cycle.

Measured on this machine at the planned-at commit (scratch probe over `load_skill_sets`): 744
SkillSets; the two templates the `npcs_*` files use resolve to
`infantry_heavyinfantry_level1_template_skills` = `{Athletics 20, OneHanded 30, TwoHanded 10,
Polearm 30, Throwing 20, Riding 10, Bow 10, Crossbow 10}` and
`infantry_heavyinfantry_level16_template_skills` = `{Athletics 80, Riding 50, OneHanded 100,
TwoHanded 50, Polearm 100, Bow 50, Crossbow 50, Throwing 70}`.

### The gate: `tools/taom_schema.py`

- `Registries` dataclass (:66 to :91). Its last field (:91), the pattern to copy:
  ```python
  couchable_ids: frozenset = None                           # weapon item ids with a couch usage (None = read the install when a pass needs it)
  ```
- `Validator.__init__` (:477 to :494) stores `self.game_modules = Path(game_modules) if game_modules else None`
  (:485), the install's Modules folder. `validate_moduledata.py` passes it; `tools/taom_query.py:127`
  (the MCP) and `tools/check_release_pair.py:87` construct a Validator without it.
- `_couchable_ids` (:1703 to :1721) is the pattern for an install-read memo with a registry override:
  ```python
  given = getattr(self.reg, "couchable_ids", None)
  if given is not None:
      return given
  if not self.game_modules or not self.game_modules.is_dir():
      return None
  if not hasattr(self, "_couch_memo"):
      try:
          self._couch_memo = couchable_item_ids(self.game_modules, self.moduledata / PLACEHOLDER_DIR)
      except Exception as exc:   # an install the catalogue cannot read: say so once
          issues.append(Issue(...))
          self._couch_memo = None
  return self._couch_memo
  ```
  Lazy tools imports in this file use `import sys as _sys` then
  `_sys.path.insert(0, str(Path(__file__).resolve().parent))` (for example :548 to :549).
- `_upgrade_troop_index` (:1958 to :2038) builds `troops[id]` for every `<NPCCharacter>` in
  `troops/troops_*.xml` and `characters/npcs_*.xml`. The template lines:
  ```python
  1984                # Whether a templated character's inline rows agree with its SkillSet is
  1985                # SKILL_TEMPLATE_MISMATCH's question (validate_moduledata.py, #626), which resolves
  1986                # the template; here the flag only keeps the edge checks off numbers they cannot see.
  1987                tmpl = self._SKILL_TEMPLATE_RE.search(attrs)
  ...
  2008                troops[idm.group(1)] = {
  2009                    "file": rel, "line": line, "skills": skills, "upgrades": upgrades,
  2010                    "templated": bool(tmpl),
  ```
  `_SKILL_TEMPLATE_RE = re.compile(r'\bskill_template=["\']([^"\']+)["\']')` (:1934). The key
  `"templated"` is read only at :2164 (verified with `git grep -n '"templated"' -- tools`; the other
  consumers of the index, `_upgrade_tier_collapse` and the armour passes, never read it).
- `_upgrade_skill_regressions` (:2149 to :2187), the part this plan changes:
  ```python
  2161                # A templated character's base skills live in a SkillSet outside these files, so
  2162                # its inline block alone is not the character. No troop or villager edge has a
  2163                # templated side (2026-09-19, #626), so the edge is skipped rather than resolved.
  2164                if source["templated"] or target["templated"]:
  2165                    continue
  2166                exempt = self._RESPECIALIZATION_EXEMPT_EDGES.get(
  2167                    (source_id, target_id), ())
  2168                drops = [
  2169                    f"{s} {source['skills'].get(s, 0)}->{target['skills'].get(s, 0)}"
  2170                    f"{' (undeclared, reads as 0)' if s not in target['skills'] else ''}"
  2171                    for s in self._COMBAT_SKILLS
  2172                    if s not in exempt
  2173                    and target["skills"].get(s, 0) < source["skills"].get(s, 0)
  2174                ]
  ...
  2180                    message=(
  2181                        f'upgrading "{source_id}" into this troop LOWERS {", ".join(drops)}. '
  2182                        f"An upgrade must never cost the player a stat. Re-run "
  2183                        f"tools/rebalance_troops.py, whose clamp pass raises a target to its "
  2184                        f"source rather than leaving the tree reading backwards"
  2185                    ),
  ```
- `tools/validate_moduledata.py:894` to :895 runs `skill_template_mismatch_issues(game_modules.parent, moduledata)`
  only when the install exists; its no-install branch (:897 to :918) prints one
  `WARNING: <CODE> SKIPPED: ...` line per install-bound pass, for example :904 to :905:
  ```python
          print(f"WARNING: {TEMPLATE_CODE} SKIPPED: the vanilla SkillSets a template can name live\n"
                f"         in the install.", file=sys.stderr)
  ```

### Writer 1: `tools/ranged_ladder.py` and its caller `tools/rebalance_ranged_ladders.py`

- `RangedTroop` (:135 to :148), last field:
  `templated: bool = False  # skill_template=: on 1.5.3 the template's skills, inline rows laid over them`.
  Set at :711 (`templated=bool(npc.get("skill_template")))`, in `load_ranged_troops`) and :732 (same, in
  `load_upgrade_sources`). Read only at :1007 and by two tests (`git grep -n "\.templated\|templated=" -- tools`).
- `planned_skill_edits(troops, launchers, spec, militia=frozenset(), exempt_edges=None, sources=None)`
  (:965 to :1028). The docstring's last sentences, from "An edge with" at the end of :972 through
  :976, say an edge with a template "is skipped, as
  UPGRADE_SKILL_REGRESSION skips it ... A templated ladder troop still takes its cell". The code:
  ```python
   989    everyone = dict(sources or {})
   990    everyone.update(troops)
   991
   992    def value(tid, cls):
   993        return new.get((tid, cls), int(everyone[tid].skills.get(cls, 0)))
  ...
  1004            for tgt_id in src.upgrades:
  1005                if tgt_id not in troops or (src_id in militia and tgt_id in militia):
  1006                    continue
  1007                if src.templated or troops[tgt_id].templated:
  1008                    continue
  ...
  1022    edits = []
  1023    for (tid, cls), v in sorted(new.items()):
  1024        old = int(troops[tid].skills.get(cls, 0))
  1025        if v != old or cls not in troops[tid].skills:
  1026            edits.append(SkillEdit(troops[tid].file, tid, cls, old, v,
  1027                                   "cell" if (tid, cls) in cells else "clamp"))
  1028    return edits
  ```
  `ranged_ladder.py` imports no other `tools/` module today (:59 to :65).
- Callers of `planned_skill_edits` outside tests: `tools/rebalance_ranged_ladders.py:112` (inside
  `build_context(spec, launchers, troops, game_modules, moduledata, ammo=None, barred=None, militia=frozenset(), exempt=None, sources=None)`,
  :109 to :113) and :737 (the after-apply re-plan in `main`). `main` builds
  `game_modules = Path(args.game_modules)` (:634, the Modules folder), reads militia, then
  `sources = rl.load_upgrade_sources(args.moduledata)` (:689) and calls `build_context(...)` (:691 to :692).
  Its tools imports are at :64 to :67 (`sys.path.insert(...)`, then `fix_upgrade_armour_regressions as fx`,
  `ranged_ladder as rl`, `rebalance_troops as rb`).

### Writer 2: `tools/rebalance_troops.py`

It never looks at `skill_template`: a record's `'old'` is its inline rows, an undeclared skill reads 0.

- Tools imports :38 to :42 (`sys.path.insert`, `taom_schema as ts`, `_gamedir`, `ranged_ladder as rl`,
  `troop_purpose as tp`); `from pathlib import Path` at :36; `SKILL_NAMES` at :51.
- `clamp_upgrade_monotonicity(all_changes, base_on_curve=True, restat_ids=())` (:728 to :846). A record
  with `new` None takes `base = {s: c['old'].get(s, 0) for s in SKILL_NAMES}` (:767). After the clamp:
  ```python
  797    pre_clamp = {tid: dict(vals) for tid, vals in base.items()}
  ...
  810    # A ladder troop's Bow or Crossbow is its cell; lifting it off the cell writes a value the
  811    # ladder tool and the RANGED_LADDER_INVERSION gate both call wrong, and the next ladder run
  812    # would put it back (#617 review). ranged_ladder.planned_skill_edits refuses the same edge.
  813    lifted = [f"{c['id']} {s} {pre_clamp[c['id']][s]} -> {base[c['id']][s]}"
  814              for c in all_changes for s in (c.get('ladder') or {})
  815              if base[c['id']][s] > pre_clamp[c['id']][s]]
  816    if lifted:
  817        raise RuntimeError(
  818            "The clamp would raise a ranged ladder troop off its cell (an upgrade source carries "
  819            "more Bow/Crossbow than the troop's tier cell): " + "; ".join(lifted[:8]) +
  820            ". Rank or tier the lines in tools/ranged_ladders.json so the cell is at least the source's.")
  ```
- `process_file(filepath, item_classes=None, purpose=False, mounts=None, couch_ids=None, slots_only=False, sources=None)`
  (:1075 to :1201). It reads `old_skills` from `npc.find('skills')` (:1113 to :1118), builds `record`
  (:1125 to :1135), then the SKIP_TROOP_IDS branch (:1137 to :1141):
  ```python
  1138        if (bare_id(troop_id) if sources is not None else troop_id) in SKIP_TROOP_IDS:
  1139            record.update(status='SKIPPED (excluded: creature/hand-tuned)', new=None)
  1140            changes.append(record)
  1141            continue
  ```
- `load_external_sources(item_classes=None, mounts=None, cultures=None)` (:1216 to :1276) walks
  `glob.glob(os.path.join(CHARACTERS_DIR, 'npcs_*.xml'))` (`CHARACTERS_DIR`, a module global at :1204),
  keeps characters with upgrade targets, reads `skills` from `./skills/skill` (:1248 to :1249), builds
  a read-only record with `'external': True` (:1250 to :1263), and puts a villager on the curve:
  ```python
  1264            villager = record['id'].startswith('villager_') and npc.find('skills') is not None
  1265            if villager and item_classes:
  ```
- `load_roster(troop_files, item_classes, mounts, purpose=False, couch_ids=None, extra_paths=None)`
  (:1279 to :1306) calls `process_file` twice (:1293, :1297) and `load_external_sources` (:1305).
- `main()` (:1398 to :1551) resolves `game_modules` (the Modules folder, default `DEFAULT_GAME_MODULES`),
  exits unless `os.path.isdir(game_modules)` (:1458 to :1462), builds `item_classes` and `mounts`
  (:1463 to :1465), then `all_changes = load_roster(...)` (:1492 to :1493).
- `write_files` (:1309 to :1335) writes a record whose `final` differs from `'old'` or that misses one of
  the eight skills; it skips `external` and `no_skills` records.
- Another caller of `load_roster`: `tools/analyze_troop_balance.py:223`, `rb.load_roster(files, item_classes, mounts)`.
  It stays as is (see Out of scope).

### The data today (measured at the planned-at commit, read-only scratch scripts)

- The Validator's own index (`ts.Validator(<ModuleData>, [], ts.Registries(set(), {}, set(), set(), set()))._upgrade_troop_index()`):
  3118 characters indexed, 1440 of them templated, 913 upgrade edges with both ends indexed, **0 edges
  with a templated side**. All 1440 templated characters are in `characters/npcs_*.xml`, none carries
  an inline `<skills>` row, and no indexed edge starts or ends at one; `troops/troops_*.xml` has 0
  templated characters.
- `python tools/validate_moduledata.py --code UPGRADE_SKILL_REGRESSION --code UPGRADE_INDEX_EMPTY --code SKILL_TEMPLATE_MISMATCH`
  printed `PASS: no validation issues found.` and exited 0.

### The mirrored C# gate (not changed)

`TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs:227` to :230 skips a
templated edge too (`if (source.Templated || target.Templated) continue;`). It runs in CI without the
game install, so it cannot read the vanilla SkillSets; the brief rules out a C# change. It keeps its
skip; the docs this plan edits say so.

### Conventions that bind this change

- AGENTS.md "TDD": every implementation step below follows a RED step whose failure you quote.
- `.claude/rules/simplicity-criterion.md`: one shared function, no new validator code, no new flag.
- `.claude/rules/moduledata-validation.md` "Discipline": a NEW code needs a row in
  `docs/reference/moduledata-validator-codes.md` and a hook `--code` line. This plan adds no code; the
  existing row changes. The rule file itself holds no row for `UPGRADE_SKILL_REGRESSION` (it links
  the codes doc at :27), so it needs no edit; Done criteria proves that.
- `.claude/rules/data-scripts.md` (read it from your worktree): the writers' XML I/O conventions. This
  plan adds no write path; it only refuses writes.
- No C# type changes, so AGENTS.md "Blast radius first" (graphify) does not apply; the Python
  callers above come from `git grep` at the planned-at commit.
- ADR-002, ADR-007, ADR-008 bind C# only; nothing here touches C#.
- Prose (comments, docs, commit body): no em or en dash; `.claude/rules/output-style.md` Part 2.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | 43028 passed, 3 failed (the three named above), 9 skipped |
| Python tools | `python -B -m unittest discover -s tools/tests -t .` | the baseline's failure set (none), more tests than 10549 |
| One Python module | `python -B -m unittest tools.tests.<module> -v` | the named tests run; read `Ran N tests` and `OK` |
| Data | `python tools/validate_moduledata.py` | 0 ERRORs |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |

Prefix every dotnet command with `TEMP="<tmp>" TMP="<tmp>"` from your dispatch rules, and run every
command from your worktree root. Never `./build.ps1`: it deploys into the game install. Never run
`tools/rebalance_troops.py`, `tools/rebalance_ranged_ladders.py` or `tools/sync_lord_inline_skills.py
--apply`: they write tracked files.

## Scope

**In scope** (the only files you modify):
- `tools/sync_lord_inline_skills.py`: the shared `effective_skills` function
- `tools/taom_schema.py`: `Registries.skill_sets`, `Validator._skill_sets`, the index's `template`
  key, `_upgrade_skill_regressions`
- `tools/validate_moduledata.py`: one no-install WARNING line
- `tools/ranged_ladder.py`: `RangedTroop.template`, the two loaders, `planned_skill_edits`
- `tools/rebalance_ranged_ladders.py`: load the SkillSets in `main`, pass them through `build_context`
  and the after-apply re-plan
- `tools/rebalance_troops.py`: resolve templated records in `process_file` and
  `load_external_sources`, thread `skill_sets` through `load_roster`, refuse a templated raise in
  `clamp_upgrade_monotonicity`, load the SkillSets in `main`
- `tools/tests/test_sync_lord_inline_skills.py`, `tools/tests/test_upgrade_skill_monotonicity.py`,
  `tools/tests/test_ranged_ladder.py`
- `docs/reference/moduledata-validator-codes.md` (the `UPGRADE_SKILL_REGRESSION` row, :18),
  `docs/features/moduledata-validation.md` (one dated entry under `## Changelog`),
  `docs/features/ranged-ladders.md` (:257 to :262 and :437 to :438),
  `docs/features/troop-skill-balance.md` (:262 to :265 and the :332 table row)

**Out of scope** (do NOT touch, even though they look related):
- `TAOM.Tests/**` and every other C# file: the C# mirror keeps its skip (no install in CI); no C# change.
- `.claude/rules/moduledata-validation.md`: it holds no row naming this rule (proved in Done criteria).
- `tools/analyze_troop_balance.py`: it calls `rb.load_roster` without SkillSets; with 0 templated
  troops and sources it is unaffected, and a templated one would make it stop with the RuntimeError
  this plan adds rather than misjudge. A follow-up for the orchestrator (Maintenance notes).
- `tools/taom_query.py`, `tools/check_release_pair.py`: they build a Validator without the install, so
  templated edges stay skipped there, as every other install-bound check is.
- `SKILL_TEMPLATE_MISMATCH` (`validate_moduledata.skill_template_mismatch_issues`) and `_scan`: they
  already resolve templates and stay as they are.
- The vanilla module list in `sync_lord_inline_skills.VANILLA_MODULES` (NavalDLC ships a
  `naval_skill_sets.xml` it does not read): a separate question, Maintenance notes.
- Any ModuleData XML: the shipped data must not change.
- The gates themselves. Never turn a gate green by editing it: deleting or skipping a test, loosening
  an assertion, or adding an allowlist entry (`_LATERAL_BY_DESIGN`, `_RESPECIALIZATION_EXEMPT_EDGES`,
  `RESPECIALIZATION_EXEMPT_EDGES`) without the reason that allowlist requires. STOP and report instead.
- `CHANGELOG.md`, `plans/README.md`, GitHub issues.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- One commit at the end (Step 12). Subject `fix(tools): <version> - <description>`, at most 72
  characters, `<version>` being the `<Version value="...">` in `Main/_Module/SubModule.xml` when you
  commit (`v2.0.40` at the planned-at commit; a hook refuses any other). Suggested:
  `fix(tools): v2.0.40 - judge templated upgrade edges on the SkillSet`.
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. Name the engine rule (template copied, inline rows laid over it), the gate change, the two
  writers' refusal (and that a templated troop in `troops/` gets no kit planning from
  `rebalance_troops`, being read-only), the before and after validator result from Step 10, and that
  the C# mirror still skips. Add `Not-tested: no shipped edge has a templated side, so the resolution ran on synthetic
  fixtures only`. No AI attribution trailer. Write the message to a file and run `git commit -F <file>`.
- Stage explicit paths only: the 13 in-scope files that changed (`git status --porcelain` lists them).

## Steps

### Step 1: record the base

1. Run the drift check at the top of this plan, then save the tree's state for Step 12:
   `git status --porcelain > "<scratch>/status_before.txt"` (it may already list untracked files that
   are not yours, for example under `plans/`).
2. Create `<tmp>`, then run the C# suite and the Python suite once and record the totals lines:
   `TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` and
   `python -B -m unittest discover -s tools/tests -t .` (redirect each to a log under your scratch
   folder; read the totals line from the log).
3. Record the shipped-data result before any edit:
   `python tools/validate_moduledata.py > "<scratch>/validate_before.txt" 2>&1; echo "exit $?"`.
4. Write this probe to `<scratch>/count_edges.py` with the Write tool and run it with
   `python -B "<scratch>/count_edges.py" "<your worktree>"`:
   ```python
   import os, sys
   wt = sys.argv[1]
   sys.path.insert(0, os.path.join(wt, "tools"))
   import taom_schema as ts
   v = ts.Validator(os.path.join(wt, "Main", "_Module", "ModuleData"), [], ts.Registries(set(), {}, set(), set(), set()))
   troops, _ = v._upgrade_troop_index()
   key = "template" if any("template" in t for t in troops.values()) else "templated"
   edges = [(s, d) for s, t in troops.items() for d in t["upgrades"] if d in troops]
   tmpl = [(s, d) for s, d in edges if troops[s][key] or troops[d][key]]
   print(f"indexed: {len(troops)}; templated: {sum(1 for t in troops.values() if t[key])}; "
         f"edges: {len(edges)}; edges with a templated side: {len(tmpl)}")
   ```

**Verify**: C# totals match "Baseline at that commit" (43028 passed, 3 failed with the three named
tests, 9 skipped); Python `Ran 10549 tests` with `OK (skipped=13)`; the validator log ends with its
summary and the exit is 0 (or the difference is explained in your report); the probe prints
`edges with a templated side: 0`. A nonzero templated-edge count is a STOP condition.

### Step 2: RED, the shared resolver

Add a class `EffectiveSkillsTests(unittest.TestCase)` to `tools/tests/test_sync_lord_inline_skills.py`
(the module is imported there as `sync`). Use a literal `sets = {"t": {"Polearm": 70, "Bow": 30}}`.
Tests:

- `test_no_template_is_a_copy_of_the_inline_rows`: `sync.effective_skills(None, {"Bow": 5}, None)`
  equals `{"Bow": 5}`, and mutating the result leaves the input dict unchanged.
- `test_inline_rows_are_laid_over_the_template`: `effective_skills("SkillSet.t", {"Bow": 90}, sets)`
  equals `{"Polearm": 70, "Bow": 90}`.
- `test_a_template_without_inline_rows_is_the_skillset`: `effective_skills("SkillSet.t", {}, sets)`
  equals `{"Polearm": 70, "Bow": 30}`, and mutating the result leaves `sets["t"]` unchanged.
- `test_a_template_that_names_no_skillset_reads_as_an_empty_one`:
  `effective_skills("SkillSet.missing", {"Bow": 40}, sets)` equals `{"Bow": 40}`.
- `test_a_template_without_skillsets_is_refused`: `effective_skills("SkillSet.t", {}, None)` raises
  `ValueError`, and the message names `SkillSet.t`.

Run `python -B -m unittest tools.tests.test_sync_lord_inline_skills -v`.

**Verify**: the five new tests ERROR with `AttributeError` (module `sync_lord_inline_skills` has no
attribute `effective_skills`); the 16 existing tests still pass. Quote the totals line.

### Step 3: GREEN, `effective_skills`

Add to `tools/sync_lord_inline_skills.py`, after `load_skill_sets_from` (it reads no file, so it
belongs beside the loader):

```python
def effective_skills(template: str | None, inline: dict[str, int],
                     sets: dict[str, dict[str, int]] | None) -> dict[str, int]:
    """The skills a character plays with since v1.5.2, the one place the tools resolve a
    skill_template (#626). BasicCharacterObject.Deserialize copies the SkillSet the template names
    into a fresh MBCharacterSkills and lays the inline <skills> rows over it, so a listed row
    overrides its one skill and the SkillSet supplies the rest. UPGRADE_SKILL_REGRESSION
    (taom_schema), ranged_ladder.planned_skill_edits and rebalance_troops read a templated
    character through this.

    No template: a copy of the inline rows. A template `sets` does not hold: the inline rows alone,
    because the engine registers a presumed, empty SkillSet for an id nothing defines
    (MBObjectManager.GetPresumedObject). `sets` comes from load_skill_sets over the install and the
    repo; without it a dangling id cannot be told from an unread one, so a templated character
    raises ValueError rather than being read as its inline rows."""
    if not template:
        return dict(inline)
    if sets is None:
        raise ValueError(f"skill_template {template} cannot be resolved without the SkillSets "
                         f"(load_skill_sets reads them from the install and the repo)")
    resolved = dict(sets.get(template.split(".", 1)[-1], {}))
    resolved.update(inline)
    return resolved
```

The docstring and comments in this plan are drafts: check each claim against the engine excerpt in
Current state before you keep it.

**Verify**: `python -B -m unittest tools.tests.test_sync_lord_inline_skills -v` gives `Ran 21 tests`
and `OK`.

### Step 4: RED, the gate judges a templated edge

In `tools/tests/test_upgrade_skill_monotonicity.py`:

1. Give `ValidatorGateTests._run` (:74 to :76) two optional arguments; `skill_sets` is set as an
   attribute, so the RED fails on an assertion, not a constructor error (`game_modules` is an existing
   `Validator` keyword, :478):
   ```python
   def _run(self, skill_sets=None, game_modules=None):
       reg = ts.Registries({}, {}, {}, {}, {})
       if skill_sets is not None:
           reg.skill_sets = skill_sets
       validator = ts.Validator(self.root, [], reg, game_modules=game_modules)
       return validator._upgrade_skill_regressions()
   ```
2. In `SkillTemplateEdgeTests` (:151 to :189), keep `test_the_upgrade_index_leaves_template_parity_to_its_own_pass`
   and `test_a_template_with_an_empty_block_is_fine`. Rename
   `test_a_templated_edge_is_not_judged_for_regression` (:178) to
   `test_without_skill_sets_a_templated_edge_is_skipped` and rewrite its docstring: with no install and
   no `skill_sets` the SkillSets cannot be read, so the edge is skipped, as every install-bound check
   is; `validate_moduledata.py` prints the skip. Its body and assertion stay.
3. Rewrite the class docstring (:152 to :159) so it says the gate now resolves a template through
   `sync_lord_inline_skills.effective_skills`, and parity stays `SKILL_TEMPLATE_MISMATCH`'s question.
4. Add five tests. Each builds a templated character with the same literal XML shape the existing
   test uses (:183 to :187: `skill_template="SkillSet.<id>"`, an empty `<skills></skills>` or inline
   rows, `<upgrade_targets>`), and an untemplated one with `_troop(...)`:
   - `test_a_templated_source_is_judged_on_its_skillset`: source `p` templated with
     `SkillSet.src`, no inline rows, upgrading into `c`; `c` = `_troop("c", 11, "Infantry", {k: 50 for k in rb.SKILL_NAMES})`;
     `skill_sets={"src": {k: 50 for k in rb.SKILL_NAMES} | {"Polearm": 70}}`. Expect exactly one issue,
     code `UPGRADE_SKILL_REGRESSION`, `entry_id == "c"`, message containing `Polearm 70->50`.
   - `test_a_templated_target_reads_its_skillset_for_an_undeclared_skill`: source `p` =
     `_troop("p", 1, "Infantry", {k: 40 for k in rb.SKILL_NAMES}, ["c"])`; target `c` templated with
     `SkillSet.tgt` and no inline rows; `skill_sets={"tgt": {k: 60 for k in rb.SKILL_NAMES} | {"Bow": 30}}`.
     Expect exactly one issue, `entry_id == "c"`, message containing `Bow 40->30` and NOT containing
     `undeclared` (the SkillSet declares it) and NOT containing `Polearm`.
   - `test_an_inline_row_overrides_the_template`: same source as the previous test; target `c`
     templated with `SkillSet.tgt` and one inline row `<skill id="OneHanded" value="20" />`;
     `skill_sets={"tgt": {k: 60 for k in rb.SKILL_NAMES}}`. Expect exactly one issue whose message
     contains `OneHanded 40->20`.

   The next two pass no `skill_sets`: they test the read from the install. Each makes a temp install
   folder `inst` with `tempfile.mkdtemp()` (remove it with `self.addCleanup(shutil.rmtree, inst, True)`),
   creates `inst/Modules`, and calls `self._run(game_modules=os.path.join(inst, "Modules"))`. The
   Modules folder is what `validate_moduledata.py` passes; the SkillSets are read from its parent.
   - `test_skill_sets_are_read_from_the_install_root`: write
     `inst/Modules/Native/ModuleData/x_skill_sets.xml` holding
     `<SkillSets><SkillSet id="src">` with one `<skill id="<k>" value="<v>" />` per `rb.SKILL_NAMES`
     entry, Polearm 70 and the rest 50, then `</SkillSet></SkillSets>`. Source `p` templated with
     `SkillSet.src`, no inline rows, upgrading into `c` = `_troop("c", 11, "Infantry", {k: 50 for k in rb.SKILL_NAMES})`.
     Expect exactly one issue, `entry_id == "c"`, message containing `Polearm 70->50`. (A `_skill_sets`
     that passes the Modules folder instead of its parent finds no file and gives the `(skill sets)`
     ERROR instead, so this test fails on that mistake.)
   - `test_an_install_with_no_skill_sets_gives_one_error`: `inst/Modules` stays empty. Two templated
     sources, `p1` (`SkillSet.a`) and `p2` (`SkillSet.b`), no inline rows, both upgrading into the
     untemplated `c` of the previous test. Expect exactly one issue: `entry_id == "(skill sets)"`,
     `code == "UPGRADE_SKILL_REGRESSION"`, `severity == ts.Severity.ERROR` (one ERROR per run, not one
     per edge).

Run `python -B -m unittest tools.tests.test_upgrade_skill_monotonicity -v`.

**Verify**: the five new tests FAIL with an assertion error showing 0 issues where 1 is expected (with
`assertEqual(1, len(issues))`, `AssertionError: 1 != 0`): the gate still skips templated edges; the renamed test and the 24 other existing tests pass. Quote the totals line
(`Ran 30 tests`, `FAILED (failures=5)`; 30 because Step 8's four tests come later).

### Step 5: GREEN, the gate

In `tools/taom_schema.py`:

1. Add a field to `Registries`, after `couchable_ids` (:91):
   `skill_sets: dict = None   # SkillSet id -> {skill: value} (sync_lord_inline_skills.load_skill_sets; None = read the install when an edge has a templated side)`.
2. In `_upgrade_troop_index`, replace `"templated": bool(tmpl),` (:2010) with
   `"template": tmpl.group(1) if tmpl else None,` and rewrite the comment at :1984 to :1986: the value is
   the `skill_template` as written; `_upgrade_skill_regressions` resolves it; parity stays
   `SKILL_TEMPLATE_MISMATCH`'s question.
3. Add `_skill_sets(self, issues)` next to `_upgrade_skill_regressions`, on the `_couchable_ids`
   pattern: a registry value wins; no install (`not self.game_modules or not self.game_modules.is_dir()`)
   returns None; otherwise load once into `self._skill_sets_memo` with
   `sl.load_skill_sets(str(self.game_modules.parent), str(self.moduledata))` (the install ROOT is the
   parent of the Modules folder, as `validate_moduledata.py:895` passes it). A load that raises, or
   that returns an empty dict, appends ONE `Issue(severity=Severity.ERROR, code="UPGRADE_SKILL_REGRESSION",
   file="troops/", line=0, entry_id="(skill sets)", message=...)` saying the SkillSets could not be read
   (or none were found under the install and the repo), so no upgrade edge with a skill_template side
   was judged; the memo is then None. Import `sync_lord_inline_skills as sl` lazily with the
   `_sys.path.insert` idiom shown in Current state, INSIDE that `try`, beside the `load_skill_sets`
   call, so an import failure becomes the one `(skill sets)` ERROR rather than a validator crash (the
   commit hook allows a commit when the validator crashes, exit 3).
4. In `_upgrade_skill_regressions`, replace :2161 to :2174 with this shape:
   ```python
                if source["template"] or target["template"]:
                    # Since v1.5.2 a templated character is its SkillSet with the inline rows laid
                    # over it (sync_lord_inline_skills.effective_skills, #626). Without the install
                    # the SkillSets cannot be read and the edge is skipped, never faked; the CLI
                    # says so, and a failed read is one ERROR from _skill_sets.
                    sets = self._skill_sets(issues)
                    if sets is None:
                        continue
                    src_skills = sl.effective_skills(source["template"], source["skills"], sets)
                    tgt_skills = sl.effective_skills(target["template"], target["skills"], sets)
                else:
                    src_skills, tgt_skills = source["skills"], target["skills"]
                exempt = self._RESPECIALIZATION_EXEMPT_EDGES.get(
                    (source_id, target_id), ())
                drops = [
                    f"{s} {src_skills.get(s, 0)}->{tgt_skills.get(s, 0)}"
                    f"{' (undeclared, reads as 0)' if s not in tgt_skills else ''}"
                    for s in self._COMBAT_SKILLS
                    if s not in exempt
                    and tgt_skills.get(s, 0) < src_skills.get(s, 0)
                ]
   ```
   Inside `_upgrade_skill_regressions`, import `sl` in one place only: inside the templated branch,
   directly after the `if sets is None: continue` lines and before the first `sl.effective_skills`
   call, with the lazy idiom. This is a second import beside the one Step 5.3 puts inside
   `_skill_sets`'s `try`; keep both (without 5.3's, `_skill_sets` raises `NameError` inside its own
   `try` and every templated edge gets the `(skill sets)` ERROR). Never import `sl` at the top of
   `_upgrade_skill_regressions` or of the module: then every run of the gate, including the
   no-install, `taom_query.py` and `check_release_pair.py` paths, would depend on that module and
   `_gamedir` importing cleanly. `_skill_sets` is called only for a templated edge, so a tree with
   none never reads a SkillSet file. (Other modules may still import `sync_lord_inline_skills`:
   `validate_moduledata.py:531` does, and `rebalance_troops` will after Step 9.1; do not write a
   comment that says the gate never imports it.)
5. In the message (:2180 to :2185), when `target["template"]` is set, append one sentence: this troop's
   skills come from `<template>` with its inline rows laid over it, so raise the SkillSet (an inline
   row that differs is SKILL_TEMPLATE_MISMATCH) rather than re-running the clamp, which refuses a
   templated character. Leave the untemplated message unchanged. The appended sentence must not
   contain the words `undeclared` or `Polearm`: `test_a_templated_target_reads_its_skillset_for_an_undeclared_skill`
   asserts both are absent from the message.
6. In `tools/validate_moduledata.py`'s no-install branch, after the `TEMPLATE_CODE` warning (:904 to
   :905), add:
   ```python
           print(f"WARNING: UPGRADE_SKILL_REGRESSION SKIPPED for upgrade edges with a skill_template\n"
                 f"         side: their SkillSets live in the install.", file=sys.stderr)
   ```

**Verify**: `python -B -m unittest tools.tests.test_upgrade_skill_monotonicity -v` gives `Ran 30 tests`
and `OK`. Then `git grep -n '"templated"' -- tools` prints nothing.

### Step 6: RED, the ladder writer resolves a template

In `tools/tests/test_ranged_ladder.py`:

1. Replace `test_skill_clamp_skips_a_templated_side_and_a_templated_ladder_troop_takes_its_cell`
   (:717 to :731) with the tests below. Set a template on a `_troop(...)` result with
   `t.template = "SkillSet.<id>"` (the attribute assignment works on the dataclass before and after
   the field exists, so the RED fails on behaviour). Use `_launchers()`, `_spec()` and the troops of
   the replaced test: `man_b` = `_troop("man_b", "man", 26, [{"Item0": "man_bow"}], skills={...})` is a
   ladder troop whose Bow cell is 90; `man_f` = `_troop("man_f", "man", 26, [{"Item0": "sword"}], ...)`
   is not on the ladder.
   - `test_skill_clamp_judges_a_templated_source_on_its_skillset`: villager
     `_troop("villager_man", "", 6, [], upgrades=["man_b"], skills={})`, `villager.template = "SkillSet.v"`;
     `troops = {"man_b": ...skills={"Bow": 60}}`; `planned_skill_edits(..., sources={"villager_man": villager}, skill_sets={"v": {"Bow": 120}})`
     raises `rl.LadderError` whose text contains `UPGRADE_SKILL_REGRESSION` (the cell 90 sits under
     the source's 120).
   - `test_skill_clamp_raises_a_non_ladder_child_to_a_templated_source`: villager templated
     `SkillSet.v` upgrading into `man_f` (skills `{"Bow": 50}`); `skill_sets={"v": {"Bow": 120}}`;
     the edits contain `("man_f", "Bow", 50, 120, "clamp")` as `(troop, skill, old, new, reason)`.
   - `test_a_templated_child_is_never_written_inline`: `man_a` = the ladder troop of
     `test_skill_clamp_raises_a_non_ladder_child_and_refuses_a_ladder_one` (:704; `man_a` at :706, Bow 100,
     `upgrades=["man_f"]`); `man_f` templated `SkillSet.f`, skills `{}`;
     `skill_sets={"f": {"Bow": 50}}`. Raises `rl.LadderError` whose text contains
     `SKILL_TEMPLATE_MISMATCH` and `SkillSet.f`.
   - `test_a_templated_ladder_troop_on_its_cell_needs_no_edit_and_off_it_is_refused`: `man_b` templated
     `SkillSet.b`, skills `{}`; with `skill_sets={"b": {"Bow": 90}}` the edits are `[]`; with
     `skill_sets={"b": {"Bow": 60}}` it raises `rl.LadderError` containing `SkillSet.b`.
   - `test_a_templated_edge_without_skillsets_is_refused`: the first test's villager and troops, no
     `skill_sets`: raises `rl.LadderError` containing `SkillSet.v`.
2. Update `test_loader_marks_a_skill_template` (:733 to :745): assert
   `rl.load_ranged_troops(md)["man_archer"].template == ""` and
   `rl.load_upgrade_sources(md)["villager_man"].template == "SkillSet.x"`.

Run `python -B -m unittest tools.tests.test_ranged_ladder -v`.

**Verify**: the five new tests ERROR with `TypeError` (`planned_skill_edits() got an unexpected keyword
argument 'skill_sets'`) or FAIL on the missing refusal (the last one: no LadderError raised), and
`test_loader_marks_a_skill_template` ERRORs with `AttributeError` (`RangedTroop` has no `template`);
every other test passes. Quote the totals line (`Ran 116 tests`).

### Step 7: GREEN, the ladder writer

In `tools/ranged_ladder.py`:

1. `RangedTroop`: replace the `templated` field (:148) with
   `template: str = ""  # skill_template= as written; sync_lord_inline_skills.effective_skills resolves it (#626)`.
   At :711 and :732 pass `template=npc.get("skill_template") or ""`.
2. `planned_skill_edits`: add the keyword `skill_sets=None` last. Inside, before `value`:
   ```python
       def resolved(tid):
           t = everyone[tid]
           if not t.template:
               return t.skills
           if skill_sets is None:
               raise LadderError(
                   f"{tid} names skill_template {t.template}, so its skills are that SkillSet with its "
                   f"inline rows laid over it, and the SkillSets were not given; pass skill_sets "
                   f"(sync_lord_inline_skills.load_skill_sets)")
           return _sync().effective_skills(t.template, t.skills, skill_sets)
   ```
   where `_sync()` is a small module-level helper that inserts `tools/` (`Path(__file__).resolve().parent`)
   on `sys.path` once and returns the imported `sync_lord_inline_skills` module (add `import sys`; the
   module imports nothing from `tools/` today, so keep the import lazy). `value(tid, cls)` returns
   `new.get((tid, cls), int(resolved(tid).get(cls, 0)))`. Delete the skip at :1007 to :1008.
3. In the final loop (:1022 to :1028), before computing `old`:
   ```python
           if troops[tid].template:
               have = int(resolved(tid).get(cls, 0))
               if v != have:
                   raise LadderError(
                       f"{tid} would take {cls} {v} ({'cell' if (tid, cls) in cells else 'clamp'}), but its "
                       f"skills come from skill_template {troops[tid].template} with its inline rows laid "
                       f"over it, and an inline row that differs from the SkillSet is "
                       f"SKILL_TEMPLATE_MISMATCH. Set {cls} {v} in that SkillSet, or drop the template")
               continue
   ```
   This reverses the "a templated ladder troop still takes its cell" behaviour of the #617 second review
   (`docs/reviews/rca-ranged-rebalance-second-review-2026-09-18.md`), which predates
   `SKILL_TEMPLATE_MISMATCH` (2026-09-19, `docs/features/moduledata-validation.md` Changelog): an
   inline cell row on a templated troop is now an error the commit hook blocks. Say so in the commit body.
4. Rewrite the docstring's last sentences, from "An edge with" at the end of :972 through :976 (keep
   the start of :972, "`sources` are read-only ... (the villagers)."): a templated side is judged on its SkillSet
   with the inline rows laid over it (`skill_sets`, required when a templated troop takes part), and a
   templated troop is never written: a needed change raises LadderError naming its SkillSet.
5. In `tools/rebalance_ranged_ladders.py`: add `import sync_lord_inline_skills as sl  # noqa: E402  load_skill_sets (#626)`
   after :67; give `build_context` a trailing keyword `skill_sets=None` and pass it to
   `rl.planned_skill_edits` (:112); in `main`, after `sources = rl.load_upgrade_sources(args.moduledata)`
   (:689), add `skill_sets = sl.load_skill_sets(str(game_modules.parent), str(args.moduledata))`, then
   refuse an empty load as the gate does, in `main`'s own style (:635 to :638):
   `if not skill_sets: print(f"ERROR: no SkillSet found under {game_modules.parent} and the repo. Nothing was written."); return 2`
   (as three lines). Pass `skill_sets=skill_sets` to `build_context` (:691) and to the re-plan at :737.

**Verify**: `python -B -m unittest tools.tests.test_ranged_ladder -v` gives `Ran 116 tests` and `OK`.
`git grep -n -E '\.templated\b|templated=|[[:space:]]templated:' -- tools/ranged_ladder.py tools/rebalance_ranged_ladders.py tools/tests/test_ranged_ladder.py`
prints nothing and exits 1 (at the planned-at commit it prints 8 lines: the field at :148, the two
loader arguments, the skip at :1007 and four test lines; the new test names hold `_templated_`, which
this pattern does not match).

### Step 8: RED, the troop rebalancer resolves a template

Add a class `TemplatedRecordTests(unittest.TestCase)` to `tools/tests/test_upgrade_skill_monotonicity.py`
(it already imports `rebalance_troops as rb`). Use a temp folder in `setUp` and remove it in
`tearDown`; for the last test, save and restore `rb.CHARACTERS_DIR`.

- `test_the_clamp_refuses_to_raise_a_templated_character`: records built like `ClampTests._record`
  (:259 to :262): parent `{s: 50}` upgrading into child `{s: 10}`, and add `child["template"] = "SkillSet.t"`,
  `child["new"] = None` and `child["status"] = "SKILLSET"`. The status key is required: `_record`
  sets none, and without the refusal the clamp's final loop reads `c['status']` for a record whose
  `new` is None (`tools/rebalance_troops.py:839`), so the RED would be `KeyError: 'status'` instead of
  the expected one. `rb.clamp_upgrade_monotonicity(records)` raises `RuntimeError` whose text
  contains `SKILL_TEMPLATE_MISMATCH` and `SkillSet.t`.
- `test_a_templated_troop_reads_as_its_skillset`: write `troops_test.xml` holding a templated
  `<NPCCharacter id="p" level="1" ... skill_template="SkillSet.t">` with `<skills><skill id="Bow" value="90" /></skills>`
  and an upgrade target `c`, plus `c` from `_troop(...)`. `recs = rb.process_file(path, skill_sets={"t": {"Bow": 120, "Polearm": 70}})`;
  the `p` record has `template == "SkillSet.t"`, `new is None`, a status starting with `SKILLSET`, and
  `old == {s: 0 for s in rb.SKILL_NAMES} | {"Bow": 90, "Polearm": 70}`.
- `test_a_templated_troop_without_skillsets_is_refused`: the same file, `rb.process_file(path)` raises
  `RuntimeError` containing `SkillSet.t`.
- `test_a_templated_villager_stays_off_the_curve_and_reads_as_its_skillset`: in a temp
  `characters/` folder write `npcs_gondor.xml` with two villagers that are identical except that
  `villager_b` carries `skill_template="SkillSet.v"`: each has `level="6"`, a `<skills>` block with
  `<skill id="Polearm" value="10" />`, an `<EquipmentRoster>` holding
  `<equipment slot="Item0" id="Item.test_fork" />`, and an upgrade target. Point `rb.CHARACTERS_DIR`
  at the folder and call
  `rb.load_external_sources(item_classes={"test_fork": "Polearm"}, mounts={}, skill_sets={"v": {"Bow": 40}})`.
  First assert the CONTROL: the `villager_a` record has `villager` True (the fixture reaches the curve;
  if it does not, fix the fixture, never the code). Then assert the `villager_b` record has no
  `villager` key, `external` True, `template == "SkillSet.v"`, and
  `old == {s: 0 for s in rb.SKILL_NAMES} | {"Polearm": 10, "Bow": 40}`.

Run `python -B -m unittest tools.tests.test_upgrade_skill_monotonicity -v`.

**Verify**: the four new tests fail: the first with `RuntimeError not raised`, the second and fourth
with `TypeError` (unexpected keyword argument `skill_sets`), the third with `RuntimeError not raised`.
The 30 earlier tests pass. Quote the totals line (`Ran 34 tests`).

### Step 9: GREEN, the troop rebalancer

In `tools/rebalance_troops.py`:

1. After :42 add `import sync_lord_inline_skills as sl  # noqa: E402  effective_skills, load_skill_sets (#626)`.
2. Add a helper beside `process_file`:
   ```python
   def templated_old(troop_id, template, inline, skill_sets):
       """A templated character's on-disk skills as the engine reads them: all eight, its SkillSet
       with the inline rows laid over it (sync_lord_inline_skills.effective_skills, #626). Such a
       character is read-only here: the clamp refuses to raise it (clamp_upgrade_monotonicity)."""
       if skill_sets is None:
           raise RuntimeError(
               f"{troop_id} names skill_template {template}, and its skills cannot be read without the "
               f"SkillSets; main() loads them from the install. Nothing was written.")
       eff = sl.effective_skills(template, inline, skill_sets)
       return {s: eff.get(s, 0) for s in SKILL_NAMES}
   ```
3. `process_file`: add the keyword `skill_sets=None` last. Directly after the `record = {...}` dict
   (:1125 to :1135) and BEFORE the SKIP_TROOP_IDS branch (:1137), insert:
   ```python
           template = npc.get('skill_template')
           if template:
               record.update(old=templated_old(troop_id, template, old_skills, skill_sets),
                             template=template, new=None,
                             status=f'SKILLSET (read-only: skill_template {template})')
               changes.append(record)
               continue
   ```
4. `load_external_sources`: add the keyword `skill_sets=None` last. After `skills = {...}` (:1248 to
   :1249): `template = npc.get('skill_template')`, and when set, `skills = templated_old(npc.get('id', ''), template, skills, skill_sets)`.
   Add `'template': template` to the record only when set (keep untemplated records unchanged), and make
   the villager test `... and npc.find('skills') is not None and not template` (:1264).
5. `load_roster`: add the keyword `skill_sets=None` last and pass `skill_sets=skill_sets` to both
   `process_file` calls and to `load_external_sources`.
6. `clamp_upgrade_monotonicity`: after the `lifted` refusal (:816 to :820) add:
   ```python
       # A templated character plays with its SkillSet, the inline rows laid over it (#626). A raised
       # inline row would differ from the SkillSet, which SKILL_TEMPLATE_MISMATCH refuses, so the clamp
       # refuses first. ranged_ladder.planned_skill_edits refuses the same.
       from_template = [f"{c['id']} {s} {pre_clamp[c['id']][s]} -> {base[c['id']][s]} ({c['template']})"
                        for c in all_changes if c.get('template') for s in SKILL_NAMES
                        if base[c['id']][s] > pre_clamp[c['id']][s]]
       if from_template:
           raise RuntimeError(
               "The clamp would raise a character whose skills come from a skill_template: "
               + "; ".join(from_template[:8]) + ". An inline row that differs from the SkillSet is "
               "SKILL_TEMPLATE_MISMATCH, so raise the SkillSet itself or drop the template. "
               "Nothing was written.")
   ```
   Name the list `from_template`, never `templated`: the Done criteria grep for `[[:space:]]templated:`
   over `tools/` would match `if templated:`.
7. `main()`: after the item-class `print` (:1465) add
   `skill_sets = sl.load_skill_sets(str(Path(game_modules).parent), MODULEDATA_DIR)`; refuse an empty
   load in the style of the `isdir` check above it (:1458 to :1462):
   `if not skill_sets:` print `f"ERROR: no SkillSet found under {Path(game_modules).parent} and the repo.\n       A templated character cannot be read without them. Nothing was written."`
   and `sys.exit(1)`; then `print(f"SkillSets: {len(skill_sets):,}")`. Pass `skill_sets=skill_sets` to
   `load_roster` (:1492).

**Verify**: `python -B -m unittest tools.tests.test_upgrade_skill_monotonicity -v` gives `Ran 34 tests`
and `OK`. Then `python -B -m unittest tools.tests.test_main_weapon_slots tools.tests.test_troop_skill_curve tools.tests.test_militia_elite_bonus tools.tests.test_kingdom_caps tools.tests.test_ranged_ladder`
ends with `OK` (the other modules that call `load_roster`, `process_file` or the clamp).

### Step 10: the shipped-data gate, before and after

1. `python tools/validate_moduledata.py > "<scratch>/validate_after.txt" 2>&1; echo "exit $?"`.
2. Compare the finding lines of `validate_before.txt` and `validate_after.txt`. A finding line is
   printed by `taom_schema.format_report` (:4049) as two spaces, the severity, the code: for example
   `  ERROR   UPGRADE_SKILL_REGRESSION ...`. Extract them with
   `grep -E "^  (ERROR|WARNING|INFO) " <log>` on each log, plus the `PASS:` line if present, then `diff`.
3. Re-run the Step 1 probe; it now reads the `template` key.

**Verify**: the same exit code as Step 1; `grep -c -E "^  (ERROR|WARNING|INFO) +UPGRADE_SKILL_REGRESSION" <log>`
gives the same count on both logs (0 at the planned-at commit). This counts finding lines only: the
no-install `WARNING: UPGRADE_SKILL_REGRESSION SKIPPED ...` line Step 5.6 adds starts at column 0 with
a colon, and the summary's per-code lines carry no severity, so neither is counted. The finding-line
diff is empty (no new finding of any code); the probe prints `edges with a templated side: 0`. Any new `UPGRADE_SKILL_REGRESSION` finding is a STOP
condition: list each one for the maintainer and change nothing to silence it.

### Step 11: the docs

Re-read each passage before editing; every sentence below is a draft to check against the code you
wrote.

1. `docs/reference/moduledata-validator-codes.md:18`, the `UPGRADE_SKILL_REGRESSION` row: add that a
   templated side is judged on its effective skills (the SkillSet with the inline rows laid over it,
   `BasicCharacterObject.Deserialize` since v1.5.2, through `sync_lord_inline_skills.effective_skills`);
   that without the install those edges are skipped and the CLI prints the skip; that a SkillSet read
   failure is one ERROR; and that a templated target is repaired in its SkillSet, because the clamp
   refuses to write it.
2. `docs/features/moduledata-validation.md`: a new first entry under `## Changelog` (above the
   `- 2026-11-14 (#1271)` line, :984): `- 2026-11-19 (#626): ...` stating the gate now resolves
   templates instead of skipping the edge, the two writers follow through the same function and refuse
   to write a templated character, the shipped result before and after from Step 10 (with the date),
   and that the C# `TroopUpgradeSkillMonotonicityTests` still skips templated edges (no install in CI).
3. `docs/features/ranged-ladders.md:257` to :262: replace "The tools do not resolve templates ... the
   inline row wins." with the new behaviour (judged on the SkillSet with the inline rows laid over it;
   a templated troop is never written; a needed change raises and names its SkillSet). Keep the
   sentences about the `npcs_*` files and the lords. At :437 to :438 replace "templated edges skipped and a
   templated ladder troop taking its cell" with the new test coverage.
4. `docs/features/troop-skill-balance.md:262` to :265: replace "both upgrade gates skip templated
   characters when judging an edge, because their base skills live in a SkillSet these files cannot
   see (no troop or villager edge has a templated side today)." with: the Python gate and both writers
   resolve the template; the C# gate, which runs without the install, still skips such an edge; and
   the dated count (2026-11-19: 0 of 913 edges has a templated side). In the :332 table row replace
   "25 synthetic-data tests" with the count from `python -B -m unittest tools.tests.test_upgrade_skill_monotonicity`
   (`Ran N tests`) and add the template resolution and the clamp refusal to its list.

**Verify**: `python tools/lint_docs.py --fail-on-drift` exits 0, and
`git grep -n -i -E "do not resolve templates|templated edges skipped|skipped rather than resolved|both upgrade gates skip" -- docs tools`
prints nothing outside `docs/reviews/` (dated RCAs are history and stay).

### Step 12: full suites, then commit

1. `TEMP="<tmp>" TMP="<tmp>" dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`
2. `TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`
3. `python -B -m unittest discover -s tools/tests -t .`
4. `git status --porcelain`, then stage the changed in-scope paths by name and commit as "Git
   workflow" says.

**Verify**: build exit 0; C# totals equal Step 1's (no C# changed); Python `Ran` equals Step 1's count
plus 18 (5 + 5 + 4 + 4 new; the replaced ladder test and the renamed gate test are not new) with `OK`
and the same skipped count; after the commit, `git status --porcelain` prints exactly what
`<scratch>/status_before.txt` holds (compare with `diff`; no output); `git log -1 --format=%s` shows
the subject.

## Test plan

- `tools/tests/test_sync_lord_inline_skills.py`, `EffectiveSkillsTests` (5): no template, rows over a
  template, template alone, dangling template, no SkillSets refused.
- `tools/tests/test_upgrade_skill_monotonicity.py`, `SkillTemplateEdgeTests` (5 new, 1 renamed): a
  templated source judged, a templated target's undeclared skill read from its SkillSet, an inline row
  overriding it, the SkillSets read from the install ROOT (the parent of the Modules folder), an
  install with no SkillSets giving exactly one `(skill sets)` ERROR for two templated edges, and the
  no-install skip.
- `tools/tests/test_upgrade_skill_monotonicity.py`, `TemplatedRecordTests` (4): the clamp refusal,
  `process_file` resolution, the refusal without SkillSets, `load_external_sources` resolution with a
  control twin proving the villager fixture reaches the curve.
- `tools/tests/test_ranged_ladder.py` (5 new replacing 1, 1 updated): templated source judged and
  refused against a cell, a non-ladder child raised to a templated source, a templated child never
  written, a templated ladder troop on and off its cell, no SkillSets refused.
- Pattern: model the gate tests on `ValidatorGateTests` (:58 to :149), the clamp tests on `ClampTests`
  (:256 to :294), the ladder tests on `test_skill_clamp_raises_a_non_ladder_child_and_refuses_a_ladder_one`.
- Not testable here: the live run of `rebalance_troops.main()` and `rebalance_ranged_ladders.main()`
  against the install (they write tracked files); their new lines only load the SkillSets, refuse an
  empty load, and pass them on. Name them in the `Not-tested:` trailer.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] `dotnet test TAOM.Tests ...` totals equal the baseline (43028 passed, the same 3 failed, 9 skipped)
- [ ] `python -B -m unittest discover -s tools/tests -t .` ends `OK`, with 18 more tests than Step 1
- [ ] `python -B -m unittest tools.tests.test_sync_lord_inline_skills tools.tests.test_upgrade_skill_monotonicity tools.tests.test_ranged_ladder -v`
      lists every new test name above as `ok`
- [ ] Step 10: the validator's exit code and finding lines are identical before and after, and the
      probe prints `edges with a templated side: 0`
- [ ] `git grep -n -E '"templated"|\.templated\b|templated=|[[:space:]]templated:' -- tools` prints nothing
- [ ] `git grep -n "UPGRADE_SKILL_REGRESSION" -- .claude/rules/moduledata-validation.md` prints nothing
      (the rule file holds no row for this code, so it needed no edit)
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0
- [ ] After the commit, `git status --porcelain` equals `<scratch>/status_before.txt` from Step 1, and
      `git show --stat HEAD` lists only in-scope files
- [ ] Every comment, docstring, doc line and test oracle this plan supplied was re-checked against the
      code it describes

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts.
- The Step 1 probe finds an upgrade edge with a templated side, or Step 10 finds any new
  `UPGRADE_SKILL_REGRESSION` (or any other new finding): list each for the maintainer; never silence
  one with an allowlist entry, a skip or a data edit.
- `sync_lord_inline_skills.load_skill_sets` returns no SkillSet for the install on this machine (the
  Step 10 run would then report the `(skill sets)` ERROR only if an edge were templated, so check it
  directly: a probe script written to your scratch folder with the Write tool that prints
  `len(sl.load_skill_sets(<install root>, <worktree>/Main/_Module/ModuleData))` must print more than 0;
  744 at the planned-at commit).
- Making the gate resolve templates turns any existing test red (the five Step 4 adds are new, not
  existing), or a module that calls `load_roster`, `process_file`, `clamp_upgrade_monotonicity` or
  `planned_skill_edits` fails in Step 9's run: report the test and its message.
- The fix seems to need a C# file, `.claude/rules/moduledata-validation.md`, a hook, or a ModuleData file.
- The engine behaviour in "Current state" (template copied, inline rows laid over it, dangling id an
  empty presumed SkillSet) is contradicted by anything you read: report it; do not decompile and
  improvise.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: #626. When this lands, the issue's last open half (the upgrade-edge skip and the two writers)
  is done; close it at merge with a comment naming the commit.
- Follow-up issue to consider: `tools/analyze_troop_balance.py:223` calls `rb.load_roster` without
  SkillSets, so a future templated troop would stop it with the new RuntimeError; and the C# mirror
  (`TroopUpgradeSkillMonotonicityTests.cs:227` to :230) still skips templated edges.
- No `/localize` (no player-facing text). No new feature doc: the existing feature docs are updated in
  Step 11.
- Review lens: tooling.

## After merge: the maintainer's actions

None. The commit hook runs the same validator code; nothing to restart.

## Maintenance notes

- `effective_skills` is now the one place the tools resolve a template. A future engine change to
  `BasicCharacterObject.Deserialize` (the copy-then-overlay at :337 to :364) must change it, and
  `SKILL_TEMPLATE_MISMATCH` with it.
- `sync_lord_inline_skills.VANILLA_MODULES` reads Native, SandBoxCore, SandBox and StoryMode. NavalDLC
  ships `ModuleData/naval_skill_sets.xml`, which no tool reads; if TAOM ever loads NavalDLC, a template
  defined only there would resolve as empty here. Not in scope.
- The review should probe: the empty-dict and exception paths of `Validator._skill_sets` (one ERROR,
  only when a templated edge exists); that `_skill_sets` is never called on a tree with no templated
  edge (the gate's cost stays as before); that `process_file` places the template branch before the
  SKIP_TROOP_IDS branch, so a skipped creature with a template still reads its SkillSet; and that the
  ladder refusal reverses the #617 second review's "templated ladder troop takes its cell" for the
  reason stated in Step 7.
- The writers still read templated characters as read-only. If the maintainer ever wants a writer to
  edit a SkillSet directly, that is a new feature, not a change to these refusals.
- `process_file`'s template branch sits before the kit plan, so a templated troop in `troops/` or the
  second root also gets no `--fix-purpose` or `--main-weapon-slots` planning (`main_weapon`,
  `item0_swaps`), as a `SKIP_TROOP_IDS` troop gets none. This is deliberate (the character is
  read-only); no such troop exists at the planned-at commit (0 templated in `troops/` and in
  `fords_of_war/fords_troops.xml`). Say so in the commit body.
- `effective_skills` reads the set id as `template.split(".", 1)[-1]`, as `_scan` does; the engine
  reads `value.Split('.')[1]` (`MBObjectManager.ReadObjectReferenceFromXml`). The two differ only for
  an id with two dots (`SkillSet.a.b` is `a` in the game, `a.b` here). If such an id ever ships,
  change both to the engine's split.
