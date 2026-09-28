# CHANGELOG: TAOM (Tales From the Age of Men)

> **Generated at each release; do not edit by hand.** `/release` runs
> `python tools/changelog_from_commits.py --version vX.Y.Z --write`, which adds one section per
> release from the subjects and bodies of every commit since the previous release tag. The commit
> body is the changelog entry, so write it for a reader of the release note. Between releases,
> `git log <last tag>..HEAD` is what changed.
>
> **Archive:** the hand-written entries from 2026-07-01 up to the switch to generation are in
> [`docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`](docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md);
> older ones are in [`docs/changelog-archive/CHANGELOG-2026-H1.md`](docs/changelog-archive/CHANGELOG-2026-H1.md).

## v2.0.31 (2026-09-28)

Commits since v2.0.30: 259 (245 with the version label, 14 without).

### Features

#### feat(creature-bandits): v2.0.30 - riderless spider broods in Mirkwood

`d469914a`

Giant spider broods now roam Mirkwood as bandit parties, led by a pale
broodmother. Up to four broods patrol the Mirkwood villages, castles and
Dol Guldur. On the map a brood shows as a spider, and meeting one goes
straight to attack or leave. In a field battle every spider fights on
its own, with no rider: it hunts the nearest soldier, bites, pounces and
swipes, and soldiers and archers target and kill it like any enemy.
Spiders count on the battle scoreboard like any troop, with their
casualties and kills. Spiders are bandits only: never recruited, never
taken prisoner, never fielded by lords (#692).

New campaigns only. The brood clan is part of the campaign's starting
data, so a save from an earlier version keeps playing with no broods.

The MCM has a new "Creature Bandits" group:
- Spawn Spider Broods (on): turning it off stops new broods; broods
  already on the map stay.
- Creature Hit Points (200).
- Soldiers hit per attack: bite 1, pounce 2, swipe 3.
- Damage per attack: bite and pounce 100%, swipe 50%.
- Only Crits Knock Down (on): a normal hit staggers a soldier.
- Pounce and swipe cooldowns (5 s and 2 s).
- Damage taken: 50% from missiles, 100% from cut, pierce and blunt
  blows. Charges, kicks and shield bashes count as blunt.

The ridden spider mount is unchanged.

Known limitations: the damage-taken options do not apply to TAOM's own
scripted blows (troll brute force, signature strikes, warg and spider
bites), which makes no difference at the default 100% melee values. On
the deployment screen the spiders hold still but stay visible when the
player attacks. A defeated brood drops no items. Broods can spawn
within the player's sight.

Tests: rules for the spawn swap, route A's flags, the deployment gate,
the routed-count backstop, the patrol order, the brood switch and the
scoreboard's casualty and kill credits; the creature's tuning, clamps
and damage-taken factors; the strike cap through the service, a rider
and his horse in one slot, a loose horse never taking a soldier's slot,
and the ridden spider's unchanged numbers; the troop, clan and template
XML against the code; every patch bound against the installed engine;
the models' creature clauses in the campaign and Custom Battle. Full
suite 10,979 passed, 2 skipped, 0 failed.

Review: docs/reviews/rca-creature-bandits-2026-09-28.md

#### feat(tools): v2.0.30 - integrate_branch merges with append-only union

`d648e00c`

tools/integrate_branch.py merges one branch into a linked integration
worktree. It refuses the main checkout and a dirty tree, and runs only
non-destructive git verbs: it never aborts, resets, cleans or stashes.
Conflicts in the append-only files (the lessons files and REVIEW-LOG by
default) are resolved by union, ours then theirs, with the BOM and line
endings kept. It then adds the blank line missing before a heading the
merge brought in, stops on a duplicated heading or a leftover conflict
marker, and commits with the message file. Any other conflict exits 2
with the paths listed and the merge left for a hand resolution;
--dry-run reports the conflict set without touching the index. It runs
no test suite, so each verification stays its own call.

The merge runs in diff3 conflict style. In the default style git moves
lines both sides share out of the conflict block, so a union of two
review log entries that end with the same line leaves the first entry
without it. The scratch merge scripts this replaces ran in the default
style, recounted lessons that are no longer counted, and still carried
a CHANGELOG branch that is dead since the changelog became generated.

#### feat(tools): v2.0.30 - improve_ctl runs the /improve pipeline plumbing

`218488f4`

tools/improve_ctl.py replaces the scratch scripts the last /improve run
rewrote for each batch. Run data now comes from flags, git and the
environment instead of literals in the code.

- args writes a Workflow script's args JSON. It embeds
  .claude/skills/improve/references/dispatch-rules.md verbatim as
  rules, reads the version from Main/_Module/SubModule.xml and finds
  the main checkout from git. --run-root is always required;
  TAOM_IMPROVE_ROOT, the worktree root, supplies only scratch and tmp.
  For a review each item gains its changed files by kind and the
  deep-review lenses. A hook-only change now gets the standards lens,
  which the old scope.py skipped.
- status sets one plan row's Status cell, finding the column from the
  table header and keeping every other byte, CRLF included.
- codex-prompt writes the adversarial review prompt for a branch from
  git refs. The fixed blocks are the fenced blocks in
  .claude/skills/review-codex/references/prompt-fixed.md, and each
  STOP condition of the branch's plan is quoted whole, where the old
  generator cut wrapped conditions at their first line.
- file-issue files a TITLE:/LABEL: draft with gh once
  check_public_text.py passes it. Filing is public, so it runs only on
  the maintainer's word.
- watch lists workflow agent transcripts by last write and exits 1
  when a started agent with no result has been silent past a limit
  (default 20 minutes). Two review agents once sat 9.4 and 4.5 hours
  on a hook prompt that no timeout catches.

#### feat(improve): v2.0.30 - durable Workflow scripts for /improve runs

`1198881f`

The /improve review sprint dispatched its agents from Workflow scripts
kept in a run folder and a scratch copy. They hard-coded the report
date, the version label, a baseline commit and drive paths; the tracked
copies checked out as CRLF, which the Workflow approval check refuses;
and a null agent result (a killed agent, a usage limit) was handed on
as data. Their review loop ran one fixed second pass, so seven ad hoc
workflows had to finish the convergence work.

Five scripts now live in the skill, under
.claude/skills/improve/workflows:
- fanout.js: parallel read-only agents (audit lanes, verifiers, design
  agents), each appending to its own file, then an optional checker
  over the rows a declarative selectFrom picks;
- plans.js: a writer or extender, then up to reviewRounds cold reviews,
  each followed by a reviser when it finds blocking items;
- execute.js: one executor per item for a plan, a list of maintainer
  decisions, or ordered stages that stop at the first not DONE;
- review.js: deep-review lenses in waves, a lead that verifies and
  fixes, then at most maxRounds (default 2) convergence rounds; a fix
  pass runs only when a later round will review it, and every finding
  the loop did not close comes back as residual;
- draft-issues.js: one drafter per item writes a TITLE and LABEL draft
  for improve_ctl.py file-issue.

Every run value comes from args, built by improve_ctl.py args. A script
throws without args.rules, the dispatch rules text, and opens every
prompt with it, then states the agent's role (EDITING or READ-ONLY,
which decides the rules sections that bind it) and the values of the
rules' placeholders; each role takes its model from args.model,
defaulting to claude-opus-5-5; deep-reviewer calls pass no model; one
pool bounds the agents in flight at args.pool; every acted-on agent has
a schema; a null result is reported as a failure.

.gitattributes pins that folder to LF, not every *.js: tracked tools/
scripts are CRLF in the worktree and would churn.
tools/tests/test_improve_workflows.py checks each script statically (LF
and no hidden character, a pure literal meta, the args.rules guard, no
date, version, hash or drive path, no model on a deep-reviewer call)
and, when node is on PATH, runs it against a stub Workflow runtime that
records every agent call. Against the old scripts it reports 84
failures and 4 errors; it catches 17 of 17 mutants of the new ones.

Not-tested: a run under the real Workflow tool, which CI cannot start

#### feat(tools): v2.0.30 - check public text before it is posted

`5e5877bd`

tools/check_public_text.py checks an issue body, an issue comment or a
commit message before it goes public, and exits 1 with one
"file:line: rule: text" line per finding. It flags em and en dashes
through lint_docs.scan_text_for_dashes, so code spans, fenced blocks
and lint-allow-dash lines pass; local absolute paths (a drive-letter
path or a Git Bash /x/repos/ path, code spans included); and leftover
placeholders such as MERGE_HASH or a line that is only TODO. The words
Claude and Codex pass, since issues legitimately discuss Claude Code
hooks.

It replaces the wording check the last /improve run kept in a scratch
script and pasted into five issue filing and closing scripts. Issues
drafted in that run still carried local paths that had to be stripped
by hand before filing.

#### feat(tools): v2.0.30 - read native engine calls and strings as C

`d51e4302`

Research and debugging can now read the native engine behind a managed
call, not only behind a crash offset.

tools/native_decompile.py gains two ways in. --engine-method takes a
managed [EngineMethod] call (IMBAgent.GetCurrentActionType, or its
engine name get_current_action_type) and decompiles the native function
that implements it. --string decompiles the native code that uses an
attribute, file name or assert text, which is how the engine's own
parsing of action sets, monster usage and skins can be read. Every
printed function that implements engine methods is labelled with them,
and a thunk is followed to the code it jumps to.

The shipping DLL keeps almost none of the method names, so the new
tools/native_engine_methods.py joins the two halves of the engine's
binding by id: the generated enums and SetFunctionPointer pairings of
the installed AutoGenerated assemblies, and a capstone sweep of the
three native registration functions. On v1.5.3 all 2,282 methods
resolve with no mismatch. An id found on one side only is reported,
and the tool then refuses that assembly rather than answer.

Ghidra's own analysis had missed 947 of the 2,093 registered
implementation addresses, so a crash inside an engine API function
could not be decompiled. Each project is now seeded once with a
function at every registered address, named when one method owns it.

The tools are wired into the processes that need them: /research and
the researcher agent follow a call into native code, /investigate
routes native crashes and hangs, /native-crash-triage gains the
hang-dump procedure (moved out of machine-local memory), /engine-bump
warms the decompiler and reads the map check, and the review engine
lens treats a native claim with no decompile behind it as unverified.
AGENTS.md's research rule and the lookup tables name the tool.

Refs #688

#### feat(tooling): v2.0.30 - wire the graphify code graph into the workflow

`b8833474`

graphify's C# code graph is now part of the everyday workflow (#677,
ADR-012). Before a TAOM type is changed, reviewed or debugged, the
workflow lists everything that depends on it, with file and line.
/deep-review Step 1 hands the callers outside the diff to the
completeness and data-flow lenses, and /investigate, /new-feature,
/research, the feature-builder and refactoring-specialist agents and
the Codex skills run the same check. This reverses the 2026-08-18
review's decision to keep graphify unwired; ADR-010 now carries a
pointer and an amendment.

tools/graphify_taom.py is the only way to build or query the graph. It
runs a code-only extract and a clustering pass with no LLM naming into
E:\graphify\TAOM (a linked worktree gets its own graph), refuses an
output folder inside the repo, holds a lock across sessions, stamps
each build and reports when the graph is stale. A new PreToolUse gate,
check-graphify-usage.sh, denies raw graphify write and install verbs
from Bash and PowerShell while queries still pass. The session banner
now shows whether the graph is current.

Found on the way: an incremental graphify extract writes
graphify-out/cache/stat-index.json into the scanned repo even with
--out, because graphify fixes that cache's location from its first
caller. The wrapper pins an absolute GRAPHIFY_OUT, and a contamination
check refuses to stamp a build that leaked.

Tests: 38 new unit tests in tools/tests/test_graphify_taom.py, and
tools/test_hooks.sh gains section 7e plus gate rows in 4c, 4d and 5.
The gate was proven live: the harness refused a raw graphify call from
both shell tools.

#### feat(hooks): v2.0.30 - run the eight git gates for the PowerShell tool

`e046bdf0`

Eight PreToolUse gates (block-no-verify, check-claude-files-tracked,
check-commit-subject-version, block-dangerous-git, block-broad-git-add,
check-moduledata-validation, check-native-dll-crt and
check-doc-config-drift) were registered for the Bash tool only, so the
same git command run through the PowerShell tool skipped them: an
unlabelled commit, an AI co-author trailer, --no-verify, git reset
--hard and git add -A all ran unchecked. Maintainer decision 61 asked
for the gates to cover PowerShell, with each gate's parsing checked for
PowerShell syntax and test rows per shell.

settings.json now registers all nine git gates in one Bash|PowerShell
group; the old PowerShell group, which held only validate-push, is
folded into it. Registration alone was not enough: every gate parsed
its command as Bash text, and a correctly labelled PowerShell
here-string commit (git commit -m @' ... '@) was denied because the
subject read as `@`. Each gate now reads its command through a new
_pybin.sh helper, taom_hook_command, which runs _shellwords.py: a
PowerShell command comes back as the Bash text of the same command, and
in both shells a git named by a path or in capitals (GIT, git.exe,
C:\...\git.exe) comes back as `git`. If the reader fails, the gate
reads the raw command as Bash text, as before, and says so on stderr.

Other changes that ride with it:
- block-no-verify, block-dangerous-git and block-broad-git-add now try
  Python (the reader) first and jq only when there is no Python. jq
  first would never read PowerShell on the Linux CI runner, which has
  jq on PATH.
- block-broad-git-add strips quoted spans with a bash loop instead of
  forking sed once per git segment. A 100-line PowerShell commit
  payload took 5164 to 5443 ms with the sed, past the 5 s registration
  (a killed ask gate allows), and 308 to 422 ms without it.
- The two confirm gates' raw prefilter matches git in any case, so a
  payload whose only git is GIT reaches the reader.
- check-commit-subject-version reads a single word piped into
  git commit -F - (a PowerShell here-string, echo, Write-Output or
  printf) as the message, so an unlabelled piped subject is denied.
- check-claude-files-tracked also reports an untracked .py file, since
  the reader is the first Python helper under .claude/hooks.

tools/test_hooks.sh gains section 7e (registration parity, the
PowerShell prefilter, 58 verdict rows, commit-test traces and timing
rows for three large payloads under both tools) plus a PowerShell
contract payload and three section 6 rows. The suite goes from 482 to
651 passed, 0 failed. Part of plan 027.

Not-tested: a live PowerShell tool call through the real harness (owed after merge)

#### feat(hooks): v2.0.30 - read PowerShell commands as Bash text

`b23dc899`

Add .claude/hooks/_shellwords.py, a small reader that hands a tool
call's command back as POSIX-shell text. A PowerShell command comes
back as the Bash text of the same command: here-strings, '' and ""
quoting, backtick escapes and line continuations, # and <# #>
comments, { } and ( ) as statement breaks, and the & call operator.
In both shells a git named by a path or in capitals (GIT, git.exe,
C:\...\git.exe) comes back as `git` where it is the command. Text it
cannot follow (an unclosed quote, here-string or block comment) comes
back unchanged, which is how every gate read a command before.

It has two modes: posix (the command as Bash text) and segments (that
text split at ; & | and newlines outside quotes, with a # comment
dropped), and always writes UTF-8 with LF line ends.

Nothing calls it yet; the next commit wires the git gates to it.
tools/tests/test_shellwords.py pins it with 33 unit tests whose
PowerShell argument lists were read from PowerShell 7's own parser.

This is the first step of plan 027, for maintainer decision 61 (cover
the PowerShell tool in the git gates).

#### feat(troll): v2.0.30 - hill troll troop, 200 HP trolls, loc sweep

`4ceae903`

Hill troll on troll_skeleton_a: the hill_troll troop for Mordor, 428
retargeted human clips and the 52 Fab clips bound into the standalone
as_hill_troll_warrior, its Fab idles reused for the inventory,
conversation, cheer and bodyguard-pose codes, and the Brute Force tree
(#649) on both trolls with distances scaled by eye height. Both trolls
have 200 health (campaign via TaomCharacterStatsModel and the race's
baseHitPoints; Custom Battle via the Armory Monster), cost 50 of the
player's resource to recruit with 5 and 4 upkeep, and Bolgrukig,
Zarunik and Brughash field 0 to 2 of each. New gates:
wire_hill_troll_race.py --check, TrollHitPointsLiveDataTests.

Localization: stale translations re-translated across the three
modules, Lindon and Umbar keys split, per-quest fallback keys, Duinhir
keeps his title; the translator no longer seeds or translates a key
another source owns; LocalizationKeyConsistencyTests and the
writing-system scans gate the incident classes, rows and cache.

Deep review in two waves plus a convergence pass and a review of the
follow-up features; 52 findings, RCA
docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md. Open for Mike:
the reused idles are not cyclic, the culture template's 0 to 7 trolls,
the cave troll's commented TroopWeight row, and the recruit cost for a
Free-culture player.

Other sessions' hunks in shared files (the armory LOD pass, the
creature-bandits research, a Gondor peasant and the howdah and Animalia
troop names) are left unstaged. Language rows this session's
translator wrote for other sessions' pending keys are included.

#### feat(gondor): v2.0.30 - port KEYforce's Lamedon drop, noble ladders

`998f054c`

Port the TAOM side of KEYforce's lotraom-assets 429746b2 (#669): the
Lamedon and Ringlo Vale re-kit, the renamed banner spears, the Umbar
sword swaps and seven troop renames. troops_gondor.xml and
troops_umbar.xml are merged per slot against the mirror revision he
edited from, because a verbatim copy reverted #609, #617 and #631.
gondor_ring_peasant is deleted by his data; the save break is accepted
and recorded as a known limitation.

Noble lines now carry better kit than regular troops of their level:
taom_schema gains _NOBLE_LINE_TROOPS (92 troops, tier 2 to 7), judged
one tier up and anchored a band up (never below the item's own tier)
by the armour and melee tools, and _ARMOUR_LADDER_EXEMPT_ITEMS (18
troop and item pairs) for the fixer's hand decisions. The Ringlo
poleaxe troops fight two-handed with no shield. The live Armory holds
the mesh repoint (58 dead refs), the Gondor armour restat and the
blade restat; the commit carries the audit report and catalogue.

Deep review in three waves plus convergence; RCA in
docs/reviews/rca-keyforce-lamedon-port-2026-09-25.md. Follow-ups:
#670, #671, #672, #673.

#### feat(tools): v2.0.30 - CHANGELOG release section from the commit log

`ae85381d`

Plan 020 moves CHANGELOG.md from a hand-edited shared file to one
generated at release time. This commit adds the generator,
tools/changelog_from_commits.py (pure stdlib), and its tests.

It reads every non-merge commit since the previous v* release tag
(git describe --tags --abbrev=0 --match 'v[0-9]*'), groups the commits
whose subject carries the version label (the shape the subject hook
gates) by type in a fixed order (Features, Fixes, Balance, Data,
Performance, Refactoring, Tests, Documentation, Chores, then any other
type alphabetically), and lists the commits without the label in a
last group so nothing is lost. Each entry is the subject, the short
SHA and the body verbatim. Without --write it prints the section; with
--write it inserts it above the newest release section of CHANGELOG.md,
keeping the file's line endings.

It refuses with exit 2 on a malformed --version, an empty range, a git
failure, a section already present for the version, and a hand-written
"## " section above the release sections: only /release writes the
file from now on.

Smoke on real history, v2.0.29..v2.0.30: 57 commits, 53 with the
version label, 4 without; 57 entries in the groups Features, Fixes,
Balance, Tests, Documentation, Chores and the unlabelled group.

tools/tests/test_changelog_from_commits.py holds 22 tests (parsing,
rendering, insertion and an end-to-end run in a temp git repository),
and tools/README.md gains the tool's row.

Not-tested: a live /release run; the next real release is the first.

#### feat(release): v2.0.30 - refuse to package a dirty or off-tag DLL

`5d1b1bda`

tools/package_release.py gains --require-build REV (plan 017). It reads
the build stamp out of TAOM.dll and TAOM.Dependencies.dll in the source
Modules folder and refuses to package when a stamp carries .dirty,
nogit or .nogit, names a commit other than the one REV resolves to, or
when a DLL is missing or holds no single stamp. The check also runs
under --dry-run, so it works as a gate on any Modules folder before an
upload. Exit code 2 on refusal, like the other input errors.

13 new tests in tools/tests/test_package_release.py: 9 for the stamp
reader and checker, 4 for the CLI (skipped when git is not on PATH).
Verified against the real DLLs of the previous commit: HEAD passes,
HEAD~1 is refused for both modules.

#### feat(build): v2.0.30 - mark dirty and git-less builds in the stamp

`057628a2`

The .NET SDK appends HEAD's commit SHA to InformationalVersion whatever
the working tree holds, so a DLL built from uncommitted edits looked
like a clean build of that commit (plan 017).

A new TaomStampWorkingTreeState target in Directory.Build.props runs
after InitializeSourceControlInformation and before
AddSourceRevisionToInformationalVersion. It runs git status --porcelain
over Main, Dependencies, Stubs and the props file and appends .dirty to
SourceRevisionId when anything is modified or untracked; it writes
nogit when no repository information exists and appends .nogit when
the git command fails. About 40 ms per project build.

Verified on SDK 10.0.401 by building a dirty tree (+<sha>.dirty in both
TAOM.dll and TAOM.Dependencies.dll), a build with
EnableSourceControlManagerQueries=false (+nogit), a build with git.exe
hidden from PATH (+<sha>.nogit) and a clean tree, reading each DLL's
ProductVersion.

#### feat(tactics): v2.0.30 - wire OOB Auto-Assign to HeroAutoAssigner

`d1b2c44c`

The Order of Battle overlay's Assign Heroes button printed an
unlocalized placeholder and did nothing, while HeroAutoAssigner sat
registered in IoC and never resolved. Plan 022 wires the two together.

HeroAutoAssigner.PlanCaptains is a pure global greedy over the existing
role score: every hero and open-formation pair scoring above 0, highest
score first, ties to the lower formation index and then the earlier
hero, each hero and slot used once, Unset formations never filled.

OOBCaptainAutoAssigner is the boundary class. When the player is the
general it collects formations with a troop class and no captain, and
candidates from UnassignedHeroes and each HeroTroops list (never the
player, never a hero already leading, never a disabled item), then
applies each pick through vanilla's manual-drag path: OnHeroSelection,
then ExecuteAcceptCaptain. Public members only, no reflection. A pick
counts only when the formation's Captain is the hero afterwards.

OOBButtonsVM.ExecuteAssignCharacters now delegates to it and shows one
of three localized messages. The three taom_oob_autoassign_* keys are
registered in taom_module_strings.xml and seeded with English rows in
all 12 language files; the translator run is owed to the maintainer.
The seeding helper put the rows after </strings> in these mixed line
ending files, where LocalizedTextManager.LoadLanguage never reads them,
so the rows were moved inside <strings> by hand.

The class comments now name vanilla DeploymentFormationClass correctly
(5 is InfantryAndRanged, 6 is CavalryAndHorseArcher, 0 is Unset).

Tests: 10 PlanCaptains cells and 2 overlay delegation tests.

Not-tested: OOBCaptainAutoAssigner against a live OrderOfBattleVM (needs a mission); in-game check owed
Research: v1.5.3 OrderOfBattleVM, OrderOfBattleFormationItemVM, OrderOfBattleHeroItemVM, DeploymentFormationClass, LocalizedTextManager

#### feat(crash): v2.0.30 - put the TAOM build stamp in crash bundles

`743ce2e1`

The crash bundle's Identity section named only the SubModule.xml
version label and a TAOM.dll SHA1 that nothing maps back to a commit.
The build stamp (the assembly's InformationalVersion: build time plus
commit SHA) reached a bundle only through the [BuildStamp] line near
the top of the bundled taom_debug.log.

IdentitySnapshot gains a last field, TaomBuild, read by
BuildStampReport.ReadInformationalVersion (now internal). report.txt
prints it as a Build: line in the Identity section, manifest.txt as a
TAOM build: line, and report.json gains TaomBuild, so triage no longer
depends on the bundled log. The field goes last so the positional
fallback in CrashReportService cannot shift. The BuildStampReport
summary now credits the .NET SDK, not Bannerlord.BuildResources, with
the +<sha> suffix. Plan 017, commit A; four new tests.

Not-tested: IdentityCollector wiring (reads ModuleHelper; exercised only in game)

#### feat(nazgul): v2.0.30 - the Nine's scream is the clip Mike supplied

`4b5662b2`

The SCREAM (#645) now plays the clip Mike supplied, TAOM_Nazgul
Scream 2, as one variation (nazgul_scream.ogg) in place of the three
ElevenLabs takes. The module sound name is unchanged, so no code or
config value changes. The file is mono Ogg Vorbis at 44.1 kHz, 4.41 s,
with the peak raised to -1 dBFS before encoding and the input's
metadata tags dropped.

The old takes stay in the game install, because the module copy never
removes a file. Delete them after the next deploy and before the next
release.

Deep-reviewed (XML, data flow, completeness, design and engine lenses,
then a convergence pass): no runtime defect. The text findings are
fixed; rows W1 to W9 in docs/reviews/rca-nazgul-scream-2026-09-23.md.
Full suite in a clean worktree: 10235 passed, 2 skipped, 2 failed
(an Elk and an Animalia test that fail the same way without this
change).

#### feat(nazgul): v2.0.30 - the Nine SCREAM, a second signature (#645)

`b90fd3a4`

SignatureStrikes held one signature: top-level heroIds and races, a
binary Slam/Sweep kind and two hard-wired cooldown stamps. The config
is now a signatures list. Sauron's rows are unchanged. The Nine (the
nazgul_nine hero set and race nazghul, #644) Scream on an overhead and
both side swings: a 6 m ring centred on the wraith, 0.3 of the hit's
damage with the engine's boulder falloff, a knock-back but never a
knockdown, 25 morale before the engine's morale resistance and the
falloff, one 15 s timer shared by the three directions, and a
generated shriek at the wraith's head. A thrust or a ground hit does
nothing.

- StrikeKind.Scream, StrikeOrigin (Impact or Self) and per-kind stamps
  in StrikeKindTimes. The registry resolves a signature index at spawn
  (hero ids and hero sets before race, first listed wins, overlaps
  warned); the roster falls back to the character's own id in a
  Custom Battle.
- The struck foe now takes a strike's fear, never its ring blow.
  Before, the runner skipped it outright, so Sauron's slam never
  frightened the agent he hit.
- StrikeSoundPlayer plays LOTR/Mordor/Nazgul/nazgul_scream (three
  ElevenLabs takes, mono Ogg Vorbis) through Mission.MakeSound at the
  finiteness-gated eye position, and yells if the name does not
  resolve.
- The provider validates the list: it drops a nameless, repeated or
  identity-less signature and any strike whose kind has no cooldown,
  and reverts a bad field to that signature's compiled default.

Reviewed: /deep-review, seven lenses and a convergence pass. Two MED,
seven LOW and one design change, all fixed; RCA
docs/reviews/rca-nazgul-scream-2026-09-23.md. Full suite: 10186
passed, 2 skipped on the shared working tree, and 10099 passed, 2
skipped for this commit alone on 9804f67b in a clean worktree. Not
smoked in game.

#### feat(nazgul): v2.0.30 - the Nine become race nazghul (#644)

`9804f67b`

The Armory has shipped a nazghul race (1.18 scale, its own meshes)
that no hero used: six of the Nine carried no race in lords.xslt, so
they loaded as human, and three were race uruk. All nine lords.xslt
templates now emit race nazghul.

lord_1_48_1/2/3 were also defined by characters/lords.xml rows. The
engine merges a second definition per attribute (the later file wins)
and unions EquipmentSets with different ids, so every campaign dressed
those three in the Nazgul kit or a Mordor lord kit at random, and their
age of 20 came only from the row (the templates said 31, 9 and 11).
Mike's rule: vanilla lords live in the XSLT, new ones in lords.xml. The
rows are gone; race, age 20 and face age 22.19 moved into the
templates. The other 176 lords defined twice are #648.

Vanilla never saves a character's race, and RacePersistenceService
puts the captured race back at session launch, which would restore
human and uruk on every old save. The restore now leaves the Nine's XML
race alone (INazgulRegistry) and logs how many it kept. A Player
Switcher player's barber race edit on a wraith is reverted on load, by
design. All nine get orc shield-crush (orcShieldCrushRaces adds
nazghul, Mike's call).

New gate, CultureRaceConsistencyTests
.EveryCharacterRaceIsARealRegisteredRace: every race in ModuleData XML
and XSLT must be registered by the installed skins.xml, because
FaceGen.GetRaceOrDefault is a plain dictionary index that throws on an
unknown name.

Deep review: seven lenses and a convergence pass. One pre-existing
HIGH (the double definition), one MED (no gate on the new Armory
reference), three LOW, and stale docs and comments across eleven
files, all fixed. RCA docs/reviews/rca-nazgul-race-2026-09-23.md. Full
suite 10180 passed, 2 skipped, 0 failed.

Research: BasicCharacterObject.Deserialize, FaceGen.GetRaceOrDefault,
  MBObjectManager.MergeElementAttributes and MergeElements,
  NPCCharacters.xsd, Hero.SetInitialValuesFromCharacter,
  CharacterObject.MaxHitPoints, GetMoraleResistance and
  UpdatePlayerCharacterBodyProperties (installed v1.5.3)
Save-compat: existing saves adopt nazghul on the next load; a hero's
  equipment is saved, so the trio's single kit reaches new campaigns
Not-tested: in-game render (portraits, armour at 1.18 scale, bald and
  clean-shaven), the restore log line on an old save, Player Switcher
  into a wraith

#### feat(elephant): v2.0.30 - howdah log counts arrows each archer looses

`f4503650`

The howdah status line now carries shots=N per archer: arrows actually
loosed, counted from the engine's missile-fired callback
(Mission.OnAgentShootMissile, an [MBCallback] raised for every missile;
vanilla's archery training counts shots through the same hook). Until
now the log could not tell a shot from a re-nock, because prog latches
at 1.00 once any draw completes. Read shots against restarts: both
climbing is an archer shooting, since every shot restarts the draw;
restarts climbing while shots stays flat is the re-nock loop.

The count lives on each archer's own HowdahCrewAgentOrigin, so the
callback identifies a crew shot with one type check and never searches
a list of seats, and it goes through Interlocked because the callback's
thread is the engine's business. Nothing reaches the mahout's origin.

Full suite green: 9,982 passed, 2 skipped, 0 failed.

Research: Mission.OnAgentShootMissile ([MBCallback]),
  MissionBehavior.OnAgentShootMissile, TrainingFieldMissionController
Not-tested: no /deep-review on this change; committed at Mike's
  instruction. The callback itself needs a battle to confirm it fires.

#### feat(elephant): v2.0.30 - elephant 30% bigger, howdah rides its spine

`f8b3c0ea`

The war elephant is 1.3x its old size (Mike): taom_war_elephant's
body_length 100 to 130 in the live Armory, which the engine applies at
build to the skeleton, animations, capsules and the visible howdah.
Everything it does not scale for us derives from one constant,
ElephantConfig.AuthoredScale: the howdah's height and footprint, and the
trample reach (trigger 3 to 3.9 m, radius 4 to 5.2 m). The prefab is
authored at final size, not runtime-scaled, and a test pins body_length
to the constant so a resize without regenerating the howdah fails the
suite. The rider is not scaled, so the mahout needed nothing.

The howdah now takes its height from the elephant's spine. At 1.3x the
archers looked sunk: a probe on the spine bone showed the elephant
carries its back about 19 cm higher standing than at rest, and the
visible deck bobs about 18 cm per stride. A fixed platform can only sit
below that bob (sunk) or inside it (the deck rises through the archers:
stutter and re-nock, both seen in game). Following the spine's height
only matched the visible deck on 22 of 23 samples of a two-minute
walking battle; the 23rd was a bad bone read the plausibility band
rejected, falling back to the fixed height as designed. Bone tracking
was off since June because the floor rode inside the elephant's capsule
and shoved it; the floor now clears it by at least 0.66 m.

Also: the howdah floor squashed to about 2 cm (the same lintel body whose
raised ends stranded the mumakil's archers), the logged floor clearance
now scales the Monster capsule by the agent (it read 1.374 m for a real
0.564 m gap at 1.3x), and two docs that claimed body_length scales the
rider corrected, one of them the modding handbook.

Still open, not new: archers carried at 4 to 7 m/s sometimes abort a
draw; the earlier clean-looking battle spent more of its moving time in
the reload state. A per-seat shot counter is next.

Full suite green: 9,978 passed, 2 skipped, 0 failed.

Research: MissionObject bone idiom, Mat3, Monster.BodyCapsulePoint1/2,
  Agent.AgentScale, Skeleton.GetBoneEntitialFrameWithIndex
Not-tested: no /deep-review on this change; committed at Mike's
  instruction after in-game testing
Save-compat: none, no saved state touched

#### feat(mumakil): v2.0.30 - eight archers on the war tower (#627)

`1933508e`

Five on the main deck, two on the upper, one in the crow's nest. A clone of
the elephant's howdah crew per the one-feature-per-creature convention,
carrying its four engine rules unchanged and referencing its measured
constants from the pure helpers rather than copying them.

The scale is derived, never written down: the prefab is authored mount-local
and the platform multiplies its basis by the mount's own AgentScale every
tick, so BodyLength=300 appears nowhere in the data. The basis is
orthonormalised first, because ApplyScaleLocal multiplies an existing basis
and whether the native frame behind Agent.Frame already carries AgentScale
cannot be settled from managed code. Getting that wrong would put the crow's
nest at 41 m instead of 13.8; frameScale= in the log answers it for good.

The review added a rule the howdah never needed, because the howdah has one
deck. A crew frame under a higher deck's floor needs the human capsule's full
1.92 m of clearance: Barrier is in the engine's missile-exclusion mask but not
in its agent mask, so an archer whose head is inside its own deck is shoved
every frame and put back by its seat, which is the bow-draw failure again in a
new direction. One frame was there. The first correction then put it inside
the spacing rule's margin, so it moved twice. Headroom, containment, spacing
and the deadband margin are all gates now.

Also: a write-only rider field and its false comment deleted; the deadband's
stated derivation corrected; an engine float gated before a native SetFrame in
both creatures; the public reposition entry given its callers' recycled-index
guard; a missing-prefab error deduped per battle; the sealed-CharacterObject
ban widened from a filter naming the elephant to one naming the crew; the MCM
toggle relabelled for both beasts with its property name left alone so no
existing install is handed a fresh default.

Full suite green: 9,908 passed, 2 skipped, 0 failed.

Research: Mat3.ApplyScaleLocal/MakeUnit/Orthonormalize, Agent.Frame,
  UsableMissionObject.IsDeactivated, MountVisualCreator.AddMountMeshToAgentVisual,
  Native/monsters.xml human body_capsule
Constraint: harad_mumakil_rider ships at weight 20.0, not the 28.0 the crew
  implies; the identity test has only been proven to 20.0
Not-tested: everything that needs a battle. The ordered checklist is in
  docs/features/mumakil.md, and the reference-frame mismatch it measures first
  (tower on the skeleton, platform on the root) is unresolved by design
RCA: docs/reviews/rca-mumakil-platform-2026-09-20.md

#### feat(mumakil): v2.0.30 - price the beast packages, plan platform crew

`43dada47`

Two troops put more than one agent on the field, so their weight has to price
the package rather than the rider. The rule, written into the file: every
extra battlefield agent adds 1.0, which is what that archer would have cost
as a troop of its own.

The elephant's 10.0 was set while the howdah crew were parked, so it only
ever priced the beast and its mahout. Crew are back on at two archers, so it
is 12.0.

The mumakil had NO row at all, which meant the largest unit in the mod cost
one party slot, the same as a peasant, while fielding a beast, a rider and
(with Phase 2) eight archers. Its monster is the elephant's on paper, 500 hit
points and weight 9999, but it is 3.0x the body with a 12 m trample radius
against the elephant's 4 m. By the rule it should be 28.0. It ships at 20.0,
because that is the ceiling the weight system's identity was proven for
(WeightedFrameIdentityTests): above it a normal party-size limit can fall
under a single troop's weight, which makes the clamped single-body boundary
reachable and lets the displayed over-capacity verdict disagree with the
enforced one. So the mumakil is deliberately under-priced by about eight, and
raising it means re-deriving that identity first.

The mumakil doc's Phase 2 section said platform crew were deferred pending a
crew-versus-mount collision fix. That blocker is gone: #627 solved the slide
with floor clearance and a teleport deadband, measured at drift=0.000. The
section now carries the Blender measurements instead: the war tower is
authored at elephant scale and the engine applies 3.0x from the Horse item's
BodyLength=300, giving three decks at 9.00, 11.40 and 13.80 m with 15.0, 2.6
and 1.9 m2 standable inside the walls, and geometric ceilings of 47, 14 and 5
archers at the 0.74 m spacing rule.

Decisions recorded: the prefab is authored mount-local and scaled at runtime
from Agent.AgentScale, never with 3.0 baked in; the code is cloned into
Features/Mumakil per the one-feature-per-creature convention, referencing the
pure helpers rather than copying their measured constants; eight archers, as
five on the main deck, two on the upper and one in the crow's nest.

Research: Agent.AgentScale, Monster and Horse item scaling on installed v1.5.3
Not-tested: no runtime change in this commit beyond the two weights

#### feat(elephant): v2.0.30 - #627 howdah crew shoot from a moving elephant

`3d70ca55`

Crew spawn went back on for the first time since June. Nine in-game rounds
later the archers loose arrows from a moving elephant, and a battle can end
with crew aboard without freezing the game.

Four engine gates, in series, none of which logged anything:

1. A null Formation pins Agent.MissileRangeAdjusted at 0 (Agent.cs:764 ->
   GetMissileRangeWithHeightDifference, Agent.cs:5444-5450), and the seat
   nulled it to stop the "walk to your ground slot" order. The seat now keeps
   the formation; measured after, mr=75.2 per seat.
2. Formation.DetachUnit stamps BehaviorValueSet.DefaultDetached, which keeps
   Melee at (8,7,4,20,1) and cuts Ranged to (0.02,7,0.04,20,0.03), about a
   hundredth of Melee at every distance (HumanAIComponent.cs:770-777). A
   bow-only archer then chased a melee stance it had no weapon for. The seat
   overrides Ranged, Melee and GoToPos through OverrideBehaviorParams and
   reasserts them twice a second, since RefreshBehaviorValues re-stamps the
   set whenever the formation re-applies a movement order.
3. Their ranged behaviour walked them toward a firing position they could
   never reach: legs at 3.18 m/s with the elephant standing still. Zeroing
   MovementInputVector did nothing, because the agent AI tick runs on the
   asynchronous thread and writes it again after ours. The seat now sets a
   scripted position at the seat itself, DoNotRun and deliberately no
   NoAttack, which is the flag that would silence the bow.
4. Our own per-frame teleport was the last and largest gate. An archer
   settles about 0.10 m from its frame, the seat corrected that every frame,
   and the engine reads position deltas as real movement: 30 m/s with the
   elephant motionless and its legs stopped. Nothing finishes a bow draw at
   that speed, which is why the draw stalled at 85 percent of its action and
   re-nocked about once a second at every range for nine rounds. A 0.15 m
   deadband fixed it outright: velocity 0.00 standing, the elephant's own 5.2
   to 5.8 m/s walking, action progress 1.00, one restart in a whole battle.

The mission-end hang came from a dump, not a re-read. Two battles froze with
the tick dead at over 250 fps and memory flat; procdump -ma on the second,
still frozen, gave the stack: Mission.CheckMissionEnd to set_MissionEnded to
UsableMachine.OnMissionEnded to StandingPoint.IsDeactivated = true, whose
setter spins while (HasAIMovingTo) { MovingAgent.StopUsingGameObject(); }
(UsableMissionObject.cs:129-133). A seat registered with AddMovingAgent alone
can never clear it. The machine and the seat both empty their seats first,
and UsableMachine.Disable is guarded the same way because it is a second copy
of the same loop.

Crew count is decided by capsule spacing, not deck area: two 0.37 m capsules
closer than 0.74 m shove each other back into gate 4. The howdah interior is
1.26 by 1.48 m, measured from the mesh's wall faces rather than its deck, so
it holds two archers in a line. The platform also moved 0.34 m back: its
centre had been taken from the face centres of a quad grid rather than its
extents.

The crew have their own troop, harad_howdah_crew, bow and two quivers, no
melee weapon, Bow 95 on its ladder cell, hidden from the Encyclopedia.

Reviewed with all seven lenses. Their findings are applied, including a NaN
that could still reach native, a no-op SetDetachableFromFormation with the
wrong polarity, a released archer left unable to advance or defend itself,
and a deployment-phase drop where an invalid-Z WorldPosition resolves to the
ground and would have dropped every archer off the howdah.

Research: Agent, HumanAIComponent, Formation, UsableMachine,
Research: UsableMissionObject, WorldPosition, BodyFlags on installed v1.5.3
Not-tested: siege or deployment battle, campaign battle, crewed elephant
Not-tested: killed mid-battle; translations for the troop name are owed
Save-compat: no new saved field; the crew are spawned per mission

### Fixes

#### fix(tools): v2.0.30 - improve_ctl watch and args path fixes

`ac1aa578`

watch pointed at a session's subagents folder also read every
workflow below it, and a killed workflow's journal leaves its
unfinished agents running forever, so one killed run made the watch
exit 1 for good. It no longer looks inside a folder named workflows
below --dir; a workflow is watched by pointing --dir at its own
folder.

args now refuses a --run-root, --scratch, --tmp or TAOM_IMPROVE_ROOT
that is not absolute (exit 2). Git Bash drops the backslashes of an
unquoted Windows path, leaving a drive-relative one that args used to
resolve against the current folder without a word.

Tests: a session folder with a stale workflow agent below it exits 0
and lists only its direct spawn; a tool_result for another tool call
leaves an ask pending (a mutant that cleared the ask on any
tool_result survived the suite before); each relative path is
refused.

#### fix(tools): v2.0.30 - integrate_branch dry run applies the union rule

`d24aff34`

--dry-run listed every append-only conflict as a union, so it reported
no hand resolution where the real merge stops with exit 2 (both sides
appended, and one also edited a line they share). It now runs git
merge-tree in diff3 style and tries the union on each append-only
path's merged blob, the same rule the merge applies to the file.

A git failure after git merge started (a locked index during git add,
say) exited 1, which promises that no merge started, and left the
merge in progress. It now exits 3 with the error and a note that the
merge is in progress, so exit 3 always means: read git status first.

Tests: a dry run over the edited-and-appended case exits 2; a failing
git add after the merge exits 3 with MERGE_HEAD still set; a heading
after a closed code fence gets its blank line (a mutant that never
closes a fence survived the suite before).

#### fix(improve): v2.0.30 - workflows report stops and never guess CLEAN

`bc160ca8`

review.js: the lead can return BLOCKED and a fix pass reports DONE or
BLOCKED. A lead or fix pass that stopped ends the item BLOCKED, and
one that committed nothing ends it FAILED, before a convergence round
reviews nothing. A lead verdict other than READY FOR COMMIT, and a
DEFECTS verdict with no findings listed, go to residual instead of
reading as CLEAN; the second no longer starts a fix pass with nothing
to fix. Convergence runs at max effort like deep-review's reviewers,
and a CRITICAL line in the Standards lens asks the lead to raise
deep-review's adversarial escalation for the orchestrator. An item is
num plus tag, so a tagged second review gets its own labels and a
repeated item is refused; an inherited key such as constructor is no
longer a lens id.

plans.js gives a safety-gate plan two cold reviews by default, keeps
every round's unfixed items, and never reads a review without its
lists as clean. fanout.js refuses a checker selection whose match is
not a list of objects or whose field no item schema declares as an
array, counts a result without that array as a failure, and keeps
each row's attribution. execute.js and draft-issues.js refuse a
repeated num; review, plans and execute results survive one item's
exception. Each script's header lists its args and item fields and
how top-level fields ride in an items object.

The tests now run every role on its own model id, reach the pool
bound with three items, cover each dead-agent path, check the scripts
are printable ASCII, and stub parallel() as the runtime has it. The
reviewers' mutation check catches all 26 of its mutants, up from 10.

#### fix(tools): v2.0.30 - improve_ctl watch catches hook-ask stalls

`d6742dd9`

watch now flags STALLED-ASK when an agent transcript's latest
PreToolUse hook ask has no later tool_result for that tool call: the
stall that held two review agents for 9.4 and 4.5 hours, which no age
limit tells apart from a long build. It works for direct spawns,
which have no journal. STALE applies to running workflow agents past
--stale-min, now 30 minutes by default, and an agent whose journal
key was started again is superseded instead of running forever.
Replayed on the three recorded stalls it flags each; on the live
session folder it raised no false ask.

args: an items object's fields stand unless a flag names them (pool,
model and maxRounds included), review items keep their own lenses
beside the computed ones, a review range that changes no file is
refused, and every revision is checked with --end-of-options (a base
of --output=<file> made git diff write that file). codex-prompt keeps
every STOP condition instead of the first six and checks its
revisions the same way. An unexpected error exits 3 with one line.
New tests cover the Status column anywhere in the header, a row
listed twice, 12 against 120, a dash or a path in an issue title and
a missing blank header line.

#### fix(tools): v2.0.30 - integrate_branch stages the merge, never commits

`459b63ad`

integrate_branch.py no longer commits. After a clean resolution it
stages the merge and prints the git commit -F line for the
orchestrator to run through Bash, so every PreToolUse commit gate
judges the merge commit; a commit made from Python bypassed them all.

The append-only union now reads the diff3 base: each side of a block
must start with the base lines, and the result is ours followed by
what theirs adds after them. A side that edits a shared line, such as
a fixed lesson line, goes to hand resolution; before, both versions
of the line were kept. The leftover-marker check runs on every merge,
also when another path needs a hand. The tool refuses a merge already
in progress (a stale one was committed under the new message), checks
that git left the named branch in progress, refuses a revision that
is not a commit (a leading hyphen was read as a git option), and runs
check_public_text.py on the message file before merging. A star in an
append-only glob stays inside one folder, a heading-shaped line in a
code fence gets no blank line, and an unexpected error exits 3 with
one line instead of a traceback read as exit 1.

#### fix(tools): v2.0.30 - public-text gate sees every local path form

`086abc57`

The public-text gate missed drive paths written with a forward slash
and every Git Bash drive path outside a repos folder (the home and
Steam folders among them), so a commit message or an issue body could
leak them. The local-path rule now takes a drive letter followed by
either slash, not inside a word and not a URL scheme, and a Git Bash
drive path not preceded by a word character, a dot, a slash or a
hyphen: URLs and PowerShell drives such as Env:\TEMP still pass. The
placeholder rule skips code spans, so a constant quoted from the code
is no longer a leftover placeholder. New tests cover each path form,
the allowed look-alikes and findings in line order across rules.

#### fix(hooks): v2.0.30 - deny commits over an untracked .js harness file

`e4c43e59`

check-claude-files-tracked.sh denies a commit while a file under
.claude/skills, agents, rules or hooks is untracked or gitignored, but
its extension filter named only md, sh, py, json and yaml. The /improve
skill now ships its Workflow scripts as .js files under
.claude/skills/improve/workflows, and an untracked one would have
passed the gate and never shipped. The filter now names js as well.

tools/test_hooks.sh 7b gains a row: an untracked .js file under
.claude/skills must be denied by name. It fails on the old filter and
passes on the new one.

#### fix(hooks): v2.0.30 - convergence fixes for the push guard work

`d01bddf1`

The convergence review of fff6d66e and 7b342ed5 verified every
review fix by mutating it and found two accuracy defects:

- tools/test_hooks.sh 7j checked only settings.json, while the docs
  said it covered every registration; a relative path in a skill's
  frontmatter hook passed the suite. It now also reads the skill and
  agent frontmatter command lines and requires CLAUDE_PROJECT_DIR
  there. A relative /freeze registration fails the row.
- Three places still said hooks always run in the main tree: the
  block-no-verify.sh comment, the git lookup in _pushjudge.py and
  the catalog's known gaps. A hook runs in the session's directory,
  which is the main tree unless an earlier Bash cd moved it (#690).

Also: the tool_input offset is 444 to 449 (0-based), the catalog
says which registration form each surface uses, and the reader's
test class is renamed after push_candidates (#680, #690).

#### fix(hooks): v2.0.30 - anchor hook registrations on the project dir

`7b342ed5`

All 27 hook registrations in .claude/settings.json named their
script by a relative path, .claude/hooks/<name>.sh. Claude Code runs
a hook in the session's current directory, and a Bash cd into a
project subdirectory persists across calls, so after one every hook
failed to start ("No such file or directory"), which the harness
treats as a non-blocking error. Every call then ran ungated: no
force-push guard, no commit gates, no confirm prompts. The #680
review proved it live: a trunk force push reached git after cd tools.

Each registration now names its script as
"$CLAUDE_PROJECT_DIR"/.claude/hooks/<name>.sh, as the /freeze and
/investigate frontmatter hooks already did. The hooks that read
project files already cd to the project root first, so no hook code
changes. tools/test_hooks.sh 7j fails on any unanchored registration
and runs validate-push's registered command through bash -c from
tools/ and .claude/hooks/, where it must still refuse a trunk force
push; before this change it answered rc 127 there. The hooks catalog
no longer says hooks always run in the main tree, and
hook-authoring.md and a lesson record the rule (#690).

#### fix(hooks): v2.0.30 - review fixes for the push guard work

`fff6d66e`

Fixes from the four-lens review of the #680, #681 and #689 work
(74942227..343c6f48).

Judge (#680, #689): a value option is now also read by the prefix
rule the force options use, on top of the old table, so the judge
never refuses less. git runs --e as --exec, so -f --e
git-receive-pack origin on a trunk checkout force-pushed the trunk
and is now refused. The ambiguous --rec counts as a value option
(git refuses it, so this only refuses more), and --end-of-options
takes no value.

No verdict: a payload that is no JSON object makes _shellwords.py
verdict print unread, so validate-push answers it from the raw text
(an ask on a force marker) instead of a silent allow. The raw scan
looks for the "tool_input": key in the first 2 KB only: the old
search was quadratic in the key's offset (17.7 s for a key after
256 KB), and with the key escaped a command ending in "tool_input
cut the scan past itself. --m is a force marker, so a mirror push
spelled by a prefix asks. The judge's budget is capped at 3.0 s,
since EPOCHREALTIME is the wall clock and can be set back. The push
mode and push_lines are deleted: only tests called them.

Registration: validate-push's timeout goes from 5 s to 10 s by
maintainer decision. The 3.0 s internal budget stays, so an overrun
still asks after about 3.3 s and the registration is only a
backstop.

Tests: a portable no-Python row (a sandbox _pybin.sh ending in
PYBIN="") now fails the review's mutant H10, which had passed the
whole suite. The bytecode test runs a fresh copy of the reader.
Every scratch repository sets push.default in its local config, so
a global push.default=matching no longer fails correct rows. 7h
sizes the validate-push rows by payload length and adds a 65,580
byte staged name list for the three commit gates (#681); a right
answer that misses a time bar runs once more, and 7i holds each row
to an explicit 4 s. 4e reads comments and the quoted bodies a shell
runs, takes any delimiter word, flags an operator that ends a line,
and fails a registration that names a script it cannot find (#681).

Docs: an unforced delete is not refused; --force-w force-updates
only the trunk it names, and counting a --force-if-includes prefix
is a deliberate over-block; the 250 KB overrun happened under load;
the here-string window is a document of 65,537 to 65,664 bytes;
str.lower() differs from bash on nothing under Python 3.14.
hook-authoring.md is back under its 12,288 B cap, its incident
narratives now pointers to their lessons.

A differential sweep against 343c6f48 (1,768 commands, 8,840 runs
per hook) found no push refused before and allowed now; its 30
exit-code differences are all value option prefixes now refused.

#### fix(hooks): v2.0.30 - scope the coarse push check to the command

`343c6f48`

When validate-push gets no verdict from its judge (no safe Python, or
the judge failed), it falls back to a coarse check: it asks the user to
confirm a push whose payload holds a force marker and allows the rest
with a note. That check scanned the whole payload, and the session_id
and transcript_path Claude Code sends before the command hold a UUID
that usually matches the marker, so in that state most pushes asked, a
plain one included (16 of 20 harness-shaped payloads in a probe, found
in the #680 review).

The check now scans the payload from its "tool_input" key on, with one
bash parameter expansion: no fork and no here-string, about 30 ms on a
2 MB payload. With no such key it scans the whole payload as before,
and a key written with a \u escape still asks through the \u rule. A
force push still asks.

tools/test_hooks.sh 7e sends a plain push and a force push in a
harness-shaped payload to the hook with no reader: the plain push is
allowed with the stderr note and the force push asks. The hooks
catalog describes the scope, and records that a mirror push spelled by
a prefix (--mir, #689) holds no marker, so this fallback allows it.

#### fix(hooks): v2.0.30 - refuse abbreviated force options, matching pushes

`fd1d5c17`

The force-push guard let through pushes that git 2.55 runs as a forced
update of both trunks (#689). git reads an unambiguous prefix of a long
option as that option, so --force-w and --force-with ran as
--force-with-lease, --mir and --m as --mirror, and -f --al and -f --b
as a forced --all, while the guard matched only the full spellings. The
refspec : with a force flag, or written +:, pushes every branch the
remote also has, and so does a forced push with no refspec under
push.default=matching; the guard read both as a push of the current
branch.

The judge now compares a long option's name before any = as a prefix of
--force, --force-with-lease, --force-if-includes, --mirror, --all and
--branches, from 3 characters. git refuses an ambiguous prefix such as
--f or --forc, so counting one as force only refuses a command git
refuses too. A forced : or +:, and a forced push with no refspec under
push.default=matching, are refused as a push of every matching branch.
The setting is read from -c push.default=matching between git and push
(git rejects the glued -cpush.default spelling) and, at most once a run
and only for a forced push with no refspec, from git config in the
hook's directory. Each change only refuses more: a plain push of :, a
push without force under matching, and --follow-tags stay allowed.

tools/tests/test_pushjudge.py adds the #689 shapes under both shell
tools, the lazy config lookup and a command-line row in a repository
set to matching; three pins that recorded the old answers for these
shapes move into the new table. tools/test_hooks.sh 7c adds the same
shapes on a feature checkout and in one configured for matching.
A differential sweep against 60d85ed7 (1,648 commands, 555 of them
seeded from these rules; 8,240 runs per hook under both shell tools,
on a trunk, a feature checkout and one set to matching) found no push
the old judge refused and the new one allows. Its 1,027 exit-code
differences are pushes allowed before and refused now, and its 275
message-only ones were refused both times and now name every matching
branch or every branch; each is one of the shapes above.

The hooks catalog lists what the guard now refuses and the gaps left
(push.default given through --config-env or GIT_CONFIG_*, upstream
mode, remote.<name>.push and remote.<name>.mirror), and the tooling
lessons record git's option-prefix grammar.

#### fix(hooks): v2.0.30 - judge force pushes in Python within a deadline

`a4ba6b0b`

validate-push judged each candidate line in bash at about 25
microseconds a word, so a force push to a trunk carrying 250 KB or
more of quoted text holding "push" outran the hook's 5 s registration.
The harness kills an overrunning hook, and a killed gate allows, so
such a push ran unjudged; at 2 MB every shape did (#680).

The judge now runs in Python, in the same start as the command reader:
.claude/hooks/_pushjudge.py, called through a new verdict mode of
_shellwords.py that takes the protected branch names as arguments and
prints block, warn or allow. It ports the bash judge and reads every
word as bash did: split at space, tab and newline only, git recognised
after lowering A-Z only, ASCII digits for an fd number, NUL dropped.
One difference is designed: a refspec pattern holding ?, [, ], \, (, )
or | beside * is taken to match every protected branch, which can only
refuse more, since git rejects such a refspec. The bash judge is
deleted, not kept as a fallback.

The hook gives the judge what is left of 3.0 s from its first line, so
an overrun, or a slow interpreter probe, now asks the user to confirm
instead of being killed. With no verdict at all (no Python, or the
judge failed) it asks when the raw payload holds a force marker and
allows anything else with a note; the payload's session id usually
holds such a marker, so in that rare state most pushes ask. Deleting
a trunk is still allowed, and a plain push to one still only warns.

tools/tests/test_pushjudge.py pins every 7c command with the verdict
and target the bash judge gave, and 99 lines where a natural Python
choice would read differently. tools/test_hooks.sh gains 7i: shapes A,
D and E from 250 KB to 2 MB under both shell tools must be refused
inside 4 s (the old hook took 4.4 s at 400 KB and was still running at
10 s on every 2 MB shape; the new one answers in under 1 s), and three
overrun rows must ask. The 7e NOREADER row now expects the coarse ask
where it expected rc 2, deliberately, since the bash judge it relied
on is gone; validate-push moves to 7e's own list and is checked for
its verdict call. A differential sweep against 74942227 (1,035
commands, 4,140 runs per hook, both tools, a trunk and a feature
checkout) found no push the old hook refused and the new one allows.

The session-start banner, hooks-catalog.md and hook-authoring.md
describe the new design, and the tooling lessons record the port's
traps.

#### fix(hooks): v2.0.30 - split hook text without here-strings

`143f0fa8`

Git Bash 5.3 on the desktop hangs forever on a here-string or
here-document whose text is 65,537 to 65,664 bytes: below that it
writes into a pipe, above it into a temp file, and in between it
blocks writing into its own pipe. Ten hook sites fed a command or a
git name list through a here-string, so a force push to a trunk, a
reset --hard or a broad add padded into that window hung
validate-push, block-dangerous-git and block-broad-git-add until the
harness killed them, and a killed gate allows the command (#681).

Each site now splits its text into an array under set -f, with IFS
restored on the same line, and loops over it; validate-push splits
its tokens the same way. There is no fork, and the loop runs in the
current shell as before. Empty lines are dropped, which changes no
verdict at any site. Process substitution, the fix the issue first
proposed, was measured and rejected: it forks on every call and
reads a pipe a byte at a time, 2.2 s on a 400 KB command.

tools/test_hooks.sh gains 4e, which refuses a here-string or an
expanding here-document in any script a hook registration runs, and
7h, which sends payloads sized into the window through the gates
and requires each answer inside 80% of its registration. The rule
and a lesson are recorded in hook-authoring.md and the tooling
lessons.

#### fix(hooks): v2.0.30 - keep the push line hint linear on dash runs

`d2fa6d08`

A third review of dad9b169 found no verdict change (0 exit-code
differences in 2,880 hook runs per ref) and one regression it
introduced: FORCE_HINT (-\S*f) backtracked from every dash of a run,
so a commit message of 40 to 72 KB of dashes took validate-push to
2.2 to 14.4 s, past its 5 s registration, where a killed gate fails
open. It is now -[^\s-]*f, which matches exactly where the old
pattern did (0 disagreements on 100,000 random strings) and runs in
linear time: 72 KB of dashes takes 405 ms from the PowerShell tool.

The review's doc slips are corrected: the hooks catalog recount
(graphify added two registrations, the fold removed two), the open
timing windows and their measured bounds, the cost of the hint's
over-matching on prose such as trade-off or C++, a test comment and a
figure in the new lesson. The report records the third review and a
Git Bash 5.3 here-string hang at about 64 KB that predates plan 027;
the maintainer chose to ship and track the open timing items.

#### fix(hooks): v2.0.30 - judge the push lines that could force first

`dad9b169`

A second convergence pass on 5f256f70 and the plan 027 merge found
six LOW defects and no verdict change (5,640 reader cases, 1,280 hook
runs). One was a slowdown the shortest-first sort introduced: a long
refused push line now waited behind shorter long commit messages
holding push, and passed the 5 s registration at about 250 KB (a
killed gate fails open), where the order before judged it first.

_shellwords.py now puts lines that could force (a short option
holding f, --force*, --mirror, a +refspec) before lines that cannot,
shortest first within each group. It is an ordering hint only: every
line is still judged. A 250 KB refused push line followed by two
shorter messages went from 6,287 ms back to 3,351 ms, and a long
message before a short force push stays fast (400 KB: 418 ms, base
3,733 ms).

The merge's docs and tests are brought in line with the review:
- hooks-catalog.md: why the registration count changed, the 4c and 4d
  counts with the graphify gate, and its row as an allow-list that
  answers ask on an overrun.
- test_hooks.sh 7f requires one registration per tool; the 7e own
  list is for shell hooks that are not git gates.
- The deep-review report and REVIEW-LOG record the second convergence
  and keep one window open for the maintainer: 300 KB or more inside
  the push command itself is still slower than before plan 027.
- One lesson on reordering work in front of an early exit.

#### fix(hooks): v2.0.30 - judge the shortest push line first

`5f256f70`

The plan 027 convergence pass left one timing window open: on
git commit -m "<long text holding push>" && git push --force to a
trunk, validate-push judged the long commit line first and read every
word of the message as a refspec. At 400 KB that took 5.2 s of the 5 s
registration, and a killed gate fails open; base finished in 3.6 s.

_shellwords.py push now returns its lines shortest first. The gate
stops at the first refused line, so the short force push is judged
before the long message. Any line still blocks; only the stop comes
earlier. The same payload now takes 294 ms at 400 KB and 358 ms at
800 KB (base 3,588 ms and 7,264 ms), refused in every run.

Unit test test_shortest_line_first, red first. Reader tests 59 OK.

#### fix(hooks): v2.0.30 - convergence fixes for plan 027

`747b6dae`

The convergence pass on the plan 027 review fixes found four defects,
all confirmed against the code:

- validate-push dropped a push when its quote-blind split cut inside
  a quoted value (X="a;b #c" git push --force ...) after a line that
  left a quote open: refused at 96afb6fb, allowed after the review
  fixes. A # comment is now dropped only when no quote occurs anywhere
  earlier in the text, not just in its own piece.
- The argument-boundary pass ran a quadratic shlex split on every
  quoted segment holding push; it now runs on segments up to 4 KB.
- $null =git, [int] $x = git, $a.b=git, $a[0]=git and $x, $y = git
  hid git from the gates; the reader now reads an assignment's left
  side as PowerShell's parser does.
- A 7c comment claimed JSON escapes the rows did not send; both
  spellings are now sent.

A 612-shape base-versus-fixed sweep under both tool names loses only
the named trailing-comment relaxation. Reader tests 58 OK, hook suite
798 passed, TAOM.Tests 10767 passed, 0 failed. The report gains a
Convergence section, the RCA rows 16 to 19, and one lesson.

#### fix(hooks): v2.0.30 - review fixes for plan 027

`64b9f3a6`

The deep review and Codex pass on plan 027 (96afb6fb..05dbc0d4) found
validate-push refusing less than at base in eight shapes, PowerShell
statement forms that hid git from three gates, invented commit text
for piped producers, and timing near the 5 s registration.

validate-push now reads every candidate split from one
_shellwords.py push run and judges positionals with and without the
option value skip. The reader handles assignments, the . operator,
${name}, typographic quotes and value statements. Report, RCA, Codex
prompt, lessons and catalog updates are included.

This is the state the convergence pass reviewed; its fixes follow in
the next commit.

#### fix(hooks): v2.0.30 - leave an interrupted build or test unmarked

`cfd3b48f`

mark-verification-run.sh touches .claude/logs/.verification-ran after
a dotnet build, dotnet test or build.ps1 call, on PostToolUse and
PostToolUseFailure alike, which mutes the Stop hook's "you edited C#
and never verified" reminder. A failed build still counts, since its
output is evidence. An aborted call does not: the command never
finished and there is no result.

Evidence, read from the installed Claude Code 2.1.241 binary: the
PostToolUseFailure input schema carries an optional is_interrupt
boolean beside tool_name, tool_input, tool_use_id, error and
duration_ms, and the call site sets it when the thrown error is an
abort or cancel error, or when the call's abort signal fired with the
server-fallback reason. So a payload with "is_interrupt": true now
exits before marking. If the field is never true in practice nothing
changes; if it is, the only effect is one more Stop reminder.

UNVERIFIED: whether a timed-out command sets is_interrupt. The shell
path appends "Command timed out after ..." to stderr, and nothing read
so far shows it also counts as an abort, so a timeout still marks.

The hook's split is now _shellwords.py segments, shared with
validate-push.sh, so a PowerShell command splits as its Bash twin: a
dotnet test inside a script block marks, a here-string that only holds
the words does not, and a trailing # comment is dropped. The first
word is read without quotes, so PowerShell's .\build.ps1 still marks.
tools/test_hooks.sh 7d gains 7 rows; the suite is at 705 passed, 0
failed. Part of plan 027.

Not-tested: a live interrupted tool call through the real harness (owed after merge)

#### fix(hooks): v2.0.30 - close validate-push glob, option and comment gaps

`3a061406`

validate-push.sh is the only force-push guard. The plan 011 final
convergence review found shapes it still let through in either shell,
and one false refusal. It now:
- judges a pattern refspec (refs/heads/*, +refs/heads/*:refs/heads/*,
  refs/heads/bannerlord-*) as every protected name it matches. The
  plan 011 probe against a scratch bare repo showed each glob shape
  really force-updating the remote's branches.
- reads a destination heads/<name> as <name>, as git does.
- never takes the value of -o, --push-option, --repo, --receive-pack
  or --exec for the remote. Run on a trunk, git push --force -o
  ci.skip origin passed because ci.skip was read as the remote.
- sees git named by a path or in capitals (GIT, git.exe, a Windows
  path), glued to a brace, or inside a bash backtick substitution.
- reads PowerShell through the shared _shellwords.py reader: script
  blocks, the & call operator, backtick escapes and continuations.
- ignores a trunk named only in a trailing # comment, in both shells
  (git push --force origin feature # <trunk> later was refused).

The quote-aware split is now _shellwords.py segments, shared with
mark-verification-run.sh. The quote-blind split reads the reader's
text and the raw command both, so reading PowerShell never makes the
gate refuse less than before: a Start-Process argument list, & ("git")
push and a refspec in parentheses stay refused. It cuts at ; & |
before it drops a # comment, per segment, so a # inside a quoted value
cannot hide a later push on the same line.

Maintainer decisions 30 and 60 still hold: the gate is force-push only
and protects exactly master, main, bannerlord-1.4.5 and
bannerlord-1.5.x. tools/test_hooks.sh 7c gains 47 rows, among them
eleven that pin shapes refused before this change. The suite is at
698 passed, 0 failed. Part of plan 027.

Not-tested: a live PowerShell tool call through the real harness (owed after merge)

#### fix(changelog): v2.0.30 - final convergence fixes for plan 020

`5b242cee`

A second convergence review of 971d8e97 found four LOW defects; none
changes runtime behaviour.

- The generator docstring now says /release leaves no commit that
  lands mid-release out of every section. It used to say such a commit
  falls into the next section, which holds only after the release
  commit.
- The D1 parent check now reaches the C4 lesson's Prevent line, the
  RCA's C4 row and the report's C4 resolution. The RCA gains rows for
  D1, D2 and E1 to E4.
- The first convergence claimed no edited file is read by scan.sh;
  scan_skills and scan_hooks read release/SKILL.md and
  session-start.sh. The sentence is corrected.
- test_hooks.sh 5b2 selects any non-comment taom_pybin_degraded call,
  skips it only when jq is the third argument, and fails when it
  selects nothing. RED: with check-doc-config-drift's call rewritten to
  the if form and its name dropped from the banner, the old block
  passed and the new one failed that gate.

module-taom.md's release step now uses the labelled release subject and
tags the release commit by SHA, matching release-process.md.

Tests: test_hooks.sh 373 passed, 0 failed; generator tests 33 OK;
lint_docs --fail-on-drift exit 0; dotnet test 10629 passed, 0 failed,
2 skipped.

#### fix(hooks): v2.0.30 - final convergence fixes for plan 011

`764ab098`

validate-push.sh anchored on the first `push` word and returned when no
`git` preceded it. An apostrophe in a comment or heredoc line opens a
quote that never closes, so the quoted split glued the next line into
that segment, and a following `git -C "E:/R&D/TAOM" push --force origin
bannerlord-1.5.x` passed (rc 0) under both tools; the jq-only fallback
failed the same way. The hook now anchors on the first `push` token with
a `git` token before it.

The current branch is resolved once per run and a segment already judged
is skipped: 100 no-refspec push lines took 7.9 s against the 5 s
registration, and a killed gate fails open. They now take about 0.4 s.

7c gains the two defect rows, a tool-tagged table that pins each shell's
quote escape (a single-escape mutant passed every earlier row), and a
timing row. The over-block after an unclosed quote stays by decision:
ending a quote at a newline would let a quoted value spanning lines cut
git from push. Corpus parity over 596 payloads under both tool names:
only the defect payloads change, rc 0 to 2. Comments, the hooks catalog
row, the review record and CHANGELOG follow; the out-of-range probe
findings move to plan 027.

#### fix(seams): v2.0.30 - convergence fixes for plan 026

`aeb04bc4`

The convergence pass on 12b1c200 found no code or test defect and two
LOW accuracy errors in the review records, both fixed:

- CHANGELOG: the fix entry pointed to the warden promotion smoke
  "above"; it is in the refactor entry below.
- RCA top-line: the seven findings now count P1 (the missing issue)
  and P2 as one doc gap across two feature docs, matching the table.
  The report's STANDARDS line now names Agent 1's notes as its own
  section does (note 1 is S1 and S2, note 2 is S3).

The report gains a Convergence section. Docs only; full suite
10707 passed, 2 skipped, 0 failed.

#### fix(release): v2.0.30 - prune only what the tag and its build own

`3c747f4f`

The final convergence pass found that the D1 fix to the /release
Phase 8 orphan-removal step would delete files the repo cannot
recreate and still ship stale binaries.

- TAOM.Dependencies outside bin/ is no longer pruned. Its MCM UI
  assets (55 files: AssetPackages, EmAssetPackages, GUI and the two
  ModuleData language trees) exist in the install only. The TAOM
  rule now leaves RuntimeDataCache to the packager. D1's premise is
  corrected in the review report.
- bin/ keeps the names the tag tracks under _Module/bin/ (2 and 44)
  plus what the build writes, taken from each project's
  project.assets.json runtime assets, and removes the rest. The old
  text said bin/ holds only untracked DLLs.
- Directory.Build.props: the stamp comment also names assume-unchanged
  paths as not counting.

A read-only walk of the live install under the new rule: 12 TAOM
leftovers outside bin/ (115 RuntimeDataCache files left alone) and 3
retired BehaviorTree DLLs in TAOM/bin/; nothing in TAOM.Dependencies.
The RCA gains rows for convergence findings D1 to D3 and D-A to D-C,
and the review report a Final convergence section.

Tests: test_package_release.py 57 OK; TAOM.Tests 10317 passed,
2 skipped, 0 failed; lint_docs --fail-on-drift exit 0.

#### fix(seams): v2.0.30 - review follow-ups for plan 026

`12b1c200`

The six-lens deep review and the Codex pass on the seam refactor found
no reachable behaviour change. This fixes the five small defects they
confirmed and applies two behaviour-preserving improvements:

- The refuge's per-row peace war check keeps the prisoner when the
  refuge or its faction is missing, as the old walk did; it answered
  "release", which only the start-of-walk count guard prevented.
- The promotion's rename and enrol seams act on the hero the create
  seam returns (MintedHero: id plus opaque handle), not on a second
  lookup by id.
- The eight pure FortificationSearch tests move to the untagged
  FortificationSearchTests, so hosted CI runs them.
- The raid scan sizes its candidate list up front.
- Two doc comments say a dropped prison row's captor can be none; the
  Supply Lines and Field Camp docs name the new tests and types.
- New tests pin the winner-only name render and the hero hand-off.

Report: docs/reviews/deep-review-026-seam-decision-logic-2026-09-24.md
RCA: docs/reviews/rca-seam-decision-logic-2026-09-24.md
Lessons appended to gamemodels-services, testing-qa and misc; Codex
review logged in REVIEW-LOG.md.

Not-tested: the changed seam bodies (the refuge war check, the raid
scan's list size, and the mint create, rename and enrol) run only in
game; the warden promotion and raid smokes cover them. Full suite:
10707 passed, 2 skipped, 0 failed.

#### fix(build): v2.0.30 - untracked files always mark the stamp dirty

`9cedb92d`

Deep review findings 6 and 7 for plan 017. The stamp's git status
honoured a user's status.showUntrackedFiles=no, so a new untracked
source file could build a clean-looking stamp, and the pathspec left
out GameReferences.targets, which the projects import on trunk.

The orchestrator made the Directory.Build.props edit under the bypass
granted for plan 017: git status now passes --untracked-files=normal
and GameReferences.targets joins the pathspec, with two comment lines
saying so. The release skill Gotchas and the CHANGELOG name the new
path, and the CHANGELOG drops its known-limitation bullet.

Probe, with status.showUntrackedFiles=no injected through
GIT_CONFIG_COUNT and the props edit masked by assume-unchanged: an
untracked Main/_probe017_untracked.cs built
build.20260925-185259Z+d5033e4e897f3a0500ec5808ba495d8217211c42.dirty;
with the file deleted the stamp had no suffix. The review report
records both under "Orchestrator follow-ups".

test_package_release.py: 57 OK. TAOM.Tests: 10317 passed, 2 skipped,
0 failed. lint_docs.py --fail-on-drift exits 0.

#### fix(release): v2.0.30 - convergence fixes for plan 017

`d5033e4e`

The orphan-removal step in Phase 8 and release-process.md compared the
whole install with the tag's Main/_Module tree. The tag never tracks
bin/ build output, so following it literally deleted TAOM.dll and its
runtime DLLs, and TAOM.Dependencies was never compared. The step now
compares outside bin/ only and maps both module folders to their
_Module roots.

The shallow-history skip in the predates test could never fire:
rev-parse without --verify prints an unresolvable argument to stdout.
A depth-1 clone (CI's checkout) failed the test; it now resolves the
parent through resolve_commit and skips there.

SKILL.md and CHANGELOG now say the gate refuses a missing TAOM or
TAOM.Dependencies, which is what the code checks.

#### fix(hooks): v2.0.30 - record failed runs, plan 011 settings

`18961c1e`

A command that exits non-zero raises PostToolUseFailure, not
PostToolUse, so a failed build or test never touched
.verification-ran and the verification Stop reminder fired after
every red run. settings.json now registers mark-verification-run.sh
on PostToolUseFailure for Bash and PowerShell (timeout 5), the one
settings change the plan 011 review asked the orchestrator for.

tools/test_hooks.sh 7c checks that validate-push (PreToolUse) and
mark-verification-run (PostToolUse and PostToolUseFailure) are
registered for both shell tools; run against the previous
settings.json it reports the two PostToolUseFailure rows missing.
A 7d row feeds the hook a failure payload and expects a mark. The
hooks catalog is recounted (28 scripts, 29 settings.json
registrations across 9 events, 5 in skill frontmatter, 34 in
total) and its mark-verification-run row names both events. The
review report and the plan 011 CHANGELOG entry record the change;
the Codex prompt for the review is committed with them.

Verified: bash tools/test_hooks.sh 506 passed, 0 failed;
dotnet test TAOM.Tests Failed: 0, Passed: 10629, Skipped: 2;
python tools/lint_docs.py --fail-on-drift exit 0.

#### fix(docs): v2.0.30 - convergence fixes for plan 016

`35f212c6`

The convergence pass over the review-fix commit found four LOW doc
defects and two wording slips; all confirmed against the worktree.

- mcp-servers.md: scope the deny-list derivation to the pinned git and
  filesystem servers, and note serena's editing tools are also
  readOnlyHint false and not denied yet (open decision).
- mcp-servers.md: the pin rule covers the .mcp.json project servers;
  the user-level servers are machine-local and unpinned.
- INDEX.md: name .codex/config.toml among what ignores the env vars.
- LESSONS-LEARNED.md: recount 199 to 202 and 921 to 924.
- development-machines.md and REVIEW-LOG.md: wording fixes.

#### fix(changelog): v2.0.30 - convergence fixes for plan 020

`971d8e97`

The convergence review of 6e4bfedb found three defects; all confirmed.

/release now checks, before tagging, that the release commit's parent
is the commit the generator's range ended at, and stops to ask if not.
That closes the window between the Phase 6 HEAD check and the commit.
The generator docstring, the tools/README row and Phase 4 said the
range-end commit is the one tagged; it is the release commit's parent.
The Phase 7 confirmation describes the release commit, not HEAD.

test_hooks.sh 5b2 matched the gate names in a session-start.sh comment
while the banner printed short names. It now reads only non-comment
lines and selects gates by their taom_pybin_degraded call site, and the
banner prints the hook file names. Proved red on the old banner and red
when one name leaves the echo line only.

Verified: test_hooks.sh 373 passed, 0 failed; generator tests 33 OK;
lint_docs ai_dashes 0; dotnet test 10629 passed, 0 failed, 2 skipped.

#### fix(hooks): v2.0.30 - convergence fixes for plan 011

`65b405e4`

The convergence review of c80c4108 found four defects; all held.

validate-push.sh split a command at ; & | without regard to quotes,
so a quoted -C, -c or -o value holding one cut git from push: git -C
"E:/R&D/TAOM" push --force origin bannerlord-1.5.x returned rc 0,
where 43e6780e refused it. It now judges two splits and blocks when
either does: the quote-blind one (keeps bash -c "git push ...; x"
refused) and one outside quotes only, with each shell's escape. With
jq and no Python the second keeps whole lines, which over-block.

A refspec glued to its redirection (bannerlord-1.5.x>/dev/null, >&2)
was dropped whole; the text before < or > now counts unless it is an
fd number or PowerShell's *.

Three comments still called the JSON block the only Stop output
Claude reads, and the test_hooks.sh section 4 comment misdescribed
its own check; all now say what the code does. The catalog row and
the lesson drop the claim that the split only over-blocks.

7c gained eight rows, each rc 0 before the fix. test_hooks.sh: 501
passed, 0 failed; dotnet test: 10629 passed, 0 failed, 2 skipped.

#### fix(release): v2.0.30 - review follow-ups for plan 017

`db1a7166`

Deep review (seven lenses) and Codex on plan 017 (#658). The release
gate failed open four ways and read a missing .dirty flag as proof of a
clean tree; all fixed with the failing tests written first.

- package_release.py --require-build reads every bin/<platform>/ copy
  of TAOM.dll and TAOM.Dependencies.dll (the Win64 one must exist),
  casefolds module names, refuses an empty value, a requested module
  missing from --source, a rev whose Directory.Build.props predates
  the dirty-flag target, and an unreadable DLL. One stamp regex.
- Release skill Phase 8 and release-process.md: the gate proves the
  DLLs only, so compare the install with git ls-tree before packaging;
  step 1 no longer offers a worktree git refuses.
- BuildStampReport comment and test credit the .NET SDK for the SHA
  suffix; test rename; crash-report changelog line; #658 referenced.

Report: docs/reviews/deep-review-017-build-identity-dirty-flag-2026-09-24.md
RCA: docs/reviews/rca-build-identity-dirty-flag-2026-09-24.md
Deferred: git status --untracked-files=normal needs Mike to approve the Directory.Build.props edit

#### fix(docs): v2.0.30 - review follow-ups for plan 016

`1ce8c370`

Deep review (four lenses) and Codex on plan 016 (#657) confirmed ten
documentation findings, nine LOW and one NIT; all fixed here.

- Restore note uses git restore --source=b2e387db, which PowerShell
  5.1 cannot re-encode, and covers new worktrees and switching to
  bannerlord-1.4.5, which still tracks the settings file.
- The .vscode MCP example and the kingdom-voices .mcp.json snippet
  carry the pins; mcp-servers.md states the pin and deny-list rule.
- ModuleData MCP activation, the development-machines recount, the
  MCP write-tool lesson and the agent manual Build row corrected.
- README clones straight onto bannerlord-1.5.x; plan 016 CHANGELOG
  entries moved under 2026-09-25 and linked to #657.

Report:
docs/reviews/deep-review-016-repo-hygiene-pins-readme-2026-09-24.md
RCA: docs/reviews/rca-repo-hygiene-pins-readme-2026-09-24.md
Lessons: three in docs/reviews/lessons/build-tooling-workflow.md.

#### fix(changelog): v2.0.30 - review follow-ups for plan 020

`6e4bfedb`

The CHANGELOG generator and the /release flow around it, after a
six-lens deep review and a Codex adversarial review of plan 020.

- A commit body can no longer add a heading to CHANGELOG.md. Body
  lines Markdown reads as a heading are escaped, so an example
  "## v2.0.32 (" in one release's body no longer blocks the v2.0.32
  release as a duplicate. Issue references such as "#622:" at a line
  start are untouched; output for v2.0.29..v2.0.30 and for the pending
  v2.0.30 range is byte-identical to before.
- The generator's summary names the commit its range ends at.
  /release now checks HEAD is still that commit before committing and
  tags the release commit by SHA, so a commit another session lands
  mid-release goes into the next section instead of none.
- The refusal also catches a hand-written ### or #### entry above the
  releases, and says to fold such entries into the release note.
  --version with a trailing newline is refused.
- The degraded-toolchain banner at session start names all five
  python-only gates (it said four and left out the commit-label
  gate); tools/test_hooks.sh 5b2 now derives that list from the hooks.
- The completeness lens diffs CHANGELOG.md against the review base,
  so a committed hand edit is caught, not only an uncommitted one.
- Instructions that still said to update CHANGELOG by hand now say to
  write the commit body: three feature recipes, the feature-doc
  template, the commit-split routing row, new-creature-mount, two lens
  prompts, git-and-commits, the v1.5.2 owed list, and a rule that
  claimed a deleted hook enforced it. release-process.md runs in the
  skill's order (generate, then the release note), says non-merge
  commits, and links the v2.0.12 crash reports in the archive.
- Tests: 11 new generator tests, including parity between the label
  pattern and the subject gate. Five dashes on touched lines replaced.

Not changed here: elephant.md:687 (another session has uncommitted
edits to it) and the executor wrappers under plans/ (orchestrator).

Checks: generator tests 33 OK; test_hooks.sh 373 passed, 0 failed;
tools suite 1952 run, the 3 known failures only; TAOM.Tests Failed 0,
Passed 10629, Skipped 2; lint_docs --fail-on-drift exit 0.

Report: docs/reviews/deep-review-020-changelog-at-release-2026-09-24.md
RCA: docs/reviews/rca-changelog-at-release-2026-09-24.md

#### fix(hooks): v2.0.30 - review follow-ups for plan 011

`c80c4108`

Deep review (six lenses) and Codex of 43e6780e confirmed 15 defects.

validate-push.sh took the last word of a line as the refspec, so a
trunk force push with a tail (2>&1 | tail -5, && echo done, ; git
status, # note) or a second refspec passed, as did --force --all. It
now judges each command of a line, skips redirections, judges every
refspec, and refuses --all with force and --mirror. Quoted text that
reads as a trunk force push stays refused by design.

The four Stop hooks exited at the top on stop_hook_active, so a streak
Claude ended in the continuation kept its marker and muted the next
one. The guard now sits just before the block.

mark-verification-run.sh splits in Python with each shell's escape:
the bash loop took 5.4 s on 100 KB (5 s registration), marked a
backtick-escaped PowerShell mention, missed a command on a non-final
CRLF line, and stopped marking an env prefix.

test_hooks.sh 7a now checks exit status, stderr, marker clearing and
re-arming for every Stop hook; 7c and 7d gained the shapes above.
493 hook checks pass; dotnet test 10629 passed, 0 failed, 2 skipped.

Report and RCA:
docs/reviews/deep-review-011-stop-reminders-and-trunk-guard-2026-09-24.md
docs/reviews/rca-stop-reminders-and-trunk-guard-2026-09-24.md

Not-tested: live checks A and B; a failed build still does not mark
until mark-verification-run.sh is registered on PostToolUseFailure
(settings.json, listed in the report for the orchestrator).

#### fix(rules): v2.0.30 - convergence fixes for plan 021

`e809f258`

The convergence pass on d4e6273a found four LOW defects; all four were
confirmed against the worktree.

architecture.md's service rule allowed TaleWorlds access outside an
adapter only through seams, which forbade the value types ADR-007 and
the Standards lens exempt (RefugeService.Dismantle builds a TextObject
outside any seam). It now excepts the value types, as the lens does.

decompiled-code-analysis.md registered the concrete service by default
while the Phase 4 patch sample still resolved IFeatureService, which
DryIoc throws on and the sample's empty catch swallows. The sample now
resolves FeatureService, and the registration comment says the
interface line replaces the concrete one.

CHANGELOG and REVIEW-LOG now split the owed ADR text between the
orchestrator (O3, O5, O6) and Mike (O1, O2), matching the report. The
RCA's agent lists now match the report's Source column (C9 missed by
Agent 4, C6 found by Agent 6).

The report gains a Convergence section. Full suite 10,629 passed,
2 skipped, 0 failed.

#### fix(rules): v2.0.30 - review follow-ups for plan 021

`d4e6273a`

Deep review (Standards, Completeness, Data flow, Design) and Codex on
plan 021. think-before-coding.md no longer forbids the narrow hook
interface the other rules allow (Codex P2 1). The Standards lens names
both ADR-007 exceptions (value types and seams) and says TaleWorlds,
not engine. The old IHook one-liner, the always-both-interfaces Phase
4 procedure in decompiled-code-analysis.md, two audit prompts without
the seam exception, a stale engine version and an uncomputed count
are brought in line; the CHANGELOG credits the orchestrator commits.

ADR wording still inconsistent (CampaignTime listed as sealed, the
ADR-008 and ADR-002 checklists) and two decisions (private helpers in
seam bodies, decision logic in the cited seams) are listed for the
orchestrator and Mike in the report.

Full suite: 10629 passed, 2 skipped, 0 failed.

Report: docs/reviews/deep-review-021-architecture-rule-amendments-2026-09-24.md
RCA: docs/reviews/rca-architecture-rule-amendments-2026-09-24.md

#### fix(hooks): v2.0.30 - Stop reminders reach Claude, guard both trunks

`43e6780e`

Plan 011. The four Stop reminders (verification, deep review, version
tag, CHANGELOG) printed to stderr and exited 0, which Claude Code sends
to the debug log only, so none ever reached Claude. They now print a
JSON decision block through the new _stop_reminder.sh, stay silent when
stop_hook_active is true, and fire once per streak; check-deep-review
gains the streak marker it lacked.

validate-push.sh now refuses a force push to bannerlord-1.5.x as well
as bannerlord-1.4.5 (named, not bannerlord-*, per D30), judges every
line of a multi-line command instead of the first (D38), and runs for
the PowerShell tool too. mark-verification-run.sh marks the canonical
dotnet test command its env-prefix strip used to drop, and no longer
splits inside quotes (D41); it is also registered for PowerShell.
suggest-compact.sh and notify-test-results.sh are deleted with their
registrations, tests and catalog rows (D42): their stderr reached no
one. tools/test_hooks.sh 7a, 7c and 7d pin the new behaviour.

Not-tested: that Claude Code delivers a Stop hook's block reason to
Claude, and that it fires validate-push.sh for a PowerShell tool call;
both need Mike's live check in a real session.

#### fix(composition): v2.0.30 - kernel test anchors follow 009's localized reports

`0f3af957`

Merging plan 018 after plan 009: 009 now reports patch failures with
localized TextObject phase names, so 018's kernel test anchors moved
from ReportPatchFailures("...") to the TextObject form. The runner
calls themselves were already in place and in order. Full suite:
10465 passed, 0 failed.

#### fix(tactics): v2.0.30 - convergence fixes for plan 022

`1b21a214`

The convergence pass on daeb127e found no runtime defect and four
wrong statements the review fix added. Each was re-checked against
the v1.5.3 decompile or the files:

- IOOBCaptainAutoAssigner: OrderOfBattleVM can be built anywhere
  Game.Current runs, not only in a mission.
- IHeroCombatAdapter.Equipment snapshots the equipment the adapter
  was built from, not always BattleEquipment.
- Lesson and RCA: SaveConfiguration goes through SetFormationInfos,
  which writes one of four saved lists, not "_formationInfos".
- Review report: the 12 leftover LF language rows are 3 ASO rows
  (#604) and 9 taom_behavior rows (#608).

Suite: Passed 10335, Skipped 2, Failed 0.

#### fix(binding-gate): v2.0.30 - convergence fixes for plan 008

`1f504757`

The convergence pass on 37306bca..aa59f68e found one LOW and three
NIT defects; each was verified before it was fixed.

- The zero-match triage in verify-bindings Step 2 misnamed MSTest's
  discovery warnings. "Unable to load types from the test source"
  means some of the DLL's types failed to load (discovery goes on
  with the rest); a DLL that does not load at all reports "Failed to
  discover tests from assembly". The skill, the CHANGELOG bullet, the
  report's R2 fix line and the RCA's R2 row now name both.
- The R8 evidence now counts 4 RepoPaths test files (5 with this
  one), the follow-ups count 34 private TAOM.sln walkers, and the
  REVIEW-LOG's pre-decision test_hooks.sh count is 289.
- The decisions report gains a Convergence section and no longer
  says the convergence pass was not run.

Full suite: Failed 2, Passed 10245, Skipped 2 (the two known
live-Armory tests; this branch predates a39a9c86).

#### fix(specres): v2.0.30 - convergence fixes for plan 001

`0935533a`

The RCA top-line said six findings were fixed on the branch, but its
own table marks five (F2, F3, F5, F6, F7), and it filed F4 under the
plan and prose when the table lists it under Process. Correct the count
and label the findings that wait for Mike or are only recorded.

Append the convergence pass to the deep-review report. Full suite:
10318 passed, 2 skipped, 0 failed.

#### fix(cache-rebuild): v2.0.30 - convergence fixes for plan 025

`97344cf5`

The lessons recurrence note said the config-test count went from 20
to 22, but the review-fix commit removed one of those tests, so the
note now says 22 at 032481cc. The CHANGELOG credited the 41258657
provenance to the feature doc as well as the binding catalogue; only
the catalogue carries it, so the sentence is split by file.

The review report gains a Convergence section recording both
findings and the full suite run.

#### fix(tactics): v2.0.30 - review follow-ups for plan 022

`daeb127e`

Seven-lens deep review plus Codex (P2 1, P3 2) of the OOB Auto-Assign
wiring. One MEDIUM: in a siege assault vanilla spawns every agent
without a horse, but the boundary classified candidates from campaign
BattleEquipment, so a companion who owns a horse read as Cavalry and
was never placed on the foot formations a siege offers. The boundary
now passes the agent's SpawnEquipment through a new HeroCombatAdapter
overload (RED first: HeroCombatAdapterTests).

LOW fixes: tests for the boundary's early returns, the planner's
50-point fits, each result message's text and the overlay DI graph;
corrected "sealed", co-op, save, hero-troop and "companions" wording;
reflection-site labels moved to lines 60/61; the 36 seeded language
rows restored to the files' CR CR LF terminators; one stray blank line.
Planner's used sets are bool arrays (behaviour preserved).

Seven design questions are left for Mike in the report.

Report: docs/reviews/deep-review-022-order-of-battle-auto-assign-2026-09-24.md
RCA: docs/reviews/rca-order-of-battle-auto-assign-2026-09-24.md
Tests: full suite 10335 passed, 2 skipped, 0 failed
Not-tested: siege placement in game (needs a live Agent); owed check

#### fix(binding-gate): v2.0.30 - review follow-ups for plan 008

`aa59f68e`

Round-two deep review and Codex of the maintainer decisions commit
(2ca0805b..37306bca, #652). All three decisions hold; eight LOW or NIT
findings were confirmed and are fixed here:

- The hooks catalog and CHANGELOG said binding-gate.runsettings fails
  a skipped test. It fails an Assert.Inconclusive and a zero-match
  filter; an [Ignore]d test still reports Skipped (Codex P3).
- verify-bindings Step 2 triages a zero-match run by its command: on
  the Step 1 command it is a finding (DLL load or lost category).
- BindingGateRunSettingsTests uses RepoPaths.RepoPath instead of its
  own repo-root walker (2 of 2 before and after, mutation red).
- The first RCA, report and REVIEW-LOG now record that F2, D1 and the
  hook header fix lapsed with the revert, that F13 is moot and that
  there is no 1.4.5 port (both per #652), and the three earlier plan
  008 CHANGELOG headings carry #652.

Full suite: Failed 2, Passed 10245, Skipped 2, Total 10249 (the two
known live-Armory tests).

Report and RCA, both in docs/reviews/:
deep-review-008-binding-gate-no-silent-skips-decisions-2026-09-24.md
rca-binding-gate-no-silent-skips-decisions-2026-09-24.md

#### fix(specres): v2.0.30 - review follow-ups for plan 001

`8ea31505`

Six deep-review lenses and Codex (gpt-6-astra, ultra) reviewed
a39a9c86..4263535a: no CRITICAL or HIGH. Fixed here:

- CHANGELOG and feature doc: the null-local load covers a behavior
  record without the balances key (defensive; no build wrote one),
  not a save older than the feature, which has no record and never
  reaches SyncData. That load gap is written down as a known
  limitation. Balances leaked for the player's heroes, not every lord.
- Tests: fixture id caster (Castar is the display name); one
  construction path; the second test renamed ReadsNoHero, since
  Hero.MainHero throws outside a game rather than returning null;
  the fake data store records null and throws on a duplicate key,
  as the engine's BehaviorSaveData does.
- Two comments in SpecialResourcesBehavior (no code change).

Waiting for Mike: the no-record load reset (behaviour-changing) and
the GitHub issue. Full suite 10318 passed, 2 skipped, 0 failed.

Report and RCA, both in docs/reviews/:
deep-review-001-cross-campaign-singleton-resets-2026-09-24.md
rca-cross-campaign-singleton-resets-2026-09-24.md
Lessons in state-lifecycle-save, testing-qa and misc; REVIEW-LOG entry.

#### fix(cache-rebuild): v2.0.30 - review follow-ups for plan 025

`76f22e11`

Six deep-review lenses and a Codex adversarial pass (gpt-6-astra,
ultra) found no runtime defect. Seven LOW or P3 findings were
confirmed; six are fixed here, all in tests and docs:

- The permanent absence test for the deleted types is removed
  (simplicity criterion); the retired-key compatibility test stays.
- editor-cache-rebuild.md no longer says Phase 1 paths can be
  memoized for Phase 2: v1.5.3 keeps the path local and Phase 2
  pathfinds with a different cost multiplier. The unmeasured 2-3x
  claim, the stale NavigationPath dependency and the hand-kept test
  counts are gone.
- reflection-sites.md names 41258657 as the retired row's origin and
  routes any restored self-reflection to Category D.
- CHANGELOG: the two keys were in the shipped JSON from 6a80bac6 to
  b5cb3018, before any release tag.

The seventh (plan 025's Step 6 sentence contradicts its own done
check) is recorded in the RCA; the executed plan is not edited.

Report:
docs/reviews/deep-review-025-delete-unreachable-scaffolds-2026-09-24.md
RCA:
docs/reviews/rca-delete-unreachable-scaffolds-2026-09-24.md

Full suite: 10287 passed, 2 skipped, 0 failed.

#### fix(hooks): v2.0.30 - convergence fixes for plan 013

`ee1929fa`

The convergence pass on 7aa658e3..6514fdfe found three LOW defects,
each re-checked against the code and fixed.

- test_hooks.sh 4c gives validate-push.sh a `git -C <dir> push` row.
  A prefilter narrowed to `git push` passed every earlier row; with
  the new row it fails (391 passed, 1 failed on that mutant).
- The 4d header and hooks-catalog.md name the five gates 4d covers
  and the five it does not, dropping a reason the code contradicted:
  check-changelog-changed.sh denies on a command-line pathspec with
  nothing staged, and the subject gate reads SubModule.xml.
- The lesson and REVIEW-LOG say the committed suite caught neither
  gap (it failed three rows on the other mutant), and REVIEW-LOG no
  longer counts R8 as both fixed and for Mike.

Hook suite 392 passed, 0 failed. dotnet: 10235 passed, 2 failed (the
two known live-Armory tests; the branch predates a39a9c86).

#### fix(reviews): v2.0.30 - convergence fixes for plan 010

`1e989944`

The convergence pass on 2628c66c found five defects in the evidence
the plan 010 decision records state. All five were re-checked against
the code and git objects and fixed; none was a false positive.

- The records no longer say the old tests.md sentence contradicted
  all 102 class-level tags. It governed only classes whose other
  tests pass on the stubs, and nobody counted those.
- The registration check ran in no CI step before D45 either, so
  KS10 is re-graded to agree and N1 is described as adding a check.
- GameReferences.targets is cited as :9-10, the RCA quotes the lesson
  title as written, and the two known failures are traced to
  709649c3, not a39a9c86.

2628c66c's body keeps the 102 count; the report's Convergence
section supersedes it. Docs only. Full suite: Failed 2, Passed
10256, Skipped 2 (the two known live-Armory tests).

#### fix(docs): v2.0.30 - convergence fixes for plan 005

`01b97018`

The convergence pass on a0fa3cff found six LOW defects in the review
records and the checklist line; each was re-checked and all are fixed.

- The credential sweep's archive loop now finds .tar.gz and .tgz files
  at any depth through find. A seeded scratch folder proves the old
  loop missed a nested and a .tgz archive; on the real vendor folder
  it still lists exactly the three BUTR archives.
- The CHANGELOG names both reasons the first check missed the
  credential: it ran in a worktree, and grep cannot read gzip.
- The RCA gains the missing-issue finding and quotes the lesson title
  correctly; the lesson index counts are 824 and 181.
- The review report records the convergence pass, and the REVIEW-LOG
  heading says so.

Docs only. dotnet test: 10313 passed, 0 failed, 2 skipped.

#### fix(battlebalance): v2.0.30 - convergence fixes for plan 003

`ab70555d`

The convergence review of 02157b18 found three test and doc defects,
no runtime one. Each was re-checked before the fix.

The read-through test flipped all twelve settings at once. Three bools
default to true, so all three ended false and a getter wired to the
wrong one passed. It also read no getter before the edit, so a getter
that caches its first read passed. The test now reads every getter,
then edits one setting per pass on a fresh TaomSettings and checks all
twelve. Proven with two mutants run against the old and new test: the
casualty-ratios getter reading the troop-power setting, and a
first-read cache on Tier7Power. Both passed the old test and fail the
new one.

The DryIoc comment said two public constructors fail at resolve; a
probe showed the throw comes from Register, so it now names IoC.cs:127.

The coverage claim is corrected in CHANGELOG, battle-balance.md, the
testing-qa lesson and the RCA. The deep-review report gets a
Convergence section and re-accounts Agent 6 D1.

Full suite: 10323 passed, 2 skipped, 0 failed.

#### fix(docs): v2.0.30 - convergence fixes for plan 009

`aa7a5518`

The RCA summary broke the ten confirmed findings down as 4 LOW and
2 NIT; its own table has 8 LOW and 2 NIT rows. The lessons index still
said 49 Localization & UI lessons after two were appended (52). The
Harmony & IL (63) and Build, Tooling & Workflow (173) counts had
drifted earlier and are re-derived in the same edit.

Convergence section appended to the plan 009 decisions review.
Full suite: Failed 2, Passed 10256, Skipped 2, Total 10260 (the two
known live-Armory tests on a branch based before a39a9c86).

#### fix(career): v2.0.30 - convergence fixes for plan 002

`8186cc9c`

The convergence pass on 96832589 found five inaccuracies in the
docs and records it added; all five re-checked and fixed.

- career-system.md: the calculator step says float parameters (GetInt
  exists and is out of scope), and the Tests table gains the
  MutationParamsTests row (7 tests).
- testing-qa lesson and RCA: the over-constrained mutant returns the
  default, so a negative mutation is dropped, not zeroed; the plan is
  quoted from its line 329 instead of an invented phrase.
- RCA: the overflow follow-up came from Agents 1, 2, 5 and 6, not
  every lens.
- LESSONS-LEARNED index recounted: 66, 86, 823 in total.
- Deep-review report: corrected test file count (42) and a new
  Convergence section.

Docs only; full suite 10320 passed, 2 skipped, 0 failed.

#### fix(hooks): v2.0.30 - decision review follow-ups for plan 013

`6514fdfe`

Deep review (six lenses) and Codex of the D39/D40 follow-up found no
changed gate decision; the confirmed findings are in its tests and text.

- tools/test_hooks.sh 4c: the commit gates get a git -C <dir> commit
  trigger row, and the default escaped row holds neither git nor
  commit. Two planted mutants passed the committed suite and fail the
  new one. Suite: 391 passed, 0 failed.
- Twelve hook comments: the escape arm is "never skip", not "fail
  open", with the JSON reason it is safe; comments only.
- CHANGELOG: the D39/D40 before-case is now the payload the base
  actually let through, and the parity claim names what it compared.
- hooks-catalog.md: safety rests on JSON grammar, 4d covers five
  blocking gates, and the savings claim notes the description field.
- First review record: seven silent gates, the suggest-compact
  deviation from D39's text, and #661 filed after the commit.

Report:
docs/reviews/deep-review-013-bash-hook-prefilter-decisions-2026-09-24.md
RCA:
docs/reviews/rca-bash-hook-prefilter-decisions-2026-09-24.md

#### fix(crash-report): v2.0.30 - convergence fixes for plan 006

`9e8c99bd`

The convergence pass over 8c84fa20 found five comment, doc and test
defects; runtime parity held. OnUnhandled is now internal so a test
can raise it on a chosen thread: it pins that the hook passes the
service offMainThread false on the subscribing thread and true on a
worker, since the parameter defaults to the unsafe false. The Finalizer
wiring test now covers the worker direction too. Both new assertions
fail against the matching mutations.

Stale comments reworded: IsOffMainThread names the watchdog as the
open exception, the main-thread id note no longer says "mark", and the
HandleAndSwallow arity and the helper test header are current. The
config row no longer claims the AppDomain hook hands exceptions back
with master off, and the deep-review record points at the comment that
still exists.

#### fix(tests): v2.0.30 - review follow-ups for plan 010

`2628c66c`

Second deep review and Codex pass on the maintainer decisions commit
c139bc50 (D44 to D46). Codex found nothing; the six lenses confirmed
five LOW or INFO text defects, all fixed here:

- CHANGELOG: restore the convergence entry's heading, which the
  decisions commit had overwritten.
- tests.md: a class tag stays the default; a method tag is permitted,
  not required (the old sentence contradicted 102 class-level tags).
- GameReferences.targets and the first report: carry decision 44's
  1.4.8 re-check, name the tagger manifest row that would restore
  the Patch86 class tag, and record the replay's scratch roots.

Needs Mike: tag the Patch86 registration check BindingVerification
so it runs on the hosted gate.

Full suite: 2 failed (the known live-Armory tests), 10256 passed,
2 skipped.

Report and RCA, both in docs/reviews/:
deep-review-010-ci-on-hosted-windows-decisions-2026-09-24.md
rca-ci-on-hosted-windows-decisions-2026-09-24.md

#### fix(patchshield): v2.0.30 - convergence fixes for plan 007

`b0a93fb4`

The convergence pass over the review-fix commit found five defects in
the review record and none in code or tests. The reword list for plan
006's merge now names lessons/harmony-il.md:613, the fourth text on
this branch that says nothing on a shim preserves the stack; the
decisions report, its RCA and the build-tooling lesson say six texts
and two misses, and the lesson's search matches every inflection.

The decisions report now records the convergence pass before its
verdict, and corrects the lesson count (173), the SESSION SUMMARY line
(PatchShield.cs:423) and the claim made for the named-argument call.

Full suite: 10247 passed, 2 skipped, 2 failed (the two live-Armory
tests; this branch is based before a39a9c86).

#### fix(tools): v2.0.30 - review follow-ups for plan 005

`a0fa3cff`

Deep review (five lenses) and Codex on 6b34fd00. The faction-map argv
fix was correct; the prose and its verification were not.

The adoption checklist grep could not see inside the .tar.gz drops
TAOM keeps, so it reported clean while three gitignored BUTR archives
under Dependencies/.vendor-source/ still hold a
packageSourceCredentials block. The line now sweeps archives, uses a
-E pattern ripgrep reads the same way, drops an em dash and names the
harvest finding by path. The CHANGELOG no longer says the credential
is gone from disk; 6b34fd00's commit body still does (Mike's call at
merge).

New tools/tests/test_process_faction_map.py pins the path fix: a
plain folder, a quote and Python text in the folder name. The last
two fail on the a39a9c86 tool and pass here.

Report: docs/reviews/deep-review-005-security-hygiene-2026-09-24.md
RCA: docs/reviews/rca-security-hygiene-2026-09-24.md
Three lessons in docs/reviews/lessons/build-tooling-workflow.md.

Tests: dotnet test 10313 passed, 0 failed; the new module 3 of 3.

#### fix(battlebalance): v2.0.30 - review follow-ups for plan 003

`02157b18`

Deep review (six lenses) and Codex gpt-6-astra on 7feca96b found no
runtime defect. The follow-ups remove a latent start-up trap and the
test gap behind it.

BattleBalanceSettingsProvider now caches TaomSettings.Instance lazily
on the first non-null read (the NameplateRelationSettingsProvider
pattern) instead of in its constructor. The constructor read was safe
only because the one resolve runs under OnGameStart, after MCM sets
BaseSettingsProvider.Instance; a future resolve in OnSubModuleLoad
would have pinned the defaults for the session with no log line. On
today's wiring the values read are identical.

Tests first: the IL rule now requires the private lazy accessor, not a
constructor, to read Instance (RED against the constructor form); a
read-through test edits all twelve settings after construction, each
to its own value (a Tier8-reads-Tier9 mutation fails it); all twelve
fallbacks pinned against the TaomSettings defaults; the provider
resolves from a real DryIoc container. Full suite: 10323 passed,
2 skipped, 0 failed.

Docs: battle-balance.md and file-catalogue.md no longer describe a
per-access proxy; CHANGELOG corrected.

Report and RCA, in docs/reviews/:
deep-review-003-hot-path-resolve-and-grid-caching-2026-09-24.md
rca-hot-path-resolve-and-grid-caching-2026-09-24.md
Lessons: lessons/state-lifecycle-save.md, lessons/testing-qa.md.

Not-tested: in game; change a Battle Balance slider mid-campaign and
check auto-resolve follows it without a restart.

#### fix(harmony): v2.0.30 - review follow-ups for plan 009

`27fd23bb`

Second deep review and Codex of the maintainer decisions
(4c728dac..b6cb6ff5). Every lens and Codex found the translated
notice rows outside <strings>; 7eae4704 moved them, and a new gate,
LanguageDataXmlTests.AllTranslationFiles_StringRowOutsideRootStrings_
IsNeverPresent, fails any row LocalizedTextManager.LoadLanguage would
skip (red on the b6cb6ff5 blobs, green now).

The German phase names gain their genitive article and the French and
Japanese sentences read correctly once {PHASE} is filled; XML rows and
translation caches changed together. PatchCategoryIndexTests pins the
null-category guard (mutation-checked) and multi-class parity, and two
test names follow the convention. Apply and TryApply now say that a
category which lost a class at index time still reports success;
whether it should fail is left for Mike. Line references, the
architecture tree and the CHANGELOG test snapshot are corrected.

Full suite: 10256 passed, 2 skipped, 2 failed (the two known
live-Armory tests; this branch predates a39a9c86).

Report: docs/reviews/deep-review-009-guarded-patch-category-apply-
decisions-2026-09-24.md
RCA: docs/reviews/rca-guarded-patch-category-apply-decisions-
2026-09-24.md

#### fix(career): v2.0.30 - review follow-ups for plan 002

`96832589`

Deep review (six lenses) and Codex follow-ups for the MutationParams
finiteness guard. Two MutationParamsTests pin the unparseable fallback
and a negative finite value passing through; a mutant that accepted
any parse and rejected negatives turned both RED. The career-system
feature doc tells calculator authors to read floats only through
GetFloat, and the CHANGELOG claim about FiniteFloatValidator is
narrowed to what the loaders actually do. No production code changed.

Open for Mike: the GitHub issue, the silent fallback against config
rule 5, and a finiteness gate on the calculated value in
MutationService.ApplyMutation. Full suite: 10320 passed, 2 skipped,
0 failed.

Report: docs/reviews/deep-review-002-nan-infinity-config-guards-2026-09-24.md
RCA: docs/reviews/rca-nan-infinity-config-guards-2026-09-24.md

#### fix(crash-report): v2.0.30 - review follow-ups for plan 006

`8c84fa20`

The second deep review and Codex review of the plan 006 decisions
found one runtime defect. The callback bridge told the crash service
about an off-main-thread capture only by writing a mark to
Exception.Data inside a swallowing catch, and the service read a
missing mark as the main thread. Exception.Data is virtual and is
read-only for the runtime's preallocated out-of-memory and
stack-overflow exceptions, so those would have run the Mission and
Campaign collectors and the inquiry on a worker thread. The verdict is
now a HandleException parameter from both the bridge and the AppDomain
hook, through one AppDomainExceptionHook.IsOffMainThread, and the mark
is gone. The hook logs the main thread id it records.

Tests now reach the swallow path with a reachable service
(RecordingCrashService), which kills the four mutations the earlier
tests let survive. The native-capture MCM hint, reference counts, the
tableau caller list, the combat-callback limits and the owed probes
are corrected, and the allowlist cap test is folded into the pin.

Report: docs/reviews/deep-review-006-crash-capture-boot-cost-decisions-2026-09-24.md
RCA: docs/reviews/rca-crash-capture-boot-cost-decisions-2026-09-24.md
Full suite: 10271 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

#### fix(patchshield): v2.0.30 - review follow-ups for plan 007

`900920dc`

Deep review (six lenses) and a second Codex pass over the maintainer
decisions commit 0bf2409e. No behaviour defect; six text and test
findings confirmed and fixed:

- a test comment names the renamed local alreadySeen
- ShieldCoverageTests follow the MethodName_State_Expected convention,
  one test is split, and its message claims only what it shows
- dr3-maintenance.md and the feature map count 19 Foundation classes
- the plan 007 review record lists every text to reword once plan 006
  lands (dr3-maintenance.md:261, :304, lessons/harmony-il.md:572)
- the dr3 log-sample lead-in no longer reads as the old format
- the decisions-round Codex prompt is committed

The FormatShieldPassSummary call now passes named arguments.
Full suite: 10247 passed, 2 skipped, 2 failed (the two live-Armory
tests).

Report:
docs/reviews/deep-review-007-patchshield-skip-callback-shims-decisions-2026-09-24.md
RCA:
docs/reviews/rca-patchshield-skip-callback-shims-decisions-2026-09-24.md

#### fix(loc): v2.0.30 - move the notice translations inside <strings>

`7eae4704`

b6cb6ff5 seeded the five taom_patch_apply_* keys through the
translator's sync_missing_ids, which in these language files put the
rows after </strings>, directly inside <base>. The files mix line
endings (rows end LF, the </strings> line CR CR LF), and the seeding
anchors on the last <string> line it splits out, which there already
holds </strings>. The game's LocalizedTextManager reads only <string>
children of <strings>, so all 60 translations would have been ignored.

The same 60 rows now sit just before </strings> in the 12 files, same
text and order, with the LF ending of the rows around them. Checked by
parsing each file: all five ids inside <strings>, 2,670 <string>
elements before and after. Trunk's language files have no stranded
rows (scanned). The seeding bug in tools/translate_with_claude.py and
a coverage test that cannot see placement remain open follow-ups.

Full suite unchanged: 10253 passed, 2 skipped, 2 failed (the two known
live-Armory tests). validate_moduledata.py: 0 errors.

#### fix(specres): v2.0.30 - a new campaign no longer keeps the old balances

`4263535a`

Special resource balances live in SpecialResourceStorageService, a
Reuse.Singleton that outlives a campaign. A second campaign started
without restarting the game kept every balance the first one wrote, and
its first save then persisted them into the new save file.

OnNewGameCreated now wipes the storage with RestoreData(null) before the
character-creation finalize seeds the new hero. The handler no longer
reads Hero.MainHero: the two resets it gated never used the hero, and a
new campaign must wipe the old state either way. The SyncData load reads
into a null local instead of the live dictionary, because the engine
leaves the ref unchanged on a missing key and so handed the previous
campaign's balances back. Saving is unchanged and a present key
round-trips exactly.

This is the SpecialResources half of plan 001; the CareerSystem half
landed earlier in f4273639. Five tests in
SpecialResourcesBehaviorSessionResetTests run against real storage.
Full suite: 10318 passed, 2 skipped, 0 failed.

Save-compat: no new save fields; the key and its format are unchanged
Not-tested: live new-game event dispatch, second campaign in one run

#### fix(siege): v2.0.30 - convergence fixes for plan 019

`272ac1f0`

The convergence pass on bdc80a5a found 4 defects (1 LOW, 3 NIT); all
were confirmed against the code and fixed. No production code changes.

- The fresh-copy isolation test now mutates a GetMessages("") result
  and asserts the next lookup is a different object. Without that, a
  mutant returning DefaultMessages for an empty id passed all 31 tests.
  With the mutant applied, only this test fails.
- REVIEW-LOG and the RCA called the 2,256 nullable warning count a
  "test count"; it is now "the test project's nullable warning count".
- The lesson's "Why missed" line said the warnings sat at untouched
  lines; two of the three came from the commit's own new test code.
- The review record's NOT APPLIED and FOLLOW-UP lists now cite
  SiegeDefenseService.cs by HEAD line numbers.

Test project CS86xx: 2,256, Main 0. Full suite: 10,258 total, only the
two live-Armory failures.

#### fix(enlistment): v2.0.30 - convergence fixes for plan 014

`aac0e05b`

The convergence pass on a67792c4..fd61757b found four defects, all in
tests or review docs, none in runtime code.

- EnlistmentStopEndTests now also pins the OnSettlementLeftEvent
  subscription in EnlistmentMaintenanceBehavior. Before, deleting that
  line kept the suite green; a mutation run with it commented out now
  fails the pin.
- The stop-end sentinel is renamed ReachedTheStopEnd, after its throw
  site in OnStopEnded.
- The RCA summary and the state-lifecycle lesson credit four lenses,
  not five, matching the RCA's per-agent record.
- The deep-review report gains a Convergence section, and its verdict
  now follows the completed Step 4.

#### fix(ci): v2.0.30 - apply maintainer decisions for plan 010

`c139bc50`

Apply Mike's answers to the three NOT APPLIED items in the plan 010
review (#421, the C# half; the issue stays open for its Python half).

D44, applied: delete the SandBoxCore reference from Main/TAOM.csproj
and both TaomSandBoxCoreModuleBin properties in GameReferences.targets.
The folder holds no DLL on v1.5.3 and BUTR ships no package for it.
Reference snapshots of all three projects are identical before and
after in install and RefAsm mode (6 of 6); both builds have 0 errors.

D45, applied: move RequiresGame from the
Patch86HideoutBossFightBindingTests class to
PatchClasses_AreRegisteredInAllThreePlaces. The two prefix IL checks
now run in the CI unit step and pass on the reference assemblies
(replay: total 8220, executed 8196, failed 0). tests.md allows a
method tag when the rest of the class runs on the stubs.

D46, measured and reverted: pointing the unit step at refasm-game
turned 24 skips into 14 passes and 10 failures (Patch71FillTests and
TeamCombatantSelectorTests hit stub constructors), so csharp.yml is
unchanged. Both sets of numbers are in the review record.

Full suite in install mode: Failed 2, Passed 10256, Skipped 2, Total
10260 (the two known live-Armory tests).

#### fix(hooks): v2.0.30 - apply maintainer decisions for plan 013

`7aa658e3`

Apply the maintainer's 2026-09-24 decisions on the plan 013 review.
The plan 013 issue is not filed yet, so no issue number is cited.

D39: each gate prefilters on the word it gates instead of `git`. The
six commit gates filter on `commit`, validate-push.sh on `push` and
block-no-verify.sh on `no-verify`, as each body requires; git status,
git diff and git log now start no Python in them. The two confirm
gates keep `git`, and suggest-compact.sh is untouched because plan 011
deletes it.

D40: a payload holding any JSON backslash-u escape takes the full
parse in the twelve prefiltered hooks, so an escaped letter cannot
hide the gated word. The hooks share no prefilter, so the condition
sits on each hook's prefilter line.

D38, D41 and D42 are handled in plan 011, which runs on top of this
branch; the review record says so.

Tests first: tools/test_hooks.sh went 341/24 red for D39 and 365/17
red for D40, then 382 passed, 0 failed. Old versus new hooks over 240
payload cases: 0 differences. dotnet test: the two known live-Armory
failures only.

Not-tested: the edited hooks under a live harness (a worktree's hooks
do not run in the session; the live proof is owed after merge)

#### fix(warg): v2.0.30 - convergence fixes for plan 015

`516380d2`

The convergence pass on 23f6f85b..5d9d4cc9 found the code and tests
sound and one LOW process defect: the decisions RCA left out two
confirmed findings.

- RCA: F8 added, the reordered window-end gate in
  BoneCheckDuringAnimation.Tick shipped with no NaN test. It repeats
  the testing-qa lesson "A gate moved into new code is new code",
  written on this branch the same day. F9 added, InjectedNodes skipped
  NoEnemyCloseDecorator. The count is now nine, not seven (the
  5d9d4cc9 body also said seven).
- F8's preventive action goes up a tier because it is a repeat: one
  line in .claude/rules/csharp-architecture.md says a moved, extracted
  or reordered engine-float gate gets its NaN test in the same commit,
  whichever polarity it keeps. The lesson gains a repeat note.
- Stale lines the pass reported: warg-combat.md said
  WargAttackServiceTests has 7 tests and that #178 was open (22 tests,
  2 ignored; the service takes IAgentAdapter since 5a61e174). The
  WargTickCostTests comment said no IL scan reaches the type
  initializer; DeclaredBodies does scan it. Comment only.
- The decisions report gains a Convergence section.

Full suite: Failed 2, Passed 10282, Skipped 2, Total 10286 (the two
known live-Armory failures).

#### fix(enlistment): v2.0.30 - review follow-ups for plan 014

`fd61757b`

Deep review (six lenses) and Codex of the maintainer-decisions commit
a67792c4 (#656). Decision 6 cleared the arrival offer's settlement latch
only in the exit sweep, which a shore-leave pass suspends, so an
accepted offer never re-armed the town. EnlistmentMaintenanceBehavior
now ends the stop on the commander's settlement-left edge (it takes the
wait-menu presenter), RED test first in EnlistmentStopEndTests.

Also: the game-end test is renamed to what it proves, ignores comment
lines and uses RepoPath; the heap-release claim and the teardown-order
comment are corrected; stale reset docs updated; tests added for the
wiring, the co-op client load with data, a throwing ColumnLeftSettlement
subscriber and the maintenance hook's container resolution.

Full suite: 10266 passed, 2 skipped, 2 failed (the known live-Armory
tests), total 10270.

Report and RCA, both in docs/reviews/:
deep-review-014-enlistment-session-scope-decisions-2026-09-24.md
rca-enlistment-session-scope-decisions-2026-09-24.md

#### fix(tools): v2.0.30 - faction-map helpers take paths as arguments

`6b34fd00`

Plan 005, ported from the June branch impl-005 (4310aa6e, 4bc520a1)
through the review pipeline (Mike's decision 17).

tools/process_faction_map.py pasted each file path into two child
python -c scripts as r'<path>', so a quote in a path broke the script
and let the path's text run as code. The children now read their
paths and numbers from sys.argv, and the doubled f-string braces the
old template needed are gone.

The external-repo vetting checklist gains a grep for inline package
credentials in vendored drops. The vendored BUTR credential behind it
is already gone from disk (checked: no nuget.config and no
packageSourceCredentials under Dependencies/); MCP pinning moved to
plan 016.

Probe (a PNG under a folder named "it's here"): trunk fails both
calls with a SyntaxError in the generated source; the port returns
bbox (1, 1, 2, 1, 4, 3) and writes the crop. py_compile passes.
Not-tested: the full faction-map pipeline on the real region art.

#### fix(config): v2.0.30 - reject NaN and Infinity in mutation floats

`78889a85`

Plan 002, ported from the June branch impl-002 (cfc47206) through the
review pipeline (Mike's decision 17). MutationParams.GetFloat returns
the default when a value parses to NaN or plus or minus Infinity, via
the existing FiniteFloatValidator, so a "NaN" in a career mutation's
XML can no longer poison later comparisons.

cfc47206's troop-weight half already landed on trunk in bee07b48, so
the cherry-pick's conflicts on TroopWeightXmlLoader and its tests were
resolved to trunk's versions; only the mutation half is new.

MutationParamsTests (5): 3 RED against trunk's unguarded GetFloat (NaN,
+Infinity, -Infinity), all GREEN with the guard. Full suite: 10318
passed, 2 skipped, 0 failed.

#### fix(siege): v2.0.30 - review follow-ups for plan 019 decisions

`bdc80a5a`

Second deep review (six lenses) and Codex round on 503b933e. No
runtime defect; 11 findings fixed, all LOW or NIT.

- SiegeDefenseServiceTests: the three nullable warnings the
  KingdomSiegeMessages? change added are gone, so TAOM.Tests is back
  at its 2,256 baseline (counted on a --no-incremental build).
- A new test mutates results from the defaults path, and the
  known-faction test asserts all five fields. Both failed under
  mutants (return DefaultMessages; AcceptMessage read from
  AcceptButton) before the real code was restored.
- Patch registry: decision 2 recorded as closed (#660), not open.
- siege-defense.md: tokens listed per field, 31 tests with GetMessages
  and Reset/Restore bullets, KingdomSiegeMessages.cs row, #660.
- siege.md: caller wording, Status Open (#660). CHANGELOG and the
  GetMessages comment no longer promise "never blank" for spaces.

Needs Mike: a warning log for incomplete KingdomMessages entries,
and whether a value of only spaces counts as empty.

Full suite: Failed 2 (the known live-Armory tests), Passed 10254,
Total 10258.

Report:
docs/reviews/deep-review-019-nullable-ratchet-decisions-2026-09-24.md
RCA: docs/reviews/rca-nullable-ratchet-decisions-2026-09-24.md

Refs #660

#### fix(warg): v2.0.30 - review follow-ups for plan 015

`5d9d4cc9`

Second deep review and Codex pass on the plan 015 maintainer decisions
(56eb4bc8..23f6f85b). No HIGH, no runtime defect; seven LOW findings
fixed, four design choices left for Mike.

- BoneCheckDuringAnimation.Tick is now driven by substitute tests
  (default ActionIndexCache needs no engine: the v1.5.3 struct is
  beforefieldinit). They replace the IL order rule, which a mutant
  fetching the skeleton on every wind-up frame passed.
- WargTickCostTests: one resolve predicate that also catches
  IoC.ResolveAll, and one node list, so NoEnemyCloseDecorator's
  constructor is scanned too.
- WargTreeNodeInjectionTests drives the attack task and the facing
  decorator with substitute services.
- Comments and docs: the service-locator claim is scoped to the four
  service nodes (LogTask still resolves), the wind-up skeleton
  difference states the recovery case, the NaN polarity is documented
  and pinned, IsWarg is gone from warg-combat.md, and How-to-Add names
  the injection pattern.

Report:
docs/reviews/deep-review-015-warg-tick-costs-decisions-2026-09-24.md
RCA:
docs/reviews/rca-warg-tick-costs-decisions-2026-09-24.md
Full suite: 10282 passed, 2 skipped, 2 failed (the two live-Armory
tests).

#### fix(patchshield): v2.0.30 - apply maintainer decisions for plan 007

`0bf2409e`

Applies the maintainer's 2026-09-24 answers to the plan 007 review
(#651):

- The pass-2 entry label in diag.log now names every start that
  reaches it: "game start: campaign, custom battle or editor". No
  test or tool reads the old text.
- PatchShield's single _shielded set is split into seen and attached
  (new ShieldCoverage). The shield pass line reports
  "(seen: N, attached: A)" with "already-seen", and the session
  summary "shielded A of N patched method(s) seen", so skipped
  methods no longer count as shielded. Which methods get a finalizer
  is unchanged. Test first: ShieldCoverageTests failed on the old
  set (attached 3, expected 1), then passed.
- Recorded without code change: the whole ManagedCallbacks namespace
  stays excluded; the fallback stack preservation was fixed on plan
  006's branch in 42624b95, not here.
- The CHANGELOG heading cites #651.

The review record gains a "Maintainer decisions applied" section.
Full suite: 10246 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

#### fix(crash-report): v2.0.30 - apply maintainer decisions for plan 006

`8e6b0935`

Second pass on the maintainer's #650 decisions of 2026-09-24,
continuing 42624b95.

D43: the callback bridge now marks a capture made off the main thread.
AppDomainExceptionHook keeps the id it records in Subscribe() at
OnSubModuleLoad in a static, read through an internal MainThreadId.
With native capture on, Native2ManagedBridge sets
ex.Data["TAOM.CrashReport.OffMainThread"] when the current thread is
not that id, or when no id was recorded (the safe direction), so
CrashReportService skips its Mission and Campaign reads and the
inquiry. MissionThreadGuard and SubModule.cs are unchanged.

D47: the ten mission combat callbacks traced into TAOM code are back
on the Native2ManagedTargets allowlist (melee, missile, charge, fall
and area damage, hit blocked, defend collision results, agent removed,
agent deleted, shoot missile), each pinned by literal name; the cap
in All_IsASmallDistinctAllowlist is now 16. Mount, dismount and
alarmed-state stay out. The attach time is not measured.

Tests first: three bridge tests and a hook test failed before the
change, and the sixteen-name pin failed with 16 expected, 6 actual.
Full suite: 10265 passed, 2 skipped, 2 failed (the known live-Armory
tests). Docs: crash-report.md, the Patch37 registry entry, the
CHANGELOG entry and the review record's maintainer-decisions section.

#### fix(crash-report): v2.0.30 - apply maintainer decisions for plan 006

`42624b95`

Applies the maintainer's 2026-09-24 answers to the plan 006 review
(#650), each test first:

- Allowlist entry 6 is now RenderTargetComponent_OnPaintNeeded, the
  tableau render callback (character, item, banner and map
  conversation tableaus), verified in the installed v1.5.3 engine.
  It replaces a tableau-setup callback nothing in v1.5.3 arms.
- The callback bridge has one exit, and every exception it hands
  back goes through RethrowStackPreserver.PreserveForRethrow.
- CrashReportPatchHelper.HandleAndSwallow routes all four hand-back
  paths through the preserver, so Harmony's rethrow keeps the
  throw site.
- Recorded without code change: bridge priority stays 400, the
  suppression log keeps its powers-of-ten cadence, capture stays on
  by default with EnableNativeToManagedCapture as the toggle. The
  in-game probe stays owed.
- Cited #650 in the CHANGELOG heading and a GitHub Issue section in
  the feature doc.

Not applied: adding the mission combat callbacks back. The trace
finds ten with a verified path into TAOM code (recorded in
crash-report.md), but several arrive off the main thread, where an
untagged bridge capture would run the mission collectors and the
on-screen inquiry. Stopped for the maintainer's call.

Full suite: 10261 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

#### fix(loc): v2.0.30 - localize the patch failure notice for plan 009

`b6cb6ff5`

Maintainer decision on review finding 15 (#653): the startup patch
failure notice is localized. TakeFailureSummary returns a
{=taom_patch_apply_failed} TextObject with PHASE and GROUPS; the three
phase names and the inquiry title are registered taom_patch_apply_*
keys; the button reuses vanilla's {=oHaWR73d}Ok row.

The five keys are translated into all 12 languages as AI first drafts.
Only these keys were sent to the model: the stock --sync-ids run would
also have translated unrelated untranslated rows and seeded 26 keybind
ids. Each language file and cache file gains exactly 5 rows, and
{PHASE} and {GROUPS} survive in every language.

Full suite: Failed 2, Passed 10253, Skipped 2, Total 10257; the two
failures are the known live-Armory tests.

#### fix(hooks): v2.0.30 - convergence fixes for plan 013

`b34d615a`

The convergence pass over the review-fix commit found no behaviour
defect and three wording defects, all confirmed against the files:

- CHANGELOG: the JSON escape for `g` had been decoded into the letter
  itself on both lines. The Edit tool decodes backslash-u escapes, so
  the fix went in through a script.
- hooks-catalog: 256 to 451 ms was the whole hook's time on an `ls`
  before the prefilter, not the cost of the two Python starts; it now
  gives the before and after figures.
- REVIEW-LOG and RCA: each proof is now credited to the finding it
  proves (slow fake for F1, planted mutations for F2 and F3), and
  F4 to F6 are no longer said to have a failing proof.
- Lessons: "Eleven gates" is now "The ten git gates".

Hook suite 341 passed, 0 failed; dotnet suite failed 2 (the known
live-Armory pair), passed 10235, skipped 2.

#### fix(harmony): v2.0.30 - apply maintainer decisions for plan 009

`7912fdd8`

Applies the maintainer's 2026-09-24 decisions on the plan 009 review
(#653).

Decision 1, review finding 3: one patch class whose [HarmonyPatch]
names a type the engine no longer has made Harmony's assembly-wide
category index throw, so all 84 categories failed. The new
PatchCategoryIndex builds that index class by class from Harmony's
public API, skips a class whose attributes cannot be read, and applies
each category's classes the way Harmony.PatchCategory does. The
skipped class is logged as [PatchApply] SKIPPED and named in the
startup inquiry; every other category still applies.
PatchCategoryIndexTests proves it on an emitted probe assembly (RED
through Harmony's own index, GREEN through PatchCategoryIndex), and
the source gate now allows no direct PatchCategory call in Main.

Decision 3: the CHANGELOG heading cites #653.

Decision 2 (localizing the notice) is not in this commit.

Not-tested: live engine apply path and the notice (needs the game)
Save-compat: no save data touched

#### fix(enlistment): v2.0.30 - apply maintainer decisions for plan 014

`a67792c4`

Apply Mike's answers (2026-09-24) to the plan 014 review's NEEDS MIKE
items, for #656. Each behaviour change is test-first.

- The commander-loss modal's shown-once latch (_lossAnnouncedFor) is
  cleared by EnlistmentReconciler.ResetForNewSession and on every
  discharge (a DischargeService.EnlistmentEnded subscription), so a
  later loss under the same lord is announced again.
- OnGameLoaded runs ResetSessionCaches above the co-op authority gate,
  so every peer resets on load; only normalization stays host-only.
- A loaded save with no Enlistment data clears the store before
  normalizing, instead of normalizing the previous session's term.
- The shore-leave offer latch clears when the stop ends: the exit path
  raises ColumnLeftSettlement and the presenter forgets the settlement
  (the 24-hour cooldown stays).
- SubModule.OnGameEnd calls ResetSessionCaches, one call inside the
  existing best-effort teardown block, so the finished campaign's
  cached commander party and army are released.
- The CHANGELOG heading cites #656.

The review record gains a "Maintainer decisions applied" section with
the RED output for each change. Full suite: 10259 passed, 2 skipped,
2 failed (the two known live-Armory tests), total 10263.

#### fix(warg): v2.0.30 - apply maintainer decisions for plan 015

`23f6f85b`

Applies Mike's 2026-09-24 answers to the plan 015 review's NEEDS MIKE
items (#659).

1. Services in tree nodes: WargBehaviorTree.BuildTree resolves
   IMissionAdapterFactory and IWargAttackService once per tree and
   passes them to the constructors of
   PeriodicallyCheckIfCanAttackAnyone, CheckOnceIfCanAttackEnemy,
   WargAiControlledIsNotFacingEnemy and WargAttackTask. The nodes keep
   them in private readonly fields and contain no IoC.Resolve. Every
   construction site is in WargBehaviorTree.cs. New IL tests (RED
   first) scan every body of the four nodes, constructors included,
   and pin one resolve per service in BuildTree, with a
   field-initializer control.
2. BoneCheckDuringAnimation.Tick tests the action and the progress upper
   bound first, reads the action progress once, and fetches the attacker
   skeleton only once the hit window is reached. One behaviour change: a
   missing attacker skeleton during the wind-up now ends the bite when
   the hit window opens instead of at once. IL rule tests (RED first)
   pin one progress read and the skeleton fetch after it. The owed
   in-game warg Custom Battle is the proof that bites land and end as
   before.
3. The wider scan results between grid rebuilds are kept: recorded, no
   code change.
4. #659 is cited in the CHANGELOG heading and in the GitHub Issue
   sections of warg-combat.md and advanced-combat.md.

The review record gains a "Maintainer decisions applied" section.
Full suite: 10273 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

Not-tested: in game; the warg Custom Battle is owed.

#### fix(siege): v2.0.30 - apply maintainer decisions for plan 019

`503b933e`

Applies Mike's 2026-09-24 answers to the NEEDS MIKE items in the
plan 019 deep review (#660).

1. Per-field fallback for siege defense popup text: a KingdomMessages
   entry that leaves Title, Body, AcceptButton, AcceptMessage or
   RewardMessage null or empty takes that field from DefaultMessages,
   and a JSON null entry gets every default. GetMessages returns a
   fresh copy, so neither the static defaults nor the config entry is
   written. KingdomMessages values are now nullable so the Siege error
   tier enforces the check. Four SiegeDefenseServiceTests, RED then
   GREEN, load their config through JsonConvert.
2. The no-settlement path in the siege camp guard is closed as
   unreachable from vanilla in v1.5.3; its log line stays as the
   tripwire. The /build-fix scope extension is kept. No code change.
3. #660 is cited in the CHANGELOG heading and in siege.md.

siege-defense.md documents KingdomMessages and its fallback; the
review record gains a maintainer decisions section.

#### fix(composition): v2.0.30 - convergence fixes for plan 018

`ecbc2199`

The convergence review of fa0b24e1 found two prose defects, both
confirmed against the code.

The Patch37 known limitation still described the code before the
fix. A faulted save owner now throws on every retry of the loading
step, so with crash capture on (the default) the load never finishes;
with it off the exception reaches the engine. The CHANGELOG, the
state-lifecycle-save lesson and the RCA summary now say so, and the
FeatureModuleHooks summary says "throw" rather than "rethrow".

The ordering note left out the co-op Harmony census after the
GameInit phase. FeatureModules.cs, the CHANGELOG and RCA row 5 now
name four hand-wired blocks after the module call.

No behaviour change. Full suite: Failed 2, Passed 10289, Skipped 2,
Total 10293 (the two known live-Armory tests).

#### fix(ci): v2.0.30 - convergence fixes for plan 010

`2897fcca`

The convergence deep-reviewer pass on b8c00045..a4b90e4d found 5 LOW
defects, 0 HIGH or MED; all confirmed against the worktree and fixed.

- D1: .ai/verification.md sent the reader from a Release build to the
  Debug csharp.yml test commands, so the recipe tested a stale or
  missing DLL. It now runs the build, unit and gate steps as written.
  The review report and REVIEW-LOG no longer claim the earlier replay
  ran as written (it added -c Release by hand).
- D2: the recipe says to unset the game variables before the build,
  since the build records the install as TaomGameFolder.
- D3: tests.md lists the ReflectionTypeLoadException signature seen in
  the executor's 6b.log.
- D4: GameReferencesTargetsTests gains one fixture row per rejected
  spelling (7 of 10 red first, the Import's own Condition row proven by
  mutation). Property names match without case; an import under a
  conditional ImportGroup, When or Otherwise no longer counts.
- D5: the report's verdict is set from the convergence pass.

No-game recipe replayed from a clean copy: build 0, unit executed 8194
failed 0, gate 338/338. Full suite: Failed 2 (known live-Armory),
Passed 10256, Skipped 2.

#### fix(hooks): v2.0.30 - review follow-ups for plan 013

`787fd366`

Deep review (six lenses) and Codex found the 13 prefilters correct
and the new test and docs wrong in six places. All fixed:

- test_hooks.sh 4c counted starts of a pinned fake interpreter;
  _pybin.sh drops a pin that misses its 0.8 s probe, so under load
  the check blamed a correct prefilter. It now reads a bash -x trace
  for the source of _pybin.sh, which has no timing.
- Section 4 gains a Bash payload with git and dotnet, so the exit
  code and JSON contract reaches each Bash hook's parse path again.
- 4c gives suggest-compact its dotnet and build.ps1 rows and finds
  Bash hooks by regex matcher, PostToolUseFailure included.
- The premise is Claude Code's payload, not JSON: hook comments,
  CHANGELOG and hooks-catalog.md say so and name the re-check.
- hooks-catalog.md paragraph moved below the one it broke.

Two planted mutations passed the old suite and fail the new one; a
fake interpreter slowed to 1 s no longer causes a false failure.
test_hooks.sh 341 passed, 0 failed; dotnet test at the known two.

Report: docs/reviews/deep-review-013-bash-hook-prefilter-2026-09-24.md
RCA: docs/reviews/rca-bash-hook-prefilter-2026-09-24.md

Deferred: validate-push.sh reads only the first line of a command,
so a force push after a cd line passes (HIGH, pre-existing, out of
plan 013 scope); needs its own issue and a multi-line test row.
Not-tested: live proof in a Claude Code session (plan 013:695).

#### fix(composition): v2.0.30 - review follow-ups for plan 018

`fa0b24e1`

The deep review and the Codex review of 4c728dac..44045b34 found one
real runner gap, dormant because no module owns save data yet: the
faulted-module skip ran before the fail-closed check, so a save owner
that faulted in a fail-open step, or on a retried campaign start, was
left out of the campaign silently. It now throws in every fail-closed
step, and a parked save owner no longer fails closed.

Also: ModuleRunner.RunCampaignStart owns the campaign-start flag; the
hooks gain overloads that take the runner and resolver, tested against
real CampaignGameStarter and BasicGameStarter instances; a failed fault
notice is logged; the ordering comments name the three hand-wired
blocks that still follow the module call; new guards pin the
Modules = modules hand-off, keep AddGameStartContent outside the
campaign branch, give the OwnsSaveData IL check a positive control,
keep IoC.Resolver inside the hooks, and feed the reader a real CRLF
file. Design: the single-implementation ITaomFeatureModule and the
FeatureState enum are deleted (a parked module has a ParkedReason),
the pilot's text test is replaced by its container test, and the decl
factories pass their delegates through.

Mike's calls: TAOM's Patch37 finalizer swallows the campaign-start
throw (CHANGELOG known limitation), the GitHub issue, the CHANGELOG
plan deviation, the stale plan precondition, one startup inquiry.

Report:
docs/reviews/deep-review-018-composition-root-first-steps-2026-09-24.md
RCA:
docs/reviews/rca-composition-root-first-steps-2026-09-24.md

#### fix(bindings): v2.0.30 - apply maintainer decisions for plan 008

`37306bca`

Applies the three decisions Mike took on 2026-09-24 for the plan 008
review's NEEDS MIKE items (#652).

1. No skip banner. notify-test-results.sh is back to its content at
   7f02fc8d and tools/test_hooks.sh section 7c is removed. The banner
   went to stderr from an exit-0 hook and only reached the debug log.
   The signal is the Skipped: count in dotnet test's own output plus
   the strict binding-gate runsettings; the hooks catalog row, the
   CHANGELOG, the RCA and REVIEW-LOG say so.
2. binding-gate.runsettings sets TreatNoTestsAsError. A zero-match
   filter under it exited 0 before and exits 1 now; the strict gate
   still passes 368 of 368 with 0 skipped. BindingGateRunSettingsTests
   pins it and MapInconclusiveToFailed (red first on the new row), and
   the verify-bindings skill names the zero-match message.
3. The resolver order stays as built: the two environment variables
   first, the build's game folder last. Recorded only.

The deep-review report gains a maintainer decisions section.

#### fix(ci): v2.0.30 - review follow-ups for plan 010

`a4b90e4d`

Deep review (six lenses) and Codex adversarial review of plan 010
confirmed 9 defects, 0 HIGH, 1 MED; all fixed here.

- GameReferencesTargetsTests now compares the BUTR version's fourth
  part with ApplicationVersion.DefaultChangeSet, so a same-label BUTR
  build no longer passes; the reference guard reads the whole element
  (HintPath included) and counts only an unconditional import.
- The binding gate step fails when any check did not execute.
- Accurate text: the csharp.yml header, both build errors, the no-game
  recipe in .ai/verification.md (run as written from a clean tree), the
  CI failure signatures in tests.md, the CHANGELOG (#421) and the
  feature map.
- Behaviour-preserving: one _TaomNuGetRoot property (9 of 9 reference
  snapshots identical) and no stub copies in refasm-game/bin (gate
  338 of 338).

Report: docs/reviews/deep-review-010-ci-on-hosted-windows-2026-09-24.md
RCA: docs/reviews/rca-ci-on-hosted-windows-2026-09-24.md
Lessons: testing-qa (2), build-tooling-workflow (1); REVIEW-LOG 133.
Full suite: Failed 2 (known live-Armory), Passed 10246, Skipped 2.

#### fix(nullable): v2.0.30 - convergence fixes for plan 019

`de288136`

The ratchet gate now parses <NoWarn> the way the compiler does: it
splits on ';', ',' and space, and expands the "nullable" alias to the
seven ratchet ids. Before, "1701,8602" or ";nullable" turned off every
graduated folder while the gate stayed green. New rows cover each
spelling.

The graduation procedure builds with -p:DisableModuleCopy=true
-p:ModuleId=, so following it from a worktree no longer deploys into
the game install. siege.md now says the catch, like the no-settlement
path, hands vanilla a camp-1 array it throws on.

#### fix(warg): v2.0.30 - convergence fixes for plan 015

`56eb4bc8`

The convergence pass over fe8f30c7 confirmed four LOW defects:

- The mixed-list CheckTargets test could not fail for a lost i--,
  because every entry after a removal was a far target. The list now
  puts a must-drop entry after each removal, includes a null-visuals
  target and asserts each far target's frame read. Deleting each of
  the three i-- lines locally turned this test red.
- IAgentAdapter.IsWarg() lost its last caller in the review fix;
  deleted, and the test comment now names WargConfig.IsWargMonster.
- advanced-combat.md diagram: TakeDamage back under the callback.
- The testing-qa lesson's control-fixture clause is narrowed to IL
  rule tests.

Full suite: 10266 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

#### fix(siege): v2.0.30 - review follow-ups for plan 019

`155e3ea5`

Six deep-review lenses and Codex (gpt-6-astra, ultra) on plan 019 found
no runtime defect: 12 findings confirmed, 1 false positive, 0 HIGH.
Eleven are fixed here.

- /build-fix told a builder to fix CS8602 with `!`; the ratchet made
  that advice live. One row now covers all seven ids and points at the
  rules, and a DON'T line bans NoWarn and lowering a folder's severity.
- The graduation procedure, fix rules and hotfix escape move from the
  plan into code-quality.md; both .editorconfig comments point there.
- NullableRatchetGateTests fails if a ratchet id returns to a
  production NoWarn (shown failing on a temporary CS8602).
- SiegeCampGuardPatchTests covers all five paths of the prefix; the
  camp-2 test asserts the array itself is handed over.
- Patch8 comment, siege.md and the registry entry say the no-settlement
  path hands vanilla an array it throws on, and that vanilla cannot
  reach it; the harmony-il lesson no longer calls Patch8 a safe defer.
- The DTO comment, siege.md ring and test lines are corrected, and the
  no-op NoWarn line in Main/TAOM.csproj is gone (NoWarn evaluates to
  1701;1702 before and after).

Main build: 2 warnings, 0 errors, 0 CS86xx. Full suite: 10246 total,
10242 passed, 2 skipped; only the two known live-Armory tests fail.

Report: docs/reviews/deep-review-019-nullable-ratchet-2026-09-24.md
RCA: docs/reviews/rca-nullable-ratchet-2026-09-24.md
Not fixed, needs Mike: the GitHub issue for plan 019.

#### fix(warg): v2.0.30 - review follow-ups for plan 015

`fe8f30c7`

Deep review (six lenses) and Codex gpt-6-astra ultra on plan 015 found
no HIGH finding and no false positive. Fixes, tests first:

- BoneCheck's moved range gate is a positive requirement, so a NaN
  visuals frame no longer reaches the native skeleton fetch.
- WargTickCostTests loads the game assemblies, fails on a body it
  cannot read, rejects a List<Agent> built per scan, checks no node
  keeps a static service or buffer, and proves each IL rule on a
  control fixture.
- CheckTargets skip guards, a mixed target list, grid column order
  and a point moved since the rebuild are pinned.
- WargRiderHandManager.Tick reads the mount's Monster directly; the
  factory plumbing into WargMissionBehavior is gone. The attack scans
  look up the warg's adapter only once a candidate passes.
- CHANGELOG and feature docs: the scan-membership change between grid
  rebuilds, the spider tie order, test counts and this suite run.

Report: docs/reviews/deep-review-015-warg-tick-costs-2026-09-24.md
RCA: docs/reviews/rca-warg-tick-costs-2026-09-24.md

#### fix(enlistment): v2.0.30 - convergence fixes for plan 014

`41754a03`

The convergence review of the plan 014 review-fix commit found two
text defects, both confirmed against the code.

The narrowed reset wording still overclaimed. CommanderLordAdapter's
one-slot MapEvent cache and BattleMeritAccumulator._pending are
singleton fields the reset never clears, yet the CHANGELOG said the
reset drops "the feature's cached engine handles" and both "not reset"
lists read as closed. The CHANGELOG, enlistment.md and the
IServiceMaintenanceService doc now name what is cleared and list the
two fields.

RCA row 3 and the EnlistmentSessionResetTests summary still gave the
rhythm cache the "a moment ago" hazard; its cache test is an equality
check, so its hazard is a same-hour reload. Both now say so.

Text only, no behaviour change. Full suite: 10246 passed, 2 skipped,
2 failed (the two known live-Armory tests).

#### fix(patchshield): v2.0.30 - convergence fixes for plan 007

`31a31f16`

Three prose defects from the convergence pass, all confirmed against the
code and the live diag.log:

- Pass 2 at a later game start does not shrink: it attaches TAOM's late
  batch (about +140), which now costs more than the first pass 2. The
  OnGameInitializationFinished comment and the CHANGELOG say so.
- dr3-maintenance.md's diag.log sample drops the AliasStub line (never
  logged) and moves SESSION SUMMARY under process exit, its only caller.
- dr3-maintenance.md no longer claims a Native2Managed-off swallow path;
  that flag only decides at launch whether the finalizers attach.

Full suite: 10243 passed, 2 skipped, 2 failed (the known live-Armory
tests).

#### fix(crash-report): v2.0.30 - convergence fixes for plan 006

`70727529`

The convergence pass on the review follow-ups found three wrong text
claims. The registry now limits the tick finalizers' reach to the
callbacks Module.OnApplicationTick dispatches and names
OnSubModuleUnloaded as outside it. PatchShield's pass 2 is described as
shielding every patched method not declared in a TAOM assembly, not
every foreign-patched one. The coexistence how-to gives priority 800
only to the Patch37 rows and 400 to the callback shims. The priority
lesson names the HarmonyMethod constructor the bridge actually uses.

#### fix(enlistment): v2.0.30 - review follow-ups for plan 014

`b9d18458`

Six deep-review lenses and a Codex gpt-6-astra (ultra) pass over
7f02fc8d..d1221b7f found no runtime defect and 11 text and coverage
findings, all fixed here.

- The attachment service's session reset now also drops the adapter's
  cached commander party; the InvalidateCommanderCache pass-through and
  its separate call in ResetSessionCaches are gone (design lens, RED
  test first).
- New tests pin the load hook's reset and its order before normalizing
  (a sentinel thrown before CampaignTime.Now), and the new-campaign
  reset after a loading SyncData.
- The CHANGELOG, feature doc and comments no longer claim every
  per-session value is reset; the loss-modal latch and the duty pace
  estimate are named as follow-ups. A test comment this change made
  false is corrected.

Behaviour-changing proposals (reset before the authority gate on load,
the commander-loss latch, a stale record normalized on load) wait for
Mike. No GitHub issue exists for plan 014 yet.

Report:
docs/reviews/deep-review-014-enlistment-session-scope-2026-09-24.md
RCA: docs/reviews/rca-enlistment-session-scope-2026-09-24.md

#### fix(patchshield): v2.0.30 - review follow-ups for plan 007

`578ac7d6`

Six deep-review lenses and a Codex pass (one P3) on 7f02fc8d..0ad253d5.
The code did what the plan asked; the findings were about its claims.

- The ManagedCallbacks prefix reaches 88 classes in v1.5.3, not the 3
  shims: 79 ScriptingInterfaceOf* wrappers Native2Managed does not wrap
  are unshielded too. Now stated in the comment, dr3-maintenance.md and
  a CHANGELOG known limitation; narrowing it waits on Mike.
- On HandleAndSwallow's fallback paths nothing on a shim now swallows
  the missing-API trinity or preserves the stack. Disclosed; the fix
  belongs in CrashReportPatchHelper (plan 006's file) and waits on Mike.
- Corrected claims: ButterLib BEW puts blank transpilers on three shims;
  the 30x Harmony.Patch swing is one machine over time; pass 2 in the
  v1.5.2 log began at 20:11:39; pass 2 runs at every game start.
- New BindingVerification test selects the shims from the installed
  DLLs as Native2ManagedPatcher does (RED with the entry misspelt).
- dr3 log sample in write order with both passes; stale symbol refs in
  arena.md and harmony-il.md; test and policy summaries.

PatchShieldPolicyTests 24 pass. Full suite: 10243 passed, 2 skipped,
2 failed (the two known live-Armory tests).

Report:
docs/reviews/deep-review-007-patchshield-skip-callback-shims-2026-09-24.md
RCA:
docs/reviews/rca-patchshield-skip-callback-shims-2026-09-24.md
Not-tested: pass-2 attach count and timing in game (custom battle).

#### fix(crash-report): v2.0.30 - review follow-ups for plan 006

`65b691b6`

Six deep-review lenses and a Codex adversarial pass on
7f02fc8d..6fe83bca found no HIGH defect and 22 confirmed ones in tests,
comments and docs, all fixed here.

Tests: the native-capture-off bridge test now throws for real and checks
the recorded throw site (a bare return fails it), the allowlist is
pinned to its six names, every skip path in
Native2ManagedTargets.Resolve is tested, both engine-name tests run
under BindingVerification, and the shape test shares
HarmonyPatchBindingTests' resolver. The bridge is
attached by nameof, so a rename is a build error.

Docs: the bridge runs at priority 400, the boot saving is scoped to the
maintainer's desktop (players measured 0 to 1 s), BUTR stays disabled
after a capture until re-enabled or a restart, the dev triggers do not
reach the callback shims, the hero-race catch point follows the real
call graph, and the dropped callbacks are listed as a known limitation.

Full suite: 10258 passed, 2 skipped, 2 failed (the two known live-Armory
tests). Eight decisions for Mike are in the report.

Report:
docs/reviews/deep-review-006-crash-capture-boot-cost-2026-09-24.md
RCA:
docs/reviews/rca-crash-capture-boot-cost-2026-09-24.md

#### fix(siege): v2.0.30 - null-clean the Siege folder, nullable as errors

`7ad7a307`

Main/Features/Siege had 8 nullable warnings once the suppression moved
into .editorconfig: 7 annotations and 1 guard fix them all.

- Patch8_SiegeCampGuard now has an explicit branch for a besieger camp
  with no settlement and no camp frames. Before, settlement.GatePosition
  threw an NRE that the catch-all logged as a patch exception before
  returning true. The branch logs and returns true: the same outcome
  (defer to vanilla), without throwing on purpose.
- KingdomSiegeMessages properties are string? because Newtonsoft
  leaves a key null when a siege_defense_config.json entry omits it;
  Resolve takes string? and spells out the null check (net472's
  string.IsNullOrEmpty does not narrow).
- ActiveSiegeDefenseEvent's two ids default to an empty string; every
  construction site already sets both.
- The folder's new .editorconfig sets the seven nullable ids to error,
  so a new null-flow mistake here fails the build.

New tests: SiegeCampGuardPatchTests (2), calling the prefix directly on
an uninitialized BesiegerCamp. Plan 019, steps 2 to 7.

Not-tested: live siege with a settlement-less BesiegerCamp (not reachable in a normal campaign; SiegeEvent's constructor sets the settlement)

#### fix(enlistment): v2.0.30 - reset session caches on a new campaign

`e99d705a`

The engine fires OnNewGameCreated, not OnGameLoaded, for a new
campaign, so the commander party handle, the army handle, the
stale-battle anchor and the three new latches all leaked into a second
campaign in the same process. The reset is field clears only, so it
is not authority-gated.

Not-tested: a real second campaign in one process (needs the game)

#### fix(crash-report): v2.0.30 - make both capture toggles live

`9cfb91b1`

Plan 006, step 3 of 4. SubModule.OnSubModuleLoad gated the crash
patches on CrashReportSettings.Instance, but MCM builds its settings
provider in its own OnBeforeInitialModuleScreenSetAsRoot, after every
OnSubModuleLoad. The instance is always null there, so both gates
always took their ?? true fallback: players could not switch the
native-to-managed capture off, and the MCM restart prompt promised an
effect that never happened.

The gates are removed (runtime parity: they were always true), the
patches install unconditionally, and both toggles are read when an
exception arrives. Native2ManagedBridge gains HandleOrPassThrough: with
the toggle off it hands the exception back through
RethrowStackPreserver so the throw site survives. Both settings now
carry RequireRestart = false with hints that say what the code does,
and SettingRequireRestartPostureTests drops them from its allowlist so
it enforces the new posture. Defaults stay ON.

Not-tested: MCM toggling in a running game

#### fix(enlistment): v2.0.30 - ResetSessionCaches drops the session latches

`f6ac2d33`

ResetSessionCaches is the feature's one reset point, so the three new
resets are called from it. The maintenance service takes the wait-menu
presenter and the rhythm service to do so. The container wiring test
now substitutes IPlayerContextAdapter and IDutyOrchestrationService,
which the presenter's graph needs and which other feature modules
register in the live container.

#### fix(enlistment): v2.0.30 - reset the dwell anchor, offer latch, rhythm

`6fb37c9f`

The dwell anchor, the arrival-offer settlement id and cooldown stamp,
and the per-hour rhythm snapshot are absolute-clock state on
process-lifetime singletons. Each now exposes ResetForNewSession, and
the rhythm service's uncalled Invalidate is renamed to match. Wiring
follows in the next commit.

#### fix(crash-report): v2.0.30 - drop four finalizers on empty virtuals

`8bd44b92`

Plan 006, step 1 of 4. Four Patch37_CrashReport finalizers targeted
base virtual methods (MissionBehavior.OnMissionTick,
MBSubModuleBase.OnSubModuleLoad, MissionView.OnMissionScreenTick and
ScriptComponentBehavior.OnTick). Harmony rewrites the exact method it
is given, and an override is a different method, so these finalizers
only ran when an override called the empty base body. They could never
capture the throws they were written for.

Patch37TargetShapeTests resolves every Patch37 target from its
[HarmonyPatch] attributes against the installed engine and fails on an
overridable virtual, so the shape cannot come back.

#### fix(map-load): v2.0.30 - convergence fixes for plan 012

`a4492fa8`

The convergence pass on the review-fix commit found three prose
defects. The screen list the review called complete still left out
the barber and the face generator, which lower the loading window on
every ready frame through BodyGeneratorView.OnTick; the patch summary,
feature doc and CHANGELOG now name them and read as non-exhaustive,
and the lessons, REVIEW-LOG, RCA and report drop "full" and "every".
A test comment described the helper-hop mechanism wrongly (the tracer
skips frames by position, so a helper pushes the Postfix into the
first-caller slot). "Two lines above" became "one paragraph above".

Comments and docs only. Full suite: 10244 passed, 2 failed (the two
known live-Armory tests), 2 skipped.

#### fix(harmony): v2.0.30 - convergence fixes for plan 009

`4c728dac`

The convergence review of bdf7d515 found no runtime defect and three
text defects, all confirmed against the code.

- Plan 018 anchors on the ReportPatchFailures calls this fix changed
  (5 before, 4 now) and its ReportFaults repeats finding 1. Listed
  under FOLLOW-UP for a re-cut after 009 merges; plans/ added to the
  rename-grep lesson.
- The SubModule comment, crash-report.md and the lifecycle doc now name
  the assembly-wide category index case, where one attribute naming a
  missing engine type fails every category.
- RCA summary count corrected to 8 LOW and 2 nits.

Nits: the chat-log lesson names both subscribers (MPChatVM and
ChatLogMessageManager), and the lifecycle row for
OnBeforeInitialModuleScreenSetAsRoot points at :623 and mentions the
startup inquiry. Comment and doc changes only; full suite unchanged.

#### fix(bindings): v2.0.30 - convergence fixes for plan 008

`2ca0805b`

The convergence pass on the review-fix commit 549afffd reported three
LOW defects. All three were confirmed against the code and are fixed.

- notify-test-results.sh: the all-skipped branch fired before the
  "Failed" fallback, so a "Test Run Failed." or "Test Run Aborted."
  response whose only count was Skipped: printed PASSED WITH SKIPS.
  With no Passed: count the branch now needs "Test Run Successful.".
  Two new test_hooks.sh 7c cases went red first.
- verify-bindings Step 2 no longer says every failure is in its table;
  it names "Main/SubModule.cs not found" as a precondition to report.
- Two test comments state the resolver fallback exactly and drop the
  stale ~37 model count.

Full suite: Failed 2 (the known live-Armory tests), Passed 10243,
Skipped 2. test_hooks.sh: 289 passed, 0 failed.

#### fix(map-load): v2.0.30 - review follow-ups for plan 012

`15e5475a`

Deep review (six lenses) and Codex gpt-6-astra ultra on plan 012
found no HIGH or MED code defect. This commit fixes the four LOW
findings and records the review.

- A round-trip test runs the real Prefix into the Postfix; a Prefix
  hard-coded to true now fails it (mutant run).
- The lowered-trace test asserts one LogInfo, no fallback caller
  chain and a first caller that is not the patch class; a helper hop
  before TraceWithCallers now fails it (mutant run). The two
  signature tests are renamed to the three-part shape.
- The screen list (inventory, clan, kingdom, quests, character,
  crafting, banner editor), the gate's "unconditionally", the class
  summary, the feature-map row and a registry identifier corrected;
  the feature doc says raises are traced per call.

Still needs Mike: a GitHub issue for the change (/issue is public).
Full suite: 10244 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

Report and RCA, both in docs/reviews/:
  deep-review-012-loading-window-trace-per-frame-2026-09-24.md
  rca-loading-window-trace-per-frame-2026-09-24.md

#### fix(harmony): v2.0.30 - review follow-ups for plan 009

`bdf7d515`

The module-load failure notice was sent from OnSubModuleLoad, where
InformationManager.DisplayMessage has no subscriber yet, and the list
was cleared as it was sent. A chat line at the first main menu would
also be cleared after the splash video. Startup failures now go into
one inquiry from the main-menu one-shot, which Native's query manager
queues and the initial screen does not clear.

The summary no longer says a failed group is wholly off (Harmony keeps
the classes it applied before the failing one), and the docs name the
case that is not category-local: an unreadable attribute fails
Harmony's assembly-wide category index for every call.

The Data Flow lens and the Harmony lesson now grep for
TryPatchCategory, and the battle-load triage tool names the
[PatchApply] line instead of the deleted Patch43 warning. Stale
comments at the Patch77, Patch61 and Patch83 sites are corrected.

Tests: PatchCategoryApplierTests 13 (RED first for the notice and the
wording). Full suite 10248 passed, 2 skipped, 2 known live-Armory
failures.

Report:
docs/reviews/deep-review-009-guarded-patch-category-apply-2026-09-24.md
RCA:
docs/reviews/rca-guarded-patch-category-apply-2026-09-24.md

Deferred: failures left by an aborted game-init batch are labelled
"mission start"; rides on the plan's residual bare initializers.

#### fix(bindings): v2.0.30 - review follow-ups for plan 008

`549afffd`

Deep review (seven lenses) and a Codex adversarial pass on plan 008
confirmed 13 defects, 0 HIGH, and 1 false positive. Ten are fixed here:

- two tests pin the resolver's override and game-dir guards; deleting
  either guard turns one of them red
- notify-test-results.sh names the skips of an all-skipped run at
  normal verbosity (a new test_hooks.sh 7c case, red first)
- the hooks catalog and CHANGELOG no longer claim the banner reaches
  Claude: stderr from an exit-0 hook goes to the debug log only
- the verify-bindings skill states which gate tests go Inconclusive
  and triages the two red forms plan 008 added
- reflection-sites.md gives the strict gate command, the floor
  messages drop stale counts, the GameAssemblies comment is exact,
  and the CHANGELOG entry moves to its local date

Left for Mike: the banner's delivery channel (with Codex P3),
TreatNoTestsAsError, the CI step's if:, build folder first, and the
GitHub issue.

Report: docs/reviews/deep-review-008-binding-gate-no-silent-skips-2026-09-24.md
RCA: docs/reviews/rca-binding-gate-no-silent-skips-2026-09-24.md

#### fix(harmony): v2.0.30 - apply every patch category through one guard

`45bcf80b`

SubModule applied its Harmony patches with bare
_harmony.PatchCategory("PatchNN_X") calls, 64 of 84 with no guard.
Harmony 2.4.2 has no catch around a category, so one patch class whose
target no longer resolves threw out of the SubModule hook: in
OnSubModuleLoad the engine rethrows and the game does not start; in
OnGameInitializationFinished the once-per-process flag is already set,
so every later category (the Patch65, Patch82 and Patch84 crash guards
among them), the watchdogs, ManualPatchApplicator and the Harmony
census were skipped for the process.

Every category now goes through TryPatchCategory, backed by the new
PatchCategoryApplier. A failure logs [PatchApply] at Error with its
full cause, costs only that category, and is named in one red
on-screen line per phase. The delegate passes typeof(SubModule)
.Assembly explicitly because the one-argument overload reads the
caller's stack frame. The Patch37, preview-batch and Patch77 sites
keep their side effects (hooks subscribe only on success, the preview
log says FAILED, a Patch77 failure still disables the switcher); nine
per-category try/catch blocks collapse into the helper. A failed
Patch61 or Patch89 main category no longer skips its sub-categories,
on purpose.

The Patch37 comment claimed its finalizers covered the rest of
OnSubModuleLoad; they cannot, and the comment now says why. Nine text
tests now pin TryPatchCategory( and the Patch65 test comment no longer
claims the batch is unguarded. PatchCategoryApplierTests adds 9 tests,
including the real Harmony 2.4.2 throw and a source gate against any
new direct PatchCategory call in Main.

Implements plan 009 (plans/009-guarded-patch-category-apply.md).

Not-tested: live engine apply path and the red notice (needs the game)
Save-compat: no save data touched

#### fix(config): v2.0.30 - Review 130 follow-ups: list entries, formations

`b2e387db`

The four owed follow-ups from Codex review 130, reviewed and fixed
where they were a problem.

- DreadAura, UncapturableHeroes and BannerBearers config loaders drop
  a null or blank list entry with a warning and trim a padded one.
  Before, a padded name matched nobody without a word; a padded
  excludeHeroIds entry left its hero uncapturable.
- AllowedFormationGroups kept anything Enum.TryParse accepted
  (" infantry ", "2", "Infantry, Ranged"), which the service's name
  compare never matches: as the only entry it switched every banner
  bearer off. It now takes declared names only through the new
  TAOM.Core.Validation.EnumNames (StrikeNames' private parser, moved
  and shared), stored as the enum prints them.
- CustomAttacksUtils comments and tests no longer give the spider AV's
  retracted cause and name the real native sinks; comments only.
- Sharing one compiled lords.xslt would save under a second (one
  full-file test). Still owed: nazghul in the Load Game thumbnail's
  safe list, after an in-game render test.

No change with the shipped configs. Other instances of the same
classes are listed in REVIEW-LOG Review 130 for Mike's word; no issue
filed. Reviewed: /deep-review, six lenses and a convergence pass; RCA
rows in docs/reviews/rca-nazgul-scream-2026-09-23.md. Full suite:
10303 passed, 2 skipped. This commit alone on c79a5852 in a clean
worktree: 10235 passed, 2 skipped, 2 failed; both failures
(ElkConfigTests, AnimaliaMountWiringTests) fail the same on bare
c79a5852, whose elk and moose data is still uncommitted.

#### fix(nazgul): v2.0.30 - Codex review follow-ups for #644 and #645

`437d5911`

Codex (gpt-6-astra, ultra) reviewed 9804f67b and b90fd3a4 in one run:
no P1 or P2, two P3 observations, both fixed.

- A signature whose identity list held only a null or blank entry
  loaded as valid and matched nobody. ValidateList now removes a null
  or blank entry with a warning and trims a padded one, as the id and
  the sound already were.
- The #644 race gate read one regex spelling of the XSLT race
  attribute. RacesEmittedByXslt now parses each stylesheet, reads every
  race verbatim as the engine does, and fails on a race computed at
  transform time; it finds the same 19 emissions.

Verifying Codex's notes corrected four sentences: the restore-skip
comment and hero-race.md now name all three runtime race writers
(co-op join reconciliation was missing), the #644 CHANGELOG entry says
176 (not 173) and that only a new campaign gets the one kit, and the
sound gate's comment and doc no longer cite the spider AV as a proven
NaN crash.

The fix diff got its own six-lens deep review (two MED, several LOW,
all fixed); Mike kept the stylesheet scan over checking lords.xslt's
transform output. RCAs: the "Codex pass" sections of
docs/reviews/rca-nazgul-race-2026-09-23.md and
rca-nazgul-scream-2026-09-23.md; REVIEW-LOG Review 130. Full suite:
10208 passed, 2 skipped on the shared tree, and 10106 passed, 2
skipped for this commit alone on b90fd3a4 in a clean worktree. Not
smoked in game.

#### fix(mission): v2.0.30 - park off-thread callback writes (#634)

`f75288eb`

A player froze mid-battle on the 1.4.5 line: heap flat, no exception,
no crash report. The #595 tripwire had caught OnAgentRemoved,
OnObjectUsed and four more engine callbacks off the main thread, which
the committed thread map said could not happen. The next frame's
OnPreTick waits in Mission.WaitTickCompletion for the agent tick, so a
stuck agent tick freezes the game with exactly this log shape.

Every TAOM writer those callbacks reach now keeps off main-thread state:
DeferredCallbackQueue.RunOrDefer runs a write inline on the main thread
and parks it for the owner's next OnMissionTick anywhere else. Parked:
the behaviour-tree component's removal, mount despawn's kill record and
forget, the career teardown when the player falls and its hit notice,
SpatialGrid removals and the warg dismount reset. Each owner marks the
main thread itself. RegisterKill takes a lock (its map's insertion
order breaks kill ties), the SignatureStrikes roster is a concurrent
map, the enlistment kill count is Interlocked. Off-thread reports go to
the file log at WARNING, never to an on-screen logger.

The cause is not proven, so Patch91 brackets the agent tick and the
main thread's whole mission frame (MissionState.TickMissionAux, armed
only while Continuing) and MissionTickStallWatchdog logs each stuck
thread's stack as [MissionStall] at 10/20/40 s. New MCM toggle, default
ON, independent of the master toggle, local-only for co-op.

Deep review (6 lenses + convergence) widened the writer census from
three to nine, moved the frame probe off Mission.OnTick (blind to a
hang inside native Mission.Tick, and it false-alarmed on teardown), and
reverted one proposal that would have scrambled kill-tie order. RCA:
docs/reviews/rca-offthread-agent-removed-2026-09-22.md.

Research: Mission.OnTick/OnPreTick/TickAgentsAndTeamsImp/OnAgentRemoved, MissionState.TickMissionAux (v1.4.8 dump + v1.5.3 taom-src, identical)
Constraint: native raises OnAgentRemoved and friends on the thread it chooses; no managed code shows which
Rejected: ConcurrentDictionary for the field commission kill map (EndBattle ties follow insertion order)
Not-tested: Thread.Suspend capture on a live thread; SignatureAgentRoster and Enlistment races (a live Agent cannot be built in tests)
Save-compat: no saved state touched

#### fix(mumakil): v2.0.30 - the tower's crew stand on a navmesh (#627)

`3515c5c5`

All eight archers confirmed drawing and loosing in game. The feature shipped
two days ago with fifteen green gates and a design that did not work at all:
every archer was placed on its seat correct to the centimetre in x and y, and
fell nine metres to the ground.

A teleport cannot hold an agent up. TeleportToPosition is a bare native
SetPosition with no ground snapping, so it reads as though it must work, and
at the elephant's 3.2 m it has worked for months. At nine it holds nobody:
25,000 teleports in one battle lifted no one off the ground. NavMeshPrefabName
appears in exactly two engine classes, SiegeTower and MissionShip, and neither
contains a single call that moves an agent. Both attach a navmesh prefab whose
faces ride their own entity. This one now does the same, face group 1, the
group that travels unconnected; 2 and 3 would splice a deck nine metres up
into the world navmesh.

Navmesh is necessary and not sufficient. Stripping the floor slabs while
keeping it dropped everyone again: navmesh makes a position valid to stand at,
a body is what you stand on. The slabs stayed, squashed 61 cm to 3 cm, because
the body is a door lintel whose raised ends stood 70 cm proud at the platform's
scale and four archers were standing on them.

Two placement rules, both now gates. A 0.74 m body overhanging its navmesh or
its deck edge gets resolved elsewhere and sits at a fixed height above its
seat, immovable; three held there through 7,619 teleports each. And the most
forward frame on each deck re-nocked forever while its deckmates shot, three
times in three battles: the beast's head is at their height and the mahout is
under the sightline. Frames now sit a body radius inside the deck, behind the
shoulders.

The prefab is authored at final in-game size with no runtime scale, since no
vanilla object carrying physics or navmesh is ever runtime-scaled; a test pins
body_length=300. Locomotion is pinned once at seating rather than on the
re-assert clock, after re-applying it mid-draw matched the draw dying on all
eight seats in lockstep.

Full suite green: 9,912 passed, 2 skipped, 0 failed.

Research: MissionObject.AttachDynamicNavmeshToEntity, SiegeTower, MissionShip,
  Agent.TeleportToPosition, Agent.SetMaximumSpeedLimit, Mat3.Orthonormalize,
  Native/monsters.xml human body_capsule
Constraint: the navmesh .bin's face encoding is not understood, so its topology
  cannot be changed without a fresh Kit export; vertex positions can be and were
Not-tested: a campaign battle with several mumakil, mission end with crew
  aboard, and shooting at a target directly below
RCA: docs/reviews/rca-mumakil-navmesh-2026-09-22.md

#### fix(ranged): v2.0.30 - #617 second review: guards, cache, crossbow rule

`b904e02a`

The tools half of the #617 second deep review and of the review of
its fixes. The live Armory's 27 ladder crossbows were regenerated from
the new spec; the lotraom-assets mirror commit carries them. RCA:
docs/reviews/rca-ranged-rebalance-second-review-2026-09-18.md

- Crossbows out-hit and out-aim every bow of their kingdom at the same
  tier (Mike): crossbow_bonus 3 to 9, crossbow damage T7 to T10
  107 / 126 / 134 / 142. Pinned by a shipped-spec test.
- rebalance_ranged_ladders refuses a planned cell no file defines
  (the placeholder merge had let it through with exit 0).
- The sync tool keeps the 12 translation caches in step (-130 / +123
  ids each), backs up live files, refuses an unparseable ladder row, a
  bad spec, an unreadable cache or a missing --cache-dir.
- restat validates its tables, including an id in both of them, in
  the shared validator; generator and restat write no sidecar into
  the mirror.
- rebalance_troops: the Iron Hills nobles leave SKIP_TROOP_IDS; the
  militia regex is the validator's object; one battle_sets reading
  with the ladder; undefined_ladder_ids reads the rosters as XML, so a
  single-quoted reference is seen.
- analyze_troop_balance references a ladder troop's cell (it judged
  179 of 227 archers by the level curve).
- planned_skill_edits no longer refuses a templated ladder troop: on
  1.5.3 an inline skill row overrides the template.
- enlist_ rosters join the hero ceiling sweep; one ladder id regex;
  shared mirror path and tier numeral regex; dead code removed.

Tools suite 1773 passed; generator, restat and sync --verify OK in
both trees; roster tool 0 pending edits; validator no error and no
RANGED_* finding.

Research: DefaultItemValueModel.CalculateAmmoTier (1.5.3)
Research: BasicCharacterObject.Deserialize (1.5.3 against 1.4.8)
Research: CustomBattleAgentStatCalculateModel.GetWeaponInaccuracy
Not-tested: in game (Custom Battle, campaign crossbow against bow)

### Data

#### data(creatures): v2.0.30 - chariot and spider hit capsules fitted

`68529428`

The chariot's and spider's per-bone hit capsules were Modding Kit
defaults (57 of 60 and 40 of 62). Fitted to the skinned mesh with
tools/skeleton_hit_capsules.py and patched into the live Armory
packages: chariot 8.8% to 98.0% of the skin hittable (53 refit),
spider 49.9% to 97.2% (38 refit; the Brown and Pale Spiders share
the skeleton). Both read back through TpacTool.Lib with zero
mismatches. The chariot's cart body and wheels stay mostly
unhittable; whether cart hits should damage it is left open. The
Yotthani adoption report records the finished download matches.

Not-tested: in game; load the Armory in the Kit once, then a Custom
Battle hit test on each
Refs: #624

### Performance

#### perf(battlebalance): v2.0.30 - read the MCM settings once per process

`7feca96b`

Plan 003, ported from the June branch impl-003 (6eb5955c) through the
review pipeline (Mike's decision 17); impl-003's warg half is
superseded by plan 015.

BattleBalanceSettingsProvider resolved TaomSettings.Instance on every
property read, and GetDefaultTroopPower reads up to seven per troop per
simulation round. It now takes the reference once in its constructor
and reads through it, so live MCM edits still apply, as in
NameplateFadeSettingsProvider.

Precondition, checked: the only resolve is RegisterBattleBalanceAndTargeting
under OnGameStart, after MCM creates the settings; the eager pass at the
end of IoC.Configure (OnSubModuleLoad) does not reach it, so the
constructor never caches a null.

Tests: 6eb5955c's six no-MCM default pins (green before and after), and
a new IL rule, Getters_NeverReadTaomSettingsInstance_TheConstructorDoes,
RED against the old provider (the getters read Instance). Full suite:
10320 passed, 2 skipped, 0 failed.

#### perf(hooks): v2.0.30 - skip Python in Bash hooks on non-git calls

`90141357`

Every Bash call ran 13 hook scripts, each starting Python twice (the
_pybin.sh probe, then a JSON parse) before looking at the command.
Each Bash hook now tests the raw payload for its trigger text (git,
dotnet or build.ps1) first and allows without Python when it is
absent: 256-451 ms per hook on an ls drops to 60-150 ms. New
tools/test_hooks.sh section 4c pins both directions; an old-versus-new
run over 156 payloads found no changed decision. Plan 013.

Committed by the orchestrator: the plan's pre-commit STOP fired only
because another session had staged files in the main tree's index,
which the harness gates read; this commit is made in the worktree's
own index and carries its own CHANGELOG entry.

Not-tested: the edited hooks under a live harness (a worktree's hooks
do not run in the session; proof owed after merge)

#### perf(combat): v2.0.30 - range-gate bone targets before skeletons

`7577894d`

While a warg's bite is live, the bone check asked the engine for a new
native Skeleton wrapper (a ref-count call, a lock, a GCHandle and a
finalizer) for every agent captured within 20 m on every frame, before
testing whether that agent was inside the roughly 4.5 m gate it can
actually hit (plan 015).

The target loop moves into CheckTargets and tests the same gate (the
target's visuals frame origin against the attacker's, same comparison)
before fetching the skeleton; a target outside it stays for a later
frame. The attacker's skeleton is fetched once per tick and its bone
positions reuse one list. The stale #219 comment in
BoneCheckDuringAnimation now names the real gate.

Per the orchestrator's amendment to plan 015, the target skeleton's
null test is `is null`: `== null` binds to NativeObject.operator ==,
whose first use runs NativeObject's native static constructor and threw
TypeInitializationException in the test host. The operator returns the
same result for a null comparison.

BoneCheckRangeGateTests pins the gate: a far target's skeleton is never
fetched and it stays a target; one on the gate edge is fetched.

Not-tested: BoneCheckDuringAnimation.Tick (ActionIndexCache, engine).

#### perf(combat): v2.0.30 - key SpatialGrid cells on x and y only

`e5f644da`

The grid was keyed on x, y and z at 20 m cells, so the warg's 60 m
"no enemy close" scan looked up 7 x 7 x 7 = 343 cells on every tick
while a battlefield is a few metres tall (plan 015). Cells are now
keyed on (x, y): that scan looks up 49 cells and a 10 m scan 4. The
distance test stays 3D, so every query returns the same set of agents,
which SpatialGridQueryTests' brute-force comparison confirms.

Order within one 20 m column is now rebuild order rather than lower
height band first. Only a stop-on-first-hit bite with two targets in
reach on the same frame, straddling a multiple of 20 m in height, can
land on the other one.

#### perf(crash-report): v2.0.30 - log repeat suppressions at powers of ten

`fb52e619`

Plan 006, step 4 of 4 (code). CrashReportService.HandleException wrote
one suppression log line per occurrence, so a crash that recurs every
frame wrote a line per frame into the taom_debug.log that the next
bundle tails.

CrashBundleThrottle.IsLoggedOccurrence now picks occurrences 1 and 2
(the only trace of a cap or cooldown suppression, and the first repeat
of a bundled crash) and then every power of ten. Suppression itself is
unchanged; only the log line is gated.

Not-tested: the gated LogError inside HandleException (needs the full collector graph)

#### perf(warg): v2.0.30 - resolve tree services once, scan into buffers

`87f9f862`

A warg tree's root runs on every mission tick, so four node types and
the player's rider-hand manager paid a DryIoc lookup per frame, and the
three enemy scans allocated a fresh List<Agent> each call (plan 015).

The nodes now resolve IMissionAdapterFactory (and WargAttackTask its
IWargAttackService) once into private readonly instance fields when
the tree is built, and scan into a reused buffer through the
zero-allocation SpatialGrid overload. WargRiderHandManager.Tick takes
the factory WargMissionBehavior resolves in its constructor, whose
signature is unchanged. The warg adapter lookup is hoisted out of the
two attack-check loops; the factory caches adapters per Agent.

WargTickCostTests pins both rules in the IL. Its one-level-down
descent skips a same-type helper whose body names a
TaleWorlds.MountAndBlade.View type, which the test host cannot load.

Not-tested: node constructors resolving in game (needs a mission).

#### perf(patchshield): v2.0.30 - skip the ManagedCallbacks callback shims

`2a3f71b1`

PatchShield pass 2 runs inside the loading screen of the first game
start and attached 372 finalizers in about 69 s on the maintainer's
machine (about 186 ms per Harmony.Patch). About 247 of those land on
the engine's native-to-managed callback shims in namespace
ManagedCallbacks, which TAOM's Native2ManagedPatcher already wraps
with a finalizer that swallows every exception while crash capture is
on. PatchShield adds nothing there: its rescue strips prefixes,
postfixes and transpilers, and those shims carry only finalizers.

The hot-layer exclusion list moves from PatchShield into
PatchShieldPolicy (unchanged for the three #331 entries) behind a pure
IsExcludedTargetNamespace, and gains "ManagedCallbacks". PatchShield's
IsExcludedTarget delegates to it and still fails open.

The shield pass line in diag.log now ends with the pass's elapsed time
and ms per attach, formatted by the tested FormatShieldPassSummary with
the invariant culture; the existing prefix up to "(total: N)" is
byte-identical.

Seven new PatchShieldPolicyTests pin the moved list, the gameplay
namespaces that must stay shielded, the new entry and the formatter.

Plan 007 (plans/007-patchshield-skip-callback-shims.md).

Not-tested: live pass-2 attach count and timing (needs a game start)

#### perf(crash-report): v2.0.30 - allowlist six callback shims, not 247

`1d94df9c`

Plan 006, step 2 of 4. Native2ManagedPatcher patched every static
method of the engine's three *CallbacksGenerated classes (247 on the
live log). Each harmony.Patch costs about 120 to 190 ms on the
maintainer's desktop, so the sweep took 29 to 33 s of every launch,
and in 30 logged sessions it never captured an exception.

The patcher now attaches the finalizer to the six shims listed in the
new Native2ManagedTargets, each chosen because it covers managed work
no Patch37 finalizer already wraps: the screen early, late and input
ticks, ManagedScriptHolder.TickComponents (the real dispatcher of every
ScriptComponentBehavior.OnTick override), the thumbnail completion
callback and the character tableau setup. A missing name costs one
warning line, and the attach now logs its own time.

Native2ManagedTargetsTests pins every name against the installed
engine and keeps the list at most 12 entries.

Not-tested: the live attach and its boot time need a game launch
Research: v1.5.3 decompile of ManagedCallbacks.*CallbacksGenerated

#### perf(map-load): v2.0.30 - log a loading-window lower only when it drops

`e6c03411`

Plan 012, step 2 of 3. The Patch89 lifecycle postfix on
LoadingWindow.DisableGlobalLoadingWindow walked the managed stack and
wrote a flushed LogInfo line on every call. The engine calls it on
every frame of the main menu, the party screen and character
creation, so one 35-minute session wrote an 84 MB log that was
262,763 lowered lines, and three hours on the main menu wrote
1.16 GB.

The engine clears IsLoadingWindowActive unconditionally before any
postfix runs, so a Prefix now captures the flag into __state and the
Postfix traces only when LoadingWindowTraceGate.IsRealLower says the
window went from up to down. TraceWithCallers is still called
directly from the Postfix, so its two-frame skip still lands on the
real caller. The Enable patch is unchanged. Four tests pin the
Prefix and Postfix shapes (Harmony binds __state by name) and both
behaviours.

Not-tested: Harmony applying the Prefix and handing __state to the
Postfix in game, and the frame-time saving; both need an in-game
session.

#### perf(map-load): v2.0.30 - add the loading-window real-lower gate

`8028bf3d`

Plan 012, step 1 of 3. LoadingWindowTraceGate.IsRealLower decides
whether a DisableGlobalLoadingWindow call actually took the window
down: the flag was true before the call and is false after it. The
engine calls that method on every frame of the main menu, the party
screen and character creation and clears the flag unconditionally,
so only the before and after values together separate a real lower
from a no-op. The gate is a pure function of two bools so the patch
stays a thin boundary (ADR-002, ADR-008); four tests pin the whole
before-by-after table.

### Refactoring

#### refactor(hooks): v2.0.30 - drop the push line ordering

`60d85ed7`

The force-push guard sorted the lines it judges so that lines able to
force came first, shortest first. That order only existed to make the
old bash judge reach a short force push before a long commit message
holding "push" in time. The judge now runs in Python inside a
deadline and takes every shape in well under a second in any order,
so the sort, its FORCE_HINT pattern and the four tests that pinned it
are gone (#680). The lines are judged in the order they are produced,
and a new test pins that order.

Which pushes are refused does not change. What changes is the branch
a message names when one command refuses or warns about more than
one: the guard still stops at the first refused line and keeps the
last warning, so the branch named now follows the order in which it
reads the command rather than line length. A sweep of 1,093 commands
(4,372 runs per hook, both shell tools, a trunk and a feature
checkout) against the previous commit found 0 exit-code differences
and 349 message-only ones: 319 BLOCKED and 30 WARNING lines name a
different branch.

The 7i comment in tools/test_hooks.sh and the tooling lessons record
the change.

#### refactor(seams): v2.0.30 - move decision logic out of nine seams

`a3957541`

Nine protected-virtual seams in SupplyOrderService, RefugeService,
WardenService and CampService carried TAOM decisions that no unit
test ran, because the test subclasses override every seam: the supply
payee routing and its unreachable-payee fallback; the refund route,
count filter and volunteer-slot walk; the refuge raid-target filters
and nearest pick; the peace release of refuge-held prisoners; the
warden companion filter; the promotion policy (culture, template
fallback, age and rename tolerance); and the nearest town-or-castle
search behind the camp and refuge keep-outs.

Each decision moves into its service as an internal method, and each
seam is now one engine operation with ids, primitives and TAOM data
classes in its signature, per the ADR-007 seam rule (plan 021,
decisions 49, 55, 56 and 57). The two identical fortification
searches now share FortificationSearch.NearestDistance, declared in
CampService.cs. Behaviour is unchanged: amounts, flags, log texts,
filter order, the strict comparisons, the tie-break, the RNG draws,
the silent drop of surplus recruits and the backwards prisoner walk
are as before. The war checks stay separate seams, so the engine's
MapFaction is read only where it was before. 76 new tests pin the
moved logic. This implements improvement plan 026.

Not-tested: the seam bodies (GiveGoldAction, the roster, item and
volunteer-slot writes, the MobileParty.All and Settlement.All scans,
EndCaptivityAction, HeroCreator and the enrol actions) run only in
game.

#### refactor(cache-rebuild): v2.0.30 - delete two unreachable scaffolds

`032481cc`

IEditorSceneAdapter had no implementation and no reference. The
EditorCacheRebuild Caching/ folder (PathReuseCache,
PersistentPathCache, their interfaces, NavigationPathCloner,
SortedPathKey) was registered in EditorCacheRebuildIoC but never
resolved or injected, so it never ran. Both came in with 6a80bac6
(2026-05-12) and never gained a caller.

Removed with them: the two registrations, the reserved
EnablePathReuse and EnablePersistentPathCache config properties
(not in the shipped JSON, never read), their 26 tests, and the
ReflectionSiteBindingTests row for PathReuseCache._store, which
named a TaleWorlds.Engine type that does not exist and resolved
only to TAOM's own class. A new test pins that a config still
carrying the two keys loads; another pins the types' absence.

Git history is the recovery path: 6a80bac6 holds the scaffold if
Phase 2 path reuse is ever built. Plan 025, maintainer decision 21.

Full suite: 10288 passed, 2 skipped, 0 failed.

Not-tested: in-game distance cache rebuild; no runtime path changed.

#### refactor(composition): v2.0.30 - move WandererAllegiance into a module

`44045b34`

Plan 018, step 2: the first feature moved off the two single-owner
files. WandererAllegianceModule registers the feature's services
(WandererAllegianceIoC now takes an IRegistrator) and declares its
dialog behavior; FeatureModules.All lists it, and its registration line
in Main/IoC.cs and its AddBehavior line in Main/SubModule.cs are gone.

The behavior is still a container singleton. It is now added after
every hand-wired behavior, which is order-free: its two lines are the
only TAOM lines on companion_hire and outrank vanilla's reply by
priority (110 over 100), and LotrIssueSuppression.SuppressAll removes
only vanilla issue types.

FeatureModulesTests gains generic declaration tests: a type declared by
a module and still wired by hand in SubModule.cs, a behavior, slot or
category declared twice, and a behavior that persists data without
OwnsSaveData all go red. WandererAllegianceWiringTests replaces its two
text asserts with a list check, a no-hand-registration check and a
handshake against a real DryIoc container. The feature doc and
CHANGELOG describe the new wiring.

Not-tested: in-game wanderer refusal (needs a campaign)
Save-compat: the behavior's SyncData is empty; no save data touched

#### refactor(composition): v2.0.30 - add the feature-module runner, empty

`9ed3ee30`

Plan 018, step 1. Every feature is wired by hand into Main/SubModule.cs
and Main/IoC.cs, so most feature commits edit one of the two
single-owner files and parallel sessions collide there. This adds the
contract a feature can own instead, with an empty module list, so
behaviour is unchanged.

Main/Composition holds the module contract (ITaomFeatureModule and the
TaomFeatureModule base with empty defaults), the declaration types for
patch categories, campaign behaviors, game models and mission
behaviors, the ordered FeatureModules.All list, and ModuleRunner: it
visits modules in list order, logs and skips a module that throws for
the rest of the session, and fails closed only for a module that owns
save data in the three steps that decide whether its SyncData runs.
Module categories go through plan 009's guarded TryPatchCategory, so a
failed category is reported by ReportPatchFailures, not as a fault.
FeatureModuleHooks is the engine-facing half; it holds startup faults
for one main-menu inquiry and shows in-game faults as a red line, the
startup inquiry rule plan 009 follows.

The kernel gains one line per phase: IoC.Configure registers modules
after the last hand-wired feature and initialises their statics last;
SubModule calls the runner at the end of each phase's feature block,
before 009's report for that phase. FeatureModulesTests pins each call
between its anchors.

Trade-off: six small types and eight kernel lines before a second
module uses them. In return every later migration only deletes lines
from the two single-owner files.

Not-tested: FeatureModuleHooks against a live engine (needs the game)
Save-compat: no save data touched

#### refactor(combat): v2.0.30 - pin SpatialGrid queries behind helpers

`b8b14463`

The grid's cell build and radius query only ran through Agent, which
no test can construct, so nothing pinned what a scan returns (plan
015). Both now live in internal static generic helpers, BuildCells and
CollectInRadius, which never touch an Agent; UpdateGrid and the buffer
overload of GetAgentsInRadius delegate to them. The query reads each
agent's position once instead of three times and reports how many
cells it looked up. The grid is still keyed on x, y and z here, so
behaviour is unchanged.

SpatialGridQueryTests checks the helpers against a brute-force sphere
scan over 600 points, five radii and 25 centres each, plus the
inclusive boundary, a tall column, the include filter and buffer
clearing.

### Tests

#### test(tools): v2.0.30 - cover a never-retried failure and a URL scheme

`0cc91b32`

Two gaps the mutation check found after the tools fixes. The watch
fixture's only failed agent also had its key retried, so a watch that
ignored failed rows still passed; an agent that failed and was never
retried now stays quiet only when the failed row is read. The pinned
local-path rule declines a drive letter followed by two slashes, so a
one-letter URL scheme such as s://bucket/key is not a local path; a
test now holds that case.

#### test(ci): v2.0.30 - tag the sprint's engine-bound tests RequiresGame

`e9b28e2a`

Replaying the C# CI workflow on the merged sprint (reference
assemblies, no game) failed 69 unit tests the sprint branches added
before plan 010's CI existed. 68 threw from engine code the metadata-
only reference assemblies stub out (CampaignBehaviorBase, Mat3, Vec3,
ViewModel, Debug.DebugManager, ActionIndexCache, TextObject and
others); the last is a module-isolation test whose engine base
constructor throws on the stubs, so the isolation count reads 0.

Five classes where every test needs the engine get the class tag; in
ten others only the failing methods are tagged, as the tests rule
permits, so the stub-safe tests keep running in CI.

Replay after tagging: unit 8494 executed, 8494 passed, 0 failed
(floor 7700); binding gate 344 of 344.

#### test(composition): v2.0.30 - read SubModule and IoC through one reader

`a0db0558`

Plan 018, step 0. The 26 test files that guard the wiring in
Main/SubModule.cs and Main/IoC.cs each located and read those files
their own way, as raw text. A raw read cannot tell code from a comment,
and a missing file sent most of them Inconclusive, so a gate could stop
guarding without saying so.

RepoPaths gains ReadSource (CRLF normalised, either separator, a missing
file fails the test) and StripComments (lifted from
CoopVetoClassificationTests: blanks comments to spaces, keeping length
and line breaks). All 26 files now read both files through
RepoPaths.ReadSource with stripComments on; their guards and orphaned
locator helpers are gone, and no assertion changed.

The switch exposed one false pass: GameModelOverrideBindingTests counted
TaomPartyNavigationModel as registered only because a commented-out
AddModel line contained "new TaomPartyNavigationModel(". The parked
model is now an explicit ParkedModels entry with its reason, and a new
test keeps that list honest in both directions.

Not-tested: none (test-only change)

#### test(bindings): v2.0.30 - make the binding gate fail loudly on skips

`8c89e042`

The BindingVerification gate passed by skipping. GameAssemblies found
the install only through BANNERLORD_OVERRIDE_DIR or BANNERLORD_GAME_DIR
in the test process, so a DLL built against the game but run without
them went Inconclusive, which MSTest reports as Skipped with exit 0.
The test-results hook then printed "PASSED (33 tests)" over 335 skips.

- TAOM.Tests.csproj records the build's GameFolder as TaomGameFolder
  assembly metadata; GameAssemblies falls back to it after the two
  variables, which keep precedence. Six new resolution tests.
- TAOM.Tests/binding-gate.runsettings maps Inconclusive to Failed for
  the gate only. The verify-bindings skill, the docs that give the gate
  command and a new CI step pass it explicitly. The default suite still
  skips when the game or the Armory is absent, as decided.
- The three discovery floors (fewer than 30 patch types or 20
  GameModels) now fail, since they only run once the game has loaded.
- notify-test-results.sh names skips (PASSED WITH SKIPS, and a skip
  count on a red run), pinned by tools/test_hooks.sh section 7c.

Evidence, strict gate on the desktop (368 tests): with the environment,
0 failed, 0 skipped; with both variables unset, 0 failed, 0 skipped
(the fallback); with a fake game folder, 335 failed and exit 1 (the
mapping); the same fake folder without the settings file, 335 skipped
and exit 0 (the decided default skip). Full suite: 10241 passed,
2 skipped, 2 failed (the two live-Armory tests that fail at the base).

Implements plan 008 (binding gate, no silent skips).

Not-tested: the discovery floors firing (needs a type-load break)
Not-tested: the new CI step (build.yml ignores pushes to 1.5.x)

### Documentation

#### docs(skills): v2.0.30 - Codex effort is sized to each review

`ef9559b1`

Codex reviews no longer always run at the ultra reasoning effort. The
session now chooses the effort for each dispatch from the size and risk
of the change: ultra or max for a large or risky first pass, xhigh for
a mid-size change, high for a focused pass over a few fixes. Every
codex exec template in the review-codex, codex-verify and deep-review
skills, the completion workflow, the Codex integration reference and
the review guide passes -c model_reasoning_effort="<level>", and the
message sent at dispatch names the level, the reason and the expected
window. The .codex/config.toml pin of ultra stays as the default and
ceiling, and the model stays gpt-6-astra.

Reviewing a git worktree: the skill now says to dispatch from the
worktree itself. codex doctor run in a worktree reports the same model
and three MCP servers as the main checkout, so the project config loads
there, and the sandbox then covers only the worktree. /improve keeps
dispatching from the main checkout on purpose, since its prompt forbids
every edit.

Stale text corrected on the way: codex-verify claimed AGENTS.md was over
32 KB and prescribed a check for a string AGENTS.md does not contain,
its report template named an old model, and the review guide still sent
readers to a retired Codex plugin.

#### docs(skills): v2.0.30 - hang triage confirms the process is the game

`9c6d4962`

The hang procedure in /native-crash-triage now opens with a step that
identifies the process before anything else. The Modding Kit also runs
as TaleWorlds.MountAndBlade.Launcher.exe, from bin\Win64_Shipping_wEditor,
and its long jobs (the settlement distance cache) sit at Not Responding
for minutes, so a process name proves nothing. The step reads the image
path, matches the PID to the session's rgl_log_<pid>.txt, and forbids
telling the user to end a process that has not been identified.

The lesson behind it is appended to
docs/reviews/lessons/build-tooling-workflow.md: a Custom Battle closed
at its victory screen was mistaken for a hang, and the editor was dumped
and nearly ended instead.

#### docs(improve): v2.0.30 - quote the last path templates, exit 1 wording

`6c4b0981`

The last convergence round found two text defects, fixed without a
third round as the skill's stop rule allows for a doc LOW: the TEMP
and commit -F templates in the dispatch rules now quote their paths
(Git Bash strips the backslashes of an unquoted Windows path), and
integrate_branch's exit 1 wording says a merge already in progress is
refused and left as it was. Also: no trailing backslash in the watch
folder example, and the run folder is passed to tools as an absolute
path.

#### docs(improve): v2.0.30 - merge, cleanup and watch text match the tools

`56e9970f`

The run protocol's cleanup deleted the improve/* branches from the
main checkout, whose unpulled trunk does not contain them, so git
refused each one and suggested the forced form, a hook ask. The
branches are now deleted from the integration worktree before it is
removed, and the integration branch, which tracks the pushed trunk,
after it.

The merge steps now match the tools: the dry run applies the union
rule, exit 1 means no merge started and exit 3 that one may be in
progress, a hand resolution is checked with git diff --cached --check
before its commit, and the lint step runs the worktree's own copy and
reads its ai_dashes count, since --fail-on-drift ignores dashes. The
watch text says it skips workflows folders.

The dispatch rules name branch --delete --force as a hook ask and no
longer claim a bash -c body is asked on; the rule forbids such text
anywhere in a command. A needs_mike line starting ADVERSARIAL
ESCALATION now tells the orchestrator to run the adversarial reviewer
before the branch counts as reviewed. Every path in the command
templates is quoted, since Git Bash drops an unquoted path's
backslashes.

#### docs(improve): v2.0.30 - name integrate_branch's exit 3 in the merge steps

`014e2302`

integrate_branch.py exits 3 on an unexpected error, possibly with a
merge already in progress; the merge checklist now says to report it
and read git status first.

#### docs(improve): v2.0.30 - close the 2026-09-23 run's PROGRESS

`4f1a7ed9`

The Opus review sprint's PROGRESS.md still showed seven RUNNING rows, a
TODO wrap-up and a Next pointing at phase W1, so a resume would have
landed in a run that ended days ago.

Each RUNNING row was a launch row whose result sits in another row; it
now reads SUPERSEDED and names that row (R4, which never got one, names
MERGE ROUND 1, where all four of its branches merged). A banner and a
closing row say the run is closed, and Next lists what its wrap-up still
owes: the merged improve/012 branch to delete, the Codex lessons
consolidation and REVIEW-LOG numbers (not recorded either way), and the
open FOR-MIKE items.

#### docs(improve): v2.0.30 - skill text matches the fixed run tools

`dc260635`

The /improve skill and its references now describe the run tools as
they behave after this round's fixes, and close the gaps the text
review found.

Merging: integrate_branch.py merges and stages but never commits. It
prints the commit command, and the orchestrator runs it through Bash so
every commit gate judges the message. The checklist gives its exit
codes, its diff3 union rule, the public-text check on the message file,
lint with --dash-base at the integration tip (HEAD sees nothing after a
commit), and a cleanup order that removes worktrees before git branch
-d, which refuses a branch checked out in one.

Liveness: watch flags STALLED-ASK when an agent's last hook "ask" has no
later tool result (direct spawns included) and STALE past 30 minutes,
and must be pointed at the running workflow's folder, since a killed
workflow's agents read as stale forever.

Workflow args: every documented improve_ctl.py args call passes
--scratch and --tmp (it exits 2 without them), and the run's worktree
root goes in the PROGRESS.md header so a resumed run has it. Each
script's header comment names its args; top-level fields ride in an
items object for plans.js and a fanout checker. Review items keep the
lenses they list, so lens 2 for changed text about engine behaviour is
added there. Blast radius runs the worktree's own graphify copy.

Other fixes: the four-agent pool is per workflow; a residual HIGH gets
one more round or a recorded deferral as deep-review requires, never
only a FOR-MIKE line; Codex output goes under the run's scratch folder,
because removing a worktree deletes its gitignored docs/reviews/raw/;
the plans commit names each path it wrote; the hook suite runs in the
background everywhere it is prescribed; hard rule 1 allows the CI
replay's extra flags.

#### docs(improve): v2.0.30 - dispatch rules: every ask form, retries

`37ddf739`

The standing rules every /improve agent receives now match what the
repository's hooks do.

HOOK-ASK lists every git form the confirm hooks answer "ask" on, each
checked against block-dangerous-git.sh and block-broad-git-add.sh: git
restore (all but --staged alone), checkout with --, -f, --force or a .
path, reset --hard, clean -f or --force, stash drop or clear, branch
-D, and add -A, --all, -u, --update or ., commit -a or --all. The old
list left out restore, checkout ., branch -D and the long flag
spellings, so an agent could stall a whole unattended run on one.

TIMEOUT gives the hook suite's full command: CLAUDE_PROJECT_DIR set to
the agent's worktree, run in the background because the suite takes
about 12 minutes and a foreground Bash call is capped at 10, then the
Summary line read from the log.

Shell hygiene says why python3 is banned: here it resolves to the
Microsoft Store alias under WindowsApps, which hangs from Git Bash.
Naming WindowsApps also clears the hook suite's section 1 check, which
failed on the unexplained python3 mention (937 passed, 1 failed).

Editing roles gain one line: a retried or resumed agent restarts in the
worktree its earlier attempt changed, so HEAD past its start commit or
a dirty tree means reading git log and status and continuing, never
resetting.

#### docs(improve): v2.0.30 - keep the playbook section names others cite

`84c606fa`

The audit playbook rewrite had renamed three sections ("Tech Debt &
Architecture", "Dependencies & Migrations", "DX & Tooling") while
removing its dashes. docs/reference/rule-provenance.md points readers
at "audit-playbook § Tech Debt & Architecture" for the deepening
deletion test, so the original names are back and that pointer holds.

#### docs(claude): v2.0.30 - /improve index row names the run variants

`989c8749`

The skills index in CLAUDE.md is what survives /compact, so its
/improve row now matches the rebuilt skill: besides audits, "what
next" and handoff plans, it covers executing, reviewing, merging and
resuming a plans backlog, and its gate column says that merge, push,
issues and paid calls happen only on the maintainer's word. CLAUDE.md
with its imports stays under the entry-doc budget (23,795 of 24,576
bytes by lint_docs.py).

#### docs(reviews): v2.0.30 - drop lesson counts, retire sprint scripts

`43db0b47`

The lessons index no longer prints a lesson count per category or a
total. Nothing computed those numbers, so they drifted, and every
merge that touched a lessons file had to recount them by hand or by a
script copied per worktree. The index keeps its category links and
names the one command that counts: grep -c '^### ' over the files. No
tool or test read the counts (checked across tools/, .claude/ and
.github/), so nothing else changes.

The 2026-09-23 sprint's orchestration scripts under
plans/_audit/2026-09-23-opus/machinery/ are removed; git history keeps
them as the run's record. The tracked copies were stale and unsafe to
run again: they check out with CRLF endings, which the Workflow tool
refuses, they lack the TIMEOUT and HOOK-ASK rules the later copies
carried, and they hard-code that run's dates, version, baseline and
paths. Their successors are the /improve workflows and the
improve_ctl.py, integrate_branch.py and check_public_text.py tools.
The run's PROGRESS.md log records the move.

#### docs(skills): v2.0.30 - non-deploying builds in /verify and Codex

`785b2703`

/verify and /review-codex built and tested without the flags that stop
a build copying into the game install, so running either one deployed,
and failed outright while Bannerlord was open. Both now use the
non-deploying forms from AGENTS.md: -p:DisableModuleCopy=true
-p:ModuleId= on build and test. /verify also drops --no-build from its
test step, because its build step compiles Main only and the test step
would otherwise run a stale test DLL.

The two fixed blocks every Codex prompt carries (the TAOM ID cheatsheet
and the prior review lessons) move verbatim from /review-codex Phase 2d
into .claude/skills/review-codex/references/prompt-fixed.md, so the
skill and the /improve Codex prompt builder read one copy.

/review-codex Phase 3h pointed at a "Lessons From Prior Reviews"
section of AGENTS.md that now lives in .ai/review-reference.md, with
the essays in docs/reviews/codex-track-record.md; it now names both
and says an /improve run consolidates them at wrap-up. Its note that
AGENTS.md overflows Codex's 32 KB read limit was also stale: the file
is held under 8 KB, and the check for a complete read now looks for a
rule that is in it.

#### docs(improve): v2.0.30 - rebuild the skill around the run pipeline

`1d1abd69`

/improve now describes the whole run that the 2026-09-23 review sprint
had to invent outside the skill: preflight and baseline, audit lanes,
a verify wave and critic, plans written, cold-reviewed and revised,
execution on improve/NNN branches in worktrees, review with a bounded
convergence loop, maintainer decisions, issues, merge and wrap-up.

SKILL.md is about 10 KB, so the whole body survives /compact. It holds
the authority rules (what needs the maintainer's word each run), 13
hard rules with their sources, the run folder, the variants (new:
review, merge, resume) and a phase map naming the workflow script or
tool for each phase. After compaction, a usage limit or a restart the
orchestrator reads the run's PROGRESS.md first.

New references:
- dispatch-rules.md: the standing rules every dispatched agent gets at
  the top of its prompt (HOOK-ASK, TIMEOUT, disk, non-deploying build
  and test, TDD, protected files, commit form, evidence).
- run-protocol.md: run folder shapes, resume, the liveness watch for an
  agent stalled on a hook ask, the live probe form, status updates,
  issue filing, the merge checklist and the wrap-up checklist.
- execute-and-review.md: replaces closing-the-loop.md with the
  preconditions, worktrees, the executor, the spec check, the review
  stop rule (at most two convergence rounds, then "ship and track"
  offered first), stopped plans, decisions and reconcile.

audit-playbook.md and plan-template.md are updated: every build and
test command carries -p:DisableModuleCopy=true -p:ModuleId= (the old
forms deployed into the game install), the tools that write on a
plain run are named, and the plan template gains a baseline line, the
graphify blast radius, a Step 0 for protected files, the versioned
commit subject and no CHANGELOG step. Their prose has no em or en
dashes.

The skill names the workflows under .claude/skills/improve/workflows/
and the tools improve_ctl.py, integrate_branch.py and
check_public_text.py, which land in their own changes.

Not-tested: the workflows and tools the skill names are built on
parallel branches; their command lines follow the pinned contract.

#### docs(plans): v2.0.30 - plan 027 merged, issues and a harness fact

`6fe4c8fe`

Plan 027 is merged into bannerlord-1.5.x (35bdf96d, with the fixes
dad9b169 and d2fa6d08). The plans index and the sprint progress
record say so, and name the issues: #682 closed, #680 (validate-push
timing on commands of 250 KB and more) and #681 (a Git Bash
here-string hang that predates the plan) open, shipped and tracked
by the maintainer's choice.

harness-facts.md gains one row: a PreToolUse ask prompts the user
even in bypass mode, and the tool call waits for an answer, so an
unattended agent keeps dangerous git text out of its shell commands.
Seen on 2026-09-26 from a plan 027 review agent; a headless claude -p
run did not wait.

#### docs(plans): v2.0.30 - plan 027 and its two cold reviews

`05dbc0d4`

The plan behind this branch (decision 61) and both cold reviews. Each
review found a blocking fail-open in the planned validate-push code
(an unescaped brace in a bash expansion; comment stripping and
PowerShell forms the new reader dropped) and a reviser fixed it with
proof on scratch copies of the hooks before execution. Plans only.

#### docs(hooks): v2.0.30 - catalogue the PowerShell gate coverage

`2d7770b6`

Record what plan 027 changed where the next hook author will look:
- hooks-catalog.md: the recount (27 files on disk, 26 registrations
  in settings.json, 31 in all, now that the PowerShell group is folded
  into one Bash|PowerShell group), a paragraph on how every git gate
  reads both shells through _shellwords.py and what it does not read,
  the eight gate rows marked PreToolUse (Bash, PowerShell), and the
  validate-push and mark-verification-run rows brought up to date
  (glob and heads/ refspecs, option values, # comments, the raw
  command judged beside the reader's text, is_interrupt).
- hook-authoring.md: a new git gate registers in the Bash|PowerShell
  group and reads taom_hook_command posix, with a table of the
  PowerShell forms the reader resolves.
- harness-facts.md: the PowerShell tool's payload shape, and the
  PostToolUseFailure input with is_interrupt read from the 2.1.241
  binary; whether a timeout sets it stays UNVERIFIED.
- mcp-servers.md: the git safety hooks match Bash|PowerShell.

lint_docs reports hook-authoring.md and harness-facts.md over the
path-scoped size cap (report-only; hook-authoring.md was already over
it before this change). Part of plan 027.

#### docs(plans): v2.0.30 - fix a mangled path in PROGRESS

`1b6b3b53`

A backslash-escaped Windows path in the round 2 PROGRESS row was
written with control characters in place of \r, \t and \b. Plans
only.

#### docs(plans): v2.0.30 - round 2 recorded, plan 027 started

`dd628416`

PROGRESS records round 2: 021, 016, 011, 026, 017 and 020 merged
into bannerlord-1.5.x in that order, trunk 96afb6fb, hosted CI green.
The index gains plan 027 (decision 61, PowerShell coverage for the
Bash-only gates), now being planned.

The dispatch scripts follow plan 020: executors and follow-ups no
longer hand-write CHANGELOG entries (the commit body is the entry);
x-plans.js can write plans into a worktree, and x-review.js passes a
binding orchestrator note to the review lead. Plans only.

#### docs(plans): v2.0.30 - plan 020 merged, the sprint's last branch

`96afb6fb`

Plan 020 is merged, so every sprint branch is on bannerlord-1.5.x.
From here CHANGELOG.md is generated at /release; the commit body is
the changelog entry. Plans only.

#### docs(plans): v2.0.30 - plan 017 merged

`ffa80908`

Plan 017 is merged. A TAOM.dll built from the merge commit reads
build...+6be50e8f70a2... with no .dirty suffix; the packager tests,
the full suite (10,767 passed, 0 failed) and a local replay of the
hosted CI (unit 8,572 passed, binding gate 344 of 344) are green. The
packager's HEAD-bound tests fail mid-merge by design, because HEAD
then predates the stamp target. Plans only.

#### docs(release): v2.0.30 - compare the prune case-insensitively

`9ca99437`

The final convergence pass on plan 017 found the release prune rule
case-sensitive as written: the tag spells GUI/PreFabs/ and the install
GUI/Prefabs/, so a literal comparison against git ls-tree deletes all
49 live TAOM prefabs before packaging, and nothing after it rebuilds
them. The walk that proved 'removes these 12' had lowercased both
sides without saying so. Both copies of the rule (/release Phase 8
and release-process.md step 8) now say to compare case-insensitively,
and why; the report's walk and the RCA row say how they compared.

Also corrects the mirror sentence: the build mirrors
Win64_Shipping_Client into _Server for both modules and into _wEditor
for TAOM only. Docs only; test_package_release.py 57 OK, lint_docs
--fail-on-drift exits 0.

#### docs(plans): v2.0.30 - plan 026 merged

`8c60d134`

Plan 026 is merged: the decision logic of seven protected-virtual
seams now lives in their services with 78 new unit tests (merged suite
10,763 passed, 0 failed). Its index row now links the plan file, which
the merge brought onto trunk. Plans only.

#### docs(reviews): v2.0.30 - plan 026 Codex prompt and RED-step note

`5685676e`

Commits the adversarial review prompt and records, in the review
report, the executor's deviation behind the review's one P3: the RED
steps required CS0122 as well as CS0115, and the compiler printed only
CS0115, a subset of the allowed codes. Docs only.

#### docs(plans): v2.0.30 - plan 011 merged

`79792aed`

Plan 011 is merged. Its two live harness checks passed in headless
sessions in the worktree before the merge: a Stop reminder reached the
session once and was answered, and a PowerShell force push to
bannerlord-1.5.x was refused by validate-push.sh with git never run.
The parsing gaps the last review found outside 011's range go to plan
027. Plans only.

#### docs(plans): v2.0.30 - plan 016 merged, decisions 57 to 61

`4d912ee5`

Records the plan 016 merge and five maintainer decisions of
2026-09-25: 57, plan 026 takes three more seams; 58, Serena's editing
tools are denied except its memory tools; 59, Mike ports to
bannerlord-1.4.5 himself; 60, the trunk guard stays force-push only;
61, a new plan 027 covers the Bash-only gates for PowerShell. Plans
only.

#### docs(plans): v2.0.30 - plan 026 and its two cold reviews

`af6329ea`

The plan behind a3957541 (decisions 56 and 57): written against
31cc629f, cold-reviewed, extended from four seams to seven, and
cold-reviewed again. After the second review the orchestrator made the
fortification site seams yield lazily instead of building a list of
every settlement per keep-out check, because those checks run as
game-menu option conditions. Plans only.

#### docs(plans): v2.0.30 - plan 021 merged, decisions 51 to 56

`e452b0c7`

Records the maintainer's decisions of 2026-09-25 and the plan 021
merge. Decision 51: plan 016 pins Serena to the newest main commit
7a296833 instead of the v1.7.0 release. Decisions 52 to 54: plan 005's
vendored credential stays where it is, BUTR is not told, and the issue
is filed trimmed (#668). Decisions 55 and 56 settle two plan 021 review
items: a private helper only seams call is part of the seam body, and
the four seams that carry decision logic get refactored in new plan
026. The index marks 021 merged (b177ecbc) and adds the 026 row, which
ADR-007 now cites. Plans only.

#### docs(reviews): v2.0.30 - close the plan 021 review records

`ec5a1927`

The final convergence pass on 16e45bf8..6a2ce8cb found the ADR, lens
and ADR-008 edits clean and three LOW defects in the review records:
the commit table credited 9dcf1a4d with wording b22edd47 wrote, the
REVIEW-LOG update left the GitHub issue unsettled, and an open item
(codex-verify still says v1.5.2) was dropped. All three fixed; the RCA
gains a row per convergence round.

It also noted that ADR-007 presented the four seams plan 026 refactors
as a complete list, while three more hold similar logic. ADR-007 now
says the plan 021 review found four and that any seam breaking
condition 2 is a finding (protected file, edited under the bypass Mike
granted for plan 021). Whether plan 026 takes the other three is
Mike's call. No further reviewer pass: these are records, checked
directly against git show.

#### docs(adr): v2.0.30 - seam helpers, seam debt, convergence fixes

`6a2ce8cb`

Maintainer decisions 55 and 56 on the plan 021 review, and the three
LOW defects the convergence pass on the orchestrator commits found.

Decision 55 (review item O1): a private helper that only seams call,
such as an id lookup, is part of the seam body; condition 1 applies to
the calling seam's signature. One sentence in ADR-007 after condition
4, mirrored in the Standards lens check 1.
Decision 56 (O2): the four seams that still carry decision logic are
refactored by new plan 026. ADR-007's rationale names them and the
plan, and says test subclasses, since RefugeServiceTests has two.

Convergence fixes: ADR-008's recommended CI step grepped for
CampaignTime.Now in services with no seam exemption, contradicting the
edited checklist; it is removed, because a grep cannot tell a seam from
a violation. The CHANGELOG no longer lists csharp-architecture.md among
the hook-interface files (it changed only interface and seam rows).
The review report and REVIEW-LOG entry now record what was applied and
decided.

ADR edits made by the orchestrator under the protected-file bypass
Mike granted for plan 021. Verified: lint_docs --fail-on-drift exits
0, reviewctl and ai_documentation tests OK, test_hooks.sh 392 passed,
0 failed; no C# changed.

#### docs(reviews): v2.0.30 - Codex prompt for plan 021

`16e45bf8`

The adversarial review prompt the orchestrator ran against
bec0389d..9c29d732, kept with the review record. Its output stays in
the gitignored docs/reviews/raw/.

#### docs(changelog): v2.0.30 - rewrap the plan 021 entry, name b22edd47

`9dcf1a4d`

The previous commit's CHANGELOG edit joined two lines into one of 150
characters and referred to its own commit as 'a third'. Rewrapped at
100 and named b22edd47. Text only.

#### docs(adr): v2.0.30 - three ADR lines the plan 021 review found

`b22edd47`

The deep review of plan 021 listed three one-line ADR contradictions
for the orchestrator, because the ADRs are protected files (report
table O3, O5, O6). Applied under the bypass Mike granted for plan 021.

O3: ADR-007 listed CampaignTime among the sealed classes that need
adapters, but it is a struct, and the seam conditions allow it in seam
signatures. It leaves the Singletons line.
O5: ADR-008's review checklist and its Related line still forbade the
CampaignTime and other static calls that its Rule 1 exception now
allows inside a boundary seam. Both now name the seam.
O6: ADR-002's entry-point checklist asked only for mocked adapters,
narrower than guideline 7; it now accepts a test subclass that
overrides the service's boundary seams.

Not applied: O4 (tighten condition 1 from 'sealed TaleWorlds class' to
'any other TaleWorlds reference type') changes wording Mike chose in
decision 49, so it is his call, as are O1 and O2. The CHANGELOG entry
now says which ADR commit rests on which approval.

#### docs(adr): v2.0.30 - let seams call the engine, interface optional

`7d1b7a54`

Two wording fixes the adversarial review of plan 021 found (P2 2 and
P2 3), made by the orchestrator because the ADRs are protected files.

ADR-008's new exception allowed only a static read inside a boundary
seam, while ADR-007's amended conditions (decision 49, the looser
variant) allow the engine calls for one action. RefugeService's
ChargePlayer and RefundPlayer are such seams and mutate gold, so a
reviewer following ADR-008 would reject what ADR-007 permits. It now
says a static method call or property access.

ADR-002's migration step 4 still said to create a service interface
every time, contradicting the amended Service Design Guideline 2. It
now creates one only when a test fakes the service or a second
implementation exists.

#### docs(workflow): v2.0.30 - the commit body is the changelog entry

`30b54df7`

Plan 020, decision 18. AGENTS.md "Documentation duty" drops
"CHANGELOG.md is updated every session" and says instead: the commit
body is the changelog entry, written for a reader of the release note;
/release generates CHANGELOG.md from commit subjects and bodies, and
nobody edits that file by hand.

/release Phase 4 now runs tools/changelog_from_commits.py --write to
generate the section, and Phase 5 writes the release note from that
section; Phase 6 names what the release commit stages (the version
files, CHANGELOG.md and the release note). release-process.md step 5
gives the same command.

/verify Step 5 and the deep-review completeness lens used to demand a
CHANGELOG edit; they now flag one, since a hand edit outside /release
is the defect. Every other instruction that told an agent to update
CHANGELOG.md now points at the commit body: CLAUDE.md, README.md, the
builder role, the taom-build Codex skill, the Codex guide, the Serena
checklist, completion-workflow.md, external-repo-adoption.md, the lord
skills and new culture guides, five rules (external-skill-ports,
troops, gui-ui, simplicity-criterion, working-discipline) and the ship,
deep-review, finish-branch, author-armor, verify-bindings,
lint-cleanup-loop, scope-check, new-adr, improve and armory-audit
skills. finish-branch loses its CHANGELOG step and hook note and is
renumbered.

Left alone on purpose: lines that name CHANGELOG as a kind of prose or
record history (hook-authoring.md:59 among them), every code comment
that cites a past CHANGELOG date (those entries now live in the
archive), docs/ai-includes/git-and-commits.md (its shared-file rules
still protect other files) and new-creature-mount/SKILL.md (another
session's file; its line 92 is a follow-up).

#### docs(rules): v2.0.30 - align three architecture rules with the code

`9c29d732`

Sprint decision 19 amends three written rules that disagreed with the
code and with each other. The ADR text (ADR-002, ADR-007, ADR-008) is
the previous commit on this branch; this one brings the rule files,
the review reference and the docs/ai-includes guides in line with it.

A, seams: the review reference, csharp-architecture.md and the
/deep-review Standards lens accept a protected-virtual boundary seam
that meets the four ADR-007 "Exceptions" conditions.

B, interfaces: a service gets an interface only when a test fakes it
or a second class implements it; every adapter keeps one. The
Standards lens, think-before-coding.md, the feature-builder agent and
/new-feature now say so.

C, hooks: the hook interface between a patch and its service is
optional (a narrow seam or a test fake) in AGENTS.md, the review
reference, csharp-patterns.md, harmony-patches.md and three
docs/ai-includes guides.

No code changed and no interface or hook was deleted; those go when
their files are next touched. Plan 021.

Not-tested: docs and rules only; no code changed

#### docs(plans): v2.0.30 - merge round 1 recorded, decisions 49 and 50

`31cc629f`

The sprint record after merging 19 reviewed branches into
bannerlord-1.5.x: README statuses, PROGRESS entries for the merge round
and the protected-plan round, decisions 49 (looser seam wording in
ADR-007) and 50 (the merge), and a status banner in FOR-MIKE.md.
Plans only.

#### docs(readme): v2.0.30 - drop stale counts, fix version and test command

`8a638831`

README: Bannerlord v1.5.3 in all three places (was v1.4.8 twice and
v1.5.2), development branch bannerlord-1.5.x, the setup script's
real job, and the non-deploying dotnet test command. Hand-kept
engineering counts removed rather than refreshed; nothing computes
them and several were wrong (50 careers vs 67).

agent-operating-manual.md: build and test rows now pass
-p:DisableModuleCopy=true -p:ModuleId=, as the binding-gate row
already did. Plan 016, commit 2 of 2.

#### docs(adr): v2.0.30 - record boundary seams, conditional interfaces

`1cdf8eb0`

Plan 021 Step 0, made by the orchestrator on Mike's approval (bypass for
plan 021) with the looser seam wording Mike chose (decision 49):
ADR-007 gains the protected-virtual boundary seam exception and its
checklist lines; ADR-002 makes a service interface conditional on a
fake or a second implementation; ADR-008 allows a static read inside a
qualifying seam. Plan 021's executor does the rule and doc edits.

#### docs(release): v2.0.30 - build and package at the release tag

`ff84e1b8`

Plan 017 documentation. The /release skill gains Phase 8: the Phase 2
build predates the release commit, so rebuild at the tag (from a clean
worktree of the tag when another session is editing), gate the DLLs
with package_release.py --require-build vX.Y.Z --dry-run, then package.
Its version-vs-stamp gotcha now explains the +<sha> and .dirty/nogit
suffixes.

release-process.md step 1 now agrees with the skill's Phase 1: a clean
tree, never a release built beside another session's edits, because
build.ps1 compiles and deploys every file in the tree. It gains step 8
(the gate) and a triage paragraph on reading the Build: and TAOM build:
lines of a crash bundle. crash-report.md's Identity row names the new
field, and the CHANGELOG entry covers the whole plan.

#### docs(reviews): v2.0.30 - recount lessons after the sprint merges

`b2363ed0`

Each merged branch appended lessons; the category counts and the
total are recomputed with grep -c '^### ' per lessons file (921
lessons), the rule the index states.

#### docs(patchshield): v2.0.30 - shim comments after merging 006 and 007

`88408869`

With plans 006 and 007 both merged, the PatchShield exclusion comment
and dr3-maintenance.md no longer said what the code does: the
Native2Managed bridge wraps the 16 allowlisted shims, not all 247,
and its fallback hand-backs now keep the throw site (plan 006's
HandBack). The 231 shims off the allowlist carry no TAOM finalizer.
Comment and doc only; the review of 007 asked for this at merge.

#### docs(reviews): v2.0.30 - Codex prompt for plan 022

`0192a858`

The adversarial review prompt its review used, kept beside the
deep-review record like the other branches' prompts.

#### docs(changelog): v2.0.30 - cite #664 for plan 003

`aee17290`

The plan 003 issue was filed after the branch's entry was written.

#### docs(changelog): v2.0.30 - cite #663 for plan 002

`82ae6cf1`

The plan 002 issue was filed after the branch's entry was written.

#### docs(changelog): v2.0.30 - cite #661 for plan 013

`2feb70ad`

The plan 013 issue was filed after the branch's entry was written.

#### docs(reviews): v2.0.30 - Codex prompt for plan 005

`337372d8`

The adversarial review prompt its review used, kept beside the
deep-review record like the other branches' prompts.

#### docs(reviews): v2.0.30 - Codex prompt for plan 010

`2bc6695e`

The adversarial review prompt its review used, kept beside the
deep-review record like the other branches' prompts.

#### docs(reviews): v2.0.30 - Codex prompt for plan 009

`4da415e2`

The adversarial review prompt its review used, kept beside the
deep-review record like the other branches' prompts.

#### docs(reviews): v2.0.30 - Codex prompt for plan 003

`cf6e06aa`

The adversarial review prompt its review used, kept beside the
deep-review record like the other branches' prompts.

#### docs(tactics): v2.0.30 - document OOB Auto-Assign

`66e3fd59`

Plan 022 documentation for the Assign Heroes button wired in the
previous commit.

companion-tactics.md gains an Auto-Assign subsection (general only,
open slots, candidates, the global greedy and its tie-breaks, the
vanilla accept-captain path, messages, visibility, no co-op gate, no
save data, the DeploymentFormationClass numbering), the new boundary
class in the solution tree, a known limitation, and corrected test
counts: 97 tests across 9 files, with TroopStanceManagerTests at the 9
tests it really has.

feature-map.md gains the missing CompanionTactics row, and CHANGELOG.md
gains the player-facing entry.

#### docs(plans): v2.0.30 - plans 020-025, night progress, FOR-MIKE

`1091f3b6`

Overnight record of the 2026-09-23 Opus sprint (Mike: "continue
throughout the night"):

- Plans 020 (CHANGELOG generated at /release, decision 18), 021 (three
  architecture rule amendments, decision 19), 022 (wire the Order of
  Battle Auto-Assign button, decision 21) and 025 (delete the
  unreachable scaffolds, decision 21), each written, cold-reviewed and
  revised; their reviews and briefs are in the run folder. 020 and
  021 each need one protected edit from Mike first (Step 0).
- FOR-MIKE.md: what waits on Mike, including the protected edits the
  auto-mode classifier would not let the orchestrator make.
- README status cells and PROGRESS entries for the night's branches.
- Plan 015 amendment 2 (decision 32) and its issue number (#659).
- machinery: the plan writer (x-plans.js) and issue drafter
  (x-draft-issues.js), a binding note field and an updated test
  baseline in the executor, a disk rule in the reviewer and plan
  prompts, a file-name tag for second reviews, and a tolerant title
  lookup in scope.py.

Plans only; nothing under Main/, TAOM.Tests/ or the harness changes.

#### docs(changelog): v2.0.30 - cite #662 for plan 018

`71fd2169`

The plan 018 issue was filed after the branch's entry was written.

#### docs(rules): v2.0.30 - new patches need no ResetForUnload

`636b50ab`

Mike's decision 22 (sprint finding COMP-05). Nothing reloads TAOM in
one process: the engine's only caller of OnSubModuleUnloaded is
Module.FinalizeModule at shutdown (v1.5.3 Module.cs:242-248 and
1296-1314), and a rebuild means stopping the game and pressing Play
again. The harmony-patches rule now says a new patch's static service
cache needs no ResetForUnload(); the existing ones stay until touched,
and ResetForUnloadSweepTests still requires each existing one to be
wired into OnSubModuleUnloaded.

#### docs(plans): v2.0.30 - sprint decisions, branch status, machinery

`a39a9c86`

The 2026-09-23 Opus sprint record, after the overnight execution and
Mike's decision rounds of 2026-09-24:

- DECISIONS.md: decisions 1 to 48, asked one at a time, each with its
  follow-through.
- NEXT-STEPS.md: the start-now summary.
- PROGRESS.md: runs, branch tips and verification per step.
- README.md: status cells for plans 006 to 019 with branch tips and
  issue numbers.
- Plan amendments made during execution: 010 (System.Numerics.Vectors
  from netstandard2.0; copy it into the RefAsm test output), 015 (`is
  null` on NativeObject), 018 (re-cut against 4c728dac, its review, the
  COMP-05 answer and the campaign-start fail-closed precondition).
- machinery/: the orchestration scripts the runs used (executor,
  review and follow-up workflows, Codex prompt builder, scope and
  status helpers).

Plans only; nothing under Main/, TAOM.Tests/ or the harness changes.

#### docs(reviews): v2.0.30 - Codex prompt and re-cut plan 018

`667002d8`

The adversarial review prompt the plan 018 review used, and the plan
as re-cut against 4c728dac, which the executor and reviewers followed.
The plan text matches the copy in the bannerlord-1.5.x plans record.

#### docs(reviews): v2.0.30 - Codex prompt for plan 013

`5dcef67a`

The adversarial review prompt the plan 013 review used, kept beside
the deep-review record like the other branches' prompts.

#### docs(nullable): v2.0.30 - per-folder nullable ratchet procedure

`19ca72d3`

docs/ai-includes/code-quality.md gains "How nullable is enforced": the
root .editorconfig sets the seven nullable ids to none for Main and
Dependencies, a null-clean folder raises them to error in its own
.editorconfig, and the ids must never return to a csproj NoWarn.
Main/Features/Siege is the first graduated folder.

docs/features/siege.md describes the prefix's new no-settlement branch
and its tests, and CHANGELOG.md records the change. Plan 019, step 8.

#### docs(changelog): v2.0.30 - warg tick-cost entry for plan 015

`66a85b08`

Records plan 015's four code commits under 2026-09-24: services
resolved once per warg tree node, reused scan buffers, (x, y) grid
keys, and the bone check's range gate ahead of the target skeleton
fetch. It notes the orchestrator's amendment (the `is null` test on a
NativeObject value), the full-suite totals from this worktree and the
owed review and in-game check.

#### docs(warg): v2.0.30 - record the warg tick-cost changes

`2420761b`

warg-combat.md's Performance bullets described the per-query list,
the per-tick bone list and the per-frame IoC lookups as open work;
plan 015 fixed all three, so they now say what the code does: (x, y)
grid keys and 49 cells for a 60 m scan, reused scan buffers, one
attacker skeleton per tick, a target skeleton only inside the range
gate, and services resolved once per node.

The test notes in both docs name the three new test classes in place
of the "SpatialGrid and BoneCheck are untestable" lines.

#### docs(crash-report): v2.0.30 - correct capture cost and coverage

`6fe83bca`

Plan 006, docs. The crash-report doc, the MCM doc, the patch registry
and the API snapshot described ten finalizers, a 247-method sweep that
costs zero, and CrashReport toggles that need a restart. They now
describe five finalizers, the six-shim allowlist in
Native2ManagedTargets, the per-attach boot cost and the small non-zero
steady-state cost, toggles that apply live, and the 1, 2, 10, 100
suppression-log cadence.

hero-race.md and gauntletui-viewmodel-screen.md credited the
Native2Managed sweep with catching a MapConversationTableau.OnTick
throw; the verified v1.5.3 call chain runs inside ScreenManager.Tick,
so the Patch37 finalizer there is what catches it, before and after
this plan. patch-targets.md drops the four deleted finalizer rows by
hand (the snapshot already drifted at the base, so it was not
regenerated). CHANGELOG entry added.

#### docs(reviews): v2.0.30 - drop the scream clip's source site name

`473e4ccc`

Mike asked for the website named in the clip's metadata tags to be
removed from the scream swap's RCA row W1 and its REVIEW-LOG
paragraph. Both now say the tags named a third-party website.

#### docs(changelog): v2.0.30 - enlistment session reset (plan 014)

`d1221b7f`

Records plan 014: Enlistment's per-session clocks and caches are now
cleared on a game load and on a new campaign, so an earlier save or a
second campaign in one process no longer inherits the dwell anchor,
the shore-leave offer latch, the rhythm snapshot or the cached
commander and army handles.

Not-tested: load-earlier-save and second-campaign runs (need the game)

#### docs(changelog): v2.0.30 - PatchShield skips the callback shims

`0ad253d5`

Records plan 007's two commits in the 2026-09-24 section: PatchShield
no longer re-shields the 247 ManagedCallbacks shims that TAOM's crash
capture already wraps, the shield pass line in diag.log reports its
timing, and the docs now say pass 2 runs at game start, not the menu.
Includes the full-suite result and the in-game check still owed.

Plan 007 (plans/007-patchshield-skip-callback-shims.md).

#### docs(enlistment): v2.0.30 - session reset now covers a new campaign

`74fb3d35`

The reconciler comments and the feature doc said a new campaign never
reaches ResetSessionCaches. It now does, and the doc lists the three
latches it clears. The reset's interface doc also loses an em dash.

#### docs(patchshield): v2.0.30 - pass 2 runs at game start, not the menu

`73792944`

TAOM.Dependencies' OnGameInitializationFinished is reached from
MBGameManager.OnGameInitializationFinished, which Campaign.OnInitialize
and CustomGame call at the end of a game's initialisation, inside its
loading screen. The doc comment, the diag.log label and two migration
docs said it fires when the main menu is reached, which hid where
PatchShield pass 2's cost lands.

The SubModule doc comment and entry label now say game start. The
crash-loop marker rows in dr3-maintenance.md now state that a session
which quits from the main menu without starting a game leaves the
marker behind (documented, behaviour unchanged). The same doc gains a
line on the new ManagedCallbacks exclusion and the verification sample
shows the new timing suffix on the shield pass line; the v1.5.2 impact
note places pass 2 inside the campaign's loading screen.

Plan 007 (plans/007-patchshield-skip-callback-shims.md).

#### docs(harmony): v2.0.30 - correct Patch37 coverage, name the guard

`9da9b5b9`

crash-report.md said Patch37 is registered first "to maximise coverage
of our own init". Its MBSubModuleBase.OnSubModuleLoad finalizer patches
the base method, and TAOM's override is already running when it
attaches, so it cannot catch a throw from that method. The entry now
says so and names TryPatchCategory as what keeps a drifted binding
from failing the load.

submodule-lifecycle-and-harmony.md now names TryPatchCategory and
PatchCategoryApplier as the apply path, and records that Harmony stops
a category at its first unresolvable target and throws.

Part of plan 009 (plans/009-guarded-patch-category-apply.md).

#### docs(changelog): v2.0.30 - plan 012 loading-window trace entry

`6f7ddd39`

Plan 012 CHANGELOG entry: the Patch89 Disable postfix now traces a
loading-window lower only when the window was actually up, instead
of once per rendered frame on the main menu, the party screen and
character creation. Records the full-suite result and the owed
in-game check.

#### docs(map-load): v2.0.30 - loading-window lowers trace on real drops

`bce3c27f`

Plan 012, step 3 of 3. The map-load diagnostics feature doc and the
Harmony patch registry described the lifecycle trace as logging
every raise and lower of the global loading window. They now say a
lower is traced only when it actually took the window down, why the
Disable patch needs a Prefix (the engine calls the lower on every
frame of several screens and clears the flag unconditionally), and
what the per-frame lowers cost in v2.0.29 and v2.0.30.

#### docs(plans): v2.0.30 - Opus review sprint report and plans 006-019

`7f02fc8d`

The /improve review sprint 2026-09-23-opus, run against b2e387db on
bannerlord-1.5.x: a measured baseline, a reconcile of June plans 001
to 005, a re-triage of the June harvest's 99 findings, six audit
lanes (42 findings), adversarial checking (35 confirmed, 1 refuted),
a completeness critic and two follow-up measurements.

Top findings: no C# is compiled by CI on any branch; the binding gate
can pass with 335 of 368 checks skipped; 64 unguarded PatchCategory
calls can fail the boot or loop a failed load; the four Stop reminders
never reach Claude; the loading-window trace logs a stack walk every
frame; TAOM patches the same 247 callback shims twice behind a dead
MCM toggle (about 1 to 3 s for players, about 100 s per launch on the
maintainer's slow desktop).

Adds plans 006 to 019 (each written, cold-reviewed and revised), the
rewritten index with the reconciled 001 to 005 rows, and the run record
under plans/_audit/2026-09-23-opus/ (REPORT.md first). No source file
changes. Not pushed.

#### docs(herorace): v2.0.30 - #635 CTD triage, voice-over doc corrected

`aed9dea8`

A player crashed silently after opening a voiced elf wanderer's
encyclopedia page during a menu-started map conversation (#635). No
code: the root cause needs a dump. This records what the triage read.

- Engine doc: the encyclopedia is a second layer on MapScreen, idles the
  map state, never finalizes the conversation tableau; the tableau ticks
  only while its widget renders; lip-sync path; latent OnTick NRE.
- kingdom-voices.md was wrong: an empty accent falls back to any
  gendered vanilla voice, so TAOM characters are lip-synced routinely.
- hero-race.md: MapConversationTableau is a fifth, unguarded render
  path; open #635 section.
- Two misc lessons; lesson counts re-derived (777 committed).

Research: DefaultVoiceOverModel.GetSoundPathForCharacter,
MapConversationTableau, GameStateManager.OnTick, TextureWidget.OnUpdate

#### docs(ranged): v2.0.30 - #617 second review records, RCA, lessons

`449d80a8`

The records half of b904e02a. RCA:
docs/reviews/rca-ranged-rebalance-second-review-2026-09-18.md
(17 findings of the second review, the audit of the first RCA, and
the review of the fixes, F1 to F8); REVIEW-LOG Reviews 119 and 120.

- The first RCA corrected in place: one fabricated "why missed", the
  1.4.8 template premise, an incomplete refutation, two miscounts.
- Feature doc: current cells and recipe, the per-spawn ItemModifier
  for bows and crossbows, TAOM's models in the damage chain (career
  passives reach troop hits), the arrow's missile_speed (neither tier
  nor price), the 12 cells above the hero ceiling, and a campaign
  battle for the crossbow accuracy rule (Custom Battle cannot show it).
- troop-skill-balance: the Iron Hills line left SKIP_TROOP_IDS; the
  template gates rest on the 1.4.8 loader (#626); the balance report
  references ladder cells. field-commission: open #625.
- The CLAUDE.md trap row back under 400 characters; the validation
  rule row and doc, troops rule, localization map, translator guide,
  balance levers, items chapter and feature map updated.
- Lessons: three new (testing-qa, adapters-taleworlds-api, misc),
  three extended (localization-ui, data-content-cultures,
  xslt-moduledata).

#### docs(elephant): v2.0.30 - War Sails lessons for howdah archers

`f59ea59b`

Four research passes over the War Sails decompile, the base engine
and the ship prefabs, read as a design reference only: TAOM will not
require the DLC. Ships keep crews on a navmesh island attached to the
moving entity through base MissionObject calls the siege towers also
use, hold fighting crew in a ship-local detachment, flag every moving
body moveable and release machine users from OnMissionEnded. TAOM's
howdah flags no body moveable, its walls are 1 m tall, and it
teleports seated archers every tick. Recommendation: four gated
steps, the seat model as fallback.

Research: MissionObject.AttachDynamicNavmeshToEntity, SiegeTower
Research: UsableMachine.OnMissionEnded, UsableMissionObject (v1.5.3)

### Chores

#### chore(animalia): v2.0.30 - remove the Custom Battle test riders

`d85fe6dc`

The two Animalia test riders, [Test] Animalia Elk Rider and [Test]
Animalia Moose Rider, no longer appear in the Custom Battle troop
picker. They were console test troops for the elk and moose (#646)
and were never meant to ship. Rochenlas still rides the elk, and
Thranduil and the Mirkwood lords the moose.

Removed with them: their troop file and its SubModule.xml node, their
name strings in all 12 languages, their armour and melee ladder
exemptions, the taom_test_ exemption in the recruitment reachability
test, and the test that pinned their mounts. The Animalia doc, the
quadruped workflow and the release skill say so.

Tests: full suite 10,978 passed, 2 skipped, 0 failed; ModuleData
validator 0 errors; the allowlist and ladder tool tests pass.

#### chore(mcp): v2.0.30 - newest Serena pin, deny its edit tools

`7d13adc3`

Two maintainer decisions on the plan 016 review, applied by the
orchestrator (the settings file under the protected-file bypass Mike
granted for plan 016).

Decision 51: .mcp.json pins Serena to its newest main commit
7a296833 (serena-agent 2.0.0.dev0), the code sessions already ran
unpinned, instead of the older v1.7.0 release. Checked first: the
pinned start-mcp-server --help exits 0 with --context and --project,
and ide-assistant still maps to claude-code.

Decision 58: Serena's editing tools went around every Edit, Write and
Bash hook, like the git and filesystem MCP write tools already denied.
The sixteen tools the pinned commit marks can-edit, other than its
four memory tools, join the deny list (25 entries now), including
execute_shell_command and jet_brains_inline_symbol, whose class name
lacks the Tool suffix. docs/reference/mcp-servers.md lists them, the
memory tools' real write scope, and the rule for re-deriving the list
from the class markers on a pin bump.

The final convergence pass found that last tool missing and four
record and wording defects; all are fixed here, including the freeze
skill's stale line that still called Serena's editors unblocked.

Verified: a marker-based re-derivation against the Serena source
prints missing [] and extra []; test_hooks.sh 392 passed, 0 failed;
audit_claude_config HIGH:1 INFO:7 (the known fixture); lint_docs
--fail-on-drift exits 0.

#### chore(changelog): v2.0.30 - archive the hand-written CHANGELOG

`ef5b7ff4`

Plan 020, decision 18. The hand-written CHANGELOG.md moves unchanged
(the same blob, BOM and old header included) to
docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md, next to the
2026-H1 archive. lint_docs.py already treats that directory as
verbatim history, so the move adds no lint findings.

The new CHANGELOG.md is only a header: it says the file is generated
at each release by tools/changelog_from_commits.py, that the commit
body is the changelog entry, and links both archives. It stays a
header until the next /release writes the first generated section.

The archive holds the trunk's file as of this branch's base. At merge
time the orchestrator re-cuts it from the trunk's committed CHANGELOG
(the merge recipe in plan 020), so entries other branches add before
this one lands are kept.

#### chore(hooks): v2.0.30 - retire the two CHANGELOG hooks and their rules

`c91bb95f`

Plan 020, decision 18: /release now generates CHANGELOG.md from commit
subjects and bodies, so the per-session duty to hand-edit the file is
gone, and the two hooks that enforced it go with it.

check-changelog-changed.sh (PreToolUse on Bash) denied a git commit
that staged anything under .claude/, CLAUDE.md or AGENTS.md without
CHANGELOG.md. check-changelog-updated.sh (Stop) printed a reminder
when a .cs, .xml, .xslt or .json file was dirty and CHANGELOG.md was
not. Both scripts are deleted; Mike removed their two registrations
from .claude/settings.json by hand, since the config-protection hook
keeps agents out of that file.

session-start.sh no longer prints the newest dated CHANGELOG section
(generated sections start "## v", so it would print nothing; the
recent-commits list above it stays), and its python-degraded warning
now names four gates. Comments naming a retired hook in three other
hooks are trimmed. hooks-catalog.md drops both rows and is recounted:
27 scripts, 26 registrations across 9 events, 31 total with the five
skill-frontmatter registrations. harness-facts.md and hook-authoring.md
stop citing the retired gate.

The plan 013 prefilter work landed on the trunk after this plan was
written, so this commit also drops check-changelog-changed.sh from
PF_NARROWED_LIST in tools/test_hooks.sh (the plan's own merge recipe
for 013), and moves the prefilter counts in test_hooks.sh comments and
hooks-catalog.md to five commit gates, eleven prefiltered hooks and
nine blocking gates. bash tools/test_hooks.sh: 368 passed, 0 failed
(382 before; the drop is the retired scripts' rows).

Not-tested: a live session reloading settings.json without the two
  hooks; running sessions keep calling the missing scripts until
  they restart.
Rejected: repurposing check-changelog-changed.sh as a hand-edit gate
  (the generator refuses a hand-written section; a commit gate would
  fail in-flight sessions at the cut-over)

#### chore(security): v2.0.30 - untrack local files and pin MCP servers

`470ee64e`

Untrack .claude/settings.local.json, _taom_loc.pkl and crashz/ and
ignore them. Move the nine MCP write-tool denies into the tracked
.claude/settings.json first so every clone keeps them; the MCP trust
list stays per-user. Repoint the three crashz/report.json citations
to git show b2e387db:crashz/report.json, and unlink the one Markdown
link to settings.local.json (moduledata-validation.md).

Pin serena (v1.7.0, 949a27ef), server-filesystem 2026.8.31,
mcp-server-git 2026.8.18 and elevenlabs-mcp 0.12.2 in .mcp.json
and .codex/config.toml. audit_claude_config.py: MED 1 -> 0.

Merging this deletes settings.local.json from each checkout's disk;
restore it from b2e387db (see CHANGELOG). Plan 016, commit 1 of 2.

Not-tested: MCP startup inside Claude Code and Codex (needs a restart)

#### chore(debug): v2.0.30 - managed-only default launch profile

`37ff41e4`

Mike's decision 23 from the Opus review sprint. The Bannerlord launch
profile now attaches only the managed debugger; a new profile,
"Bannerlord (mixed native debugger)", keeps the managed plus native
attach for native crash work.

The sprint found this desktop's Harmony patching 20 to 40 times slower
than players' (PatchShield pass 2 about 69 s on the first game start,
0 to 1 s in player logs) and named the mixed debugger the likely cause
(UNVERIFIED). The profile name is unchanged, so the saved
ActiveDebugProfile keeps pointing at it. The file still parses, with
the four profiles in order.

Not-tested: the next Play is the test; the diag.log shield pass line
should fall from about 69 s to a few seconds.

#### chore(build): v2.0.30 - move nullable suppression into .editorconfig

`4d2a6c9a`

The seven main nullable ids (CS8600 to CS8604, CS8618, CS8625) leave
the NoWarn lists of Main/TAOM.csproj and TAOM.Dependencies.csproj.
The compiler's /nowarn beats any .editorconfig severity for the same
id, so while they sat in NoWarn no folder could opt back in.

The root .editorconfig now sets them to none for Main/ and
Dependencies/ only, and a nearer folder .editorconfig can raise them.
Build output is unchanged: 2 analyzer warnings, 0 CS86xx. TAOM.Tests
is not covered by the block, so its 2,256 nullable warnings stay
visible exactly as before.

Plan 019, step 1.

#### chore(armoury): v2.0.30 - audit XML references and clean up dead mesh entries

`d5986f87`

### ci

#### ci(tests): v2.0.30 - build and test C# on hosted Windows runners

`b8c00045`

Plan 010. No CI job compiled TAOM's C#: the only C# job needed a
self-hosted runner that was never registered. The new csharp.yml runs
on GitHub-hosted Windows for pushes and pull requests on
bannerlord-1.5.x and bannerlord-1.4.5, building against BUTR's
metadata-only reference assemblies for the pinned Steam build. The
self-hosted build job and the check-build-config warning are deleted.

GameReferences.targets now owns every game reference. The default,
TaomGameRefs=Install, evaluates to the same Reference items as before
(normalized -getItem diff IDENTICAL for all three projects);
-p:TaomGameRefs=RefAsm switches to the BUTR packages. Two new tests pin
the BUTR build to the pinned game version and forbid $(GameFolder) in
a project's Reference items. 142 TestCategory tags (RequiresGame 103,
RequiresGameIL 29, LiveInstall 10) let CI skip what needs the game.

Amendment 1: RefAsm uses the System.Numerics.Vectors lib\netstandard2.0
copy (the net46 one is a forwarding facade the SNV extern alias cannot
follow), and package paths go through EnsureTrailingSlash.
Amendment 2: in RefAsm mode only, that copy is placed in the test
output and in refasm-game\bin, and ConsoleCommandBindingTests,
LiveTableauRefTests and Patch86HideoutBossFightBindingTests are tagged
RequiresGame.

Evidence, local with no game in the environment: unit step 8,183
executed, 0 failed; binding gate on refasm-game 338 executed, 0
skipped, 0 failed; the workflow's build, unit and gate run blocks
replayed through pwsh all exit 0. Install mode: full suite 10,249
total with only the two known live-Armory failures, strict gate on the
real game 368 passed, 0 skipped.

Not-tested: the GitHub-hosted run itself (it needs a push; every step was replayed locally)

### Commits without the version label

#### Add unit tests for eye follow and skin restoration functionality

`0934f65d`

- Implement tests for eye movement analysis in `test_check_eye_follow.py`, ensuring correct behavior when eyes are still while sockets move, and vice versa.
- Create comprehensive tests in `test_eye_follow.py` to validate the eye following logic based on socket movements, including edge cases for scaling and translation.
- Introduce `test_restore_adult_woman_dwarf.py` to verify the restoration process of skin attributes in a synthetic XML file, covering various scenarios including dry runs, applying changes, and handling errors.

#### Add unit tests for animation clip generation and native decompilation

`71907407`

- Introduced `test_gen_troll_anim_clips.py` to validate the functionality of the animation clip generation script, ensuring proper handling of keyed and unkeyed templates, as well as verifying the integrity of generated clips.
- Added `test_native_decompile.py` to implement unit tests for the native decompilation process, including cache key generation, engine version reading, and reporting functionality, with a mock backend to simulate Ghidra's behavior.

#### Add unit tests for various tools and functionalities

`30abf550`

- Implement tests for adding hill troll hammer items, ensuring correct line terminators and handling of anchors.
- Create tests for checking race morph channels, validating mesh channel counts and handling missing or unreadable FBX files.
- Add tests for setting clip balance names, ensuring proper metadata handling and validation of clip packages.
- Introduce tests for transferring hand morphs, focusing on seam weights and mirror functionality.

#### feat: Add custom battle banner bearers model to restrict trolls from carrying banners

`25995dc9`

feat: Implement patch for troll formation spacing to adjust unit diameter based on troll width

feat: Introduce troll action tracing for crash forensics during melee interactions

feat: Create tracker for troll formation spacing to manage unit counts and spacing dynamically

test: Add binding tests for Patch92 to ensure proper method targeting in troll formation spacing

docs: Document shader pre-compilation feature park and findings from review

feat: Add face morph channels to hill troll head in Armory FBX for proper engine integration

#### Add unit tests for FBX LODs and LOD fill batch functionality

`e6312852`

- Created `test_audit_fbx_lods.py` to test the functionality of the FBX LOD reader, including mesh reading, triangle and vertex counts, morph channels, and error handling for ASCII FBX files.
- Implemented `test_lod_fill_batch.py` to validate the LOD filling logic, ensuring proper interpolation of gaps, handling of existing levels, and correct behavior for tiny meshes.

#### fix: update animation paths for hill troll and adjust script for human clip handling

`fef273bf`

#### Add script to configure hill troll race in LOTRLOME Armory

`9354b0b2`

This script, `wire_hill_troll_race.py`, automates the process of updating the hill troll race configuration in the LOTRLOME Armory. It modifies the skins, monsters, and action sets XML files to ensure that the hill troll race uses the correct skeleton and mesh attributes. The script supports a dry run mode to preview changes before applying them, and it creates backups of modified files. Additionally, it checks for consistency and correctness in the edited files to prevent errors during gameplay.

#### Add unit tests for Animalia armory writer functionality

`709649c3`

- Implement comprehensive tests for the apply_animalia_armory.py script.
- Create a synthetic game directory for isolated testing.
- Validate various scenarios including dry runs, missing clips, and backup functionality.
- Ensure tests cover edge cases and maintain file integrity across runs.

#### Add scripts for generating and validating animal animation clips

`c79a5852`

- Introduced `gen_animalia_anim_clips.ps1` to create animation clips for elk and moose based on existing horse animations.
- Implemented logic to clone vanilla horse clips, adjusting parameters such as GUIDs, durations, and step points.
- Added `wire_anim_master_skeletons.ps1` to ensure all skeletal animations reference the correct skeleton GUID, with options for patching empty or incorrect references.
- Both scripts include detailed documentation and error handling for robust execution.

#### Add unit tests for collision body handling in Blender tools

`b23a27ef`

- Implement tests for argument parsing in add_collision_body.py to ensure required arguments are validated and handled correctly.
- Create tests to verify the comparison logic for collision bodies, checking for added, lost, or changed objects.
- Introduce tests for borrowed body validation in validate_moduledata.py, ensuring that collision bodies adhere to ownership rules and do not borrow from other kits improperly.
- Validate that specific cases, such as variants and shared bodies, are handled correctly without errors.

#### Add comprehensive tests for melee ladder and restat melee blades

`ea0592eb`

- Introduced `test_melee_ladder.py` to validate the functionality of the melee ladder specification and the fixer logic, ensuring constraints and edge cases are properly handled.
- Added tests for the target calculation, validation of specifications, and handling of exempt troops.
- Created `test_restat_melee_blades.py` to verify the behavior of the restat process for melee blades, focusing on the write path and ensuring that only specified pieces are modified while maintaining document integrity.
- Implemented tests to check for backup creation, preservation of BOM and line endings, and proper handling of exempt troops and couchable weapons.

#### feat(elephant): Refactor howdah crew spawning and diagnostics; add tests for crew lookup

`8b29f3b1`

#### feat: Implement career kit generation from troop data (#629)

`925db38d`

- Added `generate_career_kits.py` to derive starting kits for player careers based on the lowest-level troops of each culture.
- Introduced command-line options for dry run, applying changes, and verifying the career file against the derived kits.
- Implemented logic to select items based on difficulty, level, and culture-specific exceptions.
- Created unit tests in `test_generate_career_kits.py` to ensure correctness of the kit generation logic and item selection criteria.
- Added a deep review document for skill-template parity changes (#626) detailing findings and improvements made during the review process.

#### Add unit tests for HowdahSampleClock and ShippedFertilityConfig, and implement howdah platform prefab

`83bdad85`

- Introduced HowdahSampleClockTests to validate the timing and firing logic of HowdahSampleClock.
- Added ShippedFertilityConfigTests to ensure the integrity of shipped fertility data and configurations.
- Created a new howdah platform prefab XML file for the TAOM war elephant, detailing its structure and components.
- Conducted a comprehensive review of the howdah platform rebuild, addressing issues and ensuring compatibility with existing systems.
- Implemented a new test suite to validate skill template mismatches in character definitions, ensuring consistency with defined templates.
