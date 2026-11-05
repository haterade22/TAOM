# Plan 101: Put the public 1.4.8 troop tree on the 1.4.x trunk and record the baseline every later plan uses

> **Executor instructions**: This plan has three parts with three different owners. Part A is the
> maintainer's (it writes into the game install, which no session may do). Part B is any ordinary
> session on the laptop, working only in its own git worktree. Part C is the integrator (the
> maintainer, or the session the maintainer names), working in the main checkout. Follow your part step by step,
> run every verification command and confirm its expected result before moving on. If anything in
> "STOP conditions" occurs, stop and report; do not improvise. Part C updates this plan's status row
> in `plans/README.md`. Every code block below starts at column 0: copy it exactly as shown.
>
> **Overnight run (D25)**: this run proceeds on the `v2.0.26` Armory under D25 in
> `plans/_audit/2026-11-04-port148/data/brief-workflow3-decisions.md`. D25 overrides Part A, B1.1, the Step A2 STOP, the
> `N_MERGED` STOP and the ModuleData gate STOP below; those measurements are recorded as provisional. The baseline block's
> first line says so. All build, test, hash and ref checks still gate.
>
> **Drift check (run first, read-only)**: the plan is pinned to exact commits, so drift means a ref
> moved. Run the Step B1 commands. `origin/bannerlord-1.4.5` must still be `1b00d1acd`,
> `origin/fix/public-armory-ids-145` must still be `8df90444c`, and local `bannerlord-1.4.5` must be
> `c5b84fb41` or `1b00d1acd`. Anything else is a STOP.

## Status

- **Priority**: P1 (gate for the whole programme; XML-DATA-03 and RELEASE-INTEGRITY-04 are P1)
- **Effort**: S (one executor session, mostly waiting on build, tests and validators; two short maintainer steps)
- **Risk**: MED (moves the shared `bannerlord-1.4.5` ref and commits in the main checkout; every step is hash-checked and the ref only fast-forwards)
- **Depends on**: none (first work package)
- **Category**: migration (branch sync) and data
- **Wave / lane**: wave 0, lane "gate". WP102 (harness lane) and WP104 (code lane) start after this merges.
- **Executor**: laptop only (the desktop has no 1.4.8 engine). Part A: maintainer. Part B: ordinary session in worktree `C:\Users\mikew\source\repos\taom-port\101`. Part C: integrator in `C:\Users\mikew\source\repos\TAOM`.
- **Decisions applied**: D1 (WP101 is the only WP that hand-edits `CHANGELOG.md`), D3 (no Serena edit tools), D8 (subjects carry `v2.0.28` until the v2.4.0 release commit), D9 (no CI on 1.4.x: every gate runs locally), D10 (ordinary sessions execute plans), D15 (`taom_body_length` noise is expected from the public Armory), D19 (git only through the Bash tool), D20 (refresh the laptop Armory to the public 2026-09-29 build and record the validator baseline; data-touching commits before that are a STOP)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd`; public `origin/fix/public-armory-ids-145` `8df90444c`; local `bannerlord-1.4.5` `c5b84fb41`; 1.5.x `origin/bannerlord-1.5.x` `e45f6df2c`; 2026-11-04
- **Issue**: tracking reference #697 ("Public 1.4.8 release: troop data for the 2026-09-29 Armory", OPEN). The baseline record and the programme commit are new work with no issue: orchestrator creates the issue before implementation lands.

## Why this matters

Players run the public 1.4.8 build. Its `TAOM.dll` was built from `8df90444c` (the Harad player's
taom_debug log, line 2: `[BuildStamp] TAOM=v2.0.0.0 build.20260930-120651Z+8df90444cc2f...`), and that
commit lives only on `origin/fix/public-armory-ids-145`. The 1.4.x trunk, which every later work
package branches from, still carries the troop tree from before #697: its 16 troop files name 156 item
ids that the public troop tree dropped (76 of them retired `ladder_*` bows), and 92a30ba42's body
records 589 broken item refs for that tree against the public 2026-09-29 Armory. In v1.4.8 an unknown
item id loads as an empty equipment slot with no error, so a release or a port cut from the trunk
would ship archers without bows. The public branch in turn lacks the trunk's five newer commits
(1b00d1acd's Uruk fixes among them). Separately, the laptop's Armory is a v2.0.26 copy from
2026-09-07 with no `ladder_` items at all, so every item verdict made on this machine describes an
Armory no player runs (XML-DATA-02). This plan merges the two heads once, refreshes the laptop
Armory, records the build, test, data and harness baseline that all 44 later plans compare against,
and puts the programme's plans and decisions into git.

## Current state

### Refs (read this session, 2026-11-04)

| Name | Full SHA | Note |
|---|---|---|
| local `bannerlord-1.4.5` (main checkout HEAD) | `c5b84fb415259ba2ab08401694195f8a88891f1d` | 5 commits behind origin; merge base of both heads |
| `origin/bannerlord-1.4.5` | `1b00d1acd9f6ceda81709067ca88ee1393115b48` | trunk tip, v2.0.28 |
| `origin/fix/public-armory-ids-145` | `8df90444cc2f1e6d83bb95d2164ab764b4fcae65` | public build; no other remote branch contains it |

Trunk commits since `c5b84fb41` (all labelled `v2.0.28`): `f8c9c8ecb` fix(hero-race) Load Game
preview rider; `55d779012` fix(custom-battles) named commanders, default troops (#709); `d906fc921`
fix(marketplace) Mouth of Sauron's helm; `ccef172ad` chore(ci) remove the GitHub Actions workflow;
`1b00d1acd` fix(campaign) 1.4.8 supply, desertion and Uruk fixes.

Public commits since `c5b84fb41`: `92a30ba42` fix(data) 1.5.x troop tree for the new Armory ids;
`10a52fb3d` fix(troops) add gondor_ring_peasant and update retired troop compatibility (no version
label: history, left as is); `8df90444c` docs(data) review record for the 1.4.8 troop tree (#697).

Main checkout state: ` M .serena/project.yml` (the maintainer's own uncommitted edit),
`?? plans/_audit/2026-11-04-port148/` plus, by the time you run, the orchestrator's `plans/1*.md`
and a modified `plans/README.md`. One stash: `stash@{0}: On bannerlord-1.4.5: discarded pre-pull:
mcp blender + serena regen + settings.local`. Leave all of these alone.

### What the merge does (simulated this session with git merge-tree)

`git merge-tree --write-tree --name-only --no-messages 1b00d1acd... 8df90444c...` exits 1 and prints
tree `6905de4024b10a94ec02641b34342700df703fdc` and one conflicted path,
`docs/reviews/lessons/data-content-cultures.md`. Facts about the result:

- Files changed on both sides: only `Main/_Module/ModuleData/troops/troops_isengard.xml` (auto-merges)
  and the lessons file (conflicts). `CHANGELOG.md` and `.serena/project.yml` change on the public
  side only, so they merge cleanly. (XML-DATA-03 expected conflicts in those three; the simulation
  does not bear that out.)
- `troops_isengard.xml`: the trunk moves `sk_uruk_hai_skirt_a1` from slot `Cape` to slot `Body` for
  `urukhai_champion` (lines 1341-1342 on origin) and `urukhai_berserker` (1608-1609). The public side
  changes only single lines elsewhere (nearest hunks at 1132, 1459, 1531, 1663). The merged blob is
  `15df2d81063c842ebbc0c297146d9af2c2845cc5` and keeps `slot="Body"` at both places.
- Lessons conflict: both sides appended at end of file. Ours (trunk, 13 lines): "A filter that
  replaces a vanilla hard-coded list admits every culture vanilla never listed..." and "An id
  "exists" only in the game types whose XML defines it...". Theirs (public, 6 lines): "A data port
  between engine branches checks what the source commits changed OUTSIDE the ported files". Keep
  both, trunk entries first. The resolved file (1,434 lines, LF) hashes to blob
  `3a3298e6967b6324d5d0ed7ea46193a75c37b00d`. The file holds one vertical-tab byte, so the resolver
  below splits on LF only.
- `.serena/project.yml`: blob `b56198b5ae308c7e28f6729d022d5b48768cdde0` at both `c5b84fb41` and
  `1b00d1acd`; `10a52fb3d` rewrote it to `152e285c...` (Serena's regenerated language-server list,
  49 lines of tool churn). Keep the trunk blob. Two reasons: it is a personal tool config, and the
  main checkout's uncommitted edit of the same file would make the final fast-forward refuse if the
  merge changed it.
- With the lessons file resolved and `.serena/project.yml` kept at the trunk blob, the merge commit's
  tree must be exactly **`981c808820470f805e9ce6eda5b4f8ab1e7f5867`** (computed in memory from tree
  `6905de40...`; the same method reproduced the existing tree `1b00d1acd:.serena` exactly). The merge
  then differs from `1b00d1acd` in 26 paths: 16 troop files, 3 equipment and companion files,
  `recruitment_pools/gondor.json`, `VolunteerRecruitmentService.Gondor.cs` and its test,
  `CHANGELOG.md`, two lessons files and the new `docs/reviews/rca-public-armory-ids-2026-09-30.md`.
  None is under `tools/`, `.claude/` or `Dependencies/`, and none is `Main/IoC.cs`,
  `Main/SubModule.cs` or `Main/TAOM.csproj`.
- Data effect, computed from `git show` of every troop file: distinct `Item.` ids are 2,562 at
  `1b00d1acd` and 2,617 at `8df90444c`; the merged set equals the public set exactly. 156 ids exist
  only in the trunk tree (76 are `ladder_*`), 211 only in the public tree. NPCCharacter ids in
  `troops/*.xml` are the same 857 before and after the merge (no troop added or removed).
- `Main/_Module/SubModule.xml:6` reads `<Version value="v2.0.28" />` on both sides and in the merge;
  `Dependencies/_Module/SubModule.xml` reads `v2.0.6` everywhere. No version moves.

### Files this plan edits after the merge, as they are on origin/bannerlord-1.4.5

- `CHANGELOG.md`: starts with a doubled BOM (`ef bb bf ef bb bf`), then the header and an archive
  note; first section `## 2026-09-15` at line 5. After the merge, line 5 is the public branch's
  `## 2026-09-30` and line 7 is `### fix(data): the 1.4.8 public release takes the 1.5.x troop tree
  for the new Armory (#697)`. Sections are `## <date>` with `### <type>(<scope>): <title>`
  subsections and prose wrapped near 100 characters.
- `CLAUDE.md:189` (the file starts with a BOM; 46,704 bytes on disk with CRLF, already over the
  46,000-byte lint cap that WP103 trims). The current row, for reference only:

```
| **Two machines; the laptop is incomplete** | Every `E:\` path in these docs is the DESKTOP. A partial `TAOM_Map`/`LOTRLOME_Armory` fails every reference into it: 6,894 broken items, 414 landless cultures (2026-09-06), none a repo defect. Never "fix" the repo to quiet one. [machines](docs/reference/development-machines.md) |
```

- `docs/reference/development-machines.md:15`: `| Content modules | complete | incomplete, see the
  warning below |`; `:52-54`: "Measured on the laptop on 2026-09-06: 6,894 `BROKEN_ITEM_REF`, 414
  `LANDLESS_CULTURE` and 90 `SETTLEMENT_ECONOMY_FLOOR`, with `TAOM_Map\ModuleData` holding no
  `settlements.xml` at all. None of / it was a repo defect."
- `plans/README.md`: on origin it is the 2026-06-12 `/improve` index (plans 001 to 005). The
  orchestrator rewrites it in the main checkout as the programme index before this plan runs.

### The laptop's dependency modules (read this session)

- Game: Bannerlord v1.4.8 at `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II
  Bannerlord` (`BANNERLORD_GAME_DIR` is set; `bin/Win64_Shipping_Client/Version.xml` reads
  `Singleplayer Value="v1.4.8"`). Never write there from a session.
- `Modules/LOTRLOME_Armory`: cooked (`AssetPackages`, no `Assets/`), `SubModule.xml` reads
  `<Version value="v2.0.26" />`, dated 2026-09-07. `ModuleData/Monsters/LOTR/` has no
  `lotr_monster_elk.xml` or `lotr_monster_animalia.xml`. It defines 0 `ladder_` ids and 79 distinct
  `starter_` ids.
- The public channel's Armory reads `v2.0.29.5` (E3 line 40: `LOTRLOME_Armory vv2.0.29.5`) and loads
  `Monsters/LOTR/lotr_monster_elk.xml` and `lotr_monster_animalia.xml` (E2 rgl lines 4027 and 4033).
  The channel's numbers were typed into the channel folders by hand (RELEASE-INTEGRITY-03), so the
  version string is a weak marker; the monster files and the ladder ids are the strong ones.
- `Modules/TAOM_Map`: cooked, `v2.0.27`, dated 2026-09-07, complete in file set (XML-DATA-02).
- Logs: taom_debug at `<game>\bin\Win64_Shipping_Client\Logs\taom_debug_*.log` (`FileLogger.cs:26,40`
  write to the relative `Logs` folder); rgl logs at `C:\ProgramData\Mount and Blade II Bannerlord\logs`.

### Engine facts (verified with taom-src on the installed v1.4.8 DLLs)

- Why a missing item is silent: `TaleWorlds.Core.Equipment.DeserializeNode` (Equipment.cs:204-223)
  calls `MBObjectManager.Instance.GetObject<ItemObject>(objectName)` and sets the slot when
  `IsItemFitsToSlot(slot, item)` is true; `IsItemFitsToSlot` (:445-450) returns true for a null item,
  so the slot becomes an empty `EquipmentElement`. `MBObjectManager.GetObject<T>(string)` (:573-596)
  ends in `ObjectTypeRecord.GetObject`, which is `_registeredObjects.TryGetValue(objId, out var value);
  return value;` (:172-176), so an unknown id is null, not an exception. An item that does not fit the
  slot (a body armour in `Cape`) is skipped with a `Debug.FailedAssert`, which is what 1b00d1acd's
  skirt move fixed.
- BHA0001 is a false positive (D4): `TaleWorlds.CampaignSystem.Party.PartyComponents.LordPartyComponent`
  declares nested `public class InitializationArgs` (LordPartyComponent.cs:14) with
  `public void InitializeLordPartyProperties(MobileParty mobileParty, Hero owner)` (:29), which is what
  `Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs:22` binds. The
  analyzer cannot resolve nested types. WP104 silences it; this plan only records it.

### Harness facts that change how you work on 1.4.x

- On 1.4.x `Main/TAOM.csproj:133-136` states that `DisableModuleCopy` does not gate `CopyModule`.
  Every build and test passes BOTH `-p:DisableModuleCopy=true -p:ModuleId=`; without `-p:ModuleId=`
  the build deploys into the game install.
- Do not run `/verify`, `/ship` or `./build.ps1`: `.claude/skills/verify/SKILL.md:19` runs
  `dotnet build Main --no-restore` and `:27` runs `dotnet test TAOM.Tests --no-build`, both without the
  flags, so they deploy (D2 report section 3.4). WP103 fixes the skill.
- The commit gates are inert on 1.4.x: `check-commit-subject-version.sh:73` prints a top-level
  `permissionDecision`, which Claude Code ignores (D2 report section 2.2 A). Nothing checks your
  subject, so you check it yourself with the message check below. Its pattern is the hook's
  (`:158`): `^[a-z][a-z0-9]*(?:\([^)]+\))?!?: (v\d+\.\d+\.\d+(?:\.\d+)?) - \S`, version `v2.0.28`.
- Hook registrations are relative paths, so a persistent `cd` inside the main checkout can silence
  the few gates that work. Use absolute paths and `git -C <dir>`; a `cd` into the worktree does not
  persist across calls, which is fine.
- Run git only through the Bash tool, never through PowerShell (D19: no git gate sees PowerShell).
- `GIT_EDITOR=true` in this environment: any git command allowed to create a commit on its own
  (`git merge` without `--no-commit`, `git commit` without `-F`, `--amend --no-edit`) commits git's
  default message unedited. Always `git merge --no-commit`, then `git commit -F <file>`.
- Git Bash path conversion can mangle an argument such as `:.serena/project.yml`. Prefix git commands
  that take `:<path>` or `<ref>:<path>` with `MSYS_NO_PATHCONV=1`, and write paths as `C:/...` (not
  `/c/...`) so they survive with or without conversion.
- The four tests that find the repo root by walking up to `.git` accept a worktree's `.git` file
  (`ExitStallDisarmTests.cs:125-128`, `TaomCulturalFeatsDefinitionTests.cs:370-371`,
  `FieldDutyRuntimeTests.cs:398-400`, `EnlistmentEquipmentCultureTests.cs:71-72`). They failed only in
  the orchestrator's `.git`-less export.
- One test reads the installed Armory: `ShippedCultures_EveryBannerBearerReplacementWeaponIsOneHanded`
  (`TAOM.Tests/Features/BannerBearers/BannerBearerReplacementWeaponDataTests.cs:37`, item XML under
  `Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items` at `:106`). Its result can change with Part A.
  `EveryAllowedRaceIsARealRegisteredRace` (`CultureRaceConsistencyTests.cs:115`) reads a hard-coded
  `E:\` path and is skipped on the laptop.
- The Python tool suite rewrites `tools/settlement_economy_floor.json` during one test and restores
  it in `finally` (`.ai/verification.md`, "Shared-worktree test caution"): run it in the worktree only.

### Conventions that bind this plan

- Commit subjects: `<type>[(scope)]: v2.0.28 - <description>`, at most 72 characters; body wrapped
  at 72; no AI attribution, no `Co-Authored-By`, no "Generated with" line (CLAUDE.md "Commits"; this
  outranks any session attribution reminder). No `Ported-from:` trailer here: no 1.5.x commit is
  included. No em or en dash anywhere in what you write.
- `CHANGELOG.md` is hand-edited on 1.4.x until WP102 retires that (D1); this plan adds the last two
  hand-written entries, insert-only.
- Multi-session git safety (CLAUDE.md): stage explicit paths only, never `git add -A`, `-u` or `.`;
  never stash, `pull --rebase`, `reset`, `clean`, or `checkout --` in the main checkout; never touch
  `.serena/project.yml` or `stash@{0}`; stage an edit to a shared file in the same command that makes
  it.
- This plan changes no C#, so these bind only if you are tempted to edit code (do not): TDD (a
  failing test before any implementation); ADR-007 (services use TAOM adapters, never sealed
  TaleWorlds types); ADR-002 (entry points under 150 lines, delegating to services); ADR-003 (no
  `#region`); ADR-004 (no `[Obsolete]`); ADR-005 (no `#if DEBUG` outside the IoC registration);
  the MCM json2 rule (rename a persisted setting, never flip its default);
  `.claude/rules/harmony-patches.md` (MovementOrder postfixes go through the shared deferred category).
- No `/deep-review`: the merge adds no code beyond the public commits, which carry their own review
  record (`8df90444c`, `docs/reviews/rca-public-armory-ids-2026-09-30.md`).
- Do not use Serena's edit tools (D3); use the scripts and the Edit/Write tools as written here.

## Commands you will need

Every Bash call starts by setting `WT`, `AUX` or `MAIN` itself: shell variables do not persist
between calls. Long runs need a tool timeout of 600000 ms.

```
WT=C:/Users/mikew/source/repos/taom-port/101      # Part B worktree
AUX=C:/Users/mikew/source/repos/taom-port         # logs, message files, hand-off record (outside git)
MAIN=C:/Users/mikew/source/repos/TAOM             # main checkout (Part C only)
```

| Purpose | Command | Expected on success |
|---|---|---|
| Restore | `dotnet restore $WT/TAOM.sln` | exit 0 |
| Build | `dotnet build $WT/TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > $AUX/101-build.log 2>&1` | exit 0, `0 Error(s)`, exactly one distinct BHA line (BHA0001, Patch88) |
| Test | `dotnet test $WT/TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= > $AUX/101-test.log 2>&1` | `Passed!  - Failed:     0, Passed:  9151, Skipped:     3, Total:  9154` |
| Data | `python $WT/tools/validate_moduledata.py > $AUX/<name>.txt 2>&1` | both extra roots swept; summary recorded |
| Polearm gate | `python $WT/tools/audit_polearm_shield_parity.py > $AUX/<name>.txt 2>&1` | exit code and PASS/FAIL lines recorded |
| Hooks | `bash $WT/tools/test_hooks.sh > $AUX/101-hooks.txt 2>&1` | `N passed, M failed` recorded |
| Tool tests | `cd $WT && python -m unittest discover -s tools/tests -t . > $AUX/101-pytests.txt 2>&1` | `Ran N tests` and the result line recorded |
| Docs | `python $WT/tools/lint_docs.py --summary` | the `---` block recorded; no counter rises after your edits |

Run these exactly as written: later plans compare against numbers taken with the same commands.
Redirected Python output uses the cp1252 code page on this laptop. If a Python tool dies with
`UnicodeEncodeError`, rerun that one command with `PYTHONIOENCODING=utf-8 ` in front and say so in
the record.

Message check (run before every commit; set `p` to the message file):

```
python - <<'PY'
import re
p = r"C:/Users/mikew/source/repos/taom-port/101-msg-merge.txt"
t = open(p, encoding="utf-8").read()
subj = t.splitlines()[0]
ok = bool(re.match(r"^[a-z][a-z0-9]*(?:\([^)]+\))?!?: v2\.0\.28 - \S", subj)) and len(subj) <= 72
bad = any(c in t for c in "\u2013\u2014") or "Co-Authored-By" in t or "Generated with" in t
longest = max(len(l) for l in t.splitlines())
print("subject ok" if ok else "SUBJECT BAD", "| dash or attribution:", bad, "| longest line:", longest)
PY
```

Expected: `subject ok | dash or attribution: False | longest line:` followed by a number of at most 72.

## Scope

**In scope**
- Environment (maintainer, Part A): `<game>\Modules\LOTRLOME_Armory` on the laptop.
- Part B, branch `port148/101-baseline-sync` in the worktree: the merge of `8df90444c` (all 26 paths
  as the merge brings them, with `docs/reviews/lessons/data-content-cultures.md` resolved and
  `.serena/project.yml` kept at the trunk blob), then `CHANGELOG.md` (insert only), `CLAUDE.md`
  (line 189 only) and `docs/reference/development-machines.md` (line 15 and one new paragraph after
  line 54).
- Part C, main checkout: the fast-forward of `bannerlord-1.4.5`, then `plans/README.md`,
  `plans/101-*.md` to `plans/145-*.md`, `plans/_audit/2026-11-04-port148/**` and `CHANGELOG.md`
  (insert only).

**Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs` and `Main/TAOM.csproj` are not touched by
this plan, and the merge does not touch them.

**Out of scope** (do not touch)
- Any C# edit, any 1.5.x commit, any `Dependencies/` file.
- `.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props`, `docs/adrs/*`
  (maintainer-only).
- The main checkout's uncommitted `.serena/project.yml` and `stash@{0}`.
- The BHA0001 pragma (WP104), module versions in the validator header (WP136, the second half of
  XML-DATA-02), the `taom_body_length` pairing signal (WP115, D15), the CLAUDE.md trim (WP103).
- Fixing any data defect the measurements reveal: record it and route it (WP134 owns data defects).
- Pushing, tagging, releasing, closing issues.

## Git workflow

- Why a worktree: running `git merge` in the main checkout would refuse ("Your local changes to the
  following files would be overwritten by merge: .serena/project.yml"), because `10a52fb3d` changes
  that file, and stashing the maintainer's edit is forbidden. So Part B builds the merge in a worktree
  created from `1b00d1acd`, and Part C fast-forwards the main checkout's `bannerlord-1.4.5` onto it.
  The end state is exactly the binding note's: `bannerlord-1.4.5` fast-forwarded to
  `origin/bannerlord-1.4.5` and merged with the public branch (the merge's first parent is
  `1b00d1acd`).
- Worktree `C:\Users\mikew\source\repos\taom-port\101`, branch `port148/101-baseline-sync`, created
  from the full SHA `1b00d1acd9f6ceda81709067ca88ee1393115b48` (no upstream is set).
- Commits on the WP branch (Part B): the merge commit, then one docs record commit. Commit on
  `bannerlord-1.4.5` (Part C, integrator only): one programme commit.
- Staging: explicit `git add <paths>` in one Bash call; `git commit -F <file>` in the NEXT
  sequential Bash call, never the same command and never a parallel call. Message files live in
  `C:/Users/mikew/source/repos/taom-port/`, outside every worktree.
- No push, no PR, no tag. Pushing `bannerlord-1.4.5` is the maintainer's decision (Step C5).

## Part A: the maintainer refreshes the laptop Armory (D20)

### Step A1: install the public 2026-09-29 Armory

Maintainer only. Close Bannerlord and the Modding Kit first.

1. Move `<game>\Modules\LOTRLOME_Armory` (v2.0.26) out of `Modules` to a dated backup folder, so it
   can be restored.
2. Copy in the cooked `LOTRLOME_Armory` module that the public 1.4.8 channel ships (the 2026-09-29
   publish named in 92a30ba42's body). The release-integrity lens names the desktop channel folder
   `E:\LOTRAOM_Releases\public\Modules\LOTRLOME_Armory` (UNVERIFIED from the laptop).
3. Optional: refresh `TAOM_Map` from the same public channel package. Never use the desktop's live
   `TAOM_Map` exported after 2026-10-29: it carries 1.5.x culture and kingdom ids that 1.4.x TAOM
   does not define.

**Verify**: Step A2.

### Step A2: confirm the refresh (read-only; the Part B executor repeats it in Step B1)

```
A="$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory"
grep -o '<Version value="[^"]*"' "$A/SubModule.xml"
ls "$A/ModuleData/Monsters/LOTR/lotr_monster_elk.xml" "$A/ModuleData/Monsters/LOTR/lotr_monster_animalia.xml"
grep -rl 'id="ladder_gondor_bow_t4"' "$A/ModuleData" | head -1
grep -rho 'id="ladder_[^"]*"' "$A/ModuleData" | sort -u | wc -l
grep -o '<Version value="[^"]*"' "$BANNERLORD_GAME_DIR/Modules/TAOM_Map/SubModule.xml"
```

Expected: the Armory version is not `v2.0.26` (the channel copy reads `v2.0.29.5`); both monster
files are listed; one path is printed for `ladder_gondor_bow_t4` (the merged troop tree gives it to
`gondor_leb_archer`); the ladder count is above 0 (the merged tree names 123 distinct `ladder_` ids).
The last line prints the TAOM_Map version to record.

## Part B: build and verify the merge in a worktree (executor)

### Step B1: preflight (read-only)

**B1.1** Run the Step A2 block. Any expectation unmet: STOP, report "BLOCKED on Part A (D20)".

**B1.2** Pins, branch and path:

```
MAIN=C:/Users/mikew/source/repos/TAOM
git -C $MAIN fetch origin bannerlord-1.4.5 fix/public-armory-ids-145; echo "fetch exit=$?"
git -C $MAIN rev-parse origin/bannerlord-1.4.5 origin/fix/public-armory-ids-145 bannerlord-1.4.5
git -C $MAIN rev-parse --verify --quiet refs/heads/port148/101-baseline-sync; echo "branch exit=$?"
ls -d C:/Users/mikew/source/repos/taom-port/101 2>/dev/null && echo PATH-EXISTS
```

Expected: `fetch exit=0` (a non-zero exit is an environment failure: report it and continue on the
pinned SHAs); then `1b00d1acd9f6ceda81709067ca88ee1393115b48`,
`8df90444cc2f1e6d83bb95d2164ab764b4fcae65`, and either `c5b84fb415259ba2ab08401694195f8a88891f1d`
or `1b00d1acd9f6ceda81709067ca88ee1393115b48`; `branch exit=1`; no `PATH-EXISTS`.

**B1.3** Re-run the simulation:

```
git -C C:/Users/mikew/source/repos/TAOM merge-tree --write-tree --name-only --no-messages 1b00d1acd9f6ceda81709067ca88ee1393115b48 8df90444cc2f1e6d83bb95d2164ab764b4fcae65; echo "exit=$?"
```

Expected, exactly: `6905de4024b10a94ec02641b34342700df703fdc`, then
`docs/reviews/lessons/data-content-cultures.md`, then `exit=1`.

### Step B2: create the worktree

```
mkdir -p C:/Users/mikew/source/repos/taom-port && git -C C:/Users/mikew/source/repos/TAOM worktree add -b port148/101-baseline-sync C:/Users/mikew/source/repos/taom-port/101 1b00d1acd9f6ceda81709067ca88ee1393115b48
```

**Verify**:

```
WT=C:/Users/mikew/source/repos/taom-port/101; git -C $WT rev-parse --abbrev-ref HEAD; git -C $WT rev-parse HEAD; git -C $WT status --porcelain
```

prints `port148/101-baseline-sync`, `1b00d1acd9f6ceda81709067ca88ee1393115b48`, and nothing else.

### Step B3: measure the data at 1b00d1acd, before the merge

This is the size of the regression the merge avoids (RELEASE-INTEGRITY-04 "Verify how", run on the
laptop because it now has the public Armory).

```
WT=C:/Users/mikew/source/repos/taom-port/101; AUX=C:/Users/mikew/source/repos/taom-port
python $WT/tools/validate_moduledata.py > $AUX/101-validate-origin.txt 2>&1; echo "exit=$?"
grep -E 'Also sweeping refs in|NOT FOUND|SKIPPED' $AUX/101-validate-origin.txt
sed -n '/=== SUMMARY ===/,$p' $AUX/101-validate-origin.txt
python $WT/tools/audit_polearm_shield_parity.py > $AUX/101-polearm-origin.txt 2>&1; echo "exit=$?"
grep -E '^(Modules|Rosters):|^(PASS|FAIL|KNOWN)' $AUX/101-polearm-origin.txt
```

**Verify**: two `Also sweeping refs in:` lines, one ending in `LOTRLOME_Armory\ModuleData` and one in
`TAOM_Map\ModuleData`, and no `NOT FOUND` line for either; the summary prints `N error(s), M
warning(s)` and per-code counts. Record `N_ORIGIN` = the `BROKEN_ITEM_REF` count (expect several
hundred; 92a30ba42 measured 589 refs for the same troop id set). The polearm `Rosters:` line names the
worktree's `Main\_Module\ModuleData`; record its exit code and PASS/FAIL lines.

### Step B4: merge and resolve

**B4.1** Merge without committing:

```
WT=C:/Users/mikew/source/repos/taom-port/101
git -C $WT merge --no-ff --no-commit 8df90444cc2f1e6d83bb95d2164ab764b4fcae65; echo "merge exit=$?"
git -C $WT diff --name-only --diff-filter=U
```

Expected: `Auto-merging Main/_Module/ModuleData/troops/troops_isengard.xml`, `CONFLICT (content):
Merge conflict in docs/reviews/lessons/data-content-cultures.md`, `merge exit=1`, and the last
command prints only `docs/reviews/lessons/data-content-cultures.md`.

**B4.2** Resolve the lessons file: keep both sides, trunk first. The script works for merge-style and
diff3-style markers and for CRLF or LF.

```
python - <<'PY'
p = r"C:/Users/mikew/source/repos/taom-port/101/docs/reviews/lessons/data-content-cultures.md"
data = open(p, "rb").read()
lines = data.split(b"\n")   # LF only: the file holds a vertical tab that splitlines() would break on
if lines and lines[-1] == b"":
    lines.pop()
out, state = [], "keep"
for line in lines:
    s = line[:-1] if line.endswith(b"\r") else line
    if s.startswith(b"<<<<<<< "):
        state = "ours"; continue
    if s.startswith(b"||||||| ") and state == "ours":
        state = "base"; continue
    if s == b"=======" and state in ("ours", "base"):
        state = "theirs"; out.append(b""); continue
    if s.startswith(b">>>>>>> ") and state == "theirs":
        state = "keep"; continue
    if state == "base":
        continue
    out.append(s)
assert state == "keep", "unbalanced conflict markers"
open(p, "wb").write(b"\n".join(out) + b"\n")
print("resolved", len(out), "lines")
PY
```

Expected: `resolved 1434 lines`.

**B4.3** Stage it and check the blob:

```
WT=C:/Users/mikew/source/repos/taom-port/101; git -C $WT add docs/reviews/lessons/data-content-cultures.md && MSYS_NO_PATHCONV=1 git -C $WT rev-parse :docs/reviews/lessons/data-content-cultures.md
```

Expected: `3a3298e6967b6324d5d0ed7ea46193a75c37b00d`.

**B4.4** Keep the trunk's `.serena/project.yml`:

```
WT=C:/Users/mikew/source/repos/taom-port/101; git -C $WT checkout HEAD .serena/project.yml && MSYS_NO_PATHCONV=1 git -C $WT rev-parse :.serena/project.yml
```

Expected: `b56198b5ae308c7e28f6729d022d5b48768cdde0`.

**B4.5** Check the auto-merged Isengard file, the version and the whole tree:

```
WT=C:/Users/mikew/source/repos/taom-port/101
MSYS_NO_PATHCONV=1 git -C $WT rev-parse :Main/_Module/ModuleData/troops/troops_isengard.xml
grep -n -B1 'sk_uruk_hai_skirt_a1' $WT/Main/_Module/ModuleData/troops/troops_isengard.xml | grep 'slot='
grep -o '<Version value="[^"]*"' $WT/Main/_Module/SubModule.xml
git -C $WT diff --cached --name-only | wc -l
git -C $WT write-tree
```

Expected: `15df2d81063c842ebbc0c297146d9af2c2845cc5`; two lines, `1341-` and `1608-`, each followed
by spaces and `slot="Body"`; `<Version value="v2.0.28"`; `26`;
`981c808820470f805e9ce6eda5b4f8ab1e7f5867`.

### Step B5: commit the merge

**B5.1** Write `C:/Users/mikew/source/repos/taom-port/101-msg-merge.txt` with the Write tool, exactly:

```
chore: v2.0.28 - merge the public 1.4.8 troop tree (#697) into trunk

Players run the public 1.4.8 build, cut from fix/public-armory-ids-145
(its TAOM.dll stamp names 8df90444c). The trunk never received those
three commits: its 16 troop files still named 156 item ids the public
troop tree dropped, 76 of them retired ladder bows, and against the
public 2026-09-29 Armory that tree had 589 broken item refs
(92a30ba42). v1.4.8 loads an unknown item id as an empty slot, so a
release or a port cut from the trunk would have shipped archers
without bows. The public branch in turn lacked the trunk's five newer
commits, 1b00d1acd's Uruk fixes among them.

The merge keeps both. troops_isengard.xml auto-merges and keeps
1b00d1acd's skirt move to the Body slot for the Uruk-Hai Champion and
Berserker. docs/reviews/lessons/data-content-cultures.md keeps both
appended lessons, the trunk's two Custom Battle entries first.
10a52fb3d's .serena/project.yml churn, a regenerated personal Serena
config, is left out.

Research: taom-src v1.4.8 Equipment.DeserializeNode and
  IsItemFitsToSlot (Equipment.cs:204-223, 445-450); MBObjectManager
  GetObject returns null for an unknown id (MBObjectManager.cs:172-176)
Not-tested: in-game smoke and an old save near Glanhir (owed by #697)
```

**B5.2** Run the message check on it (expected `subject ok`).

**B5.3** In the next Bash call:

```
git -C C:/Users/mikew/source/repos/taom-port/101 commit -F C:/Users/mikew/source/repos/taom-port/101-msg-merge.txt
```

**Verify**:

```
WT=C:/Users/mikew/source/repos/taom-port/101; git -C $WT rev-parse HEAD^{tree} HEAD^1 HEAD^2; git -C $WT log -1 --format=%s; git -C $WT status --porcelain
```

prints `981c808820470f805e9ce6eda5b4f8ab1e7f5867`, `1b00d1acd9f6ceda81709067ca88ee1393115b48`,
`8df90444cc2f1e6d83bb95d2164ab764b4fcae65`, the subject above, and no status lines. Record the merge
commit's SHA as `M` (`git -C $WT rev-parse HEAD`).

### Step B6: build and test the merged tree

```
WT=C:/Users/mikew/source/repos/taom-port/101; AUX=C:/Users/mikew/source/repos/taom-port
dotnet restore $WT/TAOM.sln; echo "restore exit=$?"
dotnet build $WT/TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > $AUX/101-build.log 2>&1; echo "build exit=$?"
tail -4 $AUX/101-build.log
grep 'warning BHA' $AUX/101-build.log | sed 's/ \[[^]]*\]$//' | sort -u
```

Then, in the next call:

```
WT=C:/Users/mikew/source/repos/taom-port/101; AUX=C:/Users/mikew/source/repos/taom-port
dotnet test $WT/TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= > $AUX/101-test.log 2>&1; echo "test exit=$?"
grep -E '^(Passed|Failed)!' $AUX/101-test.log
grep -E '^\s+(Skipped|Failed) ' $AUX/101-test.log
```

**Verify**: `restore exit=0`; `build exit=0` and `0 Error(s)` (the orchestrator's fresh build of
this tree printed `2251 Warning(s)`; record your count, it is not a gate); exactly one distinct BHA
line, naming `Patch88_InitializeLordPartyPropertiesScope.cs(22,2)` and `BHA0001`. `test exit=0`;
`Passed!  - Failed:     0, Passed:  9151, Skipped:     3, Total:  9154`; the three skipped are
`WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack` and
`EveryAllowedRaceIsARealRegisteredRace`; no `Failed` line.

### Step B7: measure data, harness and docs on the merged tree

Run each block in its own Bash call and record what it prints.

```
WT=C:/Users/mikew/source/repos/taom-port/101; AUX=C:/Users/mikew/source/repos/taom-port
python $WT/tools/validate_moduledata.py > $AUX/101-validate-merged.txt 2>&1; echo "exit=$?"
grep -E 'Also sweeping refs in|NOT FOUND|SKIPPED' $AUX/101-validate-merged.txt
sed -n '/=== SUMMARY ===/,$p' $AUX/101-validate-merged.txt
python $WT/tools/audit_polearm_shield_parity.py > $AUX/101-polearm-merged.txt 2>&1; echo "exit=$?"
grep -E '^(PASS|FAIL|KNOWN)' $AUX/101-polearm-merged.txt
```

The ModuleData commit gate's own command, with the exact `--code` list of
`.claude/hooks/check-moduledata-validation.sh:97-105`, from the worktree root:

```
WT=C:/Users/mikew/source/repos/taom-port/101; AUX=C:/Users/mikew/source/repos/taom-port
cd $WT && timeout -k 2 45 python tools/validate_moduledata.py --code BROKEN_ITEM_REF --code BROKEN_TROOP_REF --code UNKNOWN_CULTURE --code DUPLICATE_NPC_ID --code DUPLICATE_CULTURE_ID --code DUPLICATE_ROSTER_ID --code BROKEN_BODY_PROPERTY_REF --code LANDLESS_CULTURE --code MOUNTED_DWARF --code SETTLEMENT_ECONOMY_FLOOR --code UPGRADE_SKILL_REGRESSION --code SKILL_TEMPLATE_SHADOWS_SKILLS --code UPGRADE_TIER_COLLAPSE --code UPGRADE_INDEX_EMPTY --code MISSING_BODY_ARMOUR --code MISSING_EDUCATION_TEMPLATES --code MISSING_HARNESS_FAMILY_TYPE --code HARNESS_FAMILY_MISMATCH --code MOUNT_WITHOUT_HARNESS > $AUX/101-gate-merged.txt 2>&1; echo "gate exit=$?"
sed -n '/=== SUMMARY ===/,$p' $AUX/101-gate-merged.txt
```

```
WT=C:/Users/mikew/source/repos/taom-port/101; AUX=C:/Users/mikew/source/repos/taom-port
bash $WT/tools/test_hooks.sh > $AUX/101-hooks.txt 2>&1; echo "exit=$?"; tail -15 $AUX/101-hooks.txt
```

```
WT=C:/Users/mikew/source/repos/taom-port/101; AUX=C:/Users/mikew/source/repos/taom-port
cd $WT && python -m unittest discover -s tools/tests -t . > $AUX/101-pytests.txt 2>&1; echo "exit=$?"; tail -6 $AUX/101-pytests.txt
```

```
WT=C:/Users/mikew/source/repos/taom-port/101; AUX=C:/Users/mikew/source/repos/taom-port
python $WT/tools/lint_docs.py --summary > $AUX/101-lint-merged.txt 2>&1; echo "exit=$?"; cat $AUX/101-lint-merged.txt
git -C $WT status --porcelain
```

**Verify**:
- Validator: both extra roots swept as in Step B3. Record `E` (errors), `W` (warnings), every code
  count, `N_MERGED` (`BROKEN_ITEM_REF`) and `L` (`LANDLESS_CULTURE`, 0 if absent). `N_MERGED` must
  not exceed `N_ORIGIN`; expect it near 0 (92a30ba42 measured 0 against the release modules). List
  every code whose count rose from Step B3 to here as "public-build data findings" for the
  maintainer; do not fix them.
- ModuleData gate: `gate exit=0`. Record the exit as `GATE_EXIT`. Any other exit is a STOP routed to
  the maintainer: report the exit code and the per-code counts from `101-gate-merged.txt`, and name the
  consequence: once WP103 makes the hook's output real, every later commit staging
  `Main/_Module/ModuleData/*.xml` on this laptop is denied until the count is 0, whatever
  `N_MERGED` compared with `N_ORIGIN` says. Do not fix the findings and do not go on to Step B8.
- Polearm gate: record exit code and lines. A `FAIL: N roster(s) pair a shield...` present here and
  absent in Step B3 is a public-build data finding: record it with the roster names, do not fix it.
- Hooks: record `N passed, M failed` and the failure names. The merge changes nothing under
  `.claude/` or `tools/`, so these are the trunk's own baseline.
- Tool tests: record `Ran N tests` and the last line (`OK`, `OK (skipped=..)` or
  `FAILED (failures=.., errors=.., skipped=..)`) with failing test names.
- Docs: record the counters. `claude_budget` is expected to be at least 1 (CLAUDE.md is over its cap).
- Status: no output. If a tracked file under `tools/` shows as modified (a test fixture that did not
  restore), record its path and restore that one path with
  `git -C $WT checkout HEAD <that exact path>`. New untracked files: leave them, list them.

### Step B8: record the laptop baseline in the docs (commit 2)

**B8.1** Record CLAUDE.md's size first (expect `46704`):

```
wc -c < C:/Users/mikew/source/repos/taom-port/101/CLAUDE.md
```

**B8.2** Write `C:/Users/mikew/source/repos/taom-port/101-machines-paragraph.md` with the Write tool,
replacing every `{...}` with the value you recorded (`{DATE}` is the output of `date +%F`). Keep the
six lines as they are; do not rewrap.

```
Re-measured on the laptop on {DATE}, after the v2.0.26 `LOTRLOME_Armory` (2026-09-07) was replaced
by the public 2026-09-29 build (`{ARMORY}`), with `TAOM_Map` at `{MAP}`: on the merged
`bannerlord-1.4.5` tree `validate_moduledata.py` reported {E} errors, {N_MERGED} of them
`BROKEN_ITEM_REF`, {W} warnings and {L} `LANDLESS_CULTURE`. That run is the laptop baseline
(decision D20 of the 2026-11-04 port programme; per-code counts in `plans/README.md`). A laptop
result above it deserves a look before it is blamed on the machine.
```

**B8.3** Write `C:/Users/mikew/source/repos/taom-port/101-changelog-record.md` the same way:

```
## {DATE}

### chore(sync): the trunk takes the public 1.4.8 troop tree, and the laptop gets a data baseline

`bannerlord-1.4.5` now holds the three commits of `fix/public-armory-ids-145` (#697), the branch the
public 1.4.8 build was cut from, merged onto `origin/bannerlord-1.4.5` at 1b00d1acd. Before the merge
the trunk's troop files named 156 item ids the public troop tree had dropped, 76 of them retired
ladder bows, so a release cut from the trunk would have shipped archers without bows. The merge keeps
1b00d1acd's Uruk-Hai Champion and Berserker skirt fix and leaves out the `.serena/project.yml` churn of
10a52fb3d.

The laptop's `LOTRLOME_Armory` is now the public 2026-09-29 build (`{ARMORY}`) instead of a v2.0.26
copy. `validate_moduledata.py` there reports {N_MERGED} `BROKEN_ITEM_REF` on the merged trunk and
{N_ORIGIN} on 1b00d1acd. Build and tests on the merged trunk: {T} tests, {P} passed, {S} skipped, 0
failed. `docs/reference/development-machines.md` and the CLAUDE.md "Two machines" row describe the
refreshed laptop.
```

Check both files (prints nothing when every placeholder is filled):

```
grep -n '{[A-Z_]*}' C:/Users/mikew/source/repos/taom-port/101-machines-paragraph.md C:/Users/mikew/source/repos/taom-port/101-changelog-record.md
```

**B8.4** Apply the three edits:

```
python - <<'PY'
import pathlib, re
WT = pathlib.Path(r"C:/Users/mikew/source/repos/taom-port/101")
AUX = pathlib.Path(r"C:/Users/mikew/source/repos/taom-port")
def nl_of(b):
    return b"\r\n" if b"\r\n" in b else b"\n"
def block(name, nl):
    t = (AUX / name).read_bytes().decode("utf-8").replace("\r\n", "\n").rstrip("\n")
    return t.replace("\n", nl.decode()).encode("utf-8")
# 1. CLAUDE.md trap row (line 189): same line ending, one byte shorter
p = WT / "CLAUDE.md"; b = p.read_bytes()
NEW_ROW = r'| **Two machines; laptop data has a baseline** | Every `E:\` path in these docs is the DESKTOP. A partial or stale `TAOM_Map`/`LOTRLOME_Armory` fails every reference into it (2026-09-06: 6,894 broken items, none real). Never "fix" the repo to quiet one: see the baseline in [machines](docs/reference/development-machines.md) |'.encode("utf-8")
pat = re.compile(rb"^\| \*\*Two machines; the laptop is incomplete\*\* \|[^\r\n]*", re.M)
assert len(pat.findall(b)) == 1, "trap row not found exactly once"
p.write_bytes(pat.sub(lambda m: NEW_ROW, b, count=1))
# 2. development-machines.md: the laptop cell (line 15) and the dated paragraph after line 54
p = WT / "docs/reference/development-machines.md"; b = p.read_bytes(); nl = nl_of(b)
old_cell = b"| Content modules | complete | incomplete, see the warning below |"
new_cell = b"| Content modules | complete | cooked installs, no loose `Assets/`; see the baseline below |"
assert b.count(old_cell) == 1, "table row not found"
b = b.replace(old_cell, new_cell, 1)
anchor = b"it was a repo defect." + nl
assert b.count(anchor) == 1, "paragraph anchor not found"
b = b.replace(anchor, anchor + nl + block("101-machines-paragraph.md", nl) + nl, 1)
p.write_bytes(b)
# 3. CHANGELOG.md: the new dated section directly above "## 2026-09-30"
p = WT / "CHANGELOG.md"; b = p.read_bytes(); nl = nl_of(b)
anchor = b"## 2026-09-30" + nl
assert b.count(anchor) == 1, "changelog anchor not found"
p.write_bytes(b.replace(anchor, block("101-changelog-record.md", nl) + nl + nl + anchor, 1))
print("ok")
PY
```

Expected: `ok`.

**B8.5** Verify the edits:

```
WT=C:/Users/mikew/source/repos/taom-port/101
git -C $WT diff --numstat
head -c 6 $WT/CHANGELOG.md | od -An -tx1; head -c 3 $WT/CLAUDE.md | od -An -tx1; wc -c < $WT/CLAUDE.md
git -C $WT diff -U0 | python -c "import sys; d=sys.stdin.buffer.read().decode('utf-8','replace'); print(sum(1 for l in d.split('\n') if l.startswith('+') and not l.startswith('+++') and ('\u2013' in l or '\u2014' in l)))"
python $WT/tools/lint_docs.py --summary
```

Expected: exactly three numstat rows, `1 1 CLAUDE.md`, `8 1 docs/reference/development-machines.md`
(one changed table line, one blank line, six paragraph lines) and `<k> 0 CHANGELOG.md`;
` ef bb bf ef bb bf`; ` ef bb bf`; the B8.1 size minus 1 (`46703`); `0`; and no lint counter higher
than in Step B7 (`ai_dashes` stays 0).

**B8.6** Stage:

```
git -C C:/Users/mikew/source/repos/taom-port/101 add CHANGELOG.md CLAUDE.md docs/reference/development-machines.md
```

**B8.7** Write `C:/Users/mikew/source/repos/taom-port/101-msg-record.txt`, filling `{ARMORY}`,
`{N_MERGED}` and `{N_ORIGIN}`, and run the message check on it:

```
docs(machines): v2.0.28 - laptop data baseline on the public Armory

The laptop's LOTRLOME_Armory was a v2.0.26 copy from 2026-09-07 with
no ladder bows, so every item verdict made there described an Armory
no player runs. It is now the public 2026-09-29 build ({ARMORY}),
the one the public 1.4.8 channel ships. On it validate_moduledata.py
reports {N_MERGED} BROKEN_ITEM_REF on the merged trunk, against
{N_ORIGIN} on 1b00d1acd before the merge: the size of the regression
the merge avoided.

development-machines.md records the re-measured laptop state, and the
CLAUDE.md "Two machines" row now points readers at that baseline
instead of calling the laptop incomplete. The full baseline table
(build, tests, data, hook and tool tests) goes into plans/README.md
with the programme record.

Not-tested: no game run; this records data and test results only.
```

**B8.8** In the next Bash call:

```
git -C C:/Users/mikew/source/repos/taom-port/101 commit -F C:/Users/mikew/source/repos/taom-port/101-msg-record.txt
```

**Verify**:

```
WT=C:/Users/mikew/source/repos/taom-port/101; git -C $WT diff --name-only HEAD~1 HEAD; git -C $WT rev-parse HEAD~1^{tree}; git -C $WT status --porcelain
```

prints exactly `CHANGELOG.md`, `CLAUDE.md`, `docs/reference/development-machines.md`, then
`981c808820470f805e9ce6eda5b4f8ab1e7f5867`, then nothing. Record this commit's SHA as `R`.

### Step B9: hand the baseline to the integrator

Write `C:/Users/mikew/source/repos/taom-port/101-baseline-record.md` (outside git) with every value
filled in:

```
### Baseline after WP101 (measured {DATE} on the laptop)

Trunk: `bannerlord-1.4.5` = {R} (merge {M}, tree 981c808820470f805e9ce6eda5b4f8ab1e7f5867, parents
1b00d1acd and 8df90444c). Installed: Bannerlord v1.4.8; LOTRLOME_Armory {ARMORY} (public 2026-09-29
build); TAOM_Map {MAP}. Later plans quote these numbers plus the tests they add.

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors, {BUILD_WARNINGS} warnings; one distinct BHA0001 (Patch88, false positive until WP104) |
| Tests | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | {T} total, {P} passed, {S} skipped ({SKIPPED_NAMES}), 0 failed |
| ModuleData | `python tools/validate_moduledata.py` | {E} errors, {W} warnings: {PER_CODE_COUNTS}; BROKEN_ITEM_REF {N_MERGED} (1b00d1acd before the merge: {N_ORIGIN}) |
| ModuleData commit gate | the 19 `--code` command of Step B7 | exit {GATE_EXIT} |
| Polearm and shield | `python tools/audit_polearm_shield_parity.py` | exit {POLE_EXIT}: {POLE_LINES} (1b00d1acd: exit {POLE_EXIT_ORIGIN}) |
| Hooks | `bash tools/test_hooks.sh` | {H_PASS} passed, {H_FAIL} failed ({H_FAIL_NAMES}) |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | Ran {PY_RAN}: {PY_RESULT} ({PY_FAIL_NAMES}) |
| Docs | `python tools/lint_docs.py --summary` | total_findings {LINT_TOTAL} ({LINT_NONZERO}) |

Public-build data findings for the maintainer (codes that rose from 1b00d1acd to the merge, new
polearm failures): {FINDINGS_OR_NONE}.
```

**Verify**: this prints nothing:

```
grep -n '{[A-Z_]*}' C:/Users/mikew/source/repos/taom-port/101-baseline-record.md
```

Your final report quotes this file in full.

## Part C: move the trunk and commit the programme (integrator, main checkout)

### Step C1: preconditions (read-only)

```
MAIN=C:/Users/mikew/source/repos/TAOM; AUX=C:/Users/mikew/source/repos/taom-port
git -C $MAIN rev-parse --abbrev-ref HEAD
git -C $MAIN merge-base --is-ancestor bannerlord-1.4.5 port148/101-baseline-sync && echo ff-possible
git -C $MAIN status --porcelain --untracked-files=all | cut -c4- | sort > $AUX/101-main-dirty.txt
git -C $MAIN diff --name-only bannerlord-1.4.5 port148/101-baseline-sync | sort > $AUX/101-incoming.txt
comm -12 $AUX/101-main-dirty.txt $AUX/101-incoming.txt
git -C $MAIN stash list > $AUX/101-stash-before.txt; cat $AUX/101-stash-before.txt
grep -c '101-baseline-sync' $MAIN/plans/README.md
ls $MAIN/plans/1[0-4][0-9]-*.md | wc -l
```

Expected: `bannerlord-1.4.5`; `ff-possible`; `comm` prints nothing; the stash list shows the single
`discarded pre-pull` entry (record whatever it shows); the README count is at least 1; the plan-file
count (45 when every plan was written; record it, and list the missing numbers if it is lower). If a
number is missing, replace "101 to 145" in the C3.2 and C3.4 texts with the true list (for example
"101 to 145 except 136"), and report the missing plan in the hand-off.

### Step C2: fast-forward bannerlord-1.4.5

```
git -C C:/Users/mikew/source/repos/TAOM merge --ff-only port148/101-baseline-sync
```

**Verify**:

```
MAIN=C:/Users/mikew/source/repos/TAOM; AUX=C:/Users/mikew/source/repos/taom-port
git -C $MAIN rev-parse bannerlord-1.4.5 port148/101-baseline-sync
git -C $MAIN status --porcelain -- .serena/project.yml
git -C $MAIN stash list | diff - $AUX/101-stash-before.txt && echo stash-unchanged
```

Expected: the same SHA twice (`R`); ` M .serena/project.yml`; `stash-unchanged`. No rebuild is
needed: the tree is `R`'s, which differs from the built and tested merge only in the three docs files
(Step B8 verify).

### Step C3: record the baseline and commit the programme (commit 3)

**C3.1** Edit `C:\Users\mikew\source\repos\TAOM\plans\README.md` with the Edit tool:
- In the execution table, set the last cell (Status) of the row that links `101-baseline-sync.md`
  to `DONE (<today's date>)`.
- Find the heading that names the integrator (it contains "Integrator" or "Integration", for
  example `## Integrator record`). Put the whole content of
  `C:/Users/mikew/source/repos/taom-port/101-baseline-record.md` at the end of that section, before
  the next heading of the same or higher level. If the section already holds a "Baseline after
  WP101" block (for example the orchestrator's pre-measured 9154/9151/3 figures), replace that block
  instead of adding a second one. If no such heading exists, append `## Integrator record` at the
  end of the file and put the block under it.

**C3.2** Write `C:/Users/mikew/source/repos/taom-port/101-changelog-programme.md` with the Write tool:

```
### docs(plans): the 1.4.8 port programme is in git

Plans 101 to 145 of the 2026-11-04 port programme, their index in `plans/README.md` and the planning
record under `plans/_audit/2026-11-04-port148/` (decisions D1 to D24 in
`data/brief-workflow3-decisions.md`). The index's integrator section holds the WP101 baseline that
every later plan compares against.
```

**C3.3** Insert it and stage everything in one Bash call (a shared-file edit is staged in the command
that makes it):

```
cd C:/Users/mikew/source/repos/TAOM && python - <<'PY'
import pathlib, re, datetime
p = pathlib.Path(r"C:/Users/mikew/source/repos/TAOM/CHANGELOG.md")
aux = pathlib.Path(r"C:/Users/mikew/source/repos/taom-port/101-changelog-programme.md")
today = datetime.date.today().isoformat().encode()
b = p.read_bytes(); nl = b"\r\n" if b"\r\n" in b else b"\n"
sub = aux.read_bytes().decode("utf-8").replace("\r\n", "\n").rstrip("\n").replace("\n", nl.decode()).encode("utf-8")
m = re.search(rb"^## (\d{4}-\d{2}-\d{2})\r?\n", b, re.M)
assert m, "no dated section"
if m.group(1) == today:
    j = m.end()
    if b[j:j + len(nl)] == nl:
        j += len(nl)
    b = b[:j] + sub + nl + nl + b[j:]
else:
    b = b[:m.start()] + b"## " + today + nl + nl + sub + nl + nl + b[m.start():]
p.write_bytes(b)
print("ok")
PY
git add CHANGELOG.md plans/README.md plans/1[0-4][0-9]-*.md plans/_audit/2026-11-04-port148
git diff --cached --name-only | grep -v -e '^plans/' -e '^CHANGELOG.md$'
git diff --cached --name-only | grep -c '^plans/1[0-4][0-9]-'
git diff --cached --numstat -- CHANGELOG.md
head -c 6 CHANGELOG.md | od -An -tx1
git diff --cached --name-only -z -- plans | xargs -0 grep -IlE 'sk-ant-[A-Za-z0-9_-]{10,}|ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|-----BEGIN [A-Z ]*PRIVATE KEY-----'; echo "secret scan exit=$?"
```

Expected: `ok`; the first `grep -v` prints nothing; the count equals the Step C1 plan-file count;
CHANGELOG numstat `<k> 0`; ` ef bb bf ef bb bf`; no file names, then `secret scan exit=123`
(xargs reports grep's "no match"; a scan of the audit folder on 2026-11-04 found none). A file name
printed there is a STOP: unstage nothing, report the path and the credential type, never the value.

**C3.4** Write `C:/Users/mikew/source/repos/taom-port/101-msg-programme.txt`, filling the values from
the record, and run the message check on it:

```
docs(plans): v2.0.28 - commit the 1.4.8 port plans and their baseline

Adds the 2026-11-04 port programme to git so every work-package
worktree carries it: plans 101 to 145, their index in plans/README.md
and the planning record under plans/_audit/2026-11-04-port148/. The
record's data/brief-workflow3-decisions.md holds decisions D1 to D24,
which the maintainer delegated to the orchestrator on 2026-11-04.

The index gains the integrator's baseline after WP101: {T} tests,
{P} passed, {S} skipped, 0 failed; {E} ModuleData errors with
{N_MERGED} BROKEN_ITEM_REF on the laptop's public Armory; hook and
tool test counts. Later plans compare against these numbers.
```

**C3.5** In the next Bash call:

```
git -C C:/Users/mikew/source/repos/TAOM commit -F C:/Users/mikew/source/repos/taom-port/101-msg-programme.txt
```

**Verify**:

```
MAIN=C:/Users/mikew/source/repos/TAOM
git -C $MAIN log -3 --format='%h %s'
git -C $MAIN status --porcelain | grep -v '^ M .serena/project.yml$'
git -C $MAIN show HEAD:plans/README.md | grep -c 'Baseline after WP101'
```

Expected: the programme, record and merge subjects (newest first); the second command prints nothing
except files other sessions already had before Step C1 (compare with `101-main-dirty.txt`); `1`.

### Step C4: optional cleanup

If `git -C C:/Users/mikew/source/repos/taom-port/101 status --porcelain` prints nothing, free one of
the four worktree slots and keep the branch:

```
git -C C:/Users/mikew/source/repos/TAOM worktree remove C:/Users/mikew/source/repos/taom-port/101
```

### Step C5: maintainer decisions (outward-facing)

- Whether and when to push `bannerlord-1.4.5` (the desktop sees the plans only after a push).
- Close #697 after the in-game checks below (owed since `8df90444c`).
- Optional, on the desktop: `python tools/validate_moduledata.py --game-modules E:/LOTRAOM_Releases/public/Modules --code BROKEN_ITEM_REF`
  on the merged trunk, to confirm the laptop number against the channel's own Armory.

## Disposition

The pack owns no 1.5.x port group and no 1.5.x commit. Every item it names:

| Item | Kind | Disposition |
|---|---|---|
| `f8c9c8ecb` fix(hero-race): Load Game preview rider | trunk commit | taken as the merge's first-parent history; reaches local `bannerlord-1.4.5` by the Step C2 fast-forward |
| `55d779012` fix(custom-battles): named commanders, default troops (#709) | trunk commit | taken (as above); it computes Custom Battle defaults from culture data, with no hard-coded troop id, and the merge keeps all 857 troop ids |
| `d906fc921` fix(marketplace): Mouth of Sauron's helm | trunk commit | taken (as above) |
| `ccef172ad` chore(ci): remove the GitHub Actions workflow | trunk commit | taken (as above); D9 keeps CI removed |
| `1b00d1acd` fix(campaign): 1.4.8 supply, desertion and Uruk fixes | trunk commit | taken (as above); its `troops_isengard.xml` skirt fix survives the merge (blob `15df2d81...`) |
| `92a30ba42` fix(data): 1.5.x troop tree for the new Armory ids | public commit | merged unchanged (Step B4) |
| `10a52fb3d` fix(troops): add gondor_ring_peasant... | public commit | merged except its `.serena/project.yml` hunk (personal tool churn; it would also block the fast-forward against the maintainer's uncommitted copy) |
| `8df90444c` docs(data): review record (#697) | public commit | merged; lessons conflict resolved keep-both, trunk first (blob `3a3298e6...`) |
| XML-DATA-03 (trunk lacks the public troop tree) | finding | fixed by the merge; proven by the item-id set equality and the Step B3 against Step B7 `BROKEN_ITEM_REF` numbers |
| RELEASE-INTEGRITY-04 (public build and trunk diverged both ways) | finding | fixed by the merge; its validator check runs on the laptop after Part A (Steps B3, B7); the desktop rerun is optional (Step C5) |
| XML-DATA-02 (laptop data half trustworthy) | finding | Part A refreshes the Armory (D20); Step B8 updates `development-machines.md` and the CLAUDE.md trap row; the validator-header half goes to WP136 |
| BHA0001 Patch88 analyzer warning | build warning | recorded in the baseline (false positive, D4); silenced by WP104 |

## Test plan

- No new tests: the plan changes no C#.
- Regression evidence for the merge: the full suite on the merged tree in a real worktree, 9154 total,
  9151 passed, 3 skipped, 0 failed. The orchestrator measured 9154 total and 3 skipped on this tree in
  a `.git`-less export, where the four repo-root tests failed for want of `.git`.
- The one test that reads the installed Armory,
  `ShippedCultures_EveryBannerBearerReplacementWeaponIsOneHanded`, must pass on the refreshed Armory
  (it resolves the 43 banner-bearer replacement weapon ids named in `taom_spcultures.xml` and
  `spcultures.xslt`).
- Data: `validate_moduledata.py` and `audit_polearm_shield_parity.py` before and after the merge on
  the refreshed Armory.
- Harness and tools: `test_hooks.sh` and the Python tool suite, recorded once (the merge changes no
  file under `tools/` or `.claude/`).
- Not testable here: anything in game (see "In-game checks owed"); the commit body's `Not-tested:`
  trailer names it.

## Done criteria

All must hold:

- [ ] `git -C C:/Users/mikew/source/repos/TAOM merge-base --is-ancestor 8df90444cc2f1e6d83bb95d2164ab764b4fcae65 bannerlord-1.4.5` exits 0
- [ ] `git -C C:/Users/mikew/source/repos/TAOM merge-base --is-ancestor 1b00d1acd9f6ceda81709067ca88ee1393115b48 bannerlord-1.4.5` exits 0
- [ ] `git -C C:/Users/mikew/source/repos/TAOM rev-parse port148/101-baseline-sync~1^{tree}` prints `981c808820470f805e9ce6eda5b4f8ab1e7f5867`
- [ ] `git -C C:/Users/mikew/source/repos/TAOM diff --name-only port148/101-baseline-sync~1 port148/101-baseline-sync` prints exactly `CHANGELOG.md`, `CLAUDE.md`, `docs/reference/development-machines.md`
- [ ] `git -C C:/Users/mikew/source/repos/TAOM diff --quiet 1b00d1acd9f6ceda81709067ca88ee1393115b48 bannerlord-1.4.5 -- .serena/project.yml` exits 0
- [ ] Step B6: build exit 0 with `0 Error(s)`; tests `Failed: 0`, `Total: 9154`, `Skipped: 3`
- [ ] Step B7: the validator swept both extra roots, and `N_MERGED` is at most `N_ORIGIN`
- [ ] Step B7: the ModuleData gate command printed `gate exit=0`, recorded in the baseline block
- [ ] `git -C C:/Users/mikew/source/repos/TAOM log -3 --format=%s bannerlord-1.4.5` shows three subjects, each matching the v2.0.28 pattern and at most 72 characters
- [ ] `git -C C:/Users/mikew/source/repos/TAOM show bannerlord-1.4.5:plans/README.md | grep -c 'Baseline after WP101'` prints `1`
- [ ] `git -C C:/Users/mikew/source/repos/TAOM ls-tree -r --name-only bannerlord-1.4.5 -- plans | grep -c '^plans/1[0-4][0-9]-'` equals the Step C1 plan-file count
- [ ] `git -C C:/Users/mikew/source/repos/TAOM status --porcelain -- .serena/project.yml` prints ` M .serena/project.yml`, and `git -C C:/Users/mikew/source/repos/TAOM stash list | diff - C:/Users/mikew/source/repos/taom-port/101-stash-before.txt` prints nothing
- [ ] The installed Armory passes Step A2

## STOP conditions

Stop and report (do not improvise) if:

- Step A2 fails: the Armory is not refreshed. D20 makes every data-touching commit on the laptop a
  STOP until it is; report "BLOCKED on Part A".
- `origin/bannerlord-1.4.5` or `origin/fix/public-armory-ids-145` is not at its pinned SHA, or local
  `bannerlord-1.4.5` is neither `c5b84fb41` nor `1b00d1acd`: someone moved a ref after planning.
- Branch `port148/101-baseline-sync` or path `C:\Users\mikew\source\repos\taom-port\101` already
  exists: another session started this plan.
- The simulation in Step B1 prints a different tree id or a different conflict list.
- After `git merge --no-commit`, the unmerged paths are not exactly the lessons file. Run
  `git -C C:/Users/mikew/source/repos/taom-port/101 merge --abort` (it only touches the worktree) and
  report.
- The resolved lessons blob is not `3a3298e6...`, the `.serena/project.yml` blob is not `b56198b5...`,
  the Isengard blob is not `15df2d81...`, or `git write-tree` is not `981c8088...`. Do not hand-edit
  to force a hash; report what you see.
- `Main/_Module/SubModule.xml` does not read `v2.0.28`.
- The build has an error, or any BHA warning other than the single Patch88 BHA0001 line.
- Any test fails, the total is not 9154, or the skipped count is not 3. In particular a failure of
  `ShippedCultures_EveryBannerBearerReplacementWeaponIsOneHanded` after Part A means the public
  Armory lacks or reshapes a banner-bearer replacement weapon: a real public-build finding for the
  maintainer.
- The validator skips `LOTRLOME_Armory` or `TAOM_Map` (`NOT FOUND` or `SKIPPED` for an extra root),
  or `N_MERGED` exceeds `N_ORIGIN` (the installed Armory is not the one the public troop tree
  targets).
- The ModuleData gate command in Step B7 exits non-zero. Route it to the maintainer: every later
  ModuleData commit would be denied once WP103 is live, so the programme cannot start from this
  baseline until the maintainer decides.
- A tracked file stays modified after Step B7 and is not a `tools/` test fixture.
- `CLAUDE.md` grows, or the numstat in Step B8 shows any deletion in `CHANGELOG.md` or more than one
  changed line in `CLAUDE.md`.
- Part C: the main checkout is not on `bannerlord-1.4.5`, the fast-forward is not possible, `comm`
  in Step C1 prints any path, `plans/README.md` does not mention `101-baseline-sync`, or the stash
  list or the ` M .serena/project.yml` line changes. Never stash, reset or check out paths in the main
  checkout to get past this.
- Any hook denies or asks: report it; never retry with `--no-verify` and never route git through
  PowerShell.
- You catch yourself about to run `/verify`, `/ship`, `./build.ps1`, a build without
  `-p:ModuleId=`, or anything that writes into the game install.

## In-game checks owed (maintainer)

Executors cannot run the game. These need a build of `bannerlord-1.4.5` deployed by the maintainer
(the maintainer's own `./build.ps1` or release flow) on the refreshed laptop or on the desktop.

1. Gondor bows. New campaign, encyclopedia: Belfalas Hunter (`gondor_bel_hunter`) carries
   `ladder_gondor_bow_t2`, Lebennin Archer (`gondor_leb_archer`) carries `ladder_gondor_bow_t4`; in a
   battle those troops hold bows. An empty slot logs nothing, so this is a visual check.
2. Isengard skirts (1b00d1acd). Uruk-Hai Champion (`urukhai_champion`) and Uruk-Hai Berserker
   (`urukhai_berserker`) wear `sk_uruk_hai_skirt_a1` as body armour.
3. Old save near Glanhir (#670, owed by `8df90444c`). An old campaign saved near Glanhir
   (`castle_EW2`) loads, and Glanhir's volunteers are `gondor_ring_militia` and
   `gondor_ring_footman`.
4. Logs. `<game>\bin\Win64_Shipping_Client\Logs\taom_debug_*.log` shows no new ERROR at campaign
   start. The rgl log in `C:\ProgramData\Mount and Blade II Bannerlord\logs` will now show three
   `The 'taom_body_length' attribute is not declared` errors each time the monster XML loads (game
   start and every in-game load; 36 errors over 12 loads in the 2026-11-01 player log) from the public
   Armory's elk and animalia monster files: expected (D15; WP115 turns them into a pairing signal).

## Maintenance notes

- WP102 (plan 020) retires the hand-edited CHANGELOG; the two entries this plan adds are the last
  ones (D1). From WP102 on, the commit body is the entry.
- WP103 trims `CLAUDE.md` under its cap and inherits the new "Two machines" row; keep its meaning.
  WP103 also lists this plan's Armory refresh as a precondition for its ModuleData gate.
- WP142 and WP144 sync docs from 1.5.x, whose `development-machines.md` differs a lot (76 lines added
  there). Keep this plan's dated laptop paragraph and table cell: they are 1.4.x facts the 1.5.x copy
  does not have.
- WP136 adds module versions and dates to the validator header (the other half of XML-DATA-02).
  WP104 silences BHA0001. WP115 handles the `taom_body_length` signal (D15).
- Every later data plan compares against this plan's validator numbers; a new `BROKEN_ITEM_REF` on
  the laptop is now a real regression, not an environment gap.
- If `origin/fix/public-armory-ids-145` ever gains commits, merge it again the same way.
- `plans/README.md` belongs to the integrator: this plan's Part C is the one exception. Later executors
  never edit it; they report status, commit hashes and measured counts in their hand-off, and the
  integrator records them after each merge.
- The next 1.4.x release (D8: v2.4.0) must pin its `LOTRLOME_Armory` and `TAOM_Map` packages. This
  plan's laptop Armory is the public 2026-09-29 build.
- Reviewer focus: the merge tree hash, the dropped `.serena/project.yml` hunk, and that the record
  commit is docs-only. `/deep-review` is not needed (no new C#); `/security-scan` is not needed (no
  hook or settings change).
