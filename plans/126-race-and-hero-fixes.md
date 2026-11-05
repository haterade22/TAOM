# Plan 126: Keep every hero's race across a load, frame dwarf portraits, and gate character races against skins.xml

> **Executor instructions**: Follow this plan step by step, in one session, in the worktree named
> under "Git workflow". Run every verification command and confirm the expected result before moving
> on. If anything in "STOP conditions" occurs, stop and report; do not improvise. When done, report
> status, commit hashes and measured counts in your hand-off (Step 5). Never edit `plans/README.md`:
> it belongs to the integrator, who records your numbers after the merge.
>
> **Drift check (run first, inside Step 0)**:
> `git diff --stat 1b00d1acd..HEAD -- TAOM.Tests/Core/CultureRaceConsistencyTests.cs .claude/rules/xslt.md Main/_Module/ModuleData/configs/CharacterImagePatch.json Main/Adapters/HeroRosterAdapter.cs Main/Adapters/IHeroRosterAdapter.cs Main/Features/HeroRace/RacePersistenceService.cs Main/Features/HeroRace/RacePersistenceBehavior.cs TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs docs/features/hero-race.md`
> Earlier WPs of this programme are expected to have changed some of these: WP104 converts
> `HeroRaceWiringTests.cs` and `RacePersistenceBehaviorTests.cs` to `RepoPaths`; WP119 (if merged)
> edits `RacePersistenceService.cs`, `RacePersistenceBehavior.cs`, their tests and two diagram lines
> plus a changelog line of `hero-race.md`. Step 0 lists the exact checks; STOP only when one of them
> fails.

## Status

- **Priority**: P1
- **Effort**: S (one session)
- **Risk**: LOW
- **Depends on**: `plans/105-composition-root.md` merged (it sits on WP104, which brings
  `TAOM.Tests/Infrastructure/RepoPaths.cs` and converts `HeroRaceWiringTests.cs` to it), and WP101's
  laptop Armory refresh done (D20). Not required: WP119; either merge order works (Step 3 handles both).
- **Category**: bug
- **Wave / lane**: wave 3, lane D
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (HeroRace has no
  1.5.x feature module and nothing here is wired, so `Main/IoC.cs` and `Main/SubModule.cs` stay
  untouched), D7 (the co-op half of `0f7902684` is not ported), D8 (subjects carry `v2.0.28`), D10
  (an ordinary session executes this file), D13 (the `lord_1_48_*` double definition is recorded as a
  follow-up, not fixed here), D19 (git only through the Bash tool), D20 (the race gate reads the
  installed Armory, so it needs the refreshed one)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. The 1.5.x ref has since moved: `git diff e45f6df2c ca1cba35f` over every
  in-scope path is empty (checked), and at `ca1bc5f2e` the only in-scope change is `834e0703c`, which
  rewrites `CultureRaceConsistencyTests.cs` on 1.5.x. It is not in this WP; Step 1 takes the pinned
  `437d59111` blob, so a moving tip cannot change this plan.
- **Issue**: port of #1011 (`f981629cd`) and #1022 (`0f7902684`); the race gate comes from #644 and
  #645 (`9804f67b9`, `437d59111`). `6c26f003a` carries no issue number. The orchestrator creates the
  issue (or one WP126 tracking issue) before implementation lands.

## Why this matters

- **Dead heroes lose their race on every load.** On load, v1.4.8 sets the race of every non-original
  hero character, dead and disabled ones included, back to its origin character's race
  (`SandBoxManager.InitializeCharactersAfterLoad`, quoted below). The 1.4.x `HeroRosterAdapter` reads
  only `Hero.AllAliveHeroes`, so TAOM never saves or restores a dead hero's race. A non-human player's
  dead child comes back with its origin character's race: the encyclopedia shows the wrong race, and
  because TAOM builds a child from the same-sex parent's `CharacterObject`
  (`Main/Features/RaceAge/Models/TaomHeroCreationModel.cs:9-16`), a child born after that parent died
  inherits the wrong race, lifespan and abilities.
- **One unregistered race name throws during the character load.** v1.4.8 reads every
  `NPCCharacter race=` through `FaceGen.GetRaceOrDefault`, a plain dictionary index that throws
  `KeyNotFoundException` on an unknown name. The races live in the unversioned Armory, so an Armory
  update or reinstall can drop one with no change in this repo. On 1.4.x no test checks character
  races against skins.xml, and there is no CI (D9). Its one race-registration test,
  `EveryAllowedRaceIsARealRegisteredRace` (it checks `cultures.json` only), hard-codes
  an `E:\Steam` path, so it is skipped on the laptop on every run (one of the three skips in the WP101
  baseline). After this plan both install checks run on the laptop.
- **Dwarf 2D portraits sit too low**: settlement notables, party and battle screens show little more
  than hair and eyes. 1.4.x ships the same pre-fix values as 1.5.x did.
- **A code comment, its test and the feature doc state a co-op claim the 1.5.x line disproved**
  ("a co-op host's save transfer never raises `OnBeforeSaveEvent`").
- Save cost was checked against the save-time evidence (V01): v1.4.8 saves a dictionary as one
  container holding an element per key and value (`ContainerSaveData.cs:71-91` in the dump), so dead
  heroes add entries to one existing container, not new containers. One long-campaign timing check is
  still owed (In-game checks).

## Current state

### Files and their roles (`origin/bannerlord-1.4.5` line numbers; WP119 may shift the HeroRace ones)

| File | Role |
|---|---|
| `Main/Adapters/HeroRosterAdapter.cs` | Adapter (ADR-007) over `Hero`; reads only `Hero.AllAliveHeroes` (:14, :22, :28) |
| `Main/Adapters/IHeroRosterAdapter.cs` | `HeroRaceInfo` struct and the interface; `GetAllAliveHeroRaces()` at :19 |
| `Main/Features/HeroRace/RacePersistenceService.cs` | Captures and restores races; calls `GetAllAliveHeroRaces()` at :70 (capture) and :109 (restore); saves `_taom_heroRaceMap` and `_taom_raceNameLegend` at :179-180. No `#644` wraith skip on this line |
| `Main/Features/HeroRace/RacePersistenceBehavior.cs` | Entry point (ADR-002); comment :23-34 above `OnSessionLaunched` (:35-39) carries the co-op claim at :26-29 |
| `TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs` | 32 tests on 1b00d1acd, 40 uses of `GetAllAliveHeroRaces`; class closes at :715; `RoundTripDataStore` at :723 |
| `TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs` | 6 tests; the co-op claim repeats in a comment at :72-74 (:73-75 after WP104) |
| `TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs` | 6 tests; after WP104 it has `using TAOM.Tests.Infrastructure;` and reads sources through `RepoPaths.ReadSource` |
| `TAOM.Tests/Core/CultureRaceConsistencyTests.cs` | 2 tests; `EveryAllowedRaceIsARealRegisteredRace` (:114-147) hard-codes `E:\Steam\...` skins.xml paths (:123-124) and goes Inconclusive at :127-128 |
| `Main/_Module/ModuleData/configs/CharacterImagePatch.json` | 2D portrait offsets, read by `RacePositionStore.ResolveImage` and applied in `CharacterSpawnerService.cs:140-147` |
| `.claude/rules/xslt.md` | XSLT rules; "## The gate" section ends at :65, "## Identity Transform" at :67 |
| `docs/features/hero-race.md` | Feature doc: persistence paragraph :32, session-launch paragraph :34, diagram line :80, Dependencies :143, Tests :148, `## Changelog` :223 |
| `Main/IoC.cs:215` | `container.Register<IHeroRosterAdapter, HeroRosterAdapter>(Reuse.Singleton);` (unchanged) |

Other consumers of the adapter, unchanged by this plan (they call `GetHeroRace`/`SetHeroRace`, whose
names stay): `CharacterCreationContentService.cs:313, :335`, `NamedCompanionService.cs:57`,
`JoinReconciliationService.cs:80, :82`.

### Excerpts (`origin/bannerlord-1.4.5`)

```csharp
// Main/Adapters/HeroRosterAdapter.cs
 9    public IReadOnlyList<HeroRaceInfo> GetAllAliveHeroRaces()
14        return Hero.AllAliveHeroes
15            .Where(h => h?.CharacterObject != null)
16            .Select(h => new HeroRaceInfo(h.StringId, h.CharacterObject.Race))
22        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == heroStringId);   // GetHeroRace
28        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == heroStringId);   // SetHeroRace

// Main/Features/HeroRace/RacePersistenceService.cs
70        var heroes = _heroRosterAdapter.GetAllAliveHeroRaces();     // CaptureHeroRaces
109       var heroes = _heroRosterAdapter.GetAllAliveHeroRaces();     // RestoreHeroRaces

// TAOM.Tests/Core/CultureRaceConsistencyTests.cs
121       var skins = new[]
122       {
123           @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\skins.xml",
124           @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\skins.xml",
125       }.Where(File.Exists).ToArray();
127       if (skins.Length == 0)
128           Assert.Inconclusive("Bannerlord install not found; race registration cannot be checked here.");

// Main/_Module/ModuleData/configs/CharacterImagePatch.json (whole file, one line, no newline)
{"Items":[{"Race":"dwarf","Horizontal":0.0,"Vertical":0.15,"Zoom":-0.1}]}
```

### Engine facts (v1.4.8, read this session with `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`; cache `C:\Users\mikew\.taom-src\v1.4.8`)

- `TaleWorlds.CampaignSystem.Hero`: `Hero.cs:2130` `public static Hero FindFirst(Func<Hero, bool> predicate)`
  returns `Campaign.Current.Characters.FirstOrDefault((CharacterObject x) => x.IsHero && predicate(x.HeroObject))?.HeroObject`;
  `Hero.cs:2140` `public static IEnumerable<Hero> FindAll(Func<Hero, bool> predicate)` selects
  `x.HeroObject` from `Campaign.Current.Characters` where `x.IsHero && predicate(x.HeroObject)`;
  `Hero.cs:888` `public static MBReadOnlyList<Hero> AllAliveHeroes => Campaign.Current.AliveHeroes;`;
  `Hero.cs:890` `DeadOrDisabledHeroes`; `Hero.cs:194` `public CharacterObject CharacterObject => _characterObject;`.
- `TaleWorlds.CampaignSystem.Campaign`: `Campaign.cs:503` `public MBReadOnlyList<CharacterObject> Characters => _characters;`,
  filled at `:1107` with `objectManager.GetObjectTypeList<CharacterObject>()`.
- `TaleWorlds.CampaignSystem.SandBoxManager.InitializeCharactersAfterLoad(bool isSavedCampaign)`
  (`SandBoxManager.cs:384-421`): on a saved campaign it calls `InitializeHeroCharacterOnAfterLoad()` for
  every non-original character in `AliveHeroes`, in `DeadOrDisabledHeroes`, and for every not-ready,
  non-original `CharacterObject` that has a `HeroObject`.
- `CharacterObject.InitializeHeroCharacterOnAfterLoad()` (`CharacterObject.cs:519`) calls
  `BasicCharacterObject.InitializeHeroBasicCharacterOnAfterLoad(_originCharacter)` (`BasicCharacterObject.cs:297-313`),
  which runs `Race = originCharacter.Race;` at `:304`. `BasicCharacterObject.cs:76` `public int Race { get; set; }`.
- `CharacterObject.CreateFrom` (`CharacterObject.cs:416-419`) sets `_originCharacter = character._originCharacter ?? character;`.
  `HeroCreator.cs:261` asks `HeroCreationModel.GetCharacterTemplateForOffspring`, and
  `BasicCharacterObject.FillFrom` copies `Race = character.Race;` (`:201`).
- `TaleWorlds.Core.FaceGen.GetRaceOrDefault(string raceId)` (`FaceGen.cs:40-42`) returns
  `_instance?.GetRaceOrDefault(raceId) ?? 0`; `TaleWorlds.MountAndBlade.FaceGen.GetRaceOrDefault` (`FaceGen.cs:115-118`)
  is `return _raceNamesDictionary[raceId];` (throws on an unknown name). `BasicCharacterObject.Deserialize`
  sets `Race = 0;` (`:323`) then `Race = FaceGen.GetRaceOrDefault(xmlAttribute2.Value);` (`:327`).
- Save system (dump `C:\Decompiled_Bannerlord\_categories_v1.4.8\Core\TaleWorlds.SaveSystem\TaleWorlds.SaveSystem.Save\ContainerSaveData.cs`):
  `CollectChildren` (:71-91) makes one `ElementSaveData` per dictionary key and value inside one
  container; `CollectStrings` (:364-368) puts string elements in the save's string table.

### Data facts (this session)

- The laptop's installed skins.xml files (Armory still `v2.0.26` today; WP101 replaces it) register,
  by the test's own regex: Native `human`; LOTRLOME_Armory `dwarf, uruk, nazghul, orc, uruk_hai,
  berserker, cave_troll, hill_troll, pale_uruk, dg_uruk, goblin, elf, saruman, sauron`. TAOM_Map's
  skins.xml is a placeholder with no race.
- The races 1.4.x data spells (Python replica of the new gate over `origin/bannerlord-1.4.5`): XML
  `dg_uruk, dwarf, pale_uruk, uruk_hai, orc, berserker, elf, uruk, goblin, human, cave_troll`;
  `lords.xslt` `sauron` (1), `uruk` (6), `orc` (3); no race computed at transform time; all 8
  stylesheets parse; `cultures.json` uses 10 races. Every one is registered by the `v2.0.26` Armory.
  Whether the public 2026-09-29 Armory registers them all is UNVERIFIED from here (STOP if not).
- `origin/fix/public-armory-ids-145` (merged by WP101) changes no `race=` attribute.
- `lords.xslt:1060` is the only `sauron` race line: `<xsl:attribute name="race">sauron</xsl:attribute>`.

### Not on 1.4.x yet (`git grep` on `origin/bannerlord-1.4.5` this session)

No `GetAllHeroRaces`, `Hero.FindAll`, `EveryCharacterRaceIsARealRegisteredRace`, `RacesEmittedByXslt`
or `"Vertical":0.3` anywhere in `Main`, `TAOM.Tests`, `.claude` or `docs`. `Main/Features/SignatureStrikes`
does not exist on 1.4.x. `Main/Features/NazgulFamily` (with `INazgulRegistry`) exists, but the #644
race content is not ported, so nothing here uses it.

### Conventions that bind this change

- ADR-007 adapters: services never touch sealed TaleWorlds types. `HeroRosterAdapter` is the only file
  that touches `Hero`; `RacePersistenceService` sees `HeroRaceInfo` only. Keep the adapter's
  `h?.CharacterObject != null` null guard (`.claude/rules/adapters.md`: `?.` on computed properties).
- ADR-002 thin entry points (under 150 lines, delegate to services): `RacePersistenceBehavior` only
  gets a comment change.
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`: the rename `GetAllAliveHeroRaces` to
  `GetAllHeroRaces` migrates every caller and test in the same commit, with no shim; ADR-005 no
  `#if DEBUG` outside IoC registration.
- TDD: failing test first for new work. Step 1 carries this plan's new work (the install lookup) with
  a red run first and a mutation probe; ported commits bring their own tests.
- MCM json2 rule (rename a persisted setting, never flip its default): no setting is touched.
- `.claude/rules/harmony-patches.md` (MovementOrder postfixes through the shared deferred category):
  no Harmony patch is touched.
- Line endings: this checkout uses `core.autocrlf=true` (worktree files are CRLF, blobs are LF). The
  edit scripts below detect and keep each file's line ending; never convert a file wholesale.

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/126 &&` (Bash tool). Give full
test runs a 600000 ms timeout. Never `./build.ps1`; never two builds at once in one worktree.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 Patch88 warning may print if WP104 did not silence it; ignore it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` | as each step says |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline |
| Docs | `python tools/lint_docs.py` | no finding on a line this plan added |

Both `-p:` flags are mandatory on every build and test: on 1.4.x `Main/TAOM.csproj` does not gate the
CopyModule target on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game
install. `python` is `C:\Python314\python.exe` (on the Bash PATH as `python`).

## Scope

**In scope** (the only files you modify):
- `TAOM.Tests/Core/CultureRaceConsistencyTests.cs`
- `.claude/rules/xslt.md` (one paragraph that documents the gate; per-port procedure step 2 allows it)
- `Main/_Module/ModuleData/configs/CharacterImagePatch.json`
- `Main/Adapters/HeroRosterAdapter.cs`, `Main/Adapters/IHeroRosterAdapter.cs`
- `Main/Features/HeroRace/RacePersistenceService.cs`
- `Main/Features/HeroRace/RacePersistenceBehavior.cs` (comment only)
- `TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs`, `TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs`,
  `TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs` (comment only)
- `docs/features/hero-race.md`
- Any other file under `Main` or `TAOM.Tests` that an earlier WP left calling `GetAllAliveHeroRaces`
  (the Step 3 rename only; name it in your report)
- Transient, never committed: one probe edit to `Main/_Module/ModuleData/lords.xslt` in Step 1, reverted
  in the same step.

**Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs` and `Main/TAOM.csproj` get no edit. The
adapter stays registered at `Main/IoC.cs:215`; new `.cs` files are not needed (the test project is
SDK-style anyway). Needing any edit there is a STOP.

**Out of scope** (do not touch, even though the 1.5.x commits do):
- The #644 Nazgul content: `lords.xslt`/`characters/lords.xml` race, age and kit edits, the
  `RacePersistenceService` wraith skip and its `INazgulRegistry` constructor parameter, the DreadAura,
  UncapturableHeroes and CombatMechanics race lists and configs, `ShippedCombatMechanicsConfigTests.cs`,
  `NazgulRaceDataTests.cs`, `INazgulRegistry.cs`, `.claude/rules/tests.md`.
- SignatureStrikes (absent on 1.4.x).
- `docs/features/coop-interop.md` and the co-op paragraph `0f7902684` adds to `hero-race.md` (D7).
- `CHANGELOG.md` (D1), `docs/reviews/**` (RCAs, lessons, REVIEW-LOG, Codex records),
  `docs/reviews/LESSONS-LEARNED.md`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md`,
  other feature docs and `docs/modding/*` (WPs 142 to 144 sync bookkeeping).
- `CharacterAvatarPatch.json` (unchanged by `6c26f003a`).
- The `lord_1_48_1/2/3` double definition (Maintenance notes, follow-up under D13).
- `35007dbb8` and `180696430`, which the draft map in `verify/K3-wp-map-critic.md` filed under 126:
  the final map gives both to WP129.
- `plans/README.md` (the integrator's file; report your numbers in the Step 5 hand-off instead).
- Anything in the game install. The laptop's `.serena/project.yml` and any stash in the main checkout.

## Git workflow

- Worktree (create once; reuse it if it already exists):
  `git -C /c/Users/mikew/source/repos/TAOM worktree add /c/Users/mikew/source/repos/taom-port/126 -b port148/126-race-and-hero-fixes bannerlord-1.4.5`
  Base: local `bannerlord-1.4.5` after WP101, plus every WP merged before this one. Work only there.
- Machine: the maintainer's laptop; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (`BANNERLORD_GAME_DIR`).
  Never write into the game install. At most four executor worktrees at once.
- Git only through the Bash tool (D19). Stage explicit paths; never `git add -A`, `git add .`,
  `git commit -a`. Never stash, `reset --hard`, pull, push, tag, open a PR or merge into
  `bannerlord-1.4.5`; the integrator merges.
- Per-port procedure (binding): apply with `git cherry-pick -n <sha>` only; never `--continue`, `-C`,
  `-c` or `--amend --no-edit` (`GIT_EDITOR=true` on this laptop would commit the 1.5.x message).
  Drop bookkeeping hunks: a conflicted or changed bookkeeping path goes back with
  `git checkout HEAD <path>`; a bookkeeping file the pick adds goes with `git rm -q -f <path>`. If
  the `block-dangerous-git` hook asks about these, approve: they discard only the pick's own output in
  this worktree. If `git status` still reports a cherry-pick in progress after a commit, run
  `git cherry-pick --quit`.
- Commit: write the message with the Write tool to
  `C:\Users\mikew\source\repos\taom-port\msg-126-<n>.txt` (outside every worktree tree; a literal path
  lets the subject hook read it), stage with explicit `git add <paths>`, then run
  `git commit -F C:/Users/mikew/source/repos/taom-port/msg-126-<n>.txt` in the NEXT sequential Bash
  call, never in the same command and never through PowerShell.
- Commit template: subject `<type>(<scope>): v2.0.28 - <description>`, at most 72 characters; body
  wrapped at 72, written for 1.4.x readers (under plan 020 bodies become release notes), no em or en
  dashes, no Co-Authored-By or "Generated with" line; trailers `Research:`, `Not-tested:`, optional
  `Save-compat:`/`Rejected:`, and one `Ported-from: <40-hex sha>` per included 1.5.x commit. The
  exact messages are given in each step.
- Build and test after each commit. Full suite at the end (Step 5).

## Steps

### Step 0: Preflight (read-only apart from the worktree and the build)

1. Create the worktree (command above), then:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/126 && git status --porcelain | wc -l && git rev-parse HEAD && echo "$BANNERLORD_GAME_DIR"
   ```
   Expected: `0`, a sha (record it as `BASE`), and the game path. An empty game path is an
   environment failure: STOP and report; do not set the variable yourself.
2. Preconditions:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/126 && \
   test -f Main/Composition/FeatureModules.cs && echo WP105-OK; \
   test -f TAOM.Tests/Infrastructure/RepoPaths.cs && echo REPOPATHS-OK; \
   grep -c 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
   grep -c '^using TAOM.Tests.Infrastructure;' TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs; \
   grep -o '<Version value="[^"]*"' "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/SubModule.xml"; \
   git grep -n -e 'GetAllHeroRaces' -e 'EveryCharacterRaceIsARealRegisteredRace' -e 'RacesEmittedByXslt' -- Main TAOM.Tests .claude; echo "grep exit $?"; \
   grep -c 'Hero.FindAll\|Hero.FindFirst' Main/Adapters/HeroRosterAdapter.cs; \
   git diff --quiet 1b00d1acd HEAD -- Main/_Module/ModuleData/configs/CharacterImagePatch.json Main/Adapters/HeroRosterAdapter.cs Main/Adapters/IHeroRosterAdapter.cs && echo ADAPTERS-AND-JSON-UNCHANGED; \
   git diff 1b00d1acd HEAD -- TAOM.Tests/Core/CultureRaceConsistencyTests.cs | grep '^[-+]' | grep -v '^+++\|^---'; echo "race-test drift listed above"
   ```
   Expected, in order: `WP105-OK`; `REPOPATHS-OK`; `1`; `1`; an Armory version that is NOT `v2.0.26`
   (WP101 installs the public 2026-09-29 build, which plan 101 expects to read `v2.0.29.5`); no
   `git grep` line, then `grep exit 1`; `0`; `ADAPTERS-AND-JSON-UNCHANGED`; and for the race test
   either no lines, or only added `[TestCategory(...)]` lines (write those down for Step 1.1). Any
   other result is a STOP (see "STOP conditions").
3. Drift check (the command at the top). Then confirm the text anchors the edit scripts need still
   exist exactly once:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/126 && \
   grep -c 'the trailing capture covers save transfers that never raise OnBeforeSaveEvent' docs/features/hero-race.md; \
   grep -c 'A multiplayer host handing its world to a joining client never raises that event' docs/features/hero-race.md; \
   grep -c 'iterates all alive heroes and sets race values' docs/features/hero-race.md; \
   grep -c 'and `CaptureHeroRaces_EmptyRaceList_DoesNotOverwriteRicherCapturedData`\.' docs/features/hero-race.md; \
   grep -c '`SyncRaceData` also clears both fields when `dataStore.IsLoading` before syncing' docs/features/hero-race.md; \
   grep -c '^## Changelog' docs/features/hero-race.md; \
   grep -c 'The capture half exists for save transfers that never raise' Main/Features/HeroRace/RacePersistenceBehavior.cs; \
   grep -c "A co-op host's save transfer to a joining client never raises OnBeforeSaveEvent" TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs; \
   grep -c '^## Identity Transform' .claude/rules/xslt.md
   ```
   Expected: nine lines, each `1`.
4. Baseline build and tests:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/126 && dotnet restore TAOM.sln && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=
   ```
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/126 && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=
   ```
   Expected: build exit 0; tests `Failed: 0`. Record `BASE_TOTAL`, `BASE_PASSED`, `BASE_SKIPPED`
   (compare with the count the previous merged WP recorded in the main checkout's
   `C:\Users\mikew\source\repos\TAOM\plans\README.md`, if any; read it, never edit it). Then run the
   Filtered command three times and record each total:
   - `--filter "FullyQualifiedName~CultureRaceConsistencyTests"`: expected `Passed: 1`, `Skipped: 1`,
     `Total: 2` (the install check skips because `E:\Steam` is absent here).
   - `--filter "FullyQualifiedName~RacePersistenceServiceTests"`: record as `RPS_BASE` (32 on
     `1b00d1acd`; more if WP119 landed).
   - `--filter "FullyQualifiedName~HeroRaceWiringTests"`: record as `HRW_BASE` (6 on `1b00d1acd`).

**Verify**: every expectation above holds.

### Step 1: Gate every character race against skins.xml (`9804f67b9` and `437d59111`, partial; test file transplanted, install lookup adapted)

Sources: `9804f67b9` "feat(nazgul): v2.0.30 - the Nine become race nazghul (#644)" and `437d59111`
"fix(nazgul): v2.0.30 - Codex review follow-ups for #644 and #645". Only their test gate and one
rules paragraph are ported; the rest is content or a 1.5.x-only feature (Disposition table).

Method: transplant. `TAOM.Tests/Core/CultureRaceConsistencyTests.cs` on 1.4.x equals the file before
`9804f67b9`, and only these two commits touch it up to `437d59111`, so the `437d59111` blob is exactly
the 1.4.x file plus both test changes. Not cherry-pick: the simulation (`git merge-tree` onto
`1b00d1acd`) conflicts `9804f67b9` in `CHANGELOG.md`, `ShippedCombatMechanicsConfigTests.cs`,
`hero-race.md`, `lord-identity-reconciliation.md`, `lords-and-heroes.md`, `feature-map.md` and three
lessons files, and `437d59111` in `RacePersistenceService.cs`, two SignatureStrikes files and their
test, `CultureRaceConsistencyTests.cs`, two feature docs and seven bookkeeping files (`CHANGELOG.md`,
REVIEW-LOG, LESSONS-LEARNED, two lessons, two RCAs); none of that is wanted.

1.1 **Bring the ported test file (RED for the new install lookup).**
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && git checkout 437d59111 TAOM.Tests/Core/CultureRaceConsistencyTests.cs && git status --porcelain
```
Expected: `M  TAOM.Tests/Core/CultureRaceConsistencyTests.cs`. If Step 0.2 listed added
`[TestCategory(...)]` lines, insert each again directly below the `[TestClass]` line now. Then Build,
and run Filtered with `--filter "FullyQualifiedName~CultureRaceConsistencyTests"`.

**Verify (RED)**: build exit 0; `Failed: 0`, `Passed: 5`, `Skipped: 2`, `Total: 7`. The two skips are
`EveryCharacterRaceIsARealRegisteredRace` and `EveryAllowedRaceIsARealRegisteredRace`: the 1.5.x
lookup only knows `E:\Steam`. (If all 7 pass, this machine has the desktop path; note it and go on.)

1.2 **Adapt the install lookup and the engine cites (GREEN).** Run:
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && python -I - <<'PY'
from pathlib import Path
p = Path("TAOM.Tests/Core/CultureRaceConsistencyTests.cs")
raw = p.read_bytes().decode("utf-8")
nl = "\r\n" if "\r\n" in raw else "\n"
lines = raw.split(nl)

def one(text):
    hits = [i for i, l in enumerate(lines) if l == text]
    assert len(hits) == 1, (text, hits)
    return hits[0]

sig = one("    private static HashSet<string> RegisteredRaces()")
start = sig - 4
assert lines[start] == "    /// <summary>", lines[start]
inc = one('            Assert.Inconclusive("Bannerlord install not found; race registration cannot be checked here.");')
assert inc > sig
lines[start:inc + 1] = [
    r'    // Desktop install path. BANNERLORD_GAME_DIR (set on the laptop) is tried first, the same',
    r'    // order LordFamilyTransformTests.SandBoxModuleData uses.',
    r'    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";',
    r'',
    r'    /// <summary>',
    r'    /// Race ids the installed skins.xml files register. They live in the Armory, a game-install',
    r'    /// path, so a caller skips (Inconclusive) rather than fails when the install is not present.',
    r'    /// The install is the first of BANNERLORD_GAME_DIR and the desktop path that holds the Armory',
    r'    /// skins.xml; the Armory and Native skins.xml of that install are read.',
    r'    /// </summary>',
    r'    private static HashSet<string> RegisteredRaces()',
    r'    {',
    r'        var game = new[] { Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"), DefaultGameDir }',
    r'            .FirstOrDefault(root => !string.IsNullOrEmpty(root)',
    r'                && File.Exists(Path.Combine(root, "Modules", "LOTRLOME_Armory", "ModuleData", "skins.xml")));',
    r'',
    r'        if (game == null)',
    r'            Assert.Inconclusive("Bannerlord install not found; race registration cannot be checked here.");',
    r'',
    r'        var skins = new[] { "LOTRLOME_Armory", "Native" }',
    r'            .Select(module => Path.Combine(game!, "Modules", module, "ModuleData", "skins.xml"))',
    r'            .Where(File.Exists)',
    r'            .ToArray();',
]

def swap(old, new):
    lines[one(old)] = new

swap("        // alike, and despite its name that is a plain dictionary index (v1.5.3",
     "        // alike, and despite its name that is a plain dictionary index (v1.4.8")
swap("        // (#644 put nazghul on the Nine).",
     "        // (the gate came with #644 on the 1.5.x line).")
swap("        // the name as given (v1.5.3 BasicCharacterObject.cs:327, FaceGen.cs:115-118), so a padded",
     "        // the name as given (v1.4.8 BasicCharacterObject.cs:327, FaceGen.cs:115-118), so a padded")
out = nl.join(lines)
assert "v1.5" not in out
assert out.count(r"E:\Steam") == 1
p.write_bytes(out.encode("utf-8"))
print("RegisteredRaces adapted")
PY
```
The three swapped comment lines cite v1.4.8 lines verified this session (`BasicCharacterObject.cs:327`,
`TaleWorlds.MountAndBlade` `FaceGen.cs:115-118`). Then Build and the same Filtered run.

**Verify (GREEN)**: script prints `RegisteredRaces adapted`; build exit 0; `Failed: 0`, `Passed: 7`,
`Skipped: 0`, `Total: 7`.

1.3 **Prove the gate can fail (mutation probe, never committed).**
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && sed -i 's|<xsl:attribute name="race">sauron</xsl:attribute>|<xsl:attribute name="race">sauron_probe</xsl:attribute>|' Main/_Module/ModuleData/lords.xslt && grep -c 'sauron_probe' Main/_Module/ModuleData/lords.xslt
```
Expected `1`. Run Filtered with `--filter "FullyQualifiedName~CultureRaceConsistencyTests.EveryCharacterRaceIsARealRegisteredRace"`:
expected `Failed: 1`, and the failure message contains `lords.xslt -> 'sauron_probe'`. Then revert the
probe (approve the hook prompt; it discards only the probe):
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && git checkout Main/_Module/ModuleData/lords.xslt && git diff --quiet -- Main/_Module/ModuleData/lords.xslt && echo PROBE-REVERTED
```
**Verify**: `PROBE-REVERTED`; the same Filtered run now passes (`Passed: 1`).

1.4 **Document the gate in the XSLT rules** (the `437d59111` paragraph, verbatim):
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && python -I - <<'PY'
from pathlib import Path
p = Path(".claude/rules/xslt.md")
raw = p.read_bytes().decode("utf-8")
nl = "\r\n" if "\r\n" in raw else "\n"
lines = raw.split(nl)
assert not any("EveryCharacterRaceIsARealRegisteredRace" in l for l in lines), "paragraph already present"
hits = [i for i, l in enumerate(lines) if l == "## Identity Transform"]
assert len(hits) == 1, hits
lines[hits[0]:hits[0]] = [
    r'A race a stylesheet spells (`xsl:attribute name="race"` or a literal `NPCCharacter race=`) is checked',
    r'verbatim against the installed `skins.xml` races by',
    r'`CultureRaceConsistencyTests.EveryCharacterRaceIsARealRegisteredRace`, because the engine indexes the',
    r'name exactly and throws on an unknown one. It fails on a race computed at transform time, so spell a',
    r'race as literal text.',
    r'',
]
p.write_bytes(nl.join(lines).encode("utf-8"))
print("xslt.md paragraph inserted")
PY
```
**Verify**: prints `xslt.md paragraph inserted`;
`MSYS_NO_PATHCONV=1 git diff --stat -- .claude/rules/xslt.md` shows `1 file changed, 6 insertions(+)`.

1.5 **Commit.** Stage and check:
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && git add TAOM.Tests/Core/CultureRaceConsistencyTests.cs .claude/rules/xslt.md && git status --porcelain
```
Expected exactly `M  .claude/rules/xslt.md` and `M  TAOM.Tests/Core/CultureRaceConsistencyTests.cs`.
Message file `msg-126-1.txt` (the 1.5.x types were feat/fix(nazgul); only the test gate lands, so the
type is `test`):
```text
test(races): v2.0.28 - gate every character race against skins.xml

A character whose race no installed skins.xml registers breaks the
character load: v1.4.8 reads each NPCCharacter race through
FaceGen.GetRaceOrDefault, a plain dictionary index that throws
KeyNotFoundException on an unknown name. The races come from the
Armory, which is not versioned with TAOM, so an Armory update or
reinstall can drop one with no change in this repo.

EveryCharacterRaceIsARealRegisteredRace now checks every race spelled
in ModuleData XML and in the stylesheets against the installed Native
and Armory skins.xml. Stylesheets are parsed, every race is read
verbatim, and a race computed at transform time fails the gate. Both
install checks find the game through BANNERLORD_GAME_DIR before the
desktop path, so they run on the laptop; the older cultures.json check
was skipped there on every run.

Research: v1.4.8 TaleWorlds.MountAndBlade FaceGen.GetRaceOrDefault
  (FaceGen.cs:115-118), TaleWorlds.Core FaceGen.GetRaceOrDefault
  (FaceGen.cs:40-42), BasicCharacterObject.Deserialize (:323-328)
Rejected: reading skins.xml from every install root at once; a stale
  second install could register a race the live one lacks
Not-tested: nothing beyond the data test itself; the Nazgul race
  change the gate shipped with on 1.5.x is content and is not ported
Ported-from: 9804f67b9deba24813da1f2ad7ef2d5f9ad6fa14
Ported-from: 437d591112a74aab10a2880a5f60010cefd95278
```
Then, in the next Bash call: `cd /c/Users/mikew/source/repos/taom-port/126 && git commit -F C:/Users/mikew/source/repos/taom-port/msg-126-1.txt`

**Verify**: `git log -1 --format=%s` prints the subject above; `git status --porcelain` prints nothing.

### Step 2: Dwarf portraits no longer sit too low (`6c26f003a`, `git cherry-pick -n`, clean)

Source: `6c26f003a` "fix(heroRace): v2.0.34 - dwarf portraits no longer sit too low". The 1.4.x file
equals the 1.5.x pre-image (checked), and the simulation picks clean (exit 0). No test pins the
values; `RacePositionConfigValidator` accepts offsets in `[-10, 10]` (`MinOffset`/`MaxOffset`, :30-33).
No doc quotes the image values (the `taom.set_race_offset avatar dwarf 0 0.15 -0.1` example in
`hero-race.md` is the unchanged avatar surface).

```bash
cd /c/Users/mikew/source/repos/taom-port/126 && git cherry-pick -n 6c26f003a && git status --porcelain && grep -c '"Race":"dwarf","Horizontal":0.0,"Vertical":0.3,"Zoom":-0.2' Main/_Module/ModuleData/configs/CharacterImagePatch.json
```
Expected: exit 0; exactly `M  Main/_Module/ModuleData/configs/CharacterImagePatch.json`; `1`. Build,
then Filtered with `--filter "FullyQualifiedName~ShippedRacePositionConfigTests|FullyQualifiedName~RacePositionStoreTests"`:
`Failed: 0`.

Message file `msg-126-2.txt`:
```text
fix(heroRace): v2.0.28 - dwarf portraits no longer sit too low

Dwarf faces in the small 2D portraits (settlement notables, party and
battle screens) sat at the bottom edge of the frame, showing little
more than hair and eyes. The dwarf row in CharacterImagePatch.json
moves from Vertical 0.15, Zoom -0.1 to Vertical 0.3, Zoom -0.2, the
values tuned by eye on the 1.5.x line. The 3D preview offsets in
CharacterAvatarPatch.json are unchanged.

Not-tested: framing in game on v1.4.8 (maintainer check)
Ported-from: 6c26f003af0868fd10560e8b08380b277bd1aadf
```
Stage (`git add Main/_Module/ModuleData/configs/CharacterImagePatch.json`), then in the next Bash call
`git commit -F C:/Users/mikew/source/repos/taom-port/msg-126-2.txt`.

**Verify**: commit made; `git status --porcelain` prints nothing.

### Step 3: Dead heroes keep their race across a load (`f981629cd`, `git cherry-pick -n` with resolution)

Source: `f981629cd` "fix(hero-race): v2.0.35 - dead heroes keep their race across a load" (#1011).

Simulation (`git merge-tree` onto `1b00d1acd`, and again onto a synthetic base carrying WP104's
`a0db0558b` plus WP119's `6ae924638` and `eb5cf0289`): both give the same conflict set,
`Main/Features/HeroRace/RacePersistenceService.cs`, `TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs`,
`docs/features/hero-race.md`, `docs/reviews/lessons/state-lifecycle-save.md`,
`docs/reviews/lessons/testing-qa.md`. Cause: the 1.5.x pre-image carries #644 code that 1.4.x lacks
(a `keptWraiths` counter beside the restore call, and four wraith tests at the end of the test
class). The adapter, its interface and `HeroRaceWiringTests.cs` merge cleanly; the pick also adds
`docs/reviews/rca-b09-followups-2026-10-26.md` (bookkeeping). Resolution: keep the clean code, reset
the five conflicted paths to HEAD, and re-apply the 1.4.x share by script, so the result does not
depend on whether WP119 landed (WP119's own test lines, if present, also get the rename).

3.1 **Pick, then drop what does not belong.**
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && git cherry-pick -n f981629cd; git status --porcelain
```
Expected: the pick reports conflicts (a non-zero exit is normal here) only within the five paths
above; the planning simulation conflicted all five. A conflicted path outside those five is a STOP.
Then:
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && git checkout HEAD Main/Features/HeroRace/RacePersistenceService.cs TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs docs/features/hero-race.md docs/reviews/lessons/state-lifecycle-save.md docs/reviews/lessons/testing-qa.md && git rm -q -f docs/reviews/rca-b09-followups-2026-10-26.md && git status --porcelain
```
Expected exactly: `M  Main/Adapters/HeroRosterAdapter.cs`, `M  Main/Adapters/IHeroRosterAdapter.cs`,
`M  TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs`. Check the clean half:
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && grep -c 'Hero.FindAll\|Hero.FindFirst' Main/Adapters/HeroRosterAdapter.cs; grep -c 'AllAliveHeroes' Main/Adapters/HeroRosterAdapter.cs; grep -c 'IReadOnlyList<HeroRaceInfo> GetAllHeroRaces();' Main/Adapters/IHeroRosterAdapter.cs; grep -c 'HeroRosterAdapter_ReadsEveryHeroCharacterThroughHeroFindAll' TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs
```
Expected `4`, `0`, `1`, `1`. The adapter now reads `Hero.FindAll(_ => true)` and the two lookups use
`Hero.FindFirst(h => h.StringId == heroStringId)`, verbatim from `f981629cd`; keep that text exactly
(WP139 ports later commits whose context is these lines).

3.2 **Rename every remaining caller and add the capture comment.**
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && python -I - <<'PY'
import subprocess
from pathlib import Path
files = subprocess.run(["git", "grep", "-l", "GetAllAliveHeroRaces", "--", "Main", "TAOM.Tests"],
                       capture_output=True, text=True).stdout.split()
for f in files:
    p = Path(f)
    p.write_bytes(p.read_bytes().replace(b"GetAllAliveHeroRaces", b"GetAllHeroRaces"))
    print("renamed in", f)
p = Path("Main/Features/HeroRace/RacePersistenceService.cs")
raw = p.read_bytes().decode("utf-8")
nl = "\r\n" if "\r\n" in raw else "\n"
target = "        var heroes = _heroRosterAdapter.GetAllHeroRaces();"
assert raw.count(target) == 2, raw.count(target)
comment = "        // #1011: the roster holds alive AND dead or disabled heroes; the engine resets both on load."
assert comment not in raw
p.write_bytes(raw.replace(target, comment + nl + target, 1).encode("utf-8"))
print("capture comment added")
PY
```
Expected output: `renamed in Main/Features/HeroRace/RacePersistenceService.cs`,
`renamed in TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs` (plus any file an earlier WP
added; name each in your report), then `capture comment added`. The first `var heroes` line is in
`CaptureHeroRaces`; that is where the comment goes, as on 1.5.x. Do not add the 1.5.x `keptWraiths`
counter, the `INazgulRegistry` field or its constructor parameter.

**Verify**: `git grep -n GetAllAliveHeroRaces -- Main TAOM.Tests; echo "exit $?"` prints only `exit 1`.

3.3 **Add the six #1011 service tests** (lines 814-854 and 876-961 of the `f981629cd` test file: the
`// --- #1011` comment and every new test except `RestoreHeroRaces_DeadWraith_KeepsTheXmlRace`, which
needs the #644 `INazgulRegistry` this line does not have):
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && python -I - <<'PY'
import subprocess
from pathlib import Path
src = "TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs"
blob = subprocess.run(["git", "show", "f981629cd:" + src], capture_output=True, check=True).stdout.decode("utf-8")
b = blob.split("\n")
block = b[813:854] + b[875:961]
assert b[814].startswith("    // --- #1011: dead and disabled heroes."), b[814]
assert sum(1 for l in block if l.strip() == "[TestMethod]") == 6
assert not any("_nazgul" in l or "IsWraith" in l for l in block)
p = Path(src)
raw = p.read_bytes().decode("utf-8")
nl = "\r\n" if "\r\n" in raw else "\n"
lines = raw.split(nl)
assert not any("CaptureHeroRaces_DeadHeroInRoster_StoresItsRace" in l for l in lines), "already inserted"
end = lines.index("}")
assert lines[end - 1] == "    }", lines[end - 1]
lines[end:end] = block
p.write_bytes(nl.join(lines).encode("utf-8"))
print("inserted", len(block), "lines before line", end + 1)
PY
```
Expected: `inserted 127 lines before line <n>` (the first column-0 `}` closes `RacePersistenceServiceTests`;
`RoundTripDataStore` follows it). The six tests are `CaptureHeroRaces_DeadHeroInRoster_StoresItsRace`,
`RestoreHeroRaces_DeadHeroRaceDiffers_SetsSavedRace`, `RestoreHeroRaces_DeadHeroAbsentFromOldSave_DoesNotSetRace`,
`RestoreHeroRaces_DeadHeroRemovedRaceName_SkipsAndWarns`, `RestoreHeroRaces_DeadHeroRaceAlreadyMatches_DoesNotSetRace`
and `CaptureHeroRaces_OneRaceLegend_KeepsPriorDeadEntries`. They use only members that exist on 1.4.x
(`RoundTripDataStore.LastSavedDict`/`NextLoadDict`/`NextLoadLegend`, the setup's three-race table, and
the restore warning text `no longer exists`).

**Verify**: `grep -c '\[TestMethod\]' TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs`
prints `RPS_BASE + 6`; `grep -c '_nazgul\|IsWraith' TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs`
prints `0`.

3.4 **Update the feature doc for 1.4.x** (the 1.5.x additions rewritten for this line: no #644
sentences, v1.4.8 cites, and the touched lines keep no long dash, as 1.5.x did):
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && python -I - <<'PY'
import datetime
from pathlib import Path
p = Path("docs/features/hero-race.md")
raw = p.read_bytes().decode("utf-8")
nl = "\r\n" if "\r\n" in raw else "\n"
today = datetime.date.today().isoformat()

def once(text, old, new):
    n = text.count(old)
    assert n == 1, (n, old[:70])
    return text.replace(old, new, 1)

a_old = "`SyncRaceData` also clears both fields when `dataStore.IsLoading` before syncing"
a_new = ("**Dead and disabled heroes are included (#1011).** On load, v1.4.8 "
         "`SandBoxManager.InitializeCharactersAfterLoad` (`SandBoxManager.cs:384-421`) sets the `Race` of every "
         "non-original hero character, alive or not, back to the race of its origin character "
         "(`BasicCharacterObject.cs:304`). `HeroRosterAdapter` therefore reads and writes every hero character "
         "through `Hero.FindAll` and `Hero.FindFirst` (both scan `Campaign.Characters`, `Hero.cs:2130-2145`): "
         "alive, dead, disabled, and dead heroes the engine dropped from `DeadOrDisabledHeroes`, with no "
         "de-duplication. A hero who died before #1011 was never captured, so a save written before it keeps the "
         "reverted race of that hero. Because the saved race now covers dead heroes too, a later XML race "
         "correction reaches a dead lord only through an explicit restore skip, as it already did for living "
         "lords. " + a_old)
raw = once(raw, a_old, a_new)
# The same paragraph line keeps no long dash, as 1.5.x f981629cd rewrote it:
raw = once(raw, "captures again \u2014 both halves", "captures again: both halves")
raw = once(raw, "or the module set changes \u2014 so restore translates", "or the module set changes, so restore translates")
raw = once(raw, "deliberately \u2014 a `Dictionary<string,string>`", "deliberately; a `Dictionary<string,string>`")
raw = once(raw, "- `RacePersistenceServiceTests.cs` \u2014 verifies that", "- `RacePersistenceServiceTests.cs`: verifies that")
raw = once(raw, "- `IHeroRosterAdapter` \u2014 iterates all alive heroes and sets race values",
           "- `IHeroRosterAdapter`: iterates every hero character through `Hero.FindAll` (`GetAllHeroRaces`, "
           "#1011) and sets race values")
e_old = "and `CaptureHeroRaces_EmptyRaceList_DoesNotOverwriteRicherCapturedData`."
raw = once(raw, e_old, e_old + " #1011 adds six: `CaptureHeroRaces_DeadHeroInRoster_StoresItsRace`, "
           "`RestoreHeroRaces_DeadHeroRaceDiffers_SetsSavedRace`, `_DeadHeroAbsentFromOldSave_DoesNotSetRace`, "
           "`_DeadHeroRemovedRaceName_SkipsAndWarns`, `_DeadHeroRaceAlreadyMatches_DoesNotSetRace`, and "
           "`CaptureHeroRaces_OneRaceLegend_KeepsPriorDeadEntries`. They feed the service a fake adapter, so they "
           "pin its treatment of dead entries; the adapter is pinned by "
           "`HeroRaceWiringTests.HeroRosterAdapter_ReadsEveryHeroCharacterThroughHeroFindAll`, which checks the "
           "API the source calls, not live collections.")
raw = once(raw, "## Changelog" + nl, "## Changelog" + nl + "- " + today + ": #1011 (port of 1.5.x "
           "`f981629cd`), dead and disabled heroes are saved and restored. The engine resets their race from the "
           "origin character on load just as it does for living heroes, but the roster adapter only read "
           "`Hero.AllAliveHeroes`. `GetAllAliveHeroRaces` is now `GetAllHeroRaces`. Heroes who died before this "
           "fix keep the reverted race." + nl)
p.write_bytes(raw.encode("utf-8"))
print("hero-race.md: 8 edits")
PY
```
**Verify**: prints `hero-race.md: 8 edits`;
`git diff -U0 -- docs/features/hero-race.md | grep '^+' | grep -v '^+++' | grep -c $'\xe2\x80\x94\|\xe2\x80\x93'`
prints `0`.

3.5 **Build and test.** Build, then Filtered with
`--filter "FullyQualifiedName~TAOM.Tests.Features.HeroRace|FullyQualifiedName~CharacterCreationContentServiceTests|FullyQualifiedName~NamedCompanionServiceTests|FullyQualifiedName~JoinReconciliationServiceTests"`,
then once each with `FullyQualifiedName~RacePersistenceServiceTests` and `FullyQualifiedName~HeroRaceWiringTests`.

**Verify**: build exit 0; all three runs `Failed: 0`; the service run totals `RPS_BASE + 6`; the wiring
run totals `HRW_BASE + 3` (the new test has three `[DataRow]` cases; MSTest 3.1.1 normally lists each.
If it shows `HRW_BASE + 1`, the runner folds data rows: note it, and use +12 instead of +14 in Step 5).

3.6 **Commit.**
```bash
cd /c/Users/mikew/source/repos/taom-port/126 && git add Main/Adapters/HeroRosterAdapter.cs Main/Adapters/IHeroRosterAdapter.cs Main/Features/HeroRace/RacePersistenceService.cs TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs docs/features/hero-race.md && git status --porcelain
```
Add any extra renamed file from 3.2 to that `git add`. Expected: those paths staged (`M `), nothing
else listed. Message file `msg-126-3.txt`:
```text
fix(hero-race): v2.0.28 - dead heroes keep their race across a load

A hero who died with a race different from the character they were
made from, such as a non-human player's child, came back with that
character's race after a save and load. The encyclopedia then showed
the wrong race, and a child born after that parent's death could
inherit the wrong race, with the wrong lifespan and abilities.

On load v1.4.8 resets the race of every non-original hero character,
dead and disabled ones included, but TAOM saved and restored only the
living. The hero roster now reads every hero character, so dead heroes
keep their race. Heroes who died before this build cannot be
recovered: their race was never saved.

Research: v1.4.8 SandBoxManager.InitializeCharactersAfterLoad
  (SandBoxManager.cs:384-421), BasicCharacterObject
  .InitializeHeroBasicCharacterOnAfterLoad (:297-313), Hero.FindFirst
  and FindAll (Hero.cs:2130-2145), Campaign.Characters (:503),
  ContainerSaveData.CollectChildren (one container per dictionary)
Save-compat: same save key; saves now also list dead heroes, and an
  older build ignores the extra entries
Not-tested: a dead hero's race across a real save and load, and save
  time on a long campaign (maintainer checks)
Ported-from: f981629cd86f34c2ea907c768a586e224adbd4ab
```
Then, in the next Bash call, `git commit -F C:/Users/mikew/source/repos/taom-port/msg-126-3.txt`. If
`git status` then reports a cherry-pick in progress, run `git cherry-pick --quit`.

**Verify**: `git status --porcelain` prints nothing; `git show --stat HEAD` lists the six paths (plus
any extra renamed file) and no `docs/reviews` path.

### Step 4: Call the session-launch capture a backup (`0f7902684`, adapted by hand)

Source: `0f7902684` "docs(hero-race): v2.0.35 - co-op race restore checked, no change" (#1022). Its
simulation conflicts only in `docs/features/coop-interop.md`, but its clean hunks would write, as
fact on this line, the co-op module's behaviour read for the 1.5.x module build (`SaveInterface.cs:109`).
Co-op is unsupported on 1.4.x (D7) and no co-op build for v1.4.8 is available here to re-check it, so
this step withdraws the old "never raises" claim without asserting the new one. Not taken: the long
co-op paragraph for `hero-race.md` and the `coop-interop.md` owed item 15.

```bash
cd /c/Users/mikew/source/repos/taom-port/126 && python -I - <<'PY'
import datetime
from pathlib import Path
today = datetime.date.today().isoformat()

def edit(path, pairs):
    p = Path(path)
    raw = p.read_bytes().decode("utf-8")
    nl = "\r\n" if "\r\n" in raw else "\n"
    for old_lines, new_lines in pairs:
        old, new = nl.join(old_lines), nl.join(new_lines)
        n = raw.count(old)
        assert n == 1, (path, n, old_lines[0][:60])
        raw = raw.replace(old, new, 1)
    p.write_bytes(raw.encode("utf-8"))
    print("edited", path)

edit("Main/Features/HeroRace/RacePersistenceBehavior.cs", [(
    ["    /// The capture half exists for save transfers that never raise <c>OnBeforeSaveEvent</c> \u2014 a co-op",
     "    /// host handing its world to a joining client does exactly that (multiplayer field report",
     "    /// 2026-08-03 \u00a71), so without capturing here the race map is empty at serialization time and the",
     "    /// joiner receives a world with no race data at all."],
    ["    /// The capture half was added because a co-op host handing its world to a joining client was",
     "    /// believed to skip <c>OnBeforeSaveEvent</c> (multiplayer field report 2026-08-03 \u00a71), which would",
     "    /// leave the race map empty at serialization time. A 1.5.x review (#1022) read the co-op module",
     "    /// transfer save raising it (SaveInterface.cs:109); that was not re-checked for a v1.4.8 co-op",
     "    /// build, so this capture stays as a backup for any transfer path that skips the event."])])

edit("TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs", [(
    ["        // A co-op host's save transfer to a joining client never raises OnBeforeSaveEvent, so without",
     "        // a capture here the race map is empty when the world is serialized and the joiner receives",
     "        // no race data at all."],
    ["        // The capture is a backup for a save transfer that skips OnBeforeSaveEvent (a co-op host",
     "        // transfer was believed to; see RacePersistenceBehavior.OnSessionLaunched), where the race",
     "        // map would otherwise be empty when the world is serialized."])])

edit("docs/features/hero-race.md", [
    (["A multiplayer host handing its world to a joining client never raises that event, so a joiner received "
      "a world with no race data at all \u2014 the map was empty at serialization time."],
     ["A co-op host handing its world to a joining client was believed to skip that event, which would leave "
      "the map empty at serialization time and give the joiner a world with no race data at all. A 1.5.x review "
      "(#1022) read the co-op module transfer save raising it; co-op is unsupported on this line and that was not "
      "re-checked for a v1.4.8 co-op build, so the session-launch capture stays as a backup for any transfer that "
      "skips the event."]),
    (["|       the trailing capture covers save transfers that never raise OnBeforeSaveEvent"],
     ["|       the trailing capture is a backup for a save transfer that skips OnBeforeSaveEvent"]),
    (["## Changelog"], ["## Changelog",
      "- " + today + ": the session-launch capture is described as a backup for a save transfer that skips "
      "`OnBeforeSaveEvent` (port of 1.5.x `0f7902684`, #1022, without its co-op module findings, which were not "
      "re-checked for a v1.4.8 co-op build). Comments and docs only."]),
])
PY
```
**Verify**: three `edited ...` lines; the dash check from 3.4 prints `0` again; Build exit 0; Filtered
`--filter "FullyQualifiedName~RacePersistenceBehaviorTests"` `Failed: 0` with the same total as before
this step.

Stage `Main/Features/HeroRace/RacePersistenceBehavior.cs TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs docs/features/hero-race.md`
(and confirm with `git status --porcelain`). Message file `msg-126-4.txt`:
```text
docs(hero-race): v2.0.28 - the session-launch race capture is a backup

The race restore's session-launch capture was documented as covering
a co-op save transfer that never raises the save event. A review on
the 1.5.x line found the co-op module's transfer save does raise it.
Co-op is not supported on this line and that was not re-checked for a
v1.4.8 co-op build, so the comment, its test comment and the hero race
doc now call the capture a backup for any transfer that skips the
event. No code change.

Not-tested: none (comments and docs only)
Ported-from: 0f7902684ae00959b3cff49f53710025159af175
```
Then, in the next Bash call, `git commit -F C:/Users/mikew/source/repos/taom-port/msg-126-4.txt`.

**Verify**: commit made; `git status --porcelain` prints nothing.

### Step 5: Wrap up

1. Full Build and Test. **Verify**: `Failed: 0`; total = `BASE_TOTAL + 14` (or `+ 12` if Step 3.5
   showed folded data rows); skipped = `BASE_SKIPPED - 1` (the cultures.json install check now runs).
2. `python tools/validate_moduledata.py` (about 40 s). **Verify**: no ERROR that the WP101 baseline in
   the main checkout's `plans/README.md` does not list (this plan changes no XML).
3. `python tools/lint_docs.py`. **Verify**: no finding names a line this plan added to
   `docs/features/hero-race.md` or `.claude/rules/xslt.md`.
4. Machine checks:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/126 && \
   git log --format=%s "$BASE"..HEAD; \
   git diff --name-only "$BASE"..HEAD; \
   git log --format=%B "$BASE"..HEAD | grep -c $'\xe2\x80\x94\|\xe2\x80\x93'; \
   git diff "$BASE"..HEAD -U0 -- docs .claude | grep '^+' | grep -v '^+++' | grep -c $'\xe2\x80\x94\|\xe2\x80\x93'; \
   git grep -n 'GetAllAliveHeroRaces' -- Main TAOM.Tests; echo "exit $?"; \
   git grep -n 'Hero.AllAliveHeroes' -- Main/Adapters/HeroRosterAdapter.cs Main/Features/HeroRace; echo "exit $?"; \
   git grep -n '_nazgul\|IsWraith\|INazgulRegistry' -- Main/Features/HeroRace TAOM.Tests/Features/HeroRace; echo "exit $?"; \
   git diff --quiet "$BASE" HEAD -- Main/_Module/ModuleData/lords.xslt Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj CHANGELOG.md && echo UNTOUCHED
   ```
   Replace `$BASE` with the sha from Step 0. **Verify**: the four subjects of Steps 1 to 4, each with
   `v2.0.28 - ` and at most 72 characters; the 11 paths listed under "Done criteria" (plus any extra
   renamed file); `0`; `0`; `exit 1` three times (the new wiring test names `Hero.AllAliveHeroes` in
   its assertion text, which is why that grep stays out of `TAOM.Tests`); `UNTOUCHED`.
5. Hand-off: do not edit `plans/README.md`. Report to the integrator: status, the branch
   `port148/126-race-and-hero-fixes`, the four commit hashes (`git log --format='%H %s' "$BASE"..HEAD`),
   the test counts (total, passed, skipped), the validator result, and any extra renamed file from
   Step 3.2. The integrator records them on the plan 126 row after the merge; no old-plan row needs
   updating for this WP. Delete the `C:\Users\mikew\source\repos\taom-port\msg-126-*.txt` files
   afterwards.

## Disposition (every commit in the pack; no shared hunks were assigned to this WP)

| 1.5.x commit | Subject | Disposition | Detail |
|---|---|---|---|
| `9804f67b9deba24813da1f2ad7ef2d5f9ad6fa14` | feat(nazgul): v2.0.30 - the Nine become race nazghul (#644) | adapted (partial), Step 1 | Taken: the `CultureRaceConsistencyTests` hunk (`RegisteredRaces` helper, `EveryCharacterRaceIsARealRegisteredRace`), with the skins.xml lookup moved to `BANNERLORD_GAME_DIR` first and engine cites moved to v1.4.8. Skipped as content (OUT): `lords.xslt` and `characters/lords.xml` race, age and kit edits; DreadAura, UncapturableHeroes and CombatMechanics race lists, configs and tests (`ShippedCombatMechanicsConfigTests` among them); the `RacePersistenceService` wraith skip, `INazgulRegistry` injection and their tests; the `INazgulRegistry` comment; `NazgulRaceDataTests`; the `.claude/rules/tests.md` line (it names `NazgulRaceDataTests`); every doc hunk (character-creation, dread-aura, hero-race #644 text, lord-identity-reconciliation, nazgul-family, player-switcher, uncapturable-heroes, modding docs, Armory snapshot README). Skipped as bookkeeping: `CHANGELOG.md` (D1), `feature-map.md`, lessons, the RCA. The `lord_1_48_1/2/3` double definition it repairs exists on 1.4.x: follow-up in Maintenance notes (D13) |
| `437d591112a74aab10a2880a5f60010cefd95278` | fix(nazgul): v2.0.30 - Codex review follow-ups for #644 and #645 | adapted (partial), Step 1 | Taken: `RacesEmittedByXslt`, its four self-tests and the computed-race assertion in `CultureRaceConsistencyTests`; the `.claude/rules/xslt.md` paragraph. Skipped: the SignatureStrikes provider, sound player, test and doc (feature absent on 1.4.x); the `RacePersistenceService` comment and `hero-race.md` sentence (they belong to the #644 race change); `CHANGELOG.md`, REVIEW-LOG, LESSONS-LEARNED, codex-track-record, the Codex prompt, lessons and RCAs (bookkeeping) |
| `6c26f003af0868fd10560e8b08380b277bd1aadf` | fix(heroRace): v2.0.34 - dwarf portraits no longer sit too low | ported, `git cherry-pick -n`, clean, Step 2 | One JSON line; framing check owed in game |
| `f981629cd86f34c2ea907c768a586e224adbd4ab` | fix(hero-race): v2.0.35 - dead heroes keep their race across a load | ported with resolution, Step 3 | Taken: adapter and interface (verbatim), `HeroRaceWiringTests` test (verbatim), the service rename and capture comment in the 1.4.x shape (no wraith skip), the renamed service tests plus six of the seven new tests (`RestoreHeroRaces_DeadWraith_KeepsTheXmlRace` needs #644's `INazgulRegistry`), `hero-race.md` minus its #644 sentences. Skipped: two lessons files and `rca-b09-followups-2026-10-26.md` (bookkeeping) |
| `0f7902684ae00959b3cff49f53710025159af175` | docs(hero-race): v2.0.35 - co-op race restore checked, no change | adapted (partial), Step 4 | Taken as rewording: the behaviour comment, its test comment and two `hero-race.md` lines stop claiming a co-op transfer never raises `OnBeforeSaveEvent`, without asserting the 1.5.x co-op module findings. Skipped: the `hero-race.md` co-op paragraph and the `coop-interop.md` owed item 15 (D7; the co-op module build used with 1.4.8 is not available to verify `SaveInterface.cs:109`) |

## Test plan

- New in `TAOM.Tests/Core/CultureRaceConsistencyTests.cs` (5): `EveryCharacterRaceIsARealRegisteredRace`
  (every race in ModuleData XML and XSLT against the installed Native and Armory skins.xml),
  `RacesEmittedByXslt_XslAttributeText_IsReadInEverySpelling`, `RacesEmittedByXslt_PaddedRace_IsReadVerbatim`,
  `RacesEmittedByXslt_LiteralResultElement_IsRead` and `RacesEmittedByXslt_RaceBuiltAtTransformTime_IsUnverifiable`
  (scanner self-tests, game-free). Changed: `EveryAllowedRaceIsARealRegisteredRace` now finds the
  install through `BANNERLORD_GAME_DIR` and runs on the laptop. The Step 1.3 probe shows the gate fails
  on an unregistered race and names the stylesheet.
- New in `TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs` (6): the dead-hero capture, restore,
  absent-from-old-save, removed-race, already-matching and one-race-table cases listed in Step 3.3.
  Structural pattern: the file's existing NSubstitute fakes plus `RoundTripDataStore`.
- New in `TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs` (1 method, 3 data rows):
  `HeroRosterAdapter_ReadsEveryHeroCharacterThroughHeroFindAll` reads the comment-stripped adapter
  source through `RepoPaths.ReadSource` and requires `Hero.FindAll(`/`Hero.FindFirst(` and no
  `Hero.AllAliveHeroes`/`Hero.DeadOrDisabledHeroes` in each of the three members.
- Untestable here (commit `Not-tested:` trailers, In-game checks): the live contents of
  `Hero.FindAll` and the engine's reset on a real load; portrait framing; save time.
- Verification: the Test command, `Failed: 0`, total `BASE_TOTAL + 14` (`+ 12` if data rows fold).

## Done criteria

- [ ] Build exits 0; the Test command reports `Failed: 0`, total `BASE_TOTAL + 14` (or `+ 12`, as
      Step 3.5 established) and skipped `BASE_SKIPPED - 1`.
- [ ] Filtered `CultureRaceConsistencyTests`: `Passed: 7`, `Skipped: 0`, `Total: 7`.
- [ ] `git diff --name-only BASE..HEAD` lists exactly: `.claude/rules/xslt.md`,
      `Main/Adapters/HeroRosterAdapter.cs`, `Main/Adapters/IHeroRosterAdapter.cs`,
      `Main/Features/HeroRace/RacePersistenceBehavior.cs`, `Main/Features/HeroRace/RacePersistenceService.cs`,
      `Main/_Module/ModuleData/configs/CharacterImagePatch.json`, `TAOM.Tests/Core/CultureRaceConsistencyTests.cs`,
      `TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs`, `TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs`,
      `TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs`, `docs/features/hero-race.md`
      (plus any reported extra renamed file).
- [ ] `git log --format=%s BASE..HEAD` shows four subjects, each with
      `v2.0.28 - ` and at most 72 characters; no body line holds an em or en dash.
- [ ] `git grep -n GetAllAliveHeroRaces -- Main TAOM.Tests` prints nothing; `grep -c AllAliveHeroes Main/Adapters/HeroRosterAdapter.cs` prints `0`.
- [ ] `grep -c '"Vertical":0.3,"Zoom":-0.2' Main/_Module/ModuleData/configs/CharacterImagePatch.json` prints `1`.
- [ ] `grep -cF 'E:\Steam' TAOM.Tests/Core/CultureRaceConsistencyTests.cs` prints `1` (the `DefaultGameDir` constant).
- [ ] `Main/_Module/ModuleData/lords.xslt`, `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`,
      `CHANGELOG.md` and `plans/README.md` are unchanged since `BASE`; `git status --porcelain` prints nothing.
- [ ] `python tools/validate_moduledata.py` shows no ERROR outside the WP101 baseline.
- [ ] The hand-off report carries the status, the four commit hashes and the measured test counts (Step 5).

## STOP conditions

Stop and report (do not improvise) if:

- Step 0.2: `Main/Composition/FeatureModules.cs` or `TAOM.Tests/Infrastructure/RepoPaths.cs` is
  missing, `RepoPaths.ReadSource(string, bool)` is not found, or `HeroRaceWiringTests.cs` lacks
  `using TAOM.Tests.Infrastructure;` (WP104/WP105 not merged).
- The installed Armory reads `v2.0.26` (WP101 Part A, D20, not done) or `BANNERLORD_GAME_DIR` is empty
  (environment; report it, never set it).
- `GetAllHeroRaces`, `EveryCharacterRaceIsARealRegisteredRace` or `RacesEmittedByXslt` already exists,
  or `HeroRosterAdapter.cs` already uses `Hero.FindAll`/`FindFirst`: an earlier WP took part of this
  plan; the orchestrator must re-scope.
- `CharacterImagePatch.json`, `HeroRosterAdapter.cs` or `IHeroRosterAdapter.cs` differ from `1b00d1acd`,
  or `CultureRaceConsistencyTests.cs` differs by anything other than added `[TestCategory(...)]` lines.
- A Step 0.3 anchor count is not `1`, or an edit script's assertion fails.
- Step 1.2 leaves the install checks skipped (the test host does not see `BANNERLORD_GAME_DIR`), or
  `EveryCharacterRaceIsARealRegisteredRace` or `EveryAllowedRaceIsARealRegisteredRace` FAILS on the
  real data: report the unknown races it lists. That would mean the installed public Armory lacks a
  race 1.4.x data uses (a player-facing load failure); do not edit data, skins.xml or the test.
- The Step 1.3 probe does not make the gate fail, or the probe edit cannot be reverted cleanly.
- `git cherry-pick -n 6c26f003a` conflicts, or `git cherry-pick -n f981629cd` conflicts in any path
  other than the five listed in Step 3.
- After Step 3 a test outside the touched classes fails (for example the adapter consumers
  `CharacterCreationContentServiceTests`, `NamedCompanionServiceTests`, `JoinReconciliationServiceTests`);
  do not edit those tests.
- A step's verification fails twice after a reasonable fix attempt, or the Step 5 test total differs
  from the expected delta.
- The work appears to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `CHANGELOG.md` or
  any out-of-scope path, or a hook blocks a commit asking for `CHANGELOG.md` (D1: never stage it).
- The build reports `Hero.FindAll`, `Hero.FindFirst` or any engine member above as missing: the
  installed engine differs from the v1.4.8 signatures in "Current state".

## In-game checks owed (maintainer, after the integrator deploys a build that contains this WP)

1. **Dwarf portraits.** Open a settlement with dwarf notables, the party screen with a dwarf hero, and
   a battle that shows dwarf portraits. Look for: the face framed (eyes, nose and beard visible), not
   only hair and eyes at the bottom edge. In the dev console, `taom.print_race_offsets image` should list
   the dwarf row as Horizontal 0, Vertical 0.3, Zoom -0.2. If a `[TableauDiag]` "Spawner frame:
   race='dwarf'" line appears in the session's taom_debug log, it should read `z=-0.2 v=0.3`.
2. **Dead heroes keep their race.** On a save that holds a dead hero created at runtime with a race
   different from its origin character (most likely a dead child of a non-human player), save with this
   build, quit to the main menu, load, and check that hero's race in the encyclopedia. The taom_debug
   log prints `RacePersistenceService: Restored race for N heroes.` on each load. A hero who died before
   this build was never captured and keeps the reverted race; that is expected. Where possible also
   check that a child born after its parent's death takes that parent's race.
3. **Save time.** On one long-campaign save, compare the rgl_log save timings
   (`SaveContext::CollectSaveDataForObject::Containers` and the total) for the same save with a build
   before this WP and with it. Expected: no measurable change (the race map is one dictionary
   container). A clear increase goes to WP114 with both numbers.

## Maintenance notes

- **WP139 depends on this text.** Its `e4dbdf9c4`, `40006d078` and `75c0c27a3` (race-persist check lines)
  were written on top of `f981629cd`: their hunks use the adapter's `Hero.FindAll(_ => true)` body and
  #1011 comment, `IHeroRosterAdapter.GetAllHeroRaces`, and the new `HeroRaceWiringTests` test as
  context. Keep those verbatim.
- **WP119 ordering.** If WP119 merges before this WP, its `eb5cf0289` test line must call
  `GetAllAliveHeroRaces` (this WP's Step 3.2 renames it). If WP119 merges after, it can keep the 1.5.x
  spelling `GetAllHeroRaces`. Either way `ResetForNewSession` and the `_isNewCampaign` flag are WP119's.
- **WP133** (`30003668b`, liege titles) conflicts on `CultureRaceConsistencyTests.cs`; its port notes
  leave the #956 refactor and the facegen test out, so it should keep this WP's file.
- **WP122**: plan 010's `LiveInstall`/`RequiresGame` tags are not ported (D9), so
  `CultureRaceConsistencyTests` carries neither tag even though it reads the install.
- **WP112's hollow-parent repair** (`1165a00de`, `de90a2451`) keeps the race `RacePersistenceService`
  restored at session launch; after this WP that restore also covers dead parents.
- **The saved race now covers dead heroes.** A later XML race correction to a lord who is dead in a
  save is overwritten by the saved race on load, exactly as for living lords; ship such a correction
  with an explicit restore skip (1.5.x's #644 `INazgulRegistry.IsWraith` skip is the model).
- **Follow-up for the orchestrator (D13, content decision).** The double definition #644 repaired on
  1.5.x exists on 1.4.x: `lord_1_48_1/2/3` are rows in `Main/_Module/ModuleData/characters/lords.xml`
  (:24935, :24986, :25037; race `uruk`, age 20) and templates in `lords.xslt` (:6107, :6162, :6217). The
  1.5.x research (on v1.5.3, UNVERIFIED on v1.4.8 here) says the engine merges a second definition per
  attribute and unions EquipmentSets, so those three may draw a random kit per campaign. Deleting the
  rows alone would turn the three human; WP134 Step C4 fixes it: today's effective race and age move into the templates.
- **Reviewer focus** (the orchestrator runs `/deep-review`: C# in more than two files): the adapter's
  `Hero.FindAll`/`FindFirst` stay inside `Main/Adapters` (ADR-007); the rename left no
  `GetAllAliveHeroRaces` and no `[Obsolete]` shim (ADR-004); the six tests match the 1.4.x service (no
  wraith logic); the `RegisteredRaces` lookup order; the probe never reached a commit; the reworded
  comments assert nothing about the 1.4.8 co-op module. No `/verify-bindings` run is needed: no Harmony
  target or reflection site changes (`Hero.FindAll`/`FindFirst` bind at compile time).
- `Hero.FindFirst` scans `Campaign.Characters` linearly per `SetHeroRace`. The restore calls it only for
  heroes whose race differs from the saved one (mostly the player's family), so the cost is small; a
  campaign with thousands of such heroes would be the case to measure.
