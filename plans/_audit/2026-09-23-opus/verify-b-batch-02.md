# Verify batch B-02: adversarial check of TEST-L5-05, TEST-L5-06, TEST-L5-08 (lane 5)

Checker: fresh adversarial pass, read-only. Source reads are against `b2e387db` through `git show` or
`git grep <pattern> b2e387db -- <path>` (HEAD has moved to `4b5662b2`; the working tree belongs to
another session). No build and no test run. The lane's recorded probe artifacts in the session
scratchpad (`lane5-probe\results\*.trx`, `raw\tests.trx`) were re-parsed with my own scripts
(`vb02_trx.py`, `vb02_cls.py`), and the lane's `diff.py` was re-run on them.

## TEST-L5-05: take the live-Armory tests out of the unit gate

**Outcome: CONFIRMED, with corrected counts and one sub-claim that does not reproduce. Impact today: LOW.**

- **Holds, the core mechanism**: the committed test pins data that lives only in the unversioned
  install. `TAOM.Tests/Features/Elk/ElkConfigTests.cs:93-112` (at `b2e387db`) reads
  `LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` from `BANNERLORD_GAME_DIR` or the
  hard-coded `E:\Steam\...` fallback (`:101`) and expects `body_length` equal to
  `ElkConfig.AuthoredScale * 100`; `Main/Features/Elk/ElkConfig.cs:38` at `b2e387db` is `2.0f`, so 200.
  The live file (mtime 2026-09-23 17:54, before the 18:30 baseline run) now holds `body_length="100"`
  for `taom_elk_a` (read directly). The baseline trx (`raw\tests.trx`) has exactly 2 failures, both in
  this family: `ElkConfigTests.TheElkItem_DeclaresTheScaleTheReachIsTunedFor` (Expected 200, Actual 100)
  and `AnimaliaMountWiringTests.AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`. So a
  checkout of `b2e387db` is red or green by install state, as claimed.
- **Correction 1, the count**: `git grep -n -i "LOTRLOME_Armory" b2e387db -- TAOM.Tests` plus a read of
  each hit gives **9** files that read the Armory at runtime, not 10: `Core/CultureRaceConsistencyTests.cs`,
  `Features/Animalia/AnimaliaMountWiringTests.cs`, `Features/BannerBearers/BannerBearerReplacementWeaponDataTests.cs`,
  `Features/Elephant/{HowdahCrewLoadout,HowdahHarnessItem,HowdahPrefab,LegacyHowdahPrefab}Tests.cs`,
  `Features/Elk/ElkConfigTests.cs`, `Features/Mumakil/MumakilPlatformTests.cs`. The lane's own
  Measurement 3 list names these same 9. Of them, **8** carry an `E:\Steam` literal (`git grep -F
  'E:\Steam'`); `BannerBearerReplacementWeaponDataTests.cs:40,45` uses `GameAssemblies.GameDir` and is
  already `[TestCategory("BindingVerification")]` (`:36`). The tenth file is
  `Core/LordFamilyTransformTests.cs:32,54`, which reads the **vanilla** SandBox install with an
  `E:\Steam` fallback. Counted as "install-coupled" (which is what the proposed `LiveInstall` tag means)
  the set is 10 files with 9 `E:\Steam` literals, so the fix sketch's scope is right; calling all 10
  "Armory readers" is the mis-citation. The others that mention the Armory (`WarRamConfigTests`,
  `ElkMountWiringTests`, `FieldCommissionConfigProviderTests`, `BannerBearerServiceTests`,
  `CoopVetoClassificationTests`) only mention it in comments or read in-repo files.
- **Correction 2, the fallback wording**: `CultureRaceConsistencyTests.cs:123-124` is not a fallback: it
  hard-codes the full `E:\Steam\...\skins.xml` paths and never reads `BANNERLORD_GAME_DIR`.
- **Holds, "all new since `141b749`"**: none of the 10 exists at `141b749` (`git cat-file -e`); first
  added between 2026-07-25 (`9758eb22`) and 2026-09-23 (`c79a5852`).
- **Does not reproduce, the "different message in run A" sub-claim**: in `probeA_envset.trx` (run A)
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` fails with the byte-identical message of
  the baseline ("as_animalia_elk: act_animalia_elk_antler is not an as_horse action, so nothing fires
  it"). The extra Animalia failure in run A, `TestRiders_RideTheirAnimaliaMount_WithTheElkSaddle`, is a
  layout artifact (`DirectoryNotFoundException` for `Main\_Module\ModuleData\troops\troops_animalia_test.xml`
  resolved from the copied bin), not Armory drift. Not load-bearing: the Elk case above proves the
  install dependence on its own.
- **Nuance the finding understates**: on this desktop the tests never skip. `probeB_envunset.trx`
  (`BANNERLORD_GAME_DIR` unset) still runs and fails both, because the `E:\Steam` fallbacks find the
  install. "Missing means Skipped" applies only to a machine without `E:\Steam`.
- **By-design check**: no ADR, rule or trap-index row asks tests to read the live install. The trap
  index row "Unversioned modules ... land an in-repo gate with it" is honoured by the fix, which keeps
  the tests and runs them as a labelled step. `.claude/rules/tests.md` (the transform section) already
  prefers a synthetic stub "so the test is deterministic and needs no game install". Only one category
  exists repo-wide today (`BindingVerification`, 290 attributes), so `LiveInstall` is new.
- **Why LOW today**: no player impact; the cost is an ambiguous local red. Part of that red is a real
  signal (committed `AuthoredScale = 2.0` against a shipped Armory at 100), which is why the finding's
  "run it as a labelled step, do not drop it" condition matters.

## TEST-L5-06: tag `RequiresGame` from the measurement, not from `using` lines

**Outcome: CONFIRMED (the measurement reproduces from the recorded trx), with two corrections to the
fix sketch. Impact today: LOW (nothing consumes the tag until hosted CI, TEST-L5-07, exists).**

- **Reproduced**: re-running the lane's `diff.py` on `results\f2_layout_envunset.trx` (real DLLs,
  variable unset) against `results\ci_RefAsm.trx` (BUTR stubs, variable unset) prints 1,307 new
  failures, 100 classes, 92 files, 1,301 of them `NullReferenceException`; my output file is
  byte-identical to `lane5-probe\requiresgame_refasm.txt`. Both runs have the variable unset, so the
  diff isolates engine-body execution from the `GameAssemblies` skip, as claimed.
- **Reproduced, the proxy comparison**: `git ls-tree -r --name-only b2e387db -- TAOM.Tests | grep -c '\.cs$'`
  is 753; `git grep -l -E '^\s*using\s+(static\s+)?(TaleWorlds|SandBox)' b2e387db -- 'TAOM.Tests/*.cs'`
  is 102 files, identical to the lane's `imports_tw.txt`. Crossing with the 92: 57 in both, 35 need the
  game with no import, 45 import with no need (`comm`). So a `using`-based tag misses 35 and
  over-excludes 45, exactly as stated.
- **Reproduced, the stub mechanism (partly)**: the Core nuspec in `lane5-probe\butr\core.nupkg` is
  `Bannerlord.ReferenceAssemblies.Core 1.5.3.122374-beta`, description "stripped metadata-only", 50 DLLs
  under `ref/net472/`; `TaleWorlds.Library.ref.dll` contains no `ReferenceAssemblyAttribute` string
  (grep count 0), consistent with "they load". The "every body is `ldnull; throw`" claim I did not
  re-disassemble; the 1,301 NRE results are the recorded consequence.
- **Correction 1, the tag unit**: the fix sketch says "attribute on 92 classes"; it is **100 classes in
  92 files**. The one odd row in the list (`?TAOM.Tests.Infrastructure.Dependencies.AssemblyRedirectListTests`)
  is only a mapping miss in `diff.py`; the class is `TAOM.Tests/Infrastructure/Dependencies/AssemblyRedirectListTests.cs:35`.
- **Correction 2, class-level over-exclusion**: tagging at class level also drops the results in those
  classes that pass on stubs. Measured with `vb02_cls.py` on the same two trx files: the 100 classes
  hold 1,598 results on the stub run, of which **288 pass**. Class-level tagging therefore removes
  about 288 runnable results from CI (about 18% of what it excludes); method-level tagging avoids that
  at the cost of about 1,307 attributes. A trade-off for the plan, not a refutation.
- **Overclaim in the risk line, with a concrete counter-example**: "a test that starts touching engine
  code without the tag goes red on CI, never green" does not hold. 6 of the 1,307 new failures are not
  NREs (`vb02_nonnre.py`): 5 in `PlayerClanLeadershipServiceTests` are NSubstitute `ReceivedCallsException`
  or `Assert.IsTrue` failures, because `Main/Features/PlayerSwitcher/PlayerClanLeadershipService.cs:57,96-100`
  (at `b2e387db`) wraps `RepairIfNeeded` in `try/catch (Exception)` and returns `false`, so the stub
  throw on the path through `AiPartySizeService.IsTakenOverPlayerClan` (`:69`) is swallowed. The same
  class's `RepairIfNeeded_EngineDeclinedThePromotion_ReportsFalseAndStaysQuiet`
  (`TAOM.Tests/Features/PlayerSwitcher/PlayerClanLeadershipServiceTests.cs:182-191`) **passes on the stub
  run** without ever reaching `PromoteToClanLeader`: it asserts only `false` and no message, which the
  catch path also produces. That is a green-for-the-wrong-reason result on CI, and the class is not
  among those a per-method tag would catch. Class-level tagging of the 100 classes happens to cover
  this one; the "self-policing" argument for future drift does not. The lane lists vacuous passes as
  not audited; this shows they exist.
- **By-design check**: no category policy exists in `.claude/rules/tests.md`, the ADRs or the trap
  index; the only category today is `BindingVerification`. Nothing to refute on design grounds.
- **Why LOW today**: the tag only has a consumer once a hosted CI job runs the suite (TEST-L5-07). On
  its own it changes nothing a player or the local gate sees.

## TEST-L5-08: direction, run the binding gate against the next engine build before Steam installs it

**Outcome: REFUTED as stated. The evidence lines hold; the premise that gives the item its value
("before any player or the desktop installs it") does not. Impact today: LOW.**

- **Holds**: `results\ci_bind_refasm.trx` Counters are `total=368 executed=368 passed=338 failed=30`
  (re-parsed), so 338 of 368 `BindingVerification` results pass on BUTR metadata and the 30 fail
  loudly, not skip. The Core nuspec in `lane5-probe\butr\core.nupkg` carries
  `<tags>... buildId:25302170 appId:261550 moduleVersion:v1.5.3` and version `1.5.3.122374-beta`;
  `E:\Steam\steamapps\appmanifest_261550.acf:13` is `"buildid" "25302170"`. `/engine-bump` is indeed a
  response after the fact (`.claude/skills/engine-bump/SKILL.md:9-12` at `b2e387db`).
- **Refuted, the timing premise**: BUTR builds these packages from Steam, after the fact. The
  `BUTR/Bannerlord.ReferenceAssemblies` README (fetched this run) says "Packages are generated from Steam
  builds" and "Update Build Registry checks for builds every three hours and requests generation when
  packages are missing"; beta means "the build on the `beta` branch". A package can only exist once
  TaleWorlds has pushed the build to a Steam branch, which is the moment auto-updating clients on that
  branch receive it. Measured on two real bumps (nuget.org version history, day resolution, fetched
  this run): `1.5.3.122374-beta` was published 9/15/2026, the same day the installed
  `bin\Win64_Shipping_Client\TaleWorlds.Library.dll` was rewritten (mtime Sep 15 08:16); `1.4.6.115439`
  was published 6/11/2026, the same day as the force-bump the engine-bump skill records
  ("2026-06-11 17:39"). The desktop's manifest has `"AutoUpdateBehavior" "0"` (`appmanifest_261550.acf:22`),
  which is Steam's always-keep-updated setting (general Steam knowledge, not read from a Steam doc
  this run). So the job would report on the same day as the install, not before it, and never before
  players on the same branch.
- **What survives, narrower and not what the finding claims**: (1) a scheduled hosted run could flag a
  new build within about three hours of the Steam push without anyone starting a session, but the
  session-start GAME VERSION DRIFT banner already flags it at the next session, so the gain is hours;
  (2) the one genuine pre-player window is beta versus stable (players on the stable line get a beta
  only when it is promoted), yet the desktop already runs the beta for `bannerlord-1.5.x`, so the
  bindings are exercised there first. Either could be re-raised as a separate LOW direction item after
  TEST-L5-07 lands; neither needs this one.
- **By-design check**: nothing in the ADRs or rules decides this; the refutation is on facts, not
  design.

## What I did not cover

- No build, no test and no `dotnet vstest` run: every run result above is the lane's recorded trx,
  re-parsed. I did not re-disassemble a BUTR stub body (`ldnull; throw`); the NRE outcomes are the
  recorded consequence.
- The working-tree (another session's) versions of the Elk, Animalia, Elephant and Mumakil tests were
  not read; only `b2e387db`.
- BUTR's publish latency is known only to day resolution (nuget.org version history) and from the
  README's three-hour poll; the ordering within a day was not measured. The web pages were read through
  a summarizing fetch, not raw.
- The 288 stub-passing results in the 100 `RequiresGame` classes were counted, not audited one by one
  for vacuous passes; only `PlayerClanLeadershipServiceTests` was read.
