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

## v2.4.1 (2026-11-10)

Commits since v2.4.0: 4 (4 with the version label, 0 without).

### Features

#### feat(special-resources): v2.4.0 - option to pay elite troops in gold

`bcd0f6fb`

Players asked to turn special resources (Castar, War Spoils, Gems and
the rest) off and pay for elite troops in gold instead (#1109). The MCM
group "Special Resources" adds two settings:

- Use Special Resources (default on, so nothing changes on update).
- Gold Per Resource Point (default 100, 1 to 1000).

With the toggle off, every resource price becomes gold at that rate:

- Upgrading into an elite costs its upgrade_cost x rate on top of the
  game's own gold price, in the party screen's own cost, check and
  charge.
- Recruiting an elite volunteer or tavern mercenary adds its
  recruit_cost x rate to the recruit price. Supply Lines orders carry
  it too, times their own tier and war markup.
- Recruiting an elite from prisoners charges recruit_cost x rate in
  gold when the party screen closes. Like the resource charge it is
  not gated (#563): it takes at most the gold you hold.
- Elite upkeep is added to the party's daily wages and shows as
  "Elite troop upkeep" in the wage tooltip. Unpaid wages follow the
  game's own rules; there is no resource desertion.
- The Elite Emissary sells for merchant_cost x rate in gold, and only
  to a player whose own resource is the owner faction's, so gold does
  not open every faction's elites. One purchase stops at 10,000,000.
- No resource is earned, the map-bar entry is hidden and the
  encyclopedia badge shows a coin with the gold prices.

Only the player's main party pays the extra gold; AI lords pay what
they paid before. Balances are kept while the toggle is off and come
back when it is switched on, with one grace day before upkeep.

Known limitation: the recruit price has no party argument, so garrison
auto-recruit in your own towns and the LordNeedsGarrisonTroops quest
reward also carry the gold recruit price for the troops that have one.
The recruit screen's wage preview leaves the gold upkeep out until the
troop has joined.

Three new strings (taom_res_gold_upkeep, taom_res_badge_title_gold,
taom_emissary_gold) are English in all 12 languages until the
translator runs.

Deep review (tier 1: data flow, engine compatibility, XML) found no
CRITICAL or HIGH; its findings are fixed or documented in
docs/reviews/rca-special-resources-gold-mode-2026-11-09.md.

Research: DefaultPartyTroopUpgradeModel.GetGoldCostForUpgrade,
PartyWageModel.GetTroopRecruitmentCost and GetTotalWage callers,
RecruitPrisonersCampaignBehavior, GiveGoldAction, ExplainedNumber
(installed v1.4.8 DLLs)
Save-compat: no save data; both settings are MCM and read live
Not-tested: in game; owed checks are listed on #1109

### Documentation

#### docs: v2.4.0 - link the v2.4.0 release notes from the index

`708a6a10`

The v2.4.0 release added docs/releases/v2.4.0-discord.md and
v2.4.0-testers.md without a row in docs/INDEX.md, so the trunk failed
tools/tests/test_doc_graph_ratchet.py (orphans 23 against 22,
components 28 against 26). The release verified the tree before the
release commit wrote the notes, and nothing ran the tool tests after.

Both notes now sit in the index beside the other release notes, and
the /release skill's Phase 5 says to add those rows.

#### docs(reviews): v2.4.0 - record the gold mode review and its lessons

`2de1ca08`

The #1109 deep review found eight elites free to recruit from
prisoners in gold mode, the second time TAOM missed the prisoner
raiser of OnUnitRecruited (the first was #558). The engine process doc
now lists all three v1.4.8 raisers: the volunteer screen, dialog
mercenaries and RecruitPrisonersCampaignBehavior's own listener.

Two lessons join the master record: list every path an old charge
covered before a toggle stands it down (campaign-mechanics), and add
an ExplainedNumber label only when its creator asked for descriptions
(gamemodels-services). The incident report is
docs/reviews/rca-special-resources-gold-mode-2026-11-09.md.

### Chores

#### chore(release): v2.4.0 - pin the shipped Map and Armory, reseed troops

`7383cc5a`

tools/release_pins.json is the first pin on this line. It records the
ModuleData fingerprints of the TAOM_Map and LOTRLOME_Armory that ship
with v2.4.0 (TAOM_Map v2.4.0 sha256:6e9d81ad9c79, 49 files;
LOTRLOME_Armory v2.4.0 sha256:1c3827bf3679, 464 files), so the next
release's pairing check can tell whether either package changed. It
was recorded after the build at the tag passed the stamp, package,
validator and Armory reference checks.

tools/shipped_troop_ids.txt now names v2.4.0 as the baseline of the
TROOP_ID_REMOVED gate: 857 troop ids from 16 files, the same ids the
branch baseline held.

Not-tested: none (tool data)

## v2.4.0 (2026-11-08)

Commits since v2.0.28: 815 (710 with the version label, 105 without).

### Features

#### feat(tools): v2.0.28 - click through engine asserts without a stall

`d2928f8c`

AssertAutoIgnore.exe by poheniks stopped on the rglBuffer.cpp:899
assert: it printed "Closing: ASSERT" once and never clicked. Its
button click uses a synchronous send, and one blocked call stops its
only thread.

tools/assert_auto_ignore.py does the same job with a posted
WM_COMMAND, so it never waits on the dialog. It keeps the exe's title
table, clicks only in the Launcher and Bannerlord processes, never
presses a button named Abort, sleeps between polls, and logs each
window once with its text. An ignored assert is no pass for asset
work: judge that with a game launch.

On 1.5.x the tool imported its process listing and dialog helpers
from run_fords_autorun.py. That runner belongs to Fords of War, which
this line does not carry, so the four helpers (powershell,
list_processes, game_dialogs, click_button) and their two constants
are copied into the tool itself, unchanged.

The title table came from reading the decompiled exe, so the
provenance register gains a behavioural-port row, license unknown and
uncleared. It is a dev tool and is never packaged. tools/README.md
gains its catalog row.

Not-tested: a live Modding Kit assert
Ported-from: ca1bc5f2e62de5263df40de74a1a5b39814a3116

#### feat(tools): v2.0.28 - /verify and /release report in-game verdicts

`df1f8c41`

/verify gains Step 6, which runs python tools/ingame_verdict.py --owed
--sessions 10 and reports the verdicts as information only: NOT
REACHED is the normal state before anyone plays, and a FAIL is shown,
never counted against the tree. /release runs the same command before
the notes are written, lists every owed check that is not PASS for
the testers, and names a FAIL to Mike before the release commit.

The architecture rule's two Outcome rows came in with WP143, so that
item of the step is not repeated. ingame-verdicts.md and the provenance
register drop their "once those edits land" qualifiers.

Research: none (skill and doc text)
Not-tested: a /release run with owed verdicts
Ported-from: c5705474ed5899a8bd5576436e572ff3614ca593
Ported-from: 53e13cfe35d72f4253c81df7b1f7db24ededc457

#### feat(tools): v2.0.28 - one release check for the TAOM and TAOM_Map pair

`27f70ceb`

TAOM and TAOM_Map ship as separate packages that name each other's rows.
tools/check_release_pair.py checks a channel's pair in one command: exit
0 clean, 1 a disagreement, 2 a missing or unreadable file, 3 a crash.

BROKEN_SETTLEMENT_REF is new. It reads Settlement.X refs in the XML and
in TAOM's XSLT, because spclans.xslt, spkingdoms.xslt and spcultures.xslt
write homes as element text, against the world the game builds. The sweep
no longer reads a commented-out row as a live ref. It joins both commit
gates: no finding here on the v2.0.26 Armory, provisional under D20.

NavalDLC adaptation: the 1.5.x commit removes NavalDLC from the roster
roots because its SubModule.xml lists the module as incompatible. The
1.4.x SubModule.xml names no NavalDLC row, so the roots keep it and the
test asserting its exclusion is left out.

release-process.md gains one paragraph in this line's own words and takes
none of the 1.5.x deletions. /release gains step 6d. equipment-rosters.md
is skipped: its hunk conflicts, and the 1.4.x text never claims no gate
reads these ids.

Research: none, offline tool
Not-tested: a real release channel folder
Ported-from: 002f8ff7e16e66bdfb796958dc7e13ce83b7af64
Ported-from: 334c187e4ba93a27247b698ad5309b876f2e9513
Ported-from: 0385cc0418719b2d000e19632c20e7b2a44b85da
Ported-from: 1499aaa697fb216c951b41a1b34ee19ba521a8f2

#### feat(tools): v2.0.28 - BROKEN_ROSTER_REF, a misspelt EquipmentSet id

`fb16ffda`

A misspelt <EquipmentSet id> stops every later character loading, and no
gate resolved those ids. The validator now reports one as an ERROR.

Refs come from TAOM's XML, the extra roots and TAOM's XSLT, because
lords.xslt names rosters too. Roster ids come from TAOM, vanilla, the
Armory and TAOM_Map, minus any an XSLT strips. Skipped without the
install. The code joins both commit gates and the codes doc.

Measured here on the v2.0.26 Armory: no finding. A clean result on that
Armory stays provisional under D20 and is owed a re-run on the public
one.

Research: none, offline tool
Not-tested: the public Armory
Ported-from: 3b4a406f702ac87475e78c4cb56280a762f3f44c

#### feat(hooks): v2.0.28 - commit gates for every committer

`3af9423b`

Only commit-msg bound a commit made from a terminal or an IDE. .githooks/
pre-commit now runs the gitignored harness file check, the doc linter on the
tree the commit records, and the ModuleData validator with the same code list
as the Claude gate, which CodeList holds equal. pre-merge-commit runs the same
script for a clean merge. Both are POSIX sh, since an IDE's git ships no bash,
and they fail open and say so. They run beside the Claude commit gates; both
stay on this line.

setup-dev-env.ps1 sets core.hooksPath, CONTRIBUTING.md's hooks paragraph names
pre-commit, and commit-gates.md records what each hook reads. The setting
itself stays the maintainer's to run. Left out: the career kit gate, which
this line does not have, the verify-section test the plan made conditional is
kept because the check exists, .gitleaksignore and the 1.5.x retirement date.

Not-tested: an IDE-bundled git (maintainer)
Ported-from: 434d1d795465fe9b78c06decd31db5a26558132c
Ported-from: b6012355be4f21100a890e62a7774cf7e82e3391
Ported-from: 3ead3765eb9b357725da0ef127db6c4cbb35d1aa

#### feat(tools): v2.0.28 - the doc linter judges what a commit records

`f20d1971`

Under --staged or --only-paths every gating check now reads the tree the
commit records, through one git cat-file --batch, so another session's
unstaged edit cannot refuse a commit and a staged breach cannot pass behind
a clean working copy. A crash exits 3, which the hook reports as UNCHECKED.

Check 11 is new and gates: a feedback_ citation in the rules, agents, skills,
ai-includes or engine study, a backticked symbol in a rule that no tracked
.cs or tools .py holds, and a dotnet line with no ModuleId. It found one real
line: xslt.md named LordTemplateRosterTests, which this line does not have.

check-doc-config-drift.sh runs on the folders check 11 scans, reads names
with quotePath off, and quotes every gating section. Lens 7 calls
data_gates.py. CLAUDE.md's size budget stays this line's: the 1.5.x ADR-011
context budget, its orientation trap index and scoped-rule ratchet stay out,
with their tests. check_model_registry changes in the next commit.

Ported-from: 35ff005dc5bb80870e49e46122b69ae57ac1a916
Ported-from: 2129f753c0f8d33730d6303ce3c8dccd6c78423e

#### feat(tools): v2.0.28 - commit-msg refuses a closing keyword list

`7bd77dae`

GitHub closes only the number right after a closing keyword, so "Closes
#1, #2" leaves #2 open with no signal. check_commit_msg.py now refuses that
form, ignoring code spans, and a crash of the checker exits 3 instead of 1,
so a checker bug no longer reads as a refused message.

Ported-from: 35ff005dc5bb80870e49e46122b69ae57ac1a916

#### feat(hooks): v2.0.28 - warn when MEMORY.md passes what loads

`929c6c13`

MEMORY.md loads only its first 200 lines or 25 KB (harness-facts.md), so a
longer index silently stops reaching sessions. session-start prints a
note at startup when the file passes either limit. A session in a sibling
worktree reads the main checkout's memory folder, for this note and for the
Dead ends lines after a compaction. The limits are the load cap: this line
has no ADR-011.

Ported-from: 3ea14a730b4c811e6d63699616e5df4199bb9416

#### feat(improve): v2.0.28 - durable Workflow scripts for /improve runs

`f9430690`

Five Workflow scripts (fanout, plans, execute, review, draft-issues) let
a /improve run survive a dropped session and never guess a CLEAN verdict.
.gitattributes pins that one folder to LF: the Workflow tool refuses a
script holding a CR. The hook suite's 7b denies an untracked .js under
.claude/skills; the hook's filter already names js (WP116).

Graphify stays in plans.js: K13 is true on this line (WP141).

Not-tested: a /improve run through the Workflow tool (live check owed).
Ported-from: 1198881f078bbe7d3fc74aa26e7fbf2dc64b5603
Ported-from: bc160ca8518675e5721b09ed5bd586b57cad7862
Ported-from: a7bbf3fe7915393a9151cd7fdffe0fe59f91da61
Ported-from: e4c43e59e30803c3bf7eefe62b5ace8cb9d41856

#### feat(tools): v2.0.28 - improve_ctl and integrate_branch run /improve

`33176e75`

improve_ctl.py holds a /improve run's mechanical steps: Workflow args,
plan status cells, Codex branch prompts from prompt-fixed.md, issue
filing on the maintainer's word and a liveness watch. integrate_branch.py
merges a finished branch, unioning conflicts only in
docs/reviews/lessons/*.md and REVIEW-LOG.md. It needs no CHANGELOG.md
rule here: no WP branch edits that file.

Neither tool is run by this commit, and no paid call is made.

Ported-from: 218488f4e773597209d66326c6810a4d90575242
Ported-from: d6742dd91fd17aaedda8aaee9949e0286a5c1497
Ported-from: ac1aa578bf800be54fcb59c45acadcd5edb4dd16
Ported-from: 0cc91b320d1b411ade2eca28d0b498a80ad2c4d6
Ported-from: d648e00c001f838ef89b0ecd52248a9d5fbed247
Ported-from: 459b63ad7813256b5e428702d9d5068e6f7b7e61
Ported-from: d24aff3408261dbb7adb830de597a71c0e951c79
Ported-from: 6c4b0981b79f5f6ff588c9fd51b6d2c29e53afbd

#### feat(tools): v2.0.28 - frontmatter allowlist and a verify canary

`cf71082a`

Claude Code silently ignores an unrecognised frontmatter key, so a typo
turns a setting off with no error. Hook suite 3b now checks skills and
agents against an allowlist and that each name matches its folder or
file stem, reads the rules too, and proves itself on a planted tree.

VerifyCanaryTests fails when TAOM_VERIFY_CANARY=1 is set, and /verify
Step 2c runs it to prove the runner can report a failure. /verify also
treats a total under 7,000 as broken discovery (the WP101 baseline was
9,154; the 1.5.x figure of 20,000 is that line's own suite).

Ported-from: c237c84e48d8b51a3c2e94508adbf99c3343e456
Ported-from: ad3967222267b7e0d5a142a6fca1e74ef0197650

#### feat(tools): v2.0.28 - ported ralph rules enforce themselves

`8aae8399`

Rules from the ralph-claude-code review land where they apply: name what
a verification run did not cover, an empty search proves absence only
inside its scope, fix a bad value where it is produced, ask before
adding a guard for a speculative case, and digits only into bash
arithmetic (hook suite 4f probes every hook for it).

The loop breaker is a tool: loop_progress.py ends a work tick and exits
3 after three ticks with no new commit and no change in status, diff or
watched files. session-start.sh also runs on compact and prints recent
Dead ends, the only compact-time channel that reaches Claude.

The 1.5.x rule files were rewritten after the fork, so the final wording
is inserted by hand at the matching place. Left out: the CLAUDE.md item
8 this line does not have, section 8 of the hook suite (a scanner this
line keeps under /context-budget), and the perf_runs, run-protocol and
workflow hunks, which land in later commits.

Ported-from: 4407fbc055cb48f7f748415e5298540e210af060
Ported-from: 4d17aeab8da29c7426ebfa80c813fdda890ee6fd
Ported-from: a7bbf3fe7915393a9151cd7fdffe0fe59f91da61
Ported-from: a87bdc3a95ed7bab1766e5523401ea7aa6437964
Ported-from: 9e9f04df40c4a2350fd705b9dc2c0c158aee49c6

#### feat(tools): v2.0.28 - check public text before it is posted

`39749d21`

check_public_text.py refuses local drive paths, long dashes and other
leaks in a file meant for a public issue or PR. /issue now builds its
body in a file and checks it first, its Analysis marks each claim as
observed, source-derived or hypothesis, and Testing lists only checks
that actually ran.

Ported-from: 5e5877bdfaba2746d98b94167cac616b9a41f65e
Ported-from: 086abc57b8f8bded08e59bf30cf92afb49f933cd
Ported-from: 0cc91b320d1b411ade2eca28d0b498a80ad2c4d6
Ported-from: 9e9f04df40c4a2350fd705b9dc2c0c158aee49c6
Ported-from: 4407fbc055cb48f7f748415e5298540e210af060

#### feat(harness): v2.0.28 - status line shows the usage windows

`a3943b64`

statusline.sh prints the five-hour and weekly usage windows the harness
passes it. A new harness-facts table records that statusLine renders
only in the terminal CLI: the VS Code panel shows /usage instead. The
claude-hud adoption record and its register row come along; nothing
from that project is installed.

Ported-from: 2473fa80edfa4bba8beb3bef48999b443cb3e3ea
Ported-from: dfd4cf9e0a8f0850b5e8547f3549646d08732b8d
Ported-from: 49bf3b11518082cbcd43e92e636bbaa3301c1517

#### feat(tools): v2.0.28 - yield by lens, provenance and stale loc checks

`4be12dc7`

analyze_reviews.py now reports yield by lens and by effort. The new
check_provenance.py checks the register's closed vocabulary, shipped
binaries and adoption records: it reports 3 misses on this line
(mattpocock improve architecture, mattpocock teach handoff, ponytail,
all adoption records with no register row) and does not gate a commit.
check_stale_loc_rows.py lists language rows left holding an old
translation after their English changed, and /localize gains Case E.
The doc graph ratchet gets its new cases and a baseline re-measured on
this tree: 22 orphans, 26 components, from 26 and 29.

/verify gains Step 2e (data gates), 5b (gates silenced) and 5c
(provenance). The provenance rule and the register's intro now describe
a checker that exists.

Adapted: LiveLogTests reads this line's own REVIEW-LOG.md, which has no
"(at merge)" rows, so it asserts more than 50 reviews and not the
1.5.x count. Left out: /verify Step 7 (WP143) and the REVIEW-LOG hunk.
The API snapshot hunks wait on K7: /verify has no Step 2d yet on this
line, a deferred .claude hunk of WP137.

Ported-from: 69d2e8c0a27ac3b061df1549065b1e68460ab437
Ported-from: f1675c0581a472fcc72fb3c8da7f1566912255fe

#### feat(harness): v2.0.28 - harness retro measures harness changes

`02488806`

tools/harness_retro.py mines this machine's Claude Code transcripts for
hook kills, tool errors, output size and tokens by agent, and the
/harness-retro skill turns that into a verdict on each harness change.
session-start.sh prints HARNESS RETRO DUE once the newest record is
seven days old, so a measurement gets read. The transcript folder name
is now derived from the checkout per machine; the source tool hardcoded
the desktop's slug. The banner reads DUE until a 1.4.x retro exists,
because the only record on this line is the 1.5.x one the hook comments
cite.

Adapted: the cadence block's comment drops the shared probe deadline
this line does not have, and hook suite section 7o carries the cadence
cases with its own fixture instead of 1.5.x's upstream-watch one.

Left out: the .gitleaksignore hunk of 4358d1622 and every tools/README,
INDEX and lessons hunk. CLAUDE.md no longer states a skill count (WP103
trimmed the sentence), so only the routing row was added.

Research: empirical slug rule in harness-facts.md "Memory file".
Ported-from: c8a3ac9d97ec33acfac996d1478ccf8404aa4317
Ported-from: c64c78e7273d666404335845af266b31a596dba4
Ported-from: 78fe34c16878593b0ba15cfe8355e0ad865793cb
Ported-from: 0985c95157c461615c96232b4d0a5af099d88e0f
Ported-from: c237c84e48d8b51a3c2e94508adbf99c3343e456
Ported-from: d94bddecb3d50967a8b9266183cee879cb8d6844
Ported-from: ad3967222267b7e0d5a142a6fca1e74ef0197650
Ported-from: 4358d1622c073ae6b2827c83223aad71a04a52bf
Ported-from: 3f53e4623a632c66feb4df9fa8477f632dbc3f2e
Ported-from: 69d2e8c0a27ac3b061df1549065b1e68460ab437
Ported-from: 549bf390246afd94a364a6fab082445b2da6d751
Ported-from: b6f0ec794b03e8341001bd2052f05e0aebab1ce8
Ported-from: a87bdc3a95ed7bab1766e5523401ea7aa6437964
Ported-from: 452796db4b02217624ab0075d5c93742b2820fa3
Ported-from: 4fbfb7adc8797ae4a6180f2e135ca5849ec08b07

#### feat(tools): v2.0.28 - security audit vets whole trees and user scope

`5dd2160f`

--external reads the whole foreign tree (it scanned almost none of a
plugin repo before) and gives every script and JSON file the hook rules.
--user vets the machine-local scope. A run that collects nothing prints
UNCHECKED and exits 3.

The 1.5.x copy also scans files CLAUDE.md @-imports, through
lint_docs.markdown_imports, which this line lacks. That part
(_claude_md_imports, its seven lines in collect() and CollectTests) came
from c79a58521 and is left out here.

Research: 1.5.x blobs at fdf9f38cb
Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005

#### feat(build): v2.0.28 - fail restore on high NuGet advisories

`12c5a7d0`

NuGet Audit now covers transitive packages, and a high or critical
advisory (NU1903, NU1904) fails restore and so the build; offline it
stays a warning. /verify Step 1b forces a full restore, because NuGet
skips the audit on a no-op restore. The executor applied step M2's
Directory.Build.props block under D31 rule 2; no advisory is
suppressed, and the first forced restore reported none.

Research: dotnet restore --force-evaluate clean on 2026-11-06
Not-tested: none (build gate)
Ported-from: affcee2ead013f29a4b059af5ea999be440e0998

#### feat(hooks): v2.0.28 - gates read both shells, judge pushes in Python

`07db926b`

Each git gate now reads its command through _shellwords.py, so a
PowerShell command is judged as the Bash command it equals. The reader
itself arrived with WP103; this commit wires it into the gates. A
here-string commit message no longer reads as the subject "@", and git
named by a path, in capitals or with a backtick continuation no longer
slips past the confirm gates.

Each Bash hook tests the raw payload for its own word before starting
Python. validate-push judges every line, every refspec, abbreviated
force options, --mirror and forced matching pushes in one Python run
under a 3.0 s deadline, and asks when it cannot judge. The verification
marker ignores quoted mentions and interrupted runs.

check-doc-config-drift, check-moduledata-validation and
check-native-dll-crt get small deltas instead of their 1.5.x copies,
which need validator codes and a lint flag this line lacks. The
validator code list WP108 and WP136 added stays as it was.
block-dangerous-git is the fff6d66e9 state here; the next arms follow.

Research: 1.5.x blobs at 88e805450 (block-dangerous-git at fff6d66e9)
Not-tested: the PowerShell tool's live payloads (live checks L2 to L6)
Ported-from: 90141357d4113e5a5dbd8acd1a3acd862c510afc
Ported-from: 787fd3663d5238c45c3e4bf8b08c98d9229e953b
Ported-from: 7aa658e3e6c12db7a38fffe403ec45a5e0fee2fb
Ported-from: 6514fdfe77d85b9c49e9dfb59bb81b21e4cfb4a4
Ported-from: ee1929fa4c2a9f6fd0476533b1acf14cd1bb1786
Ported-from: 43e6780e4421fb785ac9bf1c4ee5bc53e8b69a68
Ported-from: c80c4108c88e9d5e28ec047458542235639f53c7
Ported-from: 65b405e42529bc390c9e9b8eca3e0427948c3389
Ported-from: 18961c1ec6b92c54b54bd1d25e6cd3ec066c6b03
Ported-from: 764ab0984eeedd7658a67a3829d46aa338cd1b8d
Ported-from: b23dc8999a9a0339fcee23a603003f4b60237c88
Ported-from: e046bdf09a9f4cf9b6924d59444e774710dab75a
Ported-from: 3a061406134188239ee506372aaf1ee5d486485f
Ported-from: cfd3b48f64cbddb658d646fc6b654d32bcc4977e
Ported-from: 64b9f3a6c2b522cdd5ee353f9ce594c7e4e9d5db
Ported-from: 747b6dae8e68ae9354739cc7a5d696a9733f559b
Ported-from: 60d85ed7d5a98d0a298c492866f26d72cadd1db5
Ported-from: a4ba6b0b3e17145c0d0d58d73fdf98b4be18b258
Ported-from: fd1d5c17de387bda26a5966396b69c1782e3a9e8
Ported-from: 343c6f48881b20e74fd1ca234ab0029f8941d1c7
Ported-from: fff6d66e9bd5a2db11c0ef20ab167225efbd1a22
Ported-from: d01bddf104e9271f600e5ad5895b71faedcaa888

#### feat(release): v2.0.28 - packager reports sacks and JIT state

`e84112c9`

Three read-only reports on every run: scene shader caches with a format
WARNING, module sacks with size and format under one policy line, and
JIT optimization per shipped DLL copy (OFF is expected for the Debug
builds TAOM ships). None of them refuses or changes the exit code.

Module sacks keep shipping on 1.4.x. The 1.5.x exclusion rested on an
untested assumption that the game uses a Kit-built sack, and E3 shows
2,720 shaders pending at battle start, so leaving them out waits on a
v1.4.8 test. Port of #717 and #448.

The fixture of TestModuleShaderSackCli now writes an empty report that
adds up, because its Armory has to pass the B5 shader gate, which landed
before this step. Review records, lessons and plans/035 are dropped; the
release docs follow in step C5.

Not-tested: a real packaging run into a channel folder
Ported-from: e4c2c159a93fd8dd5680638e0c4da664cbf33f9e

#### feat(release): v2.0.28 - refuse to package a dirty or off-tag DLL

`b94e1b54`

--require-build REV reads every bin/<platform>/ copy of TAOM.dll and
TAOM.Dependencies.dll and refuses a stamp that is .dirty, nogit or
.nogit, names another commit, or cannot be read; a requested module
missing from --source; an empty or unresolvable REV; and a REV whose
Directory.Build.props lacks TaomStampWorkingTreeState. This gate would
have refused the public pair (E3: TAOM +8df90444, Dependencies
+0c323437). Port of #658.

B4 to B7 landed first, so this is a three-way merge of the plan 017 end
state, not the plan's checkout, which would have dropped their gates.
Both sides of every conflict stay. Gaming.Desktop leaves
TestRequireBuildCli and Game Pass leaves the SHIPPED_DLLS comment and
the matching test comment, finishing step B6.

Ported-from: 5d1b1bda12271de7383a1c2ca0ccbbd32698541a
Ported-from: db1a7166b974b4d6b927fadfb0fd077278e914e9
Ported-from: d5033e4e897f3a0500ec5808ba495d8217211c42
Ported-from: e6ee8c9985beee1b63dafb2cda11c1a9b5879ae4

#### feat(build): v2.0.28 - mark dirty and git-less builds in the stamp

`d5dd8430`

The SHA in the stamp could not prove the DLL held only committed code
(the public +8df90444). TaomStampWorkingTreeState marks .dirty, .nogit
or nogit, and the packager's --require-build refuses all three. The file
is applied at the plan's pinned state (M1) by the executor under D31:
the maintainer delegated the protected-file lane.

Research: dotnet --version 10.0.401 here; E3 line 2 shows the SDK +sha
 suffix on a 1.4.x build
Ported-from: 057628a2b1464cff588a429c2b0ed7f58878e1e5
Ported-from: 9cedb92df241a24a2ba65d5aa3f5dc47805484b1
Ported-from: 3c747f4fb7ff809c66697b62f00ad71a67a51de5

#### feat(tools): v2.0.28 - lint_docs --drift-only keeps the drift gate fast

`b61193a6`

The gate ran the whole linter under a 20 s bound and that run took 25 s
on the laptop when planned, so once its output is read a gated commit
could get a timeout prompt instead of a verdict. --drift-only runs only
the checks the gate blocks on: config-example drift, version markers,
the budget and, since WP136 put it in --fail-on-drift, the RCA FOLLOW-UP
check.

Research: lint_docs check timings measured in-process on the laptop, 2026-11-04
Ported-from: c79a585218ad22841c8db1f273e013385512b332

#### feat(git): v2.0.28 - a commit-msg hook checks every committer

`730edf52`

Terminal and IDE commits now meet the same rules as the subject gate.
.githooks/commit-msg runs tools/check_commit_msg.py on every commit and
merge message: the label with the committed tree's version, at most 72
characters, and no AI attribution line.

It fails open, saying so, on a missing Python, a Microsoft Store alias
or a crashing checker. It runs only after a clone sets
`git config core.hooksPath .githooks` (a maintainer step), and merges
then need a labelled message.

Not-tested: whether git cherry-pick or git revert run commit-msg
Ported-from: 0382d5d2a435c35a16c8d4c8da6017fcf258176a
Ported-from: 434d1d795465fe9b78c06decd31db5a26558132c

#### feat(tools): v2.0.28 - gates for removed troops and half-armed kits

`d8674268`

Three new validator codes. TROOP_ID_REMOVED (error, in the commit hook)
fails when a troop id in tools/shipped_troop_ids.txt is gone from the
troops files, which would leave a hollow character in a save. The first
baseline is the 857 ids of branch bannerlord-1.4.5, read with git show,
because the v2.0.28 tag lacks two of them. INCONSISTENT_WEAPON_SLOT
(warning) reports a launcher in a slot of some battle sets only, or no
weapon slot filled in every set. TOWNSFOLK_WITHOUT_BATTLE_ROSTER
(warning) reports a civilian-only character in characters/npcs_*.xml.

The baseline needs no Armory. Both warnings print 0 here, but the
launcher half reads the installed Armory v2.0.26, so its 0 is void until
the public Armory is installed (D20, D25).

Constraint: the ammo half needs CREATURE_GEAR's item_trade, absent here
Research: v1.4.8 BasicCharacterObject.cs:108
Save-compat: none; a removed id would keep a hollow character in a save,
 which this gate refuses
Not-tested: none (offline)
Ported-from: f1675c0581a472fcc72fb3c8da7f1566912255fe
Refs #295

#### feat(validator): v2.0.28 - every Faction reference must name a clan

`70433853`

validate_moduledata.py now checks that every Faction.X reference, such
as a settlement owner or a hero's clan, names a clan the game loads. It
reads characters/clans.xml and vanilla spclans.xml, leaves out the five
bandit clans spclans.xslt removes, and allows Faction.neutral, the
engine's clanless value. A wrong reference loads the object ownerless;
it is now an error and the commit hook names the code.

A validator crash now exits 3 with its traceback, not Python's 1, which
read as findings. The commit hook allows that commit and carries the
traceback as additionalContext. The moduledata query and MCP reference
search learn the Faction. and BodyProperty. prefixes.

Research: 235 clans, 0 unresolved Faction refs over the repo and the
 installed Armory v2.0.26 and TAOM_Map v2.0.27 (provisional, D25)
Not-tested: tools/test_hooks.sh block; needs WP103's decision_of
Ported-from: ca92db87882f6b9dc08d526a231e8e4a531dcc57
Refs #949

#### feat(tools): v2.0.28 - CHANGELOG release section from the commit log

`13c6d7ee`

Adds tools/changelog_from_commits.py, the generator /release is to use
to write CHANGELOG.md. It reads every non-merge commit since the
previous v* release tag and groups the commits whose subject carries
the version label by type (Features, Fixes, Balance, Data,
Performance, Refactoring, Tests, Documentation, Chores, then any other
type alphabetically). Commits without the label get a last group of
their own, so nothing is lost. Each entry is the subject, the short
SHA and the body. A body line that Markdown would read as a heading is
escaped, and a commit that landed twice with the same subject and body
is listed once.

Without --write it prints the section; with --write it inserts it
above the newest release section of CHANGELOG.md. It refuses (exit 2)
a malformed --version, an empty range, a version that already has a
section and any hand-written heading above the release sections. Its
summary names the commit the range ends at, which /release is to check
before it tags.

On this line the range starts at v2.0.28, the newest release tag
reachable from bannerlord-1.4.5, and the next release is v2.4.0.

tools/tests/test_changelog_from_commits.py holds 35 tests, including a
check that the label pattern matches the subject gate in
.claude/hooks/check-commit-subject-version.sh in both directions.

This is Step 2 of plan 102 alone. /release, AGENTS.md and the hooks are
unchanged until Step 3 onward lands, so the README row and the
docstring describe the flow those steps put in place.

Not-tested: a live /release run; v2.4.0 will be the first.
Ported-from: ae85381dc3666b9869692930c1f57a9df0b6d0b5
Ported-from: 6e4bfedb7769bdc1d45d3cf8f90f1564851b9015
Ported-from: 971d8e97581cf6a43d7f9671eb9b19b25cb07c76
Ported-from: 5b242cee660b1675f2c3f582f91130bd9a85cd35
Ported-from: 9bfd0f4117f52de59a259df04a029ebd9c9f1590

#### feat(logging): v2.0.28 - report swallowed exceptions, add a ratchet

`dea1c7d8`

Silent catch blocks in the enlistment lord-conversation gate,
character creation, hero race spawning, the order of battle overlay,
army targeting and the creature behaviour tree wrapper now write one
[Swallowed] warning per site and a count in the [Session] summary.
Every fallback is unchanged; catches that are silent on purpose (a
failing log call) carry a swallow-ok comment. The behaviour tree
wrapper's other three failure paths reach the log the same way.

SilentCatchRatchetTests counts silent catches in every Harmony patch
and Hooks file of Main against a per-file baseline (133 files, 196
catches on this line). A new silent catch fails the tests; a fixed
one lowers the baseline in the same commit.

Research: none (no engine member changes)
Not-tested: the [Swallowed] lines in a real session
Ported-from: cd4fb371983b6250c3a58a0d9a857da1f1c15059
Ported-from: 00fddd3c5ebfbd89c7b37363fc3aec80d36af2ec

#### feat(recruitment): v2.0.28 - a daily log check for AI castle recruits

`eff3f1ab`

With AI Recruits From Castles off, the log says at each daily tick
whether the setting held (#918). taom.castle-recruit.ai-off, keyed by
the campaign day, passes when no AI lord took a castle notable's
volunteer and counts the castle rule's refusals (blocked=); it fails
when one did, with a second line naming the castle, the lord, the
notable and the troop; it skips with reason=toggle-on. The reader
passes after three days that each turned an AI lord away. The day
floor is a local copy of 1.5.x's War Chronicle helper.

Research: v1.4.8 RecruitmentCampaignBehavior ApplyInternal raises
 OnTroopRecruited (:606-638); garrisons use their own query (:124);
 Campaign.CreateCampaignEvents reruns the loaded day only for a save
 made in its first half (dump Campaign.cs:1002-1011)
Not-tested: three in-game days with AI castle recruiting off
Ported-from: 852678f8600969deeb0f46627002ee7834275ecd
Ported-from: da5c87c23b1ef61e900653b80e0fc434a7eac7b7

#### feat(hero-race): v2.0.28 - race persistence writes check lines

`5021586a`

Race persistence writes check lines that show, from the log alone,
whether dead heroes kept their race across a save and a load (#1011).
Each save writes taom.race-persist.capture with the hero and dead
counts (dead=0 fails: every campaign starts with a dead lord_E6_2).
Each load writes taom.race-persist.restore after reading every race
back: mismatch=0 passes, a new campaign or a save with no record
skips, an empty map from the save's record fails. The hero roster
reports IsAlive. No saved field changes.

Research: v1.4.8 Hero.IsAlive, Hero.FindAll; lord_E6_2 is
 alive="false" in characters/heroes.xml:152-154
Not-tested: a save in one session and its load in the next
Save-compat: no saved field changes
Ported-from: e4dbdf9c4f50ff3d680471602611d04abef6ac65
Ported-from: 40006d078a5ff4098d173eb30e3697cac911e5b3
Ported-from: 75c0c27a3d3cd0cce416de72639ccd04ffac975f

#### feat(career): v2.0.28 - career screen open and init check lines

`7b16afda`

The career button logs whether its click opened the career screen, so
python tools/ingame_verdict.py --issue 971 judges it from the log.
taom.career-screen.open passes when the career screen is on top after
the click, fails with reason=not-on-top, or with the exception's type
when the click throws (the exception still propagates).
taom.career-screen.init passes at the end of OnInitialize with
mode=normal or mode=switch. IlCallScanner gains ExtractOpCodes and
ExtractReferencedMembers, which the new IL tests use.

Research: v1.4.8 ScreenManager.TopScreen, TrySetFocus;
 GameStateScreenManager.CreateScreen uses Activator.CreateInstance
Not-tested: the career button click in a campaign
Ported-from: 0ab3dc35ad6e1f40a5154f5d12175841a34b4cb8
Ported-from: 4e0c2a3b0b3cc53353a061db611392f84ff8834b
Ported-from: 2d6934006f6e842a2706626a3f73ff8a6460494e
Ported-from: 92839bc52da2a4d5bf2aeb6404ce415fdd24b3fe
Ported-from: 5fcf8390c63d6955255a291ed050095066d8a862

#### feat(logging): v2.0.28 - battle action bar attach and stance checks

`393100a4`

The battle action bar (#970) writes [Check] lines, so
python tools/ingame_verdict.py --issue 970 judges it from the log.
taom.battle-action-bar.attach passes once per mission when the bar's
layer is added and its movie loaded; it fails when the attach throws,
when the movie did not load, or at mission end when a Battle-mode
tick saw the setting on and the bar never attached; it skips with
reason=setting-off. taom.battle-action-bar.stance passes when a click
reaches Brace for Cavalry, Pike Wall, Testudo, Line Charge or
Skirmish. The verdict mappings live in BattleActionBarChecks.

The view passes ADR-002's 150-line limit for entry points at 160
lines, as on 1.5.x, so it takes one row in the shrink-only
EntryPointLineCountBaseline.txt and MaxBaselineEntries rises by one.
Splitting the view is a separate refactor.

Research: v1.4.8 GauntletLayer.LoadMovie, IGauntletMovie.IsLoaded,
 Mission.EndMissionInternal, MissionScreen.OnEndMission (taom-src)
Not-tested: a field battle with Show Battle Action Bar on
Ported-from: b4ad748f2dcf4946f404659871990a6e2734c1f1
Ported-from: 30a938689bf08da25705cc7ab0a59f4735b178c9
Ported-from: 0bc2a132fffe8daf834518dccb02d8f71a477d75

#### feat(tools): v2.0.28 - in-game check verdicts read from session logs

`a90190b3`

A new tool, tools/ingame_verdict.py, reads a play session's logs and
gives each owed in-game check of an issue a verdict: PASS, FAIL or NOT
REACHED, with up to three evidence lines per check. Each issue's checks
live in tools/ingame_checks/<issue>.toml. Evidence counts only from a
build whose [BuildStamp] sha contains the fix (git merge-base
--is-ancestor), and each session's last [Session] summary is printed
with its swallowed sites. Read-only unless --post is given.

The first manifest is #1076 (a birth from a hollow parent). The
reader's own tests use copies of the 1.5.x #970 and #981 manifests as
fixtures; #981 (co-op server settings) does not apply to this line.

Research: 1.4.x FileLogger, BuildStampReport and DiagLog write the
 formats the reader parses; E3's BuildStamp line matches its pattern
Not-tested: a verdict over a played 1.4.x session
Ported-from: c8d69e426bb843ce73ef300e140dd4451d56eb65
Ported-from: c5705474ed5899a8bd5576436e572ff3614ca593
Ported-from: 53e13cfe35d72f4253c81df7b1f7db24ededc457
Ported-from: 95647d13b230016d28c6f51732f6d1b315b82bc5
Ported-from: b4ad748f2dcf4946f404659871990a6e2734c1f1
Ported-from: 30a938689bf08da25705cc7ab0a59f4735b178c9
Ported-from: 1165a00dec70c2b37b35dce4b97fa8507f7f3272
Ported-from: de90a24514689b1f60afdd32971ffdb6afff141e

#### feat(logging): v2.0.28 - check lines, counters and a session summary

`17dc7bb1`

TAOM's debug log can now record that a feature did what it was built
to do, and how a session ended, in lines a script can read.
IOutcomeLog writes "[Check] id=<id> result=pass|fail|skip <detail>"
once per id, result and scope, counts hot paths with no string or
disk write, and reports a swallowed exception once per feature and
site. A "[Session] summary" with those totals and PatchShield's
swallowed count, which until now reached only diag.log, is written
before each save, before a crash report collects its log tail, just
before the [Shutdown] line at unload, at process exit, and every 10
minutes of real time. Features move onto it in the next commits.

Research: no engine member; PatchShield.SwallowedTotal,
 StopwatchStampClock (plan 118), DryIoc 4.8.8 on both lines
Not-tested: the heartbeat, exit and crash summaries in a real session
Ported-from: cb4eeaa3de5cffc961d6b287460f76d612636faf

#### feat(release): v2.0.28 - check the TAOM_Map and Armory pairing

`2a075ecb`

TAOM_Map and LOTRLOME_Armory are unversioned installs outside git, so
nothing recorded which copies a release shipped or whether they suit
this line. tools/check_release_pairing.py reads the package being
released and reports: MODULE_VERSION (a fourth part or a 1.5.x number),
VILLAGE (an add_map_villages table id the map lacks), HIDEOUT (a bandit
culture with no den, which throws on a new game), and PIN_MISSING or
PIN_MISMATCH against ModuleData fingerprints in tools/release_pins.json.

Monster attributes outside Monsters.xsd print as a pairing signal on
every run and are stored in the pin. Under D15 a signal such as
taom_body_length is never allowlisted and never changes the exit code.
The tool is read-only except --record, which refuses while a non-pin
finding is open. No pin is committed here: the maintainer records it
from the copies the line ships.

Not-tested: the tool against a real channel folder

#### feat(tools): v2.0.28 - each release lists its owed in-game checks

`d7f1bd96`

tools/ingame_ledger.py lists the closed issues still labelled
triage-needs-ingame that this release's commits name, for the testers.
It never blocks: the exit code is 0 however many checks are owed. The
1.5.x tool selected by close date, which is wrong here: a ported fix
closed months earlier is new to this line and a 1.5.x-only issue would
be listed. On 1.4.x an issue counts when a commit message in <tag>..HEAD
names it. Port of #1010.

/release does not call the tool yet: that wiring is plan 115 step C5,
which waits on the changelog plan and the --require-build gate.

Not-tested: the tool against the real gh and a real release range
Ported-from: 715530875ca12d3f3058aa7cc748738f73ca5745

#### feat(diagnostics): v2.0.28 - BuildStamp names each DLL's configuration

`65107ef5`

E3 and E4 cannot tell a Debug DLL, and every frame time from a public
build measures unoptimized code. The [BuildStamp] line now follows each
stamp with config=<configuration> jitOpt=<ON|OFF|unknown>, placed so
"TAOM=... build." and the " TAOM.Dependencies=" boundary still parse.
The shipped configuration is unchanged: Debug remains the recorded
policy, and this only makes it visible.

Research: net472 reference docs,
 DebuggableAttribute.IsJITOptimizerDisabled and
 AssemblyConfigurationAttribute.Configuration
Not-tested: the line in a player log

#### feat(crash): v2.0.28 - put the TAOM build stamp in crash bundles

`cdd9ef5d`

IdentitySnapshot gains a last field, TaomBuild (the assembly's
InformationalVersion), printed as "Build:" in report.txt and as
"TAOM build:" in manifest.txt and serialised in report.json, so triage
no longer depends on the bundled log. The field goes last so the
positional fallback cannot shift. Port of #658.

Not-tested: IdentityCollector reads ModuleHelper in game only
Ported-from: 743ce2e1d892a312f8f2ff91a759ac91606eb23a
Ported-from: db1a7166b974b4d6b927fadfb0fd077278e914e9
Ported-from: ff84e1b88fe01e21fabc920dc8b922c1c474a270

#### feat(banners): v2.0.28 - drop 39 dead banner icon sprite sheets

`9e1b0379`

The ui_taom_bannericons sprite category declares two sheets and the
engine loads sheets 1 to SpriteSheetCount and nothing else, yet the
module still carried sheets 3 to 41 from an old 41-sheet bake: 39
texture descriptors, 39 source PNGs (70,479,496 bytes) and one stray
Kit temp file. Nothing loads them, but every published pack0.tpac
carries them: 41,529,118 of its 188,902,921 bytes today. They are
gone, and a new tool test fails on any sheet file the sprite data
does not declare. Players get the smaller pack with the next Kit
publish.

Research: v1.4.8 TaleWorlds.TwoDimension.SpriteCategory.Load; table of
 contents of the laptop's installed pack0.tpac
Ported-from: 9985f2db3a0396eb8c209e8b1229f5368a658f21

#### feat(banners): v2.0.28 - mirror TAOM.Dependencies into the Kit's bin

`8448326a`

A deploying build now copies TAOM.Dependencies' assembled client bin
into its Win64_Shipping_wEditor folder, as Main/TAOM.csproj already
does for TAOM. The v1.4.8 Modding Kit looks for each submodule's DLL
in that folder and then in its own bin, which holds no Harmony, MCMv5
or UIExtenderEx, so until now the Kit could not load TAOM at all: it
showed a "Cannot find" box per submodule and stopped. The community
modules that once supplied those DLLs are DLL-less stubs on this
line. This unblocks every Kit job on 1.4.x, starting with the banner
atlas re-import. Agent builds skip the target, and players see no
change.

Research: v1.4.8 Module.LoadSubModules and CheckIfSubmoduleCanBeLoadable
 (installed DLL Module.cs 1026-1082, editor build)
Not-tested: the target runs only in a deploying build (plans/121 Part B)
Ported-from: 9985f2db3a0396eb8c209e8b1229f5368a658f21
Ported-from: b41de91e8e9b71b1c2003d367f7217b55713f8e2

#### feat(tools): v2.0.28 - a gate for text rows that nothing reads

`e43218a4`

The gate lists registered TAOM rows that no code, data, composed family
or reasoned allowlist entry reads. Its first run here removed 53 rows
from the English files (35 unread, 18 shadowed), plus the 35 unread rows
and cache entries in each of the 12 languages. A shadowed row leaves the
English file only.

Not-tested: none (rows nothing reads)
Ported-from: 616cf12f16d2942e0235d793afc375509a8c204c

#### feat(localization): v2.0.28 - troop, lord, clan and kingdom names

`3ccd8888`

Those names carried {=KEY} defaults that no English source registered,
so every language showed English. The generator registers them in four
English files (troop 857, lord 988, clan 120, kingdom 60), and every
language file holds an English row until the translation run.

Not-tested: in game, names show translated after Part C
Ported-from: 303bf2a7d9db038474eca90af2a6bced8047868c
Ported-from: 9e2a39f4092dd32c7bd58e193e24ee210594f26f

#### feat(tools): v2.0.28 - translation fit report and screen checklist

`8d1b52d8`

The report lists translations much longer than their English for a check
in game. The checklist lists what a new screen must handle.

Research: dump EventManager.cs:880 (disabled widgets are never hovered)
Not-tested: none (report only)
Ported-from: df375d99e6312ad583fa48ba9a6482eb2c988b6c

#### feat(tooling): v2.0.28 - graphify wrapper, tests and doc (unwired)

`0596f9e7`

tools/graphify_taom.py is the only supported way to build or query a
graphify code graph: refresh, status, report and the query verbs, plus
the gate judge that denies raw write verbs. Its 82 tests use temp git
repos and need only git. graphify is not installed on the maintainer's
laptop, so every graph command there exits 3 until it is.

The hook script .claude/hooks/check-graphify-usage.sh, its
tools/test_hooks.sh rows and its settings.json registration are held
back until the hook work lands; the feature doc and the provenance
register say so. The workflow wiring and ADR-012 stay on the 1.5.x line.

Port of #677 and #995.

Research: the wrapper needs nothing from .claude at runtime; the gate
script at e45f6df2c is what would call its gate subcommand.
Not-tested: a real graph build, and the gate live in a session.
Ported-from: b8833474ee24856fd5ecd1b073cd20f648e80f69
Ported-from: e6745a49208ae2b9bb7223a501aec41bfe2b1d9b

#### feat(tools): v2.0.28 - check_rdc_entries lists packages the client skips

`1cacd398`

The client renders only packages with a RuntimeDataCache/<package
GUID>.rdc entry, which only the Modding Kit writes, and it skips the
rest with no log line. check_rdc_entries.py lists packages under a
module's loose Assets folder that lack an entry. It is read-only and stdlib only.
A run that checked nothing exits 1, and skeletal animation masters,
which never have an entry, are counted without failing.

This laptop's Armory and TAOM_Map are cooked, with no RuntimeDataCache
folder, so this is a desktop gate. Only this tool is taken from
4f7ae42ef; the cave troll pipeline and tpac_fix_item_checksums.py stay
out. The skip itself was measured on v1.5.3.

Port of #616.

Research: native-commit-audit-2026-08.md:146 and :259 settle the RDC
premise on this line.
Not-tested: a run against the desktop's loose Armory.
Ported-from: 4f7ae42ef14e18d646f0160f748c10e580c1f019
Ported-from: 4520af802d9c31f44263c7a9ce4a263e611d0c5d

#### feat(tools): v2.0.28 - a bare FOLLOW-UP in an RCA fails the doc gate

`d3c44847`

A deferred finding written as a bare FOLLOW-UP in an RCA gets lost:
three times a real defect sat in one with no owner until someone filed
an issue. A new FOLLOW-UP line in docs/reviews/rca-*.md must now cite an
issue or say "no issue:" and why, or lint_docs.py --fail-on-drift exits
1. Only new lines count, and --staged reads the index instead of the
working tree. The commit hook half of the change waits for plan 103.

Not-tested: none
Ported-from: bc30b76dac099ad4433f3e73d5106c26179a930b
Refs #984

#### feat(tools): v2.0.28 - validator header names each module's vintage

`b186b942`

A stale TAOM_Map or Armory install reads exactly like a repo defect:
every reference into it fails and nothing says what was swept. Each
"Also sweeping refs in" line now carries that module's SubModule.xml
version and the date of its newest ModuleData file.

Not-tested: none

#### feat(tools): v2.0.28 - one command runs the data gates for what changed

`5499889a`

Authors ran one gate, saw it pass and called the work clean, while the
gate they skipped was the one that read their file. tools/data_gates.py
owns a table of which gates read which files and prints PASS, FAIL or
NOT RUN for each. A gate that skipped, validated nothing or could not
run reads NOT RUN, which has not passed. It selects from the files git
shows as changed, plus any live file named on the command line; --all
runs every gate. The table holds only gates whose script or test exists
on this line.

Not-tested: none
Ported-from: f1675c0581a472fcc72fb3c8da7f1566912255fe

#### feat(tools): v2.0.28 - the item registry reads only Items documents

`a19131b8`

validate_moduledata.py counted every <Item id> row in any XML file as an
item definition, so a feature config that quotes an id defined it: the
culture marketplace blacklist kept sk_mordor_mouth_of_sauron_helm in the
registry that way, and no ref check could ever fail on such an id. The
registry now reads only documents with an <Items> root, as the engine
does. The count falls from 5,901 to 5,900 ids on this laptop and the
per-code findings are unchanged.

Not-tested: none
Ported-from: 1ebcf86a894dae460a66b3fa851cb87ea2081f7f

#### feat(enlistment): v2.0.28 - the board's daily pay is what service pays

`5b147620`

While the commander was missing and no wage was paid (the grace
state that waits for him to return), the service board still showed
the rank's usual wage as "Daily pay". It now shows what service pays
today, which is 0 in that state, the same number the wallet tooltip
already used.

Rejected: the retinue-cost line on the oath and the board from the
  same 1.5.x commit (a new feature; plan 132 decision P132-2)
Not-tested: the board during the grace state, in game
Ported-from: 4666a254a1a0f4c9020110e4c6ff3a3216628ef4

#### feat(combat): v2.0.28 - cavalry knockdown parity (#610)

`2334a1d7`

Cavalry knocks down like vanilla again. The charge knockdown rule scaled
vanilla's 0.4 penetration by clamp(weightRatio / 6, 0.25, 2.5), so every
victim heavier than the charger got less than vanilla's penetration;
with TAOM's heavy orcs, uruks and trolls the 1.5.x line measured a
horse-vs-orc knockdown needing 30 to 52 damage on hits that deal 5 to
15. The floor is now 1.0, so the weight term never pushes a light
charger below vanilla, and the race rows do the resisting: cave and
hill trolls 4.0 (new), dwarves 2.5 and Sauron 3.0 (unchanged), and
uruk-hai lose their 1.25. The auto-knockdown ratio drops from 8 to 6,
so a full-speed horse and man contact (ratio exactly 6) floors a man
from any angle.

The MCM slider "Auto-Knockdown Weight Ratio" is stored under a new
name, ChargeAutoKnockdownRatio, so the new default of 6 reaches every
player; a value saved before is reset and must be set again if it was
chosen. The per-culture charge damage and the three extra charge
sliders of the 1.5.x change are not part of this port.

Research: taom-src v1.4.8 MissionCombatMechanicsHelper (horse charge
  knock-back and knock-down)
Not-tested: cavalry into orcs, uruks, trolls and dwarves in game
Save-compat: none; MCM key ChargeAutoKnockdownWeightRatio is now
  ChargeAutoKnockdownRatio
Ported-from: 3f75217dcde84c96974dffa0977f0c3403cec1da

#### feat: v2.0.28 - Custom Battle keeps trolls off banners

`7ff48381`

Custom Battle builds its own banner model,
CustomBattleBannerBearersModel, off a BasicGameStarter (v1.4.8
CustomGame.InitializeGameModels), so TAOM's campaign model never ran
there and nothing kept a troll from carrying or picking up a standard.
TaomCustomBattleBannerBearersModel subclasses the Custom Battle model
and adds only the race gate to CanAgentBecomeBannerBearer and
CanAgentPickUpAnyBanner; bearer counts, tiers and formation rules stay
Custom Battle's own.

SubModule gains RegisterCustomBattleModels, called from OnGameStart
before the campaign branch and the feature-module step. It returns for
a campaign and adds Custom Battle models on a BasicGameStarter only,
after the game type added its own, so the TAOM model is the one the
mission resolves (the last model added wins).

Research: taom-src v1.4.8 CustomBattleBannerBearersModel,
  BasicGameStarter.AddModel, GameModelsManager.GetGameModel; dump
  CustomGame.OnInitialize and InitializeGameModels
Not-tested: a troll formation with banners in Custom Battle
Ported-from: 25995dc91d019fa90038bd849be4c84ba0d55765
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1

#### feat(tools): v2.0.28 - engine bump checks shipped refs and bound bodies

`8c186442`

Two engine-bump tools. check_engine_member_refs.ps1 resolves every engine
member and type a shipped DLL references against the installed engine,
with no assembly loading: the public build resolves all 3,257 on v1.4.8.
engine_body_diff.py diffs the body of every member the API snapshot says
TAOM binds, old decompile against new, reading engine code only from
vanilla modules. The taom-src test gets its ModuleOrder class back, which
reads engine_body_diff.py.

The /engine-bump Phase 1 item 5 and Phase 3 text that calls them waits
for WP103 (D25 rule 4).

Research: check_engine_member_refs.ps1 run on v1.4.8 (0 unresolved for
 the fresh build, the public TAOM.dll and TAOM.Dependencies.dll)
Not-tested: a real body diff (no older decompile on the laptop)
Ported-from: 4f9985852086d5498e0a1c0357f8371a0a521dcf
Ported-from: 4e408e0e07b93b72ab40bb8aa75bf7148bd7722a

#### feat(tools): v2.0.28 - native build match and the native build archive

`db464161`

An RVA from an archived build now carries to the installed one (MATCHED
... (hash), or AMBIGUOUS/UNKNOWN, never "removed"), scored by --oracle
against both builds' engine-method maps. On v1.4.8 the client-to-wEditor
control makes 0 wrong matches. native_archive.py copies each client and
wEditor DLL by SHA-256 with its map and index; on this line it runs by
hand, not from the session-start hook.

The /engine-bump text that calls it waits for WP103 (D25 rule 4).

Research: installed-build negative control run on v1.4.8 (0 wrong, 117 s)
Not-tested: a real old-to-new carry (this line has one engine build)
Ported-from: fc5408508687c9f813123b6b1db006dd6ab0cee7
Ported-from: 09030511ae4f34dab7ead3068b11d725770de554
Ported-from: e1dffb13807448f4b8faf091e54f8c546b58ead6
Ported-from: 2838b25c2f6cd2677425212aca6cee5b8e2226fb

#### feat(tools): v2.0.28 - native decompile and engine-method map

`373d9ce8`

Crash triage prints the faulting thread's registers from a minidump and
ends every site with the command that decompiles it, also for a leaf
function with no unwind entry. native_decompile.py reads a crash site,
an [EngineMethod] call or a string's users as C through headless Ghidra
(desktop). native_engine_methods.py joins the managed ids with the
native registration table (capstone, runs on the laptop: 2,261 methods
on v1.4.8, none one-sided). TAOM_GHIDRA_PROJECTS moves the project and
cache folder off the desktop's E: drive.

Research: engine-method map, triage and decompile CLI run on the
 installed v1.4.8 client DLL
Not-tested: the Ghidra-backed decompile (no Ghidra on the laptop)
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 719074072ab5777bd614cda70afc330f8346f1c1
Ported-from: d51e430287cf0d063bd07f006aea7c1cd1fc67a2
Ported-from: f39b399b49e8d2249ca3933ece419c07823d5be4

#### feat(enlistment): v2.0.28 - no battle promotion in the grace state

`0790c3df`

While the commander was missing (the grace state that waits for him to
return or for the term to lapse), a battle could still promote an
enlisted soldier, although the daily service tick already refuses
promotion in that state. The battle payout now refuses it too.

Research: 1.4.x EnlistmentDailyService.cs:194 already refuses promotion
in CommanderUnavailable; EnlistmentRecord.IsEnlisted includes that state
Not-tested: a battle fought during the grace state, in game
Ported-from: 75e190abe685cba1b403af8847cb4da9d0b944c0

#### feat(enlistment): v2.0.28 - merit counts the hero's own kills only

`84c444f8`

After the player's hero fell in an enlisted battle, the game lets him
take over one of the soldiers still fighting, and that soldier's kills
and positioning were counted toward the hero's merit. The sampler now
counts a kill only when the hero himself lands it, and treats a
controlled soldier as the hero being down.

Research: v1.4.8 Mission.CanTakeControlOfAgent (:6332-6336, needs
!Character.IsHero); SandBoxMissions.cs:757 and :1510 enable it;
CharacterObject.PlayerCharacter (CharacterObject.cs:250);
Agent.Character (Agent.cs:1392)
Not-tested: taking over a soldier after the hero falls, in game
Ported-from: 9304a4cb941d577cd4b2d5810af13529e313ebf6

#### feat(enlistment): v2.0.28 - a joined battle starts no private war

`919a014c`

An enlisted soldier who swore as his own clan took on his lord's wars
only at the oath. If the lord's realm declared a new war later, his
'Attack' in the lord's battle against that faction declared a private
war that ending his service never undid. Now, before he joins such a
battle, the lord's war is taken on first, with the game's default
reason (open quests from that faction are cancelled, not failed), and
it is recorded so that discharge ends it again. A soldier whose clan
serves a kingdom keeps the oath-only rule. A war or peace that another
rule vetoes is no longer reported as done.

Research: v1.4.8 MenuHelper.EncounterAttackConsequence (:265-269) and
BeHostileAction.ApplyEncounterHostileAction (:187-197) declare war for
the player's faction; DeclareWarAction.ApplyByDefault (:62);
QuestHelper.cs:138-151 cancels quests on a war the player did not cause;
MapEventSide.OtherSide/LeaderParty; Clan.PlayerClan
Rejected: the hourly war follow and the crown-war release from the same
1.5.x commit (new behaviour and a new message; plan 128 decision P128-1)
Save-compat: no new field; join-time wars go into the existing
MirroredWars list, which discharge already unwinds
Not-tested: a mid-term war declaration followed by a join, in game
Ported-from: 1559b3261c2087149f7811bebeca8d7d7c7fc309

#### feat(enlistment): v2.0.28 - soldiers fight lords who relieve a raid

`843e411f`

When a lord rode to save a village from the soldier's commander, the
fight happened inside the raid itself, so the soldier sat it out while
his commander could lose and be taken. Now, while a lord with men
still standing defends the raided village, the soldier joins his
commander's side and fights. A raid defended only by militia or a
patrol is still left to the column. When the battle is decided the
raid ends and the soldier returns to normal service.

Research: v1.4.8 StartBattleAction.ApplyInternal (:23-30 joins the
running event, :113-115 keeps it a Raid), EncounterManager.cs:157 (the
lord-defender test), MapEventSide.Parties, MobileParty.IsLordParty,
TroopRoster.TotalHealthyCount; PlayerEncounter.SetupFields (:750, :763)
Not-tested: a relief lord attacking the commander's raid, in game
Ported-from: c04ba3fe58a39c2538dbb5a0708393e63d21b1e9

#### feat(recruitment): v2.0.28 - AI lords skip recruit trips alignment bars

`d0a3dd5f`

AI lords no longer plan recruiting trips to towns and villages where
the Free and Evil recruitment rule would refuse them. When the AI
scores a settlement for recruiting, the volunteer estimate now counts
zero where the alignment rule blocks that lord, using the same rule
and settings as recruiting itself, including "Apply To AI Lords".
Lords still visit those places to buy food and trade.

The postfix arrives as AlignmentRecruitmentModule plus one line in
the feature-module list and keeps the 1.5.x number, Patch114. The
alignment settings are now read through a cached accessor, because the
AI reads them far more often.

Research: v1.4.8 AiVisitSettlementBehavior.cs:406 estimate, :304 call
Not-tested: AI trip choices across the Free and Evil divide
Ported-from: 93684c32cac592270911513816b84a81a51602fd

#### feat(recruitment): v2.0.28 - AI castle recruiting gated on volunteers

`250f77eb`

AI lords now treat a castle as a recruiting stop only when one of its
notables holds a volunteer in the slots vanilla's own estimate counts
(0 to 3). Before, every castle drew lords in: a lord could ride to an
empty castle and leave with nothing, and an empty castle lost
vanilla's normal score for other visits.

One transpiler on the AI visit scoring replaces three patches. Kingdom
lords far from home no longer target friendly foreign castles; lords
outside a kingdom keep them, as in vanilla. An AI lord inside a castle
now recruits each hour through an event listener that calls vanilla's
own recruit method, bound once per campaign, instead of a postfix.

New binding tests fail if an engine update breaks the patch site, the
private recruit method, or the vanilla facts the code relies on.

Research: v1.4.8 AiVisitSettlementBehavior.cs:279, :327, :406-435
Research: v1.4.8 RecruitmentCampaignBehavior.cs:291-303, :414
Not-tested: AI lords skipping empty castles; the hourly castle recruit
Ported-from: f83a63e57e4a03d956ccaf76e0274baf6993e226

#### feat(ui): v2.0.28 - four campaign screens open faster (Shadowfax)

`fc85e8b3`

The clan, inventory and trade, party and quest screens skip work they
repeat while they build. The clan screen builds its parties list once
per open instead of three times. The inventory and trade screens skip
a name sort that the screen's own sort replaces a moment later. The
party screen builds each troop portrait code once per screen instead
of once per row. The quest log builds a finished quest's stages only
when the player selects it. What the screens show is unchanged.

Each change has a switch in the new "Screen Performance" group of the
Battle Load Diagnostics mod options page, on by default and read on
every open; turning one off restores the vanilla build. "Log Screen
Open Times" (off by default) writes one timing line per screen open
to the TAOM debug log, so each switch can be measured off against on.
Each rewrite checks the game's code when a campaign starts and leaves
its screen vanilla, with one log line, if the code is not as expected.

This is the screen speed half of the 1.5.x Shadowfax Screens work; the
instant screen transitions are not part of this change.

Research: taom-src v1.4.8 of the eight vanilla view models patched
Not-tested: every screen in game, and the A/B timing per switch
Save-compat: none; no saved state, five new MCM settings
Ported-from: ea7cce7bc06475aea4e0a4d48a5087d3e1c5ec01
Ported-from: 11aeb8aee7e4976ba22b7d814096b433aa982c83
Ported-from: 33c3bdf1a15732d014a9a8920bdf7e3acea07418
Ported-from: 3f816822837914717ac1d33ce214c0218203ae07
Ported-from: 3db115423b16355e711c00f0c86a4f63102cef3e
Ported-from: c3266287205bc14f049bab5ff693c3888172386f
Ported-from: 22695bd017a35bf6a6fe6e537755d3c36c694142
Ported-from: 579bb93f00f3581996f451ea2353de832c1ba93d
Ported-from: 642f92e3966ee4d33255c9ed2b26d92d26fd8c77
Ported-from: 402280dc23cdbc26054cf76922ae77b94c74ac0a
Ported-from: b747cf398b74914d832462c7b119470df4f7ef9a

#### feat(troopweight): v2.0.28 - trace main-party troop removals

`9ff1c816`

A player lost about 60 troops right after buying elite units from the
faction emissary, and no TAOM code removes a whole party. Until the
cause is found, the TAOM debug log gets one [RosterLoss] line each time
regular troops leave your party, naming the troop, the count and the
code that removed them; nothing in game changes. If you lose troops
without a reason, send the taom_debug log.

Research: taom-src v1.4.8 TroopRoster.AddToCountsAtIndex (TroopRoster.cs:312) and SetElementNumber (:494)
Not-tested: in game, buy Elite Emissary troops and confirm [RosterLoss] DEBUG lines appear only for main-party regulars
Ported-from: 36d4dda14001262cb5660e84c7bae79e13f6883a

#### feat(lost-footing): v2.0.28 - opt-in census of stranded soldiers

`8fcf88d2`

Adds the Lost Footing census, off by default (Battle Tactics, Lost
Footing, "Lost Footing Census"). Switched on, it reads up to 64
agents a frame in field battles, sieges and sally-outs and logs
soldiers, mounts and creatures stranded off the walkable ground or in
water, with a summary per battle. It only reads; it never moves or
changes anything. The 1.5.x key defaults on, so this line uses its
own key; its gate is the mount-despawn allowlist with the co-op
stand-down and ended missions excluded, as on 1.5.x. IStampClock and
StopwatchStampClock are copied from 346f3e093 because this tree lacks
them; nothing else of that commit comes with them.

Research: taom-src v1.4.8 Agent.IsOnLand, GetAIMoveDestination,
GetAgentFlags, Mission.GetWaterLevelAtPosition, Scene.GetWaterLevel,
IsPositionOnADynamicNavMesh, WorldPosition.GetNavMesh, Formation.
GetOrderPositionOfUnit
Not-tested: the census in a live battle
Save-compat: no saved state; one new MCM key, default off
Ported-from: 639df895becd1966fbe1dc15e566b6275372e75e
Ported-from: 346f3e093ccc2c146107a3b7e005c0c06963cd85

#### feat(tools): v2.0.28 - rank the equipment assets a battle preloads

`bee6ad7e`

Adds tools/audit_battle_equipment_memory.py: an offline ranking, from
the installed packs, of the items, meshes, textures and collision
bodies a battle preloads, per troop, culture and side. It reuses the
map audit's pack index, which gains a loose-assets option. Read-only
against the packs; it attributes pack bytes, not process memory, and
each run deletes the previous report in its output folder, so point
it at a scratch folder. Its test file comes from 569f6750d, a later
commit that fixed one comment in it; that commit's troop changes are
not part of this one.

Not-tested: a run against this laptop's cooked TAOM_Map and Armory
Ported-from: 3dac7ab5b03944b0fdfd44b15043941b382d70ec
Ported-from: 569f6750d79e41b5045624ff05c9ef25d529fd4b

#### feat(mission-perf): v2.0.28 - log clip memory against the 12 MiB budget

`3956a222`

Each mission writes an [AnimMem] line every five seconds: how much
on-demand animation clip data the engine holds against its 12 MiB
eviction budget, whether a clip is loading at a sample, and how often
the total fell, a sign of evictions. The probe finds the counter once
per process by a signature scan of TaleWorlds.Native.dll, checks every
address against the module's own section table before reading it,
and turns itself off on any mismatch. It never writes engine memory.
On by default, the same key as the 1.5.x line.

Research: read-only scan of the installed v1.4.8 TaleWorlds.Native.dll
(port report V03): one pattern match, load 0x23AADF, budget float
0xB2BC9C reads 12582912; taom-src v1.4.8 Mission.AfterStart order
Not-tested: the armed line and [AnimMem] samples in a live battle
Save-compat: no saved state; one new MCM key, default on
Ported-from: a9f10688fbbb2e540b36c5fb593dda71811a881a

#### feat(map-perf): v2.0.28 - campaign map frame profiler, off by default

`cccf65a3`

With "Enable Map Profiler" on (Battle Load Diagnostics, Map
Performance; needs a restart) the campaign map's frame is split into
the campaign tick, every per-frame tick-event listener by its owner,
the map screen with TAOM's map views, TAOM's application tick and the
rest, written as [MapProfile] every 5 s and [MapProfileSummary] per
campaign session. Off, nothing is patched. PatchShield keeps
Campaign.Tick and CampaignEvents.Tick off its shield, as on 1.5.x.
The time adapter gains the engine's simplified time mode, which the
profiler's speed class reads.

Research: taom-src v1.4.8 Campaign.RealTick, Campaign.Tick,
GetSimplifiedTimeControlMode, CampaignEvents.Tick, MapState.OnTick,
MapScreen.OnFrameTick, MbEvent<T>.Invoke and InvokeList; two TAOM
map views with OnMapScreenUpdate on this line
Not-tested: Patch101 on a live campaign map
Save-compat: no saved state; one new MCM key, default off
Ported-from: f6bf0e58880c077f4859d2d1b0dc0b24e168fc2f
Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db
Ported-from: 43ca92ee99c064467113e603c19c58a5901b9093

#### feat(perf-runs): v2.0.28 - per-mission rows and A/B compare from logs

`a6d69685`

Adds tools/perf_runs.py: it turns taom_debug logs into one row per
mission (load phases through triage_battle_load.py, frame time from
[MissionPerf], hitches and the camera share from the probe's lines,
GC counts, the [PerfContext] options) and compares two groups of runs,
flagging confounders such as memory pressure. Read-only on the logs.

Not-tested: a run over a real 1.4.8 log with the new lines
Ported-from: 67eff55cf9cace45727b2324a79555d05ec927b2
Ported-from: 0273e2ea48f9bf590eb80b72a3c207778279f40c
Ported-from: c7612577321b057a47639d95cd657c962af26ba8
Ported-from: fdee18f02132a259c6ea52c6c43be5e82f5ca93f
Ported-from: 685c6a1e03f799567d5c5c5e93eae052900f767b

#### feat(mission-perf): v2.0.28 - hitch probe and opt-in tick profiler

`bdfb3329`

Slow battle frames now explain themselves. The hitch probe (on by
default) times each mission frame's parts TAOM can see: the wait for
the agent tick, the mission tick, the camera and render preparation,
scene scripts, spawns and clip loading. Every frame of 250 ms or more
writes a [Hitch] and a [HitchDetail] line, and each mission writes one
[PerfContext] line with the frame limiter, vsync, quality options,
memory load and CLR version. The tick profiler (off by default, needs
a restart) adds per-behaviour attribution. Both apply once at game
start through TryPatchCategory.

v1.4.8 differs from the 1.5.x line in one target: Mission.SpawnAgent
takes two parameters, so the spawn bracket and the spawn attribution
transpiler name those two. PatchShield keeps WaitTickCompletion and
ManagedScriptHolder.TickComponents off its shield, as on 1.5.x.

Research: taom-src v1.4.8 Mission.OnTick, OnPreTick,
WaitTickCompletion, TickAgentsAndTeamsImp, SpawnAgent (two
parameters, Mission.cs:4086), ManagedScriptHolder.TickComponents,
MissionScreen IMissionSystemHandler.UpdateCamera, NativeOptions;
HitchProbeOverheadBenchmarkTests on the laptop: 0.729, 0.760, 0.759 us
per simulated frame (budget 50)
Not-tested: Patch97 and Patch98 in a live battle; the clip-loading
sample against the engine's own loads
Save-compat: no saved state; four new MCM keys, 1.5.x defaults
Ported-from: 6eb2e1b90c2fe42d6cda851434c0ff6ec1fdcd1c
Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db
Ported-from: 43ca92ee99c064467113e603c19c58a5901b9093
Ported-from: 0273e2ea48f9bf590eb80b72a3c207778279f40c
Ported-from: c7612577321b057a47639d95cd657c962af26ba8
Ported-from: fdee18f02132a259c6ea52c6c43be5e82f5ca93f
Ported-from: 685c6a1e03f799567d5c5c5e93eae052900f767b

#### feat(mission-perf): v2.0.28 - [MissionPerf] frame-time heartbeat

`5f4b4228`

Every mission now writes one [MissionPerf] line to the TAOM debug log
every five seconds: frames, fps, average, p95 and max frame time,
agent and formation counts, garbage collections, and the engine's
main and render busy times. The 1.4.8 line had no in-mission frame
record, so a battle that stutters or freezes left no number behind;
the line also stops when the mission tick stops. MCM: Battle Load
Diagnostics, Mission Performance, "Enable Mission Frame-Time
Heartbeat", on, the same key and default as the 1.5.x line.

Research: taom-src v1.4.8 Utilities.GetMainFps and GetRendererFps
(Utilities.cs:447-454), Mission.AllAgents, Agents, Teams,
Team.FormationsIncludingEmpty, Formation.CountOfUnits
Not-tested: the [MissionPerf] line in a live battle
Save-compat: no saved state; one new MCM key, default on
Ported-from: 5dfa8f4cb0a9e86118ed41b0c6bdd3d9a977990b
Ported-from: 67eff55cf9cace45727b2324a79555d05ec927b2
Ported-from: 685c6a1e03f799567d5c5c5e93eae052900f767b

#### feat(stability): v2.0.28 - release the covered map view's GPU targets

`7552b8b3`

In yotthani's measurement on the 1.5.x line, every screen opened over
the campaign map left the map view's render targets in GPU memory
until the next battle or town, with only the official modules too.
TAOM now counts the covers of the map's own scene layer and, every
20th time, releases that view's GPU resources through vanilla's
SceneLayer.ClearRuntimeGPUMemory(false) when the map comes back; that
return shows a short loading screen. No Harmony patch: one handler on
ScreenLayer.OnLayerActiveStateChanged. MCM: TAOM Battle Load
Diagnostics, Map Performance, "Release Map View Memory" (on) and "Map
View Release Interval" (1 to 1000, default 20). Adapted from
yotthani's VanillaTuning (MIT). The saving is not measured on v1.4.8.

Research: taom-src v1.4.8 ScreenLayer.cs:110-129, SceneLayer.cs:145-148,
MapScreen.cs:257, :294, :507, :651, :833-860, ScreenBase.cs:71-145
Not-tested: dedicated GPU memory over 20 covers; the release in game
Save-compat: no saved state; two new MCM keys, defaults on and 20
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
Ported-from: 7eac30153353ae401ef37c0955a29e92bb5d4b25
Ported-from: 8dd76ddb376425da6a167415186f1f4a40781355
Ported-from: 2ac85fbe777a3cdc1513ba76402b8563fcea3770

#### feat(stability): v2.0.28 - skip hidden settlement nameplates

`540bceab`

The campaign map updated every settlement nameplate every frame,
hidden ones included (988 settlements in TAOM_Map v2.0.27). A prefix
on SettlementNameplatesVM.Update now runs the same update over only
the plates that are not hidden and staying hidden; everything else
updates as in vanilla, and any doubt or error runs vanilla whole.
MCM: TAOM Battle Load Diagnostics, Map Performance, "Cull Hidden
Settlement Nameplates", on by default. On v1.4.8 a hideout's range
comes from ISpottable.IsSpotted, so the port reads that and not the
1.5.x line's Settlement.IsVisible. Adapted from yotthani's
VanillaTuning (MIT); the notice is in THIRD-PARTY-LICENSES.txt. Not
measured on v1.4.8 yet; on the 1.5.x line yotthani measured 5.5 ms
down to 2.3 ms per fast-forward map frame.

Research: taom-src v1.4.8 SettlementNameplatesVM.cs:238-248,
SettlementNameplateVM.cs:332-358, :474-532, :744-770; installed IL
call lists re-pinned in NameplateCullBindingTests
Not-tested: the cull in game; the frame-time A/B
Save-compat: no saved state; one new MCM key, default on
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
Ported-from: 7eac30153353ae401ef37c0955a29e92bb5d4b25

#### feat(diagnostics): v2.0.28 - map-load timeline, renumbered Patch89

`5f5cfdae`

The last 9 to 17 s of a v1.4.8 campaign load, from game init to the
map screen, wrote nothing to either log, and a 2026-09-12 run spent
95 s and 2.7 GB there unexplained. The map-load timeline adds a
[MapLoad] heartbeat every 5 seconds from a Campaign.RealTick postfix
(frames, fps, tick time, a party census on emit frames only, campaign
time, loading-window state and the whole game-state stack) and a
numbered lifecycle trace: state pushes and pops, the map state and
screen seams, the first map frame, and each raise and real lower of
the global loading window with its caller chain. A lower is traced
only when the window actually drops: the engine lowers it on every
frame of the main menu and many screens, which on the 1.5.x line
wrote a line per frame before this gate. Four categories, each
applied on its own; the timeline resets on every session launch.

Research: taom-src v1.4.8 Campaign.RealTick, GameStateManager,
LoadingWindow, SceneView, MapState; SandBox.View MapScreen (dump)
Not-tested: in game (heartbeat and trace on a v1.4.8 load; no lower
 lines while the main menu stays open)
Save-compat: the behavior's SyncData is empty
Ported-from: 502f7cde946f767bf2e1ce6f100bef9179e036d8
Ported-from: 8028bf3df1b1651cd1178c2d2f990bdf5fd79edb
Ported-from: e6c03411dc0cca984d6aac7cfd01c64fccaa9d03
Ported-from: bce3c27f42f29ae7a047dbce1230561dc7a1ed76
Ported-from: 15e5475a45c9f601ddce87b66abd503cfa694bc6
Ported-from: a4492fa8900dbfe74c1b974d215b6cc0b1c8fa99

#### feat(texture-advice): v2.0.28 - advise 16 GB players on texture quality

`4e67e433`

Texture Quality is the largest memory lever a player has: on TAOM's
campaign map, highest to lowest freed 2.3 GB in a 2026-09-12
measurement on the maintainer's desktop, live, with no restart, and
nothing told players. At the first main menu TAOM now reads the
installed memory and the Texture Quality option once. On a machine
with about 16 GB or less at the highest setting it shows one popup
that suggests one step lower, then Foliage Quality. It never shows
again on that install. Every launch writes one [TextureAdvice] line
with the memory size and the setting, so a player log says both.

Research: taom-src v1.4.8 NativeOptions.GetConfig (:274),
NativeSelectionOptionData.GetOptionsLimit (TextureQuality has 3
levels, :39; level 2 is High in Native global_strings.xml),
InquiryData, InformationManager.ShowInquiry
Not-tested: the popup in game on a 16 GB machine
Save-compat: no saved state; a marker file in the game's Logs folder

#### feat(load-stamps): v2.0.28 - time module XML, patch groups and handlers

`a0c64e6e`

taom_debug.log now says where a campaign load's time goes. Always on:
one [LoadXml] line per MBObjectManager.LoadXML call (one per XML type)
with its files, XSLTs and time split into the merge and the object
creation, a [LoadXml] summary at every game initialization, a
"[PatchApply] phase=... scope=total" line for each of the four patch
apply phases, and one "[Lifecycle] dispatch=..." line for each
campaign dispatch of a new game, a loaded save and the session start.
With "Enable Load-Time Stamps" on (MCM, Battle Load Diagnostics,
default off) it also writes one [PatchApply] line per patch group,
[LoadPhase] steps of TAOM's game start and game initialization, and a
[Lifecycle] line for every campaign handler of 10 ms or more.

The stamps only observe. PatchShield in this build leaves the seven
patched methods alone (MBObjectManager.LoadXML and CreateMergedXmlFile
and five CampaignEventDispatcher lifecycle methods), so a missing-API
exception there behaves as in the unpatched game. With an older
TAOM.Dependencies.dll the module applies neither patch group and logs
one warning instead. On the 1.4.8 line one player's 12 loads spent
292.9 s in the XML merge window and nothing in taom_debug.log said so.

One transplanted test changes for this order: its TAOM callback under
a plain owner token came from MapLoadDiagnostics, which lands later,
so it uses EnlistmentWaitMenuOptions.Register instead.

Research: taom-src v1.4.8 MBObjectManager, CampaignEventDispatcher,
CampaignEvents, MbEvent`1, MbEvent`2, Campaign
Not-tested: in game (ready, per-type and dispatch lines; the detail
 lines after a restart with the toggle on)
Save-compat: no save data
Ported-from: 346f3e093ccc2c146107a3b7e005c0c06963cd85

#### feat(diagnostics): v2.0.28 - ask Dependencies which targets it shields

`105e678a`

Adds ShieldExclusionCheck, which asks the loaded TAOM.Dependencies.dll
whether PatchShield leaves a list of engine methods alone
(PatchShieldPolicy.IsExcludedTargetMethod). It never throws: a
Dependencies DLL too old to have that method, or one that does not
exclude a target, comes back as a reason naming the gap.

The load-time stamps and the XML merge fast path that follow patch
MBObjectManager and CampaignEventDispatcher methods that must not run
under PatchShield's finalizer: it would swallow a missing-API
exception into an empty XML type or a half-run campaign dispatch and
then unpatch TAOM for the session. The exclusions live in
TAOM.Dependencies, and the public 1.4.8 build already pairs a newer
TAOM.dll with an older TAOM.Dependencies.dll (#371), so the modules
ask before they declare their patch categories.

Not-tested: against a real older TAOM.Dependencies.dll (a probe that
 throws MissingMethodException stands in for it)

#### feat(stability): v2.0.28 - skeleton buffer guard and watch

`a3b12100`

The engine reserves per-frame room for the skeletons it draws in two
pools and never checks the limit; one reservation too many freezes
the battle for good, with no log. The guard rewrites 13 bytes at each
reservation at the first main menu, only after an exact byte match,
so an overflow reuses the pool's first slots (wrong bones for a
frame). The watch logs each mission's peak fill. MCM: TAOM Crash
Report, "Skeleton Buffer Guard" (restart) and "Skeleton Buffer
Watch", both on. Adapted from yotthani's VanillaTuning (MIT).

Research: read-only scan of the installed v1.4.8 TaleWorlds.Native.dll
(14,185,944 bytes): six patterns 1/1/0/1/1/0, global 0xD96D18,
pool-2 call link 0x8849E to 0x898D0
Not-tested: the engine-code write in the game process; the look of a
refused skeleton; MCM readable at the main-menu phase
Save-compat: no saved state; two new MCM keys, default on
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7

#### feat(diagnostics): v2.0.28 - opt-in memory dump on a frozen game

`1c7ef016`

A Task Manager dump is about the game's memory use, 9 to 11 GB in the
2026-11-01 logs, and a frozen player rarely takes one. The new MCM
option "Write Memory Dump On Freeze", off by default, makes TAOM write
one thread-stacks-only minidump of a few MB to the Logs folder when the
game has finished no frame for 80 seconds, the heap is flat and the
engine log shows no recent shader compile. One per game session, for a
maintainer to read the native frame of a freeze from.

Research: MiniDumpWriteDump shape and MINIDUMP_TYPE values from the shipped Bannerlord.ButterLib.dll
Not-tested: writing a dump from inside the frozen game

#### feat(crash-report): v2.0.28 - QA trigger throws from mission AfterStart

`c709fb9d`

Adds a QA-only MCM toggle, "Throw On Next Mission AfterStart", that
throws from a TAOM behaviour's AfterStart on the next mission start:
the built-in repro for the mission-start guard. It resets itself
before it throws and needs Enable Crash Capture on. Off by default.

Research: taom-src v1.4.8 Mission.AfterStart behaviour loop
(:3823-3826) and Continuing (:3835)
Not-tested: the trigger itself (in-game only)
Save-compat: one new MCM key, default off
Ported-from: 649cc30ff2ddcfbc01228e9c8a1ee1e03241ac4a

#### feat(stability): v2.0.28 - BattleLoad closer outlives a wiring throw

`126f3e22`

A throw anywhere in TAOM's mission wiring skipped the BattleLoad
window closer, so the window never closed and the 300 s stall
watchdog wrote a spurious bundle. The wiring now runs in
AddTaomMissionBehaviors inside a try, and the closer is added in the
finally, once per mission. There is no catch: the throw still
reaches the mission-start guard.

Research: taom-src v1.4.8 Mission.AddMissionBehavior (:4615, calls
only OnCreated) and Mission.GetMissionBehavior (:4631)
Not-tested: a real wiring throw in game
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7

#### feat(stability): v2.0.28 - survive mission start throws (Patch103)

`7ca00e10`

A mission whose start calls threw loaded again every frame for ever,
and TAOM.Dependencies' SaveShield swallowed the throw silently, so
players saw an endless loading screen with no report. A transpiler on
Mission.AfterStart now wraps its six start calls: a call that throws
is logged with its type and assembly, shown once on screen and cut
short, and the mission starts. MCM: TAOM Crash Report, "Survive
Mission Start Failures", on. Adapted from yotthani's VanillaTuning
(MIT); the notice is in THIRD-PARTY-LICENSES.txt.

Research: taom-src v1.4.8 Mission.AfterStart (:3799-3836, six calls
once each), MissionState.TickLoading, LoadMission and
FinishMissionLoading; SaveShield MISSION-INIT row
Not-tested: Patch103 in a live mission; the on-screen notice
Save-compat: no saved state; one new MCM key, default on
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7

#### feat(diagnostics): v2.0.28 - honest notice during a battle render wait

`04057819`

A battle held at the render gate now gets a small window over the
loading screen. It says "Preparing the battle: N shaders compiled" only
while compile_shader lines are arriving in the engine log, and "Still
loading: no shader compiles for N s" after 60 s with none. It never reads
the main thread's shader count as progress, because that count freezes
with the thread and once told a player a wedged load was draining.

The window runs on its own thread, exits with the wait, never touches
the engine and never changes the process priority. It is off on a
dedicated server and with VanillaTuning. The four texts are in English in
all 12 language files; WP135 runs the translator.

Research: engine log tail only; native counter stays main-thread
Not-tested: window over borderless and exclusive fullscreen; engine log flush latency

#### feat(tools): v2.0.28 - triage reads [Freeze] compile evidence

`bb3c0282`

The battle-load triage tool now reads the new log lines. In a silent
render wait, compile_shader lines counted during the silence read as a
slow cold compile, and none for 60 s or more points to a wedge and a hang
dump. With no engine log the "cannot separate" wording stays. Only lines
from the load being triaged count. A [ShaderCache] line saying the cache
was dropped at this start adds a note, and --json gains freeze and
shader_cache_rewritten.

Research: C# FreezeLog and ShaderCacheState literals (twin pins)
Not-tested: none (offline tool)

#### feat(mission-perf): v2.0.28 - call-site swap and PE scan helpers

`c671ce2c`

Adds the helpers the mission-start guard and the skeleton buffer
guard build on, copied from the 1.5.x line: the soft-failing
call-site swap (TickProfilerTranspiler, CallSwap), a trimmed
TickProfileLines with only the warnings that swap writes, and the PE
section and byte-pattern helpers (PeSectionTable,
ClipBudgetSignature). RecordingLogger joins the test helpers at its
1.5.x path. Nothing calls them yet; players see no change.

Research: taom-src v1.4.8 MissionBehavior.OnMissionTick and
OnPreDisplayMissionTick, Mission.WaitTickCompletion,
ScriptComponentBehavior.OnTick, TWParallel.For with a dt delegate
Not-tested: nothing runtime; the helpers have no caller yet
Ported-from: 6eb2e1b90c2fe42d6cda851434c0ff6ec1fdcd1c
Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db
Ported-from: a9f10688fbbb2e540b36c5fb593dda71811a881a
Ported-from: 346f3e093ccc2c146107a3b7e005c0c06963cd85

#### feat(diagnostics): v2.0.28 - main-thread heartbeat and freeze sampler

`b136f892`

When the game finishes no frame for 10 seconds, anywhere, the TAOM debug
log now gets [Freeze] lines at 10, 20, 40 and 80 s, then one recovered
line if frames come back. Each line says how many compile_shader lines
reached the engine log since the last frame, so a slow shader compile
and a wedge finally read differently, and carries the main thread's
stack when that is safe.

It logs nothing during the shader pre-compilation walk or a mission exit
(the exit sampler owns that thread), leaves the stack to [MissionStall]
while a mission frame is in flight, and writes one stack-less line at
40 s when the managed heap is moving, as it does during a save or a
load. The MCM switch "Enable Main Thread Freeze Sampler" turns it off.

Research: v1.4.8 Managed.ApplicationTick/ApplicationTickLight -> CoreManaged -> Module.OnApplicationTick -> submodules; f75288ebf ThreadStackCapture
Not-tested: Thread.Suspend on a live main thread; engine log flush latency; ticks while minimized

#### feat(shaders): v2.0.28 - log whether the shader cache was dropped

`9f724579`

Every log now carries one [ShaderCache] line at startup: whether
shader_mapping.bin was written after this process started, its size,
the .sacx count and both stamps. v1.4.8 rewrites that file when it drops
its runtime shader cache on a change of the module set or game build, so
the line answers "was this session cold from the start?" for any later
loading-screen report. A file stat only, no engine call.

Research: D3 disassembly of v1.4.8 RVA 0x1f3ba0 (delete, then header rewrite)
Not-tested: rewrittenThisStart=true after a mod-set change (in game)

#### feat(shaders): v2.0.28 - show shader compile progress during rebuilds

`fdb85cd8`

After a mod set change, a game update or a fresh install, the game
rebuilds its shader cache before the main menu. Every core is busy and
the loading symbol freezes for a minute or more, so the game looks hung.

TAOM now shows a small window over the game while that runs, counting
shaders from the game's own log, remembering the last full total for
the next time, never taking the focus, and closing at the main menu.
While it is up the game runs at below-normal priority so the rest of
the computer stays usable; the priority is restored afterwards.

It stays off on a dedicated server and when yotthani's VanillaTuning is
loaded, which shows the same notice. The idea is from VanillaTuning;
the code is TAOM's own. The 1.5.x cache keeper is not on this line
(D11), so every change of the active module set pays this rebuild.

The three new texts are in English in all 12 language files; WP135 runs
the translator.

Research: v1.4.8 Module.cs:262/:267 order; native compile_shader format (D3)
Not-tested: the window, thread and priority in game (#752 check owed)
Ported-from: 82645b53fbae055e139fc7a8b96eda5ab3e020e0

#### feat(tools): v2.0.28 - triage compares a stall with the session's rate

`fca93680`

When the same log holds an earlier render wait whose 1 Hz stream went
silent and came back with a lower count, the report now prints the
stalled load's silence against that measured rate. The line is labelled
a heuristic and is decoration only: it never changes the verdict or the
exit code. The --json payload carries it as session_render_rate.

Research: player logs of 2026-11-01 (E4: 1569 to 0 across 69 s; 806 then 206 s silent)
Not-tested: none

#### feat(wotr): v2.0.28 - log the phase days and their source on load

`3d3a2da3`

The session-launch log line now names the phase days the War of the
Ring actually uses and where they come from: the mod options, Test
Mode or war_of_the_ring.json. The "Loaded War of the Ring config" line
prints the JSON values only, which hid an options file that still held
day 2 and day 14.

Not-tested: the line in a live load (in-game check 9)

#### feat(tools): v2.0.28 - the Armory reference audit (#599)

`c6fcc6a2`

tools/audit_armory_refs.py runs the Armory checks that each had to be
remembered separately as one command and writes a committed report,
so the next run's git diff is the change log: mesh and collision-body
refs over the whole Armory ModuleData plus this repo's, catalogue
drift flagged by what the live XML names, every broken item joined to
the troops carrying it, dead Item ids, and the two scaffolders'
--verify. Exit 1 on a missing body or mesh, a dead id or a referenced
delete; generator drift is a warning.

The 1.5.x line wrote it after an Armory art drop renamed the elven bow
bodies and every elf start hung on its first tournament (#599, the
#352 class). Its package list comes from
validate_mesh_refs.tpac_paths_for_modules, so it reads the tree the
engine loads. The committed report and catalogue are not copied: both
describe the 1.5.x Armory, and the report is regenerated against the
public 1.4.8 Armory later in this change. The feature doc describes
the whole change; the cooked-install guard and the validator passes
land in the next two commits.

Research: none bound; the tools it imports are byte-identical to the
  versions it was written against
Ported-from: 05bd0a7301663369bcccbc3249e34de7229d2b37
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 332f2ac71414ea39d252ee5e3bea9886576f5071

#### feat(tools): v2.0.28 - diff two save censuses from a taom_debug log

`abcdfe30`

Adds tools/diff_save_census.py for the save-growth investigation: given
a taom_debug log it compares two [SaveLoad] phase=SaveCensus blocks and
prints the per-type object and container changes, largest first. It is
read-only and needs no game install.

Not-tested: on a real reload-session log (none exists until the census
ships to a build).

#### feat(preload): v2.0.28 - Patch90 guards missing-body load hangs (#601)

`2da06b50`

On v1.4.8 PreloadHelper.WaitForMeshesToBeLoaded is a do/while with no
exit. Every pass counts each registered collision-body name that
PhysicsShape.GetFromResource(name, true) returns null for, and null
means both "not loaded yet" and "no such body". One body_name that no
loaded tpac ships freezes the game on the first frame after the
loading screen drops, with no error and no crash. Two Armory typos did
it to sieges on this line (#352).

Patch90 repairs the precondition instead of replacing the method. The
prefix receives the private name set by Harmony injection, polls only
the body names for a bounded 5 s through a pure service, removes what
still does not resolve, logs each drop at ERROR as [PreloadGuard] with
the repair command, and returns true so vanilla's loop runs against a
set that can now empty. A healthy load pays one pass of lookups; a
broken one waits 5 s once and loads with that item carrying no
collision shape.

BattleLoadPhaseBehavior logs BattlePlayable about eleven frames before
this wait runs, so until now such a hang left BattlePlayable as the
last [BattleLoad] line and no watchdog line. It is not the 2026-11-01
Harad freeze, which stalls before BattlePlayable.

Hand-wired in IoC.cs and in OnSubModuleLoad before Patch62, through
TryPatchCategory in its own try/catch (the feature has no module on
1.5.x either). The co-op veto registry lists the prefix as
ReviewedSafe: it never returns false. Docs: the feature doc, the
registry section, the 1.5.x review RCA and its two harmony-il lessons.

Research: taom-src v1.4.8 PreloadHelper (_uniqueDynamicPhysicsShapeName
  at :14, WaitForMeshesToBeLoaded at :58-75),
  PhysicsShape.GetFromResource(string, bool) at :10; the six callers in
  the v1.4.8 module dump
Not-tested: the in-game probe (a bogus body_name loads after about 5 s
  with one [PreloadGuard] line)
Save-compat: none, no campaign state touched
Ported-from: fb8d731451880c84969fa455e9d74873f9a65c6b
Ported-from: ef6afeb6729b50756c2b7352603c9870a91e5fda
Ported-from: b23a27ef45b3dd060c9729e63a26f65388e12a6f
Ported-from: 1571c2f915f40d79a02d12013ad647009aed6473

#### feat(diagnostics): v2.0.28 - census of save types after a reload

`8d5756dd`

After any load that is not the first in a session, the next save writes
a count of every saved type to taom_debug, so a log from a reload
session shows what grows. taom.arm_save_census asks for the same count
on the next save (console, cheat mode). It changes nothing a save holds.

Research: taom-src v1.4.8 MBObjectBase :15, :25, :79-82, MBGUID :17
Not-tested: the census pass time on a 2 M object save

#### feat(diagnostics): v2.0.28 - per-behavior SyncData sizes at save

`90a09dc0`

At each save taom_debug now writes one line summing every campaign
behavior's saved data, and names any behavior whose data is very large
or slow to collect. Nothing changes in play.

Research: taom-src v1.4.8 CampaignBehaviorDataStore :8-79, Game.SaveAux
:199-205
Not-tested: in game

#### feat(diagnostics): v2.0.28 - log a save's object and container counts

`319d1880`

taom_debug now logs how many objects and containers each save writes
and each load reads, and how long the save's object walk took. Nothing
changes in play; the counts let one log show whether a save grew across
a reload, without reading the engine's own log beside it.

Research: taom-src v1.4.8 SaveContext :75-83, :175, :225, :328-346 and
LoadContext :11-15, :54
Not-tested: the counts against the rgl SAVE/LOAD STATISTICS in game

#### feat(diagnostics): v2.0.28 - saves and loads name the process context

`73979409`

Save and load lines in taom_debug now say how many saved games this
session has loaded, whether a load started inside a running game, the
process memory at the start and end of every save, and whether the
campaign from before the last load has been freed. The game's start
logs which .NET runtime and GC mode it runs on. Nothing changes in
play: these lines exist to find out why saves grow after an in-game
reload.

Research: taom-src v1.4.8 Game (Current :77, Destroy :246-263,
OnSaveCompleted :216-224)
Not-tested: the new tokens in a live save and load

### Fixes

#### fix(combat): v2.0.28 - every creature rider dismounts before death

`57ba233b`

A v2.0.29.5 player on Bannerlord 1.4.8 crashed mid-battle on
2026-11-07. A warg bite killed its target and the game faulted inside
the engine's native Agent.Die; the battle had 122 wargs and the
player's war rams. The fault was caught and logged, the battle kept
running with a half-killed agent, the parallel agent tick faulted 24
more times, and leaving the battle crashed the game.

Patch47 already routed the spider around that native fault by
dismounting the rider just before death, but it checked for spiders
only. It now covers every mount whose Monster is not one of vanilla's
horse, horse_2, camel or mule: spider, warg, fell warg, war ram, both
elks, moose, elephant, mumakil and chariot. A rider seated on one of
them dies the on-foot death, and a dying creature mount drops its
rider first. Vanilla mounts are unchanged.

The private Agent.SetMountAgent lookup the patch depends on is now
pinned by ReflectionSiteBindingTests and the API snapshot, typed to
its overload, and prints one red line if an engine update removes it.

The /new-creature-mount skill no longer tells authors to extend the
predicate, which after this change would have switched the guard off
for a new creature. Docs and comments that described Patch47 as
spider-only, or exempted the war ram, are corrected.

Known limitation: the crash did not log the victim or a native offset,
so a war ram or warg fault is inferred, not proven. The review's
disassembly of the 1.4.8 native DLL confirms the dismount skips the
engine's mounted death branches for any mount. Patch48, the guard for
a non-lethal dismount hit, stays spider-only pending a decision.

Not-tested: the patch in a live battle; owed with Blow Diagnostics on.
Research: Agent.Die, Agent.SetMountAgent, native set_mount_agent/die
Rejected: a list of TAOM creature ids; new creatures would go unguarded
Issue: none filed; asked 2026-11-08, no answer before the push
RCA: docs/reviews/rca-creature-mount-death-dismount-2026-11-08.md

#### fix(tools): v2.0.28 - Gondor generator writes spears the Armory has

`c8753c5e`

A re-run of generate_gondor_troops.py wrote three spear ids the public
Armory no longer defines, so Dol Amroth knights and the upper Pinnath
Gelin spearmen would have come out with an empty weapon slot. The
generator now writes wm_gondor_spear_b_swan and wm_gondor_spear_b_pg,
the ids the shipped troops_gondor.xml already uses. The validator's
GENERATOR_RETIRED_ITEM_REF warning is gone and the two generator gate
tests that measured it on the public build pass.

Research: LOTRLOME_Armory v2.0.29.5 LOTRAOM_weapons.xml defines both
Not-tested: the generator was not re-run (troop data is unchanged)
Ported-from: 998f054c05edcd7537e7699a2e39d726719a2767

#### fix(nazgul): v2.0.28 - three lesser Nazgul get one kit each

`9a315f0e`

The Tainted, the Shadow of the Northmen and the Shadow of Umbar were
defined twice, so each new campaign dressed each of them in a Nazgul
kit or a generic Mordor lord kit at random. They now always wear the
Nazgul kit. Their race (uruk) and age (20) do not change: those values
moved from the second definition into the one that stays. They stay
available in Custom Battle.

A new test fails if characters/lords.xml defines them again.

Research: taom-src v1.4.8 MBObjectManager.MergeElements (:820-875)
Research: taom-src v1.4.8 Hero.SetInitialValuesFromCharacter (:2237)
Save-compat: a saved campaign keeps the kit these lords already wear
Not-tested: the three lords' kit and face in a new campaign
Ported-from: 9804f67b9deba24813da1f2ad7ef2d5f9ad6fa14

#### fix(lords): v2.0.28 - generated lords stop drawing Calradian kits

`82d4e25b`

Lords the game creates during a campaign now dress from TAOM's lord
templates. A Dunland lord who came of age could pick among 26 vanilla
Empire battle kits and one TAOM kit; Rohan, Harad, Easterling, Dale
and Khand lords were alike. A new stylesheet takes the lord and ruler
template flags off vanilla's rosters for those six cultures. Noble
teens of those cultures now wear the TAOM teen kit too; children keep
their usual clothes.

Dale's template now uses Dale's mounted-noble armour instead of
vanilla Sturgian gear.

A new test fails if a pool the game asks for goes empty, or if another
roster file is registered after the stylesheet.

Research: taom-src v1.4.8 DefaultEquipmentSelectionModel (:57-89)
Research: taom-src v1.4.8 MBObjectManager.CreateMergedXmlFile
Save-compat: lords already generated keep the gear they have
Not-tested: a generated lord's kit in a live campaign
Ported-from: bb68eeca3aeee49ba95ea4956f2b3941820a3f93
Ported-from: db805427b89cc354abb7206f926f91eab4a12ad8

#### fix(balance): v2.0.28 - Lindon troops weigh like Rivendell's

`a697ba33`

Lindon's troops now take party space the way Rivendell's do. The
weights were lost when Lindon became its own culture (cc1713eb3): its
troop tree, a clone of Rivendell's, never reached troop_weights.xml.

- Lindon's 22 elite troops take the same party space as their
  Rivendell twins: the 15 at level 41 and above count as three
  troops each, the 7 from level 26 to 36 as two. Before, every
  Lindon troop counted as one. Recruits, infantry, militia and
  mercenaries stay at one, as Rivendell's do.

Saved campaigns pick this up on load. A Lindon AI party that already
holds too many elites sheds the surplus the next time it upgrades
troops; the player's party is never trimmed and shows its weighted
size on the party screen.

A new test fails if any Lindon troop stops weighing the same as its
Rivendell twin. The survival-bonus half of the source commit is not
in this change.

Research: 30 lindon_ troops checked on the merged tree, twins by level
Not-tested: party screen weights in a live campaign
Ported-from: 0e75ec50a34e33fd7f4a6b4c61a3cd15e1330c3d

#### fix(hooks): v2.0.28 - repo-aware gates read an exported GIT_DIR

`b7bbcef7`

The commit gates judge the repository a commit records, and read a
GIT_DIR or --git-dir on the call itself. One set in an earlier
statement was not read: `export GIT_DIR=<main>/.git; git commit` from
a worktree, or PowerShell `$env:GIT_DIR = '<main>/.git'`, was judged
in the worktree, so a DLL or ModuleData file staged in main went
unchecked (integrated review 3).

The shared reader (_shellwords.py git_invocations) now carries GIT_DIR
and GIT_WORK_TREE as the shell would: export NAME=value, export of a
name assigned before, unset and export -n, an assignment to a variable
already exported, PowerShell $env: assignment, $null and Remove-Item
Env:. A subshell's export ends at its close, a bash -c script
inherits, and a GIT_DIR= prefix or --git-dir on the call still wins.
A plain assignment is not exported and moves nothing. A value only
known at run time ($HOME, Join-Path) is not guessed: the call is
judged where it runs, as before. declare -x and set -a are not read.

New rows: GitRepoTests in tools/tests/test_shellwords.py, and
tools/test_hooks.sh 7p, where check-native-dll-crt now denies a DLL
staged in main reached by an export or $env:GIT_DIR from the worktree.

#### fix(hooks): v2.0.28 - confirm gates read an escaped space in -C

`3d59e247`

block-dangerous-git.sh and block-broad-git-add.sh skip git's global
options to find the subcommand. A value spelled with a backslash-escaped
space (`git -C a\ b reset --hard`, `git -c x.y=a\ b add -A`) stopped
the skip at the space, no subcommand matched, and the gate allowed
without asking. A backslash now takes the character after it as part
of the value, as quoted spans already did, so these ask for
confirmation like their quoted twins (integrated review 3).

New rows in tools/test_hooks.sh 7r: -C, -c, --git-dir and --work-tree
with escaped spaces ask, a Windows path with backslashes still asks,
and `git -C a\ b status` and `git -C a\ b add a.txt` still pass.

#### fix(hooks): v2.0.28 - commit gates see git in a variable or split lines

`3cb1df79`

Three gaps let a real commit skip the commit gates, found by integrated
review 3.

- Commit plumbing in front of a commit made through a variable
  (`git -C . commit-graph write; g=git; $g commit -m x`) passed
  check-claude-files-tracked, check-moduledata-validation,
  check-doc-config-drift and check-native-dll-crt as plumbing only. The
  is-commit reader now gates any text holding the word commit on its
  own, with or without git beside it; commit-tree and commit-graph
  (`| tee git-commit-graph.log` included) still pass.
- `git \<newline> commit` and `git  commit` (two blanks) skipped all
  five commit gates, check-commit-subject-version included. The raw
  matcher is now one helper, taom_names_commit in _pybin.sh, which
  asks again with blanks of any length when the old substrings miss,
  and the shared reader drops a backslash-newline as bash does, so the
  subject gate parses the message behind it.

New rows: tools/test_hooks.sh 7q (all four state gates and the subject
gate) and IsCommitTests plus a line-continuation test in
tools/tests/test_shellwords.py. No gate is weaker than before: every
earlier 7q row and IsCommitTests case still holds.

#### fix(skills): v2.0.28 - context-budget scan finds memory from a worktree

`4f6e08ac`

/context-budget run from a sibling worktree measured no MEMORY.md, or
the wrong one. A session in a sibling worktree loads the main
checkout's memory, so the scanner now builds the project slug from the
main checkout (git's common dir), not the worktree it runs in. Its
fallback search ends at the checkout's own name, so another project
whose folder holds that name (TAOM-old, TAOM-Online) is not read.

Section 7s of tools/test_hooks.sh pins both: a sibling worktree reads
the main checkout's memory, and the fallback skips a project named
after the checkout plus a suffix.

The 1.5.x commits also taught the memory link check to follow an
archive. That check (ADR-011's dead-link and orphan pass) is not on
this line, so there was nothing to apply it to; its test rows are
left out too.

Not-tested: none (offline scanner)
Ported-from: 8afe46d3926dbdf108084cf4e2bec6aaa826cc31
Ported-from: 000bcf1d152f69ea49149029b6258c41f52f89e5
Ported-from: 0385cc0418719b2d000e19632c20e7b2a44b85da

#### fix(banners): v2.0.28 - Lindon and Blue Craig troops raise banners

`6f1309ce`

Formations of Lindon's and Blue Craig's own troops carried no banner
bearer unless a lord with a banner led them: the banner config had no
row for either culture, while both cultures' infantry march in TAOM's
party templates. Lindon now raises the elves' archers' flag
(archers_flag_t1) and Blue Craig the orcs' standard
(standard_of_fury_t1), like their kin.

A new test reads the shipped troop and party template data and fails
when the culture of any infantry troop a party template fields has no
banner row, so a new culture cannot miss one. On 1.4.x data it found
exactly these two.

Not-tested: in game, a Lindon and a Blue Craig formation without a lord
Ported-from: 21094307642c6802f4b1733592169a34787e9ef7

#### fix(tools): v2.0.28 - a scissors lookalike no longer hides a message

`51e48ac5`

The commit-msg checker stopped reading at any line shaped like git's
scissors, so `git commit -m` with such a line kept an AI attribution
line below it in the commit, unchecked. git always follows its own
scissors with a comment line in the same character, on every locale and
core.commentChar, so the cut now requires that line. A real `git commit
-v` file, with `#` and with `;`, still cuts.

A lookalike followed by a line opening with its own first character
still cuts; that takes two deliberately typed lines.

#### fix(hooks): v2.0.28 - commit gates keep eval commits and git-dir aims

`f901bf2c`

Two gaps in the commit gates, both narrower than the base at edcfddc63.

is_commit answered "plumbing only" for a command holding an
option-prefixed plumbing call and a commit behind a runner it does not
peel, such as `git -C . commit-graph write; eval "git commit -m x"`.
check-claude-files-tracked, check-moduledata-validation and
check-doc-config-drift then skipped it. It now answers "no" only when
the text also lacks `git` then `commit` on one segment, heredoc bodies
included. `git commit-tree T -m "after git commit"` is gated as a
result, which over-blocks on the safe side.

taom_git_repo_dir judged a commit aimed through --git-dir or GIT_DIR by
the cwd, so check-native-dll-crt allowed a DLL staged in main from a
worktree. A commit records the index in the git directory, so the reader
now takes the folder holding that .git, or for a linked worktree's git
directory the work tree named beside it. A --work-tree alone moves
nothing. Every gate reading git-repo gets this.

Not read: `export GIT_DIR=...` and PowerShell `$env:GIT_DIR`, both set
in an earlier statement.

#### fix(tools): v2.0.28 - verify_mount_assets names the real missing cause

`38e29279`

When a usage set's movement rows all bind clips but none was checked,
because no live tpac holds them or the rest are exempt, the tool blamed
the usage set for having no row that binds a clip. On a machine with no
loose Assets/ that was false for every creature.

It now says how many bound clips went unchecked and why. The old text
stays for a set that binds no movement row. It still counts as a FAIL.

#### fix(tools): v2.0.28 - check_commit_msg stops at git's scissors line

`b3c79bf9`

git commit -v hands commit-msg the staged diff below the scissors line.
Those lines do not start with the comment character, so a staged doc
that quotes a closing list or an attribution line refused a commit
whose message was fine, and the error blamed the message.

check() now cuts the message at that line, whatever core.commentChar
is. The same text above the line is still refused.

#### fix(hooks): v2.0.28 - config-protection reads a doubled slash in a path

`41f95199`

docs//adrs/x.md names the same file as docs/adrs/x.md on Windows, but
the ADR guard's pattern missed it, and a doubled slash can come from an
accidental path join. Separators are now collapsed before the match,
with 7g rows for both separator spellings.

The comment claimed 7g holds every spelling. It now says which it does
not: a trailing dot or space and an NTFS stream suffix still pass, as
those take deliberate evasion and this gate targets accidents.

#### fix(hooks): v2.0.28 - confirm gates read quoted global option values

`c8322a22`

block-dangerous-git.sh and block-broad-git-add.sh took a git global
option's value as [^[:space:]]+, so git -C 'E:/a b' reset --hard and
git -c x.y='a b' add -A stopped at the space, matched no verb and
allowed with no prompt.

A value is now a run of unquoted characters and quoted spans. A value
with a lone quote still falls to the old bare form, so nothing the old
regex stripped is lost. test_hooks.sh 7r covers both gates.

#### fix(hooks): v2.0.28 - a mention of git commit- no longer skips a gate

`4fe468be`

All five commit gates exited on a raw *"git commit-"* test, so a real
commit skipped them when its message named commit-tree or an earlier
call ran git commit-graph.

The four state gates now ask _pybin.sh taom_commit_plumbing_only, which
runs the new _shellwords.py is-commit only when the text holds those
words. It says plumbing only when it read the whole command and found a
plumbing call and no commit; bad quoting, an oversized text, a parse
fault and a missing Python all stay gated. The raw test for
"git commit" before it is unchanged, so sudo and eval commits stay
gated. The subject gate lost its arm, since its parser already allows a
command holding no commit call.

Side effect: grep "git commit-" now gates the way grep "git commit"
always has. git -C . commit-tree no longer gates, on the three state
gates that used to take it for a commit.

Rejected: is-commit deciding alone, in place of the raw test. It would
stop gating sudo and eval commits, which git_invocations cannot read.

#### fix(hooks): v2.0.28 - native DLL gate judges the commit's repository

`f76b5fac`

check-native-dll-crt.sh kept cd "$CLAUDE_PROJECT_DIR" when its sibling
gates moved to taom_git_repo_dir. A session started in the main checkout
that committed in a worktree never saw a DLL staged there, and a DLL
staged in main denied a worktree commit that does not hold it.
.githooks/pre-commit has no native check, so nothing backed it up.

It now takes the repository from taom_git_repo_dir, denies a command
that commits in two repositories, and runs pe_inspect only in a work
tree of this project. test_hooks.sh 7b lists it and 7p covers each way
of reaching the worktree, both directions and a foreign repository.

#### fix(harmony): v2.0.28 - manual patch failures join the game-init notice

`3d81168c`

A manual patch that threw was logged and then forgotten. TryPatch
returned false and nothing kept the label, and SubModule drained the
applier's failure list before ApplyAll ran, so the player saw no
notice for a patch that was off for the session.

TryPatch now takes the failure list as a parameter, so the
EveryManualPatch_RunsInsideTheGuard split on TryPatch(" still holds.
ApplyAll returns the labels, PatchCategoryApplier.RecordManualFailures
adds them, and the one game-init report moved after ApplyAll, before
the manual_patches stamp. A missing target stays a warning outside the
notice. No new string: the labels fill the existing GROUPS variable.

The GitHub issue for this is owed by the maintainer. The
ManualPatchApplicator.cs line cited for GetCaptainTooltip moved to :55
in reflection-sites.md and its DataRow.

Rejected: A second report after ApplyAll. It protects the category
report from a throw at IoC.Resolve<IModLogger> in ApplyAll, but the
same singleton is resolved earlier in that method and would throw
there first, so it adds a call for no reachable case.
Not-tested: a real harmony.Patch failure in game; the bracketed labels
in a rendered chat line.
Research: MBTextManager.ProcessTextToString, MPChatVM (v1.4.8)

#### fix(tools): v2.0.28 - war ram parity section fits the 1.4.x ram

`a6872417`

Step G4 ported section G with its 1.5.x shape, so --strict demanded
as_war_ram, as_war_ram_map, as_war_ram_town_and_village and a typed
act_war_ram_butt, none of which exist on this line by design. The
mount-parity gate exited 1 for every creature path while the fixtures,
which held the same shape, stayed green.

Section G now asks for what this line ships: base_monster="horse",
monster_usage absent or "horse", and action_set="as_horse". The 1.5.x
set name is itself a gap. The data_gates note said the report always
exits 0; under --strict it does not.

After the fix --strict exits 0 on the installed Armory and section G
reads clean. The exit code on the v2.0.26 Armory is recorded only, not
an Armory verdict.

Research: docs/features/war-ram.md, WarRamConfig.cs, the installed
lotr_monster_war_ram.xml
Not-tested: none (offline tool)

#### fix(tools): v2.0.28 - mount asset gate matches exact clip names

`f403c639`

verify_mount_assets did a byte scan, so a name like x_trot could hide
inside x_trot_strafe; an empty usage set passed; and --game never worked.
It now reads each tpac's own list and matches exact names.

Adapted: the pin's dire wolf entry in CREATURES is left out, as the wolf
is not on this line. The generic untagged_ok stays and its test uses a
synthetic config.

verify_mount_assets elephant exits 1 on this laptop's v2.0.26 Armory:
RESULT FAIL on skeleton, usage and phantom, with phantom bindings named.
That verdict is about this Armory's meshes and is void under D20 and D25,
so it is a report and no data was edited.

Research: none, offline tool
Not-tested: none (offline tool)
Ported-from: 53cf195ac7e59854fb4a4fd57657a058be808101

#### fix(tools): v2.0.28 - mount parity gains a war ram section that can fail

`af356117`

audit_mount_parity gains a war ram section that returns its gaps, and
--strict exits 1 on a gap instead of only printing it. A missing war ram
file is a gap, not a crash.

completion-workflow.md stays as it is: the lesson its hunk links is not
in docs/reviews/lessons/misc.md on this line.

Research: none, offline tool
Not-tested: none (offline tool)
Ported-from: 1011a4ef6786ffee68d2f716a8e206145c6326ac
Ported-from: b6dc611dd014d6e3b5bddb899e23eccabe8dbb99

#### fix(tools): v2.0.28 - the translation rebuild writes only on --apply

`8b7baab7`

rebuild_translation_files.py wrote on a plain run. A run is now a dry run
unless it carries --apply, and it must name each module it may write with
--module; with none it exits 2 and writes nothing. The Armory and
TAOM_Map writes are backed up first, and only when their bytes change.
The tool reads the shared source table and key_owners, and writes bytes,
so a CRLF file no longer gains a second CR. The translator's seeded row
takes the file's usual indent and terminator.

Adapted: 1.4.x has no tools/name_tags.py (WP135 Part D is optional and
has not landed), so the pin's tool loses its grammar tag calls and
--allow-live-write, and the pin's test file loses its tag cases. The
backup suffix keeps the pin's name. The shared-key test names its own
files, because taom_module_strings.xml and global_strings.xml share no
key on this line. The guide's rebuild commands now carry the new flags.

Neither tool ran for real here: no --apply, no translation, no paid call.

Research: none, offline tools
Not-tested: none (offline tool)
Ported-from: 44d4dba4697207fe0e33fd4a83c6b9595d0b933d
Ported-from: 570fa0e8526400a3bbc51538fe1d630e88ebe0f3
Ported-from: 0385cc0418719b2d000e19632c20e7b2a44b85da
Ported-from: 931d59084b4e5b571628d1baf32dd05680b5d0d4

#### fix(tools): v2.0.28 - village writer checks scenes, ladder html opt-in

`cd1a7f55`

add_map_villages refuses a scene name with no scene.xscene, in --check
and before --apply writes, and it reads the live row's scene as well as
the table's. rebalance_ranged_ladders no longer rewrites the tracked
ranged-troops.html on its read-only default run; it writes it on --apply
or with --docs-html.

add_map_villages --check on this laptop's install exits 1 with 210
findings across 15 table ids: missing rows, entities and loc rows, none
of them a dead scene. No data edit; the tool ran without --apply.

Research: none, offline tools
Not-tested: none (offline tool)
Ported-from: c30c4e618e0d906569fa2f3a46247dfda2d2d40f
Ported-from: ad8f13d00023e0c24d09002fd7d5570d31f9aff2
Ported-from: b6dc611dd014d6e3b5bddb899e23eccabe8dbb99

#### fix(tools): v2.0.28 - map tools stop writing factions.json

`5d6268b5`

factions.json is maintained by hand, and the map tools' tables disagree
with it. assemble_faction_map and process_faction_map no longer write it,
and make_new_factions_playable.py, whose only job was to write it, is
gone. REGION_META left with them, so border_match no longer imports it.

FactionMapDataTests pins Blue Craig's and Lindon's game_faction, which
already read as pinned on this line.

Research: none, offline tools
Not-tested: none (offline tool)
Ported-from: b7fefdae60a1b8debff14bce8877b480f32572e1
Ported-from: b6dc611dd014d6e3b5bddb899e23eccabe8dbb99
Ported-from: f9a8bdebc7a2996c3ffca9533d75ff5518a12b27

#### fix(tools): v2.0.28 - secret-nuget-cleartext reads either order

`2d8d2454`

The NuGet secret rule now reads a ClearTextPassword whichever order its
key and value attributes come in, and a value that holds a newline. A
new rule only; no existing rule changes.

Research: none, offline tool
Not-tested: none (offline tool)
Ported-from: 1011a4ef6786ffee68d2f716a8e206145c6326ac
Ported-from: 285c5a1e8631fe30ce2bc69c42ca7d96f57cb571
Ported-from: 0385cc0418719b2d000e19632c20e7b2a44b85da
Ported-from: 931d59084b4e5b571628d1baf32dd05680b5d0d4
Ported-from: ff21145567ce5c0c6110dfe8c87320d510456018

#### fix(tools): v2.0.28 - gate holes from the 1.5.x review

`3eae5ba5`

The gate holes that hold on this line:

- commit-msg reads the version from the index only, so an unstaged edit
  of SubModule.xml no longer changes the version it wants.
- data_gates selects its gates for a path with a non-ASCII character.
- ServiceSignatureGateTests scans generic services and adapters. It
  finds no new violation on 1.4.x.
- TROOP_ID_REMOVED reads a single-quoted or spaced id.
- The dotnet lint rejects a named ModuleId and a mention in a comment.
- The doc graph ratchet refuses an empty corpus.

Research: none, offline tools and a reflection gate
Not-tested: none (offline tools)
Ported-from: ff21145567ce5c0c6110dfe8c87320d510456018

#### fix(hooks): v2.0.28 - raw fallback refuses again, adrs path normalised

`ca5049e4`

block-no-verify's raw fallback takes the net end state of the 1.5.x
fixes. The raw path runs only past the reader's limit, after a reader
crash or a 20 s overrun, so over-blocking is the safe direction: a
message or a later command that names the long flag refuses too.
config-protection now normalises docs/./adrs and docs/x/../adrs.

The context-budget scanner half of these commits is NOT ported, so the
plan's subject ("scan finds its memory") is not used. Applied to
.claude/skills/context-budget/scan.sh it conflicted, so the plan's
fallback ran: the scanner is restored and its section 8 test rows are
left out of tools/test_hooks.sh.

Research: none, offline hook
Not-tested: none (offline hook)
Ported-from: 8afe46d3926dbdf108084cf4e2bec6aaa826cc31
Ported-from: 000bcf1d152f69ea49149029b6258c41f52f89e5
Ported-from: 0385cc0418719b2d000e19632c20e7b2a44b85da
Ported-from: 931d59084b4e5b571628d1baf32dd05680b5d0d4
Ported-from: ff21145567ce5c0c6110dfe8c87320d510456018

#### fix(tools): v2.0.28 - validator reuses its file reads and ref walk

`b02dd67d`

The validator's file cache held 64 entries while a sweep reads far more
files, so each entry left before its next read. The cache now has no
bound. The two collision passes also walked the Armory's XML and this
repo's twice; they now share one walk per run.

Timed on this laptop, two runs each, on the v2.0.26 Armory: the old code
took 25.9 s and 21.0 s, the new code 14.2 s and 12.2 s. Output was byte
identical. Per-code counts equal the baseline (void as data under D20).

The MCP server's find_references outlives an edit, so it reads each file
fresh through _read_fresh instead of the larger cache.

Adapted: 1.4.x has no skill_template_mismatch_issues or
armour_class_table_issues, so the new test does not mock them.

Research: none, offline tool
Not-tested: none (offline tool)
Ported-from: 5f87810edf77ff8a55329dc7f12722be33550d14

#### fix(tools): v2.0.28 - drift gate reads .ai and .agents for dotnet

`624568cf`

Plan F3 wants DOTNET_FLAG_DIRS equal to the DIRS of
test_dotnet_commands_flagged.py. The port copied the six entries from
1.5.x, so an unflagged dotnet command in .ai or a .agents skill never
reached check_unflagged_dotnet. The tuple gains both, and a test now
holds the two lists equal.

A scanned directory also has to start the linter, or the check never
runs at commit, so .githooks/pre-commit and check-doc-config-drift.sh
treat both directories as relevant surfaces. No existing document
raises a finding.

#### fix(harness): v2.0.28 - restore the 1.4.x engine lookup order

`339da0f7`

Step D2 keeps the 1.4.x lookup-order text, but the port replaced it
with the 1.5.x pointer paragraph, which cited an AGENTS.md heading
this line does not have. AGENTS.md still sends every client to this
file for the lookup instructions, so the step table and the sections
after it come back from port148/overnight. One line routes
the E:\ paths through development-machines.md (plan G3).

Three smaller review findings go with it: the architecture pointer
names the test coverage rule rather than the table Part C removed,
hook-authoring drops upstream-watch.sh (absent here), and the
config-protection comment drops a decision number no one on this
line defined.

#### fix(hooks): v2.0.28 - block-no-verify reads git the way bash does

`232ba743`

The gate matched one word, so git commit -n, git's own abbreviations and a
one-call core.hooksPath all skipped commit-msg. It now reads the command the
way bash does, through _shellwords.py bypass: it follows bash -c, eval, sudo,
subshells and quoted flags, and a commit message that only mentions a flag
passes. git push -n stays allowed. A command too long to read is judged on its
raw text. The test suite also gains the UNCHECKED rows for the doc and
ModuleData gates and the wrapper signature F3 changed.

Ported-from: 9cfcadd07e61e8b21f9c0a296995901990855d02
Ported-from: 434d1d795465fe9b78c06decd31db5a26558132c

#### fix(tools): v2.0.28 - schema check reads project.mbproj like the engine

`747eccb1`

WP136 already carried _read_like_engine and its EngineReaderTests in its
transplant of the tool, so this step only re-cites the engine source to
the installed v1.4.8 line: XmlResource.GetMbprojxmls reads
new StreamReader(mbprojPath) at XmlResource.cs:121, where the 1.5.x tool
cited :92.

Research: taom-src v1.4.8 XmlResource (:114-123).
Ported-from: 09f3126a759c3b956e63cee789ff4bd6f8ed355f

#### fix(hooks): v2.0.28 - drift gate judges what the commit records

`4bbe3892`

check-doc-config-drift.sh used to judge the index against HEAD whatever
the commit was. It now reads the command with _shellwords.py
commit-plan: an amend is judged against HEAD~1 (the empty tree for a
root commit), a path commit by the named paths' working copies, and a
feature doc that git add stages in the same command through --also-new.
The ask and deny texts are JSON encoded and quote the arguments that
ran. Its awk now also extracts the new-feature-doc and git-diff-failed
sections.

Adapted: this line's relevance list and taom_git_repo_dir lines stay.
The hook suite's real-linter wrapper stubs check_claude_md_budget. This
line has no section 5d relevance list, so the feature-doc glob row greps
the hook itself.

Ported-from: 88c0b6985bf7828abd744c1eef54eea6ae3a67d4
Ported-from: 09f3126a759c3b956e63cee789ff4bd6f8ed355f
Ported-from: ad3967222267b7e0d5a142a6fca1e74ef0197650

#### fix(tools): v2.0.28 - doc lint gates new feature docs and dash bases

`bc6792aa`

lint_docs.py now fails a newly added feature doc with no "How to verify
in game" section that has a body, and --also-new names a doc a pending
git add will add. A doc a merged branch already has is not new.

An unresolvable --dash-base used to read as zero findings; it now exits
2 before any check runs, and a diff that fails on a good base is its own
gating git-diff-failed finding. The dash scanner uses the shared fence
walker, so an indented fence and a three-backtick line inside a
four-backtick block read correctly. --only-paths judges what a path
commit records.

The hunks sit on 1.5.x refactors this line never took, so they are
ported by hand: this line's function is check_claude_md_budget, not
check_context_budget, and the tests mock it under that name. No case
was dropped. test_audit_repo_secrets.py gains the audit-allow marker for
its deliberate literal.

Ported-from: 4407fbc055cb48f7f748415e5298540e210af060
Ported-from: 88c0b6985bf7828abd744c1eef54eea6ae3a67d4
Ported-from: bc7a7f0d9a157a395b06d6afee61802823abad6e
Ported-from: 09f3126a759c3b956e63cee789ff4bd6f8ed355f
Ported-from: ad3967222267b7e0d5a142a6fca1e74ef0197650

#### fix(settings): v2.0.28 - 30 s git gates, Bash length gate, depth 1

`655caa2c`

The harness retro on the 1.5.x line found the 5 s and 10 s git gates
killed, failing open, about 400 times in a month, so they now register
at 30 s. check-bash-length.sh refuses a Bash command the tool would cut
short (7,500 characters, each single quote counted as 4) and names the
fix. detect-docs-gaps.sh converts every feature folder in one sed and
one tr, so it no longer overruns its 10 s registration.
CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH=1 keeps a subagent from spawning
its own; PYTHONIOENCODING keeps hook output in UTF-8.

The VS Code "Run Tests" task gains both no-deploy flags. The example
MCP file named servers this line removed, so it goes.

Left out: the co-op half of the source commit, the CLAUDE.md, lesson,
BannerIcons and Dependencies hunks, and the optional enabledPlugins
block, which stays the maintainer's.

Protected file: .claude/settings.json carries exactly plan 138's M1,
minus the optional block, applied under programme decision D31.

Not-tested: that the harness delivers check-bash-length's exit-2 reason
(live check L1).
Ported-from: 6ce20f6edde1df897ed0032927dfcb43b35d756b
Ported-from: 1c622732ecfce085fab27c56d3c7f39e3d59f2c5
Ported-from: ae7c4edbf3f2ec958bbf50ef009ca5bca609f6fa
Ported-from: b84dbaeb2a713301908c69b8366a2a8b639e6ede

#### fix(hooks): v2.0.28 - git gates read past wrapper words and grammar

`70451ab6`

The confirm gates, the commit label gate and the verification marker
found git only as a segment's first word, so `timeout 600 git ...`,
`env ... git ...`, a brace group, a subshell or a loop body slipped past
them. They now read parsed segments from _shellwords.py, past `timeout`,
`env`, `nice`, `xargs`, subshells, brace groups and loop bodies; a
heredoc fed to a command is data, not a command.

The hooks catalog keeps this line's rows; only the sentence on
block-broad-git-add carries over from the source commit.

Ported-from: caa7ac35669d728bdcd0498f1044e5e744558d9e

#### fix(hooks): v2.0.28 - config override unlocks one session only

`daf24751`

The override file name now comes from the payload's session_id, then
CLAUDE_CODE_SESSION_ID, then CLAUDE_SESSION_ID, cut to [A-Za-z0-9_-]. No
id means no override, so one session's flag no longer unlocks every
session. Paths are normalised (backslashes to /, case folded) before
matching, so a Windows ADR path is guarded (#677).

Research: 1.5.x blob at 88e805450
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 88e80545094ad408d8376c66c51ea6ebbb59b781

#### fix(hooks): v2.0.28 - polearm gate reports through additionalContext

`56fce7c6`

The polearm and shield audit printed its report to stderr on exit 0,
which Claude never sees. It now reports through PostToolUse
additionalContext. tools/test_hooks.sh 7l fails any PostToolUse or
PostToolUseFailure hook that writes unguarded stderr.

Research: 1.5.x blobs at 88e805450 (its test section 7k is 7l here)
Not-tested: delivery of additionalContext (live check L8)
Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005

#### fix(hooks): v2.0.28 - confirm five more forms that destroy git work

`8454d56e`

The confirm gate now asks on switch --discard-changes and -f, worktree
remove --force, reflog expire and delete, update-ref -d, --stdin or an
all-zero id, a branch delete plus force in any spelling, and git's
unambiguous long-option prefixes. restore -S alone stays allowed. The
ECC adoption record comes along because the hook cites it.

Research: 1.5.x blob at 88e805450
Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005

#### fix(tools): v2.0.28 - packager refuses TAOM files the commit lacks

`c4d8116d`

A deploy never deletes, so an install keeps every prefab a later commit
removed, and a release packaged from it ships them again. WP140 deletes
19 screen files, which makes that a live risk on this line.

--require-build now also compares the TAOM copy list with the release
commit's Main/_Module tree through stale_module_files, ignoring case,
because the tree spells GUI/PreFabs and the install GUI/Prefabs. It
exits 2 on a stray file, a git failure or an empty tree. bin/,
Shaders/D3D11/ and RuntimeDataCache* are build output and are skipped.
An editor pack is kept in a published folder with no loose Assets/ and
refused beside one. TAOM.Dependencies is exempt.

The release doc's "the gate proves the DLLs only" gained a qualifier so
it does not contradict the new sentence. The CLI test the plan makes
conditional on a _taom helper is not ported: TestRequireBuildCli here
has _modules instead.

Not-tested: a real dry run against an install; the next release does it
Ported-from: 450332b74cf82403d78033463687dafec446fedf

#### fix(hooks): v2.0.28 - Stop reminders reach Claude through a block

`e06c0afa`

The verification, version-tag and deep-review reminders printed to
stderr on exit 0, which goes to the debug log only. They now print one
{"decision":"block","reason":...} per streak through _stop_reminder.sh,
stay silent when stop_hook_active is true, and clear their streak
markers when the condition clears.

check-deep-review.sh is taken whole at 88e805450, which also carries
fa8c06f82's reviewer-type pattern and wider file list. The pattern
matches the deep-reviewer agent WP138 brings; until then the reminder
behaves as today, since the old pattern matched no logged agent type.

Research: hooks docs "Stop decision control"; 1.5.x blobs at 88e805450
Not-tested: that Claude receives the block reason (live check L7)
Ported-from: 43e6780e4421fb785ac9bf1c4ee5bc53e8b69a68
Ported-from: c80c4108c88e9d5e28ec047458542235639f53c7
Ported-from: 65b405e42529bc390c9e9b8eca3e0427948c3389

#### fix(release): v2.0.28 - gate the release number and installed manifests

`258ba11b`

--require-build now reads the release commit's Main/_Module/SubModule.xml
and refuses when it cannot be read, when its number fails the D8 check
added earlier, or when that number is a tag this commit does not descend
from (tag_on_another_line, which also covers v2.1.0 and v2.1.1). For each
planned TAOM or TAOM.Dependencies module with an installed SubModule.xml
it refuses one that does not parse or whose version or DLL set differs
from the commit's. An absent installed manifest is not this check's
finding. The public channel's hand-typed v2.0.29.5 would now be refused
twice. Port of #658 and #968.

This is the rest of plan 115 step B7; release_number_problem landed ahead
of the gate.

Ported-from: e6ee8c9985beee1b63dafb2cda11c1a9b5879ae4

#### fix(hooks): v2.0.28 - RCA follow-ups reach the drift gate; crash rows

`5ab6567d`

The two items plan 136 left for this harness work (B3 step 6, D4).

check-doc-config-drift.sh now runs for a commit staging only a
docs/reviews/rca-*.md, asks the linter for --drift-only --staged so the
RCA check reads what the commit records, and quotes the linter's RCA
section in its deny.

tools/test_hooks.sh gains the 7m block: a validator crash (exit 3)
allows with the traceback in additionalContext, a findings exit (1)
still denies. It also gains three rows for the RCA trigger, written
inline because the live awk names its sections.

Ported-from: ca92db87882f6b9dc08d526a231e8e4a531dcc57
Ported-from: bc30b76dac099ad4433f3e73d5106c26179a930b

#### fix(hooks): v2.0.28 - anchor hook paths, gate PowerShell, deny Serena

`5aa07eb0`

Relative registrations failed to start after a persistent cd, so every
call ran ungated (#690). Each now names
"$CLAUDE_PROJECT_DIR"/.claude/hooks/<name>.sh. The git-gate group
matches Bash|PowerShell (read as Bash text for now). Serena's sixteen
edit tools are denied because they bypass every Edit, Write and Bash
hook; its read and memory tools stay. attribution is empty to match the
no-attribution rule. 7j and 7k pin it.

The settings change is the plan's script, run in this worktree under
D31 rule 2, with the plan's measured 52 insertions and 27 deletions.
CLAUDE.md ended over its 46,000 byte cap with the new line, so under
D31 rule 7 two passages a linked doc holds went: the shipping-client
aside in the lookup order (docs/reference/mcp-servers.md) and the
migration notes bullet (the header links docs/migration/).

Ported-from: 7b342ed59ffeb3b1a856717b65ef32f1bd8e1c94
Ported-from: d01bddf104e9271f600e5ad5895b71faedcaa888
Ported-from: 7d13adc369490cf6d7e866c6a44a4dcc76c85f68
Ported-from: c79a585218ad22841c8db1f273e013385512b332

#### fix(hooks): v2.0.28 - validate-push guards bannerlord-1.5.x too

`c50c1c16`

validate-push protected master, main and bannerlord-1.4.5 only, so a
session on this line could force-push the 1.5.x trunk. It refuses that
now. block-no-verify's comment no longer claims hooks always run in the
main tree (#690).

Ported-from: d01bddf104e9271f600e5ad5895b71faedcaa888

#### fix(hooks): v2.0.28 - git gates judge the repository a commit runs in

`d11d520f`

The gates cd'd to CLAUDE_PROJECT_DIR, which stays at the checkout a
session started in, so a worktree commit was judged on the main
checkout's index, version and tools. This port runs executors in
worktrees.

The five gates now read the repository the git call runs in (cd or
git -C, then the payload cwd, resolved to the top level) through
taom_git_repo_dir over the 1.5.x reader _shellwords.py. A command that
commits in two repositories is refused, and the validator gates run a
repository's tools only in this project.

Ported-from: 12d96adc10448a27e9d225b23453f4558d196579
Ported-from: 7e26d77fec30e1ea2b18521c247cfcd3eb59db3e
Ported-from: 407570ecea85fb22762075eb1ff5c665830afe2e

#### fix(hooks): v2.0.28 - split hook text without here-strings

`71200e67`

Git Bash 5.3 hangs forever on a here-string of 65,536 to 65,663 bytes
of text (measured on the 1.5.x line, #681) and a killed gate allows the
command. Nine sites now split into an array under set -f; validate-push
still judges the first line only. 4e and 7h pin it.

The hang does reproduce on this laptop (GNU bash 5.3.9): before the fix
all six 7h rows hit their 10 s bound, and after it all six answer.

Ported-from: 143f0fa8540032657beb979977e2a52329a6e95d
Ported-from: fff6d66e9bd5a2db11c0ef20ab167225efbd1a22

#### fix(hooks): v2.0.28 - gates answer in hookSpecificOutput, read UTF-8

`6aef3c26`

Claude Code reads a PreToolUse decision only under hookSpecificOutput.
Seven gates and the /freeze check printed a top-level
permissionDecision, so they decided nothing. They now nest it and
JSON-encode each reason. _pybin.sh exports PYTHONIOENCODING=utf-8,
because one character outside cp1252 emptied the extracted command and
every Python-parsed gate allowed it.

Also the bulk files-tracked query, the subject gate's parser and AI
attribution refusal, the /freeze path decode and the validate-push
refusal text. The hook suite reads decisions as Claude Code does
(decision_of).

check-moduledata-validation.sh was merged by hand with git merge-file:
WP108 and WP136 added code rows and a crash block that already prints
hookSpecificOutput, and those stay. Two comments keep this line's
"TAOM hooks MUST fail open" wording.

Not-tested: a live refusal through the harness (maintainer check after merge)
Ported-from: c79a585218ad22841c8db1f273e013385512b332

#### fix(tools): v2.0.28 - refuse items written to a slot they cannot fill

`1e555447`

validate_moduledata.py now reports ITEM_SLOT_REJECTED, an error the
commit hook names, for an equipment row whose item type the engine will
not put in that slot. Equipment.DeserializeNode assigns an item only
when IsItemFitsToSlot passes; otherwise it asserts and leaves the slot
empty, so the troop never wears the item. No reference is broken and
the XSD types slot as a plain string, so nothing else saw it.

The pass reuses audit_battle_equipment_memory.py's slot rule, rosters
and troop overrides over Native, SandBoxCore, SandBox, the live Armory
and the repo module. A missing module is a warning, not a pass. It finds
0 rows and costs about 0.6 s of a 19 s run here, but the item types come
from the installed Armory v2.0.26, so that 0 is void until the public
Armory is installed (D20, D25). apply_isengard_troop_revamp.py already
writes the skirt to Body on this line, so it is unchanged.

Research: v1.4.8 Equipment.DeserializeNode :204-220, IsItemFitsToSlot
Not-tested: none (offline)
Ported-from: 2420508fb009f7d221ed0884869e60b13f3aee59
Refs #729

#### fix(tools): v2.0.28 - collision-body gates run in the validator (#622)

`43b433ba`

validate_moduledata.py gains three passes from the 1.5.x line, built
on validate_mesh_refs.py and all called from main():

- MISSING_COLLISION_BODY (error): a body_name, holster or collision
  body that no loaded tpac ships, the hang Patch90 guards at run time.
  Only packs whose table of contents failed to parse are byte-scanned;
  a run with no packs, or a scan that raises, is one NOT-verified
  error, and without the install the pass says SKIPPED.
- MISSING_VISUAL_MESH (warning): the same for mesh and holster_mesh;
  the item renders invisible.
- COLLISION_BODY_BORROWED (error): a weapon or piece whose body is
  another kit's bo_ twin. It resolves, so the first pass lets it
  through, but it ties the item to art it does not own, which an art
  drop can rename (#633). tools/blender/add_collision_body.py authors
  the item's own twin.

The commit hook's --code list names both errors, which
CommitGateCoverageTests requires, and a new test fails if any
*_issues pass is not called from main() (#622: on 1.5.x the first
pass sat uncalled for three days). A Modules folder missing a live
module no longer crashes main(): Validator.missing_ref_roots holds
Paths. Docs: moduledata-validation, the rule rows, the weapon
workflow, ranged ladders and the #633 RCA.

armory-ref-audit, preload-body-guard and mesh-ref-validation read as
live again: fb3e560e4 had reworded them to say this step was held.

Measured on LOTRLOME_Armory v2.0.26: MISSING_COLLISION_BODY 0,
COLLISION_BODY_BORROWED 3, MISSING_VISUAL_MESH 0, void under D20, not
the public Armory; the whole validator takes 21.5 s warm on this
laptop.

Not-tested: a commit-time run through the real hook
Ported-from: ef6afeb6729b50756c2b7352603c9870a91e5fda
Ported-from: 332f2ac71414ea39d252ee5e3bea9886576f5071
Ported-from: b23a27ef45b3dd060c9729e63a26f65388e12a6f
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 719074072ab5777bd614cda70afc330f8346f1c1

#### fix(diagnostics): v2.0.28 - the exit sampler hint no longer overpromises

`4b89b2da`

The Enable Exit Stall Sampler hint said turning it off keeps the other
diagnostics "without any thread suspension". The Battle Freeze Sampler
and the Main Thread Freeze Sampler default on, answer to their own
switches and both reach Thread.Suspend through ThreadStackCapture. The
hint now says the toggle stops this sampler only. The ThreadStackCapture
summary, two comments and two doc lines that called it the only
component that suspends the main thread are corrected with it.

The hint is an unregistered MCM literal, so there is no key to keep.

#### fix(localization): v2.0.28 - old saves keep six Umbar clan names

`0a9216b2`

Clan names are saved whole, key included. A save from before WP135 holds
{=aom_clan_umbar_1_name} on all six Umbar clans, each with its own inline
name, and at base that key had no row. WP135 gave clan_umbar_1 a row
under it, so in any language but English the other five clans showed the
first clan's name. The plan's "keeps its saved one" was wrong about what
the player sees.

clan_umbar_1 now uses aom_clan_umbar_casimirioni_name, renamed in the
registration, all 12 language files and the translation caches so the
next translation run reuses the text. The old key has no row, so old
saves fall through to each clan's inline name. A gate test keeps it so.

Research: v1.4.8 Clan.cs:101-102, TextObject.cs:13-14,
  SandBoxManager.cs:371-374, MBTextManager.GetLocalizedText (:232-286),
  LocalizedTextManager.cs:20-27
Save-compat: in a save from before WP135 these six clans read their own
  English names in every language, the behaviour before WP135; new
  campaigns get six translated names
Not-tested: an old German save in game

#### fix(tools): v2.0.28 - data gates say NOT RUN when nothing was checked

`4487816f`

Four rows of data_gates.py gave a verdict the tool had not earned.

check_rdc_entries.py read a missing RuntimeDataCache or Assets folder, or
zero packages, as a data failure (exit 1). They now exit 2, and it finds
the module through $BANNERLORD_GAME_DIR. audit_action_set_parity.py and
validate_mesh_refs.py hardcoded E: and now use the same lookup, and an
unreadable action_sets.xml exits 2. On this laptop both tools now reach a
real verdict instead of an error.

check_prefab_budget.py exits 1 in its WARN band and 2 over the cap, which
data_gates read as FAIL and NOT RUN: inverted, at a total under the cap.
A new --gate flag gives runner exit codes (1 only at or over the cap),
and the report prints the TOTAL and any WARN line. The cap is unchanged.

The moduledata row also drops a note naming a file this line lacks.

#### fix(startup): v2.0.28 - contain a throw from a manual Harmony patch

`95acf768`

Every patch category is isolated by PatchCategoryApplier, but the eight
manual patches called harmony.Patch bare, in the same callback. A throw
left through MBGameManager's submodule loop, which has no catch on
v1.4.8, and skipped the remaining manual patches and the Harmony census.

Each one now runs inside ManualPatchApplicator.TryPatch, which logs a
[PatchApply] error naming it and carries on, so the SubModule comment is
true. The three settlement guard patches stay off if their service
cannot be resolved, because their prefixes read it.

Research: plans/104 Maintenance notes (the unguarded calls, and the
  uncaught submodule loop)
Not-tested: a real harmony.Patch failure in game

#### fix(battle-corpses): v2.0.28 - register the Apply button failure text

`4c05cc28`

The MCM button's last-resort catch showed a literal English string in
all 12 languages. It is now taom_bc_apply_failed, registered in
taom_module_strings.xml with an English placeholder row in each language
file. The translation pass stays owed: it is a paid call.

#### fix(career): v2.0.28 - less [CareerPerks] log volume on the game thread

`e8426847`

Two kinds of line were written far more often than their comments said.

The stat lines are written for every hero agent in a campaign mission,
and careers are player-only, so a lord with no career wrote "none" once
per mission, twice if mounted. A hero with nothing applied now writes no
line until something has been applied. "none" stays for a buff ending.

HeroHealing runs twice per in-game hour for the main party, and
TroopUpgradeCost once per upgrade target per party-screen row. Each line
is a durable INFO write. The same numbers now write one line until the
next passive-cache refresh. Only the line is silenced, never the factor.

Research: v1.4.8 PartyHealCampaignBehavior.OnHourlyTick (:102-105),
  PartyCharacterVM.InitializeUpgrades (:1258-1269)

#### fix(hero-race): v2.0.28 - hollow heroes keep their saved race

`efe9c6bf`

WP126 moved the race roster from Hero.AllAliveHeroes to Hero.FindAll to
reach dead heroes. Hero.FindAll scans Campaign.Characters, and a hollow
character is unregistered, so a living hero with one stopped being
captured or restored, and the session-launch capture dropped his entry
from the saved map.

The roster is now the union of Hero.FindAll, Hero.AllAliveHeroes and
Hero.DeadOrDisabledHeroes, each hero once, and the two lookups read the
same list. The birth repair now keeps a non-zero race on an unregistered
character, because the roster can have restored it by then; race 0 still
takes the twin's.

Research: v1.4.8 Hero.FindFirst and FindAll (Hero.cs:2130-2145),
  AllAliveHeroes and DeadOrDisabledHeroes (:888, :890), MBObjectBase
  (GetHashCode override, no Equals override)
Save-compat: same save key; saves now also list hollow heroes
Not-tested: a real hollow hero across a save and load (no reproduction
  exists; the Patch83 census logs how many a save has)

#### fix(ui): v2.0.28 - remove dead visual definitions and gate them

`bdb6f541`

A VisualDefinition no widget names never runs, and a widget naming a
definition its prefab does not hold throws at movie load. A new test
checks both directions across every prefab.

Its first run named three orphans, whose widgets are inside comments:
InfoPanel in GameMenu.xml, FormationListPanel and FormationSeperator in
PartyScreen.xml. Those blocks go, with the two constants of PartyScreen
and the one of GameMenu that only they read.

Not-tested: the game menu and party screen, opened in game
Ported-from: 498d07e084799de6e09c0021399b95c10f14887d
Ported-from: ed3f911eb7f6637a81fd9eed20e5aadaf876cfba
Ported-from: 0385cc0418719b2d000e19632c20e7b2a44b85da

#### fix(action-bar): v2.0.28 - drop the dead DataSource on the movie root

`f53460e9`

BattleActionBar.xml set DataSource="{..}" on the widget directly under
Window. GauntletMovie.LoadMovie reads no DataSource on the root view,
so the attribute did nothing and read as though it did. A test now
keeps the root free of it.

Ported-from: 258db5e7320a88aec25173c78a4658c241fb1e7b

#### fix(ui): v2.0.28 - gate brush sprites, delete an unused gold brush

`7affb889`

A new test fails on any layer in TAOM's brushes that names a sprite no
module defines and no game brush names, because that layer draws
nothing. Names the game's own brushes carry are allowed, since a TAOM
copy of such a brush inherits them.

The first run named one sprite, Icons\Coin@2x in GoldIcon.xml. Nothing
in Main uses ROT.Icon.Gold, so the file is gone.

Ported-from: 462d745ec4bb5d278a39f4d7654a86aa2cd3d04a

#### fix(mixed-formations): v2.0.28 - log the cavalry gate, correct comments

`72b640cc`

When a formation becomes cavalry, Mixed Formations hands it to
SmartCavalryAI and returns no position. That happened silently. It now
logs one info line per formation per mission, so a layout that stops
applying can be explained from the log.

The Mixed Formations Debug Mode hint said it showed messages on the
HUD. It only gates two lines in the TAOM log, so the hint and the docs
now say that. The setting's name and default are unchanged.

Stale comments are corrected in IFormationAdapter and
IFormationLayoutService. Two conflicted doc hunks in
mixed-formations.md were left out: the 1.5.x hotkey row and per-frame
bullet describe a parser this line does not have.

Not-tested: the log line and the hint text in game
Ported-from: 38e5e627427d8f8bb7064d77fca37ab7a2d5945b
Ported-from: 35aa37439e17db923b672e0cde1184d8b9709f23
Ported-from: 35044f5832fa3e8c067a2a20f8fd91121d11df9c

#### fix(nameplate-fade): v2.0.28 - stop pinning MCM defaults at construction

`554f4d0b`

NameplateFadeSettingsProvider read TaomSettings.Instance in its
constructor, so a resolve before MCM was up kept the defaults for the
whole session. It now caches the reference on the first non-null read
and reads through it.

The hot path settings test now covers this provider, and also the two
that already read lazily (BattleBalance and NameplateRelation). A wrong
"disabled for the session" message in Patch35's log is corrected, and
module-dependencies.md gains an MCM bump check for cached references.

Not-tested: fade distances after a slider change
Ported-from: 69275212f939569d20a30f7ebbbfcc7d6db7f925

#### fix(career): v2.0.28 - a non-finite mutation keeps the old value

`dc921e31`

A career mutation whose result overflowed to infinity wrote that value
into the ability. The service now keeps the old value and logs a
warning.

Ported-from: 95a348a602a168e50db793d009c5685033113307

#### fix(career): v2.0.28 - the tooltip's frame draws under its canvas

`0edd3e39`

Each of the three diamond tooltips on the career screen had a frame
child with StretchToParent inside a CoverChildren parent, the fault the
stretch ratchet counts. The frame is now the tooltip's own brush and
the canvas sprite moved to an inner widget, so the size comes from the
text and the frame needs no stretched child. No text line changed.

The ratchet's allowance for this file is gone.

Not-tested: the tooltip in game
Ported-from: 95a348a602a168e50db793d009c5685033113307

#### fix(careers): v2.0.28 - cloned careers show their source's ability icon

`cecfb89a`

18 enabled careers were cloned from another career and kept a new
ability template id, but no CareerSystem\Abilities sprite is registered
for that id, so the career screen drew an empty icon frame.

A career may now carry an ability_sprite attribute. The screen draws
that sprite when it is set and the template id otherwise. The 18 clones
name their source's icon, and the career generator writes the attribute
on the next clone. No art is added.

Not-tested: the icons on the career screen
Ported-from: 4d9ae0e8a514f412ba83abaf9546c567d6821ce2

#### fix(warg): v2.0.28 - a rage always closes and gives the formation back

`526ea447`

A dead hitter threw in the tree pass that had just taken the rider's
formation, and that stopped the warg's whole behaviour tree for the
rest of the battle. A riderless warg threw twice more, because a mount
has no Team. All three are now guarded.

The rage start stores the rider beside his formation, and the stored
rider gets the formation back first, even after leaving the warg. A
new rider closes the old rage and keeps his own formation. A panicked
rider stays out, because Agent.OnFleeing cleared his on purpose.

The 6 s timeout now means 6 s with no attack: the timer starts once
per rage and only an attack restarts it. Before, every tree pass
restarted it, so it could not fire. Rages end sooner than before, and
players will see that. A rage is now always 2 attacks, with no roll.

Research: v1.4.8 Agent.OnFleeing :5128, Agent.IsRunningAway :784
Not-tested: a rider leaving a raging warg, and a rage's end, in a real battle
Ported-from: 17b071c7a85b20434115bc5e3408e80762ad0eda
Ported-from: 7a21239dcb59e8fa6f5d5d1f543a0bd01ef8e301
Ported-from: 4a7b7164d0168eb42354be20c087b793564e9650
Ported-from: 931d59084b4e5b571628d1baf32dd05680b5d0d4
Ported-from: 38e5e627427d8f8bb7064d77fca37ab7a2d5945b

#### fix(tools): v2.0.28 - API snapshot check passes; snapshot regenerated

`c7397224`

-Check drops the backlinks footer before comparing and prints the
differing lines, so it exits 0 on committed files. The snapshot is
regenerated: the header count equals the rows at 313 patches and 49
models, and the five classes that were missing are in. Patch37_CrashReport
is a marker class with no [HarmonyPatch], so it has no row. The
ContainerLoadData_Fill_Patch and HeaderLoadData_Readers_Patch rows now
sit under their nested site classes with the same targets.

The README points at each file's header instead of hard-coded counts,
and the generator says how it resolves (AccessTools).

Research: regenerated against installed v1.4.8; -Check 0 on the committed files and 1 on an edited row
Not-tested: none (tool and docs)
Ported-from: 4ab005dd10202c7b17d2bcacfd6a423a8245784e

#### fix(ui): v2.0.28 - screens drop settings the game never applied

`c4afe8e3`

Eight prefabs lose attributes the engine never applied: a misspelt
VerticalAlinment, AcceptEvents on three buttons, EaseIn on custom battle
panels, an unused character selection animation, two StackLayout paths
that name nothing, and AlwaysRefreshAll on the troop pop-up list. Four
screens loaded by nothing go too: mainmenuHUD.xml, PoppyHUD.xml,
ROTsprite.xml and MomentumView/KingdomIcon.xml. Nothing changes on
screen.

PrefabAttributeNameBindingTests now fails any attribute that is not an
exact-case public settable property of the element's widget type,
outside an allowlist of 16 pairs v1.4.8 vanilla also carries. It also
fails a VisualDefinition or VisualState attribute the engine never
reads. The custom battle panel ease is left alone: v1.4.8's own file
carries the same dead EaseIn, so there is no vanilla look to match.

Research: WidgetPrefab.LoadVisualDefinitions, GetObjectAndProperty,
GridWidget, ListPanel, StackLayout (v1.4.8 taom-src)
Not-tested: character creation, custom battle, order of battle and party
screen in game
Ported-from: 450332b74cf82403d78033463687dafec446fedf
Ported-from: abb718c2fc835297e2e5a6e9ab9973acaf291f79
Ported-from: 7012783b35f0329c128e5bc1d3aaf2781a24ad79

#### fix(ui): v2.0.28 - unused weapon preview prefab removed

`2bf0ce48`

PreviewPanel.xml was loaded by nothing and carried a Brush.Fontsize
typo. Gauntlet drops an unknown Brush.<Property> without a word, so
PrefabBrushPropertyTests now fails any such attribute in a GUI prefab
or a PrefabExtension source whose first segment is not a public
property of TaleWorlds.GauntletUI.Brush, compared case-sensitively.

Research: TaleWorlds.GauntletUI.Brush public properties (v1.4.8 taom-src)
Not-tested: none
Ported-from: 10363643999af79aaec0d9e49e0d5f0447167531

#### fix(ui): v2.0.28 - nine screen widgets draw with their real brushes

`0494ed4b`

Nine brush names in TAOM screens are declared by no module TAOM loads,
so those widgets drew with the default brush and logged nothing. The
battle action bar labels now use Tooltip.Text. The career picker uses
CareerSystem.Effects.Title and .Passive. The custom battle army slider
uses vanilla's CustomBattle.Slider.* brushes and its number box
CustomBattle.SliderValue.*. The presets label uses Popup.Button.Text.
The unused EventXml.xml, which held the ninth name, is gone.

EveryGuiPrefabBrush_IsDeclaredByAModule now fails any literal brush or
Constant BrushName in a GUI prefab that TAOM's module set does not
declare.

Research: brush files of Native, SandBoxCore and TAOM (v1.4.8)
Not-tested: the four screens in game
Ported-from: 62fb6bb1deaac84d4daceada5657c30151ea811c

#### fix(gui): v2.0.28 - drop eleven screen files copied from the base game

`598918ee`

Eleven prefab files were identical to the v1.4.8 files they replaced:
the eight the 1.5.x line deleted, plus both party tuple rows and the
custom battle troop type pop-up, which are identical on this line.
Nothing changes for players, and a future engine patch to those screens
now reaches them. Installs must remove the old Modules\TAOM folder or
the copies stay, because a deploy never deletes.

Two gates keep it so. PrefabCloneWidgetReferenceTests fails a TAOM copy
that drops a widget reference or DataSource vanilla declares, and a
copy identical to the file it shadows. PartyTupleVanillaParityTests
compares any party tuple copy a later port brings back; it passed on
both rows before the deletion.

Research: WidgetFactory last-wins prefab index (v1.4.8)
Not-tested: the party screen, custom battle and campaign nameplates in game
Ported-from: fc932d4ca22a6741366c8c13b4f98d7532a3c7de
Ported-from: 8d5cfc6231a8638610b6c1a4e2225cb98c92ca50
Ported-from: 7012783b35f0329c128e5bc1d3aaf2781a24ad79
Ported-from: 5408487956f6f7bf41ca21bc6a4aeecafd5c4b18

#### fix(ui): v2.0.28 - the troop upgrade pop-up shows used horses again

`2a7b5fa1`

The party screen's troop manager pop-up was an old vanilla copy. It now
follows the installed v1.4.8 file: header layout, list, key-hint
placement, the used-horses tooltip binding, and the tertiary key hint
parent PartyManageTroopPopupWidget needs before it shows any gamepad
hint. TAOM's two deliberate edits are kept: the single centred Done
button with no Cancel button, and the top-right close button.

Research: PartyManageTroopPopupWidget.OnLateUpdate (v1.4.8 dump :73-81)
Not-tested: the pop-up in game (mouse and gamepad)
Ported-from: 7012783b35f0329c128e5bc1d3aaf2781a24ad79
Ported-from: 8d5cfc6231a8638610b6c1a4e2225cb98c92ca50

#### fix(ui): v2.0.28 - prefab element and XPath gates, drop HUD leftovers

`f113ced5`

Two binding gates now read TAOM's prefabs against the installed game.
One fails an element that names neither a widget class nor a prefab.
The other fails a PrefabExtension whose movie is missing or differs in
case, or whose XPath matches nothing in the winning prefab.

The first gate found one offender. The AgentStatus.xml copy was v1.4.8
vanilla plus an 81-line dead block naming a class that exists nowhere,
and CareerSystem/AbilityHUD.xml was an unreferenced empty override.
Both are gone. The career energy bar's XPath resolves in vanilla's file.

Research: WidgetFactory prefab index and CreateBuiltinWidget, UIExtenderEx
PrefabComponent movie lookup (v1.4.8, 2.13.2)
Not-tested: the combat HUD and the career energy bar in a battle
Ported-from: 720e421fa41423d563d1beec4b2174ec57b80a0c
Ported-from: 71484b8e4651b05e14bed6133d95655ac9a3749a
Ported-from: f48ef74dc42d9eebb68af634a6320947384d5fc7

#### fix(module): v2.0.28 - TAOM.Dependencies listed first in both blocks

`531798c7`

The launcher now shows TAOM.Dependencies at the top of TAOM's dependency
list, in DependedModules and in its version-pinned mirror. This is
launcher display only: GetSortedModules is multiplayer-only on v1.4.8,
so the entry order sets no single-player load order. The 1.4.x pins stay
as they were. Port of #371.

Research: v1.4.8 dump ModuleHelper.cs:271 GetSortedModules is called
 only by Diamond (multiplayer)
Ported-from: de385cab9f149ad86cf2aa83539137f5f6326129

#### fix(release): v2.0.28 - add the D8 release-number check

`aa3a445b`

release_number_problem() names why a version cannot be a 1.4.x release:
a fourth part (the public channel's hand-typed v2.0.29.5 is in no
commit) or a number in the 1.5.x range, v2.0.29 to v2.0.36. The pairing
check uses it for TAOM_Map and LOTRLOME_Armory.

This is the part of plan 115 step B7 that stands alone. The rest of B7
waits on the --require-build gate, and so on the maintainer's
Directory.Build.props step: tag_on_another_line, the commit manifest
read and the installed SubModule.xml comparison.

#### fix(build): v2.0.28 - drop Game Pass staging and refuse its bin

`90d3187e`

Game Pass is not supported (maintainer, 2026-10-22), and that applies
here too: every binaries path in the player logs is
Win64_Shipping_Client (E1 and E2 39 each, E3 1, none names Gaming.Desktop)
and the Game Pass folder in the laptop's TAOM.zip carries a retired
BehaviorTreeWrapper.dll. TAOM and TAOM.Dependencies stop staging that
folder. FailOnGamePassStagingInModule stops a deploying build while an
older checkout still holds one, because CopyModule deploys everything
under _Module, and the packager refuses a Game Pass bin, dry runs too.
Port of #968.

This forces a cleanup before the next deploying build: delete both
_Module/bin/Gaming.Desktop.x64_Shipping_Client folders in every
checkout, and the matching folders in every install and channel folder
(plan 115, M2). Left out: the TAOM.Client guard and ClientDeploymentTests
(D7), and the TestRequireBuildCli and SHIPPED_DLLS edits, which arrive
with the plan 017 gate.

Not-tested: FailOnGamePassStagingInModule in a deploying build
Ported-from: e6ee8c9985beee1b63dafb2cda11c1a9b5879ae4

#### fix(tools): v2.0.28 - packager refuses Armory shaders with no source

`aa2959bc`

Under D12 the 1.4.x line installs no shader sources, so any taom_ shader
in the Armory's shader_compile_report.log is a gap, and a native shader
needs its .rs in <game-root>/Shaders/Sources. A material with neither
renders invisible for every player. The gate fails closed: a missing
report, a report whose rows do not sum to its Total variants: trailer,
or a missing sources folder is a refusal. The report is read with a
right-anchored row pattern because a split on two spaces skips 168 rows
of the laptop's report. --game-root lets a channel folder be checked.

Research: installed Armory v2.0.26 report, dry run exits 0 naming 10
 shaders; this says nothing about the public Armory (D25 rule 3)
Ported-from: d99398f1210eeabc67a5158c56479fb9a5f6749f

#### fix(tools): v2.0.28 - packager refuses retired DLLs and test troops

`ec9bb668`

The packager now refuses, in a dry run too, a retired binary under any
bin folder (BehaviorTrees.dll and BehaviorTreeWrapper.dll, inlined into
TAOM.dll in May; deploys never delete, and the laptop's TAOM.zip ships
both) and a shipped ModuleData file that defines a taom_test_ id.
MinHook.x64.dll and TAOM.NativeSkinFixes.dll stay allowed: they are the
1.4.x vendored allowlist. Port of #736.

This lands ahead of the plan 017 --require-build gate, which waits on
the maintainer's Directory.Build.props step, so the refusals sit where
that gate will follow them.

Ported-from: 9c310bc238fa1441f4a1344c5cc3fe5dccadb6a2
Ported-from: a4b5d625863f80f542eb8240ee061ae16386e9f3

#### fix(diagnostics): v2.0.28 - BuildStamp pairs DLLs by commit, not clock

`fc10cfbf`

E3's MISMATCH is a carried-over TAOM.Dependencies whose sources were
identical, and its log shows every preview patch applied, so the old
"expect the preview patches to fail" claimed a failure that did not
happen. Equal clean commits now read "(pair OK: both built from <sha>)"
whatever the time between the builds. Different commits keep the time
tiers, and MISMATCH names both commits. A .dirty or .nogit stamp never
reads as a commit pair: it falls back to the clock.

Not-tested: the verdict line in a player log

#### fix(diagnostics): v2.0.28 - a log names its engine and its ending (#481)

`724e25c2`

A TAOM log now names the Bannerlord build and every active module with
its version, so a player log states the TAOM_Map and Armory versions
too. A clean exit writes "[Shutdown] clean module unload"; a log ending
without it means the process died, which E3 could not tell. 30 logs are
kept instead of 10, and the crash bundle carries TAOM.Dependencies'
diag.log. Port of #481.

Research: taom-src ModuleHelper.GetModuleInfo (:35),
 GetActiveModules (:196), ModuleInfo.Id (:23), ModuleInfo.Version (:35)
Not-tested: the [Engine] and [Shutdown] lines in game
Ported-from: c8000699480b59f24e42f2e992ad39b6be7fde26

#### fix(banners): v2.0.28 - a Kit session leaves no crash-loop marker

`c06c4f4a`

TAOM.Dependencies writes session-launching.marker when it starts and
deletes it when the game reaches the main menu; a marker still there
at the next start reads as a crash loop. Now that the Modding Kit can
load TAOM.Dependencies, a Kit session would write the marker and never
reach the main menu, so the next game would report a crash loop that
never happened. The detector now skips its marker when its DLL loads
from the Kit's Win64_Shipping_wEditor bin. Game and server launches
are unchanged.

Research: v1.4.8 editor build Module.LoadSubModules loads the DLL from
 <module>\bin\<working directory name>; RuntimeLog.ModuleDir
Not-tested: a real Kit session (plans/121 Part B checks diag.log)
Ported-from: b41de91e8e9b71b1c2003d367f7217b55713f8e2

#### fix(dependencies): v2.0.28 - say what a swallowed postfix fault returns

`d31a815a`

The PatchShield comment said a swallowed fault returns the default
because a finalizer cannot reach __result. Neither half held. The method
returns its result local as it stood at the fault: the default for a
fault in a prefix or the body, the original's own value for a fault in a
postfix, since Harmony 2.4.2 stores it before any postfix runs.

Comment only, so the #371 pairing is untouched. A new test pins the
postfix case on this line's Harmony.

Ported-from: 48d66b59a7454eee36ed3f989f5776dce4e74a0a

#### fix(faction-map): v2.0.28 - Mordor's card states its +20% party size

`7b43bac1`

Text only: every feat value stays. Mordor's feat (TaomCulturalFeats.cs,
_mordorPartySize) has given +20% since 4f72e160e while its card still
said +10%. The JSON, the English row, 12 language rows and 12 cache rows
now say +20%, so the next translator run cannot bring +10% back.

A test now ties every card's party-size number to its feat. It also
fails a party-size mention with no number, which caught the Rhun and
Harwan descriptions; they now say +5%, what _rhunPartySize and
_haradPartySize give. Their 24 language rows carry no number to swap and
stay as they are, still true but without the figure: a translation pass
is owed for them.

Research: EffectBonus literals read from TaomCulturalFeats.cs
Not-tested: the culture card and faction screen in game
Ported-from: e166d8c847f663112f68a3463d222aad31b2d437

#### fix(dunland): v2.0.28 - Hunter and Falcon troops fight as archers

`e9801c79`

The Cigfran-lûth Hunter carries only a bow but was tagged Infantry, and
the three Hebog-lûth troops (Scout, Horse Archer, Noble Horse Archer)
carry bows but were tagged Cavalry. For a regular troop default_group
is the battle formation (v1.4.8 Mission.GetAgentTroopClass reads
GetFormationClass, which is DefaultFormationClass), so the Hunter
fought in the infantry line and the Hebog-lûth with the melee cavalry,
and in sieges as infantry. The Hunter is now Ranged and the three are
HorseArcher. Only the four tags change; skills and kits are untouched,
and the Hunter is no longer a banner-bearer candidate.

Known limitation: the next reseed of Dunland's enlistment kits will
take the Hunter's kit for the archer recruit, and it has no sidearm.

Research: taom-src v1.4.8 Mission.GetAgentTroopClass; dump
  BasicCharacterObject.GetFormationClass
Not-tested: a battle with Dunland archers and the Hebog-lûth
Ported-from: 1641fb98542bdd3ec7ad6fcbf4d33c1f96a30b7b

#### fix(tools): v2.0.28 - data gates run check_rdc_entries again

`0fb873f6`

WP141 landed tools/check_rdc_entries.py, so the two rows WP136 left out
of the gate table come back verbatim from the 1.5.x tree: rdc-entries for
tpac packages and banner-rdc for Kit-imported banner icons. The armory
ladder test names the tool again.

FBX_GLOBS and NAME_GLOBS served only rows this line does not have
(race-morphs, fbx-lods, banner-sheets) and are deleted. The {taom_module}
substitution stays, because banner-rdc uses it.

Not-tested: the gates against a module with a RuntimeDataCache folder
Ported-from: e45f6df2c2c8cbb52099d6d2ffb7defa136f7766

#### fix(tools): v2.0.28 - Isengard generator writes the skirt to Body

`aa5b9382`

apply_isengard_troop_revamp.py still put sk_uruk_hai_skirt_a1 in the
Cape slot for urukhai_champion and urukhai_berserker, while
troops_isengard.xml carries it in Body. The skirt is BodyArmor, which
the engine accepts only in Body, so a re-run of the generator would
have undone the fix and the troops would silently lose the item. Both
table rows now write Body. This touches the tool only, no ModuleData.

Only the generator half of the 1.5.x commit lands here. Its validator
half, the ITEM_SLOT_REJECTED pass, stays deferred, so no gate on this
line catches a wrong slot yet.

Research: read both rows against troops_isengard.xml; generator not run
Not-tested: none
Ported-from: 2420508fb009f7d221ed0884869e60b13f3aee59
Refs #729

#### fix(banners): v2.0.28 - seven clan banners lose blank and debug layers

`71a8251c`

Seven clan banners named icons or colours the game does not have, so
parts of them drew nothing or drew in the engine's debug colour:

- Nos Alenwe and Nos Iminye of Mirkwood drew a blank emblem; they now
  fly the Mirkwood kingdom banner.
- Zorian of Rhun drew a bare background; it now flies the Rhun kingdom
  banner.
- One Rivendell house and one Lothlorien house lose two top layers
  that drew nothing; the rest of their heraldry is unchanged.
- Two Khand houses drew most of their layers in an undefined colour;
  those layers now use the house's own map colours.

A new test checks every icon and colour in every clan, kingdom and
culture banner key against Native's and TAOM's banner_icons.xml, so a
borrowed key with a missing icon fails before it ships.

The data rule allows this step on the v2.0.26 laptop Armory: banner
keys touch no item, roster, equipment, troop or mesh reference, and the
verdict reads only Native's and the repo's banner files. Validator
output is unchanged by the commit. The two 1.5.x feature-doc hunks are
dropped because those docs do not exist on this line.

Research: test_banner_icon_ids.py red on exactly the seven planned rows
Not-tested: the banners in game
Ported-from: 487e3d9a99ba005ec6e3b706ce4f743a6f5bd9ce
Refs #951

#### fix(refuge): v2.0.28 - the engine saves the AI pin

`602c6cf7`

Comments, one test message and the refuge doc claimed the AI pin is
lost on reload. v1.4.8 saves it, so the pin taken on load is an
idempotent belt and not a repair. No behaviour change.

Research: taom-src v1.4.8 MobilePartyAi.cs:124-125
([SaveableProperty(11)])
Not-tested: comment, message and doc only
Ported-from: 63630e8a09ce5b98b5ab7eccbfb0cf9984d532d9

#### fix(localization): v2.0.28 - vanilla translations for 181 rewordings

`576aa825`

TAOM rewords 181 game texts in English only, but shipped a machine
translation of each that replaced the game's curated rows. 1,803 rows
and 1,800 cache entries go; 369 rows stay where the game's row would read
wrong with TAOM's names. The tools now leave these keys out.

Research: installed vanilla ids (VanillaLocalizationKeys over Native, SandBoxCore, SandBox, StoryMode, CustomBattle)
Not-tested: in game, German "Call ... to war" reads the game's own German
Ported-from: 8a40271107ff75703db4ccfa5b3ed99963cd210f

#### fix(tools): v2.0.28 - translator keeps conversation animation tags

`f71ef480`

A translation that drops or changes an [ib:], [if:], [rb:] or [rf:] tag
is rejected, and the prompt says to copy them. The language files carry
four such rows per language today.

Not-tested: none (tool only)
Ported-from: 7abce5db992d52f9fd881f0214b01b52cf417740

#### fix(localization): v2.0.28 - one text per key, damaged rows reset

`3a4c17a4`

New gates keep one English text per key, inline defaults equal to their
registration, one name per name key, and no word from another writing
system. The Combat and Deliver Personnel quests get their own fallback
keys; they showed the Deliver Goods text in other languages. Two
messenger lines and two data texts now match their registration, and the
six Umbar clans get their own name keys.

156 language rows (the damaged ones and the two changed keys) go back to
English and 175 cache entries go,
for the next translation run. taom_wotr_victory_lead is registered with
its key, so its English row counts as untranslated rather than damaged.

Save-compat: clan names are saved (Clan.Name is a SaveableProperty), so an existing campaign keeps the shared Umbar key; new campaigns get six
Not-tested: in game, a German Combat issue without its own texts reads "a foe" text
Ported-from: 4ceae903bf1f52e1587506883a434439e8f85379
Ported-from: 9e2a39f4092dd32c7bd58e193e24ee210594f26f
Ported-from: 9dd2f594fd887f792a5cd336781b6832ff6ff753

#### fix(tools): v2.0.28 - free English seeding, a real cap of zero

`b4fa0026`

--sync-ids --seed-only --apply writes the English rows and stops before
any provider is touched. --max-entries 0 is a cap of zero (it meant no
cap) and a negative cap is refused. A sync that reaches the Armory
checks the install before writing.

Not-tested: none (tool only)
Ported-from: c2c8afe420257616acf61991ce3fe2f1e0c58b1c
Ported-from: a9f2d7edab4096dee8cf2524949686d0be0bb560
Ported-from: 71fea53c83a8db1a9ce33a3ac20ff5be6e06ac42
Ported-from: b11b6d142e99774f462f7b8e365fb29d6bb0c009

#### fix(tools): v2.0.28 - translator keeps key owners and line endings

`f85f2336`

A key two English sources declare is seeded and translated only into its
first source's file. A key another file of the language carries is not
seeded twice. Every line keeps its own terminator and the result is
parsed before it is written.

Not-tested: none (tool only)
Ported-from: 4ceae903bf1f52e1587506883a434439e8f85379

#### fix(tools): v2.0.28 - string harvest stops adding blank lines

`ffe01dab`

Each harvest that wrote rows left one more blank line before
</strings>; the block now replaces the blank lines.

Not-tested: none (tool only)
Ported-from: 337228885b0725f1a2b0c657e5106c7e0090fe6d

#### fix(localization): v2.0.28 - module strings load in Custom Battle

`7f10150c`

The GameText file was registered for Campaign and CampaignStoryMode
only, so its rows (the str_ culture texts among them) were absent in
Custom Battle and the editor. No current FindText id is affected, so
this is groundwork.

Research: dump GameTextManager.cs:107-117
Not-tested: Custom Battle shows no "Text with id" error for a TAOM culture text
Ported-from: e74ef39315b7ae787bb847b03924a7f83f0a89cb

#### fix(localization): v2.0.28 - grammar processors run again

`02f4fd74`

A language file that names a language but declares no processor resets
it to one that prints no grammar, so possessives and case endings came
out empty ("Aragorn Party"). The Armory and TAOM_Map files do exactly
that. TAOM's own files now carry Native's name and processor for the
eight processor languages and restore them when TAOM loads after those
modules.

The coverage tool gains the processor check and --processors-only for a
release channel. The live TAOM_Map and Armory files still need the
maintainer's edit.

Research: taom-src LanguageData.Deserialize (:107-111), LocalizedTextManager.CreateTextProcessorForLanguage (:53-67); dump LocalizedTextManager.cs:86-115, Module.cs:294-301
Not-tested: in game, English party names read "<Name>'s Party"; German and Turkish lines keep their article and suffix
Ported-from: 41aa2779bf2b9f6c715775b303fc8894f9538a91

#### fix(localization): v2.0.28 - English overrides only for English

`357ba58c`

The override table answered in every language, so non-English players
saw English for 313 strings that have rows in all twelve language files.
It now applies only while the text language is English; any other
language falls through to vanilla, which reads that language's own row.

taom_debug.log gets one INFO line per switch away from English and one
per return to it. The language is read through the new
ITextLocalizerAdapter, so the gate never touches MBTextManager.

Research: taom-src MBTextManager.GetLocalizedText (:232, English branch :264), ActiveTextLanguage (:52)
Not-tested: in game, German shows an aom_ notable's German row; switching to English logs the line
Ported-from: 407b1e767546ae7f8b35436ffd59c4bba59447c4

#### fix(tools): v2.0.28 - map renamer writes only renames, dry run first

`a3da987f`

Apply-MapVillageNames.py ran at import with no dry run, rewrote all 438
of its ids in all 12 languages on every run (resetting their
translations to English) and evicted nothing from the translation cache,
so the next translator run wrote old names back. It now writes only ids
whose English changed, is dry unless --apply, refuses a new name another
settlement carries, writes the cache and loc files first and the master
last so a failed run is finished by a re-run, and exits 2 on a missing
file or the wrong master. Its NAMES table is unchanged, and the 1.5.x
name-tag calls are left out because this line has no tools/name_tags.py.

Port of #1042 and #1039.

Research: an ast.literal_eval comparison of NAMES prints 438 438 True.
Not-tested: --apply against the live TAOM_Map (tests use a temp dir).
Ported-from: 244c15d2779dec00f2e80d4e23a98387448e49df
Ported-from: 87ffea9b2fbc0d96f2e43b9173d2f8d8a22ab28b
Ported-from: af2b198e45432233e696ce17c6898fe73101c9b1

#### fix(tools): v2.0.28 - village tool refuses taken names, checks culture

`6e8de97d`

add_map_villages.py checked a new name only against its own table, so a
batch could give two settlements one name. It now refuses a name another
settlement already carries, and --check names that, plus a master row
whose culture or bound settlement differs from the table. A new helper,
evict_cached_names, removes rows from tools/translation_cache, which
keys on the string id alone and would otherwise write an old name back.
The docstring no longer says proper nouns stay untranslated: a later
translator run translates or transliterates them.

Port of #1039 and #1042: the map tool hunks only, no 1.5.x village rows
or meshes.

Research: add_map_villages.py read on this branch; helpers taken from
the 1.5.x commits, unit tests on synthetic fixtures.
Not-tested: --apply and --check against a real map batch on the desktop.
Ported-from: 244c15d2779dec00f2e80d4e23a98387448e49df
Ported-from: 87ffea9b2fbc0d96f2e43b9173d2f8d8a22ab28b
Ported-from: af2b198e45432233e696ce17c6898fe73101c9b1
Ported-from: 4b207f951b373e4182e0b4a1ee4507b0d61b3523

#### fix(tools): v2.0.28 - Tripo converter: no live default, no overwrite

`ddbda5fb`

convert_tripo_prop_textures.py defaulted its output into the live
TAOM_Map textures and overwrote what it found. --src and --dst are now
required and an existing output is replaced only with --force. It also
handles one triple per texture set, builds the ORM map and has a
corrected AO filename pattern.

Left out: the Blender report clearing, which needs blender_job.py and
kitbash_kits.py (absent here) and has no poller on this line.

Not-tested: a real Tripo export.
Ported-from: 041808e5fd13b0db0d729e178b6922a040ec98f3
Ported-from: 71fea53c83a8db1a9ce33a3ac20ff5be6e06ac42
Ported-from: c79a585218ad22841c8db1f273e013385512b332

#### fix(tools): v2.0.28 - clan heraldry tool keeps the file's line endings

`8622a99a`

generate_clan_heraldry.py wrote hard-coded LF lines into its targets,
so on a core.autocrlf=true checkout spclans.xslt, characters/clans.xml
and taom_partyTemplates.xml came out with mixed line endings and the
spec suite's no-op test failed. It now uses each file's own terminator,
by majority.

Port of #930.

Research: the tool and its test applied cleanly from the 1.5.x commit;
test_applying_every_spec_is_a_no_op fails before and passes after on
this CRLF worktree.
Not-tested: --apply against a real clan batch.
Ported-from: b75fbab076e60e0e7c8352eb418123dad65c9177

#### fix(tools): v2.0.28 - troop rebalance skips civilian sets; atomic writes

`fcfaf53a`

rebalance_troops.troop_weapon_classes counted weapons in civilian sets,
and its docstring wrongly said those sets hold no inline weapons:
imladris_recruit carries its only bow in a civilian roster. A weapon
only in an EquipmentRoster civilian="true" or an EquipmentSet
equipmentType="Civilian" no longer counts; an <equipment> written
straight under <Equipments> still does, because v1.4.8 applies it to
every set. No troop's swap flips today, so this is latent.

The militia binding regex now takes either quote, like the validator's
copy. fix_upgrade_armour_regressions parses every file before writing
any, so a bad file late in a batch no longer leaves earlier files
written.

Left out: the #617 per-tier ladder rework and the translation sync tool
(1.5.x only), and planned_skill_edits for templated troops, whose
premise is false on v1.4.8.

Research: taom-src BasicCharacterObject.Deserialize (365, 372, 388-398,
412) and MBEquipmentRoster.InitEquipment (88-101), as the plan records.
Not-tested: a real rebalance run.
Ported-from: 8465cc9843601b7ac1c885d9116629c1c186700f
Ported-from: b904e02a3625d5a81308e0880a85b71912232c9b

#### fix(tools): v2.0.28 - enlistment seed marker parses; fallback kit gated

`78c4a366`

generate_enlistment_rosters.py --seed-missing wrote an XML comment
holding a double hyphen, so parse_written refused its own output and the
mode could never succeed. The marker no longer spells the flag. The
generator's DEFAULT_ROSTER_ITEMS table is now registered in
check_generator_item_refs.py, so a fallback kit id the installed modules
lack shows up in that gate.

Port of #798.

Research: the gate prints no generate_enlistment_rosters.py line on
this laptop; validate_moduledata.py stays at BROKEN_ITEM_REF 3768. Both
readings are on the installed v2.0.26 Armory, so the id verdict is
provisional until the public Armory is installed.
Not-tested: a real --seed-missing run.
Ported-from: 44e9edd48bfa5d3db85efe5f821cfd71341a3f9b
Ported-from: 71fea53c83a8db1a9ce33a3ac20ff5be6e06ac42

#### fix(tools): v2.0.28 - gui-tests names only tests on this line

`cba16a88`

The data gate table named PrefabSpriteTests, which did not land: on this
tree the gate finds 19 sprite names in no SpriteData, more than the plan
allows without a maintainer's call. The row now runs the brush test
alone, and the manual step reads prefab sprites by hand until the gate
lands.

Not-tested: none

#### fix(tools): v2.0.28 - tavern mercenary tool: dry run, finishes a run

`1cc28c47`

generate_tavern_mercenaries.py wrote on any run and named
troops_mistymountainorcs.xml, a file f577bd547 removed, so every run
crashed part-way. It is now a dry run unless --apply, plans every copy
and repoint before writing, parses each new file first and writes only
files whose bytes changed. The mistymountainorcs pick moves to the
goblin tree's goblin_fighter, which that culture already binds. This
line's Harad picks stay harad_noble.

Research: the dry run on this branch's repo data ends with "0 files
changed (dry run, pass --apply to write)".
Not-tested: --apply.
Ported-from: a9f2d7edab4096dee8cf2524949686d0be0bb560
Ported-from: 71fea53c83a8db1a9ce33a3ac20ff5be6e06ac42
Ported-from: 44e9edd48bfa5d3db85efe5f821cfd71341a3f9b

#### fix(tools): v2.0.28 - item generators leave the assets mirror alone

`ba9606bf`

The starter kit and ranged ladder generators wrote the lotraom-assets
mirror whenever the folder existed, against the 2026-09-22 rule that the
mirror is the maintainer's to sync, never a session's. Both now touch
only the live Armory unless --asset-repo names a mirror. The starter
kit's --no-asset-repo stays accepted so older commands still run.

Research: re-implemented, because this line has no
_gamedir.armory_trees; the v1.4 example path stays in --help.
Not-tested: --apply against the live Armory on the desktop.
Ported-from: 798e4d0bf8b4a1aa9f40fb6e15f0e97725a59264
Ported-from: 72cad16b105d4f752558ac1b7f004b2cfc92e285

#### fix(tools): v2.0.28 - starter kit keeps foreign rows, writes all or none

`9d10bad3`

generate_starter_kit.py could delete another author's rows inside its
marker blocks and could leave a refused run half written. It now refuses
to rewrite or revert a block holding foreign rows in any of the three
files (the two stylesheets and the pieces file), computes and parses
every new text before the first write, and --verify exits 2 when it
checked nothing or crashed instead of reading as a failed item check.

Research: all 16 hunks of the guard patch applied cleanly on this
branch; 1.4.x RETIRED_DONORS, ASSET_REPO and _refuse_shrink text kept.
Not-tested: --apply and --verify against the live Armory.
Ported-from: a9f2d7edab4096dee8cf2524949686d0be0bb560
Ported-from: 71fea53c83a8db1a9ce33a3ac20ff5be6e06ac42

#### fix(tools): v2.0.28 - translator: --max-entries 0 is zero, free seeding

`f4ef52be`

translate_with_claude.py read --max-entries 0 as "no cap", so a run
meant to make no paid call could make all of them. Zero is now a cap of
zero, and a negative cap is refused (its slice would have sent all but
one entry to the provider). --sync-ids --seed-only --apply seeds the
English rows and stops before any provider call; the coverage test's
failure message now names that free command.

Port of #804, #812 and #813 (tool half).

Research: translate_with_claude.py main() read on this branch; the
new test patches every provider, cache and write function to raise.
Not-tested: a real paid run or a real seeding run on the desktop.
Ported-from: a9f2d7edab4096dee8cf2524949686d0be0bb560
Ported-from: 71fea53c83a8db1a9ce33a3ac20ff5be6e06ac42

#### fix(tools): v2.0.28 - mbproj audit checks the ids v1.4.8 requests

`88b3a106`

A row must now name an id something asks for or a type native reads.
The audit used vanilla Native's own soln_ ids as a stand-in, so a row
with a vanilla id nothing requests read clean. It now holds a table of
14 ids, each with its source. A monster row that loads through
SubModule.xml is a warning, and a run that audits too little exits 2.
On this laptop: 0 errors and 5 warnings, the four Armory soln_monsters
rows and the known soln_voice_definitions row.

Research: v1.4.8 Module.cs requesters, MBMusicManager.cs:345, Game.cs:437
Constraint: the five native ids and NATIVE_BY_TYPE are v1.5.5 data,
  unverified on 1.4.8
Not-tested: none
Ported-from: 09f3126a759c3b956e63cee789ff4bd6f8ed355f
Refs #987

#### fix(tools): v2.0.28 - mbproj audit reads rows as the engine does

`47490126`

The engine loads a project.mbproj row two ways: managed code asks by
exact id, and native code asks by type and never reads the id. The old
regex audit knew neither, so an invented id read clean. The audit now
parses rows as the engine does and reports a row nothing can reach as
DEAD-ID. soln_worldmap_color_grades is not a requested id on this line:
nothing in the v1.4.8 dump reads it. On this laptop's three modules it
prints 0 errors and the known soln_voice_definitions warning. The
Armory snapshot stays as it is until the public Armory is installed.

Research: v1.4.8 MBObjectManager.GetMergedXmlForNative,
  XmlResource.GetMbprojxmls, requester grep
Constraint: NATIVE_BY_TYPE is v1.5.5 data, unverified on 1.4.8
Ported-from: bc30b76dac099ad4433f3e73d5106c26179a930b
Refs #983

#### fix(tools): v2.0.28 - culture generators keep blocks, never half-write

`42e2de9f`

insert_new_factions.py and promote_borrowed_cultures.py deleted their
region and re-appended it before the closing tag, so a block added after
it moved on every run and read as drift. Both now replace the region in
place through tools/_marker_region.py. promote_borrowed_cultures.py also
parses every output before it writes any, so one broken file no longer
lands while the rest never do. No generator was run against the data.

Not-tested: none
Ported-from: c73cf296139d8270e5bb6a9ded1a4b88dfc5946b
Refs #976 #979

#### fix(tools): v2.0.28 - one guard for generated XML blocks

`6eacd881`

A generator writes its rows between BEGIN and END markers so a re-run
can find them. tools/_marker_region.py now owns the two rules for that
region: replace it where it stands, and refuse a damaged file (two
regions, a lone marker, no closing tag), because the engine loads every
row in document order and a stale copy of a repeated id would win.
generate_new_faction_kingdoms.py calls it instead of its own copy.
No generator was run against the data.

Not-tested: none
Ported-from: f963c6188e2dd724964aa04e1b28aa928dd8bd9a
Refs #975

#### fix(tools): v2.0.28 - scene audit skips a settlements file no one loads

`b3fa36cd`

audit_scene_names.py read Modules/TAOM/ModuleData/settlements.xml as if
the engine loaded it, and printed its rows as crash suspects. The
installed TAOM SubModule.xml registers no Settlements, so those are
false. A row whose module registers no Settlements now prints one skip
line and takes no part in the suspect list. A damaged manifest counts as
registered, so it can never hide a suspect. The feature doc names the
two log lines that are expected noise.

Not-tested: none

#### fix(tools): v2.0.28 - prefab budget sums every module, finds the install

`a8680610`

check_prefab_budget.py hardcoded the desktop's path, so on the laptop it
printed "not a directory" and returned 2, and it counted only TAOM_Map
while the engine's queue is global across every loaded module. It now
finds Modules through the install variables and sums each module's
Prefabs by default; --dir keeps the single-folder count. This laptop
reads 130,468 of the 131,072 cap, TAOM_Map 93,830 of that, and exits 1.
The cap asserts in the Modding Kit only, so this is a Kit hazard and not
a player one.

Not-tested: none

#### fix(devconsole): v2.0.28 - spawn_troops finds troops in Custom Battle

`6dcfa70f`

taom.spawn_troops looked its troop up as a CharacterObject, the type the
campaign registers for NPCCharacter. Custom Battle registers
NPCCharacter as BasicCharacterObject (v1.4.8 CustomGame.OnRegisterTypes)
and CharacterObject is sealed, so the lookup found nothing there and
the command reported an unknown troop in TAOM's main test venue. It now
looks up BasicCharacterObject, which still returns the campaign's
CharacterObject in a campaign, and gives a non-campaign troop a
BasicBattleAgentOrigin, because SimpleAgentOrigin casts its troop to
CharacterObject.

Research: taom-src v1.4.8 MBObjectManager.GetObject, SimpleAgentOrigin,
  BasicBattleAgentOrigin; dump CustomGame.OnRegisterTypes
Not-tested: taom.spawn_troops in a Custom Battle
Ported-from: d469914abaa48cc32907256aa911597ad4243101

#### fix(xslt): v2.0.28 - drop three hero templates that never matched

`9be32a79`

A stylesheet runs over the earlier modules' merged file, so a template
naming a hero TAOM itself defines never fires (CreateMergedXmlFile,
v1.4.8 MBObjectManager.cs:966). Three heroes.xslt templates did that:
lord_1_9_5, lord_1_52_4 and lord_1_71_1. Their texts never reached a
player, so the templates and their string rows in English and the 12
languages go. A new test fails any non-identity template that selects
nothing in the vanilla file it transforms.

Research: CreateMergedXmlFile v1.4.8; test lists exactly these 3 rows
Not-tested: none
Ported-from: bc5b5d71d6971bcbdd80f1df329a3bf77d416cb4

#### fix(enlistment): v2.0.28 - siege duties for defenders, and wage order

`ffe56725`

A soldier whose commander was defending a besieged town was never
offered the siege duty (walking the works) and was offered the quiet
night watch instead: only the besiegers counted as being in a siege.
Defenders now count too.

When a soldier's unpaid wages were already above the cap, a day's
wage paid by his commander went to the banked debt instead of that
day, so he lost a day of back pay. Today's wage is now paid first.
This could only happen after the wage table or the cap changed
mid-term.

The column check behind the duty offers no longer keeps an hourly
copy, so a new commander's column is read at once. The offer
chance's built-in default now matches the shipped value, two
comments about the menu link colours were corrected, and the duty
text tool now ignores commented-out rows and refuses duplicates.

Research: taom-src v1.4.8 MobileParty.SiegeEvent (:1132, the
  besieger's camp event), Settlement.SiegeEvent (:103),
  GameMenuVM.OnFrameTick (:481-512) and IsMenuTextChanged (:522)
Not-tested: a siege duty offered to a defender, in game
Ported-from: b0625f0d18e6d7a67efc70fd16f23a321fa91133

#### fix(combat): v2.0.28 - battle action bar registers, keeps combat input

`ccd5efba`

The Companion Tactics battle action bar never worked on 1.4.x. SubModule
added its view in OnMissionBehaviorInitialize, which runs in
Mission.AfterStart after MissionScreen.OnMissionAfterStarting has
registered the views (v1.4.8 MissionState.FinishMissionLoading), so the
view never ticked or finalized. The same code added a second warg rage
controller beside the [DefaultView] the engine builds, and that second
one never unsubscribed the handlers it registers in EarlyStart.

Both views are now [DefaultView]s the engine builds with the mission,
and SubModule adds neither. The bar attaches on the first Battle-mode
tick of a field battle (the mode is Deployment at screen init). Its
layer takes mouse buttons only, so the cursor stays hidden and attack,
block and mouse-look keep working; the 1 to 9 hotkeys, which clashed
with vanilla's formation keys, are gone; turning the setting off in a
battle removes the bar. The bar stays parked: the setting is now "Show
Battle Action Bar", off by default, under a new name so a saved "on"
does not carry over, because its stances still change nothing in
battle and a hidden cursor cannot click it.

Research: taom-src v1.4.8 MissionState.FinishMissionLoading,
  MissionScreen.OnMissionAfterStarting, ViewCreatorManager,
  InputRestrictions.SetInputRestrictions, GauntletLayer.LoadMovie,
  MissionMainAgentController
Not-tested: the #970 in-game check in docs/features/companion-tactics.md
Save-compat: none; MCM key EnableBattleActionBar is now
  ShowBattleActionBar
Ported-from: d96f349f1124ab474eb9b299b7c1389f6d9f8365
Ported-from: 40385aa5bdb759eb52d92a30b3a78a194cf000e0

#### fix(enlistment): v2.0.28 - one fitness check for a commander

`eeacc5bc`

The six places that ask whether a commander can still lead an
enlisted soldier (alive, free, with an active party) now share one
check, CommanderSnapshot.IsFit. Nothing a player sees changes.

Rejected: the board line for a battle the oath keeps the soldier out
  of, from the same 1.5.x commit (deferred, plan 132 P132-8)
Ported-from: 5e7a0da6ef91d512a90dc9764f431c3d1b1534a7

#### fix(warg): v2.0.28 - the rage controller swap tolerates a missing one

`71c7d580`

The warg rage controller is a [DefaultView] the engine builds only for a
mission with a screen. Without it, SwitchMainAgentController disabled
vanilla's controller and then threw on the null one, leaving the player
without controls; the tree steps that set its target or look direction
threw too. Every lookup is now null-checked, and the swap returns
before touching vanilla's controller. This lands before the next change,
which stops SubModule adding a second controller instance.

Not-tested: a warg rider raging, then regaining control, in a field
  battle
Ported-from: c9138027466ee2bcdcdcf8a84117abca8a3968ab

#### fix(warg): v2.0.28 - late-spawned warg riders keep their formation

`18668a39`

A warg's tree stores its rider's formation when it is built, but for a
warg that joins after the first scan the tree is built in the mount's
OnAgentBuild, before the rider is built, so it stored null (v1.4.8
Mission.SpawnAgent builds the mount first, and Agent.Build sets a
rider's formation). When that warg raged or died, the reset wrote the
null back and the rider left his formation for the rest of the battle.
The rage start now stores the rider's live formation before clearing
it, and the reset writes a formation only when one was stored.

Research: taom-src v1.4.8 Mission.SpawnAgent, Agent.Build
Not-tested: warg reinforcements raging, and losing their warg, in a
  campaign battle
Ported-from: 726975a529427d6e68f3240d3c2eb8b39d8b0e5e

#### fix(enlistment): v2.0.28 - the service upkeep runs once a frame

`e2c7ee6c`

While an enlisted soldier waits in his service menu, a short upkeep
pass keeps him beside his commander's column and refreshes the status
board. Two clocks fed that pass: the campaign's own tick and the wait
menu's tick. With time running both fired in the same frame, so the
upkeep ran twice as often as intended. The wait menu now feeds it
only while time is stopped, the one case the campaign tick does not
cover (a paused game, or a save loading onto the menu).

When the commander fights off a raid on a village and the soldier
joins from the running service menu, the join now waits for the next
frame of that menu, where the game does not advance the raid first,
so his troops no longer take a round of casualties before he sees the
battle. Field battles, sieges and joins made away from the service
menu happen at once, as before.

Research: v1.4.8 dump MapState.OnTick (:156-160, the wait menu ticks
  before the campaign), GameMenu.RunOnTick (:224-232, campaign time
  since its last frame), Campaign.Tick (:958-962), MapEventManager
  .Tick (:59-61, a raid updates even as the player's own event),
  GameMenu.SwitchToMenu and ActivateGameMenu stop time (:358-360,
  :335-337)
Not-tested: the board cadence and a raid relief joined from the
  running service menu, in game
Ported-from: 069962aa43773a2201f8ec358f4c0c9b40f046e9

#### fix(combat): v2.0.28 - Custom Battle runs TAOM's combat mechanics

`76ac5344`

TAOM's Combat Mechanics rules now apply in Custom Battle too (#788).
Before, only the campaign model carried them, so a Custom Battle used
vanilla rules for skill, monster and orc shield crush-through, creature
cleave and stagger immunity, the per-race stagger threshold, the
weight-driven charge knockdown, shield penetration and the charge
penetration.

TaomCustomBattleDamageModel extends the engine's Custom Battle damage
model, because the campaign model's SandBox base reads Campaign.Current
on a mounted hit (v1.4.8 SandboxAgentApplyDamageModel, the mounted
weapon penalty), and makes the campaign model's CombatMechanicsHooks
calls in the same order. RegisterCustomBattleModels adds it on the
BasicGameStarter. Career passives and the Refuge reduction stay
campaign-only: they need heroes and parties. A new test fails when the
campaign model gains an override or a hook call the twin lacks.

Research: taom-src v1.4.8 CustomAgentApplyDamageModel,
  SandboxAgentApplyDamageModel.ApplyDamageReductions; dump
  DefaultSkillEffects, CustomGame.InitializeGameModels
Not-tested: crush-through, cleave, stagger and charge knockdown in a
  Custom Battle
Ported-from: b0154d86ea126363c23f9190d7e45a8891d5681e

#### fix(enlistment): v2.0.28 - soldiers fight their commander's battles

`05067d9c`

An enlisted soldier could let his commander's battle resolve without
him by choosing "Leave it to the others" (or "Send troops") and still
be paid as if he had fought. That option now follows his place in the
line:
- wounded: it stays open and costs nothing;
- when Attack is impossible: it stays open, so he always has a way
  forward;
- in a siege assault or a sally-out: it is closed;
- when his side holds 85 percent of the strength on the field: it is
  open, but it costs a little of his commander's trust;
- otherwise: it is closed, with the reason in the tooltip.

The rule only narrows the option: anything the game already closed
stays closed, and a soldier who leads his side keeps the normal
choice. A battle he stayed out of pays no renown; at overwhelming
odds it also pays half the base battle XP and costs one point of
trust. A line says when he stayed back. The new lines show in
English until the next translation run.

Research: taom-src v1.4.8 EncounterGameMenuBehavior
  .game_menu_encounter_order_attack_on_condition (:1919, private bool),
  MenuHelper.EncounterAttackCondition (:182) and
  CheckEnemyAttackableHonorably (:452), MapEvent
  .GetStrengthsRelativeToParty (:1456, read-only), MapEvent
  .IsPlayerSimulation (:242), set only by the BattleSimulation
  constructor (dump BattleSimulation.cs:60)
Not-tested: the option in a live encounter menu, and the payout after
  a simulated battle
Ported-from: 1d9cc42f1bf13a49a9c340182cc77dc9ec44817c

#### fix(tools): v2.0.28 - faction-map helpers take paths as arguments

`3af4e689`

Both child python -c scripts read their paths and numbers from sys.argv,
so a quote in a folder name no longer breaks them or runs as code. Closes
plan 005 item C on this line. Item A's guard, the credential sweep line in
external-repo-adoption.md, waits for WP102 (D25 rule 4). Item B is plan
016.

Research: none (stdlib tool)
Not-tested: the full faction-map pipeline on the real region art
Ported-from: 6b34fd00f4f1800a2c98aa3cf185100b8e7828be
Ported-from: a0fa3cff234b9cbe411e9825b5028844bb3c237e

#### fix(enlistment): v2.0.28 - the quartermaster says why he refuses

`85ad7ba4`

When an enlisted soldier asked for his service kit and did not get
it, the answer was always "You have drawn your due for your rank
already", whatever the real reason. The quartermaster now gives the
true reason: already drawn (the old line), nothing in his stores for
the soldier's company, or the kit cannot be loaded into the baggage
just now. The three new lines show in English until the next
translation run.

Not-tested: each answer in a live conversation
Ported-from: 1a132c727cf473f6010bd5fc29e36f5a99589c54

#### fix(dread): v2.0.28 - the dread aura weakens close troops, never routs

`93fe6eac`

The Aura of Dread of the Nazgul and Sauron no longer reaches across the
battlefield. It touches only the enemy troops in contact with a wraith
(5 m, full strength within 2 m; it was 12 m and 4 m), and it drains
their morale down to 10 and no further, so the fighting finishes them
instead of the aura routing them.

Why it spread on 1.4.x too: the aura could drain morale to 0, and each
rout takes morale from up to 10 allies, first those within 4 m and then
anyone in the routing troop's formation (v1.4.8
AgentMoraleInteractionLogic), so once a line thinned, dread seemed to
act on the whole field.

The MCM "Dread Radius" setting is stored under a new name,
DreadAuraCloseRadius, so every settings file starts on the new 5 m and
an old saved 12 is ignored. The slider now stops at 15 m, where the
1.5.x line measured the engine's proximity search switching to a scan
of every agent; the v1.4.8 managed switch is the same, its native value
is not measured here.

Research: dump v1.4.8 AgentMoraleInteractionLogic (4 m, 10 allies,
  formation fill); taom-src AgentProximityMap.BeginSearch
Not-tested: a campaign battle against a Nazgul
Save-compat: none; MCM key DreadAuraRadius is now DreadAuraCloseRadius
Ported-from: efa32184b31cd903b49989d603226b3907b10f1b

#### fix(tools): v2.0.28 - the translator checks every placeholder

`48a67925`

The translation tool's placeholder check now also sees lowercase and
mixed-case engine tokens such as {newline}, and conditional heads
written as expressions such as {?DAYS>1}. Before, a translation that
dropped one of those passed the check and shipped broken text.
Nothing in the game changes; the next translation run uses the
stricter check.

Rejected: the rest of the 1.5.x commit (a single-player gate for
  1.5.x-only features, the survival-roll split, prefab test helpers)
Not-tested: a paid translation run (WP135 owns it)
Ported-from: 113d3483bb1c6b303546e96c63e369eb2d920d5d

#### fix(dependencies): v2.0.28 - delete the dead SubModule guard

`bbf0537b`

SubModuleConstructionGuard installed from OnSubModuleLoad, which v1.4.8
calls only after Module.LoadSubModules has constructed every SubModule,
so it could never catch a construction failure. The class, its install
and its comments are gone, and CoopPresence's note now states what
v1.4.8 does: a throwing constructor ends the load. Two Dependencies
reflection probes become gate rows. This changes TAOM.Dependencies.dll:
ship it with TAOM from one build (#371).

Research: taom-src v1.4.8 Module.cs 141-153, 201-208, 236-243, 267,
 1026-1103; ModuleHelper.cs:196; ApplicationVersion.cs:40
Not-tested: a third-party SubModule constructor that throws (no such mod
 here)
Save-compat: none
Ported-from: 4e408e0e07b93b72ab40bb8aa75bf7148bd7722a

#### fix(battle): v2.0.28 - enlisted players roll survival as themselves

`048b723f`

An enlisted player who goes down in his commander's battle now rolls
survival exactly as a freelancer does, with his own party.

The healing model rolled him against his commander's party, on the
belief that his own party had no surgeon. The game uses the party
leader as surgeon when none is assigned, so he already had his own
Medicine and Cheat Death. The redirect swapped those for the
commander's surgeon, which made a player with better Medicine or
Cheat Death more likely to die, and it gave him the commander's
culture and career survival bonuses instead of his own. The healing
model no longer reads enlistment state.

Research: taom-src v1.4.8 DefaultPartyHealingModel
  .AddSurgeonSurvivalBonus (:328-338, surgeon role holder or else the
  party leader) and MobileParty.EffectiveSurgeon (:834-844)
Not-tested: an enlisted player falling in his commander's battle
Ported-from: 5d70714c48c93ed7d93315536d433d9e9f433581

#### fix(banners): v2.0.28 - trolls never pick up a dropped banner

`f8a60f9c`

A formation whose bearer falls sends its nearest member to fetch the
banner off the ground, and the engine picks that member by
CanAgentPickUpAnyBanner alone (v1.4.8 BannerBearerLogic, the
FindBestSearcherForBanner loop). SandBox's answer checks only that the
agent is humanoid, holds no banner and is free to move, so a cave troll
could lift the standard TAOM already refuses to hand it. The campaign
banner model now adds the race gate to that check through
IBannerBearerService.PassesRaceGate, which defers to vanilla while the
feature is off.

Research: taom-src v1.4.8 BannerBearerLogic.FindBestSearcherForBanner,
  SandboxBattleBannerBearersModel.CanAgentPickUpAnyBanner,
  DefaultItemPickupModel, HumanAIComponent.ItemPickupTick
Not-tested: a dropped banner next to a troll in a campaign battle
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1

#### fix(titles): v2.0.28 - every realm's ruler has a liege title

`9f7db4dd`

The coronation title showed an engine error text for every TAOM-only
realm, Dunland crowned its ruler "Emperor of Calradia", and lords of
those realms named their liege with no title in dialog. Every realm
now has a title for its ruler, king and queen alike (Lord of Gondor,
King under the Mountain, Elvenking of Mirkwood, Dark Lord of Mordor
and the rest), and Dunland's ruler is hailed as Brenin of Dunland.

The 16 TAOM-only kingdom cultures each get a conversation tag class
(Main/Features/LiegeTitles/TaomCultureTags.cs) and two tagged rows:
the coronation picks a row by its id suffix, dialogs pick it by tag.
These are the first ConversationTag subclasses on this line; a test
pins that each one instantiates with a unique id. The titles are
English in every language until the translation pass.

Research: v1.4.8 ConversationManager :239, :983; GameTextManager
Not-tested: in game, a TAOM coronation, first talk with a Gondor lord
Ported-from: 30003668bd201bf7707f44f792689aab151ed45f

#### fix(taverns): v2.0.28 - tavern maids offer a dish in every town

`73ed3541`

In a tavern of a Middle-earth culture's town, the tavern maid offered
"our local speciality" with the food and drink of the last town of a
vanilla culture, or with both names blank: v1.4.8 sets the two text
variables only inside its seven vanilla culture branches. She now
offers a mug of ale with a bowl of stew wherever the game has no dish
of its own; towns of the vanilla cultures keep their dishes.

A void prefix, Patch111_TavernSpecialities, presets the two variables
before vanilla runs. It arrives as its 1.5.x feature module, listed in
FeatureModules.All (decision D2). The two dish names are English in
every language until the translation pass.

Research: taom-src v1.4.8 TavernEmployeesCampaignBehavior :729-820
Not-tested: in game, the tavern maid in a Gondor town and a Rohan town
Ported-from: d39370059aa0ccc74b222d3b0def69d5b65c2a23

#### fix(sounds): v2.0.28 - elf horn registers the file that ships

`ff4c691b`

The elf horn alert was registered as elf_horn.wav while the file ships
as horn_elf.wav. Nothing plays this sound today, so players hear no
change. A new test fails on any sound path with no file behind it;
this was the only one of 485.

Not-tested: the sound in game (nothing plays it)

#### fix(wanderers): v2.0.28 - give 40 wanderers their own backstories

`b0d50330`

Wanderers of the Goblin-town, Misty Mountain Orc, Blue Craig and
Lindon cultures showed "ERROR: Text with id backstory_c doesn't
exist!" instead of their introduction: their templates were cloned
from other cultures without the dialogue rows the conversation reads.
All 40 now tell their own stories. Other languages show the English
text until the translation run.

A new test fails on any wanderer missing one of the seven rows the
conversation reads, so a new culture cannot ship this gap again.

Research: taom-src v1.4.8 LordConversationsCampaignBehavior.cs:1719-1766
Not-tested: the conversation text in game
Ported-from: 9dd2f594fd887f792a5cd336781b6832ff6ff753

#### fix(tools): v2.0.28 - taom-src takes short names, caches per game build

`7d009084`

A short type name now resolves through a per-build index
(engine-types.tsv, from assembly metadata) instead of probing every DLL
and failing. The game's own modules are searched before any other
module, and module-bin types decompile against the game bin. The cache
folder carries the build stamp (v1.4.8-202609061157 here) so a
same-version hotfix gets fresh sources. decompile_bannerlord.ps1 forces
UTF-8 output. The first run after this decompiles into the new folder;
the old v1.4.8 folder stays unused.

The taom-src and engine-bump skill text, and the same lines in the
codex guide, agent manual and mcp-servers page, wait for WP102 and
WP103 (D25 rule 4).

Research: taom-src test types on v1.4.8 (BuildInfo, AgentVisuals,
 OrderTroopPlacer, DefaultPartyWageModel)
Not-tested: none (tool, test and docs only)
Ported-from: 857734c64f3588959ce46e6b74ba734aa57de721
Ported-from: 97219836c5184fbe0dc5fb3549342b290ce904b9
Ported-from: f39b399b49e8d2249ca3933ece419c07823d5be4
Ported-from: fe20eddff233417935d6e0d6faa59fbd20298250

#### fix(marketplace): v2.0.28 - culture markets skip not-for-sale items

`3cdd97b7`

Culture markets no longer stock items the Armory marks not for sale
(is_merchandise="false"), such as the troll gear. The market pool took
every loaded item with a culture and honoured only its per-culture
blacklist, so hero-only and troop-only kit could turn up on a town's
stalls. Vanilla's own inflows already skip these items (workshop
output, tournament prizes); the pool scan now does too. Stock a town
already holds stays until it is bought.

The 1.5.x line reached the same result through its armour acquisition
gate, which this line does not have; this is the 1.4.x form of the fix.

Research: taom-src v1.4.8 ItemObject.NotMerchandise :154, :428-432
Not-tested: in game, no troll gear in new market stock
Ported-from: bc39f6e4d0f55a70d4fbdc6b515b5439145ced21

#### fix(alignment): v2.0.28 - every side follows the realm it serves

`9c4ce616`

Executions, Wanderer Allegiance and Prisoner Recruitment judged a
hero by his realm's id but his own culture. Whenever the realm counts
as Neutral (Khand, Umbar, Shaghana, Abanissa), the hero's own culture
decided instead, so a Free-born lord or player serving one of them
was treated as Free. All three now read the realm and the realm's
own culture:

- Executions: the relation change and the Honor penalty. Executing a
  lord of a Neutral realm is no longer kinslaying for a Free player
  just because the lord was born Free.
- Wanderer Allegiance: a player sworn to a Neutral realm is Neutral,
  so no wanderer refuses him for his birth.
- Prisoner Recruitment: a lord of a Neutral realm waives the morale
  cost only for prisoners of his own culture, never for his birth
  side. The same-culture rule still uses his own culture.

A clanless hero is still judged by his own culture, and a clan that
serves no kingdom by the clan's culture. The Marriage Alignment hint
no longer calls Dunland Neutral: Dunland is Evil and Khand Neutral.

Research: taom-src v1.4.8 KillCharacterAction.ApplyInternal (:31)
Research: DefaultExecutionRelationModel (:28)
Research: DefaultPrisonerRecruitmentCalculationModel (:53, int)
Save-compat: no saved data changes; ExecutionContext is thread-local
Not-tested: the three seams in a live campaign
Ported-from: 5e43518df7980ebaaec4d22a7370d2df726dd839

#### fix(fiefs): v2.0.28 - one map-menu gate for F6 and Make camp

`4406042c`

F6 (the fief hub) and the field camp's Make camp button now ask one
map-menu gate, Main/Adapters/MapMenuGate.cs, before they push a game
menu onto the campaign map. Besides vanilla MapScreen's own modal set,
which both already copied, the gate refuses while the escape menu is
open, while the campaign has a menu context, and whenever vanilla's
map-action check refuses (prisoner, raft, mission or encounter). Two
copies of the list had drifted apart; there is now one. F6 keeps its
enlisted-attached refusal and now checks the key first.

Taken from the 1.5.x Fiefs button change (#789) without the button.

Research: taom-src v1.4.8 MapScreen :1355, CampaignUIHelper :2387
Not-tested: in game, F6 with the escape menu open and while a prisoner
Ported-from: ca248d1b709376b681b096371352b355ab6c1323

#### fix(tools): v2.0.28 - kingdom generator keeps its block in place

`e18684f3`

No change in game. tools/generate_new_faction_kingdoms.py, which
writes the goblin, Misty Mountains orc, Blue Craig and Lindon kingdom,
clan, lord and hero blocks, now updates each marker block where it
stands. Before, a re-run deleted the block and re-appended it before
the closing tag, so anything written after it moved: on 1.4.x,
heroes.xml holds 36 hand-authored heroes after the Lindon block, so a
re-run would have reordered the file.

The generator also refuses a file that holds two copies of its block,
a lone start or end marker, or no closing tag, so a stale copy cannot
slip through. Ten unit tests cover upsert() on synthetic text.

Ported-from: bd457ee6ec9ab7c5bc9b7180be94922ee9078dcf

#### fix(relations): v2.0.28 - relation notes match the engine

`56d24135`

No change in game. The enlisted commander adapter passed
affectRelatives: true to ChangeRelationAction.ApplyPlayerRelation,
which reads as a relation change that also spreads to the hero's kin.
v1.4.8 ignores the flag: the change lands on the pair the diplomacy
model's GetHeroesForEffectiveRelation picks. The flag is no longer
passed, and a new source test stops code from passing it as true.

Research: taom-src v1.4.8 ChangeRelationAction.cs:14-33
Ported-from: 6450688362da573898e49cdc8fe8c856a0a7f95a

#### fix(resources): v2.0.28 - map-bar resource leaves when the hero loses it

`986fa126`

When the hero stops having a culture resource during a campaign, its
counter now leaves the map bar. Before, it stayed with the old amount,
warning colour and icon, and an empty tooltip, until the next load.
This happens when a goblin, Misty Mountains orc, Blue Craig or Lindon
hero leaves a kingdom that has a resource, or when the heir who takes
over after the player's death belongs to one of those peoples. The
counter comes back with the right amount if a resource returns.

The feature page no longer claims every kingdom has a resource: on
1.4.x data, 10 of the 14 kingdoms in taom_spkingdoms.xml have one.

Not-tested: in game, counter leaves after the hero loses the resource
Ported-from: 406dd739f4b0c7211b7e146ea8dc814ffe3bd87c

#### fix(resources): v2.0.28 - map-bar resource shows on the first frame

`f96ec89c`

The culture resource counter on the map bar's bottom row now appears
with the rest of the bar when a campaign loads. Before, it popped in
about two seconds later on every load: the mixin skipped the map bar's
first refresh, which MapBarVM.Initialize runs itself, and the next
refresh comes two seconds after the bar is built.

The entry is still added only once (a Contains check), and it comes
back if the bar ever rebuilds its list. A hero whose realm has no
resource still gets no entry.

Research: taom-src v1.4.8 MapBarVM :237-251, MapInfoVM :138-153
Not-tested: in game, counter visible on the first frame after a load
Ported-from: fee906345b53cac93ae22193ff310b86203eb699

#### fix(ui): v2.0.28 - clan colours reach portraits, refuge rows keep names

`6088bf38`

Portraits now show clan colours. Every portrait patch of Banner Color
Persistence set the colours on a portrait code after the game had
already built the text the portrait is drawn from, so no clan colour
reached the party screen, the clan screen, hero panels or the Order of
Battle. The code is now rebuilt with the clan colours, and the Order of
Battle colours the hero pool as well as captains.

Refuges on the clan screen keep their "(being raised)" name, the
hidden wage slider and the zero wage line after the screen opens: the
clan screen refreshes every row after building the list, which reset
them. A postfix on ClanPartyItemVM.UpdateProperties now reapplies the
decoration on every refresh.

The party screen's portrait cache now stores the recoloured code; only
its comment changes.

Research: taom-src v1.4.8 CharacterCode :18, :87; ClanPartyItemVM :1125
Not-tested: in game, portraits, Order of Battle pool, refuge row name
Ported-from: 365f2ecfef13d435689138992ec866dd18784de8
Ported-from: 963218f7fe104ee848066086e1481dfafde51994

#### fix(balance): v2.0.28 - Lindon troops survive like Rivendell's

`fae65880`

Parties and garrisons of Lindon's culture now get Rivendell's survival
bonus: a soldier struck down in battle dies 40 percent less often. The
bonus was lost when Lindon became its own culture (cc1713eb3), because
Lindon's culture id never reached the table keyed on Rivendell's. Saved
campaigns pick it up on load.

A new test fails if the shipped config loses Lindon's row or it stops
matching Rivendell's.

The other half of the 1.5.x fix, Lindon's 22 elite troop weights and
the twin test that guards them, is held back until the public Armory
is installed, because those rows name troops (D25 rule 2).

Research: TaomPartyHealingModel.ApplyCulturalSurvivalBonus reads by id
Not-tested: survival in a live campaign
Ported-from: 0e75ec50a34e33fd7f4a6b4c61a3cd15e1330c3d

#### fix(enlistment): v2.0.28 - duty text has one source, in every language

`0628be1c`

A player using English read "a task for the company" for every field
duty order and "It went well." for every result, because the game
shows English straight from the text in the code and never reads the
translation file. Each duty's words now live with the duty in the
enlistment duty file, with their translation keys, so English players
see "a reconnaissance sweep" and the line the duty was written with.
A tool keeps the translation file in step with the duty file; every
key and English line is unchanged, so no translation moves. A duty row
with missing or malformed words is skipped with a warning naming the
field.

Research: v1.4.8 LocalizedTextManager.LoadLanguage reads strings only
when the language is not English (:235, :247)
Not-tested: the duty toasts and board in English and German, in game
Ported-from: a20b4d1540b782bde877b0333070b55aa38ea402

#### fix(kingdoms): v2.0.28 - drop dead policy ids, fix Gondor's policy

`0681029d`

Kingdoms no longer list two policies the game does not have. All 14
kingdoms in taom_spkingdoms.xml listed Lord's Prerogative and
Religious Privilege; Bannerlord 1.4.8 defines neither id and drops
them without a message, so nothing changes in play.

Gondor's only kingdom policy used the policy's display spelling
instead of the engine id (policy_land_grands_for_veteran), so Gondor
started without it. New campaigns now start Gondor with Land Grants
for Veterans: settlement militia veterancy +10%, village tax income
-5%. A saved campaign keeps the policy list it saved.

A new test fails on any kingdom or culture policy id the game does
not define.

Research: taom-src v1.4.8 DefaultPolicies.RegisterAll (32 ids, :145-176)
Research: taom-src v1.4.8 Kingdom.Deserialize (Kingdom.cs:801-812)
Save-compat: no new data; Kingdom._activePolicies is saved (:79-80)
Not-tested: Gondor's policy list in a new campaign
Ported-from: 685db7e46cfae268fe3ac765cab7ac6e1468cc84

#### fix(config): v2.0.28 - list entries trimmed, formation names only

`9b28cdd1`

The DreadAura, UncapturableHeroes and BannerBearers config loaders now
drop a null or blank list entry with a warning and trim a padded one.
Before, a padded name matched nobody without a word, and a padded
excludeHeroIds entry left its hero uncapturable.

BannerBearers' AllowedFormationGroups kept anything Enum.TryParse
accepted (" infantry ", "2", "Infantry, Ranged"), which the service's
name compare never matches: as the only entry it switched every banner
bearer off. It now takes declared FormationClass names only, through
the new TAOM.Core.Validation.EnumNames, stored as the enum prints them.
No change with the shipped configs.

Not-tested: in game (no behaviour change with the shipped configs)
Ported-from: b2e387db7e4e2ed074fceb1edaa33427af29ab9f

#### fix(banner): v2.0.28 - inventory doll keeps the clan colours

`138d4ea2`

With Banner Color Persistence on, the paper doll in the inventory now
keeps the clan's colours every time it redraws. Before, only switching
to another character showed them: opening the inventory, toggling the
outfit, Reset, loading an equipment preset and the Quick Actions all
repainted the doll in the kingdom's colours. In stealth mode the doll
keeps the game's own stealth colour.

The postfix moves from UpdateCurrentCharacterIfPossible to the private
UpdateCharacterArmorColor, the one step that paints the doll, which
every UpdateRightCharacter call reaches.

Research: taom-src v1.4.8 SPInventoryVM :4377-4381, :4421-4432
Not-tested: in game, doll colours on open, outfit toggle, preset load
Ported-from: 97426dcd07987a680d7a0e19a55cff634cffc2f6

#### fix(quickactions): v2.0.28 - drop the "include equipped" sell options

`8655a8a4`

The MCM options "Sell Damaged Equipped" and "Sell Low Value Equipped"
are gone. They never did anything: the two sells take their items from
the inventory list, and gear a hero wears is never in that list, so
turning either option on sold nothing extra. Quick sells still never
sell equipped gear; run Unequip All first, then the sell.

Saved MCM settings load as before: the two old entries are ignored and
drop out on the next save. Both options were player-local, so the
co-op settings fingerprint is unchanged.

Save-compat: MCM file only; no save data involved
Not-tested: in game, the two options gone from MCM
Ported-from: cd999f9176bd9ce8600a87ea96d86162775db849

#### fix(quickactions): v2.0.28 - batch sells remove sold-out rows

`b413f1f5`

Quick Actions Sell Damaged and Sell Low Value now remove a row from
the item list as soon as they sell its whole stack. Before, the row
stayed at a count of 0 until another row was clicked.

The batch sell passes cameFromTradeData, which skips the game's own
prune of empty rows (v1.4.8 SPInventoryVM.cs:3565-3568). The refresh
that Quick Actions runs after every batch now ends with that prune,
once per batch, through the helper the preset load uses.

Research: taom-src v1.4.8 SPInventoryVM ProcessSellItem :3565-3568
Not-tested: in game, Sell Low Value on a full stack
Ported-from: eb89a3e53a9dfa30c95cf2d057b568f24b3956bb

#### fix(equippresets): v2.0.28 - preset load refreshes the paper doll

`8716f06b`

Loading an equipment preset with the inventory open now updates the
paper doll, armour, weight, capacity, the no-saddle warning and the
Done button at once, and a row whose last item the preset equipped
leaves the list instead of staying at a count of 0.

The load called the inventory's label refresh, which on v1.4.8
redraws text only. It now runs the same panel refresh as Quick Actions
Unequip All through one shared helper, InventoryVMAdapter.RefreshPanel:
the paper doll and the information panel, then the removal of empty
rows.

Research: taom-src v1.4.8 SPInventoryVM :3182, :3437, :3475-3486
Not-tested: in game, preset load with the inventory open
Ported-from: 89e7af6c0b9e7e3911afcc25a462eaf2736538b0

#### fix(quickactions): v2.0.28 - Unequip All updates the open inventory

`2c8f156e`

Quick Actions "Unequip All" now moves the main hero's battle and
civilian gear through the open inventory screen's own transfers and
then refreshes the character panel. The gear shows in the item list
at once, the slots empty, and the paper doll, armour, weight,
capacity, the no-saddle warning and the Done button update without
reopening the screen. Cancel and Reset undo it like a manual unequip.

Before, the service wrote the equipment slots directly, so the open
screen kept showing the old gear. The panel refresh calls the two
private methods the game runs when the shown outfit changes, bound
once and pinned by the reflection binding gate. The unreachable
direct-write fallback is removed.

Research: taom-src v1.4.8 SPInventoryVM :4377, :3968, :3840, :3494
Not-tested: in game, Unequip All with the inventory open, Reset, Cancel
Ported-from: 1888b6f2804a2e80337e20b8d906cbc043e91433

#### fix(careers): v2.0.28 - career screen shows the root perk, keystones

`41a309ce`

The Active Effects panel left out the career's root perk and the
passives of keystone picks, although the passive service applies
both. It now sums every stored choice once, keystones included, plus
the root when it is not stored (the rule CareerPassiveService uses),
and the header shows the root perk's text under the career
description. A new prefab test pins every binding of the career
screen and the new line's place.

Not-tested: the header line and the panel totals on the career screen
Ported-from: 23799be827f64d2f5a4b5f6743bbf4d03d202abe

#### fix(enlistment): v2.0.28 - no battle encounter is left without a way out

`ed1f490c`

Four rare states could leave an enlisted soldier stuck after a battle:
- when the battle menu failed to open after he had joined his
  commander's battle, the join was rolled back, which could freeze the
  battle or pull him out of it; he now stays and the game opens its
  own battle menu;
- a leftover joined-battle encounter kept through a save was never
  closed once he was back with the column; the stale-battle recovery
  now closes it;
- inside his commander's town, after attackers retreated from an
  assault, service waited on that battle for the rest of the siege; the
  recovery now hands him back without closing the town encounter, and
  the next assault forgets the old result first so it is fought;
- switching Enlistment off in the mod options mid-battle could pull
  him out of it; the release now waits until the battle is over.

Research: v1.4.8 PlayerEncounter.IsJoinedBattle (:253), static
CampaignBattleResult (:267-277), the siege continue check (:1853,
:1860-1866) and ContinueBattle (:1287-1295), FinishEncounterInternal
(:1091-1093) and Finish (:1082-1084); 1.4.x ownership rule R1b is inert
(GetOwnership never passes isBattleEncounter)
Not-tested: the four stuck states in game (plan 128 in-game checks)
Ported-from: 90035a799207619c134c1fe84d495fd8cde6d50b

#### fix(careers): v2.0.28 - mount health perks show in the effects panel

`a65940c5`

The career screen's Active Effects panel treated MountHealth and
InventoryCapacity as counts, but both are fractions (61 shipped
MountHealth pips of 0.10 to 0.15), so a pick rounded to 0 and printed
nothing. Both now print as percentages, and a test keeps the panel's
count list equal to the one the perk report uses.

Not-tested: a MountHealth pick shows "+10% mount health" on the screen
Ported-from: bce566059fa0d9b0caeee51d18e1723cef921af0

#### fix(career): v2.0.28 - an heir no longer inherits a stale career quest

`499fd85d`

When an heir takes over after the player's hero dies or retires,
vanilla cancels every running quest except special ones, and a career
quest is special, so the old hero's quest kept polling the old hero's
skill forever. Before each offer the career quest behaviour now
cancels a running career quest whose owner is not the main hero, with
vanilla's own reason text, and the heir gets the normal offer. Each
offer gets its own StringId, because the engine keys a quest's journal
record by StringId. It cancels only in single player.

The single-player predicate CoopSessionPolicy.IsSinglePlayerAuthority
comes along from the 1.5.x line with its test.

Research: taom-src v1.4.8 QuestManager.OnPlayerCharacterChanged
 skips special quests (:282-292); QuestBase.CompleteQuestWithCancel
 (:223); JournalLogEntry.IsRelatedTo matches StringId (:119-122);
 CampaignTime.ToMilliseconds (:132)
Not-tested: an heir takes over during a career quest: the old quest is
 cancelled and the heir is offered their own
Save-compat: quests already started keep their old StringId; new
 offers use the longer one
Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3
Ported-from: 18069643062cd6fb0312ca33ecca015f13ef222e
Ported-from: 113d3483bb1c6b303546e96c63e369eb2d920d5d

#### fix(enlistment): v2.0.28 - vassals stay out of their lord's new wars

`b2968af2`

An enlisted soldier whose clan serves a kingdom could follow his lord
into a battle against a faction his own kingdom was not at war with.
Choosing Attack or Send troops there declared war for his whole
kingdom, and ending his service never undid it. Such a soldier now
sits that battle out and keeps serving; he joins his lord's next
battle against a faction his kingdom does fight.

Research: v1.4.8 BeHostileAction.ApplyEncounterHostileAction declares
war for attackerParty.MapFaction (BeHostileAction.cs:192-195); the 1.4.x
enlist gate refuses only a lord at war with the player's kingdom
(EnlistmentDialogGateService.cs:47-53)
Not-tested: a vassal under a foreign lord meeting a faction at peace
with his kingdom, in game
Ported-from: d4af27d3e464b51eb50de410e28fe081643a5db9

#### fix(career): v2.0.28 - career button keeps one click handler (#971)

`031af9b8`

The career button had a second click handler in its view-model mixin
that never ran: UIExtenderEx adds a mixin method to the view model
only when it carries [DataSourceMethod]. The dead method is gone and
the Patch27 postfix, the handler that always ran, says so. The click
sound was never missing: the postfix's ExecuteDone runs the developer
screen's close callback, which plays it. New IL tests pin the order of
the two calls, the absence of a second sound and vanilla's click.

Research: UIExtenderEx 2.13.2 ViewModelComponent adds only
 [DataSourceMethod] methods; taom-src v1.4.8
 CharacterDeveloperVM.ExecuteDone (:731-735),
 GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen plays
 event:/ui/default (:139-143)
Not-tested: one click sound, and the career screen opens
Ported-from: 0761c01b75d58d46eb6f97e06d94db568ac33d53
Ported-from: 7100f7d8d30816087a20827dc4286d7d77aef5e5

#### fix(messengers): v2.0.28 - messengers no longer move armies or troops

`ba54e160`

A conversation through a messenger let you do things that need you to
stand beside the lord. Agreeing to join his army snapped your party
across the map to him, asking him to join yours did the same to his
party, swearing service moved you to his camp, and a few lines handed
troops, goods or a companion over at a distance.

Through a messenger these are now hidden: joining his army or asking
him to join yours, the board game, trading troops and goods with a
party of your clan, and calling a companion back. The offer to take
service is greyed with a reason: swear it in person. Barter and hiring
a wanderer still work through a messenger.

A messenger to someone in a town where your army's leader is, while
you belong to that army but are not marching with it, used to attach
your party to the army in that town. Such a talk now happens in the
field. If anything goes wrong while the conversation opens, the game
cleans up as after a normal talk. The new greyed-oath line is English
in every language until it is translated.

Research: v1.4.8 LordConversationsCampaignBehavior.cs:2044, :2622
Research: v1.4.8 EnterSettlementAction.cs:71-75, PlayerEncounter :1082
Research: v1.4.8 LordConversationsCampaignBehavior.cs:334-336, so the
Research: "follow us" claim in the Patch87 comment is false; comment fix
Not-tested: the in-game checklist in docs/features/messengers.md
Ported-from: 1dc0a8d7c552cf56341cdaedface37da90ded9c1

#### fix(careers): v2.0.28 - promoted-culture careers get their archetypes

`bf176564`

The 18 careers of the six cultures promoted in cc1713eb3 (goblin,
Misty Mountains, Blue Craig, Lindon, Shaghana, Abanissa) had no
archetype, so their battle ability played its toast and cooldown and
applied nothing. Each now maps to the archetype of the career it was
cloned from, and a test fails when any career in taom_careers.xml has
no archetype.

Not-tested: a goblin, Lindon or Abanissa career ability applies its
 buff in battle
Ported-from: b02467744d0edbf63dc0774131c3d8f199014577

#### fix(field-commission): v2.0.28 - promote every soldier race (#612)

`c1d4d649`

Battlefield promotion allowed only human, dwarf and elf soldiers, so a
player of any evil faction never saw an offer. The default list now
holds every soldier race (human, dwarf, elf, orc, uruk, uruk_hai,
pale_uruk, dg_uruk, goblin, berserker); creatures and unique heroes
stay out. The shipped JSON, the provider's fallback and the config
default are pinned together, every name is pinned to the races
race_age_config.json lists, and a configured name the engine does not
know logs one warning.

Not-tested: an Isengard or Mordor campaign gets a promotion offer
 after a won fair battle
Ported-from: 35007dbb8d0324efd71a49ce356636262225d9ab

#### fix(resources): v2.0.28 - hideout send-troops clear pays once (#790)

`f194f62e`

A "send troops" hideout clear raises the completed event twice, from
the success menu and again when the map event is finalized, and both
paid the hideout reward. Only the finalized event pays now, so the
reward is paid once, also after a save made in the success menu.

Research: taom-src v1.4.8 HideoutCampaignBehavior (:229) and
 HideoutEventComponent.OnBeforeFinalize (:59-64) both raise
 OnHideoutBattleCompleted; MapEvent.FinalizeEventAux sets
 WaitingRemoval (:2078) before BeforeFinalizeComponent (:2138);
 IsFinalized (:225)
Not-tested: one send-troops clear writes one [SpecRes] HIDEOUT: line
Ported-from: bc59806f097a749d3125ea43fe50a911cc49a2bd

#### fix(messengers): v2.0.28 - send-messenger button gets its button look

`1786e4dd`

The Send Messenger button drew as a plain box: it named two brushes no
module defines, and the game falls back to a default style without a
warning (#989). It now uses the game's popup button brushes, with
their hover and pressed looks, and its label dims with the button when
no messenger can be sent.

A new test checks every brush a TAOM prefab extension names against
the brushes the engine loads for TAOM's module set, so a wrong brush
name fails a test instead of shipping unstyled.

Research: v1.4.8 Native/GUI/Brushes/Popup.xml:2 and :195 declare both
Research: v1.4.8 ButtonWidget.cs:141 needs UpdateChildrenStates
Not-tested: the button look in game
Ported-from: 2c7f7fae934b015857e8a3be6f30bde8ba122962

#### fix(resources): v2.0.28 - raids stop paying the battle credit (#770)

`1eace0ac`

A raid paid the battle credit on top of the raid credit, and a raid
where the militia fought first paid the raid credit twice. A raid now
pays the raid credit once, when the engine marks the village looted,
and only to the winner. A battle inside a raid or an extortion pays
the battle credit only when a real field party (neither militia nor
villagers, with healthy troops at the start) was beaten. Tournament,
hideout and raid messages show the amount actually credited. The
ported test lists v1.4.8's eleven battle types, without the 1.5.x
SiegeAmbush.

Research: taom-src v1.4.8 MapEvent.BattleTypes (:24-37), EventType
 (:196), MapEventParty.HealthyManCountAtStart (:63),
 RaidEventComponent.OnBeforeFinalize sets Looted, then raises
 RaidCompleted (:128-138)
Not-tested: a raid with a lord inside, defending a village, a raid
 against militia only
Ported-from: 30cbcd465844e98388123274f2fa7e5a0887277c

#### fix(careers): v2.0.28 - party size picks apply at once, own party only

`494b3165`

A PartySize career pick now changes the party limit at once (#768)
instead of after the next roster change: a passive refresh bumps the
cached limit of the parties the affected heroes lead. The bonus goes
only to the party its hero leads, so caravans, garrisons, militia,
villagers, patrols, supply caravans and refuges of the owner's clan
no longer get it, and it is added after every percentage factor, so
the AI lord party size setting no longer multiplies it. A party filled
to its old limit may be over the new one on an existing save; vanilla
desertion trims it over a few days.

The Rohan "Garrison Commander" text now reads "Your company swells
with volunteers"; its 12 translations show the English, and its cached
translations are removed so the next translation run redoes them.
The taom.career_perks report prints the engine's cached party limit.

Research: taom-src v1.4.8 PartyBase.PartySizeLimit caches on
 MemberRoster.VersionNo (:343-355); TroopRoster.UpdateVersion (:731);
 MobileParty.All (:258), LeaderHero (:776), Army.LeaderParty (:118)
Not-tested: the instant limit change and the console lines in game
Ported-from: c74ad5aed0c950247c0d187e21019b563885aa02

#### fix(messengers): v2.0.28 - greyed Send Messenger button says why

`cb9cff5e`

The Send Messenger button on a hero's encyclopedia page now explains
itself when it is greyed out: hovering it shows why. Before, nothing
appeared, because the game never hovers a disabled widget and the hint
sat inside the button. The button now sits in an always-enabled
wrapper of the same size that holds the hint, the shape the vanilla
governor button uses. A failure while checking availability also gives
the "not available" reason.

Research: v1.4.8 EventManager.cs:859-897 skips disabled widgets
Research: SandBox TownManagement.xml:105-111 governor button shape
Not-tested: the hint on a greyed button in game
Ported-from: 6647f8f577eaac144206df9d5be52f22cd8a253d

#### fix(resources): v2.0.28 - career gain scales every earning (#767)

`cd7d3dad`

The SpecialResourceGain career passive scaled only the daily town
income, so a hero without towns got nothing from it. It now scales the
battle, raid, siege, prisoner, tournament and hideout earnings too, and
the map bar tooltip shows the scaled amounts. Daily upkeep is computed
in one place for the tooltip, the daily tick and desertion; a troop
whose career-reduced upkeep is zero no longer deserts, and an unusable
upkeep modifier falls back to the configured upkeep.

Not-tested: the scaled earnings and the tooltip rows in game
Ported-from: 01ca483e2e6538a8ac508eb288c06b5939e099fe

#### fix(careers): v2.0.28 - link each career choice to its group (#766)

`aef43480`

Career choices took their group from a group_id attribute that no
nested choice carries, so all 2,010 nested choices loaded with an
empty group. The tier lock on the career screen, the one keystone per
tier rule and the save repair for choices left from another career
never applied, and a level 1 hero could take a tier 3 choice. Each
choice now takes its group from the ChoiceGroup that holds it. An id
claimed by two careers gets no owner and the repair keeps it, and a
shipped-data test fails on such data. The player sees one message
when a repair refunds choices; its 12 translations show the English
until the next translation run.

Not-tested: the click refusals and the refund message in game
Ported-from: 259ae2cba5e460a59d9b27415911b30fa825e199

#### fix(enlistment): v2.0.28 - soldiers stay out of their lord's raids

`7e1b25ba`

An enlisted soldier was pulled into every village raid his commander
led, as if it were a battle. He stayed in battle state for the hours a
raid lasts, could not ask for release, and ended in the raid menu with
its Abandon Army and Leave Army exits. A raid, forced supplies or
forced volunteers that the commander leads is no longer a battle to
join: the soldier keeps marching with the column, release stays open,
and the service board says "The column is raiding." A commander who
defends his own village still takes the soldier into that fight, and
sieges, sally-outs and field battles join as before. The new line
shows in English until the next translation run.

Research: taom-src MapEvent.IsRaid/IsForcingSupplies/IsForcingVolunteers
(MapEvent.cs:205-209), PartyBase.Side (:329); forced events are created
only from PlayerEncounter.cs:861/865
Not-tested: a commander's raid while enlisted, in game
Ported-from: 7a912dcd544c1c3560b52de2df571f406131a782

#### fix(enlistment): v2.0.28 - a way out of the service menu after discharge

`13c1bf4b`

A discharge that runs while a save loads (Enlistment switched off in
the mod options, or the identity check) left the saved service menu
on screen with every option hidden, and the campaign could not go on.
The menu no longer hides itself: each option checks whether anyone is
serving, and when nobody is, a single 'Go on your way' option leaves
the menu, into the town menu when the player stands in a town. The
board then reads "Your service has ended." The two new lines show in
English until the next translation run.

Research: taom-src GameMenu.GetMenuOptionConditionsHold (GameMenu.cs:
158-168, a wait menu ANDs its condition with every option's) and
RunMenuOptionConsequence (EndWait before an isLeave consequence, :276)
Not-tested: a discharge during load, in game
Ported-from: b24e8549e88e8b0b465bc1615f4335855a3486d9

#### fix(enlistment): v2.0.28 - no walking out of the commander's battle

`7dd6c9f6`

An enlisted soldier could still take vanilla 'Leave' or 'Abandon army'
in his commander's battle. Either one stranded service in its battle
state, and 'Abandon army' also left a stray army around the commander.
The encounter menu now hides both while the soldier serves in the
commander's battle and does not lead his side. When a safe-passage
truce greys out both 'Attack' and 'Send troops', the exits come back,
so the menu always keeps a way forward. A player's own battles keep
every exit.

Research: taom-src MapEventHelper.CanMainPartyLeaveBattleCommonCondition
(MapEventHelper.cs:109), its three EncounterGameMenuBehavior readers
(:1566, :1638, :1877), MenuHelper.CheckEnemyAttackableHonorably (:452),
MenuCallbackArgs(MenuContext, TextObject), MobileParty.SiegeEvent
(:1132)
Not-tested: the hidden exits and the truce release in a live battle menu
Ported-from: 2d1e5f3385f871c5a829611641230266b87fb763

#### fix(recruitment): v2.0.28 - AI castle toggle covers entry recruits

`289b7a5c`

"AI Recruits From Castles" off now also stops AI lords taking castle
volunteers when they enter a castle, not only on their hourly recruit
(#918). Every AI lord recruit asks the volunteer model, so the castle
rule sits there, before the alignment rule. With the setting or
Castle Recruitment itself off, an AI lord recruits nothing from a
castle notable. The player's own party is never blocked; parties of
the player's clan led by a companion count as AI lords. Garrisons keep
their daily recruit, which uses a different engine call.

The 1.5.x change also paused the hourly castle recruit during a co-op
session (#920). That part is not ported: co-op is not supported on
this line.

Research: v1.4.8 RecruitmentCampaignBehavior.cs:504, :522, :563, :601
Research: v1.4.8 GarrisonRecruitmentCampaignBehavior uses its own index
Not-tested: entering a castle with the AI toggle off
Ported-from: 0d3f2bc88e664e10d62589b114757eed2609f117

#### fix(career): v2.0.28 - typed resistances, ammo, perk diagnostics (#613)

`1d51c44c`

Five resistance pips authored a Blunt or Cut mask, which the mask enum
did not have, so they parsed as All and resisted every hit. The mask
now has a damage-kind axis (Cut, Pierce, Blunt) read from the engine's
damage type, corrected the way vanilla corrects it: a bare hand, a
haft, a kick or bash, a fall and a horse charge count as Blunt. An
unknown mask name warns at load and leaves the pip inert.

The Ammo passive skipped thrown weapons and pushed stacks above an
unchanged maximum after spawn. It now runs where vanilla's own extra
ammo perks run, in InitializeMissionEquipment, raising the maximum with
the amount, and covers javelins.

The new console command taom.career_perks lists every passive the
player holds, where it is consumed and what the engine holds for it,
and the debug log carries [CareerPerks] lines for stat, mount and ammo
application.

Research: taom-src v1.4.8 MissionCombatMechanicsHelper (:157, :192),
 AttackCollisionData.DamageType (:112), MissionEquipment.SetAmountOfSlot
 (:506), SandboxAgentStatCalculateModel.InitializeMissionEquipment
 (:93, SetAmountOfSlot at :188), WeaponComponentData.IsAmmo (:99)
Not-tested: taom.career_perks in a campaign and a battle; a blunt
 resistance career hit by a mace, a sword and a horse; a javelineer's
 stack at spawn
Save-compat: none, mission and console only
Ported-from: 7ede923be86130f4d74a274397350df1b3f2c7e1

#### fix(career): v2.0.28 - cavalry mount bonuses reach the mount (#611)

`a92da65a`

The MountChargeDamage career passive and the mount half of the Cavalry
ability (mount speed and charge damage) were multiplied on the rider's
own driven properties. No base model writes those two values on a
human and the engine reads them off the horse, so the 92 pips and the
Cavalry ability's mount buff never did anything.

ApplyMountStatModifiers now applies the passive and both buff sources
on the mount's properties, keyed on its rider. The stat model calls it
after base has rewritten the mount, and the ability and fallen-hero
paths refresh the rider's mount beside the rider.

Research: taom-src v1.4.8 SandboxAgentStatCalculateModel
 UpdateHorseStats (:1216, :1221, :1230), AttackInformation (:335),
 Mission.ChargeDamageCallback (:6034), Agent.RiderAgent and MountAgent
Not-tested: a Cavalry career ability and a MountChargeDamage pick in
 battle (the horse's charge damage and speed rise)
Save-compat: none, mission-time only
Ported-from: 2b1d1ef96633676a130cd9064e5d112ace584a12

#### fix(troopweight): v2.0.28 - garrisons and patrols pay no size penalty

`27ab0ec1`

Garrisons and patrol parties no longer pay the troop-weight size
penalty. The penalty shrank a garrison's limit by the weight of its
elite troops; vanilla desertion then removed a quarter of the excess
each day and morale fell with it, while lords refilled the garrison
to its full, unweighted limit. A patrol is refilled at full size and
nothing trims it, so an elite patrol sat over its reduced limit for
good, under the over-limit speed penalty.

One predicate, TroopWeightPenaltyScope.PaysSizePenalty, now decides
for the penalty and for every capacity display, so the party and clan
screens show a garrison's or patrol's raw count against its real
limit. Lord parties and caravans are unchanged.

Research: v1.4.8 DefaultPartySizeLimitModel.cs:81-96 routes garrisons
Research: and patrols; DefaultPartyDesertionModel.cs:54-65 trims
Not-tested: an elite garrison over a month; an elite patrol's speed
Ported-from: c2449a306c0f813fdebc641f9907b16a045a0b13

#### fix(creatures): v2.0.28 - cap one behaviour tree walk at 1,000 nodes

`67ecf055`

BehaviorTree.RunTree walks nodes until the walk returns to the root
or reaches a node that waits, and nothing bounded that loop. No
reachable cycle is known, but a future node or listener that left an
unexpected state would hold the mission thread for ever: a frozen
battle with no exception, the hardest report to diagnose.

RunTree now stops a tree after 1,000 node transitions in one call,
the way the exception path already stops one: IsRunning turns false
and the creature falls back to the engine's AI. The first runaway of
each tree type is logged through the tree logger.

Research: Main/BehaviorTrees/BehaviorTreesCore.cs RunTree, identical
on both lines; Selector and Sequence in BehaviorTreesNodes.cs
Not-tested: a live runaway (none is known to occur)
Save-compat: no saved state touched

#### fix(crashreport): v2.0.28 - crash bundles list only TAOM's settings

`83ca27ff`

Crash bundles were meant to list TAOM's Mod Options values but listed
none, and the collector read every mod's settings classes during crash
handling, which can run another mod's code at the worst moment and copy
its free-text settings into a file players upload. Bundles now list
TAOM's four settings pages, skip other mods' settings by design (the
bundle already names every mod and version), and show a dropdown's
selected value.

Research: Dropdown<T>.SelectedValue indexes the list by SelectedIndex and throws out of range, handled (read on MCMv5 5.12.1; the pinned version is 5.12.3)
Rejected: the 13fb30f9c dedicated-server settings source and fingerprint float bits (co-op, D7)
Not-tested: in game, trigger the QA crash and read the bundle's MCM Settings section
Ported-from: 13fb30f9c9579c3f8cb3ecaf87a0a38f63aa34fc
Ported-from: a59a48bb6a8464f854e56c4c5a4964fcb602c409

#### fix(refuge): v2.0.28 - a refuge defender's mount shares the reduction

`d7c86dcd`

A refuge's defenders take less damage, but their horses did not: the
refuge code looked at the struck agent's own origin, and a mount has
none. A mount hit now counts for its rider's party, as the career
passives already do, and a riderless mount gets nothing.

Research: taom-src v1.4.8 AttackInformation sets VictimRiderAgentOrigin from the rider (AttackInformation.cs:286); PartyAgentOrigin.BattleCombatant => Party (:48)
Not-tested: in game, a refuge defence with a mounted defender (Blow Diagnostics shows the horse taking reduced damage against the MCM bonus at 0)
Ported-from: bb68eeca3aeee49ba95ea4956f2b3941820a3f93

#### fix(diagnostics): v2.0.28 - log the campaign date once the clock is set

`9aa26792`

The session snapshot in every TAOM log printed "time read failed:
DivideByZeroException" on a save load, because it runs before the game
sets up its calendar. It now says where the date is logged, and a new
line writes the in-game date and hero right after the load finishes, so
a log that stops before the first battle still carries the date.

Research: taom-src v1.4.8 Campaign.OnInitialize calls OnGameStart (Campaign.cs:1391) before CampaignTime.Initialize (:1394) and OnGameInitializationFinished last (:1452)
Not-tested: in game, load a save: the session snapshot shows date=<logged at game init> and a "Campaign at game init: now=" line follows

#### fix(enlistment): v2.0.28 - the enlistment trace ships off again

`c7d9bd80`

The routine enlistment trace was switched on for a test in late August
and shipped on in every release since; it buried crash-report lines.
It is off again under a new MCM name ("Enable Enlistment Trace"),
because MCM keeps a saved value per setting name and a flipped default
would not have reached players who saved settings. Real faults always
log.

Save-compat: the old key is ignored on load and dropped by the next settings save
Not-tested: in game, an enlisted session logs no routine [EnlistDiag] TICK or SYNC line
Ported-from: 890fe29dec3a44a4a706376089e8dc375b65e692

#### fix(logging): v2.0.28 - auto-resolved battles are no longer logged

`53755ae2`

Auto-Resolve Diagnostics is removed. It wrote one record for every
battle fought on the campaign map, was on by default, and wrote each
record to disk on the campaign thread; in one 35-minute 1.4.8 session
it was 39 percent of the TAOM log. Its balance study is finished. The
two MCM switches ("Log Auto-Resolved Battles", "Log Troop Census") and
tools/analyze_battle_logs.py go with it. Only the auto-resolve removal
of 6ae924638 is ported.

Research: git grep of the 1.4.x tree finds no consumer of the removed types; the behaviour's SyncData was empty
Save-compat: nothing was saved; a TAOM.json value for either removed switch is ignored
Not-tested: in game, a campaign session writes no [AutoResolve] line and MCM shows no Auto-Resolve Diagnostics group
Ported-from: 158408c7350f4de661901ae17a81bd852d22113e
Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3

#### fix(tools): v2.0.28 - lord generator only adds, never rewrites

`d0dc5b92`

complete_lords_xslt.py --apply rebuilt lords.xslt from scratch and
lost everything it did not generate itself: lord equipment, the race
attributes, hand layout, explicit genders and the byte-order mark.
Nothing ran it automatically, so no shipped data was hurt.

The tool now never rewrites a byte already in lords.xslt. It only
inserts a template for each vanilla lord the file lacks, in that
lord's faction section, and keeps the BOM and line endings; on the
1.4.x file it adds nothing. --dry-run says how many lords --apply
would add and lists vanilla attributes no template carries; an empty
vanilla lords file stops it. lords_inventory.csv is regenerated from
1.4.x data and gains a race column. The 1.5.x test that needs
sync_lord_inline_skills.py stays behind: that tool is not on this line.

Not-tested: an --apply run that adds a lord (the tests cover it on
 copies)
Ported-from: 9165a672781e9996bb109cc1fc3ce99e30f4f32c

#### fix(logging): v2.0.28 - flush TAOM.Dependencies startup lines

`b0d776ad`

TAOM.Dependencies loads before TAOM and holds its startup lines in
EarlyLog until TAOM hands it a logger. Nothing ever did, so its static
init, the Harmony fork version, every AssemblyResolve redirect, the
UnpatchAll guard, UIExtenderEx's start-up, a second 0Harmony.dll and
any shim install failure were dropped on every launch: the Harad
player's 2026-11-01 taom_debug log has no [TAOM.Dependencies] line.

TAOM now connects EarlyLog in OnSubModuleLoad right after the
container is configured. Buffered lines come out in order at their own
level, each starting with "[buffered HH:mm:ss]"; later lines go
straight to the log. A line logged on another thread during the flush
queues behind the older ones, and a failing logger costs only its line.

Not-tested: in game (the [TAOM.Dependencies] lines in taom_debug.log)
Ported-from: d31026b5becadfa49e4baf57dd867611ce41cbf5

#### fix(deps): v2.0.28 - BUTR stack refreshed with 1.4.6 to 1.4.8 builds

`c7049363`

TAOM.Dependencies shipped ButterLib 2.11.0 and MBOptionScreen 5.12.1
with per-engine builds only up to game 1.4.5. Both loaders take the
build that matches the running engine and otherwise the latest they
find, so v1.4.8 has run the 1.4.5 builds. This refresh brings
ButterLib 2.12.0 (implementations 1.4.6, 1.4.7 and 1.4.8 added, the
six older ones re-copied), MBOptionScreen and MCM.UI.Adapter 5.12.3,
NuGet Bannerlord.MCM 5.12.3 and Bannerlord.UIExtenderEx 2.13.3 in both
csproj files, the ButterLib stub at v2.12.99.0, the licence notice and
the maintenance record. The 1.5.x line's 1.5.1 builds are left out:
they would only ever be a wrong fallback here.

Research: decompiled ButterLib 2.11.0 and MBOptionScreen 5.12.1
implementation loaders; BundledDependencyManifestTests
Not-tested: in game (MCM opens, the session snapshot lists
 Implementation.1.4.8 and MBOptionScreen.v1.4.8, a dev-trigger crash
 writes a bundle)
Ported-from: 6027ecfae1f2be5bc57a1c8eeb33730f825cd81b

#### fix(mission): v2.0.28 - siblings leave dead OnBehaviorInitialize (#606)

`b93b8da0`

On v1.4.8 Mission.AfterStart sends OnBehaviorInitialize to the
behaviours already in the list (Mission.cs:3811) before it asks each
submodule to add its own (:3815), and AddMissionBehavior calls only
OnCreated (:4628). TAOM adds every behaviour in that second step, so
an OnBehaviorInitialize override on one never runs.

Three did their setup there. Smart Cavalry AI and Mixed Formations
only logged their settings; those lines now appear once per battle.
Siege Dismount never ran at all on this line. Its start now runs
from AfterStart, after every EarlyStart (where the siege flag is
set) and before the first agent spawns, so in a campaign siege your
horse and harness go to the inventory before you spawn and come back
after the siege, as the Siege Dismount options describe (on by
default, mode 3, Auto Remount).

MissionBehaviorLifecycleTests now fails any new TAOM override of
OnBehaviorInitialize. The one allowed override is
CustomBattleTeamFixBehavior, which a Custom Battle postfix adds
before AfterStart runs. The Harmony rule for Hooks/** states the
firing set.

Research: taom-src Mission.AfterStart (Mission.cs:3799-3836),
  AddMissionBehavior (:4615-4628), IsSiegeBattle (:1375),
  MissionCombatantsLogic.EarlyStart (:92-94); v1.4.8 dump
  DefaultBattleMissionAgentSpawnLogic.AfterStart (:158) and
  OnMissionTick (:171)
Not-tested: a mounted campaign siege (dismount and remount) and a
  Custom Battle siege assault in game
Ported-from: 2a5363e90981d3fe9c7cd241f8163f2c2beb0dde
Ported-from: fac17e05c5203abe38b1f2f0d33bd0847247b518
Ported-from: 9cb6d49e77168f04006330244599a29df321e519
Ported-from: bb85e92849a99252dfe0bc9bc1bfed3670c86734

#### fix(siege-dismount): v2.0.28 - guard adapters outside a campaign (#699)

`b8c88d37`

Siege Dismount read Hero.MainHero and MobileParty.MainParty, whose
getters throw when there is no campaign, which is every Custom
Battle. Its two adapters now read each step guarded and report no
mount outside a campaign, and the service's first adapter call sits
inside the same try as the others, so an adapter fault logs an error
instead of escaping mission start.

Nothing changes for players yet: the feature's start still sits in
OnBehaviorInitialize, which v1.4.8 never sends to a behaviour TAOM
adds. The next commit moves it to AfterStart; these guards land
first so that a Custom Battle siege cannot throw out of mission
start once it does.

Research: taom-src Hero.MainHero (Hero.cs:892),
  CharacterObject.PlayerCharacter (CharacterObject.cs:250),
  MobileParty.MainParty (MobileParty.cs:256), Game.PlayerTroop
  (Game.cs:54)
Not-tested: nothing beyond the unit tests; the code is unreachable
  until the next commit
Ported-from: cfca3c5fd24a127ed367527dfa8dcd1147f53770
Ported-from: bb85e92849a99252dfe0bc9bc1bfed3670c86734

#### fix(battles): v2.0.28 - end AI battles stuck at N vs 0 (#748)

`3205dd76`

An AI battle on the campaign map could freeze for good, for example
at 8000 against 0. Lords of the same faction kept joining it, the
whole faction stood still, and the player could join neither side. A
player traced the cause and sent a workaround module, Stuck Battle
Guard; this fix is written from their diagnosis.

The engine runs a battle round only while both sides have healthy
troops, and picks a winner only inside a round, so a side that drops
to 0 healthy troops between rounds never loses. A destroyed party
still attached also makes the battle refuse every joiner.

Once per in-game hour TAOM now checks every AI battle at least 12
in-game hours old. A destroyed party still attached is removed (a
quest's party is left alone). A side with no healthy troops loses:
the other side wins, with the usual prisoners and loot. When both
sides have none, the defenders hold. The player's own battle, an
enlisted player's commander's battle and raids are never touched,
and the check stands down in a co-op session. The console command
taom.print_stuck_battles lists every running battle with what the
check would do.

Known limitation: while the player is enlisted, a stuck battle
holding their commander is not resolved.

Research: taom-src MapEvent.Update (MapEvent.cs:995-1046),
  SetOverrideWinner (:1058), BattleState setter (:284-301),
  CheckIfOneSideHasLost (:1275-1290), CanPartyJoinBattle
  (:1447-1454), MapEventSide.RecalculateMemberCountOfSide (:319),
  PartyBase.MapEventSide (:289-322), MbEvent listener order
Not-tested: the hourly sweep on a live save
Ported-from: a913629cdb33b2b30e6aaf875146447abe1597bb

#### fix(battle-corpses): v2.0.28 - fade and cap battle corpses (#701)

`083f1f7b`

Players on 1.4.8 reported mid-battle freezes that stopped when they
lowered the vanilla Number of Ragdolls and Number of Corpses options.
In a normal battle a fallen soldier never times out: the engine's own
corpse timer is an hour, so bodies pile up until the corpse cap
pushes them out, and on Unlimited that is 1,021 bodies, which the
mission tick walks every frame.

Field battles, sieges and sally-outs now fade each body 60 seconds
after it settles and keep at most 25, through the engine's own
per-battle corpse settings. If your Number of Corpses option is
lower, yours is kept; TAOM never raises it. Stealth missions, towns,
arenas and hideouts keep their bodies. Both values are sliders under
Mod Options, TAOM, Performance, Battle Corpses, and apply mid-battle.

Ragdolls cannot be limited per battle, so on the first main menu
after the game starts TAOM offers to lower Number of Ragdolls to 5
and Number of Corpses to Low when yours are higher. Each option is
only ever lowered, nothing changes unless you click Apply, and an
MCM button does the same at any time.

Each battle also logs one [BattleSettings] line with your ragdoll,
corpse and battle-size options, so a freeze report shows what was
set.

Known limitations: the notice reads in English in every language
until the next translation pass. Whether removing a corpse frees its
agent slot, and whether a lowered ragdoll option applies before a
restart, are still to be checked in game.

Research: taom-src Mission.SetMissionCorpseFadeOutTimeInSeconds and
  SetOverrideCorpseCount (Mission.cs:1723-1731), NativeOptions,
  ManagedOptions, BannerlordConfig, OptionsVM.ApplyChangedOptions;
  v1.4.8 TaleWorlds.Native.dll engine methods 559 and 561 (0x70C160,
  0x70C1B0), mission reset 0x5CF3C0 reached from InitializeMission,
  corpse cap jump table 0x5D3BB4 (0, 25, 75, 125, 250, 1021)
Not-tested: the per-battle apply, the [BattleSettings] line and the
  main-menu offer in game
Ported-from: c17541f2c4595149ea22fe0e473397a513c20da9

#### fix(config): v2.0.28 - reject NaN and Infinity in mutation floats

`e83ebbee`

A career mutation's float parameter is read with float.TryParse, which
accepts "NaN", "Infinity" and "-Infinity". Such a value then passes
every later range check. MutationParams.GetFloat now returns the
default for them, as it already did for a missing or unparseable
value. No shipped XML holds such a value; this closes plan 002, whose
troop-weight half landed earlier.

Research: Main/Core/Validation/FiniteFloatValidator.IsFinite(float)
Not-tested: nothing in game (latent guard)
Ported-from: 78889a85c4558eae75127cfba85cdce46f45e0a1
Ported-from: 96832589679f84d0ea6f8b6e76cf2928932ab093
Ported-from: 8186cc9caee5da0ae82db588572469329b8c98d8

#### fix(adapters): v2.0.28 - read the main party through the campaign

`7810e374`

The engine's PartyBase.MainParty returns null only when there is no
campaign; with one it reads the campaign's main party and throws while
that is null. Eleven places null-checked it or read through it with ?.:
the messenger popup's hourly check, the encounter map-event checks,
the party gear roster, Unequip All, the recruitment screen's weighted
party size and the enlistment battle check. Each now reads the stored
main party through the campaign. Nothing changes while a main party
exists.

A source scan now fails if production code null-checks the getter
again, in any of its written forms.

Research: taom-src PartyBase.MainParty (:208-218)
Not-tested: no visible change in game
Ported-from: 91f3699843d167f8fc0950bde3c8db295ee61988
Ported-from: dc1c370316d40ab4fcb0438fe81f27308337e6cf
Ported-from: e9854bbd0864ac58fe7fcaab425ff63126f5f7ef
Ported-from: 18615680851ce800ccbb0f86278fe8aa421bced2
Ported-from: 2c09a414594fd18d00da80310d6e5cd02c56ace3
Ported-from: 74bdd5eff5f52b5717a209b2a2c531597f27a385
Ported-from: 4f27b3bdeac863f2c6cb99026c49f2225fdec011

#### fix(main-menu): v2.0.28 - main-menu setup never throws into the engine

`22197763`

Bannerlord calls every module's main-menu setup with no try, and calls
the whole chain again on every tick while no game state is active, so
one throw in TAOM's menu setup would re-run every module's setup on
every tick instead of failing once.

TAOM's menu customization now runs through MainMenuStep. A throw is
caught and logged once as a [MainMenuStep] WARNING with the full
exception, and the menu opens without the rename. The step is not
latched, so a later call can still succeed.

Research: v1.4.8 Module.OnApplicationTick (:500-509),
SetInitialModuleScreenAsRootScreen (:750-759)
Not-tested: none needed beyond a normal boot to the main menu
Ported-from: 85c608963ca6f31839071fea30ea76cdd6e28da3
Ported-from: 2afea8878a567c632d9dcb03e0594ec280c8873c

#### fix(bandits): v2.0.28 - guard bandit clans and dens that load apart

`56ff526e`

A bandit clan ships in TAOM and the dens of its culture ship in
TAOM_Map, and a saved campaign reloads settlements but not clans, so
the pair can arrive half. Two vanilla lookups crashed on the missing
half: a clan with no den of its culture threw on every new game, and a
den with no clan threw when the Gold Rush incident tried to fill it.

Both now log one line naming the mismatch, and vanilla runs in every
other case. A test checks that every bandit clan has a den on the
installed map, so a release can check the map it ships.

Research: taom-src BanditSpawnCampaignBehavior (_hideouts :23,
AddBanditToHideout :312, GetInfestedHideoutCount :490)
Not-tested: a new campaign with a mismatched TAOM_Map
Ported-from: cd06da904d35f9160c32ff33030814a325344ab6
Ported-from: 1ef7d1af6c4f43d343eebc4a2f159c209edc44d8
Ported-from: 0799af3765ba1c46a25282fb7f930adcb7b21868

#### fix(diagnostics): v2.0.28 - stall notice says what the last load did

`a2a69d4b`

The next-session notice used to say only that a load did not finish. It
now reads the prior log's tail and adds one sentence: the battle was
still being prepared (compile lines arrived during the silence), it had
stopped loading (none for 60 s or more), or the log cannot tell. A load
that ended in any other phase keeps the message as it was. The marker's
WARNING line gains evidence=<Kind>. The three sentences are in English
in all 12 language files; WP135 runs the translator.

Not-tested: the inquiry text in game

#### fix(crash-guards): v2.0.28 - crash guards contain only their own fault

`0c018d75`

The guards that catch a null reference so a vanilla crash cannot end
the game now contain only the fault each was written for:

- Army gathering (Patch49) contains the error only in its two known
  dead ends, and only when the vanilla body actually ran.
- The character creation mount (Patch20) checks before it spawns,
  instead of catching every error afterwards.
- The flagged-item drop guard (Patch50) logs the first swallowed error
  once per game, with its stack.
- The tournament end (Patch69) and race cinematic (Patch56) guards log
  the full error, and the cinematic always releases input.
- Every guard passes other exceptions on with their original stack.

Research: taom-src Army.FindBestGatheringSettlementAndMoveTheLeader
(:652-728), CharacterCreationNarrativeStageView (:194-215),
TournamentBehavior (:149-259), Mission tick handshake (:3585-3740)
Not-tested: siege start, tournament end, become-king cinematic and a
horseless culture's character creation in game
Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3
Ported-from: 0002cb5614e52fbdd638b402d302613f04678cf5

#### fix(campaign): v2.0.28 - supply path query skips off-navmesh endpoints

`5a5f3a4a`

A supply caravan's route query now skips the native path search when
either end of the route lies off the land navmesh, as vanilla parties
do. Native code reads its face table without a bounds check, and PR
#1064 reported a crash in this query on Bannerlord 1.4.8; such a route
takes the straight line, as a failed query always did. Routes already
followed the land rules on this line.

A test also pins the desertion rule from the same PR: a party or
garrison in a battle loses nobody that day (#750).

Research: taom-src IMapScene.GetPathBetweenAIFaces (:34),
PathFaceRecord.IsValid, MobileParty.ComputePath (:3693-3698)
Not-tested: a remote supply order to a party off the navmesh
Ported-from: ab78564e064e173bef9f8d23d22d46bf86e3b3aa

#### fix(taverns): v2.0.28 - talking to a musician no longer crashes

`03bf6ee6`

Talking to the tavern musician crashed the game in a town whose
culture owns no other town: his song names a random town of the same
culture, and with none to pick the game read the name of nothing. On
the 1.4.8 map checked for this port that is seven towns, among them
Dol Guldur, Isengard, Lothlorien and Rivendell.

The musician now picks the town the game would have picked; only when
there is none does he name another town, and failing that his own. His
songs are unchanged. If a game update changes his code, the fix stands
down and logs one warning.

Research: taom-src conversation_talk_bard_on_condition (:641-665),
Extensions.GetRandomElementWithPredicate (:288)
Not-tested: the musician in the seven single-town cultures
Ported-from: eed06b9c70bc562a4a77d0b71ed08260940d48f2

#### fix(stale-character): v2.0.28 - repair a hollow parent before a birth

`4817671c`

A lord's child birth could crash the campaign map when a parent's
character came back from a save with no culture of its own, a hollow
character that XML never reached. The birth copies the missing culture
onto the child, and the game throws while it builds the child.

Before each birth TAOM now repairs a hollow parent: from the
registered character of the same id when there is one, keeping a race
restored at session launch, otherwise from the hero's own or clan
culture. It never invents a culture. A census line at load names every
hollow hero, and [Check] lines record each repair and whether the
birth finished.

Research: taom-src HeroCreator.DeliverOffSpring, CharacterObject,
BasicCharacterObject.FillFrom, Hero.SetCharacterObject (v1.4.8)
Not-tested: a birth with a hollow parent in a real campaign
Constraint: no IOutcomeLog on this line; IModLogger, same line format
Save-compat: writes _originCharacter on a hollow parent only
Ported-from: 1165a00dec70c2b37b35dce4b97fa8507f7f3272
Ported-from: de90a24514689b1f60afdd32971ffdb6afff141e

#### fix(combat): v2.0.28 - creature blows log Pierce, never a crush-through

`5a409831`

A blow TAOM registers directly (creature attacks through
CustomAttacksUtils.TakeDamage) built its combat-log record with
positional arguments: knockDown went into crushedThrough, so every
creature knockdown the player saw read "Crushed through!", and the
rider check compared the victim with itself. The constructor also sets
the damage type to Blunt, and only the engine's own collision code
copies the real type in, so every creature blow logged as Blunt.

The record is now built with named arguments: never a crush-through,
the victim's rider compared with the attacker as vanilla does, and the
damage type set to Pierce to match the blow.

Research: taom-src v1.4.8 CombatLogData (constructor :367, Blunt at
:385, crush text at :185), MissionCombatMechanicsHelper :192,
Mission.GetDefendCollisionResults :6462
Not-tested: the combat log in a live battle
Ported-from: c79a585218ad22841c8db1f273e013385512b332

#### fix(tools): v2.0.28 - triage tells a silent render wait from a drain

`c0182a61`

The battle-load triage tool no longer calls a stuck loading screen a
draining shader cache when the 1 Hz render-wait lines stopped long
before the log or the watchdog line. The count in those lines comes
from the main thread, so when it blocks the count freezes with it.
When the stream went silent for 30 s or more, the verdict now says the
log alone cannot separate a slow compile from a wedge and points at the
compile_shader lines in the full rgl_log and a hang dump. It reports
only the last load's own watchdog line, words a stuck shaders=0 wait
consistently, and flags a flat heapMB tail after BattlePlayable (the
#634 freeze shape).

Research: 1.4.x tool equals 1.5.x 89374fc4f^ apart from the Patch43 hint; v1.4.8 MissionState.cs:110 gate
Not-tested: none (offline tool)
Ported-from: 89374fc4f26683f3978072d30aed0832e9a3815e
Ported-from: d99398f1210eeabc67a5158c56479fb9a5f6749f
Ported-from: b9eabd71250e4861fe3921fa1dfea5973e7c2c36
Ported-from: 76a3957297d2733afcce95c45e234ab8b2d931a1
Ported-from: 1581d4d0adfa0d6be4f04423de18f8ad99f01f8a
Refs #1025

#### fix(hero-race): v2.0.28 - old saves no longer take the last game's races

`4aa44067`

Loading a save made before TAOM tracked races, after playing another
campaign in the same session, gave that save's heroes the races of
the previous campaign's heroes with the same ids. The game skips the
race data load for a save that holds none, so nothing cleared the old
map. The race persistence now resets itself at the start of every
campaign, before any save data loads (#1035).

Research: taom-src v1.4.8 Campaign.OnInitialize (GameManager.OnGameStart
:1391 builds the behavior before LoadBehaviorData :1429),
MBGameManager.OnGameStart :159-164
Not-tested: a pre-TAOM save loaded after another campaign in one session
Ported-from: eb5cf02898fd2ccec2e3fb0429c99168e30d2af7

#### fix(hero-race): v2.0.28 - a new game no longer warns of a pre-TAOM save

`956e9782`

Every new campaign logged a warning that no saved race data was found
and that the save might predate TAOM. A new campaign has no race data
yet, so it now logs an information line instead. A loaded save with
no race data still warns, also when it is loaded after a new campaign
in the same session. Ported from the 1.5.x line (only the
race-persistence part of 6ae924638).

Research: taom-src v1.4.8 CampaignBehaviorDataStore.LoadBehaviorData
:86 (a save without the behavior's record gets no SyncData call)
Not-tested: the log line in a live new campaign (plans/119 checks)
Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3
Ported-from: eb6d1843197a1c005ce7a536a21cff8ee39d5a13

#### fix(specres): v2.0.28 - a new campaign no longer keeps the old balances

`b57e8d5e`

Special resource balances are kept by an object that lives as long as
the game process. A second campaign started without restarting the
game kept the first campaign's balances, and its first save wrote
them into the new save file.

A new campaign now empties the balances before character creation
gives the new hero a starting amount, and loading reads the saved
balances into an empty value, so a save record without them loads
empty instead of keeping the previous campaign's. Saving is unchanged.
Still open: a save made before special resources existed, loaded
after another campaign, keeps that campaign's balances (feature doc).

This finishes plan 001 on this line; its career half is f4273639.

Research: taom-src v1.4.8 CampaignBehaviorDataStore.LoadBehaviorData
:86 (SyncData only for a behavior the save holds) and
BehaviorSaveData.SyncData :26-38 (a missing key leaves the ref alone)
Not-tested: a second campaign in one process (see plans/119)
Save-compat: no save field added; the key and its format are unchanged
Ported-from: 4263535a1d60353a65827700b15d57e4c70e4fc7
Ported-from: 8ea3150522e642ca39ece0b8c4c7a1ba273a2c5e

#### fix(battle-load): v2.0.28 - watchdog bundles skip main-thread reads

`eea9c819`

The battle-load stall watchdog runs on a thread-pool timer. When it
asked for a crash bundle, the capture ran the Mission and Campaign
collectors and built the crash inquiry from that timer thread. It now
tells the crash service the capture is off the main thread, so the
bundle skips those two sections and no inquiry opens. The STILL
LOADING marker and the battle-load phase log stay the primary signal.

Not-tested: a stalled battle load in game

#### fix(crash-report): v2.0.28 - crash reports keep the throw site

`ffb53945`

When PatchShield or SaveShield handed an exception back to Harmony,
Harmony rethrew it with `throw`, which replaced its stack trace with
the frames from the shielded method outward. A 1.4.8 player bundle
(2d446100) showed a childbirth failure as five frames ending at
MapState.OnTick, and different crashes through one shielded method
shared one crash signature.

The shields now move the original trace aside before the rethrow
(RethrowStackPreserver, new in TAOM.Dependencies). A report prints the
real throw site first, with one marker line for each shielded method
the exception crossed, and keeps the first five frames of the original
throw under TAOM.ThrowSite. The crash signature and the report's frame
sections use them, so two different throws through one frame get two
signatures. A crash that crossed a shield gets a new signature once;
crashes that never crossed one keep theirs.

TAOM.Dependencies changed: a release must ship TAOM and
TAOM.Dependencies from one build.

Research: Harmony 2.4.2 as shipped in TAOM.Dependencies (finalizers run
on every call; a value-returning finalizer set ends in throw)
Not-tested: a shielded rethrow in a live game
Ported-from: c79a585218ad22841c8db1f273e013385512b332

#### fix(hero-race): v2.0.28 - dead heroes keep their race across a load

`496a68fd`

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

#### fix(heroRace): v2.0.28 - dwarf portraits no longer sit too low

`56052dba`

Dwarf faces in the small 2D portraits (settlement notables, party and
battle screens) sat at the bottom edge of the frame, showing little
more than hair and eyes. The dwarf row in CharacterImagePatch.json
moves from Vertical 0.15, Zoom -0.1 to Vertical 0.3, Zoom -0.2, the
values tuned by eye on the 1.5.x line. The 3D preview offsets in
CharacterAvatarPatch.json are unchanged.

Not-tested: framing in game on v1.4.8 (maintainer check)
Ported-from: 6c26f003af0868fd10560e8b08380b277bd1aadf

#### fix(enlistment): v2.0.28 - session caches reset on load, new game, exit

`7fab4acb`

Enlistment keeps per-session state on objects that live as long as
the game process. Starting a second campaign without restarting the
game, or loading an earlier save, could keep the last session's
commander party and army, the town-stop timer, the shore-leave offer
and its once-a-day cooldown, the army-rhythm snapshot and the
commander-loss popup flag. A soldier could be held in a town after
his commander left, never be offered shore leave, or lose his lord
without a word.

All of that is now cleared on every load, on a new campaign and when
the game returns to the main menu. A save with no enlistment data
loads with no service record instead of the previous session's. The
shore-leave offer comes once per town stop again: the stop ends when
the commander leaves town. A discharge also re-arms the
commander-loss popup.

Research: taom-src v1.4.8 Campaign.DoLoadingForGameType (OnGameLoaded
:1670 for a save, OnNewGameCreated :1694 for a new campaign),
CampaignBehaviorDataStore.LoadBehaviorData :86, Game.Destroy
(OnGameEnd :253 before Campaign.OnDestroy :254)
Not-tested: a second campaign in one process and an earlier-save load
while enlisted (in-game checks in plans/119)
Save-compat: no save field added or changed
Ported-from: 6fb37c9f7114a6b133ab73e88edfff09bfabaafe
Ported-from: f6ac2d335fd6373eda7d8006be501ddd34279e50
Ported-from: e99d705a3f2d3ad924f200ff3bb3a7a6c90f670e
Ported-from: 74fb3d35e44f70f008d6d35069880d9319d20c04
Ported-from: b9d18458ed73ce8d4b48c86ec2ec7e4e48c8a125
Ported-from: 41754a03e8abe41f977fe224193c94f0f7f0e219
Ported-from: a67792c408042b3653f1615511ab138286ea5aae
Ported-from: fd61757bf342f67975f730f3ff39bef014afa855
Ported-from: aac0e05bff28c70cdba160656f738e77f2c7a2b7

#### fix(diagnostics): v2.0.28 - SaveCollected stops printing strings=0

`35534f1a`

The always-on SaveCollected line read SaveContext._strings in the
CollectObjects() postfix. The engine creates that list empty per save
and only fills it inside CollectSaveDatas, which runs after the postfix,
so every save logged strings=0. Beside a real strings=N on LoadCounts
that reads as a difference, and the phase log is add-never-rename.

The token is now left out, the same way an unbound field is. The hook no
longer binds _strings and the binding test no longer pins it.

Research: taom-src v1.4.8 SaveContext (:119, :252-271, :342-346, :491, :505); ilspycmd TaleWorlds.SaveSystem.dll
Not-tested: the line in a live save

#### fix(momentum): v2.0.28 - a resisted raid no longer scores twice (#774)

`4fc0cbeb`

The game reports a raid party that beats a village's militia as a raid
win even when the village is not looted yet, so war momentum scored
that raid twice: once for the fight and again for the looting. War
momentum now scores a raid only when the village is looted.

Research: v1.4.8 RaidEventComponent.cs:128-138; Village.cs:16-22
Not-tested: a militia-resisted raid in a live campaign (check 10)

#### fix(momentum): v2.0.28 - show the lead that decides the war

`d5ad43fb`

With victory enabled, the War of the Ring popup now shows the momentum
lead the victory check uses, against the threshold. The map bar shows
the ratio of the two sides' totals, so after a long war a side could
win with the bar near the middle. The option texts now say that
battles and sieges by any lord of your realm count toward the player
requirement, won or lost, which is how it has always worked (#375).
The new popup label is English in every language until the next
translation run.

Not-tested: popup row rendering (in-game check 7)

#### fix(mission): v2.0.28 - restore tickCompleted after a mission-tick throw

`49384eee`

Mission.OnTick clears tickCompleted before it runs the mission
behaviours and starts the agent tick last; only the agent tick sets
the flag again, and the next frame waits for it with no timeout. On
1.4.x every catcher above swallows, so one exception out of any
OnMissionTick froze the battle for good with audio still playing. The
QA toggle Throw On Next Mission Tick is exactly that throw.

Patch91b notes whether the flag was set when OnTick began and marks
the agent-tick launch from a finalizer on TickAgentsAndTeamsAsync.
When OnTick then exits by exception with the flag down and no launch,
it sets the flag back and logs one [TickRecovery] error; the exception
still reaches the crash report. It never restores after a launch, so
behaviours cannot overlap a running agent tick, and it arms only when
both patches and the private field are verified at game start.

Research: taom-src v1.4.8 Mission.cs 1040, 1825, 3529-3538,
3585-3591, 3601-3618, 3636-3776; MissionState.cs 201, 212-219
Constraint: Mission is sealed and tickCompleted private; written
through a cached AccessTools.FieldRefAccess
Rejected: restoring on any exception (unsafe once the agent tick has
launched)
Not-tested: the live engine path; the in-game check is the QA toggle
Save-compat: no saved state touched

#### fix(tools): v2.0.28 - Armory audit skips catalogue drift when cooked

`1a1a2af7`

generate_armory_catalogue.build_rows inventories a module's loose
Assets tree only. The public 1.4.8 release, and this laptop, install
the Armory cooked (AssetPackages only), so there the drift section
read every committed catalogue row as deleted, the verdict was BROKEN
by construction, and --regen-catalogue would have written an empty
catalogue over the committed one.

The audit now checks for a loose tree first (has_loose_tree). Without
one it reports catalogue drift as SKIPPED with the reason, marks the
verdict "catalogue drift not checked", leaves the exit code to the
other checks and refuses --regen-catalogue. Three tests, written
first.

Not-tested: a run on a machine with both trees (the desktop)

#### fix(tools): v2.0.28 - mesh refs: loose tree, piece owners, --check-name

`236e7872`

Three changes to tools/validate_mesh_refs.py from the 1.5.x line,
taken as one end state:

- The package list follows the engine's order per module
  (module_tpacs): the loose Assets tree when the module has one, its
  cooked AssetPackages otherwise. The old order preferred a cooked
  tree wherever one existed, so a stale pack could pass art already
  deleted from Assets. The public 1.4.8 release ships the Armory
  cooked only, and there the engine and the tool both read the packs.
- A <CraftingPiece> now owns its mesh and BladeData body_name refs.
  The item regex never matched it, so every piece ref carried item id
  "", which the borrowed-body gate in this change needs (#633).
- --check-name NAME says whether a new mesh name is already held in
  any module's loose or cooked packages (exit 0 free, 1 taken, 2 when
  a package could not be read).

The allowlist tests now own a fixture instead of reading the shipped
KNOWN_DEAD_MESHES, whose three troll entries stay until the public
Armory is checked. Docs: mesh-ref-validation and the armory guide's
"Two asset trees".

Research: engine tree order from a player's 2026-11-01 rgl log, which
  loads $BASE/Modules/LOTRLOME_Armory/AssetPackages; no engine API bound
Ported-from: b23a27ef45b3dd060c9729e63a26f65388e12a6f
Ported-from: d5986f87f853e0a1d8f36b54a88add9e4212571c
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 719074072ab5777bd614cda70afc330f8346f1c1
Ported-from: 7bbe6f4de154127b9c61e517b4ad5a74c2d4bcf0

#### fix(momentum): v2.0.28 - reset the momentum war per campaign (#773)

`d2a25717`

Loading a save made before the momentum war existed, after playing
another campaign in the same game session, no longer carries that
campaign's momentum, rosters and player events into it. The momentum
behavior is now built fresh for every campaign and resets the momentum
state in its constructor, like the War of the Ring behavior (#764); a
loaded save with a momentum record then restores its own war.

Research: v1.4.8 CampaignBehaviorDataStore.cs:86-105 skips SyncData
Research: when a save holds no record; Campaign.cs:1391, :1427-1430
Save-compat: save keys unchanged
Not-tested: a record-less load after another campaign (in-game check 8)

#### fix(momentum): v2.0.28 - empty rosters never decide the war

`869fc5dd`

With victory enabled, the war could end in a Free Peoples win the day
it started when no Evil realm had enrolled, or when both rosters were
empty, with the bar at the centre and no battle fought. The momentum
war now starts only when both sides have an enrolled realm, and empty
rosters on both sides decide nothing. The "War of the Ring decided"
log line keeps its wording and now also names the rule that fired
(threshold or elimination), both roster sizes and the player gate, so
a player's log shows why the war ended.

Not-tested: a one-sided enrollment in a live campaign (in-game check 7)

#### fix(momentum): v2.0.28 - non-finite realm strength scores nothing

`6b8c6753`

A battle's war momentum divides the losers' casualties by their realms'
strength. A strength that is not a finite positive number now scores
nothing, instead of casting to the smallest integer and swinging the
war for either side with the bar at the centre. The momentum lead is
computed without integer overflow, and a negative event value is
refused.

Research: v1.4.8 Kingdom.cs:222 CurrentTotalStrength sums clans
Not-tested: trigger not reproduced; shipped strength models are bounded

#### fix(mission): v2.0.28 - park off-thread callback writes (#634)

`c4d70e67`

Bannerlord raises OnAgentRemoved, OnAgentDeleted, OnAgentDismount and
other agent callbacks on the thread it chooses, and 1.4.8 player logs
caught them off the main mission thread. TAOM handlers wrote
main-thread collections from them with no lock, which can corrupt a
Dictionary and stall the agent tick that the next frame waits for.

Those writes now run inline on the main thread and are parked for the
owner's next mission tick anywhere else: the behaviour-tree
component's cleanup, mount despawn's kill record and forget, the
career teardown when the player falls and its hit notice, SpatialGrid
removals and the warg dismount reset. Field commission kills take a
lock (tied kills keep first-kill order) and the enlistment kill count
is Interlocked. Off-thread reports go to the file log at WARNING
instead of an on-screen message.

The freeze cause is not proven, so Patch91 brackets the agent tick and
the main mission frame, and a watchdog writes each stuck thread's
stack to the TAOM log as [MissionStall] at 10, 20 and 40 seconds. New
MCM toggle Enable Battle Freeze Sampler, default on.

Research: taom-src v1.4.8 Mission.cs 1040, 3585-3591, 3601-3618,
3636-3776; MissionState.cs 212-219; MissionBehavior.cs 121-124
Not-tested: Thread.Suspend capture on a live thread; the off-thread
races themselves (a live Agent cannot be built in tests)
Save-compat: no saved state touched
Ported-from: f75288ebf5c6866b01e5c578aa232c123c9aa27b

#### fix(settings): v2.0.28 - changed defaults reach every player's settings

`5e2b1249`

The mod options keep a saved value for each setting by its name and
load it over the built-in default from then on, so a default changed
without renaming the setting never reached a player who had saved his
options before. Thirteen settings were caught this way on the 1.4
line. Each now has a new internal name, so everyone gets the intended
value (the labels in the options are unchanged):

- War of the Ring: Phase 1 Start Day 30 and Phase 2 Start Day 44 (an
  options file saved on v2.0.13 or earlier still held 2 and 14).
- World / Bandit Scaling: Initial Hideouts Per Faction 7 (was 14), Max
  Parties Per Hideout Cap 6 (was 3).
- Battle Tactics: Enable Smart Cavalry AI and Enable Formation Presets
  off, as shipped since v2.0.7 and v2.0.8.
- Battlefield Promotions: Merit To Promote 32, Offers Per Battle 2
  (were 8 and 1).
- AI Party Size: the lord party multiplier and flat bonus, the garrison
  multiplier and the food and wage relief return to neutral, as shipped
  since v2.0.26.

A player who had chosen his own value for one of these must set it
again. A new check, tools/check_mcm_default_flips.py, fails when a
default changes under an unchanged name; four older changes that are
safe to leave as saved are allowlisted with their reasons (#1017).

Research: 1.4.x tags v2.0.0 to v2.0.28 compared: 13 flips, 4 harmless
Not-tested: an old TAOM.json picking up the new defaults (check 6)
Ported-from: 42971374650eee9546589d0177ae852597199f95

#### fix(career): v2.0.28 - siege and capture credit follow the player

`fa367a51`

The career quest credited a siege only when the player's own party, or
an enlisted soldier's commander, led the attack. A player fighting
inside another lord's army got nothing for a town he helped take, and
an enlisted soldier who sat a siege out still got credit when his
commander led it. It now credits the player when his party fought on
the side that took the town, whoever led it. War momentum adds the same
side rule to its realm test, so a vassal is still credited for a
capture by his own realm.

The career objective and the issue quest for capturing enemy lords now
count every lord captured by the side the player fought on: the game
hands a captured lord to a winning party chosen by chance. War momentum
also counted a win by defenders who joined a siege from outside the
walls as a captured town; the game takes no town there, and momentum
no longer counts one (#1015).

Constraint: no co-op player-control query on 1.4.x; the side rule reads
Constraint: the local main party's map event side, passed in
Research: v1.4.8 KingdomManager.cs:238-243; MapEvent.cs:2093,2115,2158
Research: v1.4.8 MapEvent.CaptureDefeatedPartyMembers :1954-1993
Not-tested: enlisted and army siege credit end to end (in-game check 5)
Ported-from: ec2a3026f7391467c0ea0f19de6b4a0af59f4cda

#### fix(wotr): v2.0.28 - declare the Full War wars for real

`090f1ef2`

When the War of the Ring reaches the full war (day 44 by default),
every pair of kingdoms set as Hostile now really goes to war: each war
is declared, the war notice and the history entry appear, and each
kingdom's list of enemies is rebuilt at once (#772). Before, no
Hostile-pair war was ever declared. Peace stayed blocked, but the
stored stances stayed at peace, so AI armies could miss the new enemies
as siege and raid targets until the next load.

Each declaration first asked whether the two kingdoms were already at
war, and TAOM's diplomacy model answers "at war" for every Hostile pair
once the full war starts, so every declaration was skipped. The
declarations now check the stored stance instead. Saves already in the
full war are repaired when they load: on the host, every Hostile-pair
or scripted Phase 2 war still at peace in its stored stance is
declared once. Scripted war lists in war_of_the_ring.json now drop
empty and self pairs and name an unknown kingdom id with a warning.

Research: v1.4.8 FactionManager.cs:144-160 asks IsAtConstantWar first
Research: v1.4.8 FactionManager.cs:78-87; Kingdom.GetStanceWith :662
Research: v1.4.8 AllianceCampaignBehavior.OnWarDeclared :685-710
Not-tested: Full War transition and old-save repair (in-game checks 2-3)
Ported-from: d1b9fd91ac32dd2dce5facc0567c577772f4acc6

#### fix(wotr): v2.0.28 - reset the War of the Ring per campaign

`eddf11e6`

Starting a new campaign without restarting the game no longer carries
the previous campaign's War of the Ring into it. Before, a campaign
started after one that had reached the full war began in the full war
on day 0: it never declared the Isengard war or the full war, and its
momentum war started at once. After a war that had ended, the new
campaign never got a war at all (#764).

The War of the Ring state lives for the whole game process. Its
campaign behavior is built fresh for every campaign and now resets the
state in its constructor, before any campaign event can read it; a
loaded save then restores its own phase. A save already written by an
affected campaign keeps the phase it saved.

Research: v1.4.8 Campaign.cs:1391 OnGameStart precedes LoadBehaviorData
Research: v1.4.8 Campaign.cs:1694-1695; MbEvent.cs:24-44 newest-first
Not-tested: a second campaign in one game process (in-game check 1)
Ported-from: 2719e59e8c81648a0b5cc15364a8548f755c00ce

#### fix(race-age): v2.0.28 - orcs and goblins overbreed (#628)

`863cfbd9`

Orc and goblin clans no longer fill to twice their population cap.
The race fertility bonus now applies only while a clan is at or under
its cap and never to a marriage that involves the player; a penalty
(elves, dwarves, trolls) still applies everywhere. Orc-kin fertility
drops from 2.0-3.0x to 1.3-1.5x, orcs and berserkers are fertile to 45
(was 50) and humans to 60 (was 195). Goblin, Misty Mountain and Blue
Craig clans no longer start a new campaign topped up with children,
like the other orc cultures. Existing saves keep the children they
have; the new rates apply after a full restart of the game.

Research: the code, config and tests on this line were byte-identical
to the 1.5.x parent of 83bdad853; the fix binds no engine member.
Not-tested: in game (clan sizes need a long campaign to show).
Save-compat: no save format change.
Ported-from: 83bdad8537b9760deb6cd849c7ced8943a5f5a37
Ported-from: 925db38d14a55e8851b7aeabd7e1a3627fe9dc75

#### fix(harmony): v2.0.28 - apply every patch category through one guard

`0271b628`

SubModule applied its patches through 80 PatchCategory call sites,
64 of them with no try/catch. Harmony 2.4.2 has no catch around a
category, so one class whose target no longer resolves threw out of
the hook. In OnGameInitializationFinished the once-per-process flag
is set before the batch, so every later category (the Patch65,
Patch82, Patch84 and Patch85 guards among them), the battle-load
watchdog, ManualPatchApplicator and the census were skipped for the
process, and the throw then left TAOM through MBGameManager's
submodule loop, which has no catch on v1.4.8. In OnSubModuleLoad the
throw leaves Module.InitializeSubModuleBases, also without a catch.

Every category now goes through TryPatchCategory, backed by
PatchCategoryApplier: a failure logs [PatchApply] at Error with its
cause, costs only that category, and is named in one localized
notice per phase (an inquiry at the first main menu, a red chat line
at game initialization and at the first mission). PatchCategoryIndex
builds the category index class by class, so a [HarmonyPatch] that
names a type the engine no longer has costs that class, not every
category. The Patch37, Patch77 and preview-batch sites keep their
side effects. The five taom_patch_apply_* strings ship in all 12
languages, inside <strings>, as the 1.5.x AI first drafts. Ported
from the post-merge end state of 998ae7e95 (plan 009).

Research: taom-src Module.cs:201-208, MBGameManager.cs:110-125
Research: ilspycmd Lib.Harmony 2.4.2 PatchCategory, BuildCategoryCache
Not-tested: live apply path and the notices (needs the game)
Save-compat: no save data touched
Ported-from: 45bcf80bd34bf6759210c3ce01b7485447cd31ed
Ported-from: bdf7d515c49740c60d0204770c503bdb2b5436dc
Ported-from: 4c728dac0f2a4ec2b410692ecd08931abaaced4f
Ported-from: 7912fdd8fa31d323d5b31db0b9c3a2014768ab50
Ported-from: b6cb6ff5df940d2f265428fd10fba40cc332dc5c
Ported-from: 7eae4704434dcbe73b951628e4968023cd0c60cb
Ported-from: 27fd23bbb7c4ad8b79947d3621f3b6989f778c71

#### fix(harmony): v2.0.28 - fail translation rows outside root <strings>

`b71f560c`

LocalizedTextManager.LoadLanguage on v1.4.8 reads only the <string>
children of the root's <strings> element and skips every other row
without a log line, so a misplaced row renders in English while a
check that walks all descendants counts it as present. A new
LanguageDataXmlTests gate fails on any such row. Every language file
on this line is clean, so the gate lands green and guards the rows
the next commit adds.

Research: taom-src LocalizedTextManager.LoadLanguage (:243-255)
Not-tested: none (test only)
Ported-from: 27fd23bbb7c4ad8b79947d3621f3b6989f778c71

#### fix(campaign): v2.0.28 - 1.4.8 supply, desertion and Uruk fixes

`1b00d1ac`

Fixes for Bannerlord 1.4.8 players on the legacy branch, contributed by
Marcelo Araujo Marangoni (Jopila) in PR #1064.

Supply caravans now route with the campaign's land navigation rules:
the path query passes the navigation model's terrain exclusions and the
distance model's region costs, as vanilla land parties do. A player
reported a native crash when ordering supplies from a city other than
the current one; the exact failing argument was not isolated.

Supply route arrows check the map scene for a terrain heightmap before
the terrain height query. Without one they take the land height from
the collision query with SandBox's land mask. This targets a crash
reported on returning from a defensive ambush to a siege.

Alignment desertion skips a field party or garrison while it is in a
map event, as vanilla desertion does (#750).

Special Resources and the troop count diagnostics no longer leave a
stale screen subscriber behind after a campaign reload.

The Uruk-Hai Champion and Uruk-Hai Berserker now wear the Uruk skirt in
the Body slot. The engine refused it in the Cape slot, so on 1.4.8 both
troops had no skirt at all. They now gain its armour and its 20.7 kg
weight in battle.

Reviewed against the 1.4.8 decompile (engine compatibility, data flow,
XML and tooling); no blocker. Not run: a 1.4.8 build, MSTest and any
in-game check. The same supply and desertion fixes are on
bannerlord-1.5.x in ab78564e0 (#1068).

#### fix(marketplace): v2.0.28 - keep the Mouth of Sauron's helm off stalls

`d906fc92`

The shared Armory ships the Mouth of Sauron's helm (not for sale). On
this line the culture marketplace takes every item with a culture and
honours only its blacklist, and nothing here wears the helm, so Mordor
towns could sell it. A mordor blacklist row keeps it out of their new
stock (Mike, 2026-10-04; review #733 on bannerlord-1.5.x). A helm a
saved game's town already stocks stays until it is bought: the stock
filter removes only foreign-culture items.

ShippedConfig_MordorBlacklistsTheMouthOfSauronHelm pins the row. It was
not run locally: this branch does not compile against the installed
Bannerlord v1.5.3; its CI runs it.

#### fix(custom-battles): v2.0.28 - named commanders, default troops (#709)

`55d77901`

Custom Battle no longer crashes on Start when a side picks Nord,
Vakken or Darshi. Those vanilla minor cultures have no faction banner,
and vanilla recolours each side's banner without checking it has a
layer. The faction picker now lists only cultures with a banner.

Mordor, Gondor and Rohan now show their full curated commanders in
Custom Battle: Sauron, the Witch-king and the Nazgul, Boromir, Faramir,
Imrahil, Theoden and his captains. 21 of them are vanilla lords that
TAOM rebuilds, and vanilla loads them only in a campaign, so Mordor
showed only the three lesser Nazgul and Gondor and Rohan one or two
names. A Custom Battle-only stub file now lets the same rebuild create
them. Khamul also heads Dol Guldur's commander list now, and in
Sergeant mode the random general can be one of these lords.

Each formation now defaults to the culture's own TAOM troop where that
troop fits the slot. TAOM's defaults never applied in Custom Battle
before, and the six re-skinned cultures defaulted to Calradian troops
(Rohan infantry: the Vlandian Swordsman). Where no troop fits, vanilla's
pick stays; a culture with no soldiers of its own still gets the troops
its culture names, so Start cannot spawn an empty slot.

Known limitation: Sauron fights in Custom Battle without TAOM's race
combat rules, which are campaign-only.

Port of the bannerlord-1.5.x fix (9e2a39f4 and dffdf879) with 21 stubs
instead of 24: this branch defines lord_1_48_1/2/3 in characters/
lords.xml. Not built here: this machine has only the v1.5.3 engine,
which this branch does not compile against (errors in untouched
files). The stub transform and data were checked against this
branch's files; the C# matches bannerlord-1.5.x, verified there.

RCA: docs/reviews/rca-custom-battle-bannerless-factions-2026-10-02.md

#### fix(hero-race): v2.0.28 - Load Game preview rider no longer lies flat

`f8c9c8ec`

The hero preview on the main-menu Load Game screen showed the rider
lying flat in bind pose while the mount stood normally (#700). Every
in-game screen was fine.

The preview poses the rider with one of the engine's cached animation
indices. On v1.5.3 those indices are broken before the game finishes
loading on almost every launch, and TAOM's repair for them only ran
once a campaign or battle started, after this screen. The preview's
existing patch now runs the repair first, so the rider stands in the
inventory idle. Nothing changes for other screens.

Pinned by a wiring test that fails if the repair stops being the first
statement of the patch. Review record:
docs/reviews/rca-load-game-preview-bind-pose-2026-10-01.md; incident
addendum in docs/reviews/rca-prone-character-tableau-2026-07-31.md.

Refs #700

(cherry picked from commit 1e39c1b8; not built on the desktop, which has
no 1.4.8 engine)

#### fix(data): v2.0.28 - 1.5.x troop tree for the new Armory ids

`92a30ba4`

The public 1.4.8 release ships the 2026-09-29 Armory publish, which
carries KEYforce's Mirkwood helmet and Rohan sword texture fixes. That
Armory retired 159 item, piece and monster ids the 1.4.5 troop data
still named: the #582 ladder bows, the Swan Knight and Pelargir
spears, the Lamedon kit. Against it this branch had 589 broken item
refs, so archers would have lost their bows.

The 16 troop files, recruitment_pools/gondor.json and the three Gondor
equipment and companion files now match bannerlord-1.5.x (the #617
per-tier bows, the #669 Lamedon swaps), with 998f054c's Gondor
volunteer fallback and its test so the lockstep test holds. One line
differs from 1.5.x: the Harad war elephant keeps sk_elephant_armor_a.
1.4.8 has no howdah crew or arrows (those came with 1.5.3), and this
harness keeps the mahout's Cavalry charge.

validate_moduledata against the release modules: 0 broken refs. The
83 SKILL_TEMPLATE_MISMATCH rows in lords.xml and lords.xslt are the
v1.5.2 inline-skill rule, which 1.4.8 does not apply. Not built on the
desktop, which has no 1.4.8 engine; the laptop build and tests are the
compile check.

#### fix(diplomacy): v2.0.28 - Patch80 seams D+E close a decided vote (#550)

`d8e3777c`

Seams A to C (#547) gate on a cancelled election and never see the
second mechanism behind the same stuck window: when the player is not a
supporter, StartElection() applies the outcome and fires
KingdomDecisionConcluded synchronously inside DecisionItemBaseVM's
constructor, so IsKingsDecisionOver is already true when the view model
binds. KingdomDecisionPopupWidget's IsKingsDecisionDone latch is
edge-triggered and never reset, so the first such window closes on the
five-second timer and every later one in the visit stays open with map
navigation locked.

Seam D, a Priority.Last postfix on KingdomDecisionsVM.RefreshWith:
when the item just built for this decision is already IsActive and
IsKingsDecisionOver, run vanilla's own ExecuteDone through a MethodInfo
cached in KingdomVoteDeadlockBinding. Safe exactly where seam B's close
is not (_chosenOutcome is set on this path), it hides the popup, shows
the outcome, clears the concluded listener and runs OnDecisionOver;
nothing is re-implemented. ExecuteDone stays out of the binding's
IsReady so losing it on a bump disables seam D alone. A hit logs at
warning level: the Player Switcher's route into this state is closed at
the source, so a warning in a player's log names a route nobody has
traced.

Seam E, a prefix on DecisionItemBaseVM.ExecuteDone, from Codex review
113 F1: the bind still arms the widget timer and only the timer's own
ExecuteFinalDone disarms it, so a seam D close leaves a FinalDone due
five seconds later against whatever item is bound by then (the same
closed item, a duplicate inquiry the query manager rejects; or the next
live one, an NRE on a null _chosenOutcome). ExecuteDone now runs only
on an active window whose election is over, which is every legitimate
call: the timer arms only on the true edge and nothing but ExecuteDone
clears IsActive.

Main/SubModule.cs registers the PlayerClanLeadershipRepairBehavior from
the previous commit alongside the two seam initializers. The five
Patch80 reflection members join the reflection-site catalogue (the four
from #547 had only ever been pinned by the feature suite). RCA
docs/reviews/rca-player-switcher-clan-leadership-2026-09-15.md.

Seam E's CoopVetoClassificationTests registry row is held back: that
file carries another session's staged hunk. Until it lands,
EveryBoolPrefix_HasACoopDisposition names the new prefix.

Research: DecisionItemBaseVM.ExecuteDone, KingdomDecisionPopupWidget,
  KingdomDecisionsVM.RefreshWith/HandleDecision/OnDecisionOver,
  KingdomManagementVM.ForceDecideDecision, GauntletQueryManager
  .CreateQuery, InquiryData.HasSameContentWith (v1.5.3)
Rejected: closing through _onDecisionOver as seam B does; ExecuteDone
  is safe here and re-implementing it is how seam B once leaked the
  item view model
Not-tested: the widget timer lifecycle needs a live campaign; binding
  tests pin the target, the two state reads and the ExecuteDone invoke

(cherry picked from commit 1571c2f9 on bannerlord-1.5.x)

#### fix(player-switcher): v2.0.28 - taken-over lords lead their clan (#550)

`8a6316c0`

Players starting as Boromir or Faramir kept reporting the frozen
kingdom-decision window #547 was meant to end. Their screenshot named a
different mechanism: the title was the SUPPORT wording (the player is
not the chooser) and the Yes scroll already carried the king's seal, so
the vote was over before the window existed. The takeover path set
Hero.MainHero and moved Clan.PlayerClan but never reassigned clan
leadership, so Clan.PlayerClan.Leader stayed Denethor and vanilla's
Supporter.IsPlayer (Clan.Leader.IsHumanPlayerCharacter) was false for
every election; StartElection() then resolved each one through
ReadyToAiChoose() inside DecisionItemBaseVM's own constructor.

Two changes hold the invariant Clan.PlayerClan.Leader == Hero.MainHero:

- IPlayerIdentityAdapter.PromoteToClanLeader wraps vanilla
  ChangeClanLeaderAction.ApplyWithSelectedNewLeader, the call vanilla
  itself makes for an elected non-leader king and for the player's
  heir. HeroSwitchService runs it right after ReassignPlayerClan, after
  the swap on purpose: a throw reads SwitchedWithErrors rather than a
  clean failure that had already re-headed a lore clan. Gold, party
  leadership, 70% of the old leader's relations and OnClanLeaderChanged
  ride along, and because a kingdom's leader IS its ruling clan's
  leader, taking over Boromir makes Boromir the Steward; Denethor stays
  alive as a clan member. That consequence was chosen, not tolerated.
- PlayerClanLeadershipService.RepairIfNeeded() at session launch
  repairs a save made before this change, through an eight-row state
  table (leader is the player; vanilla player_faction; the player's
  clan is not Clan.PlayerClan, warn; no leader, warn; dead, disabled or
  not spawned; co-op client; prisoner still repaired; otherwise promote,
  log, one taom_ps_clan_leader_repaired line). One test per row. The
  in-play flag folds IsDisabled and IsNotSpawned because Hero.IsAlive
  is only !IsDead; a deep-review agent caught the doc claiming "dead or
  disabled" while the code declined dead alone.

The new string is seeded in English in all twelve languages until a
translator run picks it up. Registration of the repair behavior lands
with the Patch80 commit that follows, because Main/SubModule.cs carries
both hunks.

Research: KingdomElection.StartElection/ReadyToAiChoose,
  DecisionItemBaseVM ctor/InitValues, Supporter.IsPlayer,
  ChangeClanLeaderAction.ApplyInternal, ApplyHeirSelectionAction,
  KingSelectionKingdomDecision.ApplyChosenOutcome, Hero.IsAlive (v1.5.3)
Save-compat: no new save data; the promoted leader is the engine's own
  clan leader field, identical in shape to a vanilla succession
Not-tested: the in-game Boromir two-decision check and a pre-fix save
  through the repair (owed)

(cherry picked from commit e44d096e on bannerlord-1.5.x)

#### fix(xslt): v2.0.28 - comment_strings overrides keep their variant tags

`a4c6d7c3`

comment_strings.xslt overrides 36 vanilla conversation strings. <xsl:copy>
copies the element but not its children, and 24 of the templates followed it
with an attribute-only apply-templates, so the <tags> child was dropped. <tags>
is what the engine uses to pick the culture, persona and trait variant, so a
stripped string stops matching the case it was written for and matches
everything at once. Twelve templates already carried the fix and a comment
explaining it: a known defect, partially applied, for months.

All 24 now emit apply-templates select="node()". Verified on the transform's
OUTPUT against the live v1.5.2 comment_strings.xml rather than by reading the
markup: 35 of 35 overridden ids keep their <tags>, 0 lose them.

CommentStringTagsTests uses the sentinel-stub pattern from .claude/rules/tests.md
and reads the overridden ids out of the stylesheet itself, so a newly added
override is covered without touching the test.

Fixed on the v1.5.0 port branch on 2026-08-19 and never reached trunk. Not
v1.5.0 drift; v1.5.0 rewrote comment_strings.xml, which is what surfaced it.

Research: python XSLT transform over the live vanilla file, per-id tags check
Not-tested: CommentStringTagsTests on this branch. The v1.4.8 engine trunk
  targets is no longer installed here, so TAOM.Tests cannot build; the class
  passed on the v1.5.0 branch and depends on no engine assembly.

#### fix(module): v2.0.28 - restore the #371 Dependencies pairing guard

`95085de3`

SubModule.xml carried an eight-line comment explaining that
<DependedModule Id="TAOM.Dependencies" /> is the element the engine honours,
that ModuleInfo.LoadWithFullPath does not parse DependedModuleMetadatas, and
that without it a user can run any TAOM against any Dependencies until Harmony
and UIExtenderEx fail at the member level and every character renders in bind
pose. The element the comment described was not in the file. Neither was the
BUTR-launcher mirror the second comment calls a "version-pinned mirror of the
DependedModule above".

Both were present at v2.0.20 and went out as collateral in cc1713eb, a cultures
commit whose message does not mention dependencies. v2.0.21 through v2.0.28
shipped the documentation of a guard without the guard. The v1.5.0 port branch
caught and restored it on 2026-08-19 (0f1488b4), then got parked; the
2026-09-14 tracker sweep closed #371 against that branch-only evidence.

Restored verbatim from v2.0.20 rather than re-derived. The pin reads v2.0.6 and
Dependencies/_Module/SubModule.xml reads v2.0.6, so the pairing is accurate and
not merely present. docs/modding/module-taom.md had recorded the absence as by
design and the release doc as wrong; it now records the deletion and the
restore, with the date.

Research: git show v2.0.20:Main/_Module/SubModule.xml; git log -S on the element
Not-tested: TAOM.Tests. The v1.4.8 engine this branch targets is no longer
  installed on this machine (Steam moved to v1.5.2 on 2026-09-14) and no copy
  survives, so the suite cannot build against trunk here. Verified by XML
  parse, the pairing equality, and the same elements shipping in v2.1.0/v2.1.1.
Closes #371

#### fix(tools): v2.0.28 - package_release drops dated sidecars, scene Backups

`6a4cd270`

The packager excluded only names ending in exactly ".xml.bak". The 164
sidecars the pre-release sweep found in the install all carried a dated
or topic suffix (.bak-kingdomcurve-583, .bak_mapvillages_<stamp>,
.bak-rangedladder), so that rule matched none of them, and the Modding
Kit's SceneObj/Backups and SceneEditData/Backups (437 MB) sat under two
folders the include-list copies because vanilla ships them.

BACKUP_SUFFIX_RE is the same expression as $SuffixRx in
tools/sweep_module_backups.ps1 (change both or neither): rule
BACKUP_SIDECAR replaces XML_BAK. The two Backups folders are excluded
under SCENE_BACKUPS. foo.bak.xml still ships on purpose: the engine
globs *.xml and parses it, so dropping it would hide the duplicate-id
hazard the validator is there to catch.

Test-first: the dated-suffix table, the suffix-must-be-last guard and
the scene folder pair in test_package_release.py (33 tests; tools suite
1,500 passed). Dry run on the live install after the sweep: 9,912 files,
0 unknown, nothing left for either rule to drop.

Not-tested: a real package build; the dry run is the evidence.

### Balance

#### balance(special-resources): v2.0.28 - Gondor elites cost Castar, BN upkeep halved (#600)

`c5b84fb4`

Backport of #600 from bannerlord-1.5.x (ef6afeb6's SpecialResources hunks
plus d9bd8bc2), only these changes and nothing else from that line.

Every Gondor troop at level 41 and above, sixteen in all, now carries an
upgrade_cost and a daily_upkeep at the Mordor band (L41 4 / 0.2, L46 5 /
0.3, L51 6 / 0.4); until now Gondor's only rows were eight merchant-only
emissary prices, so a Gondor player promoted into Fountain Guards for
gold and XP alone. The two a notable can hand out directly (Ithilien
Ranger, Fountain Guard) also carry a recruit_cost equal to their upgrade
cost, because TaomVolunteerModel seeds empty slots from the pools at any
level and upgrade_cost never fires on that path. The Black Numenorean
line's daily_upkeep is halved again to 0.03 / 0.05 / 0.08 / 0.1 / 0.15
on player feedback; the 2026-09-11 rescale shipped in v2.0.29 on the
other line, so the report likely predates it.

Codex review 112 found the recruit gate itself bypassable: the recruit
view's Confirm hotkey calls RecruitmentVM.ExecuteDone without reading
IsDoneEnabled and OnDone rechecks gold only, so the greyed Done button
was the whole gate for every recruit_cost troop since Patch51 shipped.
RecruitmentVM_ExecuteDone_Patch (same category) re-runs the cart
verdict at that boundary; RecruitCartGrouping is the shared pure fold.

Three shipped-data tests, four patch tests, the co-op veto disposition,
the registry row, the API snapshot row, feature doc, RCA, two lessons,
the review record and the Codex prompt come with it.

Research: v1.4.8 dump, RecruitmentVM.cs:867 ExecuteDone, :882 OnDone
  (gold only), SandBox.GauntletUI GauntletMenuRecruitVolunteersView
  Confirm -> ExecuteDone, RecruitPrisonersCampaignBehavior.cs:130
  OnUnitRecruited per unit: identical to v1.5.3 on every point the
  patch relies on
Not-tested: no C# build on this branch. This machine holds only the
  v1.5.3 install and a 1.3.15 backup; bannerlord-1.4.5 does not compile
  against v1.5.3 (six pre-existing errors in BannerBearers, BattleBalance,
  CulturalFeats, Execution, PrisonerRecruitment, none in
  SpecialResources). The same source compiled and passed 9,219 tests on
  bannerlord-1.5.x; here validate_moduledata.py (0 errors), the XML
  parse, the three data-test invariants replayed in Python and the
  tool tests (154) ran green.
Save-compat: none, cost rows read at load; the patch adds no state

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

### Performance

#### perf(enlistment): v2.0.28 - the commander-party check is an id compare

`ed93fd0b`

Checking whether a party is the enlisted soldier's commander's party
(a career quest's capture check and the stuck-battle sweep) built a
full picture of the commander, with a hero lookup and several names
turned into text, only to compare one party id. It now reads the party
id alone. Nothing a player sees changes.

Ported-from: f21123c6aed80344e47a52f5468195a0c3561997

#### perf(career): v2.0.28 - perk hit lines once per battle with a summary

`b7439900`

The per-hit [CareerPerks] amplification and reduction lines from #613
were written for every hit a passive changed. A TroopDamage or
TroopResistance pick touches every troop in the hero's party, and on
this line FileLogger writes every DEBUG line, so one battle could add
thousands of lines. Each combination of direction, hero or party
leader, hit mask and passives now writes its first hit at DEBUG and is
only counted after that; the mission end writes one INFO summary line
per combination. The summary is also written when the mission's
behaviours are removed without a normal end.

Research: taom-src v1.4.8 Mission.OnMissionStateFinalize removes every
 behaviour (:2207, :2219; OnRemoveBehavior at :4645); MissionBehavior
 OnEndMissionInternal (:121) and OnRemoveBehavior (:130)
Not-tested: a battle with a TroopDamage pick writes one hit amp line,
 then the summary at mission end
Ported-from: e0ad0db765d3f6f47cbce580eb8eb5662377d120

#### perf(enlistment): v2.0.28 - map profiler times the service wait menu

`079fdec3`

With "Enable Map Profiler" on, the campaign map profiler now also
times three things on the per-frame path of the enlisted soldier's
service wait menu: the game's menu redraw, TAOM's menu link colouring
and the status board refresh. They answer whether the service menu
costs more per frame than a vanilla wait menu. Nothing is patched
while the profiler is off. The menu redraw joins the two other
methods only the profiler patches on PatchShield's exclusion list,
so switching the profiler on does not change how an error there is
handled.

This changes TAOM.Dependencies: a release that contains it must ship
TAOM and TAOM.Dependencies from the same build.

Research: taom-src v1.4.8 GameMenuVM.OnFrameTick and ContextText
Not-tested: the menu probes in game (steps in map-perf-profiler.md)
Ported-from: 943ab74c03edfcc6cc124ef14a1b75db7ef038f4

#### perf(enlistment): v2.0.28 - the service board is drawn once per change

`6152f34f`

The service wait menu redraws its text every frame, because the game
treats a menu text with no attributes as changed on every frame, and
TAOM handed it the status board as a nest of texts that was rebuilt
from scratch each time. The board is now turned into plain text once
each time it changes, so each frame only redraws finished text. A
change of game language reaches the board within one status refresh
(about 2 seconds). The board's line breaks no longer depend on the
game having set its newline marker earlier, so a board written right
after a campaign starts keeps its lines apart.

A log line now records when a soldier's battle join is seeded from
inside the village he stands in while a raid reaches it, so a game
run can confirm which menu the game hands him there.

Research: taom-src v1.4.8 GameMenuVM, MBTextManager, StartBattleAction
Not-tested: the board in game; the raid-from-inside join line
Ported-from: 943ab74c03edfcc6cc124ef14a1b75db7ef038f4

#### perf(mixed-formations): v2.0.28 - lock-free path for plain formations

`c41b7d5f`

Patch30 on Formation.GetOrderPositionOfUnit runs about twice a second
for every AI unit on the engine's worker threads. Each field-battle
call allocated a FormationAdapter, read the MCM switch, took the
service's global lock and asked for the cavalry flag before learning
whether the formation had a layout, which almost none do.

FormationLayoutService now keeps its layouts in a ConcurrentDictionary
keyed by reference, with the adapter the mission behaviour passed;
every write still holds the lock. Patch30 asks FindLaidOutFormation
first: no layout means vanilla with no lock, allocation or formation
read; a laid-out formation reuses the stored adapter and keeps the
evaluating cavalry gate. A throw in the prefix, silent before, is
logged in full once per mission with a count at mission end. Keys use
ReferenceIdentity because v1.4.8 Formation.GetHashCode reads
Team.TeamIndex and the order preview builds team-less formations.

Research: taom-src v1.4.8 Formation.cs 1239, 2423-2425, 512-534,
1810-1813, 1943-1983; FormationQuerySystem.cs 195-197, 431-440;
OrderController.cs 99, 1338, 1443-1452; Agent.cs 1592, 3708, 4684-4726
Not-tested: the prefix on the worker threads (game-tested, ADR-008)
Save-compat: no saved state touched
Ported-from: 13f34c1cbc7c43f9d52f8c9df94af60e01065bc2

#### perf(creatures): v2.0.28 - tree tick checks slots only when due

`92962e5e`

BehaviorTreeAgentComponent.TickOnMissionThread made two native reads
(Agent.IsActive and the slot identity check) on every tick of every
scheduled tree, due or not. It now adds the elapsed time first and
makes both reads only when the tree is due, still before RunTree.
BehaviorTreeMissionLogic.OnMissionTick copied the schedule with
List.AddRange, which on .NET Framework allocates a temporary array of
the whole schedule every frame; it now copies element by element.

Research: installed .NET Framework mscorlib List<T>.InsertRange
copies another collection through a new T[count] (ilspycmd)
Not-tested: nothing beyond the IL pins; behaviour is unchanged
Save-compat: no saved state touched
Ported-from: a498e357e43e9a5a834c1fd7af0d8a5788cfbf3c
Ported-from: 70320bbe1b16a1b5dc5fa4f151f2a95fcc3403ad

#### perf(creatures): v2.0.28 - fewer allocations in creature battles

`e8656e54`

Creature trees and the spatial grid did work in every battle, with
or without creatures. The trees (wargs, spiders, elephants, mumakil,
war rams) now time sleeps, waits, cooldowns and rage with
DateTime.UtcNow: no time-zone conversion per node per frame, no hour
jump at a daylight-saving change, still wall-clock time. The selector
reuses its child lists. BehaviorTreeMissionLogic returns before
building arguments or parking an off-thread replay when no tree
listens; taom_debug.log gets one INFO line at the first such skip
and a mission-end summary.

SpatialGrid reuses two maps and their lists, removes a deleted agent
from its one cell, and skips a scheduled rebuild nothing queried; the
next query on the mission thread rebuilds first, an off-thread query
never builds, and removals stay parked for the mission tick (#634).
One INFO line each per mission records the first skip and the first
rebuild a query made. The grid's reuse test compares lists by
reference through ReferenceIdentity, a small comparer added here.

Research: taom-src v1.4.8 Agent.cs 4992-4995 (GetHashCode returns the
managed creation index, so the grid's Agent-keyed maps are safe)
Not-tested: warg, spider and elephant battles in game; the
[SpatialGrid] and [BehaviorTree] lines in a battle without creatures
Save-compat: no saved state touched
Ported-from: efe326242585a920e33238b3da20e88ee50f0a32
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1

#### perf(warg): v2.0.28 - warg trees resolve once, scan buffers, x,y grid

`687f9e2d`

Warg battles paid per-tick costs for nothing. The tree nodes resolved
their services from the container on every evaluation, each enemy
scan allocated a new List<Agent>, and a live bite fetched a native
Skeleton wrapper (ref count, global lock, GCHandle, finalizer) for
every captured target before any range check.

WargBehaviorTree.BuildTree now resolves both services once per tree
and injects them into the four nodes that use them; the rider-hand
check reads the mount's Monster, so IAgentAdapter.IsWarg is gone.
Scans fill reused buffers. SpatialGrid keys cells on x and y only (a
60 m scan looks up 49 cells, not 343; the distance test stays 3D).
BoneCheck range-gates each target before its skeleton (a positive
test, so a NaN frame fails it) and fetches the attacker's skeleton
once per tick. BoneCheckDuringAnimation reads the action progress
once and fetches the skeleton only inside the hit window, so a
skeleton missing during the wind-up ends the bite only if it is
still missing when the window opens.

Research: taom-src v1.4.8 MBAgentVisuals.cs 145-148, NativeObject.cs
32-43 and 62, Skeleton.cs 9, 75 and 95, ActionIndexCache.cs 5 and
482-485, Agent.cs 782, 1001, 2748 and 2773
Not-tested: a live bite in game (warg Custom Battle owed, #659)
Save-compat: no saved state touched
Ported-from: 87f9f862e10323d22efffd693686b4d75ac8b3be
Ported-from: b8b1446353491f2ba06c2edd35d03c492165b7c0
Ported-from: e5f644da4ade79caf67263a7f5b314f6856b2cd6
Ported-from: 7577894d4623f5a9aecb432e7ca4184864d64f4c
Ported-from: 2420761ba6b9231ce5b26a9309445fc3d19d98df
Ported-from: fe8f30c7abfca4ba1cfb590df8013b82757709e4
Ported-from: 56eb4bc81414615ad9fde5cc8e773a4d7521cd68
Ported-from: 23f6f85bd1433cb0306533093b3b935f46c53c65
Ported-from: 5d9d4cc974d21c1980a9f20a8a06a9a3de5b2afd
Ported-from: 516380d2efcc890f948f5cef3d8b2987e280e107

#### perf(campaign): v2.0.28 - refuge, supply and camp settings read once

`bb6ec0cd`

The refuge defence bonuses are read on every hit against a refuge's
defenders, and the supply-line and field-camp switches every campaign
frame; their providers now keep the Mod Options object and read through
it, still clamped.

Not-tested: in game, change the Refuge defence bonus and the Supply Lines switch in Mod Options and see each apply
Ported-from: bb68eeca3aeee49ba95ea4956f2b3941820a3f93
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7

#### perf(campaign): v2.0.28 - cheaper campaign settings reads and filters

`09d3ebe8`

Campaign code that runs per party per hour, per party and garrison per
day, per caravan score or every map frame looked up Mod Options on
every read. Seven settings providers (castle recruitment, caravan
trade, alignment desertion, battlefield promotions, quick actions, time
acceleration, map figure scale) now keep the object. Alignment
desertion skips owners that cannot lose troops before copying a roster,
the refuge listener skips battles while the player has no refuge, party
speed walks a roster only for the Rohan infantry feat, and the culture
market builds its routed item sets once and counts a town's guaranteed
items in one walk. Every recruitment, desertion, speed, price and stock
result is unchanged.

Research: the culture market keeps 1.4.x behaviour: no pool-keep (1.5.x armour acquisition, 301cd338a)
Not-tested: in game, Map Figure Scale and the fast-forward multiplier apply without a restart; a Free lord sheds Evil troops overnight; an Isengard town stocks its warg items
Ported-from: 423d948d89f115ddcde6709ed9a8a1d23b88bb13

#### perf(battle): v2.0.28 - read MCM settings once, not per blow or frame

`841079a0`

Battle code looked up the Mod Options object on every read, and some
reads run on every melee blow, every frame or every agent stat update.
The combat mechanics, blow diagnostics, Mixed Formations, Companion
Tactics, Dread Aura, Smart Cavalry AI and siege prop diagnostics
providers now keep the object from their first successful lookup; a
change in Mod Options still applies at once, mid-battle included.
Cleave, stagger immunity, crush-through and the race modifiers test the
cheap facts of a hit before reading a setting, the stance-cancel patch
checks the player's team first, and the English text override looks an
id up without cutting a new string. Every result is unchanged.

Research: GlobalSettings<T>.Instance asks BaseSettingsProvider.GetSettings on every read (read on MCMv5 5.12.1; CountingMcm counts it on the pinned 5.12.3)
Not-tested: in game, change a Combat Mechanics toggle, Smart Cavalry AI and Dread Aura in Mod Options during a field battle and see each apply
Ported-from: 74e9172d42e7482453b6caa25112cab16f64242d

#### perf(battlebalance): v2.0.28 - read the MCM settings once per process

`3a63d18a`

The battle balance settings were looked up in Mod Options on every
read, and the engine reads them once per casualty, twice per hit that
earns experience and once per roster row in every strength sum. The
provider now keeps the settings object from its first successful lookup
and reads through it; a change in Mod Options still applies at once.

Research: DryIoc 4.8.8 still selects the single public constructor (Provider_ResolvesFromARealContainer)
Not-tested: in game, change a Battle Balance slider mid-campaign and see auto-resolve follow it
Ported-from: 7feca96bed1a91cade702ac4432c35b8408b2288
Ported-from: 02157b18ba69c48c5a172024ebe4dd0685f5a806
Ported-from: ab70555dbbd1b5107d8c345cc730ab1d40719e9d

#### perf(diagnostics): v2.0.28 - cheaper always-on mission diagnostics

`202ee14e`

Three diagnostics that run in every battle stop paying per-frame or
per-spawn costs for lines they rarely write. The action-set census
(first 5 seconds of a mission) checks each agent on an integer key and
reads names only for a new action set, race and sex; its lines are
unchanged and two INFO lines now open and close the window. An empty
Companion Tactics postfix on Mission.OnTick is deleted. A banner colour
store that every spawned agent wrote twice and nothing read is deleted
with its patch member and cleanup behaviour; troop armour tint still
comes from the Mission.SpawnAgent prefix.

Research: taom-src v1.4.8 MBActionSet.GetHashCode returns its index (MBActionSet.cs:31-33); Agent.ActionSet builds a new MBActionSet per read (Agent.cs:696)
Not-tested: in game, census lines in a mission's first 5 s, clan-coloured troops keep their tint, OOB preset buttons work
Ported-from: e0ad0db765d3f6f47cbce580eb8eb5662377d120

#### perf(lords): v2.0.28 - lords.xslt sheds 360 KB of runaway indentation

`35e5eb7e`

82 skill blocks in lords.xslt had grown thousands of spaces of
indentation (up to 4,960 on one line; 320 lines over 200), about a
quarter of the file. Each block now sits at its </face> line's level,
taking the file from 1,493,730 to 1,125,090 bytes. The change is
whitespace only: the stylesheet gives byte-identical output under lxml
and under the .NET Framework transform the game uses, checked on
SandBox's lords.xml and TAOM_Map's spnpccharacters.xml. A new tools
test fails if any block regrows. The game parses the stylesheet once
per load (once per process with the XML merge fast path), so the load
gain is small; the lord tools that parse the file get faster.

Ported-from: 5e45d2782c6acc0a591bd4da9454977071c0345c

#### perf(campaign): v2.0.28 - switch off ButterLib's Distance Matrix

`bbe9bbb8`

ButterLib, which TAOM ships, keeps a Distance Matrix of every
settlement, clan and kingdom. Its GeopoliticsBehavior builds the full
settlement table at every load and new game (about 355,000 pairs on
TAOM's map) and rebuilds the clan and kingdom tables whenever a
settlement passes to another clan: about 170 ms, up to 281 ms in one
frame, per siege end (yotthani, v1.5.3). Nothing installed reads them.

TAOM now calls ButterLib's own Disable() on that subsystem at the
first main menu, after the TAOM.Dependencies submodules (ButterLib,
MCM) have run their own main-menu hooks, so ButterLib never adds the
behavior. One [ButterLibDistance] line says whether it was switched
off, was already off, or could not be.

Research: decompiled ButterLib 2.11.0 Implementation.1.4.5
DistanceMatrixSubSystem and its game-start gate; binding test against
the vendored 2.12.0 Implementation.1.4.8
Not-tested: in game (the log line; no stall at a siege end)
Save-compat: GeopoliticsBehavior's SyncData is empty
Ported-from: 1325c36ea8d14bcba9dc9d0a3f6f5087c63833a9

#### perf(xml-merge): v2.0.28 - merge module XML in one document per load

`058d37b1`

Every campaign load and custom battle merges each module's ModuleData
XML into one document per type. On v1.4.8 the engine converts the
whole document merged so far to XDocument and back for every file and
compiles every XSLT on every call (MBObjectManager.cs:966-1010), so
the cost grows with the square of the merged size: one player's load
spent 27.2 s of 70.5 s there, 15.1 s on NPCCharacters alone.

The fast path (Patch99, XmlMergeModule) runs the same loop with the
engine's own loader, merge and converters in the engine's order, but
keeps one document across the files and compiles each XSLT once per
game process. Only validated merges take it. It stands aside while
another mod patches a method it bypasses, re-runs the engine's merge
on any exception, and turns itself off for the session if the engine
succeeds where it failed. It applies only when the loaded
TAOM.Dependencies.dll keeps PatchShield off CreateMergedXmlFile.

The live harness merged every type of the installed v1.4.8 module set
both ways, in the players' module order, for a campaign and a custom
battle: identical output, heavy types at 18.4 percent (campaign) and
11.4 percent (custom battle) of the engine's time on the laptop, with
the installed Armory v2.0.26. In-game saving not yet measured.

Research: taom-src v1.4.8 MBObjectManager merge bodies and XmlResource;
IL fingerprints of CreateMergedXmlFile, MergeTwoXmls, ApplyXslt,
ToXDocument, ToXmlDocument
Not-tested: in game (the [XmlMerge] start, per-type and summary lines;
 a saved-campaign load against the rgl merge window)
Save-compat: no save data
Ported-from: a27c6b33817f6375f793b37c3892f897450e4bfe
Ported-from: 43ca92ee99c064467113e603c19c58a5901b9093

#### perf(cheat-screens): v2.0.28 - cheat mode no longer lists every item

`d7d71885`

With vanilla cheat mode on, opening your own inventory listed every
item in the game (5,166 with TAOM's content) and the party screen
listed every troop. A 2026-11-01 player log shows one such open
(cheat mode inferred) growing the managed heap from 489 to 2,841 MB
and silencing the game for 31 s on a 16 GB machine. TAOM's taom.*
console commands need cheat mode, so a player who turns it on for a
rescue command paid this at every open. Both screens now open as
they do without cheat mode unless MCM, TAOM Battle Load Diagnostics,
Map Performance, "Cheat Mode Lists All Items And Troops" is on
(default off). One chat line per launch says so. Loot, trade and
stash screens are unchanged. The [MemStation] note in the feature doc
is corrected too: a re-entry reading follows one collection, not two.

Research: taom-src v1.4.8 InventoryScreenHelper.cs:174-196 and
PartyScreenHelper.cs:185-195, one Game.get_CheatMode read each in
the installed IL; Game.CheatMode reads NativeConfig.CheatMode
Not-tested: both screens in game with cheat_mode = 1
Save-compat: no saved state; one new MCM key, default off
Ported-from: af990ca769da52f408b8e804d127c446b515a379

#### perf(patchshield): v2.0.28 - skip callback shims, no per-call lookup

`4c31d214`

PatchShield (TAOM.Dependencies) no longer wraps the engine's
native-to-managed callback shims. Its second pass at the first game
start attached a finalizer to each: this laptop's diag.log of
2026-09-02 shows that pass attaching 354 finalizers, about 247 of them
on the shims. Crash capture already covers the shims it needs.

Its finalizers no longer take Harmony's original-method parameter,
which made every shielded method call GetMethodFromHandle on every
call; after a throw the shield finds the method from the stack. What
it swallows, rethrows and unpatches is unchanged. A method-level
exclusion list joins the namespace list, starting with
Mission.TickAgentsAndTeamsImp: a swallow there would skip the line
that marks the agent tick done and freeze the next frame.

diag.log: the pass line reports seen and attached methods and its
time; a skipped method is named once; a repeating swallow line is
written once and counted, with a running count at each mission start
and a total at a clean exit; pass 2 is said to run at game start.

TAOM.Dependencies changed: a release must ship TAOM and
TAOM.Dependencies from one build.

Research: Harmony 2.4.2 (as shipped) GetOriginalMethodFromStackframe,
Patches.Owners; taom-src v1.4.8 Mission.TickAgentsAndTeamsImp,
MBGameManager.OnGameInitializationFinished,
MBSubModuleBase.OnBeforeMissionBehaviorInitialize
Not-tested: pass timing and the skip line in a live diag.log
Ported-from: 2a3f71b12b95be0a7c7f46583ca99de20faafe0f
Ported-from: 73792944591e214595515f7c68bbb7c3265b0e83
Ported-from: 578ac7d617f1fd0ba62827c9f6960611708c4cb4
Ported-from: 31a31f16b4ca3ac8384066b23968beac26e38afd
Ported-from: 0bf2409e668db7382e02ea3d0ce2162ddb65cacb
Ported-from: 900920dca4daadbc2684c80e2f5c997f2235b395
Ported-from: 88408869fbbfcc7d1aa68c145b71fa9dd8645dd4
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 6eb2e1b90c2fe42d6cda851434c0ff6ec1fdcd1c
Ported-from: 5ee49d05f1d24e060ecf597cdd75724f1d545618
Ported-from: d90b4be31ad6988ac120d355747d5ebb0926e4df

#### perf(crash-report): v2.0.28 - capture wraps 16 callback shims, not 247

`7bea5421`

Crash capture patched every static method of the engine's three
*CallbacksGenerated classes at launch: 247 finalizers in a 1.4.8
player's log, on per-agent and per-hit callbacks. It now patches an
allowlist of 16: the screen early, late and input ticks, the
scene-script tick, thumbnail and tableau rendering, and ten mission
combat callbacks that reach TAOM code. A throw inside one of the other
231 is no longer swallowed by TAOM; in the agent tick a swallow left
the next frame waiting forever.

A capture on a thread other than the main one now skips the Mission
and Campaign collectors and the crash inquiry, even for the runtime's
preallocated out-of-memory exception. Both MCM toggles (Enable Crash
Capture, Enable Native-to-Managed Capture) now apply without a
restart: they were read at launch, before MCM had built its settings.
Four finalizers that could never fire are gone, a crash that repeats
every frame logs at occurrences 1, 2, 10, 100 and so on, and a
handed-back exception keeps its throw site.

Needs TAOM.Dependencies from the same build (RethrowStackPreserver).

Research: taom-src v1.4.8 ManagedCallbacks.EngineCallbacksGenerated and
CoreCallbacksGenerated (all 16 names), Managed, Module, ScreenManager,
Mission, ScriptComponentBehavior, MissionView, MissionBehavior,
MBSubModuleBase; MCMv5 5.12.1 MCMSubModule
Not-tested: the boot line, a live combat-callback capture, live toggles
Ported-from: 8bd44b92456917597c3d70c00f6f02e2829f5464
Ported-from: 1d94df9c48666490fdf2124df123ebf7b6c46ed4
Ported-from: 9cfb91b151c6b0bd8434bba6f4682710a2cd7e7e
Ported-from: fb52e619d1af26f7fb978b58f4aba0eaa04a83ce
Ported-from: 65b691b6a745d622afa66eca8e39a3751219a047
Ported-from: 70727529052be7948305405087633d600a08ca0e
Ported-from: 42624b95bc1c065a456e72fec4b715e39c90410a
Ported-from: 8e6b0935cab35e31b127d5877d1b1aa93a98d932
Ported-from: 8c84fa20e972521ddd79c67f9153cb0ceb8d9fa2
Ported-from: 9e8c99bdb21de8f31e1d5ecb2bc94bb1c3784e1a
Ported-from: 5f6a96d2eb678143444b6113d5b91534a6fd931f

#### perf(diagnostics): v2.0.28 - load finalizers stop binding the method

`f201fb98`

Two always-on load hooks no longer ask the game which method they
patched on every call; each now knows its own name. Their log lines are
unchanged and every load does less work per saved object.

Research: 1.5.x 5ee49d05f measurement of the same binding
Not-tested: load time in game

### Refactoring

#### refactor(banner-colors): v2.0.28 - remove a patch that changes nothing

`446c54fe`

ClanPartyItemVM_GetCharacterCode_Patch can change nothing on v1.4.8.
For a hero, the private GetCharacterCode returns
CampaignUIHelper.GetCharacterCode, which TAOM's own patch already
recolours. For a non-hero, BannerHeroAdapter.GetClanColorInfo returns
null and the postfix returns early. The patch, its Initialize line and
its registry rows go.

Research: v1.4.8 ClanPartyItemVM.GetCharacterCode :1331-1342
Not-tested: a clan screen party row's hero portrait with clan colours on
Ported-from: 8dbdfeb125dcff6555eb00e4abac018131eec262

#### refactor(race-age): v2.0.28 - drop an offspring rule vanilla repeats

`d1fa877a`

TaomHeroCreationModel held one override, and it restated what v1.4.8's
DefaultHeroCreationModel already does: a son takes his father's
character as a template and a daughter her mother's. The class and its
AddModel line go. Behaviour is the same. The docs now name the vanilla
model, and Patch13_RaceAge is untouched.

Research: v1.4.8 DefaultHeroCreationModel.GetCharacterTemplateForOffspring :346-353
Not-tested: a child born in a campaign takes the same-sex parent's race
Save-compat: none; a GameModel holds no save data
Ported-from: 7983854e79d87e985bc09cc6f19defb70eecfe64

#### refactor(race-age): v2.0.28 - pregnancy rules move into the service

`ed0929d6`

The daily pregnancy formula and its immortal and fertility guards move
from TaomPregnancyModel into RaceAgeService, behind plain ints and
floats, and the model keeps only the Hero reads. The guards keep their
own call so no clan is read first: a clanless immortal still gets 0.
Every input gives what it gave.

Research: PregnancyCampaignBehavior lets a clanless hero through (1.5.x finding; the model keeps the order either way)
Ported-from: 79d90c08f518e757ff629a5b69c0aca8c72a9541
Ported-from: 174a7e898fff45a72675319e2dbc8ed5a933e3d3

#### refactor(stale-character): v2.0.28 - write checks through IOutcomeLog

`44dd1d74`

The hollow-parent repair (#1076) wrote its [Check] and [Swallowed]
lines through IModLogger while IOutcomeLog was missing from this
line. It now writes them through IOutcomeLog, as on 1.5.x, so they
count in the [Session] summary and a repeated swallow is counted
instead of logged again. The line text is unchanged; a fail line is
now written at INFO, like every outcome line.

Research: none (no engine member changes)
Not-tested: a birth with a hollow parent in a real campaign
Ported-from: 1165a00dec70c2b37b35dce4b97fa8507f7f3272
Ported-from: de90a24514689b1f60afdd32971ffdb6afff141e

#### refactor(tools): v2.0.28 - one table of English loc sources

`28de2b98`

The translator and the template writer read one ordered table,
tools/_loc_sources.py, instead of hand-kept lists. The template writer
no longer crashes printing a non-Latin language tag.

Not-tested: none (tool only)
Ported-from: 9e2a39f4092dd32c7bd58e193e24ee210594f26f
Ported-from: 303bf2a7d9db038474eca90af2a6bced8047868c

#### refactor(enlistment): v2.0.28 - drop a battle rule that never ran

`359cae82`

A rule meant to keep the service away from a battle's loot and
prisoner screens never ran in a game: the check it needed was never
passed to it. It was not needed either. The player stays in his
battle until those screens close, and an older rule already waits for
that. Turned on, it would have made a soldier released right after a
battle wait forever for an encounter that was already over. The rule
and its check are removed, and the comments that described the wrong
battle order now cite the game's own code. Nothing a player sees
changes.

Research: v1.4.8 dump PlayerEncounter.Finish (FinalizeBattle :1071,
  FinalizeEvent :1909), MapEvent.FinalizeEventAux (HandleMapEventEnd
  :2158), MapEventSide.cs:434, MapEvent.Update skips FinishBattle for
  the player's event while an encounter is open (:1041),
  JoinBattleInternal (:945)
Ported-from: 3b848c4111db58b86be10f126b643839ed8cb2b6

#### refactor(enlistment): v2.0.28 - kit-tier members say tier, not rank

`15b10e32`

IssueForRank, HasIssuedForRank, HighestIssuedRank, RankToken,
ProbeRanks, AlreadyIssuedForRank and HighestIssuedEquipmentRank now
say tier. The quartermaster log line now prints AlreadyIssuedForTier.
Nothing a player sees changes.

The save key stays "kitRank", and a new test pins it, so a later
rename cannot wipe every save's kit ledger.

Rejected: the parameter and comment renames of the 1.5.x commit (they
  sit on the seven-rank ladder, which is not ported)
Save-compat: unchanged; the record still reads and writes "kitRank"
Ported-from: 5a8c73d26018d0e795a1efeccb4cf1057061f518

#### refactor(enlistment): v2.0.28 - EnlistmentRank becomes EnlistmentKitTier

`e8f3b474`

The equipment rosters key on four kit tiers, and the career rank is a
separate enum, ServiceRank, with the same four member names. The
kit-tier enum now says what it is, so the two cannot be confused.
Members, values and roster ids are unchanged. Nothing a player sees
changes.

Ported-from: 5c5072dbc2f5dc50189ddb04c3df663bc2b99839

#### refactor(enlistment): v2.0.28 - name the dialog gate for what it checks

`cee71c91`

IEnlistmentDialogGateService.CanRequestDischargeFrom is now
IsServingUnder. It is true when the player serves under the
conversation partner, and it gates the kit request, reassignment and
release lines, not only discharge. Nothing a player sees changes.

Ported-from: 01f02c3a0dd52746bc313c31b77bdb4ce58db9e3

#### refactor(alignment): v2.0.28 - add the one-entity side pair

`6a992a4c`

Adds SideIds, the id and the culture of one entity, and FactionSides,
the only place that builds one from engine objects: a hero in a clan
is the clan's realm (or the clan itself) with that realm's own
culture, a clanless hero is his own culture, and the player is the
player clan's realm. IAlignmentService gains a ResolveSide(SideIds)
extension. Nothing uses them yet, so nothing changes in play; the
next commit moves the alignment features onto the pair.

Research: v1.4.8 Hero.Clan (:499-503), Hero.MapFaction (:567-590)
Research: v1.4.8 Clan.MapFaction (:339-349), Clan.PlayerClan (:276)
Ported-from: 5e43518df7980ebaaec4d22a7370d2df726dd839

#### refactor(enlistment): v2.0.28 - remove enlistment data nothing reads

`44ab4f6c`

Enlistment kept data that nothing in the game read: battle win and
loss counters, a duty failure count, tournament wins, a duty's food
requirement, a training value, four reputation scores that were
updated but never shown or used, two events nobody listened to, an
unused petition withdrawal and an equipment payoff calculator that was
never called. They are removed, with their entries in the enlistment
settings files. Nothing a player sees changes.

Save-compat: old saves still load; the retired record keys are ignored
by ServiceContentRecord.TryParse (pinned by
ContentRecord_SaveWithRetiredKeys_StillLoads)
Ported-from: 218fa405376cb3072cc82c62667214165f41295c

#### refactor(enlistment): v2.0.28 - the daily column check skips unread work

`da499e08`

Once a day, and when the soldier asks for a duty, the service read
the commander's column: his food, his troops' tiers and wounds, and
every kingdom's wars. Nothing in the game used those parts. The check
now reads only what the duties use (a march, a siege, a garrison, an
army, an enemy close by), and an unused hour value is gone. Nothing a
player sees changes.

Ported-from: 388e89de080e043bd1b6b18665fed74a10b5e5dd

#### refactor(messengers): v2.0.28 - flag an open messenger conversation

`b6734002`

A messenger conversation sets up a real encounter and, for a target in
a settlement, moves the party into that settlement, so no engine state
tells it apart from a visit. A small singleton flag now records it:
set just before the encounter starts, cleared when the mission ends,
when it fails to open and at each session launch. Nothing reads it
yet; the next commit's session rules do. No change in game.

Taken from the messenger part of a 1.5.x change whose feature, Lord
Courtesies, is not on this line.

Ported-from: 06a1a1b71b6a178e1762800077cf2f8381603f2d

#### refactor(messengers): v2.0.28 - drop the dead HeroSnapshot.IsInMapEvent

`0fbbb095`

HeroSnapshot.IsInMapEvent was filled in two places and never read:
arrival waits for a battle to end by reading the engine directly. The
field and its two constructor arguments are gone. A comment now
records why ResolveHero scans with Hero.FindFirst: Hero.Find misses a
dead hero the engine has unregistered, which would drop the messenger
without the "messenger lost" notice. No change in game.

Research: v1.4.8 Hero.cs:2130-2138 FindFirst and Find; Hero.cs:1719
Ported-from: 464cbd37575cb1afde901dc9b77cd278fc679e45

#### refactor(combat): v2.0.28 - damage model glue moves to feature hooks

`0322ba6d`

Nothing changes in game. TaomCombatMechanicsModel, the one
AgentApplyDamageModel slot every combat feature shares, had grown to
215 lines against the 150 that ADR-002 allows an entry point (#737).
Each override is now base plus one call per feature, in the order
that matters, and the model is 85 lines.

CombatMechanicsHooks (CombatMechanics/Hooks) holds the four combat
services with their context builders and extractors, moved unchanged;
RefugeDamageHooks (Refuge/Hooks) holds the refuge defender reduction.
The horse-charge gate moved into CombatMechanicsHooks.ChargeKnockdown,
which declines every other hit before it builds a context, so every
verdict is unchanged, a NaN charge velocity included. The charge
penetration still comes from combat_mechanics_config.json through the
config provider. SubModule resolves the hooks from the container.
RefugeDamageHooks itself landed earlier with the #741 fix.

Tests: a ceiling test keeps the model under 150 lines; source pins
check which agent, origin and base result reach each seam; the hooks,
the refuge reduction and the container registration have their own
tests.

Research: taom-src v1.4.8 AgentApplyDamageModel (the ten overridden
members), AttackCollisionData :20 :82 :102 :108 :138 (test fixture),
MBRandom.RandomFloat (falls back when there is no Game),
MissionWeapon.CurrentUsageItem (null for an empty weapon),
SandboxAgentApplyDamageModel.DecideCrushedThrough :641-655.
Not-tested: a live battle; behaviour is pinned by unit and source
tests only.
Ported-from: f7e9bd0d230c57922859fd84e5184c903da22113

#### refactor(seams): v2.0.28 - move decision logic out of nine seams

`44d64660`

Nothing changes in game. Nine protected-virtual seams in
SupplyOrderService, RefugeService, WardenService and CampService held
TAOM decisions that no unit test ran, because each service's test
subclass overrides every seam: the supply payee routing and its
unreachable-payee fallback, the refund route and volunteer-slot walk,
the refuge raid-target filters and nearest pick, the peace release of
refuge-held prisoners, the warden companion filter, the promotion
policy, and the nearest town-or-castle search behind the camp and
refuge keep-outs.

Each decision now lives in its service as an internal method, and
each seam is one engine operation whose signature carries ids,
primitives and TAOM data classes only (the ADR-007 boundary-seam rule
this port series adds). The two copies of the fortification search
share FortificationSearch.NearestDistance, declared in CampService.cs.
Amounts, flags, log texts, filter order, the tie-break and the RNG
draws are as before. The port includes the 1.5.x review fixes: a
refuge's per-row peace check keeps a prisoner when the refuge or its
faction is missing, as the old walk did, and the promotion's rename
and enrol steps act on the hero the create step returned. 78 new
tests pin the moved logic.

Research: the four service files matched the 1.5.x parent of
a39575410 byte for byte; taom-src v1.4.8 MobileParty.MapFaction,
PrisonRoster and GetPosition2D, TroopRoster.GetCharacterAtIndex,
Hero.MapFaction, IFaction.IsAtWarWith, Settlement.GetPosition2D.
Not-tested: the seam bodies (gold transfers, roster, item and
volunteer-slot writes, the MobileParty.All and Settlement.All scans,
EndCaptivityAction, HeroCreator and the enrol actions) run only in
game.
Ported-from: a395754109cd26d829a83680dfce5d5dcf29cf55
Ported-from: 12b1c2003fbf2539f9df62246f70e655c20bfc6d

#### refactor(cache-rebuild): v2.0.28 - delete two unreachable scaffolds

`eb12746d`

Nothing changes in game. Two pieces of code that nothing ever called
are gone. Main/Adapters/IEditorSceneAdapter.cs was an adapter
interface with no implementation and no reference. The
EditorCacheRebuild Caching/ folder (PathReuseCache,
PersistentPathCache, their interfaces, NavigationPathCloner and
SortedPathKey) was a path-memoization scaffold that
EditorCacheRebuildIoC registered but nothing resolved or injected.
Both arrived in 6a80bac6 and never gained a caller.

Also removed: the reserved EnablePathReuse and
EnablePersistentPathCache config properties (never read), the 26
tests that kept the scaffold alive, and the ReflectionSiteBindingTests
row for PathReuseCache._store, which named a TaleWorlds.Engine type
that does not exist and only resolved to TAOM's own class. A config
that still carries the two keys loads as before (new test). The
feature doc stops promising that Phase 1 paths can be reused by Phase
2: on v1.4.8 Phase 1 keeps its path local and CheckBeingNeighbor
pathfinds with a different extra-cost multiplier.

Research: taom-src SandBoxNavigationCache (v1.4.8 :88 passes 1f,
:134 passes 2f); git grep found no other reference to the deleted
types on this line.
Not-tested: the in-game distance cache rebuild; no runtime path
changed.
Ported-from: 032481cc6d23d31e9f08de048bc4b96a2d3a1d99
Ported-from: 76f22e111a18673bc0d9a33be7090a787c1b4b98

#### refactor(core): v2.0.28 - one guarded SyncData chunk loop for momentum

`6d54efd8`

The War of the Ring momentum save passed its saved chunk count straight
to new List<string>(count), so a corrupt save threw on load (a negative
count) or ran out of memory (an absurd one). The chunker moves to
Core/Persistence as SyncDataChunker, and SyncChunks does the whole
pass: a count below 0 or above 4,096 is ignored with a warning and the
momentum loads fresh. The saved keys are unchanged, so existing saves
load as before, and a save from before the _v3 keys still loads fresh
momentum without a warning. Comments now give the engine's real string
limit, 32,763 UTF-8 bytes.

Research: v1.4.8 SaveContext.cs:308-321 writes (short)(4 + UTF-8 bytes)
Research: v1.4.8 CampaignBehaviorDataStore.cs:26-39 for EngineDataStore
Save-compat: keys _taom_wotr_momentum_v3_count and _v3_<i> unchanged
Ported-from: 16a34027dcd60e51baa9bacb47342eb582aa844f
Ported-from: ea8f2ad52d3a8bb0cfac3ddc9119b4b8e780bf5c

#### refactor(composition): v2.0.28 - move WandererAllegiance into a module

`fd62c36b`

Wanderer Allegiance (#575) is the first feature module.
WandererAllegianceModule registers the feature's services
(WandererAllegianceIoC now takes an IRegistrator) and declares its
dialog behavior. FeatureModules.All lists it, and its registration
line in Main/IoC.cs and its AddBehavior block in Main/SubModule.cs are
gone. The wiring test now checks the list entry, the absence of hand
registration and the service graph against a real DryIoc container.

The behavior is still a container singleton. It is now added after
every hand-wired campaign behavior, which changes nothing: its two
lines are the only TAOM lines on companion_hire and outrank vanilla's
reply (priority 110 over 100), and LotrIssueSuppression.SuppressAll
removes only its fixed list of vanilla issue behaviors.

Research: taom-src v1.4.8 CampaignGameStarter.AddDialogLine, AddBehavior
Not-tested: in-game wanderer refusal (needs a campaign)
Save-compat: the behavior's SyncData is empty; no save data touched
Ported-from: 44045b348dfd5ed0444c5868909d4180d2214a28
Ported-from: fa0b24e139ae81d606630e78513462dc4b626b75
Ported-from: 1efdf29852998922128ad97db1896d569b084df3

#### refactor(composition): v2.0.28 - add the feature-module runner, empty

`a3438b98`

Adds Main/Composition, the feature-module runner of the 1.5.x line's
plan 018, with an empty module list. A feature can now keep its wiring
in one XModule.cs listed in FeatureModules.All instead of lines in
Main/IoC.cs and Main/SubModule.cs.

IoC.Configure builds the runner after the last hand-wired registration
and runs module statics as its last statement. SubModule calls the
runner once per patch phase (process load, main menu, game init, first
mission), at game start and at every mission start, each call after
the hand-wired features of that hook. A module that throws is logged
and switched off for the session instead of breaking the load. With no
module listed nothing changes in game.

Trade-off: five small types and a few kernel lines before any module
uses them. Every later 1.5.x module then ports as one file and one
list line instead of edits to the two single-owner files.

Research: taom-src v1.4.8 CampaignGameStarter, BasicGameStarter,
 IGameStarter, MBGameModel`1, CampaignBehaviorBase, InformationManager,
 InquiryData, InformationMessage; v1.4.8 dump Mission.AfterStart,
 MissionState.TickLoading and FinishMissionLoading
Not-tested: the runner calls in a live game (empty list, nothing runs)
Ported-from: 9ed3ee308d3b65e3d22c1d81e1f63d0ed73cefab
Ported-from: fa0b24e139ae81d606630e78513462dc4b626b75
Ported-from: ecbc2199e49338916b912909586f105b2471b457
Ported-from: 0f3af95774a4c04429c0699dbfbebf4980bdc923
Ported-from: 1efdf29852998922128ad97db1896d569b084df3
Ported-from: 06252ae2c7efd68842d59ced471e8848c37d090a

### Tests

#### test(banners): v2.0.28 - the 20 major cultures stay pinned to banners

`5bb7fa1b`

The derived banner test only reaches cultures whose infantry appear in
taom_partyTemplates.xml, so it misses lothlorien, battania (Khand),
shaghana and abanissa. Deleting any of their CultureBanners rows would
have passed every test. The old pin on the 20 major cultures is back
beside the derived scan, so both gaps are covered.

#### test(tools): v2.0.28 - map scene memory tests run under unittest

`b5dbe9e4`

The tool suite runs with python -m unittest discover, but the tests for
audit_map_scene_memory.py were written for pytest: they imported it and
leaned on its tmp_path, capsys and fixture machinery. On a Python with
no pytest the whole module failed to load, so all 33 tests had never
run on this line and every suite run counted one error for it.

The tests are now plain unittest: a small base class hands each test a
fresh temporary directory, the module tree is built by a function,
assertRaises replaces pytest.raises and redirect_stdout replaces
capsys. Every assertion is unchanged. All 33 pass, matching the file's
own claim that it needs only the standard library.

#### test(tools): v2.0.28 - bound a militia veteran's ladder skill step

`6d5d061e`

The ladder exemption only asked the veteran to match the basic, so Bow
330 and Bow 40 on Dale's veteran both passed. The skill must now rise,
by at most 30 for each ladder band the launcher climbs. Every one of
the 15 exempt checks meets that today. The 30 is measured from the
committed troops, not a spec: this line has no skill_cell table.

Not-tested: none (mutations of Dale and Gondor run in memory)

#### test(tools): v2.0.28 - ladder launchers own a militia archer's skill

`b7041631`

The merged troop tree carries #617's ladder skills, so Dale's veteran
militia archer has Bow 95 against 55 and failed the flat elite step. A
Bow or Crossbow a ladder launcher owns now only has to match the basic
militia. The count stays 30. The 1.5.x skill_cell equality check is
left out because this line has no such tool.

tools/README.md warns against rebalance_troops --apply on ranged troops
until #617's ladder awareness is ported.

#### test(tools): v2.0.28 - stale module file refused through the CLI

`3a06bd63`

The unit tests called stale_module_files directly, so nothing proved
the --require-build gate calls it. Removing that call from
package_release.py made the new test fail; restoring it passes.

#### test(tools): v2.0.28 - every validator code has a row in the codes doc

`1b3a34ab`

The second half of WP136's hand-off, which 4604d882c left out. Plan 138
C8 item 4 adds test_every_emitted_code_has_a_row_in_the_codes_doc and
calls a code with no row in 2ca35d6e2 or f1675c058 a STOP. RED printed
one such code: SKILL_TEMPLATE_SHADOWS_SKILLS.

Ruling under D27, which gives STOP rulings to the delegated run: that
one row is written here rather than copied. The code predates this
programme (c7392d5af, 2026-08-31, #523) and is neither a WP136 nor a
WP108 code. f1675c058 only has SKILL_TEMPLATE_MISMATCH, and that text
describes v1.5.2 behaviour, so it cannot be copied to this line.

Research: BasicCharacterObject.Deserialize on the installed v1.4.8 DLL,
lines 337-358

#### test(bindings): v2.0.28 - repoint the GetCaptainTooltip line label

`c15740fc`

The guard around the manual patches moved the reflection lookup from
ManualPatchApplicator.cs:25 to :48, which
EveryLineLabel_PointsAtALineThatNamesItsMember caught. The DataRow and
its reflection-sites.md row now name the new line.

#### test(screens): v2.0.28 - stand-ins Harmony patches stay out of line

`af3094b2`

The managed-tests gate in .ai/verification.md builds Release, where the
JIT inlined two patched stand-in methods past the Harmony patch, so
PartyFastOpenPatchShapeTests and QuestLogPatchTests failed there. Both
passed under COMPlus_JITMinOpts=1. A test defect, not a product one.

#### test(core): v2.0.28 - race test reads cultures through the real loader

`70f18e54`

CultureRaceConsistencyTests parsed cultures.json with its own regex, so
it could disagree with what the game reads. It now goes through
CultureCreationDataProvider.LoadCultures, the loader character creation
uses, and finds the repo through RepoPaths, not its own walk.

Not-tested: none (test only)
Ported-from: 834e0703c9b9563fe78c97d708b71460d27fa66e

#### test(infra): v2.0.28 - gate banned constructs repo-wide

`c0255ced`

ADR-003 (no #region), ADR-004 (no Obsolete attribute) and ADR-005 (no
conditional compilation outside Main/IoC.cs) were prose. A test now
reads every .cs under Main, Dependencies and TAOM.Tests for the
directives, and reflects over TAOM, TAOM.Dependencies and TAOM.Tests
for the attribute; the tree is clean today.

Not-tested: none (test-only change)
Ported-from: affcee2ead013f29a4b059af5ea999be440e0998

#### test(infra): v2.0.28 - one engine-faithful data store fake

`35104b0c`

The career persistence tests used a private IDataStore fake that
skipped null on save and overwrote a duplicate key, so a test could
pass where the engine throws. EngineDataStore mirrors v1.4.8
BehaviorSaveData.SyncData (Add on save, TryGetValue on load) and is now
the one fake; RacePersistenceServiceTests keeps its type-keyed
RoundTripDataStore on purpose. The career test that loads an empty
store is renamed for what it models. The two session-reset tests on
this line (WarOfTheRing, SpecialResources) drop their private copies.

Research: v1.4.8 CampaignBehaviorDataStore.cs:26-39 and LoadBehaviorData :86-106
Not-tested: none (test-only change)
Ported-from: ea8f2ad52d3a8bb0cfac3ddc9119b4b8e780bf5c

#### test(migration): v2.0.28 - gate every ViewModelMixin refresh name

`a104a2a6`

UIExtenderEx 2.13.2 throws a NullReferenceException at
UIExtender.Register for a refresh name the engine renamed, which takes
TAOM's UI extensions down at startup. A binding test resolves each of
the five named refresh methods on its target view model the way
UIExtenderEx does and refuses a missing, overloaded or abstract one.

Research: UIExtenderEx 2.13.2 ViewModelMixinAttribute.RefreshMethodName
Not-tested: none (test-only change)
Ported-from: e7cdb2e96bae643e809c97df4edc9ce897da5a68

#### test(patches): v2.0.28 - one patch number names one feature

`940d3c56`

A new test reads every patch category (attributes and feature modules)
and fails when a PatchNNN number names two features, when a category
has no registry heading, or when a feature-map row cites another
feature's number; each scan has a floor so a broken scan fails instead
of passing. Patch63's two features stay listed by name. Six
sub-categories that earlier ports brought stay documented under their
parent's heading (Patch89, Patch98, Patch101), each named in the test.

Not-tested: none (test-only change)
Ported-from: 6ddcc8f9b3d097cfe8d31d203015c163a9346093

#### test(migration): v2.0.28 - pin reflection-site labels to their lines

`bf8e78b2`

A reflection-site label (File.cs:N) shows only in a failure message, so
a wrong one stays wrong; 17 line references had drifted and the
catalogue lacked one pinned row. Two game-free tests now check that
each labelled line still spells its member and that every DataRow has
its catalogue row; the labels are repointed and the convention is
written in the catalogue. Six MissionStartGuardSwaps labels drop their
second line, which reads a variable and spells no member. The two
NavigationCacheAdapter labels that passed only through a helper's name
now point at the name filter inside the lookup.

Not-tested: none (test-only change)
Ported-from: ee19f105269304b1fbdca1ca2cef088192f6a012

#### test(migration): v2.0.28 - feed installed IL through eight transpilers

`8d3a3c5e`

Every TAOM transpiler is fail-safe, so a missed anchor after an engine
update silently disables its feature while the target-binding gate
stays green. Eight new binding tests read the real v1.4.8 IL, run each
production transpiler, and assert the exact number of sites it rewrote
(or, for the disabled Patch15, that the banner-layer cap is still gone).

The plan listed nine. WP131 deleted Patch42_FillSettlements and renamed
the swapped call to CastleAiToggle.IsExcludedFromRecruitScoring, so the
ninth test has no transpiler to run and the Patch42 test reads the new
name. Later WPs' transpilers keep their own feature binding tests.

Research: taom-src v1.4.8 sites listed in each test
Not-tested: none (test-only change)
Ported-from: 8b9f0a233043c66883bd0c2487eca3f98f8bcff8

#### test(bindings): v2.0.28 - resolve patch targets the way Harmony does

`848ffde5`

The gate resolved targets with AccessTools.Method, which walks base
types and falls back to the parameterless overload, and merged a
class's attributes into one spec, so it could pass a patch Harmony
refuses at apply time; it never checked patch parameter names, and the
hand-applied CompanionTactics tooltip patch was outside it. It now
follows Harmony 2.4.2's own path per patch method (PatchClassProcessor,
GetOriginalMethod, TargetMethod rules, parameter binding, a body to
patch), covers the manual patches, and fails when ManualPatchApplicator
gains a class it cannot see. Game-free fixtures pin each rule and the
Harmony internals it reads.

Research: ilspycmd of 0Harmony 2.4.2 PatchClassProcessor, AttributePatch, PatchTools.GetOriginalMethod, MethodCreatorTools.EmitCallParameter; v1.4.8 ClanPartyItemVM.UpdateProperties is concrete (ClanPartyItemVM.cs:1125)
Not-tested: none (test-only change)
Ported-from: 197454e68e40946cc322cd1ec58ee4e21185f5fa

#### test(bindings): v2.0.28 - make the binding gate fail loudly on skips

`f16680ea`

The binding gate passed by skipping whenever the test process lacked
the game variables; MSTest reports Assert.Inconclusive as Skipped with
exit 0, so a run that checked nothing read green. GameAssemblies now
falls back to the game folder the test DLL was built against;
TAOM.Tests/binding-gate.runsettings maps Inconclusive to Failed, fails
a filter that matches nothing and opts the gate in to the live XML
merge harness; the discovery floors fail. Every documented gate command
passes the settings, and the native pin tables now fail on an unlisted
engine build instead of skipping.

GameAssemblies.cs took the series diff, not the file, under D28: one
comment on this line no longer names the deleted PathReuseCache.

Research: MSTest 3.1.1 and Microsoft.NET.Test.Sdk 17.8.0, the versions plan 008 was proven with on 1.5.x
Not-tested: none (test-only change)
Ported-from: 8c89e042fdecce1c117e8121e98d24fbaf4ec26d
Ported-from: 549afffde2646f3fce7a142e1ae4d4d3d7bd17ea
Ported-from: 2ca0805bcb34a76d6bac159a68f6bde1e2d32fe5
Ported-from: 37306bca39c6d6955ac15fc83b6123f9374e4490
Ported-from: aa59f68ed63b7da7cc3006d1f93170f5ca1c99b1
Ported-from: 1f504757290493c0220897e913eb924f9ba8d88f
Ported-from: a27c6b33817f6375f793b37c3892f897450e4bfe

#### test(ui): v2.0.28 - gate literal sprites in GUI prefabs

`8c4dc6eb`

An unknown Sprite name draws nothing and logs nothing. PrefabSpriteTests
fails a literal Sprite in a GUI prefab or a PrefabExtension source that
no SpriteData of TAOM's module set declares. The gui-tests data gate now
selects it.

It could not land with the data integrity gates because all 19 unknown
names sat in four dead prefabs: ROTsprite.xml, EventXml.xml,
mainmenuHUD.xml and PoppyHUD.xml. Those are now deleted. With the four
files restored temporarily the test failed listing those 19; without
them it passes. One name stays allowed, Map\anchor_stroked: NavalDLC
declares it, TAOM does not load NavalDLC, and three settlement nameplate
prefabs carry it on a hidden widget.

Adapted from the 1.5.x test: the FactionUI runtime sprite folder and
its tests are dropped, because that folder does not exist on this line.

Research: SpriteData of TAOM's DependedModule set and TAOM,
NavalDLCSpriteData.xml (installed v1.4.8)
Not-tested: none
Ported-from: 8caac7da5df694fbdebc0ec4581eb4ffc0ef1cfc

#### test(ui): v2.0.28 - data gates run the GUI prefab gates

`253868c0`

The one data-gate command now runs the nine prefab gates this work
added whenever a GUI prefab changes. Before this it selected only the
brush test, so none of them ran from it.

Not-tested: none

#### test(ui): v2.0.28 - gate stretch children inside CoverChildren

`c9488c45`

Gauntlet measures every visible child against the parent's whole space,
so a StretchToParent child of a CoverChildren parent with no max size
grows the parent to everything it is offered. This ratchet scans every
GUI prefab and fails a new case. Grid parents and parents with a max
size on the axis are exempt. Four older files hold the fault today
(MomentumView 5, CareerScreen 3, BattleActionBar 1,
PreBuildCharacterSelection 1) and their counts may only fall.

Research: DefaultLayout.MeasureChildren, GridLayout.UpdateCellSizes
(v1.4.8 taom-src)
Not-tested: none
Ported-from: e6dbea3e028e4a296815de81258741c7f9dea734
Ported-from: 53a4e4e52a2a67a1098edfb588ab7f5a71900056
Ported-from: d4fb51a5056be817c48692e6e4783b52bb9bdbfe

#### test(ui): v2.0.28 - gate prefab hints whose parent refuses events

`876c8d10`

A Gauntlet HintWidget relays only its parent's hover, and hit-testing
skips a widget with DoNotAcceptEvents="true", so a hint under such a
parent never shows and nothing logs it. The 1.5.x line shipped this
three times. This gate builds the XML of every in-code PrefabExtension
in TAOM and fails on that shape. It starts green on five documents,
with a floor of five so an empty scan cannot pass.

Research: HintWidget, EventManager (v1.4.8 dump)
Not-tested: none
Ported-from: 42148af4cd851687f55b5bf94195cb57287c5b35

#### test(ui): v2.0.28 - gate every prefab insert on where it lands

`363f51e9`

PrefabInsertPositionTests fails three silent UIExtenderEx mistakes on
the TAOM assembly: a Child insert with no Index of its own (it lands
first), a Child insert whose XPath does not end in /Children (the
template drops it), and an Append or Prepend on /Children (it lands
beside the container).

The gate names one patch on this line, the career energy bar. The bar
now states Index 0, the position it has always had, so nothing moves.

The review reference records the one accepted IoC.Resolve exception a
view model mixin needs: UIExtenderEx builds it through Activator, so it
cannot take constructor injection.

Research: UIExtenderEx 2.13.2 PrefabComponent.InsertAsChild,
PrefabExtensionInsertPatch.Index, ViewModelComponent
Not-tested: none
Ported-from: 69b47d00bcb77c3f689efab03c9a8f2756f687c3

#### test(gates): v2.0.28 - gates for entry points and service signatures

`ca7d2531`

Rules the docs claimed now hold for anyone who runs the suite. An entry
point stays under 150 lines, a service or adapter interface names no
TaleWorlds class but TextObject, and a service or view model never calls
IoC.Resolve. Both baselines list this tree's older offenders (17 entry
points, 75 service members) and only shrink.

The #371 pairing test ties Main's DependedModuleMetadata for
TAOM.Dependencies to the Dependencies module's version.

PrefabExtensionBrushTests gains internal GameDir and ModuleFolders;
DeclaredBrushes keeps its name. PrefabSpriteTests lands with WP140, since
its 19 unknown sprites all sit in dead files that plan deletes.

Constraint: one assembly on this line; no TaomAssemblies
Not-tested: none
Ported-from: 8caac7da5df694fbdebc0ec4581eb4ffc0ef1cfc
Refs #371

#### test(custom-battles): v2.0.28 - picker scenes exist, sieges name levels

`492e3fbf`

The Custom Battle picker's scene rows point at scenes that live in the
unversioned TAOM_Map, so a row can name a scene only one machine has,
and a siege opens "level_N siege" from the scene's own levels block. A
new test checks every row of custom_battle_scenes.xml against the
installed scenes, and each siege row against the three levels the
picker offers. Inconclusive without TAOM_Map. The file catalogue now
says who reads the file on v1.4.8.

Research: v1.4.8 BannerlordMissions.OpenSiegeMissionWithDeployment,
  CustomGame.LoadCustomBattleScenes
Not-tested: none
Ported-from: d96167efacacfdf265a478903f7f550dd2c1c1ff
Ported-from: babcb8335e373b40285a90cdcbcec685de2850ac

#### test(cultures): v2.0.28 - no XML comment inside a culture child list

`0abc69be`

The engine walks a culture's notable_templates by child node and reads
each node's name attribute, so a comment in that list throws inside
TaleWorlds.ObjectSystem, aborts the parse of the whole file, and every
culture below it never registers. The crash a player sees is somewhere
else. The test reads taom_spcultures.xml and allows comments only on
the root and directly inside a Culture element, the two placements with
a shipping record. The file is clean today.

Research: v1.4.8 CultureObject.cs:419-424, MBObjectManager.LoadXmlWithValidation
Not-tested: none
Ported-from: d28d9a7d3ce518782f1a7dedfc1cecc053978dd4

#### test(banners): v2.0.28 - gate every shipped heraldry banner key

`8e2e87de`

A new test fails if any banner_key in clans.xml, taom_spkingdoms.xml,
spclans.xslt or spkingdoms.xslt is not a whole number of groups of
ten integers. Bannerlord reads a banner code ten numbers at a time,
and a code that yields no group gives an empty banner, which the
native banner tableau crashes on. The engine itself banners a
formation captained by a lord who carries a banner item, with no
heraldry check, so one bad key in a clan or kingdom row could crash
a battle. All 239 shipped keys pass today; this keeps it that way.

Research: taom-src v1.4.8 Banner.TryGetBannerDataFromCode (:576-593)
Research: GeneralsAndCaptainsAssignmentLogic.cs:262-264
Research: DefaultBannerItemModel.cs:31
Ported-from: 736fa2fef65a96812648df924efca0b5153a84d0

#### test(logging): v2.0.28 - measure what a durable log line costs

`00a390d7`

Three runs on the maintainer's laptop, game closed, on mains power: a
durable INFO line costs 3.8 us on the thread that logs it (runs 3.8,
3.9, 3.7), 8.0 us when four threads log at once (8.3, 8.0, 7.8), and a
DEBUG line 0.4 us. A 1,602-line arena load burst costs about 6 ms. That
is under the 50 us bar, so the synchronous INFO path stays and no
background writer is added. No player-facing change.

Research: FileLogger drains INFO, WARNING and ERROR on the caller (FileLogger.cs:79-130)
Rejected: a bounded background writer (it would save about 6 ms per 1,602-line load at 3.8 us per line, and lose the last lines before a native crash, #350)
Not-tested: the cost on players' disks under 80 to 96 percent memory load (E4 memLoad) is unmeasured

#### test(supply): v2.0.28 - pin the path query and the arrow fallback by IL

`84f435d9`

Two IL-fingerprint tests from the 1.5.x port of PR #1064, which needed
IlCallScanner.Fingerprint (now on this line with the XML merge fast
path). ComputeNavPathPoints must read the navigation model's terrain
exclusions and the distance model's two region costs before it calls
AddLandPath, and must never call the native path query itself.
SyncArrows must check the scene's terrain heightmap before the one
terrain query and fall back to the scene's collision height with the
land mask SandBox uses (Moveable | OnlyCollideWithRaycast): the 1.4.x
fallback, rewritten from the 1.5.x test, which pins a map-wrapper call.

Research: taom-src v1.4.8 Scene.GetHeightAtPoint, BodyFlags,
MobileParty.NavigationType
Ported-from: ab78564e064e173bef9f8d23d22d46bf86e3b3aa

#### test(skeleton-buffer): v2.0.28 - native pin gate with v1.4.8 rows

`e44179fe`

Both per-build native address tables (the skeleton buffer guard and
the clip-budget scan) now carry the v1.4.8 row and the
BindingVerification tag, and NativePinGateTests keeps every such
table tagged and named in the engine-bump skill. On this line an
unlisted build still reads Skipped in the binding gate until plan
008's runsettings land (WP122); the skill says to read the skip
count. Players see no change.

Research: read-only scan of the installed v1.4.8 TaleWorlds.Native.dll:
ClipBudget load 0x23AADF, budget 0xB2BC9C reads 12582912.0
Not-tested: nothing runtime
Ported-from: 15e52d7af67fbb72b6fc9bee8047f107c0d52b78
Ported-from: c286e2393100e84e2b1d802cc124a46bd312bc74
Ported-from: fdee18f02132a259c6ea52c6c43be5e82f5ca93f

#### test(creatures): v2.0.28 - pin creature behaviours to MissionLogic

`6ebc7660`

The warg, spider, elephant, mumakil and war ram mission behaviours
each override OnMissionTick and must derive from MissionLogic, never
MissionBehavior alone (the looter-battle NRE). Only the war ram's doc
comment said so, and the test it named covered only
BehaviorTreeMissionLogic. A test now pins all five.

Ported-from: c79a585218ad22841c8db1f273e013385512b332

#### test(races): v2.0.28 - gate every character race against skins.xml

`8512e807`

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

#### test(composition): v2.0.28 - read SubModule and IoC through one reader

`54feaac2`

The 25 test files that guard wiring in Main/SubModule.cs and
Main/IoC.cs now read both files through RepoPaths.ReadSource with
comments stripped, and their own locator helpers are gone; no
assertion changed. The switch exposed one false pass: the GameModel
binding gate counted TaomPartyNavigationModel as registered because
a commented-out AddModel line contains its constructor call. The
parked model is now an explicit ParkedModels entry with its reason
(NavalTravel, #120 and #296), and a new test keeps that list honest
in both directions.

Not-tested: none (test-only change)
Ported-from: a0db0558b74b7e19594fbb8072650feb2fc132bf

#### test(infra): v2.0.28 - add RepoPaths, the shared repo source reader

`597dfb30`

Wiring tests on this line each locate and read Main/SubModule.cs and
Main/IoC.cs their own way, as raw text. RepoPaths is the one reader
the later ports rely on: RepoPath finds a repo file from its own
compile-time path, ReadSource normalises CRLF and fails on a missing
file, StripComments blanks comments while keeping offsets,
MentionsOutsideComments prefilters raw text, and CSharpFiles walks
the code roots. CodeRoots holds Main only, because the 1.5.x Client
project does not exist here. Only the RepoPaths parts of the commits
below are taken.

Not-tested: none (test infrastructure only)
Ported-from: 35007dbb8d0324efd71a49ce356636262225d9ab
Ported-from: a0db0558b74b7e19594fbb8072650feb2fc132bf
Ported-from: fa0b24e139ae81d606630e78513462dc4b626b75
Ported-from: 06252ae2c7efd68842d59ced471e8848c37d090a
Ported-from: 03d9db8dcf13432bf5b4d60e3bc63d0d60d60831

### Documentation

#### docs(plans): v2.0.28 - record the docs recount and park the gate work

`a8baac30`

D35 records the maintainer's word that the programme changes code
only, and parks the hook-gate commits on port148/wave10. Closing the
remaining gate gaps grew the shared shell reader from 1,432 to 1,994
lines, and two review rounds in a row found new bypasses the fixes
had introduced. The gaps are pre-existing and need deliberate
obfuscation, so the rewrite waits for the maintainer.

The integrator record carries the run at 494d12b2e: 13492 tests, 0
failed in Debug and Release, tool tests 4393 OK. Two session-start
hook rows failed under load; the branch does not touch that hook, and
both passed when re-run alone.

#### docs: v2.0.28 - recount stylesheets as 18 and the sweep as 836 files

`494d12b2`

check_external_xslt.py prints "PASS: 18 stylesheet(s) clean" on the
public 2.0.29.5 install, 2026-11-07: 9 in the repo (find -name
'*.xslt' under Main/_Module), 8 in LOTRLOME_Armory (physics_materials
is the one the old lists lacked) and 1 in TAOM_Map. Present-tense text
still said 16 or 17, 8 external stylesheets, and an /xslt-check
mapping of 6 of 8; it now says 18, 9 external and 7 of 9, since
lord_template_rosters.xslt has its own mapping row. The repo's
stylesheets hold 1,058 xsl:template elements (heroes and lords 397
each), not 1,063.

INDEX.md and doc-lookup.md still described TAOM_Map as outside the
reference sweep with 1,012 unchecked Culture. refs. Since #462 the
validator sweeps it: validate_moduledata.py names both Armory and
TAOM_Map as "Also sweeping refs in", and rglob over the three roots
gives 338 + 454 + 44 = 836 files. settlements.xml there carries 1,002
Settlement elements and 1,002 culture= refs; 299 of the Armory's XML
sit under Languages/ and none carries a prefixed ref.

tools/README.md pointed at taom_schema.py:1110 and :1142 for the
settlement-strip regex; its two uses are now at lines 2205 and 2246,
and /xslt-check's hardcoded transform path is SKILL.md line 57.

check_external_xslt.py's missing-folder error said "the 8 live ones";
there are 9 and the script cannot count them without the folder, so
it now names the two modules instead of a number.

Dated measurement tables (clans.md, recipe-new-mod-from-zero.md,
submodule-and-registration.md and validation-and-testing.md, all
2026-09-05), ADR-010 and doc-graph.md keep their figures.

#### docs: v2.0.28 - xslt-check and validation guide count 18 stylesheets

`7f31df84`

05d4affe0 moved the stylesheet count to 18 (9 in the repo, 8 in the
Armory, 1 in TAOM_Map) in CLAUDE.md and check_external_xslt.py but
missed two copies:

- .claude/skills/xslt-check/SKILL.md still said the repo holds 8 of 17
  under the same 2026-11-07 date. It now says 9 of 18; the other 9
  stay in the game install.
- docs/modding/validation-and-testing.md said 17 in two undated,
  present-tense lines. Both now say 18. The dated 2026-09-05 table
  keeps its figure as history.

Checked: Main/_Module holds 9 .xslt files and
python tools/check_external_xslt.py prints PASS: 18 stylesheet(s) clean.

#### docs(hooks): v2.0.28 - time the ModuleData gate's validator

`ae8038ec`

The comment in check-moduledata-validation.sh said the validator
takes about 21 s on the laptop since WP108. Its own 27-code command,
run 3 times warm after a warm-up on the laptop against the public
2.0.29.5 install on 2026-11-07, took 16.0, 13.9 and 13.5 s, all exit
0. The comment now gives that range and date. The 45 s inner bound
and the registered timeout are unchanged.

Not-tested: comment only; bash -n passes.

#### docs: v2.0.28 - recount stale validator and stylesheet figures

`4ef8e38d`

Figures measured on the laptop against the public 2.0.29.5 install
of TAOM, TAOM_Map and LOTRLOME_Armory, 2026-11-07.

check_external_xslt.py now prints "PASS: 18 stylesheet(s) clean":
9 in the repo (lord_template_rosters.xslt arrived in 82d4e25b3), 8
in the Armory, 1 in TAOM_Map. CLAUDE.md's three-module trap row and
the check_external_xslt.py docstring said 17 with 8 in the repo. The
row's other figures still hold: 1,160 XML (385 repo, 467 Armory, 308
TAOM_Map by find -name '*.xml'), 454 Armory and 44 TAOM_Map
ModuleData XML.

TAOM_Map's settlements.xml carries 1,002 Culture. refs (grep -o and
an ElementTree attribute walk agree); moduledata-validator-codes.md
still said 1,012.

The troll KNOWN_DEAD_MESH allowlist was retired in 2b14ffcfa, but
armoury-mesh-cleanup.md and validation-and-testing.md still read the
three warnings as live. They now record them as history:
validate_mesh_refs.py --game <install> with the KNOWN_DEAD_MESH,
STALE_DEAD_MESH_ALLOWLIST and MISSING_MESH codes prints 0 errors, 0
warnings, 3980/3980 unique visual meshes present, and
troops_mordor.xml still names the three items 18 times. The dated
2026-09-05 transcript and tables keep their numbers with a note.

tools/README.md said the prefab WARN band was "120,000 and up";
verdict() in check_prefab_budget.py warns only when the total is
above 120,000 and errors at or over the 131,072 cap. The README now
says that. The tool reads 130,439 today, unchanged.

moduledata-validation.md gave the Armory 3,727 items and 63
monsters with no recorded method. Counting <Item>, <CraftedItem>
and <Monster> opens that carry an id, comments stripped by
taom_schema._read_stripped, gives 3,511 items, 371 crafted items and
80 monsters, and still zero NPCCharacters, rosters, party templates,
cultures and body properties. The old figure stays, marked as dated.

Not-tested: docs only; lint_docs.py --summary shows 7 findings
before and after (claude_budget 1, adr_symbols 6), ai_dashes 0.

#### docs(plans): v2.0.28 - record the public-build wave and D34

`8555540f`

D34 records that the laptop now runs the public v2.0.29.5 build of
TAOM, TAOM_Map and LOTRLOME_Armory, so data counts measured on it are
item verdicts again. The validator reads 0 errors and 111 warnings.

Plan rows move for WP101, WP108 (now DONE, in-game checks owed),
WP133, WP134, WP141 and WP145. The integrator record and the overnight
report carry the run at 3819c9799: 13492 tests, 0 failed in Debug and
Release; hook suite 1390 passed; tool tests 4393 OK with no baseline
failures left. The report lists what this wave left open.

#### docs(machines): v2.0.28 - the laptop baseline counts swept XML files

`4c44d789`

The public-build laptop baseline said 513 ModuleData files were swept.
The validator sweeps XML only: 498 files, 454 in LOTRLOME_Armory and
44 in TAOM_Map. 513 counted every file type in both folders.

#### docs(weights): v2.0.28 - Lindon's 2.0 band comment says level 26 to 36

`abb37a65`

The comment over Lindon's 2.0 troop weights said level 21 to 36, but
the lowest troop in that band is the level 26 bowman. Only the comment
changes; the weights are untouched.

#### docs(machines): v2.0.28 - record the public-build laptop baseline

`81262569`

The laptop now runs the public v2.0.29.5 TAOM, TAOM_Map and
LOTRLOME_Armory on game v1.4.8, so the provisional v2.0.26 baseline is
replaced by a measured one: 0 validator errors and 111 warnings, with
BROKEN_ITEM_REF, BROKEN_ROSTER_REF, BROKEN_SETTLEMENT_REF and
LANDLESS_CULTURE all at 0, the ModuleData commit gate clean and the
polearm audit passing. The old 3768 and 3771 figures stay as a history
line.

#### docs: v2.0.28 - TAOM_Map refs are swept; recount validator scope

`ce9d9eb2`

6b20517f8 corrected CLAUDE.md: the validator has swept TAOM_Map's
ModuleData since #462. Three other docs still said its Culture refs
were never checked. settlements.md, the validate_moduledata.py row in
tools/README.md and the coverage matrix in moduledata-validation.md
now agree with the code (_EXTRA_REF_MODULES in validate_moduledata.py
lists LOTRLOME_Armory and TAOM_Map).

Counts measured on the public 2.0.29.5 install, 2026-11-07: TAOM 338
ModuleData XML of 385, Armory 454 of 467 with 8 XSLT, TAOM_Map 44 of
308 with 1 XSLT. That is 836 files swept per run, 1,160 XML in all and
17 stylesheets (check_external_xslt.py: 17 clean). TAOM_Map's
settlements.xml carries 1,002 Culture refs, 30 distinct, where the
docs said 1,012.

Record correction: the body of d2928f8c1 says four helpers were
copied from run_fords_autorun.py. There are five: powershell,
list_processes, _user32, game_dialogs and click_button. All five are
byte-identical to their source at ca1bc5f2e.

#### docs: v2.0.28 - recount stylesheets and the prefab budget on 2.0.29.5

`6b20517f`

With the public 2.0.29.5 build of TAOM, TAOM_Map and LOTRLOME_Armory
installed on the laptop, several doc figures were stale.

check_external_xslt.py now sees 17 stylesheets: 8 in the repo, 8 in
the Armory and 1 in TAOM_Map. CLAUDE.md said 16, and the xslt-check
skill said "one of the eight" external files and left
physics_materials.xslt off its list. CLAUDE.md also still said the
validator sweeps none of TAOM_Map's ModuleData; it sweeps both live
trees (454 Armory XML, 44 TAOM_Map) since #462. The three-module XML
total is now 1,160.

check_prefab_budget.py reads 130,439 of 131,072 (633 spare) across
seven modules on 2026-11-07; the Armory's Prefabs dropped from 48 to
19 entities. The figure is updated in startup-resources.md,
module-map.md, settlements.md and tools/README.md. Those docs
already said the tool sums every module, so only the numbers moved.

#### docs(plans): v2.0.28 - record review 3, D33 and the trunk merge

`26b56760`

D33 records the authority the maintainer gave to push and merge, and
the waiver of the in-game and Armory checks. It says plainly that those
checks did not pass; they did not run.

The overnight report gains the final stretch, the verification run
alone at 3d4241801, the gate gaps left open and the maintainer's list.
The six hook failures at edcfddc63 are recorded as read to machine
load: an inference, since that load was not reproduced on purpose.

#### docs: v2.0.28 - remove the last mentions of the deleted CI

`331d6f25`

The docs agent of review 3 listed these and left them, being outside
its assignment. The workflow was removed in ccef172ad, so a sentence
saying a gate "runs in CI" promises a check that no longer runs.

context-budget's scanner also loses its sequential-thinking row: the
server is in no .mcp.json on this line, and the row counted a tool
that never loads.

#### docs(plans): v2.0.28 - fix merge steps, MCP list and 1.5.x leftovers

`3d424180`

The trunk merge steps gave a settings path that does not resolve from
the main checkout, and did not say that setting core.hooksPath refuses
ModuleData commits on the laptop until the public Armory is installed.

The MCP summary and table left out the playwright plugin server, and
docs/INDEX.md still said 9 servers. tools/README.md said the
/knowledge-compile skill was removed, which is true only on 1.5.x. A
native_archive comment still spoke of sessions; no hook runs the tool
on this line.

#### docs(tools): v2.0.28 - rewrap the native_archive docstring

`43591267`

The previous edit left one line at about 150 columns.

#### docs(machines): v2.0.28 - say the laptop native variables are unset

`2194074d`

Both variables printed empty at User scope on Mike_Laptop and the
archive folder is absent, so plan 137 Maintainer steps 2 and 3 are
still owed. engine-bump also stops quoting 230, 108 and 47 rows, which
no longer match this line's snapshot.

#### docs(tools): v2.0.28 - remove qualifiers the plan 137 merge made stale

`8095f0d4`

The skill edits these waited on have landed, so the tools state each
step directly. adopt-rea names only the wiring this line has, and the
INDEX row carries the armory report qualifier its siblings already
have.

#### docs(claude): v2.0.28 - correct build flags, release example, MCP count

`3dc6033a`

The subagent build reminder lacked -p:ModuleId=, without which a build
still deploys. v2.0.29 is a number this line's /release forbids. The
user-scope MCP rows cited a file Claude Code does not read, so context7
is listed through its plugin and sequential-thinking goes. taom-src
detects the version, so the lookup row no longer names one.

#### docs(tools): v2.0.28 - correct catalogue rows and add owed map rows

`00b81cda`

assemble_faction_map no longer writes factions.json. native_archive
has no hook on this line, so its output comes from --detach runs, not
from sessions. Add the TpacTool note and the two feature-map rows plan
137 owed, closing that ledger entry. The TpacTool note is new text
because 776054c0c's paragraph cites a -TpacBin parameter absent here.
CLAUDE.md drops its stale map row count: no gate holds it.

#### docs(provenance): v2.0.28 - register ponytail and mattpocock/skills

`02657df0`

Port the register half of 3ead3765e so check_provenance has no misses.
Adapted: this line has /context-save, so upstream's since-removed
wording is dropped, and the ladder is named Reuse ladder as it is in
think-before-coding.md here. No test added: plan 138 keeps the tool
report-only.

#### docs: v2.0.28 - drop pointers to things this line does not have

`63d89d0e`

mcp-health hooks and the refactoring-specialist agent were deleted;
/codex-rescue and /armory-audit never existed here. The rescue line
now names /codex-verify and says it costs money. The armory-refs note
lists what the tool's docstring says it checks.

#### docs: v2.0.28 - stop crediting the removed CI with live gates

`bff345ea`

ccef172ad removed GitHub CI but these sites still named it. XSLT and
voice clips now name the local data gates. The XML cell says the
schema gate reaches engine-registered files only, 94 here. The doc
graph ratchet is stated as hand-run and ungated, because no hook, CI
or /verify step runs it; no gate is added.

#### docs(plans): v2.0.28 - give the trunk merge its maintainer order

`d725e684`

The fast-forward deletes the main checkout's tracked settings.local
file, and no document said so where the merge is planned. Add a short
block after the owed table, link it from PROGRESS, and stop the
overnight report claiming every later merge: it ends at b84c3011a and
has no WP142 row.

#### docs(hooks): v2.0.28 - tracked-files gate covers gitignored files only

`23e42f27`

Plan 143 E1 kept the gitignored half of check-claude-files-tracked.sh,
but harness-facts.md, hooks-catalog.md and the hook's degraded message
still said untracked files block a commit. A forgotten new skill file
does not: git add x && git commit runs the hook before the add.

#### docs(plans): v2.0.28 - record the follow-ups; rows 137, 139, 005 done

`edcfddc6`

The follow-ups merge carries the skill and doc hunks plans 137 and 139
held back, the credential sweep line that accepts plan 005 item A, and
the manual patch failure notice. Rows 137 and 139 read DONE at the
merge; plan 005 reads DONE through plan 137 Maintainer step 6.

The record quotes the test and build results printed at the merge and
lists what stays owed, including the issue for 3d81168cb, two
feature-map rows and the maintainer's in-game checks. The hook suite,
the audit and the full tool tests were not run at this merge.

#### docs(reviews): v2.0.28 - two dated records catch up with this branch

`a80885eb`

The adoption record's "once the plan 137 skill edits land" qualifier
and its "same qualifier" pointer are gone, since both skills now run
--oracle before a carry. The overnight report's known limit on manual
patch failures now notes that 3d81168cb closed it.

Not-tested: none (docs only)

#### docs(skills): v2.0.28 - /verify says API snapshot drift blocks

`60ca37fd`

The snapshot README says Step 2d fails on drift, but neither the step
nor the report block tied DRIFT to "Ready for commit". The step now
says DRIFT makes it NO, and that NOT RUN has not passed, the same
wording Data gates uses.

Not-tested: none (docs only)

#### docs(skills): v2.0.28 - /engine-bump tells the laptop to set its vars

`e1257e85`

"The laptop sets it" was not true: TAOM_NATIVE_ARCHIVE and
TAOM_GHIDRA_PROJECTS are both unset as user variables there. Run as
written, the archive step cannot create its E: folder and an update can
then overwrite the old pair. The step now says to set both first and
what the NOTE line means, as /native-crash-triage step 7 already does.

Not-tested: none (docs only)

#### docs(skills): v2.0.28 - research guide and agent match taom-src

`8b3b907f`

Plan 137 Step A1's two research guide sentences were skipped, so the
guide disagreed with the Codex guide and the agent manual: it now says
a short or fully qualified type name and a cache per game build. The
researcher agent's cache path gains the build stamp the tool writes.

Not-tested: none (docs only)

#### docs(plans): v2.0.28 - Sauron claim limited, co-op note made specific

`41bace8b`

The record limits the Sauron sentence in fb3e560e4 to weightRatio at
the default 6 and speedFactor of 0.4 or more, and the knockdown in-game
check now names him.

The possible co-op gap wording in the overnight report and the delta
note is replaced by what was read from source for BannerlordCoop, with
Bannerlord Together and a two-peer run marked unverified.

#### docs(tools): v2.0.28 - audit report, prefab budget and preload citation

`3e7ae83d`

The Armory audit report is generated and absent until the first run on
the public Armory; the feature map and feature doc now say so.

CLAUDE.md's prefab trap claimed check_prefab_budget.py counts only
TAOM_Map. The tool sums every module and --dir counts one folder.

The visual mesh citation in moduledata-validation.md moves to the
installed decompile, View.PreloadHelper.cs:200-217.

#### docs(mcp): v2.0.28 - guides match the servers .mcp.json now lists

`14ab5df1`

context-budget and freeze still described the git, filesystem and
github servers WP116 removed. The freeze text now says those are gone
and only Serena's editing tools stay denied. mcp-servers.md gains the
elevenlabs row it lacked. CLAUDE.md's server count is left alone.

#### docs(release): v2.0.28 - release note runs the pairing check itself

`4657a0c0`

Phase 5 copied the modules: line from Phase 8 step 3, which runs after
the Phase 6 commit and the Phase 7 tag. Phase 5 now runs
check_release_pairing.py against the folder that will be packaged. A
non-zero exit does not stop the note, since PIN_MISSING is expected
until a pin is recorded and Phase 8 still gates.

The comparison sits in Phase 8 step 5, the channel folder that ships.
Step 3 reads the live install, which on the desktop legitimately
differs from the pinned copies.

#### docs(tools): v2.0.28 - API snapshot headers say when to regenerate

`33467c55`

The snapshot README and both generated headers said to regenerate after
an engine bump. With /verify Step 2d failing on drift, the rule is any
patch, GameModel or engine change, so the two generator strings and the
two committed headers change together and the README points at the
step. -Check reproduces both files on a Debug TAOM.Tests build.

The second README hunk targeted a sentence this line never had, so it
becomes a clause on the Maintenance bullet for -Check.

Research: snapshot_api_surface.ps1 -Check rc=0 after the edit
Not-tested: none (generator strings and docs)
Ported-from: 69d2e8c0a27ac3b061df1549065b1e68460ab437

#### docs(native): v2.0.28 - native tool docs stop waiting on plan 137

`015cdefc`

The skill edits these pages were waiting for have landed: /research,
/investigate, /native-crash-triage, /new-creature-mount, /engine-bump
and the researcher agent now name the native tools. So the "once the
plan 137 skill edits land" qualifiers go, along with the ghidra page's
note that its "Used by" column was only intent.

Not-tested: none (docs)

#### docs(ai): v2.0.28 - adoption checklist sweeps vendored credentials

`7d1566ec`

Before porting from a vendored drop, the security pass now sweeps it for
inline package credentials, archives at any depth included, because a
plain grep cannot see inside a .tar.gz. This is the guard that accepts
plan 005 item A; the faction-map half of the plan's step had landed.
The plan's interim line never landed here, so the insertion point comes
from the context of 01b970185.

Not-tested: none (docs)
Ported-from: 01b9701852ae2387ae12397615f36175383a272e

#### docs(verify): v2.0.28 - /verify runs the API snapshot check

`031b0619`

Now that -Check exits 0 on the committed snapshot, /verify Step 2d runs
it, so a patch target or GameModel base that changes without a
regenerated snapshot shows as DRIFT instead of being missed by
engine_body_diff.py at the next engine bump. The report gains an
"API snapshot:" line.

Placed above Step 2e so the steps read in order; the plan's anchor
would have put it after 2e. The .ai/verification.md row already exists,
so that line of the plan is not run.

Research: -Check rc=0 on a Debug TAOM.Tests build of this branch
Not-tested: none (skill text)
Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3

#### docs(skills): v2.0.28 - engine bump runs shipped refs and body diff

`a92854ce`

Phase 1 item 5 runs check_engine_member_refs.ps1 over the shipped DLLs,
because a fresh compile hides a removed member a released TAOM.dll still
calls. Phase 3 gives engine_body_diff.py's real command line, inputs and
exit codes. Both tools landed earlier without this text.

Adapted from the plan: Phase 3 already explained why bodies are diffed
and said full-replacement overrides go first, so the new paragraph
carries only how to run the tool and points back at it, with no second
copy of the reasoning.

Not-tested: a real body diff (no older decompile on the laptop)
Ported-from: 4f9985852086d5498e0a1c0357f8371a0a521dcf
Ported-from: 4e408e0e07b93b72ab40bb8aa75bf7148bd7722a

#### docs(skills): v2.0.28 - native crash triage gains the hang procedure

`2fd5d9cc`

A hang (the game spins, nothing crashes) now has a procedure: confirm
the process is the game and not the Modding Kit, spot the spinning
thread, take a full dump, read the stack in WinDbgX, then read each
native frame as C (or name it with triage on the laptop). Triage
decompiles the site and reads the faulting registers, carries an RVA
from an archived build, and archives builds with native_archive.py;
/engine-bump archives, warms the decompiler and checks the map.

Adapted from the plan's script: the TOTAL line keeps its form without
the memory citation WP143 removed, and the player report sentence keeps
the git show form WP143 gave it.

Research: v1.4.8 ships bin\Win64_Shipping_Client\Watchdog\Watchdog.exe
Not-tested: the hang procedure end to end (no procdump or WinDbgX here)
Ported-from: 9c6d4962b9b72ff0401cebd2658b4bb5cd8e8f4d
Ported-from: d51e430287cf0d063bd07f006aea7c1cd1fc67a2
Ported-from: 719074072ab5777bd614cda70afc330f8346f1c1
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: fc5408508687c9f813123b6b1db006dd6ab0cee7
Ported-from: e1dffb13807448f4b8faf091e54f8c546b58ead6
Ported-from: 2838b25c2f6cd2677425212aca6cee5b8e2226fb

#### docs(skills): v2.0.28 - research and review read native code as C

`4addc52c`

/research, the researcher agent, /new-creature-mount, the agent manual
and the MCP lookup table now send a trail that ends at an [EngineMethod]
or in natively parsed data to native_decompile.py, and report the C with
its RVA rather than infer it from a method's name. /investigate's detour
also covers a freeze, since /native-crash-triage gains the hang section
in the next commit.

Taken through a path-limited merge of d51e43028, narrowed from the
plan's list to what this tree still lacks. The debugger and
error-detective hunks are not taken because WP143 deleted both agents
and moved the shared-dimension line into /investigate. The rest of
/investigate's hunk had landed there too, so its one remaining sentence
is a hand edit. deep-review is not touched because lens 2 already says
it.

Not-tested: none (docs)
Ported-from: d51e430287cf0d063bd07f006aea7c1cd1fc67a2

#### docs(skills): v2.0.28 - taom-src docs: short names, per-build cache

`98fc6631`

The taom-src tool transplant landed earlier; the skill text it owed did
not. /taom-src, /engine-bump, the Codex guide, the agent manual and the
MCP page now say a short type name resolves through the per-build
engine-types.tsv, that the cache folder carries the build stamp, that
the game's own modules search before any mod's copy of an engine
assembly, and that a type in a module bin decompiles against the game
bin. The research guide's two sentences stay as they are in this step.

Research: taom-src path DefaultPartyWageModel on installed v1.4.8
Not-tested: none (docs only)
Ported-from: 857734c64f3588959ce46e6b74ba734aa57de721
Ported-from: 97219836c5184fbe0dc5fb3549342b290ce904b9
Ported-from: f39b399b49e8d2249ca3933ece419c07823d5be4
Ported-from: fe20eddff233417935d6e0d6faa59fbd20298250

#### docs(plans): v2.0.28 - record WP143 and WP145 E to G, decision D32

`a433ba0f`

Row 143 reads DONE at c66667f10, row 145 PARTIAL at ff48da2d4, with
G5 and the context-budget scanner half of F1 remaining. The integrator
record lines name what landed and the deferrals, and say both
integrators returned before reading the slow gates; the orchestrator's
own run at ff48da2d4 closed them and is quoted there.

D32 records the maintainer's 2026-11-06 instruction: graphify is
dropped, WP141's D1 steps 3 and 4 and D2 are no longer owed, and the
follow-ups land on port148/followups. Push, tag and trunk merge stay
unauthorised.

#### docs(loc): v2.0.28 - translator guide follows the byte writer

`ba52f460`

The line-ending paragraph still cited Path.write_text at :159 and a
taom_sources list at :132-143. Both moved, and the claims moved with
them: write_text_file writes bytes, so a rebuild no longer doubles the
CR, and the enlistment file is on the rebuild's list now.

Research: tools/rebuild_translation_files.py, tools/_loc_sources.py
Not-tested: none (docs)

#### docs(ai): v2.0.28 - native lane routes the provenance register

`cee3e083`

Step F4 dropped the provenance check id because .ai/verification.md has
no row for it, and also dropped the two patterns the plan keeps. The
native-dependencies lane now lists the provenance register and the adopt
reports. The lane still has no checks.

Research: none, routing only
Not-tested: none (routing only)
Ported-from: b935b830aa8a4b3c7622b129d9622e334eaff769

#### docs: v2.0.28 - docs follow the late 1.5.x fixes, RCA and record

`6e73ad89`

The engine lifecycle doc now says the deployment auto-finish sits inside
the tick loop, re-checked on v1.4.8 (DeploymentMissionController :83-96,
:181-184). The career docs say the VM builds the icon from ability_sprite,
else ability_template_id, and that 18 cloned careers name their source's
icon. The banner, offspring and elephant docs drop what Parts B to G made
false; the elephant monster attributes were read on this laptop's copy.

The 1.5.x RCA comes across with a note that it is that line's record.
tools/README.md gains check_release_pair and the rebuild's --apply and
--module, and the village row gains its scene check.

Not taken: the three lesson entries are already in the lesson files, so
none is appended. tools/README.md has no make_new_factions_playable row
to remove, and assert_auto_ignore gets none because G5 did not land.
elephant.md's nameplate hunk conflicts, so ours stays.

Research: DeploymentMissionController on the installed v1.4.8
Not-tested: none (docs)
Ported-from: 1d5d0a5614306e475ea8ffd3a05503325cc6dba4
Ported-from: 35044f5832fa3e8c067a2a20f8fd91121d11df9c
Ported-from: 931d59084b4e5b571628d1baf32dd05680b5d0d4
Ported-from: 3bb89aff5922b27e988fc41d06558d306fbecbdc
Ported-from: 1499aaa697fb216c951b41a1b34ee19ba521a8f2
Ported-from: 0385cc0418719b2d000e19632c20e7b2a44b85da

#### docs(ai): v2.0.28 - route the data gates to their lanes

`673c0122`

The moduledata and assets-localization lanes now run the data-gates check
that .ai/verification.md defines.

Adapted: the 1.5.x commit also routes a provenance check and adds the
provenance register and adopt reports to the native-dependencies lane.
.ai/verification.md has no provenance row on this line, so that id, its
two patterns and its verification sentence stay out. The plan's subject
named provenance, so it changes too.

Research: none, routing only
Not-tested: none (routing only)
Ported-from: b935b830aa8a4b3c7622b129d9622e334eaff769

#### docs: v2.0.28 - link the prefab clone reference from the index

`4603e8d1`

gui-prefab-clones.md came in with the path rule trim, linked only from rules,
so the doc graph ratchet counted it as a new component (27 against a baseline
of 26). The docs index now names it.

Ported-from: 4fbfb7adc8797ae4a6180f2e135ca5849ec08b07

#### docs(harness): v2.0.28 - rule lines earlier work packages left

`e3275136`

Single lines WP106 to WP141 routed to this plan: the agent-slot and callback
trap, Harmony rule paths and the category count (118), the caller-actor rule,
the SkeletonBuffer guard lines in /native-crash-triage, the merge harness,
policy ids and prefab clone lines in /engine-bump, the tpac and Armory trap
rows, the /localize length report and seed lines, the loc-orphans check, the
Budget pointers in /deep-review, and the native clause in AGENTS.md. WP136's
leavings land too: the data gate and gate names in the rules, two review
lessons, DependenciesPairing and the shipped troop reseed in /release, first
at v2.4.0.

The .githooks code list needed nothing more: it equals the Claude gate's 25
codes. The check-deep-review section number went with the hook. ADR-003 names
the 3 fields two partial files still declare, a D31 edit. The ~4 s validator
comment now gives the pre-WP108 figure and the 21 s one.

Skipped by decision (plan Part G): the /armory-audit skill and its routing
row, because it would wrap one command, and the session-start ARMORY ART DRIFT
block and its contract row, because a cooked Armory would print UNCHECKED
every session. Owed: the graphify workflow wiring WP141 left.

Ported-from: f75288ebf5c6866b01e5c578aa232c123c9aa27b
Ported-from: bd1ac829249a8333d7198b7a433071a2c1cecc52
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
Ported-from: a27c6b33817f6375f793b37c3892f897450e4bfe
Ported-from: 685db7e46cfae268fe3ac765cab7ac6e1468cc84
Ported-from: 450332b74cf82403d78033463687dafec446fedf
Ported-from: df375d99e6312ad583fa48ba9a6482eb2c988b6c
Ported-from: 8a40271107ff75703db4ccfa5b3ed99963cd210f
Ported-from: 41aa2779bf2b9f6c715775b303fc8894f9538a91
Ported-from: 616cf12f16d2942e0235d793afc375509a8c204c
Ported-from: babcb8335e373b40285a90cdcbcec685de2850ac
Ported-from: f1675c0581a472fcc72fb3c8da7f1566912255fe
Ported-from: 8caac7da5df694fbdebc0ec4581eb4ffc0ef1cfc
Ported-from: d51e430287cf0d063bd07f006aea7c1cd1fc67a2
Ported-from: 099840f33a63eb4d3b1cc0d9b46c6e1a4faeaf7f
Ported-from: 3c5e57e7193bf21339e93f5aba1b2e0bceba00c6
Ported-from: 8b9f0a233043c66883bd0c2487eca3f98f8bcff8
Ported-from: ef6afeb6729b50756c2b7352603c9870a91e5fda

#### docs(reviews): v2.0.28 - lessons from the commit gate port

`291beeac`

Four lessons from the rules review join build-tooling-workflow.md: a git hook
that claims to bind every committer runs under the git an IDE ships, shlex
ends a word at any #, a commit gate reads the tree the commit records, and a
table that feeds a decision gets its rows read. The ratchet lesson stays out:
this line has no ratchet. Sources cite the commit, since the RCA is not here.

Ported-from: b6b54ce844ab925f830c87f41a61f2e472dbba60

#### docs(rules): v2.0.28 - the registry is the one GameModel catalogue

`6e5257ed`

Two tables listed every GameModel override, so a new model owed two rows and
the counts drifted apart. check_model_registry now reads only
gamemodel-registry.md, gamemodels.md points at it, and the registry states the
total the linter checks against the code. This also supersedes the 30abf550b
count regex, whose tuple is gone.

Ported-from: 35ff005dc5bb80870e49e46122b69ae57ac1a916

#### docs(harness): v2.0.28 - drop citations of machine-local memory files

`34c7e7c2`

A feedback card lives on one machine, so a citation of one reaches nobody
on another client. 49 lines lose theirs and keep the rule text: 9 in
.claude/rules, 15 in .claude/skills, 25 in docs/ai-includes. .claude/agents
and docs/reference/engine had none left. Check 11 of the doc linter gates
this from the next commit on.

Ported-from: 2129f753c0f8d33730d6303ce3c8dccd6c78423e

#### docs(harness): v2.0.28 - agent teams need no experimental flag

`4d01ddf0`

agent-teams.md describes the lead, teammates and SendMessage in a short
Terms section instead of the experimental teams feature; the settings
line goes with E3. The Budget section records the fan-out width limit, one
checker over per-file verifiers, and the 2026-09-05 incident. WP138 left
that section to this plan; it arrives here because the incident paragraph
needs it as its anchor.

Ported-from: b6012355be4f21100a890e62a7774cf7e82e3391
Ported-from: 4fbfb7adc8797ae4a6180f2e135ca5849ec08b07

#### docs(adrs): v2.0.28 - amend the ADRs, drop false samples and gates

`05f990bd`

The ADRs described things this line never had: IAdapterFactory, the four
provider interfaces, an architecture test file, coverage and CI gates.
Their samples and those claims leave with dated amendments. ADR-002 names
EntryPointLineCountGateTests and ADR-007 names ServiceSignatureGateTests
and ServiceLocatorGateTests, which this line has since WP136. ADR-003,
004 and 005 name BannedConstructsGateTests. ADR-008's checklist names real
folders and the non-deploying test line. ADR-009 takes the lens 9a keep-list.

Applied under D31 from the plan's patch. Left out: ADR-010's amendment,
because /lint-docs, /knowledge-compile and /doc-graph still exist here;
the lens 1 phrase of 250e1b321 landed in D1; ADR-011 and ADR-012 text.

Ported-from: 250e1b321e8a14452c256b2ed0802ceec0b060d4
Ported-from: 3ead3765eb9b357725da0ef127db6c4cbb35d1aa

#### docs: v2.0.28 - decisions move from private memory to the repo

`dfa30a74`

module-map.md documents the distance cache file layout, cited to v1.4.8
NavigationCache.Serialize; the desktop measurement note stays out. The
modding docs gain the Gondor armour prefixes, the child townsperson rule
and the two-skin race rule, each checked against this line's data and the
installed Armory. TRANSLATOR_GUIDE.md says English has no language file
and names --module on its run lines. agent-teams.md gains the workflow
script gotchas, with resume described as prefix-keyed. harmony fork
research is on record. The lifecycle doc, lord skills guide and tools
README pick up their corrections and rows for scripts that exist here.

Left out: ADR-012, upstream-review, adoption-history.md, the README kingdom
table, roadmap, feature-map folders, harmony-patch-registry.md, the
mcp-servers davinci row, release-process.md's channel text, provenance rows
and 16 tools/README rows for absent tools or with no anchor on this line.

Research: taom-src NavigationCache.Serialize (the generic NavigationCache of T)
Ported-from: 683fd1331f6b8bf5ab397984292b0f47b00a37cb
Ported-from: 3ead3765eb9b357725da0ef127db6c4cbb35d1aa

#### docs: v2.0.28 - delete guides with no reader, correct the rest

`db8e2a27`

Six generic guides go: tdd-enforcement, security, decompiled-code-analysis,
research-workflow, iterative-problem-solving and multi-approach-validation.
Links now point at .claude/rules/tests.md, testing-guide.md or
taleworlds-research-guide.md. patterns.md, testing-guide.md, architecture.md
and the research guide shrink to what a reader needs on this line, without
the client seam and with Composition kept (Main/Composition exists).
.ai/review-reference.md replaces its copied path rules with links and gains
three Intentional Patterns lines. README.md stops calling a Codex review
mandatory, tools/README.md stops claiming every script has --dry-run, and
TEMPLATE.md asks for the line that proves an in-game check.

Left out: code-quality.md's rewrite (no nullable ratchet here), "Desktop
only", "No port to 1.4.5", the TAOM.dll wording line, .ai/verification.md's
RefAsm text, TRANSLATOR_GUIDE.md's and armory-guide.md's wider rewrites
(1.5.x facts I could not check line by line), and module-dependencies.md
apart from the DependedModule paragraph.

Ported-from: 4fbfb7adc8797ae4a6180f2e135ca5849ec08b07

#### docs(skills): v2.0.28 - correct the skills, drop three unused agents

`6cdd8ced`

debugger, error-detective and refactoring-specialist go: the main
checkout's audit log holds 590 lines with 0 spawns of each, against 14 of
taleworlds-researcher. Their routing rows left CLAUDE.md in the entry doc
commit; error-detective's shared-dimension list moves into /investigate.

/freeze resolves a relative path against the worktree and allows memory
and scratchpad edits. /investigate reads every log line first and names
what the maintainer must play. /native-crash-triage gains the same
paragraph. /engine-bump says triage defaults to the shipping client DLL
and that -Check ignores the backlinks footer. /issue needs the maintainer's
word. feature-builder and implementer may use Monitor. researcher's
disallowedTools goes because its tools list has no writer. review-codex and
verify lose dead memory citations and the TODO scan.

Kept on purpose: lens 4's CHANGELOG check (D1), lens 5's XML parse smoke
test, the Serena lines (D3), deep-review's paid-dispatch trigger, and the
manual DLL copy advice (session-start.sh calls no native_archive.py). Left
out: graphify and Fords lines, "Desktop only" and "No port to 1.4.5", and
the release, armory-audit, upstream-review and review-outside-pr hunks.

Ported-from: 452796db4b02217624ab0075d5c93742b2820fa3
Ported-from: 250e1b321e8a14452c256b2ed0802ceec0b060d4

#### docs(rules): v2.0.28 - cut the path rules back toward their cap

`0b2d61cd`

csharp-architecture.md drops from 28,119 to about 9,200 bytes: the config
provider rule moves whole to the new config-providers.md, and the NaN,
lookup and test coverage sections shrink to their instructions. Lens 5
rule 4b and lens 2 item 3 hold the detail. The Outcome rows join the table
because IOutcomeLog and OutcomeCounter are on this line.

gui-ui.md's prefab clone detail and vanilla-data-comparison.md's later
sections go to the new gui-prefab-clones.md and to the docs that already
held them (xslt.md, armory-guide.md, dev-console.md, lord-spawn-guard.md).
Troop party template bindings move to docs/modding/party-templates.md.
troops.md loses its race table, which said Gundabad fields goblins and orcs
while troops_gundabad.xml carries 30 pale_uruk. Its per-tier, royal-last
and weapon-stat sections stay: 1.4.x tooling does not contradict them.

Small corrections: coverage percentages leave tests.md, adapters.md cites
v1.4.8 lines, provenance.md gains the Fab row, native-cpp-ports.md loses
dead memory citations. harmony-patches.md's thread table stays in place.

adapters.md and provenance.md grew a little: the plan names those
additions. Left out: the TAOM.dll wording line (this line's notice file
has no such sentence) and the two moduledata-validation rows, which WP138
already removed.

Ported-from: 625e8fc539d408e8fd8281af60f48b60a997768d
Ported-from: 4fbfb7adc8797ae4a6180f2e135ca5849ec08b07

#### docs(harness): v2.0.28 - correct false lines in skills and rules

`bb8b46e8`

feature-builder no longer claims a coverage percentage nobody measures.
/localize says to search issue bodies for owed translator runs. /ship
points at /deep-review's Step 2 for the tier and waves, and an issue is
opened on the maintainer's word. Lens 1 names ServiceLocatorGateTests.
xslt.md keeps one line on the identity template. CLAUDE.md records how to
test for CRLF under the Bash tool; the 1.5.x python3 bullet it extended
is not on this line, so it stands as its own Notes bullet.

Left out: /verify's suite-size line (this line's skill has no floor) and
the unrelated CLAUDE.md wrap change.

Ported-from: f4b036e95d911b487c36948fa96fddd4f66d5ac0

#### docs(rules): v2.0.28 - entry docs quote effects; issue and git rules

`789d52be`

AGENTS.md gains three short clauses: quote the line that produces an
effect, a thin entry point needs no unit test of its own, and give a
generator an explicit --out. CLAUDE.md lets the maintainer waive an
issue and makes git reads free. The always-on rules lose a description
field the harness never reads, and the evidence rule's cadence table is
one sentence.

The three agent routing rows and the post-compact and check-version-tagged
contract rows go early, in this commit, because the additions would pass
the 46,000 byte budget otherwise. Their files go in D1 and E1.

Left out: the AGENTS.md section drops, the risk tier wording, every
1.5.x path, and think-before-coding's extra sentence (the section already
says it).

Ported-from: e653257a829a1f36c92b32d6b49ea4670600a1e1

#### docs(rules): v2.0.28 - correct the rules that caused harm

`50985da7`

Fixed: the weapon guide's polearm registration (OneHandedPolearm first),
the lord skills guide's "safe default" for --all-cultures, the review
reference's XSLT copy (now a pointer to xslt.md), xslt.md's pointer to
check_external_xslt.py, the IAdapterFactory samples in gamemodels.md and
csharp-patterns.md, and the thread-local cleanup rule (a finalizer, never
a Postfix). The completion workflow, /deep-review, /localize, the culture
guide, /adopt-external and the skill-port checklist no longer order paid
Codex or translator runs: those run on the maintainer's word, and
/localize seeds by default. feature-builder reports IoC and SubModule
lines for the orchestrator to apply. /investigate and /new-feature use
non-deploying test commands. .editorconfig pins shell files and githooks
to LF.

Left out: the Serena mention removal (D3), #763 and #1056 (1.5.x issues),
/localize's 1.5.x Case D wording and test list, the rewritten command
lines in docs/modding and agent-teams.md (WP103 swept them), the 1.5.x
facts in kingdoms.md, load-order-and-dependencies.md and
lord-skills-authoring.md's inline skills line, rules-catalog.md's rewrite,
and the hang wording in /investigate (this line's triage skill has no hang
section). lens 7 keeps CLAUDE.md's Traps pointer: orientation.md is absent.

Research: installed v1.4.8 SandBox and SandBoxCore ModuleData listing; taom-src DefaultPartyMoraleModel
Ported-from: 2992763f6c6f777de1d62e4ca4f8a0b83f165aa5

#### docs(rules): v2.0.28 - settlement prefixes follow the live map census

`7d1dad6a`

The xml-data line said EN=Rohan and K=Easterlings; the census in
id-cheatsheet.md says otherwise. The line now lists the 21 census
prefixes, and the cheat sheet's gotcha about the disagreement goes.
The 1.5.x realms and their prefixes stay out.

Ported-from: c625ada0fa22cc08ce69156e2035a7a9e06a8b73

#### docs(skills): v2.0.28 - /issue searches open issues before filing

`39132aa1`

The step list now opens with a search for the same file, tool or
symptom, so a known problem gets a comment instead of a duplicate.

Ported-from: 175b149fd8abd0d52b792be1a5ec4e02503fa7d7

#### docs(gui): v2.0.28 - a hint's parent must lie inside the bar it sits on

`f56be13b`

gui-ui.md already had a Hover targets paragraph from the WP140 gate.
The containment rule joins it, so the rule stays one paragraph and keeps
the PrefabHintParentGateTests sentence. The hover lesson is appended to
localization-ui.md, re-cited to v1.4.8. The FillBarHorizontalWidget
lesson and the 1.5.x feature sentence are left out.

Research: taom-src EventManager.CollectEnableWidgetsAt, HintWidget
Ported-from: 267834a2e33ffec947c8b08fd14516f03bb3abe9
Ported-from: fcd1409074c6eb13709233e5161f6779de1ccd64

#### docs(rules): v2.0.28 - list every writer before gating on an engine flag

`1fc3c304`

A gate on an engine flag, stat or total lists every writer first and
cites the line that builds it. The examples are re-cited to v1.4.8
ItemRoster.TotalFood and SellItemsAction. The 1.5.x RCA and its feature
name stay out: neither exists on this line.

Research: taom-src ItemRoster (TotalFood), SellItemsAction
Ported-from: 7c6d4862800e7369d957662eb02d12b2485bcb3c
Ported-from: ed3bb65cc00ea92fd841733430dfa8be7244d13d

#### docs(rules): v2.0.28 - new patches need no ResetForUnload

`417fbf3b`

Nothing reloads TAOM inside one process, so a new patch class owes no
ResetForUnload method. The rule cites the v1.4.8 path: OnSubModuleUnloaded
runs only from FinalizeSubModulesBases, reached through FinalizeModule
and CoreManaged.Finalize at shutdown.

Research: taom-src Module.FinalizeSubModulesBases, Module.FinalizeModule, CoreManaged.Finalize
Ported-from: 636b50abfd80ea4b107b4a66f984d5ef8ab4932c

#### docs(harness): v2.0.28 - a source the maintainer leaves unnamed

`e13fbd74`

The provenance rule gains an exception: a source the maintainer
chooses not to name gets no row and no mention anywhere in the repo.
The register and the docs index now say every named third-party
source, so the two agree with the rule.

Ported-from: 1d88b86333617d5efb6b28f29f8ef4a060dd595f

#### docs(plans): v2.0.28 - record WP142 docs sync

`ebde1aaf`

Row 142 is DONE at merge 5199471c8. Integrator record: 13475 total,
13467 passed, 8 skipped, 0 failed; three review corrections made at
merge and the F2 ordering deviation noted.

#### docs(lessons): v2.0.28 - lessons the WP138-WP145 ports deferred

`1d61a57a`

13 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: f382ff6fdc6c11de2d51ceae6d68fdd91588cc82
Ported-from: 9a2a8dae9c8893626521134855b964c77fb6c2a2
Ported-from: 5fcf8390c63d6955255a291ed050095066d8a862
Ported-from: 7012783b35f0329c128e5bc1d3aaf2781a24ad79
Ported-from: 450332b74cf82403d78033463687dafec446fedf
Ported-from: 8d5cfc6231a8638610b6c1a4e2225cb98c92ca50
Ported-from: 5408487956f6f7bf41ca21bc6a4aeecafd5c4b18
Ported-from: abb718c2fc835297e2e5a6e9ab9973acaf291f79
Ported-from: 991f415f41340823f63464c9d8692e5c7079bfc6
Ported-from: 0daece880b1e2bf174c5878e165868d9d4826700
Ported-from: 931d59084b4e5b571628d1baf32dd05680b5d0d4

#### docs(lessons): v2.0.28 - lessons the WP135-WP138 ports deferred

`e3b8bede`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: 4ceae903bf1f52e1587506883a434439e8f85379
Ported-from: 9e2a39f4092dd32c7bd58e193e24ee210594f26f
Ported-from: 41aa2779bf2b9f6c715775b303fc8894f9538a91
Ported-from: 7abce5db992d52f9fd881f0214b01b52cf417740
Ported-from: 8a40271107ff75703db4ccfa5b3ed99963cd210f
Ported-from: bc5b5d71d6971bcbdd80f1df329a3bf77d416cb4
Ported-from: fe20eddff233417935d6e0d6faa59fbd20298250
Ported-from: d51e430287cf0d063bd07f006aea7c1cd1fc67a2
Ported-from: fc5408508687c9f813123b6b1db006dd6ab0cee7
Ported-from: f382ff6fdc6c11de2d51ceae6d68fdd91588cc82

#### docs(lessons): v2.0.28 - lessons the WP130-WP135 ports deferred

`ce6e6ab2`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: 3f75217dcde84c96974dffa0977f0c3403cec1da
Ported-from: 0103a8271
Ported-from: f54581d6c2b41221f57184e8a53091d4e1739259
Ported-from: 06a1a1b71b6a178e1762800077cf2f8381603f2d
Ported-from: 10363643999af79aaec0d9e49e0d5f0447167531
Ported-from: c2449a306c0f813fdebc641f9907b16a045a0b13
Ported-from: 97426dcd07987a680d7a0e19a55cff634cffc2f6
Ported-from: 365f2ecfef13d435689138992ec866dd18784de8
Ported-from: 399a7fe277a8874f20b2bb526caba9571818d4d4
Ported-from: 0e75ec50a34e33fd7f4a6b4c61a3cd15e1330c3d
Ported-from: 9e2a39f4092dd32c7bd58e193e24ee210594f26f
Ported-from: 4ceae903bf1f52e1587506883a434439e8f85379

#### docs(lessons): v2.0.28 - lessons the WP124-WP129 ports deferred

`f59babac`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: 70320bbe1b16a1b5dc5fa4f151f2a95fcc3403ad
Ported-from: 13f34c1cbc7c43f9d52f8c9df94af60e01065bc2
Ported-from: 30003668bd201bf7707f44f792689aab151ed45f
Ported-from: 9804f67b9deba24813da1f2ad7ef2d5f9ad6fa14
Ported-from: 437d591112a74aab10a2880a5f60010cefd95278
Ported-from: f7e9bd0d230c57922859fd84e5184c903da22113
Ported-from: 76f22e111a18673bc0d9a33be7090a787c1b4b98
Ported-from: b24e8549e88e8b0b465bc1615f4335855a3486d9
Ported-from: 3b848c4111db58b86be10f126b643839ed8cb2b6
Ported-from: a20b4d1540b782bde877b0333070b55aa38ea402
Ported-from: b02467744d0edbf63dc0774131c3d8f199014577

#### docs(lessons): v2.0.28 - lessons the WP123-WP124 ports deferred

`28089ead`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: 74e9172d42e7482453b6caa25112cab16f64242d
Ported-from: bb68eeca3aeee49ba95ea4956f2b3941820a3f93
Ported-from: 5d9d4cc974d21c1980a9f20a8a06a9a3de5b2afd
Ported-from: 13f34c1cbc7c43f9d52f8c9df94af60e01065bc2
Ported-from: 726975a529427d6e68f3240d3c2eb8b39d8b0e5e
Ported-from: 4520af802d9c31f44263c7a9ce4a263e611d0c5d
Ported-from: 70320bbe1b16a1b5dc5fa4f151f2a95fcc3403ad

#### docs(lessons): v2.0.28 - lessons the WP117-WP123 ports deferred

`72b9c94b`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: 67eff55cf9cace45727b2324a79555d05ec927b2
Ported-from: 8ea3150522e642ca39ece0b8c4c7a1ba273a2c5e
Ported-from: b41de91e8e9b71b1c2003d367f7217b55713f8e2
Ported-from: 549afffde2646f3fce7a142e1ae4d4d3d7bd17ea
Ported-from: aa59f68ed63b7da7cc3006d1f93170f5ca1c99b1
Ported-from: 6ddcc8f9b3d097cfe8d31d203015c163a9346093
Ported-from: e7cdb2e96bae643e809c97df4edc9ce897da5a68
Ported-from: ee19f105269304b1fbdca1ca2cef088192f6a012
Ported-from: bb68eeca3aeee49ba95ea4956f2b3941820a3f93
Ported-from: 13fb30f9c9579c3f8cb3ecaf87a0a38f63aa34fc

#### docs(lessons): v2.0.28 - lessons the WP115-WP117 ports deferred

`bf93d8d0`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: 709649c3f920dfe86d8cfc1ee0d09a835f2c7eec
Ported-from: 9c310bc238fa1441f4a1344c5cc3fe5dccadb6a2
Ported-from: e4c2c159a93fd8dd5680638e0c4da664cbf33f9e
Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005
Ported-from: 787fd3663d5238c45c3e4bf8b08c98d9229e953b
Ported-from: 6514fdfe77d85b9c49e9dfb59bb81b21e4cfb4a4
Ported-from: c80c4108c88e9d5e28ec047458542235639f53c7
Ported-from: 60d85ed7d5a98d0a298c492866f26d72cadd1db5
Ported-from: 5dfa8f4cb0a9e86118ed41b0c6bdd3d9a977990b
Ported-from: 3dac7ab5b03944b0fdfd44b15043941b382d70ec
Ported-from: c9138027466ee2bcdcdcf8a84117abca8a3968ab

#### docs(lessons): v2.0.28 - lessons the WP114-WP115 ports deferred

`a5b9a526`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: 925db38d14a55e8851b7aeabd7e1a3627fe9dc75
Ported-from: 1325c36ea8d14bcba9dc9d0a3f6f5087c63833a9
Ported-from: 83bdad8537b9760deb6cd849c7ced8943a5f5a37
Ported-from: 709649c3f920dfe86d8cfc1ee0d09a835f2c7eec
Ported-from: 9c310bc238fa1441f4a1344c5cc3fe5dccadb6a2

#### docs(lessons): v2.0.28 - lessons the WP110-WP113 ports deferred

`5d29225e`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: 5e43518df7980ebaaec4d22a7370d2df726dd839
Ported-from: d1b9fd91ac32dd2dce5facc0567c577772f4acc6
Ported-from: 3fe86e22b
Ported-from: a913629cdb33b2b30e6aaf875146447abe1597bb
Ported-from: 1888b6f2804a2e80337e20b8d906cbc043e91433
Ported-from: 1ef7d1af6c4f43d343eebc4a2f159c209edc44d8
Ported-from: eed06b9c70bc562a4a77d0b71ed08260940d48f2
Ported-from: cd06da904d35f9160c32ff33030814a325344ab6
Ported-from: ab78564e064e173bef9f8d23d22d46bf86e3b3aa
Ported-from: b9eabd71250e4861fe3921fa1dfea5973e7c2c36

#### docs(lessons): v2.0.28 - lessons the WP107-WP109 ports deferred

`6db0b868`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: d469914abaa48cc32907256aa911597ad4243101
Ported-from: db1a7166b974b4d6b927fadfb0fd077278e914e9
Ported-from: 943ab74c03edfcc6cc124ef14a1b75db7ef038f4
Ported-from: 5ee49d05f1d24e060ecf597cdd75724f1d545618
Ported-from: f6bf0e58880c077f4859d2d1b0dc0b24e168fc2f
Ported-from: 65b691b6a745d622afa66eca8e39a3751219a047
Ported-from: 719074072ab5777bd614cda70afc330f8346f1c1
Ported-from: fa8c06f82fbc5ddd4f23ebe78157f5f2144439d2
Ported-from: a9f10688fbbb2e540b36c5fb593dda71811a881a

#### docs(lessons): v2.0.28 - lessons the WP106-WP107 ports deferred

`e1035e79`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

Ported-from: fe8f30c7abfca4ba1cfb590df8013b82757709e4
Ported-from: efe326242585a920e33238b3da20e88ee50f0a32
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 0700ca901
Ported-from: bd1ac829249a8333d7198b7a433071a2c1cecc52
Ported-from: af990ca769da52f408b8e804d127c446b515a379
Ported-from: d469914abaa48cc32907256aa911597ad4243101
Ported-from: 4e408e0e07b93b72ab40bb8aa75bf7148bd7722a

#### docs(lessons): v2.0.28 - lessons the WP103-WP106 ports deferred

`a854d7a2`

15 entries the first Part E pass skipped on a token match, re-judged
by Step E3's test: the rule holds on this tree and the entry is not
built on a 1.5.x-only feature. Engine cites were re-read on v1.4.8
with taom-src, or the v1.4.8 dump where the entry says so. An entry
that keeps a 1.5.x fact or name says so in its On this tree bullet.

This commit also corrects the skip record of 9607a4ac8 through
43aa48930. Those lists gave 232 skips; their reasons were a string
match. After the re-judge, 178 are ported, 3 had already landed in
0673e6013 and 51 stay skipped, for these reasons:

- Mark TAOM's own blows; never infer them from a field the eng:
  mechanism `IsRegisteringSyntheticBlow` (#643) is 1.5.x-only
- Fitting a foreign rig onto an engine skeleton: measure the j: built on
  the Animalia rig fit (`animalia_to_horse_map.json`), 1.5.x-only
- A mesh bent onto the target's rest needs its clips carried t: built on
  the Animalia reskin retarget, 1.5.x-only
- A Fab pack's first skeletal mesh on a skeleton may be a fur : built on
  `ue_export_cave_troll.py` and the Animalia packs, 1.5.x-only
- A CI step covers only the branches its workflow's `on:` bloc: this
  line has no CI (`ccef172ad`, D9)
- A release gate for a new artifact follows the shipped manife: built on
  TAOM.Client (ADR-013), not on this line
- Bannerlord.BuildResources cleans the whole module bin, so a : built on
  TAOM.Client as a second project, not on this line
- A new assembly needs every root scan and every typeof: built on
  TAOM.Client as a new assembly, not on this line
- A formation's unit width reaches its slots only through thre: built on
  Patch92 and `TrollFormationSpacingTracker`, 1.5.x-only
- A race's hand mesh needs the 26 hand-pose channels, and a re: built on
  `transfer_hand_morphs.py`, 1.5.x-only
- Take hand channels from a hand mesh, and pin and gate the wr: built on
  `transfer_hand_morphs.py` and its seam gate, 1.5.x-only
- Blender 5.2's `shape_key_add` makes a key at weight 1.0, and: a
  Blender fact whose tool, `transfer_hand_morphs.py`, is 1.5.x-only
- The melee attack table key is the Kit's "Blends with animati: built on
  the hill troll binder and `set_clip_balance_name.py`, 1.5.x-only
- Blender Surface Deform with a shape-keyed target moves half : a
  Blender fact whose tool, `transfer_hand_morphs.py`, is 1.5.x-only
- A cloned crafting piece does not keep the damage: the engine: hammer
  item, `analyze_melee_ladder.py` and `melee_catalogue` are 1.5.x-only
- An agent that stands in for a troop must reach every IsHuman: built on
  creature bandits, where a riderless creature is the troop, 1.5.x-only
- The region-owner check is a gate now, because the manual ste: plan 110
  verdict (4): the region-owner gate has no 1.4.x owner
- An engine rewrite can add unguarded reads to a method TAOM n: 1.5.x
  engine behaviour: v1.4.8 `HeroHelper.cs:20` reads only
  `LastKnownClosestSettlement`; Patch123 is absent
- A race head with no face morph channels crashes the first ag: built on
  `add_face_morph_channels.py` and a crash site in the 1.5.x binary
- A melee release bound to a custom clip crashes the first swi: built on
  `bind_hill_troll_action_set.py` and a crash site in the 1.5.x binary
- Rebuild a feature's owned effects from the current state on : built on
  the War Chronicle rally, 1.5.x-only
- Every animation the Modding Kit imports may name no skeleton: built on
  `wire_anim_master_skeletons.ps1`, 1.5.x-only
- A new live-data writer copies the newest sibling's guards an: built on
  the Animalia writers and `_gamedir.game_or_kit_running`, 1.5.x-only
- A mesh-editing tool's guard lists what it may edit; a run th: built on
  `strip_upper_mesh_channels.py`, 1.5.x-only
- Read the per-submodule hook order before moving a mission ad: built on
  the TAOM.Client submodule hook, not on this line
- A seam that lacks the actor has more than one caller: list t: 1.5.x
  engine seam: the re-seam onto
  `GetBloodFeudStartRelationPenaltyToOtherClan` was the 1.5.x port
- An engine event that grows a phase index needs one guard tes: 1.5.x
  engine behaviour: v1.4.8 `OnCharacterCreationIsOverEvent` is a plain
  `IMbEvent` (`CampaignEvents.cs:601`)
- Expose the domain's verdict to every consumer; never re-deri: all
  three cases and the fix are Fords of War, 1.5.x-only
- A new kingdom's generators drift from their output, and the : built on
  Arthedain and `add_map_fortifications.py`, 1.5.x-only
- A gate that flips an item's flag hides it from every reader : built on
  the armour gate (`ApplyGating`), 1.5.x-only
- Give a culture whose gear carries another culture's tag mark: built on
  market `<Stock>` rows and `armour_from`, 1.5.x-only
- An upper mesh with no skin weights ships silently; dump the : built on
  `transfer_upper_mesh_weights.py`, 1.5.x-only
- Measure texel density before choosing a UV method: built on the Carn
  Dum UV probe (`texel_p95_p5`), 1.5.x-only
- Painter refuses every scripted color in a Legacy project: built on
  `tools/painter`, 1.5.x-only
- A renderer that draws back faces hides a mesh wound inside o: built on
  `unwrap_texture_sets.py`, 1.5.x-only
- False-colour a layer to see what it covers: built on the Painter
  recipe, 1.5.x-only
- Write a detached script's completion file from one shared wr: built on
  `tools/blender/blender_job.py`, 1.5.x-only
- Seat each builder face on what hides it: built on
  `unwrap_texture_sets.py`, 1.5.x-only
- When a mission leaves out vanilla's ending, write how its fi: no
  hand-off in a merged plan names it, so it is outside E3
- A mode that copies a sibling's views must copy what they ref: no
  hand-off in a merged plan names it, so it is outside E3
- Zero-filled face channels stop the crash and leave the eyes : no
  hand-off in a merged plan names it, so it is outside E3
- A structural difference from a working asset is a lead, not : no
  hand-off in a merged plan names it, so it is outside E3
- A git hook that claims to bind every committer runs under th: no
  hand-off in a merged plan names it, so it is outside E3
- shlex ends a word at any `#`, so drop comments yourself: no hand-off
  in a merged plan names it, so it is outside E3
- A commit gate reads the tree the commit records, in every ch: no
  hand-off in a merged plan names it, so it is outside E3
- A ratchet's base at a merge is trunk, so simulate the merge : no
  hand-off in a merged plan names it, so it is outside E3
- A table that feeds a decision gets its rows read, not only i: no
  hand-off in a merged plan names it, so it is outside E3
- Check the first import before reading clothing as a sex sign: no
  hand-off in a merged plan names it, so it is outside E3
- A presence-based stand-aside breaks when the other mod stand: no
  hand-off in a merged plan names it, so it is outside E3
- A self-check fails on a swallowed exception, and its ladder : no
  hand-off in a merged plan names it, so it is outside E3
- A singleton mapped to a second interface is pinned with AreS: no
  hand-off in a merged plan names it, so it is outside E3

Ported-from: c79a585218ad22841c8db1f273e013385512b332
Ported-from: 35007dbb8d0324efd71a49ce356636262225d9ab
Ported-from: 27fd23bbb7c4ad8b79947d3621f3b6989f778c71
Ported-from: e0ad0db765d3f6f47cbce580eb8eb5662377d120
Ported-from: d96f349f1124ab474eb9b299b7c1389f6d9f8365
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: fe8f30c7abfca4ba1cfb590df8013b82757709e4

#### docs(modding): v2.0.28 - kingdoms Registration row cites line 144

`0bebc9a3`

Step C1 corrected this XmlName's line at kingdoms.md:18. The Registration
row of the same doc still said line 130. `Main/_Module/SubModule.xml:144`
is where `taom_spkingdoms` sits.

#### docs(lessons): v2.0.28 - restore four lesson bodies the port lost

`e108ebcf`

The first Part E harvest read added diff lines, and a commit that
rewrote an entry left only its heading to read. Four entries landed as
a heading and a Source: "When a decision reverses an invariant", "On
1.4.8 skill_template...", "A hand-built tpac package..." and "A rename
must evict the translation cache". Each now holds its text from the
1.5.x file at the commit named in its Source.

skill_template keeps the 1.4.8 gate, `SKILL_TEMPLATE_SHADOWS_SKILLS`,
because `SKILL_TEMPLATE_MISMATCH` is 1.5.x-only. The hand-built tpac
entry leaves out the clone tool, as plan 141 asks. The rename entry
drops `reset_stale_village_loc_rows.py`, which is not on this tree.

"The two hash fields in a tpac..." is removed. 5ccde2771 rewrote it on
1.5.x into the hand-built tpac lesson once the hash proved irrelevant
to the symptom, so keeping it would teach the refuted reading.

Research: taom-src BasicCharacterObject :337, :357
Ported-from: f6bf0e58880c077f4859d2d1b0dc0b24e168fc2f
Ported-from: 83bdad8537b9760deb6cd849c7ced8943a5f5a37
Ported-from: 5ccde2771850698ccb9be912a4b341e073630f6c
Ported-from: af2b198e45432233e696ce17c6898fe73101c9b1

#### docs(clans): v2.0.28 - correct renown bands; union-merge lessons

`8c3801d4`

The clan guide said 40 percent; the engine rolls up to 60 percent of the
band (v1.4.8 DefaultClanTierModel :43-49). The lessons files now merge
with the union driver, so parallel appends never drop an entry.
CHANGELOG stays a normal merge. Reread an entry after a merge that
touched the same line on both sides.

Research: taom-src DefaultClanTierModel :11, :43-49; git 2.54 gitattributes (merge applies to cherry-pick)
Ported-from: 4b7e36250b969c05abea32876795b4b4c3178c80

#### docs(lessons): v2.0.28 - the lessons index keeps no counts

`a1bdf086`

Nothing computed the per-category counts, so every one had drifted, and
with union merges a count line would be the one place two branches still
collide. The index now says how to count them instead.

Ported-from: 43db0b47a73acc19f522e6d11ab31a8a0b6daced

#### docs(lessons): v2.0.28 - the named lesson hand-offs, re-cited by hand

`0673e601`

The lessons the plans named and asked to be re-cited, not skipped: the
#764 constructor-reset lesson against v1.4.8 Campaign.cs :1391, :1429
and :1585, WP110's negative-relationship verdict against Kingdom.cs
:780-795, and WP114's MainParty lesson against PartyBase.cs:208-218,
keeping only the messenger example. The testing-qa recurrence line, the
c96a02a4b correction that the orphaned static handler is not
GC-eligible, and the chunker wording on the 32,763 lesson (keeping that
number) go in as edits. WP114's "last lesson of this file" pointers
exist nowhere in this tree, so that rewording has nothing to do.

Ported-from: 2719e59e8c81648a0b5cc15364a8548f755c00ce
Ported-from: 3fe86e22b27e00c0e239ec5554699b4b67c8b902
Ported-from: e8504a62b63be99a3843670b546e4f1faafd7954
Ported-from: c96a02a4bfea3960760ebbde49567958b43989ee
Ported-from: 16a34027dcd60e51baa9bacb47342eb582aa844f

#### docs(lessons): v2.0.28 - lessons the WP141-WP145 ports deferred

`43aa4893`

10 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- Trace the load path before calling an asset shipped: the c: names rgl_log_73032 (absent here)
- Measure a clip only over the frames it plays, and decode w: names docs/anim-findings.md (absent here)
- Write a detached script's completion file from one shared: names tools/blender/blender_job.py (absent here)
- Label an estimate as an estimate, with its inputs (Carn Dû: names MapCameraView (absent here)
- Seat each builder face on what hides it (Carn Dûm map icon: names hidden_kept_m2 (absent here)
- Run the whole tools suite at each work item, and prove a g: names test_live_map_writers_main (absent here)
- When a mission leaves out vanilla's ending, write how its: 1.5.x-only: Long Defeat
- A mode that copies a sibling's views must copy what they r: 1.5.x-only: Long Defeat
- Zero-filled face channels stop the crash and leave the eye: names add_face_morph_channels.py (absent here)
- A structural difference from a working asset is a lead, no: no merged WP names it
- A git hook that claims to bind every committer runs under: names BundledGit (absent here)
- shlex ends a word at any `#`, so drop comments yourself (r: no merged WP names it
- A commit gate reads the tree the commit records, in every: names CommitView (absent here)
- A ratchet's base at a merge is trunk, so simulate the merg: no merged WP names it
- A table that feeds a decision gets its rows read, not only: no merged WP names it
- Check the first import before reading clothing as a sex si: names sk_dwarf_dress_normal_ (absent here)
- A presence-based stand-aside breaks when the other mod sta: names HoN/VanillaTuning/Core/TaomOverlap.cs@784fef93 (absent
- A self-check fails on a swallowed exception, and its ladde: 1.5.x-only: Fords
- A singleton mapped to a second interface is pinned with Ar: 1.5.x-only: Fire and Tribute

Ported-from: 426f0dc88f33fd0e0778c455f67d7c81d3b74324
Ported-from: 5ccde2771850698ccb9be912a4b341e073630f6c
Ported-from: 87ffea9b2fbc0d96f2e43b9173d2f8d8a22ab28b
Ported-from: 0daece880b1e2bf174c5878e165868d9d4826700
Ported-from: af2b198e45432233e696ce17c6898fe73101c9b1
Ported-from: 931d59084b4e5b571628d1baf32dd05680b5d0d4

#### docs(lessons): v2.0.28 - lessons the WP138-WP140 ports deferred

`ac82627e`

12 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- Read the body of an engine action before a plan relies on: 1.5.x-only: v1.5
- The player's relation with a clan member lives on his clan: 1.5.x-only: v1.5
- "Read the rules" sends an agent to a rule its `paths:` tri: names .cs (absent here)
- A gate widened to new shapes that is green at once has no: names ServerLoadFixtureTests (absent here)
- A guard needs a test that fails when the guard is removed,: names StaticConstructor_UiHelperCall_IsGuardedByTheServerChe
- A fix that refutes a claim sweeps the claim's words, not a: 1.5.x-only: `Client/
- A release gate runs on more than one folder shape: test ea: names AssetPackages/pack0.tpac (absent here)
- A prefab clone inherits nothing: pin it to the vanilla fil: 1.5.x-only: v1.5
- Pin a GUI prefab copy to the vanilla file it replaces (#10: 1.5.x-only: v1.5
- A prefab element is live only if its consumer is: trace wh: names LeftPanel (absent here)

Ported-from: f382ff6fdc6c11de2d51ceae6d68fdd91588cc82
Ported-from: c8a3ac9d97ec33acfac996d1478ccf8404aa4317
Ported-from: 549bf390246afd94a364a6fab082445b2da6d751
Ported-from: 2fb9d914abd7c93e3e11c7553a8d34de687e1382
Ported-from: 8b108312744c3daabc5fab6383dbea67db0d96dd
Ported-from: 4fbfb7adc8797ae4a6180f2e135ca5849ec08b07
Ported-from: 5fcf8390c63d6955255a291ed050095066d8a862
Ported-from: 53a4e4e52a2a67a1098edfb588ab7f5a71900056
Ported-from: 42148af4cd851687f55b5bf94195cb57287c5b35
Ported-from: 450332b74cf82403d78033463687dafec446fedf

#### docs(lessons): v2.0.28 - lessons the WP136-WP137 ports deferred

`040ec67c`

12 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- A full-replacement GameModel override is a frozen copy of: 1.5.x-only: v1.5
- A module's stylesheet runs over the EARLIER modules' merge: 1.5.x-only: v1.5
- A cache keyed on the version string serves old sources aft: 1.5.x-only: v1.5
- A decompiler's auto-analysis misses functions reached only: 1.5.x-only: v1.5
- An oracle over two near-identical inputs proves little; ad: 1.5.x-only: v1.5

Ported-from: 2225939cd
Ported-from: c1ce782072e2da7de8c8b6f0e8231b1ff7c3f305
Ported-from: bc30b76dac099ad4433f3e73d5106c26179a930b
Ported-from: b409e6505
Ported-from: babcb8335e373b40285a90cdcbcec685de2850ac
Ported-from: a0fa3cff234b9cbe411e9825b5028844bb3c237e

#### docs(lessons): v2.0.28 - lessons the WP132-WP135 ports deferred

`3809b734`

4 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: 4ceae903bf1f52e1587506883a434439e8f85379

#### docs(lessons): v2.0.28 - lessons the WP132-WP135 ports deferred

`d66bdb6f`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: 41aa2779bf2b9f6c715775b303fc8894f9538a91
Ported-from: 4ceae903bf1f52e1587506883a434439e8f85379
Ported-from: c2c8afe420257616acf61991ce3fe2f1e0c58b1c
Ported-from: 8a40271107ff75703db4ccfa5b3ed99963cd210f
Ported-from: 9e2a39f4092dd32c7bd58e193e24ee210594f26f

#### docs(lessons): v2.0.28 - lessons the WP132-WP135 ports deferred

`dddaf446`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- A gate that flips an item's flag hides it from every reade: names ApplyGating (absent here)
- Patch the method that writes the value, not one of its cal: 1.5.x-only: v1.5
- Follow a UI value to what renders it before you write it (: 1.5.x-only: v1.5
- A fake that returns a state the adapter can never produce: 1.5.x-only: v1.5
- Give a culture whose gear carries another culture's tag ma: names armour_from (absent here)
- A culture promotion drops every table keyed on the host cu: names EveryLindonTroop_WeighsTheSameAsItsRivendellTwin (abse
- A clip reused on an action vanilla loops must loop too, or: names troop_stand_ (absent here)
- An upper mesh with no skin weights ships silently; dump th: names SK_Dwarf_Beard_A_12 (absent here)
- Before calling an action set's inherited clips "rarely pla: names act_cheer_ (absent here)
- A clip reused for another code must carry the flags that c: names ContinueWithAction (absent here)
- Measure texel density before choosing a UV method (Carn Dû: names texel_p95_p5 (absent here)
- Painter refuses every scripted color in a Legacy project (: names GenericColorSpace (absent here)
- A renderer that draws back faces hides a mesh wound inside: names tools/blender/unwrap_texture_sets.py (absent here)
- False-colour a layer to see what it covers (Carn Dûm map i: names bricks_01 (absent here)
- A binder takes clip availability from the files on disk, n: names bind_hill_troll_action_set.py (absent here)
- A live-install fix ships with a check mode, not only a rea: names wire_hill_troll_race.py (absent here)
- The safe invocation is the default invocation (2026-09-25): names game_or_kit_running (absent here)
- A verify mode that fails on the correct state is a dead ga: names gen_troll_anim_clips (absent here)
- A live A/B test edit left applied becomes the shipped data: names brow_swap.py (absent here)
- A language_data.xml with a name and no text_processor rese: names LiveTaomMapAndArmoryLanguageData_InstalledNative_Carry
- A line a hostile NPC says to the player must not name the: names BanditVoicesDataTests (absent here)
- Before deleting a translation row as redundant, render the: 1.5.x-only: v1.5
- A tool test must run under CI's runner, not only the autho: names tools/tests/test_ci_runner_compat.py (absent here)

Ported-from: 5d70714c48c93ed7d93315536d433d9e9f433581
Ported-from: 60dbf6429
Ported-from: bc39f6e4d0f55a70d4fbdc6b515b5439145ced21
Ported-from: 736fa2fef65a96812648df924efca0b5153a84d0
Ported-from: 9dd2f594fd887f792a5cd336781b6832ff6ff753
Ported-from: 0e75ec50a34e33fd7f4a6b4c61a3cd15e1330c3d
Ported-from: 71fea53c83a8db1a9ce33a3ac20ff5be6e06ac42

#### docs(lessons): v2.0.28 - lessons the WP130-WP131 ports deferred

`6b812140`

12 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- A battle mount has no Origin and no Character: route every: 1.5.x-only: v1.5
- Before stating an engine call has no limit or fallback, re: 1.5.x-only: v1.5
- A mount item's speed has consumers outside the item: the c: names VelocityThreshold (absent here)
- A model slot registered from two starters gets every new r: names TaomCustomBattleAgentStatCalculateModel (absent here)
- An engine field you write must have a reader (2026-10-20): 1.5.x-only: v1.5
- A plan proved against one trunk is re-checked when the bas: 1.5.x-only: bannerlord-1.5
- Deleting a GUI or data file from the repo does not delete: names EventXml.xml (absent here)
- A penalty on a shared engine limit reaches every party kin: 1.5.x-only: v1.5

Ported-from: f4f12a737c7e020be154ba857b42bcb871e65d96
Ported-from: 0103a8271
Ported-from: 1641fb98542bdd3ec7ad6fcbf4d33c1f96a30b7b
Ported-from: b2e387db7e4e2ed074fceb1edaa33427af29ab9f
Ported-from: 1dc0a8d7c552cf56341cdaedface37da90ded9c1
Ported-from: 0d3f2bc88e664e10d62589b114757eed2609f117
Ported-from: 93684c32cac592270911513816b84a81a51602fd
Ported-from: 2c7f7fae934b015857e8a3be6f30bde8ba122962

#### docs(lessons): v2.0.28 - lessons the WP128-WP129 ports deferred

`ac284b6f`

1 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: 86ef5c7af1137910c4f198d44716476890772ef3

#### docs(lessons): v2.0.28 - lessons the WP128-WP129 ports deferred

`9ed8b184`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- A menu consequence that can fail part way must know what m: 1.5.x-only: v1.5
- A guard justified by engine ordering cites the decompiled: 1.5.x-only: v1.5
- A registered key does not reach English: the inline text a: 1.5.x-only: v1.5
- A new kingdom's generators drift from their output, and th: names add_map_fortifications (absent here)
- Hold a new culture's coverage with tests that read the dat: names race_abilities.json (absent here)
- Append new settlement rows at the end of the live settleme: 1.5.x-only: v1.5

Ported-from: 2d1e5f3385f871c5a829611641230266b87fb763
Ported-from: 7a912dcd544c1c3560b52de2df571f406131a782
Ported-from: 1559b3261c2087149f7811bebeca8d7d7c7fc309
Ported-from: 1d9cc42f1bf13a49a9c340182cc77dc9ec44817c
Ported-from: 069962aa43773a2201f8ec358f4c0c9b40f046e9
Ported-from: 9304a4cb941d577cd4b2d5810af13529e313ebf6
Ported-from: 218fa405376cb3072cc82c62667214165f41295c
Ported-from: 4666a254a1a0f4c9020110e4c6ff3a3216628ef4
Ported-from: 90035a799207619c134c1fe84d495fd8cde6d50b
Ported-from: c04ba3fe58a39c2538dbb5a0708393e63d21b1e9
Ported-from: 75e190abe685cba1b403af8847cb4da9d0b944c0
Ported-from: 0761c01b75d58d46eb6f97e06d94db568ac33d53
Ported-from: bce566059fa0d9b0caeee51d18e1723cef921af0
Ported-from: 86ef5c7af1137910c4f198d44716476890772ef3

#### docs(lessons): v2.0.28 - lessons the WP125-WP127 ports deferred

`4e9ed6e4`

2 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: 12b1c2003fbf2539f9df62246f70e655c20bfc6d
Ported-from: f7e9bd0d230c57922859fd84e5184c903da22113

#### docs(lessons): v2.0.28 - lessons the WP125-WP127 ports deferred

`8b92c568`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- A missing action set's visible effect is read from the set: names as_sauron_facegen (absent here)
- A new reference into the unversioned Armory lands with a g: 1.5.x-only: v1.5
- A config list is validated entry by entry: a null or blank: names SignatureStrikesConfigProvider (absent here)
- A claim found wrong is wrong everywhere it was written: gr: names StrikeSoundPlayer (absent here)
- A vanilla lord in both `lords.xslt` and `characters/lords.: 1.5.x-only: v1.5
- A moved comment's engine claims are re-verified at the mov: 1.5.x-only: v1.5
- A handoff plan's prescribed sentence is a draft: re-derive: 1.5.x-only: v1.5
- A deletion's RED step lives in the log, not the suite: no: names RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly (ab

Ported-from: b747cf398b74914d832462c7b119470df4f7ef9a
Ported-from: 337228885b0725f1a2b0c657e5106c7e0090fe6d
Ported-from: f48ef74dc42d9eebb68af634a6320947384d5fc7
Ported-from: 9804f67b9deba24813da1f2ad7ef2d5f9ad6fa14
Ported-from: 18069643062cd6fb0312ca33ecca015f13ef222e
Ported-from: 30003668bd201bf7707f44f792689aab151ed45f
Ported-from: 76f22e111a18673bc0d9a33be7090a787c1b4b98
Ported-from: 12b1c2003fbf2539f9df62246f70e655c20bfc6d
Ported-from: f7e9bd0d230c57922859fd84e5184c903da22113
Ported-from: d4e6273a198f576cd91f938de6401cc043cb24cf

#### docs(lessons): v2.0.28 - lessons the WP123-WP124 ports deferred

`d405005f`

7 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: 70320bbe1b16a1b5dc5fa4f151f2a95fcc3403ad
Ported-from: 4520af802d9c31f44263c7a9ce4a263e611d0c5d
Ported-from: 13f34c1cbc7c43f9d52f8c9df94af60e01065bc2
Ported-from: 5d9d4cc974d21c1980a9f20a8a06a9a3de5b2afd

#### docs(lessons): v2.0.28 - lessons the WP123-WP124 ports deferred

`034488f7`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- Find an agent's party through `origin.BattleCombatant as P: 1.5.x-only: v1.5
- XSLT cultures no longer inherit vanilla's lord, teen and r: names lord_template_rosters.xslt (absent here)
- When a source that returned null starts returning an objec: names compare.py (absent here)
- An invariant lives in the feature's pure rule, never only: names CreatureBanditRules (absent here)
- A sweep over "every provider of kind X" is only as complet: names SignatureStrikesSettingsProvider (absent here)
- A test's expected value never comes from the code under te: names CachedEnumParseTests (absent here)
- A wiring pin checks the effect, not just that the call is: names CreatureBanditRules (absent here)
- `default(ActionIndexCache)` needs no engine in v1.5.3: che: 1.5.x-only: v1.5
- A read-only `QueryData` accessor makes one read cheap, not: names UnitPitch (absent here)
- Swapping an evaluating read for its cached sibling in a ga: names UnitPitch (absent here)
- An engine method's EFFECT is a decompile question too: quo: 1.5.x-only: Fords
- The Kit stores bone tracks in FBX node order and the engin: names anim_horse_stand_4 (absent here)
- A mount's clip needs a priority and `enforce_lowerbody`: t: names horse_kick (absent here)
- `dotnet test` is not the full suite: run the whole `/verif: 1.5.x-only: Fords
- A docs sweep for a changed value greps the old VALUE, not: names as_war_ram (absent here)
- Verify a re-serialised binary against the original bytes,: names wire_anim_master_clip.ps1 (absent here)
- Before an in-game test, prove the running process loaded t: names TAOM.dll (absent here)
- Expose the domain's verdict to every consumer; never re-de: 1.5.x-only: Fords
- A feature that schedules into a shared per-frame host scal: names RaceAbilityReportClock (absent here)
- Try a substitute-driven test before an IL rule, and name a: names Tick_BeforeTheHitWindow_FetchesTheAttackerSkeletonOnly
- A test that resolves a container graph runs every construc: names RaceAbilitiesWiringTests (absent here)
- A culture-id existence test reads the defining document, n: names race_abilities.json (absent here)
- Name a test for what it proves: an expectation built from: names UnitPitch (absent here)

Ported-from: bb68eeca3aeee49ba95ea4956f2b3941820a3f93
Ported-from: 74e9172d42e7482453b6caa25112cab16f64242d
Ported-from: 36d4dda14001262cb5660e84c7bae79e13f6883a
Ported-from: 13fb30f9c9579c3f8cb3ecaf87a0a38f63aa34fc
Ported-from: a59a48bb6a8464f854e56c4c5a4964fcb602c409
Ported-from: 02157b18ba69c48c5a172024ebe4dd0685f5a806
Ported-from: 70320bbe1b16a1b5dc5fa4f151f2a95fcc3403ad
Ported-from: 4520af802d9c31f44263c7a9ce4a263e611d0c5d

#### docs(lessons): v2.0.28 - lessons the WP120-WP122 ports deferred

`06a3c1ad`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- A tool the docs tell people to run again is a standing too: names pack_banner_sheets.py (absent here)
- An advisory hook's output must reach Claude: test the chan: names notify-test-results.sh (absent here)
- A whole-file revert reverts every fix in the file: re-reco: names notify-test-results.sh (absent here)
- Fill a gap with the engine's own pick on a TAOM pool, neve: names PickReward (absent here)
- A seam that lacks the actor has more than one caller: list: 1.5.x-only: v1.5
- A mixin whose target declares no OnFinalize stacks a hook: names ViewModelWithMixinPatch (absent here)
- A hint on a disabled button needs an enabled parent: names TownManagement.xml:105-111 (absent here)
- An engine event that grows a phase index needs one guard t: 1.5.x-only: v1.5
- A line-number label that drifted twice gets a gate (format: names File.cs:N (absent here)

Ported-from: 8dd76ddb376425da6a167415186f1f4a40781355
Ported-from: 0bb1f3caea18a0f65ea6bfb348b3b087e5579a30
Ported-from: b41de91e8e9b71b1c2003d367f7217b55713f8e2
Ported-from: f83a63e57e4a03d956ccaf76e0274baf6993e226
Ported-from: 549afffde2646f3fce7a142e1ae4d4d3d7bd17ea
Ported-from: affcee2ead013f29a4b059af5ea999be440e0998
Ported-from: 6ddcc8f9b3d097cfe8d31d203015c163a9346093
Ported-from: aa59f68ed63b7da7cc3006d1f93170f5ca1c99b1

#### docs(lessons): v2.0.28 - lessons the WP118-WP119 ports deferred

`14cadf5a`

2 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: f981629cd86f34c2ea907c768a586e224adbd4ab
Ported-from: 8ea3150522e642ca39ece0b8c4c7a1ba273a2c5e

#### docs(lessons): v2.0.28 - lessons the WP118-WP119 ports deferred

`4c489884`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- A SyncData key-miss guard is not a no-record guard, and th: 1.5.x-only: v1.5
- Outside a game Hero.MainHero throws; a test must not descr: 1.5.x-only: v1.5

Ported-from: 92839bc52da2a4d5bf2aeb6404ce415fdd24b3fe
Ported-from: 9165a672781e9996bb109cc1fc3ce99e30f4f32c
Ported-from: 15e5475a45c9f601ddce87b66abd503cfa694bc6
Ported-from: b9d18458ed73ce8d4b48c86ec2ec7e4e48c8a125
Ported-from: 8ea3150522e642ca39ece0b8c4c7a1ba273a2c5e
Ported-from: fd61757bf342f67975f730f3ff39bef014afa855
Ported-from: f981629cd86f34c2ea907c768a586e224adbd4ab
Ported-from: c96a02a4bfea3960760ebbde49567958b43989ee

#### docs(lessons): v2.0.28 - lessons the WP117-WP117 ports deferred

`c84daacb`

1 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: 3dac7ab5b03944b0fdfd44b15043941b382d70ec

#### docs(lessons): v2.0.28 - lessons the WP117-WP117 ports deferred

`550fa501`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- Before replacing an engine list wholesale, grep the WHOLE: names SandBox.dll (absent here)
- An offline model of an engine rule reads the callees, not: names GetHolsterMeshIfExists (absent here)
- Read the per-submodule hook order before moving a mission: 1.5.x-only: TAOM.Client
- A source-text gate must fail on the wrong code, not only o: names SubModuleMissionServerGuardTests (absent here)
- A weight that competes in an engine MaxBy gets an ordering: names CavalryDominance (absent here)
- A fixture labelled as a real line is that line verbatim; a: names TeamDoctrineInstaller.cs:46 (absent here)

Ported-from: 5dfa8f4cb0a9e86118ed41b0c6bdd3d9a977990b
Ported-from: 3dac7ab5b03944b0fdfd44b15043941b382d70ec
Ported-from: 67eff55cf9cace45727b2324a79555d05ec927b2
Ported-from: 569f6750d79e41b5045624ff05c9ef25d529fd4b

#### docs(lessons): v2.0.28 - lessons the WP116-WP116 ports deferred

`4b350461`

13 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- A tool kept in two places drifts: keep one canonical copy: names E:\LOTRAOMAssets\Elephant\_refine_tools\harness.py (ab
- An early exit moves every existing test off the path behin: names suggest-compact.sh (absent here)
- When a fix removes a premise, retire it from the argument: names suggest-compact.sh (absent here)
- A hook that parses a shell command judges each command, in: 1.5.x-only: bannerlord-1.5
- An order added for speed also chose which target a message: names FORCE_HINT (absent here)
- A texture's canvas shape is not evidence, and the kit copy: names t_rohan_ws_set1_n (absent here)

Ported-from: 09f3126a759c3b956e63cee789ff4bd6f8ed355f
Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005
Ported-from: 787fd3663d5238c45c3e4bf8b08c98d9229e953b
Ported-from: 6514fdfe77d85b9c49e9dfb59bb81b21e4cfb4a4
Ported-from: 64b9f3a6c2b522cdd5ee353f9ce594c7e4e9d5db
Ported-from: 747b6dae8e68ae9354739cc7a5d696a9733f559b
Ported-from: a4ba6b0b3e17145c0d0d58d73fdf98b4be18b258
Ported-from: fd1d5c17de387bda26a5966396b69c1782e3a9e8
Ported-from: c80c4108c88e9d5e28ec047458542235639f53c7

#### docs(lessons): v2.0.28 - lessons the WP113-WP115 ports deferred

`a8202c73`

1 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: 709649c3f920dfe86d8cfc1ee0d09a835f2c7eec

#### docs(lessons): v2.0.28 - lessons the WP113-WP115 ports deferred

`f5542b9c`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- An engine rewrite can add unguarded reads to a method TAOM: 1.5.x-only: v1.5
- A race head with no face morph channels crashes the first: names TaleWorlds.Native.dll (absent here)
- A melee release bound to a custom clip crashes the first s: names act_release_ (absent here)
- A UIExtenderEx mixin compiles vanilla code at Register: re: names ViewModelWithMixinPatch (absent here)
- An agent spawned beside the troop supplier needs its own o: names HowdahCrewAgentOrigin (absent here)
- A hand-built `AgentBuildData` gets none of what `Mission.S: names GetAgentBuildDataToSpawnTroop (absent here)
- Never null-check `PartyBase.MainParty`: with a campaign, i: 1.5.x-only: v1.5
- Before relying on another mod's setting or table, find eve: names Options.json (absent here)
- A gate's hook trigger must cover every file kind its scope: names SKILL_TEMPLATE_MISMATCH (absent here)
- An item handed to the player must pass the engine's equip: names tools/generate_career_kits.py (absent here)
- Rebuild a feature's owned effects from the current state o: names rally.json (absent here)
- A byte-compare test on a tracked text file needs a `.gitat: names HowdahPrefabTests (absent here)
- A data pin covers the contract its consumer reads, not onl: names HowdahPrefabTests (absent here)
- A moving prefab: every body `moveable`, `bo_barrier` scale: names Native/AssetPackages/core_game.tpac (absent here)
- A member reached by name joins the reflection binding gate: 1.5.x-only: MonsterSize
- Every animation the Modding Kit imports may name no skelet: names gen_troll_anim_clips.ps1 (absent here)
- At an engine bump, resolve the RELEASED TAOM.dll against t: 1.5.x-only: v1.5
- A removed binary lives on in the install and every channel: names BehaviorTreeWrapper.dll (absent here)
- "Blocked on an upstream publish" is a check with a time st: 1.5.x-only: v1.5
- A hand edit to live data is done when its replay script, s: names tools/apply_animalia_armory.py (absent here)
- A new live-data writer copies the newest sibling's guards: names apply_animalia_armory.py (absent here)
- A fix that removes a parameter removes what it rejected: n: names wire_anim_master_skeletons.ps1 (absent here)
- A consumer of a JSON report rejects its error records, not: names measure_animalia_clips.py (absent here)
- A live-data edit that needs new code changes the installed: names TAOM.dll (absent here)
- A mesh-editing tool's guard lists what it may edit; a run: names strip_upper_mesh_channels.py (absent here)
- A test pinned to one engine build skips at the next bump:: 1.5.x-only: v1.5
- Replay the incident a detector cites through the detector: names test_issue_448s_module_level_sacks_are_not_read (absen
- A binary reader's fixture takes every width branch the shi: names TAOM.dll (absent here)
- Pin an id that crosses into unversioned data as a literal,: names AnimaliaConfig (absent here)

Ported-from: 25995dc91d019fa90038bd849be4c84ba0d55765
Ported-from: 89374fc4f26683f3978072d30aed0832e9a3815e
Ported-from: b9eabd71250e4861fe3921fa1dfea5973e7c2c36
Ported-from: 925db38d14a55e8851b7aeabd7e1a3627fe9dc75
Ported-from: 83bdad8537b9760deb6cd849c7ced8943a5f5a37
Ported-from: 9c310bc238fa1441f4a1344c5cc3fe5dccadb6a2
Ported-from: e4c2c159a93fd8dd5680638e0c4da664cbf33f9e
Ported-from: 709649c3f920dfe86d8cfc1ee0d09a835f2c7eec

#### docs(lessons): v2.0.28 - lessons the WP111-WP112 ports deferred

`a504b356`

9 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: a27c6b33817f6375f793b37c3892f897450e4bfe
Ported-from: 1165a00dec70c2b37b35dce4b97fa8507f7f3272
Ported-from: de90a24514689b1f60afdd32971ffdb6afff141e
Ported-from: 96832589679f84d0ea6f8b6e76cf2928932ab093
Ported-from: 423d948d89f115ddcde6709ed9a8a1d23b88bb13

#### docs(lessons): v2.0.28 - lessons the WP111-WP112 ports deferred

`492fb3d9`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- An engine type with no id gets a per-object adapter; a cla: 1.5.x-only: v1.5
- A map event that cannot reach a round never picks a winner: 1.5.x-only: v1.5
- After a programmatic batch of inventory transfers, refresh: 1.5.x-only: v1.5
- Stage part of a shared generated file by building and pars: names tools/translation_name_tags.json (absent here)
- A vanilla pick of another settlement of my culture can com: 1.5.x-only: v1.5
- A pair that must load together across modules or a save ne: 1.5.x-only: Feral
- Reach for a seam or staged engine objects before an IL pin: names AddLandPath_ (absent here)
- A fix's regression test fails on the old route at the call: names UnequipAll_DelegatesToEquipmentAdapter_ReturnsCount (a

Ported-from: 639df895becd1966fbe1dc15e566b6275372e75e
Ported-from: c17541f2c4595149ea22fe0e473397a513c20da9
Ported-from: a913629cdb33b2b30e6aaf875146447abe1597bb
Ported-from: d4af27d3e464b51eb50de410e28fe081643a5db9
Ported-from: a27c6b33817f6375f793b37c3892f897450e4bfe
Ported-from: 423d948d89f115ddcde6709ed9a8a1d23b88bb13
Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3
Ported-from: 96832589679f84d0ea6f8b6e76cf2928932ab093

#### docs(lessons): v2.0.28 - lessons the WP108-WP110 ports deferred

`b3639f6a`

1 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: d1b9fd91ac32dd2dce5facc0567c577772f4acc6

#### docs(lessons): v2.0.28 - lessons the WP108-WP110 ports deferred

`9f83233e`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- A JVM hosted inside the Microsoft Store Python writes into: names PythonSoftwareFoundation (absent here)
- When a lesson refutes a claim, grep `docs/` for the claim': names LOTRAOM_horses.xml (absent here)
- A `*.xml` folder glob matches `foo.xmlbak` only on a volum: names foo.xml (absent here)
- Name a native budget's trigger and its target separately (: names FUN_18021dea0 (absent here)
- A per-mission summary is written on every teardown path, a: cites engine lines not re-measured
- A save keeps `Clan.Culture` and the template list, and the: names MinorClanMigration (absent here)
- A template that other cultures also list must be cloned, n: names spc_legion_of_the_betrayed_leader_ (absent here)
- Guard an action by the stored state it writes, not by a mo: 1.5.x-only: v1.5
- Reset a per-campaign singleton in the constructor of a beh: 1.5.x-only: v1.5
- A session-launch write that raises a campaign event runs b: 1.5.x-only: v1.5
- Do not make peace or war in `OnSessionLaunched` on a load: cites engine lines not re-measured
- A text scan cannot see a target-typed `new(`: use the comp: names TAOM.dll (absent here)
- A negative relationship `value` declares war whatever `isA: 1.5.x-only: v1.5
- The region-owner check is a gate now, because the manual s: names kingdom_hero_party_arthedain_template (absent here)

Ported-from: 719074072ab5777bd614cda70afc330f8346f1c1
Ported-from: fa8c06f82fbc5ddd4f23ebe78157f5f2144439d2
Ported-from: 332f2ac71414ea39d252ee5e3bea9886576f5071
Ported-from: ef6afeb6729b50756c2b7352603c9870a91e5fda
Ported-from: b23a27ef45b3dd060c9729e63a26f65388e12a6f
Ported-from: a9f10688fbbb2e540b36c5fb593dda71811a881a
Ported-from: 42971374650eee9546589d0177ae852597199f95
Ported-from: 5e43518df7980ebaaec4d22a7370d2df726dd839
Ported-from: 3fe86e22b
Ported-from: 2719e59e8c81648a0b5cc15364a8548f755c00ce

#### docs(lessons): v2.0.28 - lessons the WP107-WP107 ports deferred

`726abd97`

12 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: 65b691b6a745d622afa66eca8e39a3751219a047
Ported-from: 578ac7d617f1fd0ba62827c9f6960611708c4cb4
Ported-from: 5ee49d05f1d24e060ecf597cdd75724f1d545618
Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
Ported-from: f6bf0e58880c077f4859d2d1b0dc0b24e168fc2f
Ported-from: d469914abaa48cc32907256aa911597ad4243101

#### docs(lessons): v2.0.28 - lessons the WP107-WP107 ports deferred

`8756172c`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
Ported-from: f6bf0e58880c077f4859d2d1b0dc0b24e168fc2f
Ported-from: 943ab74c03edfcc6cc124ef14a1b75db7ef038f4
Ported-from: 65b691b6a745d622afa66eca8e39a3751219a047

#### docs(lessons): v2.0.28 - lessons the WP107-WP107 ports deferred

`7387b53a`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- Gate a non-formation agent's scripted moves on the deploym: names CreatureMayFightDecorator (absent here)
- An agent that stands in for a troop must reach every IsHum: names CreatureScoreboardBridge (absent here)
- Read engine types only from vanilla modules: a third-party: 1.5.x-only: v1.5
- "Installed" is not "effective": prove a guard can see the: 1.5.x-only: v1.5
- A check must fail on inputs it could not read, never repor: 1.5.x-only: v1.5
- Temporary diagnostics stay strippable: no rule, no logger: names CreatureBanditAgents (absent here)
- Pin every git option a gate's answer depends on (plan 017,: names .cs (absent here)
- A join seeded from inside the raided village is interrupte: 1.5.x-only: v1.5
- A sub-group under a master toggle inherits its promise: fo: names CreatureBanditTuning (absent here)
- A lookup that derives the original method from the stack m: names GetStackFrameMethod (absent here)
- Follow an escaping exception through every return branch o: names PatchInfoSerialization (absent here)
- The PatchShield exclusion covers every per-agent target of: names CreatureBanditsWiringTests (absent here)
- A performance claim names the machine that measured it, an: names plans/_audit/2026-09-23-opus/followup-patch-tax.md (ab
- A fix pass starts from the review file's own scope line, n: names claude-review.json (absent here)
- Correction: the build stamp's commit suffix is the .NET SD: names GenerateAssemblyInfo (absent here)

Ported-from: d469914abaa48cc32907256aa911597ad4243101
Ported-from: 900920dca4daadbc2684c80e2f5c997f2235b395
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
Ported-from: 43ca92ee99c064467113e603c19c58a5901b9093
Ported-from: c8000699480b59f24e42f2e992ad39b6be7fde26
Ported-from: db1a7166b974b4d6b927fadfb0fd077278e914e9
Ported-from: b0154d86ea126363c23f9190d7e45a8891d5681e
Ported-from: 65b691b6a745d622afa66eca8e39a3751219a047
Ported-from: 578ac7d617f1fd0ba62827c9f6960611708c4cb4
Ported-from: 5ee49d05f1d24e060ecf597cdd75724f1d545618
Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db

#### docs(lessons): v2.0.28 - lessons the WP106-WP106 ports deferred

`10999d60`

9 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Ported-from: 6eb2e1b90c2fe42d6cda851434c0ff6ec1fdcd1c
Ported-from: efe326242585a920e33238b3da20e88ee50f0a32
Ported-from: 8c84fa20e972521ddd79c67f9153cb0ceb8d9fa2
Ported-from: fe8f30c7abfca4ba1cfb590df8013b82757709e4
Ported-from: 7ede923be86130f4d74a274397350df1b3f2c7e1

#### docs(lessons): v2.0.28 - lessons the WP106-WP106 ports deferred

`eccfc321`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- `Formation.GetHashCode` dereferences its Team: key engine: names TrollFormationSpacingStore (absent here)
- A formation's unit width reaches its slots only through th: 1.5.x-only: v1.5
- Fetch an engine NativeObject wrapper once per tick, and ra: 1.5.x-only: v1.5
- Test a NativeObject reference with `is null` in code a uni: 1.5.x-only: v1.5
- A perf commit's win names the cost it removes, read from t: 1.5.x-only: v1.5
- A race's hand mesh needs the 26 hand-pose channels, and a: names transfer_hand_morphs.py (absent here)
- Take hand channels from a hand mesh, and pin and gate the: names SK_Uruk_Hai_BM_A_Arms (absent here)
- Blender 5.2's `shape_key_add` makes a key at weight 1.0, a: names transfer_hand_morphs.py (absent here)
- The melee attack table key is the Kit's "Blends with anima: names UnknownClipName (absent here)
- A gate that runs after the fix it checks can never fail (2: names transfer_hand_morphs.py (absent here)
- Blender Surface Deform with a shape-keyed target moves hal: names transfer_hand_morphs.py (absent here)
- A gate that prefers a cooked asset tree reads stale art (2: names AssetPackages/pack0-9.tpac (absent here)
- A line anchor ending in `.*` splits a CRLF line: the inser: names tools/oneoff/add_hill_troll_hammer_items.py (absent he
- A path gate written with forward slashes never fires on Wi: names E:\repos\TAOM\docs\adrs\x.md (absent here)
- A package check by substring passes the wrong art: the hea: names tools/oneoff/add_hill_troll_hammer_items.py (absent he
- The Write and Edit tools decode `\uXXXX`; a source file th: names .cs (absent here)
- A cloned crafting piece does not keep the damage: the engi: 1.5.x-only: v1.5
- An outside defect register is a list of hypotheses: check: 1.5.x-only: v1.5
- A reading taken from a lifecycle event follows only the wo: 1.5.x-only: v1.5
- A sibling tracker's scan is a limb, not a redundancy: carr: names SignatureStrikesMissionLogic (absent here)

Ported-from: 7ede923be86130f4d74a274397350df1b3f2c7e1
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 6ce20f6edde1df897ed0032927dfcb43b35d756b
Ported-from: f75288ebf5c6866b01e5c578aa232c123c9aa27b
Ported-from: 8c84fa20e972521ddd79c67f9153cb0ceb8d9fa2
Ported-from: 6eb2e1b90c2fe42d6cda851434c0ff6ec1fdcd1c

#### docs(lessons): v2.0.28 - lessons the WP104-WP105 ports deferred

`6d5a300c`

15 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- A release gate for a new artifact follows the shipped mani: 1.5.x-only: TAOM.Client
- Bannerlord.BuildResources cleans the whole module bin, so: 1.5.x-only: TAOM.Client
- A player-facing allow-list is checked against what a playe: names ShippedJson_AllowedRaceNames_ (absent here)
- Check a translated row where the engine reads it, not by i: 1.5.x-only: v1.5
- A new assembly needs every root scan and every typeof(X).A: 1.5.x-only: TAOM.Client
- A diagnostic's "nothing happened" line is written on the e: names AnyRegistered (absent here)
- A mission-scoped write or reset is chosen from every way t: names SandBoxViewCreator (absent here)
- A "no cost" or "no change" claim for a gated path is prove: names WriteEvent (absent here)
- A MissionView added in OnMissionBehaviorInitialize never t: 1.5.x-only: TAOM.Client

Ported-from: 03d9db8dcf13432bf5b4d60e3bc63d0d60d60831
Ported-from: bdf7d515c49740c60d0204770c503bdb2b5436dc
Ported-from: 346f3e093ccc2c146107a3b7e005c0c06963cd85
Ported-from: 27fd23bbb7c4ad8b79947d3621f3b6989f778c71
Ported-from: fa0b24e139ae81d606630e78513462dc4b626b75
Ported-from: e0ad0db765d3f6f47cbce580eb8eb5662377d120
Ported-from: d96f349f1124ab474eb9b299b7c1389f6d9f8365

#### docs(lessons): v2.0.28 - lessons the WP102-WP103 ports deferred

`9607a4ac`

13 lessons from the 1.5.x records, appended to their category files.
Each names only code on this tree. Line cites are dropped rather
than carried unmeasured, and each Source is text, because the RCAs
it names are on the 1.5.x line only.

Skipped, with the reason:
- Mark TAOM's own blows; never infer them from a field the e: names IsRegisteringSyntheticBlow (absent here)
- Fitting a foreign rig onto an engine skeleton: measure the: names swing_deg (absent here)
- A mesh bent onto the target's rest needs its clips carried: names retarget_mannequin_to_human.py (absent here)
- `\b` is not a separator in snake_case: `\bao\b` never matc: names elk_m_ao (absent here)
- PowerShell variable names are case-insensitive: `$M` and `: names gen_animalia_anim_clips.ps1 (absent here)
- A Fab pack's first skeletal mesh on a skeleton may be a fu: names ue_export_cave_troll.py (absent here)
- A CI step covers only the branches its workflow's `on:` bl: 1.5.x-only: bannerlord-1.5
- Cloning a sibling clones its unverified claims: re-verify: 1.5.x-only: v1.5
- A value-returning finalizer that hands back its exception: names CreateReplacement (absent here)
- A smoke step that proves an engine decision names the game: 1.5.x-only: v1.5
- A roster edit reaches the files generated FROM it; find th: names ElkMountWiringTests (absent here)
- Record a shared resource's dependents at the SOURCE, not o: names as_war_ram (absent here)

Ported-from: 6e4bfedb7769bdc1d45d3cf8f90f1564851b9015
Ported-from: c79a585218ad22841c8db1f273e013385512b332
Ported-from: 143f0fa8540032657beb979977e2a52329a6e95d
Ported-from: fff6d66e9bd5a2db11c0ef20ab167225efbd1a22
Ported-from: 7b342ed59ffeb3b1a856717b65ef32f1bd8e1c94

#### docs: v2.0.28 - feature map and index rows for the ported features

`cfe4f5c7`

feature-map gains a row per feature folder the port added, written on
this tree (patch numbers as registered here, no 1.5.x-only wiring), and
loses the removed AutoResolveDiagnostics row. INDEX names every feature
doc the port added. refuge.md carries the #773 note citing the rule that
csharp-architecture.md holds. release-process.md gains the #1026 check
for each channel's text processors (WP135's hand-off).

module-taom.md no longer holds the v2.1.x sentence WP102 flagged, so
that row is skipped. No doc-lookup or provenance row was named by a
merged WP beyond what WP116 landed.

#### docs(tools): v2.0.28 - README rows for the tools the port added

`66b81caa`

Rows for every tools/ script the port added, taken from the 1.5.x tip
and rewritten for this line: every flag checked against the tool, and
v1.5 examples, Client paths, co-op server logs and hooks not wired here
removed. engine_body_diff's exit line is corrected: it exits 1 only on
unparsed or unresolved rows (WP137). check_release_pairing uses WP115's
wording, and check_public_text, _marker_region and _loc_sources are
written from their docstrings.

Existing rows updated for behaviour the merged WPs changed:
generate_starter_kit, generate_ranged_ladder_items, translate_with_claude,
generate_clan_heraldry, tpac_skeleton_extract, validate_mesh_refs,
convert_tripo_prop_textures, complete_lords_xslt (WP118),
generate_enlistment_rosters. New: Apply-MapVillageNames,
generate_tavern_mercenaries, add_collision_body, taom-src. The
add_map_villages row keeps this line's text, because the 1.5.x row names
realms absent here. check_map_vista.py is not on this line (plan 121 E1
did not land), so no row is owed.

Ported-from: 1a2da1e3eabe12f4497232956b2cb77b7a3bf0bf
Ported-from: 04c3c4540f0532b55decf1f4dc90fa151233cedd

#### docs(validation): v2.0.28 - WP136 hand-offs and the audit record

`e37e8025`

The tools/README rows WP136 left: validate_xml_schemas.py, data_gates.py,
audit_mount_parity.py --strict and the two generator bullets. The
check_prefab_budget row needed nothing, because WP136 left it current.

SCHEMA_INVALID reached this line on 2026-11-05 (12931e9ef), so three
lines drop the 1.5.x date 2026-09-18; two of them moved to the validator
codes doc when the rule was split (4604d882c). The Coverage boundary section
no longer says a clan missing a required attribute passes the validator
and the hook, and editing-safely.md and validation-and-testing.md now
name the codes doc for the code table.

The skill usage audit is ported from the 1.5.x line, with a header note
and no footer, because three review records here link it. That removes
the four dead links.

Ported-from: 8b108312744c3daabc5fab6383dbea67db0d96dd

#### docs(lessons): v2.0.28 - data, IL and API lessons from the 1.5.x records

`f73dc7d8`

Twelve entries with their Source as text and engine lines re-cited to
v1.4.8: PartyBase.cs:343-355 and LocatorGrid.cs:131 and :139. Entries
built on 1.5.x features (the Eye hunt, the chronicle) stay as history
because their rule holds here. The melee-pass and #812 details, and
HaradWeaponsLiveEditTests, are dropped as absent here. The harmony-il
entry folds in the second instance from the second review, and the
figure-and-tag rule becomes its own native-cpp-port entry. The Erebor
dress lesson is content and stays out.

Ported-from: 0b08eeeabbba2a58d9a611fc46315d18787b2494
Ported-from: dde30a5b72f6cd4390d282903fc697ff0701e03b
Ported-from: 42693d9bde181d26a14eab890407b0f562a5a958
Ported-from: 33253ea9d4ab6cb1e93d8f4d82afdc4a61e9d55b
Ported-from: b3fac3021dcffbbfb1190a1854fa886dc5d78ff1
Ported-from: bc01a545ca75e847be5a2ad9ab4567b7c2460e54

#### docs(lessons): v2.0.28 - process lessons from the 1.5.x records

`f63aaca9`

Twelve process lessons, each with its Source as text because
the RCAs they name are on origin/bannerlord-1.5.x only. The untracking
lesson says what WP142 did here, and the existing MCP deny lesson names
where those entries went. The mesh-tool lesson is left out because
strip_upper_mesh_channels.py is not on this line.

Row 9 keeps the troop-name generator example: WPs before this one
landed generate_name_localization_strings.py and the troop strings.

Ported-from: 1ce8c370e36a1af37a7479b7dd5f49e88029e7b9
Ported-from: 0aad0d941a574d48c43660435457cacfe387d7ec
Ported-from: 62157c7e775288d7907283a0dfa6fa5a51da19e0
Ported-from: fd2072a0a1c7179226fc4c0f28e1dc81975ab450

#### docs(perf): v2.0.28 - the inventory memory survived one collection

`c5b8bb3f`

Both records said the 2.6 GB left between inventory opens survived two
engine collections and is rooted. The [MemStation] enter line is
written before the push's own collection, so each reading followed one
collection, the pop's, taken while the closing screen was still on the
stack. The next collection frees it, and the size is cheat mode's item
list. The correction is re-cited to v1.4.8 GameStateManager lines; the
older cite in the audit row stays as the record being corrected.

Research: taom-src TaleWorlds.Core.GameStateManager
Ported-from: af990ca769da52f408b8e804d127c446b515a379

#### docs(rules): v2.0.28 - model the deserializer when reading troop kit

`b46d9d27`

A script that decides what a troop wears must build each set the way
BasicCharacterObject.Deserialize does: an <equipment> directly under
<Equipments> overlays that slot in every set, civilian included. The
rule section and its lesson are re-cited to v1.4.8. The cherry-pick
conflicted only because an earlier port moved the neighbouring section,
so the new section lands alone and that section stays where it is.
88 troops in troops_*.xml are mounted this way.

Ported-from: df273ea24cb342708622536826d49ed3b07083c9
Ported-from: c79a585218ad22841c8db1f273e013385512b332

#### docs(engine): v2.0.28 - engine docs cite lessons, FillFrom copies all

`06bbc9ee`

Memory-card citations in the engine docs become links to lesson files,
each quoted heading checked against the file, and CLAUDE.md pointers
become the registries. feedback_ hits in the .md engine docs went from
48 to 0. item-equipment-model.md now says FillFrom copies all 12 slots,
empty ones included (Equipment.cs:184-194 on v1.4.8). The spider docs
describe the Horse-slot mount this line ships; the 1.5.x clause about
riderless Mirkwood broods is left out, as are the settlement-economy
hunk, 1.5.x SubModule lines and the agent-stat list. The XSLT tool's
docstring carries the measured 17 stylesheets and records that CI is
gone (ccef172ad).

Ported-from: 50e8dc7aea26193691a93fb8750231daff74a191
Ported-from: 66dd975363a5e227e354a3e87bb950c22086da5f

#### docs: v2.0.28 - correct modding and reference docs against the code

`f56fe022`

Re-measured on this tree: 130 cultural feats with 1 to 13 ids per
culture; 17 localization source files, 2,670 module strings of which
407 are taom_str_ keys, and 1,657 aom_ wanderer keys; the #371 fields
are all present again (cc1713eb removed them, 95085de30 restored them);
four TaomVolunteerModel overrides; the shipped console command
exceptions and three missing rows; 19 transpilers in 17 files and 55
classified prefixes.

castle-recruitment already held 9 tests and the logging clause, so that
row is skipped. Other lines of module-dependencies.md still describe the
absent pin; they are not in this step's list and stay for Part G.

Ported-from: 3b1af2777d211fd15acf0518015aef1552109c36
Ported-from: ff1fe1933dcf6d0eca4daae2fd1b1992faf32820
Ported-from: 1a2da1e3eabe12f4497232956b2cb77b7a3bf0bf
Ported-from: 2e2a422dc86e5575d7f27f618f271205433ee066
Ported-from: 86d3a774e32381cf61413478fb2efe4a40e955a6

#### docs: v2.0.28 - correct feature docs that fell behind their code

`f3c7d6ea`

Each value was re-measured on this tree: test counts, SubModule.cs and
EncounterAdapter.cs lines, the momentum JSON (25 and 40), the parked
naval default, the five CaravanTrade registrations, six TroopWeight
initialisations. return-to-army drops the claim that vanilla's "follow
us" line reaches the unattached state, because on v1.4.8 no dialog line
outputs lord_tell_gathering. settlement-food and siege-trebuchets take
the 1.5.x corrections whole; army-targeting takes only the hunks true
here, leaving the theater tables, realm lists and Patch49 changes.

Skipped: the module-armory lower-case note, because its anchor (the
resize-pass line) does not exist in this tree's module-armory.md.

Ported-from: 2e34cade18aff82914278723a271d80ad4dfbe62
Ported-from: 20616aad311cddf736d1107ab33d2d6c9fd81b9b
Ported-from: 8ce80c2ff98475938c2270fa406b4f7024470b80
Ported-from: 28b8f62824332b5a2d40d55c7c88fbff4799d867
Ported-from: 24e52abb95f55fbb2dd5a9f2f49c6117773addaf
Ported-from: f4058993aa86d98d3feaa4546131d0ab337cd2a7

#### docs: v2.0.28 - correct stale paths in the living docs

`641d7560`

The prefab folder on disk is GUI/PreFabs and the kingdoms file is
taom_spkingdoms.xml, all lowercase. Sentences that described the old
misspellings are removed with them, and the registering line is now the
measured 144. Four one-off tools are named under tools/oneoff/. The
never-landed GetCivilianEquipment_Patch row is gone, and the child
template, heroes.xslt and commanders paths match the tree. faction-ui.md
does not exist on this line.

Ported-from: bb7da8ff4e364a34aea8833ee6b3dde4b80e1039

#### docs(reference): v2.0.28 - ModuleData hook note, CompanionTactics row

`864b97ad`

The ModuleData commit hook also reads the live TAOM_Map and Armory, so
an ERROR there blocks every ModuleData commit; hooks-catalog now says
so. feature-map gains the missing CompanionTactics row, naming the
setting by this tree's name (EnableOobFormationPresets, renamed in
#1017, where the plan named the old one). The stance test count is 9.

The snapshot README's count hunk is skipped: WP137 already rewrote those
rows without counts, and only the historical 220 note remains.

Ported-from: 04c3c4540f0532b55decf1f4dc90fa151233cedd
Ported-from: 66e3fd593bb4538e48724131e2335398a2adfca7

#### docs: v2.0.28 - ADR-001 allows validated JSON feature configs

`6f9d0a02`

ADR-001 still banned JSON while 66 feature tuning files under
ModuleData are JSON. The amendment records the rule the repo follows:
engine data stays XML, and a feature's own tuning numbers may be a small
JSON file its config provider validates on load, with Replace semantics
for any list that has a default. The four B04 feature-doc hunks stay on
the 1.5.x line, because those docs do not exist here.

Ported-from: dc275dc4ef92ac469013b575352d5060fb810287

#### docs(contributing): v2.0.28 - open the repo to outside pull requests

`15fa4614`

PRs already reach this branch, and there was no route for them. This
adds CONTRIBUTING.md, a PR template and /review-outside-pr, adapted from
the 1.5.x tip state of those three files (d08637c73 plus later edits in
2992763f6, 452796db4 and 434d1d795; those are WP143's commits).

Build and test of a contributor's PR run their code on this machine, so
.ai/policy.md's prohibition holds: both need the maintainer's word for
that PR. GitHub serves the PR template from the default branch, so this
copy takes effect only if the default changes. CLAUDE.md gets the
routing row; its skill sentence no longer holds a count, so no recount.

Ported-from: d08637c738363d31ac614b34016a07296a833dc3

#### docs(skills): v2.0.28 - engine bump writes its Gates after the last run

`231014bd`

An impact doc's gate numbers go stale when a later commit moves them,
so the skill now writes them from the final /verify, after the commit,
and rewrites the whole section on any later change. It lands as Phase 4
item 8 because this line's item 7 is the native address pins.

Ported-from: 370b4bf2b24bf3edf4881a2613500ded15090a75

#### docs(release): v2.0.28 - channel numbers, version policy, v2.0.29.5

`ca354123`

/release Phase 1 now checks that no channel already carries the target
number, because the public 1.4.8 build carries a hand-typed v2.0.29.5
that is in no commit. From v2.4.0 on the TAOM.Dependencies version
moves with every release (W1), so all four modules read the release
number, and the live TAOM_Map and Armory manifests move by hand.

Crash reports naming a number git never saw resolve by build stamp;
release-process.md now says so. The Phase 8 maintainer route and the
pack0.tpac prune note come along.

Research: E3 player log [BuildStamp] and module list; git log -S
Ported-from: 62157c7e775288d7907283a0dfa6fa5a51da19e0
Ported-from: 04c3c4540f0532b55decf1f4dc90fa151233cedd

#### docs(workflow): v2.0.28 - reread the issue body, one keyword per issue

`1deee129`

A body written before the build is a plan, so the completion workflow
now rereads it against the diff before the commit. A closing keyword
closes only the number after it, which left two issues open on the
1.5.x line.

Ported-from: 4e7efe5a8a3a1df4179120523596cf97752105cf
Ported-from: 04c3c4540f0532b55decf1f4dc90fa151233cedd

#### docs(readme): v2.0.28 - players need no separate Harmony or MCM

`3b9dfae9`

The install section now lists the four modules and what
TAOM.Dependencies carries, as dr3-maintenance.md asked. Hand-kept
counts are removed rather than refreshed because nothing computes
them. The test command no longer deploys, the clone line names the
branch, and the note names both lines: origin's default branch printed
refs/heads/bannerlord-1.5.x on 2026-11-06. The agent manual's Build row
now says what each of the two flags skips.

Ported-from: c833522989a9629f998fcd55fd34ee4638867a64
Ported-from: 8a638831166f1bc979bef6433b180452347335db
Ported-from: 1ce8c370e36a1af37a7479b7dd5f49e88029e7b9

#### docs(plans): v2.0.28 - record WP138 review pipeline

`6bc210a1`

Row 138 DONE at merge 9e8d18814. Record names the four dead links in
byte-copied review records (no owner yet) and two stale references
owed to WP143.

#### docs(plans): v2.0.28 - record WP127 C and M, and WP122 C4

`1d6e2aa6`

Both integrators returned before their own suite finished, so the
merges landed without a record. Each record cites the first later
merge whose full suite contains that merge.

#### docs(harness): v2.0.28 - catalog rows state the two rules' scope

`151e4c54`

The rows copied from 1.5.x have two cells, but this line's table has
three (Rule, Scope, Content). So data-scripts.md had no scope and
moduledata-validation.md had prose in its Scope column. Both now list
the globs from their frontmatter.

#### docs(harness): v2.0.28 - end the engine lens bullet on a full stop

`b1f5412d`

Removing the Fords sentence, as plan 138's table row for
lenses/2-engine-compat.md:20 asks, left the bullet ending on a colon
with nothing after it.

#### docs(improve): v2.0.28 - rebuild the skill around the run pipeline

`62cba79d`

/improve can now run, review and merge a plans backlog: a run protocol,
dispatch rules, the execute and review stages and the workflow scripts'
tools. references/closing-the-loop.md is replaced. The review-codex text
WP103 held back until this protocol existed is restored.

Adaptations from the plan's table: section names point at what this
line's AGENTS.md and CLAUDE.md hold ("Code Review Rules", "Always",
"Critical Rules", "MCP Usage Guide", "Traps"); orientation.md cites go to
the CLAUDE.md Traps table; the budget scanner stays under
/context-budget; the E: and off-C: drive rules apply on the desktop, and
on the laptop worktrees and scratch live on C: outside the repo.

Graphify stays: K13 is true (WP141), so the plan's [G] rows are not
applied. plan-template.md loses only its dead AGENTS.md citation.
Riding hunks: 6e4bfedb7, 30b54df75, bdf7d515c, c79a58521, 332f2ac71,
2a5363e90, affcee2ea, 9c29d7324, d4e6273a1, 6a2ce8cb3, 48d66b59a,
c1ce78207, bc30b76da, d51e43028, 69b47d00b, b8833474e, e6745a492,
62157c7e7, 709649c3f, 4520af802, fdf9f38cb, 88e805450, c8d69a0dc,
03d9db8dc, d021426b0.

Not-tested: a /improve run end to end (live check owed).
Ported-from: 1d1abd6940e7765f3d08247afa3ec68acda12d23
Ported-from: 84c606fa341fd091bc893754135879b7fd5378db
Ported-from: 37ddf739d2c2dce7c095f5bd223ab07f03347313
Ported-from: dc260635b2fc6543831d72481545858935ae16b1
Ported-from: 014e23022c279628c3dab8cab52f217377313ebb
Ported-from: 56e9970fd8ebcbeed8f565dce04cf51cfffc733a
Ported-from: 6c4b0981b79f5f6ff588c9fd51b6d2c29e53afbd
Ported-from: 989c87497291fc9be92abd6c067af71d227001eb
Ported-from: ef9559b14f849f43477e5b9a55628fb6f2314ea7
Ported-from: 9e9f04df40c4a2350fd705b9dc2c0c158aee49c6
Ported-from: 4407fbc055cb48f7f748415e5298540e210af060
Ported-from: 4d17aeab8da29c7426ebfa80c813fdda890ee6fd
Ported-from: a7bbf3fe7915393a9151cd7fdffe0fe59f91da61
Ported-from: 2fb9d914abd7c93e3e11c7553a8d34de687e1382
Ported-from: 09f3126a759c3b956e63cee789ff4bd6f8ed355f
Ported-from: ad3967222267b7e0d5a142a6fca1e74ef0197650
Ported-from: 8b108312744c3daabc5fab6383dbea67db0d96dd
Ported-from: d6b537092e85ab8ee854cf64eecde34219525214
Ported-from: 785b2703e897fe3c3ccf01b8e7d9b847074d7bd1

#### docs(harness): v2.0.28 - split the moduledata rule, data-scripts rule

`4604d882`

moduledata-validation.md had grown to 33,867 B and loaded on every file
under tools/. The rule keeps the validate step, the gate per file kind,
the GUI prefab gates and a short Discipline, and is now about 5.3 KB.
Scripts under tools/ get their own data-scripts.md rule. The code table,
what gets scanned, the long Discipline and the history move to
docs/reference/moduledata-validator-codes.md.

WP136's hand-off is partly back: tools/data_gates.py restores CODES_DOC
in moduledata's read_first, and the codes doc gains six rows from the
1.5.x doc (BROKEN_BODY_PROPERTY_REF, HARNESS_FAMILY_MISMATCH,
MISSING_EDUCATION_TEMPLATES, MISSING_HARNESS_FAMILY_TYPE,
RANGED_MOUNT_USAGE, UPGRADE_SKILL_REGRESSION).

Not landed: test_every_emitted_code_has_a_row_in_the_codes_doc. This
line still emits SKILL_TEMPLATE_SHADOWS_SKILLS, which 1.5.x replaced with
SKILL_TEMPLATE_MISMATCH, so neither source commit has its row, and plan
138 makes that a STOP rather than a row to write. The test would fail
until the maintainer rules on that row.

The one moved line that is not verbatim is "Why this rule exists": its
three links to sibling rules point at .claude/rules/ from the new folder.

Ported-from: 2ca35d6e2edcdaf38c057a53b0da11e1c141c0ad

#### docs(ai): v2.0.28 - body diff, relations rule, XSLT map, stale skill

`4fbeef0b`

/engine-bump gains the member-level body diff: a member whose signature
holds and whose body moved passes every gate, so diff the bound surface
with tools/engine_body_diff.py and review replacement GameModels first.
adapters.md gains the relations and party-screen rule. The XSLT rule and
/xslt-check sent five stylesheets to SandBoxCore files that do not exist
on the installed v1.4.8; they now read SandBox's, per a table.
/migration-status is deleted: it described the finished v1.2 to v1.3
migration (audit finding DOCS-04).

The plan's merge with WP137's Phase 3 paragraph does not apply yet: that
.claude hunk is still deferred, so the body diff paragraph names the tool
and its --help itself. CLAUDE.md states no skill count any more, so the
count edit is skipped. Left out of the 14 skills the source commits
removed: all but this one. The 1.5.x sentence about the v1.5.2 calibration
is labelled as such.

Research: taom-src v1.4.8 ChangeRelationAction (:14-30),
PartyScreenLogic (:1525-1534), PartyVM (:3246-3264); the installed v1.4.8
Modules/SandBox and Modules/SandBoxCore ModuleData listings.
Ported-from: 43fe5ebe8ea8f7b20b9ab337072d3468e3674103
Ported-from: f382ff6fdc6c11de2d51ceae6d68fdd91588cc82
Ported-from: d6b537092e85ab8ee854cf64eecde34219525214
Ported-from: 8b108312744c3daabc5fab6383dbea67db0d96dd

#### docs(ai): v2.0.28 - builder template asks for docs, bounds, true text

`47f3c9b4`

The agent-teams builder template now asks each builder to update the
feature doc as the code changes and grep docs/ for a deleted or renamed
member, to range-check a config value and compute derived values in long,
and to make a result message true on every branch.

Ported-from: 8c1305e20d718912649de7da8af72e94b8dc158e

#### docs(ai): v2.0.28 - fix stale facts in skills, agents and rules

`224d1a26`

The specialist agents, rules and skills lose facts that had gone stale:
fixed version numbers now read the pin, taleworlds-researcher no longer
offers an ILSpy MCP fallback its tool list never granted, feature-builder
points at AGENTS.md and .ai/review-reference.md for the rules and runs
the no-deploy test command, and /native-crash-triage is version-neutral.

Hunks whose removed lines do not exist here are skipped: the /deslop
boundary stays on refactoring-specialist and /build-fix on feature-builder
(both skills stay on this line), gamemodels.md keeps its count, and
/localize has no Case D. Also skipped: the engine-bump CI hunk,
external-skill-ports.md and the improve hunks. feature-builder keeps the
graphify step (K13 is true) without its hook sentence: this line has no
check-graphify-usage.sh.

Ported-from: 9e9f04df40c4a2350fd705b9dc2c0c158aee49c6

#### docs(skills): v2.0.28 - Codex effort is sized to each review

`612c727c`

Every Codex dispatch now passes -c model_reasoning_effort sized to the
review; ultra in .codex/config.toml is the default and the ceiling, not
a requirement. /review-codex says what each level costs in time and how
to review a worktree, and its background timeout rises to 7,200,000 ms
for unattended runs. /codex-verify's probe string now quotes AGENTS.md
"Always", as WP103 did for /review-codex.

No Codex run happened for this commit; only the text changed.

Ported-from: ef9559b14f849f43477e5b9a55628fb6f2314ea7
Ported-from: b84dbaeb2a713301908c69b8366a2a8b639e6ede

#### docs(ai): v2.0.28 - route subagents by model and effort tier

`1b36dd8b`

A new always-load rule routes each delegated task to the cheapest model
and effort that fits, with three tier agents: fast-reader (Haiku, low),
implementer (Sonnet, medium) and architect (Opus, high). The five
specialists gain effort tiers, and error-detective and
taleworlds-researcher run on Opus. CLAUDE.md's Model Routing section
becomes a pointer; it told Explore to run on Haiku, which a spawn only
does if it names the model.

The rule's first paragraph comes from the 1.5.x tip: the maintainer
reversed "main session = orchestrator" on 2026-11-02, so porting the
older text would ship a known-wrong instruction. WP143 skips that hunk.

Ported-from: fdec324f89d7e7e7f12d77c1724a24588cdfbe54
Ported-from: d0e4bc947ef00a53dcd32db67d0f5e94b8b8d4b0
Ported-from: b84dbaeb2a713301908c69b8366a2a8b639e6ede
Ported-from: 9e9f04df40c4a2350fd705b9dc2c0c158aee49c6
Ported-from: 2c737d97cc7c22df1d1b3fc59135da9cf96facf2
Ported-from: 2992763f6c6f777de1d62e4ca4f8a0b83f165aa5

#### docs(harness): v2.0.28 - pstack checks the workflow runs

`877bba26`

worktree_audit.py sorts every worktree into prune-safe, WIP or unmerged
and removes nothing; session-start.sh names it past 15 worktrees.
perf_runs.py gains the rows perf-method.md's checks read, and that doc
says how to judge a perf result: a faster number needs an independent
check. Feature docs gain a "How to verify in game" section in the
template, and /investigate, /ship, completion-workflow and the prose
rule pick up the pstack review's other checks.

Adapted: the session-start block has no shared probe deadline on this
line, so it runs one 2 s git call and keeps the UNCHECKED note, and its
test rows sit in section 7o. worktree_audit.py's default base is this
line's trunk. perf_runs.py is a three-way merge with the WP118 docstring
edit (fb3e560e4). The rea register row is reworded in place.

The pstack register row and detail come from a later 1.5.x state, since
the record needs them. Left out: the lesson entry, and the tests.md
line, which waits for the lens it cites.

Ported-from: 2fb9d914abd7c93e3e11c7553a8d34de687e1382
Ported-from: 88c0b6985bf7828abd744c1eef54eea6ae3a67d4
Ported-from: 28de0dc7d49df9e7a6a11f4d460213babd05a4cc
Ported-from: 21f2ba79dff0e1af5688df69c3a981806ab8139a
Ported-from: 5eacdedc1d45957dff584583dc7b66468436a1ee
Ported-from: 4407fbc055cb48f7f748415e5298540e210af060
Ported-from: ad3967222267b7e0d5a142a6fca1e74ef0197650

#### docs(reviews): v2.0.28 - measure Headroom, do not install it

`a0b9fa88`

The record of an offline measurement of headroomlabs-ai/headroom on the
1.5.x line, with its register row and adoption bullet. Nothing was
taken and nothing is installed.

Ported-from: 6a54436c6a1b421f5d89c5483c1b8b3c90fc2243

#### docs(plans): v2.0.28 - record WP116 hook gates

`f1473f90`

Row 116 reads DONE and the integrator record gains the merge line.

#### docs(mcp): v2.0.28 - finish the server list and plugin row from C2

`8f9e17c3`

Step C2 item 4 of plans/116-hook-gate-end-state.md was only half done.
The Configuration list still named GitHub, filesystem and git as servers
in .mcp.json, which holds elevenlabs, ilspy, imagine, serena and
taom-moduledata. The list now reads as the plan prints it.

The same step had rewritten the first column of the plugin-overlap row,
so the row said `gh` CLI twice and no longer named its job. It now reads
GitHub issues/PRs, `gh` CLI, `github` plugin (disabled).

Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005

#### docs(skills): v2.0.28 - context snapshots record failed approaches

`1a059d43`

/context-save asks for the approaches tried and abandoned, and
/context-restore shows them first and warns when a snapshot is over 7
days old, behind HEAD, or names a commit git no longer knows.

Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005

#### docs(plans): v2.0.28 - record WP140 Part D

`df386048`

Row 140 reads DONE at merge 3bf81fc5b. A test for the require_build
call line, the WP142 and WP143 hand-off lines, the in-game checks and
removing the 19 deleted prefabs from installs stay owed.

#### docs(hooks): v2.0.28 - catalogue the PowerShell gates and Stop channel

`9a74a4fb`

The hooks catalog, hook-authoring rule, harness facts, MCP server notes
and CLAUDE.md now describe the gates as they stand: one
Bash|PowerShell group reading through _shellwords.py, a raw-payload
prefilter in every shell hook, the push judge's own deadline, and the
Stop block channel. The catalog carries a recount from measured numbers
and a note that decision ids in copied comments are 1.5.x plan ids.

To stay under the CLAUDE.md budget, the Hooks paragraph drops its
sentence on the anchored registration form, which the catalog holds.

Ported-from: 2d7770b647878b845b0b8e77e5695060d1f0b121
Ported-from: 6fe4c8fe4b7f0f44c5173b115aed265ca95be1b0
Ported-from: b34d615a0b878b8f70085b522c8b8189bd0a56e7
Ported-from: 787fd3663d5238c45c3e4bf8b08c98d9229e953b
Ported-from: 43e6780e4421fb785ac9bf1c4ee5bc53e8b69a68
Ported-from: c80c4108c88e9d5e28ec047458542235639f53c7
Ported-from: 64b9f3a6c2b522cdd5ee353f9ce594c7e4e9d5db
Ported-from: a4ba6b0b3e17145c0d0d58d73fdf98b4be18b258
Ported-from: fff6d66e9bd5a2db11c0ef20ab167225efbd1a22
Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005

#### docs(rules): v2.0.28 - name the GUI prefab gates in the rules

`cd9af9c5`

The earlier WP140 run held these .claude/rules hunks back under D27
rule 5 while the harness plans were not merged. WP102 and WP103 are in
now, so the text the plan printed lands in one commit.

gui-ui.md: the insert-position sentence, the hover target paragraph,
and the brush paragraph plus its gate line, which now describe the GUI
prefab brush, property and attribute tests. vanilla-data-comparison.md:
drop the stale clone count, and a clone with no TAOM edit is deleted
rather than re-synced. tests.md: a gate keying on repo paths compares
ignoring case. moduledata-validation.md: a table of the GUI prefab
gates.

Ported-from: 69b47d00bcb77c3f689efab03c9a8f2756f687c3
Ported-from: 42148af4cd851687f55b5bf94195cb57287c5b35
Ported-from: 62fb6bb1deaac84d4daceada5657c30151ea811c
Ported-from: 450332b74cf82403d78033463687dafec446fedf
Ported-from: abb718c2fc835297e2e5a6e9ab9973acaf291f79
Ported-from: 7012783b35f0329c128e5bc1d3aaf2781a24ad79
Ported-from: fc932d4ca22a6741366c8c13b4f98d7532a3c7de
Ported-from: 8d5cfc6231a8638610b6c1a4e2225cb98c92ca50
Ported-from: 5408487956f6f7bf41ca21bc6a4aeecafd5c4b18
Ported-from: e6dbea3e028e4a296815de81258741c7f9dea734
Ported-from: 53a4e4e52a2a67a1098edfb588ab7f5a71900056
Ported-from: d4fb51a5056be817c48692e6e4783b52bb9bdbfe

#### docs(plans): v2.0.28 - record WP115 B and C5

`67dd1800`

Row 115 reads DONE at merge 3737d668b. Maintainer steps M2 to M5 and
the in-game checks stay owed.

#### docs(release): v2.0.28 - build, gate and pair a 1.4.x release

`e71eee9c`

/release ended at the tag and push, so nothing rebuilt at the tag or ran
the packager. A new Phase 8 does: build at the tag, --require-build, the
pairing check and the ModuleData gates, prune, then the maintainer
packages and the channel folder is gated again. Phase 1 adds the
taom_test_ check and the owed in-game count, and Phase 5 writes the
testers' checklist and names the TAOM_Map and Armory that ship.

release-process.md gains step 9 with the prune rules recounted for 1.4.x,
how to read the bundle's Build: lines and the [BuildStamp] verdict, and a
section on the recorded TAOM_Map and Armory identity. The prune count for
TAOM.Dependencies is 42, measured at this commit; the plan's 36 predates
the BUTR refresh. The [BuildStamp] text also names the flagged-stamp
verdict that WP115's review added.

Ported-from: ff84e1b88fe01e21fabc920dc8b922c1c474a270
Ported-from: db1a7166b974b4d6b927fadfb0fd077278e914e9
Ported-from: d5033e4e897f3a0500ec5808ba495d8217211c42
Ported-from: 9cedb92df241a24a2ba65d5aa3f5dc47805484b1
Ported-from: 3c747f4fb7ff809c66697b62f00ad71a67a51de5
Ported-from: 9ca99437fc3879c9a981d8430ff8d44873c209a0
Ported-from: e4c2c159a93fd8dd5680638e0c4da664cbf33f9e
Ported-from: a4b5d625863f80f542eb8240ee061ae16386e9f3
Ported-from: e6ee8c9985beee1b63dafb2cda11c1a9b5879ae4
Ported-from: 715530875ca12d3f3058aa7cc748738f73ca5745
Ported-from: 709649c3f920dfe86d8cfc1ee0d09a835f2c7eec

#### docs(rules): v2.0.28 - CLAUDE.md one-liner makes the hook optional

`adb1f949`

CLAUDE.md's architecture line now matches the rules Part C aligned:
a patch calls its service directly, and an IOnXxx hook sits between
them only when the patch needs a narrow seam or a test fake. The
edit was held out of the Part C commit because the file was then over
its size cap; it adds 4 bytes, for 45920 against the 46000 limit.

Ported-from: 9c29d73242e05b33428970ae54bd439dba099b26

#### docs(adr): v2.0.28 - ADR-002 and ADR-007 name the gates that hold them

`9a565c38`

The two ADRs promised tests that did not exist. They now name the gates
that landed with WP136 Step E1: EntryPointLineCountGateTests and
ServiceLocatorGateTests for ADR-002, ServiceSignatureGateTests for
ADR-007. ADR-007 also stops calling every class sealed, since several
are public classes, static classes or a struct.

Ported-from: 8caac7da5df694fbdebc0ec4581eb4ffc0ef1cfc

#### docs(adrs): v2.0.28 - ADR-003 allows a service split by concern

`605acf68`

A hand-written service may be split into partial files by concern
when its parts share state, the main file holds the fields,
constructor and lifecycle, each part is Type.Part.cs and is listed in
the main file's remarks, and the class keeps one responsibility. A
partial file is never a way past the entry-point size limit, and
regions stay forbidden. On this line VolunteerRecruitmentService
already splits its 16 culture pools this way; it does not list its
parts in its remarks yet, which the ADR records as a known gap.

Ported-from: 583585818824083484989e8360fd376b04052f76

#### docs(adr): v2.0.28 - record boundary seams, conditional interfaces

`bc67b812`

ADR-007 gains an exception: a service may keep an engine call in a
protected-virtual boundary seam when the seam's signature carries
ids, value types and TAOM types only, its body is one engine
operation, the service's interface stays engine-free, and a test
subclass overrides every seam the tests reach. A seam that breaks a
condition is still a finding. ADR-008 points its static-call rule and
checklist at that exception and drops a CI grep that could not tell a
seam from a violation. ADR-002 lets a service skip its interface
unless a test fakes it or a second class implements it, and accepts a
test subclass that overrides seams. CampaignTime leaves ADR-007's list
of sealed singletons; it is a struct (v1.4.8 CampaignTime.cs:11).

Ported-from: 1cdf8eb022a7ac7dd3ad7bf2150b081a3357661d
Ported-from: 7d1b7a5422becd542cc1eeecd3effcb9e64b3d45
Ported-from: b22edd4733caf4bc58b2df38f30ee4f61f7a723a
Ported-from: 6a2ce8cb3df4b239571346b795183ddea66d8542
Ported-from: ec5a192702c08dbbdde92194a5aec8e264455799

#### docs(plans): v2.0.28 - record WP103 safety harness

`519ab5f1`

Row 103 is DONE at merge ee75c9a0f. The Integrator record line gives
the test counts, the moved hook and tool baselines, what stays with the
maintainer, and the commit matcher note left for WP116.

#### docs(claude): v2.0.28 - CLAUDE.md under its byte cap; pathspec commits

`3a005d72`

CLAUDE.md was 47,311 bytes on disk against the 46,000 byte cap the doc
gate enforces, so that gate would refuse every feature-doc commit once
it works. It is 45,962 now.

Cuts: the two Notes bullets on the target and the decompile dump, the
Haiku 3 note, the Status Line section (moved to the hooks catalog), the
PowerShell Tool section, the hook counts, the phantom version detail,
the rule loader convention line and the skill counts. Each is held by
the linked doc named at its place.

Gains: the pathspec-commit rule for shared checkouts, the commit-msg
hook and labelled merges, and a verification contract that names
non-deploying commands.

Ported-from: 385b7adf66bf333ccd6d867e8027081d30716c4b

#### docs(harness): v2.0.28 - every documented build and test skips deploy

`07911fb6`

The docs, skills, rules and agent prompts now pass both flags, because
without -p:ModuleId= the post-build copy deploys into the game install
on this line. An agent following one could overwrite the installed
TAOM.dll, or fail while the game runs.

The new tool test keeps the 1.5.x linter's folders clean and adds bare
`dotnet test` lines. Feature docs were swept once and are not gated.
docs/modding/module-taom.md describes what build.ps1 runs, so it names
the unflagged commands on purpose.

Ported-from: 1d1abd6940e7765f3d08247afa3ec68acda12d23
Ported-from: 35ff005dc5bb80870e49e46122b69ae57ac1a916

#### docs(skills): v2.0.28 - non-deploying builds in /verify and Codex

`0201006b`

Both skills built and tested without -p:ModuleId=, so each run copied
into the game install on this line (the csproj does not gate the copy
on DisableModuleCopy) and failed while Bannerlord was open. Both flags
now. /verify no longer tests a stale DLL with --no-build.

The Codex prompt's fixed blocks live in references/prompt-fixed.md, 3h
points at the 1.4.x lessons homes, and the complete-read probe names an
AGENTS.md rule that exists here.

Ported-from: 785b2703e897fe3c3ccf01b8e7d9b847074d7bd1

#### docs(plans): v2.0.28 - record WP102 rest

`a6513be1`

Row 102 is DONE at merge d247b6fdb, with the integrator record line:
tests, the hook suite count, the generator range and what stays with
the maintainer after the trunk merge.

#### docs(workflow): v2.0.28 - the commit body is the changelog entry

`47cbe2fe`

The commit body is now the changelog entry: write it for a reader of
the release note. /release generates CHANGELOG.md from commit
subjects and bodies, and nobody edits that file by hand.

AGENTS.md gains a "Documentation duty" section that states the rule
for every AI client, and CLAUDE.md replaces "CHANGELOG.md every
session". /release Phase 4 now runs tools/changelog_from_commits.py
--write, Phase 5 writes the release note from that section, Phase 6
checks that HEAD is still the commit the generator ended at, and
Phase 7 checks that the release commit's parent is that commit and
tags the release commit by SHA. release-process.md follows the same
order and records the version scheme for this line: the next release
is v2.4.0, then v2.4.1, because v2.0.29 to v2.0.36 are 1.5.x release
tags.

/verify Step 5 and the /deep-review completeness agent used to demand
a CHANGELOG edit; they now flag one. Every other instruction that told
an agent to update CHANGELOG.md now points at the commit body: the
ship, finish-branch, author-armor, verify-bindings, lint-cleanup-loop,
scope-check, new-adr, improve, commit-split and new-creature-mount
skills, five rules, the builder role, the taom-build Codex skill, the
Codex guide, the Serena checklist, README.md, completion-workflow.md,
external-repo-adoption.md, the lord-skills and new-culture guides,
docs/modding/module-taom.md, the feature-doc template and four
feature docs. Lines that name CHANGELOG as a kind of prose or record
history are left alone, as are code comments that cite a past
CHANGELOG date.

Ported-from: 30b54df759fe63e19ca5902110dbf702665b434b
Ported-from: 6e4bfedb7769bdc1d45d3cf8f90f1564851b9015
Ported-from: 971d8e97581cf6a43d7f9671eb9b19b25cb07c76
Ported-from: 5b242cee660b1675f2c3f582f91130bd9a85cd35

#### docs(plans): v2.0.28 - record D31, the protected-file lane

`9bb2231e`

The maintainer chose the session override, so the held harness lane
runs in a fixed order, each piece with a cold review that compares
protected-file diffs with the plan's text. The override file's real
name is not tied to one session; the orchestrator deletes it when the
lane ends. Nothing is pushed, tagged or merged to the trunk.

#### docs(plans): v2.0.28 - record WP136 Part B remainder

`eda393a4`

Row 136 stays PARTIAL at ae1860762: Step E3 is the maintainer's and the
translation pass and in-game checks are owed. The record line carries
the merge sha, the test figures, the unchanged validator count and the
reviewer's doc note, which now falls to WP142.

#### docs(tools): v2.0.28 - schema tool and testing guide name the live pass

`cc721795`

The tool's docstring said the SCHEMA_INVALID pass was still to land, and
the testing guide listed the engine XSD layer under what
validate_moduledata.py cannot see. Both now say the pass runs over the
repo module and that only the live modules need --live.

Not-tested: none

#### docs(plans): v2.0.28 - record WP102 Step 2

`0fc0f5d3`

Row 102 goes to PARTIAL: Step 2 is the only part landed, Steps 3 to 6
stay held by D30 rule 4. The record line carries the merge sha, the
test figures and the review's notes for the later maintainer run: the
two DIFF paths, the drift over the Step 1 item 5 paths and the commit
body's departure from the plan's draft.

#### docs(plans): v2.0.28 - record WP108 Step B4

`fe5913be`

Row 108 stays PARTIAL at 42843a4f3 because Step B5 needs the public
Armory. The record notes the validator's new provisional figure, 3771
errors (3768 BROKEN_ITEM_REF plus 3 COLLISION_BODY_BORROWED, void under
D20), and the executor adaptations the hand-off left out.

#### docs(plans): v2.0.28 - D30, work that needs no protected file

`da9cee0e`

WP108 B4, the rest of WP136 Part B and WP102 Step 2 run. The harness
lane stays held for the settings edit.

#### docs(plans): v2.0.28 - later waves, review 2, tip checks, symptom map

`11b47c2a`

Extend OVERNIGHT-REPORT.md with the merges after its first version,
integrated review 2, the checks run at 30868fc76 and the symptoms
traced through live code. PROGRESS.md points at them. Nothing here was
run in the game.

#### docs(plans): v2.0.28 - record integrated-review fixes

`30868fc7`

The ledger line for the merge of port148/integration-fixes: the test
and build results at 6ac230b05, the four reviewer notes applied at
merge, and what stays owed.

#### docs(plans): v2.0.28 - record the integration fixes

`2d4f5d43`

The ledger line for port148/integration-fixes: the test, build, tool and
snapshot results at this tip, the Release gate and the API snapshot that
were red with no record, and the 0fb873f67 trailer that stays owed.

#### docs(plans): v2.0.28 - programme state and the warning series

`4cae5c3c`

README's state line still held 115, 121, 122 and 127 onward, and
PROGRESS.md stopped at nineteen WPs; both now point at the rows and name
what is really held. The warning counts are two series, with and without
MSB3270 lines, so WP104's rise of 107 is a fall of 7 and WP127's fall
from 2476 is a rise of 37. What switches MSB3270 on is not established.
The check_rdc_entries owed row is done, plan 135's in-game note is
corrected, and the classified 1.5.x range is linked.

#### docs(engine): v2.0.28 - co-op session flag and the mission clock

`38a9451f`

Two facts from classifying 1.5.x 4d1a9105a..974e60bb2, hand-written for
this line and re-checked on the installed v1.4.8.
GameNetwork.IsSessionActive reads false in a co-op battle, and the
mission clock has three multipliers in MissionState.TickMission.
delta-974e60bb2.md records the classification: 45 commits, 37 non-merge
and 8 merges, no code port.

Research: v1.4.8 GameNetwork.cs:317-339 and :381-391,
  MissionState.cs:155-196; BannerlordCoop source at 4ad976c8c has no
  CurrentGameType

#### docs: v2.0.28 - correct stale notes the port programme left behind

`fb3e560e`

Plain corrections, no runtime change. Merged work no longer reads as
future (WP107, WP117, WP118, WP137, WP139). Text naming a neighbour,
file or test that is gone is corrected (Patch35_Mission_OnTick,
AbilityHUD.xml, AgentColorStoreCleanupBehavior,
CreatureBanditsWiringTests, orientation.md, the character reason in
SidePairScanTests, two WP132 citations).

Text claiming what is not on this line now says so: WP108 Step B4's
gates in three docs, and the skill sections of the deferred .claude
hunks for WP115, WP137 and WP139, as "once those edits land". Also the
Custom Battle Sauron paragraph (#788), the graphify exit codes as
measured here, and "every id translated" in localization-override.md,
which was only ever every id has a row.

Numbers re-measured: module-taom.md's tree and citations, four stale
prefab budget texts, CLAUDE.md's doc-lookup row count (85), and what the
Mixed Formations frame allocates (Enum.TryParse, about 112 bytes a
call). Sauron's charge row is explained at his real weight, 80 in Armory
v2.0.26: a Native horse and rider is 6.0, so Branch A floors him and the
row never applies. The ported threshold stays. The VanillaTuning licence
rows now name their conflict; the upstream file is not on this machine.

#### docs(api-snapshot): v2.0.28 - regenerate after two deletions

`aa8aa2a0`

WP145 Parts A to D deleted ClanPartyItemVM_GetCharacterCode_Patch and
TaomHeroCreationModel after WP137 Part D regenerated the snapshot, so
`snapshot_api_surface.ps1 -Check` has exited 1 at the tip with no record
of it. Those were its only drift lines, and D29 had left Step G6 out.
The maintainer's ruling for this run covers the step.

Ported-from: 333e7490784f2fd17d81f4b61a8a62e1977a659b

#### docs(plans): v2.0.28 - record WP145 Parts A to D

`d3a60dfb`

Row 145 reads PARTIAL at merge b84c3011a. The integrator record carries
the test counts, the two catalogue edits applied at merge, the Step C1
misprediction, and what stays deferred under D29.

#### docs(plans): v2.0.28 - D29, WP145 Parts A to D run now

`8b16882d`

The plan lands each part independently and their prerequisites have
merged. Parts E, F and G stay held on the protected files.

#### docs(plans): v2.0.28 - record WP122 test-infrastructure-gates

`6308ac19`

Row 122 reads PARTIAL at merge d0b279254: 13411 tests, 13403 passed,
8 skipped, 0 failed. C2 to C5 and Part D stay deferred.

#### docs(plans): v2.0.28 - D28, WP122 GA-SAME ruling

`118c34f8`

One comment line drifted because eb12746d1 removed a deleted type name.
Waive the token for that line and apply the series diff at A1.2.

#### docs(plans): v2.0.28 - record WP137 Part D

`eab57d08`

Row 137 stays PARTIAL: Part D merged as c7c3b3c59, but the engine-bump
and /verify .claude hunks wait for WP103. Tests 13355 total, 13347
passed, 8 skipped, 0 failed.

#### docs(verify): v2.0.28 - .ai verification runs the API snapshot check

`922a08ce`

Now that -Check exits 0 on the committed snapshot, the api-snapshot row
in .ai/verification.md runs it, so a patch target or GameModel base that
changes without a regenerated snapshot shows as DRIFT instead of being
missed by engine_body_diff.py at the next engine bump.

The /verify Step 2d and output line of .claude/skills/verify/SKILL.md
are deferred until WP103, with the other .claude hunks of plan 137.

Research: -Check exits 0 on the regenerated snapshot; the script prefers TAOM.Tests/bin/Debug/net472
Not-tested: none (docs)
Ported-from: 0002cb5614e52fbdd638b402d302613f04678cf5

#### docs(bindings): v2.0.28 - manual patch header, reflection catalogue

`6207054f`

The ManualPatchApplicator header no longer claims every manual patch
fails safe: only a missing target does, and an exception from
harmony.Patch propagates to the caller. The PathReuseCache catalogue
move was not needed, since WP127 already deleted that row with its
scaffold.

Research: verify report D4 G4, G5
Not-tested: none (comment and catalogue)

#### docs(plans): v2.0.28 - record WP139 in-game-verdict-programme

`afffa34e`

Row 139 is PARTIAL: Step D1 stays deferred under D25 rule 4 and D27
rule 5. Tests 13355 total, 13347 passed, 8 skipped, 0 failed.

#### docs(plans): v2.0.28 - record WP140 gui-prefab-gates

`0b49f68c`

Row 140 is PARTIAL at merge eb5aadd9e. Part D waits on WP115 B1, and
the gui-sprite-system.md AbilityHUD clean-up goes to the docs sync.

#### docs(engine): v2.0.28 - never spawn an agent inside AfterStart

`09f4d2cd`

The mission lifecycle doc now explains why an agent spawned inside
Mission.AfterStart is unsafe on v1.4.8. A spawn hook that adds a
behaviour changes the list the foreach is walking, the enumerator
throws, and because State.Continuing is set only on the method's last
line, the next tick loads the same mission object again. Spawn on the
first mission tick.

Research: Mission.AfterStart, AddMissionBehavior,
MissionState.TickLoading and FinishMissionLoading (v1.4.8 dump)
Ported-from: e6dbea3e028e4a296815de81258741c7f9dea734

#### docs(plans): v2.0.28 - record WP115 release-integrity

`7d730304`

Row 115 is PARTIAL at ecddc3c72; the integrator record names what
merged, the test line and what stays deferred (B1, B2, B3, C5, M2, M3).

#### docs(provenance): v2.0.28 - notices for NuGet libraries and HoN code

`d6fd0234`

The build writes DryIoc.dll, Newtonsoft.Json.dll and
System.Runtime.CompilerServices.Unsafe.dll into every TAOM bin folder
and the laptop's TAOM.zip ships all three, so the MIT notices were owed.
THIRD-PARTY-LICENSES.txt gains a RUNTIME LIBRARIES section holding the
MIT text once, and VanillaTuning now points at it. It also gains an HoN
section: on 2026-10-09 the maintainer reported that yotthani put his
code under MIT, which covers siege dismount, mixed formations, smart
cavalry AI, fief management, quick actions, equipment presets and
companion tactics. The provenance register moves the drop and
TransferbuttonMenu rows to MIT and cleared, and adds DryIoc and Json.NET
rows. MinHook and NativeSkinFixes stay as they are: 1.4.x ships both.
A new test ties the notices to the csproj's package versions. Port of
#736.

Ported-from: 9c310bc238fa1441f4a1344c5cc3fe5dccadb6a2
Ported-from: eed20591a15ac41ce66f744b98561961528cfdbb

#### docs(plans): v2.0.28 - record WP136 data-integrity-gates

`f767f035`

Row 136 is PARTIAL at 1a8153919. The integrator line gives the test
counts and what was deferred, and "Owed after the programme" gains the
check_rdc_entries verdict follow-up from the WP136 review.

#### docs(plans): v2.0.28 - record WP121 Part A

`c621250f`

Row 121 is PARTIAL: Part A merged at 21800aa91, 13055 tests, 13049
passed, 6 skipped, 0 failed. Parts B, C, D and E are not in this merge.

#### docs(plans): v2.0.28 - record WP130 Step F3

`bf3e8c06`

Row 130 moves to DONE at merge 48b610da0 and the Integrator record gains
the merge line. The plan's follow-ups and in-game checks stay owed.

#### docs(modding): v2.0.28 - prefab budget row says it sums every module

`a4817fe2`

check_prefab_budget.py has summed every module since the earlier WP136
commit, so the two rows in the validation guide that said it counts
TAOM_Map/Prefabs alone were wrong. They now give what it sums, its exit
codes, and why a machine without NavalDLC reads lower.

#### docs(plans): v2.0.28 - D27, the delegated run

`8fc0bdf9`

Records the rulings for WP115, 121, 122, 130 F3, 136, 137 Part D, 139
and 140. The protected files stay protected.

#### docs(plans): v2.0.28 - record WP144

`5c24791b`

Row 144 marked DONE and the merge recorded under Integrator record.

#### docs(plans): v2.0.28 - record WP135

`87fa7633`

Row 135 PARTIAL and its integrator record line: merge d2c2fdcf2, tests
13047 total, 13041 passed, 6 skipped, 0 failed.

#### docs(harness): v2.0.28 - traps: AfterStart reload, listener order

`2accaed3`

The Horse-skeleton reskins row now says the act_horse_strike clips are
hit reactions, since the being-struck band stops at 51 on v1.4.8. Two
new trap rows: a throw in Mission.AfterStart reloads the mission every
tick, and campaign event listeners run newest first. The Kit-facts row
stays as it was, because the base's skeleton doc has no VERBATIM fact.
CLAUDE.md bytes: BEFORE 46703, AFTER 47312. It was already over the
46,000 byte budget, so the plan keeps the full rows; WP143 owns the
trim.

Research: taom-src v1.4.8 MissionState.cs:333-352, MbEvent.cs:24-30,
Agent.cs:1338-1348
Not-tested: docs only
Ported-from: 4520af802d9c31f44263c7a9ce4a263e611d0c5d
Ported-from: b5e1d0b3d679d539f2752b7fb8a11bfef6d06203
Ported-from: ac1d8edbc82f01af77812972d14b980259108bf3

#### docs(reviews): v2.0.28 - engine lessons re-cited for v1.4.8

`953102ee`

Carries the 1.5.x engine lessons that hold on this line, re-cited to
v1.4.8 and with their RCAs named rather than linked, since those files
are not here: one-way destructible state, hidden siege gates (hedged,
in-game check 3), a pin that does not stop a party's initiative, the
Gauntlet colour order and modal root, fakes that hide a seam, bench
wins, single-session figures, unchecked test levers, closed generic
Harmony patches, doc batches written beside code, and the action index
guard. The ConditionalWeakTable lesson gains its per-lookup lock cost.

Research: taom-src v1.4.8 DestructableComponent.cs:399-412,
TeamAISiegeComponent.cs:54-57, :184, CastleGate.cs:609-622,
BehaviorAssaultWalls.cs:122, MobilePartyAi.cs:477-488, Color.cs:180-191;
dump EventManager.cs:857-877, GauntletLayer.cs:232
Not-tested: docs only
Ported-from: db8f7c10e7772612fbe20563b75b9e36c3d8e7b7
Ported-from: 985b0d54e1672ede8d9dc08ed986d0f3870aa5d3
Ported-from: b54179389de2cad5faf6bcd4d149305330d6afd3
Ported-from: 44058ce4b1c4257fecbcd7e6945df2392701b17f
Ported-from: 42e48bbe5a982d17b05a80ad51e9cb533cfe6a89
Ported-from: b5e1d0b3d679d539f2752b7fb8a11bfef6d06203
Ported-from: 4d79eca85d7c383326577debd5f8fe3e856a9173
Ported-from: 2b8f5ff365e0821d2280b95042aa4fe54819a5dd
Ported-from: 2ac85fbe777a3cdc1513ba76402b8563fcea3770

#### docs(mithrilforge): v2.0.28 - prop recipe, gate the camp tpacs

`e17d56f1`

Adds MithrilForge's recipe for a static prop in a tpac without the
Kit, with the package layout facts re-measured on v1.4.8 where the
vanilla figures allow. Whether the 1.4.8 build loads TAOM's four camp
packages is recorded as UNVERIFIED, not as the 1.5.x line's "none of it
renders". A new tool test keeps the code's PlaceCenteredPrefab meshes
and the packages in step, and tools/tpac_clone_metamesh.py comes across
because that test needs its parser. The toolchain doc gains the loose
tree corollary, and the provenance register records the MithrilForge
facts and the ButterLib measurement.

Research: v1.4.8 map_icon_parts.tpac and meshes_shared_7 re-measured
with tpac_clone_metamesh.parse; the four camp packages and their
PlaceCenteredPrefab call sites
Not-tested: whether the camps render on 1.4.8 (in-game check 1)
Ported-from: 991f415f41340823f63464c9d8692e5c7079bfc6
Ported-from: e1a940ac9c2fcaa6dd573b982409ad4e8cf17344
Ported-from: 1325c36ea8d14bcba9dc9d0a3f6f5087c63833a9

#### docs(engine): v2.0.28 - render, memory and animation leads, tagged

`6eb90c08`

Carries yotthani's render, memory and animation findings as leads for
1.4.8. The agent spawn page gains per-frame pose, LOD and shadow notes;
the GauntletUI page gains the map-view GPU leak, generated prefabs and
first-use shader compile bullets; the frame costs page gains two agent
capacity observations; the clip flags page gains the rope flag
companion clip rule, with v1.4.8 weapon counts. Each figure keeps its
source and version and says it was not re-measured on v1.4.8. The
skeleton authoring fact and its troubleshooting row are skipped: the
Kit facts were not re-measured on the v1.4.8 Kit.

Research: dump v1.4.8 Skeleton, MBAgentVisuals, GameEntity and Agent
members; native string search of TaleWorlds.Native.dll; modules dump
SandBox__SandBox.View.cs:2275, :13931 and
Native__TaleWorlds.MountAndBlade.View.cs:4765; installed SandBoxCore
items/weapons.xml
Not-tested: docs only; in-game check 5
Ported-from: b5e1d0b3d679d539f2752b7fb8a11bfef6d06203
Ported-from: 7eac30153353ae401ef37c0955a29e92bb5d4b25
Ported-from: 2b8f5ff365e0821d2280b95042aa4fe54819a5dd
Ported-from: 2ac85fbe777a3cdc1513ba76402b8563fcea3770
Ported-from: 8dd76ddb376425da6a167415186f1f4a40781355
Ported-from: 0bb1f3caea18a0f65ea6bfb348b3b087e5579a30

#### docs(battle): v2.0.28 - team AI tactic layer and morale on v1.4.8

`8e90458d`

The formations page gains the tactic layer and morale constants, each
row re-read on the installed v1.4.8. It also names the 10-slot formation
list a scan of enemy formations must use, since generals and bodyguard
formations fill in an ordinary field battle. One 1.5.x claim is
corrected: vanilla does subscribe GetAgentTroopClass_Override, in
hideouts and alley fights. The break cascade stays yotthani's v1.5.5
measurement and is tagged as not re-measured.

The combat-mechanics doc records that DecideCrushedThrough may run off
the main thread, and that the crush roll's shared MBRandom draw stays
under decision D16.

Research: taom-src v1.4.8 Team.cs, Formation.cs, TeamAIGeneral.cs,
MissionCombatantsLogic.cs, TeamAIComponent.cs, FormationAI.cs,
TacticalPosition.cs, SandboxBattleMoraleModel.cs, Mission.cs:6454-6456;
dump AgentMoraleInteractionLogic.cs, CustomBattleMoraleModel.cs,
MissionCombatMechanicsHelper.cs:311
Not-tested: docs only
Ported-from: 578770e03e369ffaba01923810ac94fb7b5c20a2
Ported-from: 5dfa8f4cb0a9e86118ed41b0c6bdd3d9a977990b
Ported-from: d690ff8d2133f2a2a4a2f79989ef7a4fd0195413
Ported-from: b5e1d0b3d679d539f2752b7fb8a11bfef6d06203

#### docs(engine): v2.0.28 - save cost, campaign map costs, listener order

`73588ea9`

The save page gains yotthani's save-cost measurements, tagged as 1.5.3
figures and not the 1.4.x cost: the 1.4.8 player saves spent 34 to
1,034 s in Containers alone. The mechanisms are confirmed on v1.4.8,
and the page says server GC is not advised on this line. The TextObject
hash bullet is rewritten with V01's correction. The campaign tick page
gains the campaign-map cost section, also tagged, and a note that
campaign event listeners run newest first.

Research: dump v1.4.8 Game.cs:216-224; SaveContext.cs:137, :157, :372,
:398; TextObject.cs:64-76, :164-168, :264-267; taom-src MbEvent.cs:24-44
Not-tested: docs only
Ported-from: b5e1d0b3d679d539f2752b7fb8a11bfef6d06203
Ported-from: 7eac30153353ae401ef37c0955a29e92bb5d4b25
Ported-from: 2ac85fbe777a3cdc1513ba76402b8563fcea3770
Ported-from: 0bb1f3caea18a0f65ea6bfb348b3b087e5579a30
Ported-from: ac1d8edbc82f01af77812972d14b980259108bf3

#### docs(engine): v2.0.28 - AfterStart reload, removal in tick, menu hook

`52fd7ccf`

The lifecycle page gains two sections re-read on v1.4.8. A throw in
Mission.AfterStart reloads the mission every tick, because
FinishMissionLoading clears _missionInitializing first; on this line
SaveShield's finalizer swallows the throw, which reads as an endless
loading screen. Removing a behaviour from inside OnMissionTick can run
another behaviour's tick twice in one frame. The lifecycle order now
says OnBehaviorInitialize and OnAfterMissionCreated never fire for a
behaviour added from a submodule.

The submodule page records that a throw from
OnBeforeInitialModuleScreenSetAsRoot repeats every tick, what
MainMenuStep contains, and yotthani's Harmony note on closed generic
patch methods. shader-precompilation.md lists the guard's in-tick
FinishDeployment as an open fix item for a later issue.

Research: taom-src v1.4.8 MissionState.cs:221-243, :263, :333-352;
Mission.cs:3740-3744, :3799-3835, :4643-4660;
DeploymentMissionController.cs:41, :80, :162, :183;
Module.cs:498-511, :750-758
Not-tested: docs only; the shader-precompilation item is a fix for a
later issue
Ported-from: b5e1d0b3d679d539f2752b7fb8a11bfef6d06203
Ported-from: 4d79eca85d7c383326577debd5f8fe3e856a9173
Ported-from: 0bb1f3caea18a0f65ea6bfb348b3b087e5579a30
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7

#### docs(races): v2.0.28 - dwarf face crash decode, face-morph reference

`6e9268f6`

arena.md blamed the female dwarf crash on the underwear mesh name. The
faulting address decodes to the static face morph over a face part
with no morph channels (#385); the underwear name was a second, real
defect, and the 2026-08-11 stopgap is what stopped the crashes. arena.md
and the Armory snapshot README now say so, and a new reference page
covers race face and hand morphs. Its engine facts were read on v1.5.3
and v1.4.7 and are tagged as not re-derived on v1.4.8.

Research: native string search of v1.4.8 TaleWorlds.Native.dll; laptop
Armory skins.xml woman meshes
Not-tested: docs only
Ported-from: 0934f65d2f848fb06fcff51ceead0ed1764abc82

#### docs(toolchain): v2.0.28 - three runtime hosts, elephant skeleton tpac

`4b78d705`

The toolchain doc and the docs index still said managed code calls back
into Mono. v1.4.8 ships three hosts (V01 and V05 of the port audit:
Bannerlord.exe carries a CLR header, and a player log recorded
CLR: 4.0.30319.42000), so the text now names managed code whichever
runtime hosts it. The doc also named a 416-byte stub as the carrier of
elephant_skeleton; adod_elephant_geo.tpac carries it, per the 1.5.x
line's desktop check, which is owed again here as in-game check 2.

Research: none on the engine beyond WP117's
Not-tested: docs only
Ported-from: 8af881f2bd639ee4a9044b665c19ea899a3d0f4e

#### docs(herorace): v2.0.28 - #635 CTD triage, voice-over doc corrected

`da052e0f`

Records the open map-conversation CTD with a voiced elf speaker: it is
a fifth raw custom-race render path, no root cause is proven, and the
next step is a dump. The engine page gains the map conversation and
encyclopedia lifecycle, with lines re-anchored to the laptop's v1.4.8
modules dump.

kingdom-voices.md said no TAOM culture can match a voice file. That is
wrong on v1.4.8: an empty accent builds a regex matching any gendered
vanilla voice, so TAOM characters speak vanilla lines with lip-sync on
their heads. Two lessons carry the triage method and the correction.

Research: taom-src v1.4.8 DefaultVoiceOverModel.cs:90; modules dump
SandBox__SandBox.View.cs and SandBox__SandBox.GauntletUI.cs lines as
cited in the engine page's Evidence list
Not-tested: docs only; #635 still needs a dump
Ported-from: aed9dea8d8bf2486ec011730adf45fdeabf68991
Ported-from: 0bb1f3caea18a0f65ea6bfb348b3b087e5579a30

#### docs(armour): v2.0.28 - helmet hair and beard cover reference

`b2a52a0f`

Adds a reference for what each hair_cover_type and beard_cover_type
value does on a helmet, with the two figures from the community Items
page. The engine lines are v1.4.8's: with no head item and an empty
Body slot the engine uses Type4 for the hair, a rule the 1.5.x text
left out. The distribution table and the race sentence were measured
on the installed Armory v2.0.26, so both are owed a re-measure on the
public build. The beard ledger the 1.5.x page linked is not on this
line, so edits are recorded in the commit body.

Research: taom-src v1.4.8 ArmorComponent.cs:190-191,
Equipment.cs:83-101;
installed Modules/Native/ModuleData/skins.xml
Not-tested: docs only
Ported-from: 75b7b6661d7f2e3193383b58c26ac33ccbcd41cf

#### docs(arena): v2.0.28 - vanilla tournament armour, six roster callers

`d39e9ae0`

Two arena docs and the NPC handbook said vanilla dresses every
tournament fighter in the host culture's kit. On v1.4.8 it does that
only in arena practice fights; in a tournament match each fighter keeps
his own armour. TAOM's override drops that mode check, so it fixes
clipping in practice fights and in matches swaps each fighter's armour
for his culture's practice kit. The docs now say so.

The Patch69 registry entry and class comment counted three and four
callers of GetParticipantCharacters; v1.4.8 has six, and the
substitution also reaches off-screen tournaments and the
TournamentFinished list. The class comment now states its claim and
points to the registry. Two lessons record both errors.

Research: taom-src v1.4.8 DefaultTournamentModel.cs:86-93; dump
TournamentBehavior.cs:105, :115, :159, :166-167;
FightTournamentGame.cs:78-88, :332;
TournamentManager.cs:140-157, :180-184;
HeroKnownInformationCampaignBehavior.cs:51;
TournamentFightMissionController.cs:353, :358
Not-tested: comment and docs only; in-game check 7 confirms the
described look
Ported-from: fe6619615844aa86aa1ae307ea824a3278898f2a
Ported-from: 2b6f6216c7e3d24120e019ace44df73213237403
Ported-from: 75b7b6661d7f2e3193383b58c26ac33ccbcd41cf

#### docs(lords): v2.0.28 - lords in both files merge, not override

`c049cd56`

The lord docs and the lord-skills skill said a lord present in both
lords.xslt and characters/lords.xml uses only the last-loaded copy. On
v1.4.8 the two are merged: lords.xml wins each attribute and skill id
it redeclares, the XSLT copy supplies the rest, and differing equipment
sets are added, so the hero picks one at random per campaign. A new
lesson records this and asks authors to grep both files first.

Research: taom-src v1.4.8 MBObjectManager.cs:820-870, Hero.cs:2237,
:2274, :2278; installed XmlSchemas/NPCCharacters.xsd:369-372, :410-415
Not-tested: docs only
Ported-from: 369eb030696718e961e7fc0fa1a4c438be4a5a1c
Ported-from: 9804f67b9deba24813da1f2ad7ef2d5f9ad6fa14

#### docs(creatures): v2.0.28 - being-struck band is 48 to 51, not 52

`2bf12daf`

Agent.IsInBeingStruckAction tests MBMath.IsBetween(type, 48, 52), and
that test is half-open, so actt_mount_strike (52) is not read as being
struck on v1.4.8. The war ram doc, the authoring guide, three community
creature guides, the modding items and recipe pages, one lesson, the
new-creature-mount skill and the WarRam config comment all said it was.

The war ram's rejected strike clips were still the wrong choice, for a
different reason: act_horse_strike_front and _back play the horse's hit
reactions (horse_hit_from_front and _back), so the ram would flinch
while it dealt damage. The texts now give that reason. The CLAUDE.md
row follows in a later commit of this plan.

Research: taom-src v1.4.8 MBMath.cs:295-302, Agent.cs:1338-1348,
ActionCodeType at Agent.cs:292 and :311-319
Not-tested: comment and docs only
Ported-from: 4520af802d9c31f44263c7a9ce4a263e611d0c5d

#### docs(plans): v2.0.28 - record WP141

`424646b1`

Row 141 PARTIAL at merge 8b5a21e93; integrator record line added,
with the WP135 merge note for the translator conflict.

#### docs(plans): v2.0.28 - record WP132

`e3a34b65`

Row 132 is DONE at merge 1266ab72d: tests 12976 total, 12970 passed,
6 skipped, 0 failed.

#### docs(tools): v2.0.28 - tpac checksum formula, extract faults, MCP keys

`b6011bb0`

The warg ledger and creature authoring row 7 now record the item
checksum (xxHash64, seed 0, over the int64 metadata length plus the
metadata), so
a metadata insert from files no longer reads as impossible; it needs the
checksum recomputed. The deprecated tpac_skeleton_extract.py page names
the two real header faults: the skeleton's item GUID reused as the
package GUID, and a TOC size of 0. The MCP page notes that project
servers are keyed by the folder's spelling, case included.

No dedicated server text (D7).

Research: extract header read at tpac_skeleton_extract.py:94; the
TOC-size fact at lotrlome-warg-changes.md section 13.
Ported-from: 426f0dc88f33fd0e0778c455f67d7c81d3b74324
Ported-from: 72cad16b105d4f752558ac1b7f004b2cfc92e285
Ported-from: 0daece880b1e2bf174c5878e165868d9d4826700

#### docs(map): v2.0.28 - map naming docs describe the safer renamer

`aa0120be`

The map naming, settlements and module-map pages now say what
Apply-MapVillageNames.py does: dry by default, only real renames, cache
eviction, cache and loc files before the master, a refused taken name.
module-map also corrects when a translated name changes: at the next
game start or a language switch, not at a save load.

Research: taom-src TaleWorlds.Localization.MBTextManager, ChangeLanguage
at 61 calls LocalizedTextManager.LoadLanguage at 68.
Ported-from: af2b198e45432233e696ce17c6898fe73101c9b1
Ported-from: 244c15d2779dec00f2e80d4e23a98387448e49df
Ported-from: 87ffea9b2fbc0d96f2e43b9173d2f8d8a22ab28b

#### docs(plans): v2.0.28 - record WP130

`c77a33e2`

Row 130 is PARTIAL and the integrator record carries the merge, the
test counts, the Sauron ruling owed and the deferred Step F3.

#### docs(plans): v2.0.28 - record WP137

`730a58fb`

#### docs(enlistment): v2.0.28 - siege duty, wage order, dead battle rule

`5647a4f2`

The enlistment feature doc records the shared commander fitness
check (#1016), siege duties for defenders and the wage order (#1014),
and the removal of the battle rule that never ran (#796).

Ported-from: 5e7a0da6ef91d512a90dc9764f431c3d1b1534a7
Ported-from: b0625f0d18e6d7a67efc70fd16f23a321fa91133
Ported-from: 3b848c4111db58b86be10f126b643839ed8cb2b6

#### docs(warg): v2.0.28 - WargConfig table matches the code (#615)

`19b1378a`

The WargConfig table in docs/features/warg-combat.md had drifted from
WargConfig.cs: SpeedForMaxDamage is 20, not 8, DamageToFall 40, not 20,
the rage roll distance 20, not 10, and TargetDetectionRange was
missing. The table now matches Main/Features/Warg/WargConfig.cs.

Not-tested: docs only
Ported-from: f54581d6c2b41221f57184e8a53091d4e1739259

#### docs(enlistment): v2.0.28 - sit-out, board pay and pump clock

`87641862`

The enlistment feature doc records the sit-out rule (#830), the
board's daily pay (#839) and the service pump clock (#841).

Ported-from: 1d9cc42f1bf13a49a9c340182cc77dc9ec44817c
Ported-from: 4666a254a1a0f4c9020110e4c6ff3a3216628ef4
Ported-from: 069962aa43773a2201f8ec358f4c0c9b40f046e9

#### docs(plans): v2.0.28 - record WP133

`59cbad2f`

Row 133 is PARTIAL at merge 4b07389c7; the integrator record holds the
test line, the conflicts resolved and the deferred steps.

#### docs(enlistment): v2.0.28 - quartermaster answers and kit-tier names

`d7d785ac`

The enlistment feature doc records the quartermaster's answers (#829)
and the kit-tier renames with their frozen save key (#936).

Ported-from: 1a132c727cf473f6010bd5fc29e36f5a99589c54
Ported-from: 5c5072dbc2f5dc50189ddb04c3df663bc2b99839
Ported-from: 5a8c73d26018d0e795a1efeccb4cf1057061f518

#### docs(dread): v2.0.28 - correct the rout chain from the v1.4.8 engine

`8a839fbe`

The Aura of Dread doc now describes how a routed troop really leaves a
party, read from the v1.4.8 engine: only a panicked troop is lost; a
troop that leaves on a retreat order keeps its place but still counts
as a casualty; deserters come only from the defeated side; and siege
defenders are exempt only in a siege assault, not in a sally-out. It
also corrects three engine claims: the 30% formation "Flee" value
changes no behaviour (no managed code reads it), only a direct Retreat()
bypasses the panic handler's bracket, and a direct Panic() skips the
morale model's veto and the siege-ladder rule. Every engine line the
1.5.x text read on v1.5.5 is re-cited to v1.4.8, and the fear effects of
features 1.4.x does not have are dropped.

Research: taom-src v1.4.8 MapEventSide.OnTroopRouted,
  MapEventParty.OnTroopRouted, PartyGroupAgentOrigin.SetRouted,
  CommonAIComponent, CharacterObject.GetMoraleResistance; dump
  BattleAgentLogic, BattleEndLogic, PartyGroupTroopSupplier, Formation,
  DesertersCampaignBehavior
Not-tested: docs and a test comment only
Ported-from: f4f12a737c7e020be154ba857b42bcb871e65d96

#### docs(plans): v2.0.28 - record WP134

`9812bf37`

Row 134 is PARTIAL: the merge cf1bc73c3 landed, and the item steps
A2 (troop weights), C3 and C4 wait for the public Armory.

#### docs(plans): v2.0.28 - record WP128

`87d9a6f8`

Records the WP128 merge 67f9b9aaf: 12616 tests, 12610 passed, 6
skipped, 0 failed.

#### docs(tools): v2.0.28 - research and review read native code as C

`8af70273`

The research guide, the review reference, the taom-research workflow and
the lookup tables now send a trail that ends at an [EngineMethod] or in
natively parsed data to native_decompile.py, and a native claim with no
decompile behind it is reported UNVERIFIED. The engine-and-toolchain page
records the v1.4.8 engine-method numbers.

The same text for /research, /investigate, /deep-review, the three agents,
the agent manual and mcp-servers.md waits for WP103 (D25 rule 4). The
CLAUDE.md and AGENTS.md lines are left to the rules sync (WP143).

Research: v1.4.8 registration functions and counts from the engine-method map
Not-tested: none (docs)
Ported-from: d51e430287cf0d063bd07f006aea7c1cd1fc67a2
Ported-from: fc5408508687c9f813123b6b1db006dd6ab0cee7

#### docs(gui): v2.0.28 - collection advice matches the ownership rule

`8ac7ab81`

The GUI sprite guide still told modders never to add items to a
vanilla collection such as the map bar's SecondaryInfoItems. The GUI
rule has since replaced that with "discouraged, on ownership grounds".
The guide now says the same: prefer bound properties and a prefab
injection, and if an item must go into such a list, add it on every
refresh behind a Contains check, never skip the first refresh, and
remove it when it no longer applies, as the special resource entry
does.

Ported-from: 8e7d1f4cbe4e4d61e7c5f7c4fbe7b27648668cf9

#### docs(plans): v2.0.28 - record WP129

`67c3ff6b`

Row 129 is DONE and the Integrator record carries the merge sha, the
test counts, the moved baseline and what was deferred.

#### docs(enlistment): v2.0.28 - the batch's in-game checks and port notes

`0b093190`

The enlistment feature doc records the #842 to #845 batch, points at
the owed in-game checks and names the new duty string tool.

Ported-from: f21123c6aed80344e47a52f5468195a0c3561997
Ported-from: 388e89de080e043bd1b6b18665fed74a10b5e5dd
Ported-from: 218fa405376cb3072cc82c62667214165f41295c
Ported-from: a20b4d1540b782bde877b0333070b55aa38ea402
Ported-from: 4bd8554a4888fc709b12e7894527848708632379

#### docs(plans): v2.0.28 - record WP131

`578dd15d`

Row 131 is DONE on the overnight branch, with the merge sha and the
test counts under Integrator record.

#### docs: v2.0.28 - inventory docs catch up with the fixes

`36583f2c`

No change in game. The Equipment Presets, Quick Actions and Banner
Color Persistence docs and the patch registry now match the inventory
fixes. Two wrong statements are corrected: the inventory label refresh
fires once, when the screen is built (v1.4.8 SPInventoryVM.cs:3163),
not on every transaction; and a renamed engine method does not make
Harmony skip a patch silently, it stops the whole patch category,
which PatchCategoryApplier logs.

Research: taom-src v1.4.8 SPInventoryVM constructor :3163
Ported-from: 399a7fe277a8874f20b2bb526caba9571818d4d4

#### docs(plans): v2.0.28 - record WP127

`481e8cd3`

Row 127 is PARTIAL: Parts A and B landed, integrator record line added.
Part C waits on the maintainer's Part M ADR commits.

#### docs(enlistment): v2.0.28 - wars, merit, grace promotion, exits

`927ef780`

The enlistment feature doc records the Part B fixes: join-time wars
(#832), vassal sit-out (#960), merit (#834), grace (#835), exits (#1021).

Ported-from: 1559b3261c2087149f7811bebeca8d7d7c7fc309
Ported-from: d4af27d3e464b51eb50de410e28fe081643a5db9
Ported-from: 9304a4cb941d577cd4b2d5810af13529e313ebf6
Ported-from: 75e190abe685cba1b403af8847cb4da9d0b944c0
Ported-from: 90035a799207619c134c1fe84d495fd8cde6d50b

#### docs(careers): v2.0.28 - RCA and resource docs for the career fixes

`8e16ec05`

The special resources feature doc now describes the scaled earnings,
the single upkeep computation, the raid and extortion payout rule and
the once-only hideout payout. The review record of the career fixes
(#766 to #790) is added with a note on what the 1.4.x port
re-checked. This line's own party-screen handler fix for #771 keeps
its existing doc section.

Ported-from: c96a02a4bfea3960760ebbde49567958b43989ee
Ported-from: 86ef5c7af1137910c4f198d44716476890772ef3
Ported-from: 4f5b412d7dd1ad126410466bb8a35ef2cf916ea7

#### docs(rules): v2.0.28 - align the architecture rules with the code

`f5aeb833`

The architecture rules now describe the code this line already has,
so a review stops flagging a pattern the services rely on.

- A service may keep an engine call in a protected-virtual boundary
  seam that a test subclass overrides (RefugeService, CampService,
  SupplyOrderService and WardenService do); a seam that carries
  decision logic, or engine use outside a seam, is still a finding.
- A service gets an interface only when a test fakes it or a second
  class implements it; every adapter keeps one.
- A patch calls its service directly; an IOnXxx hook interface sits
  between them only when the patch needs a narrow seam or a test
  fake.

Updated: the review reference, the C# architecture, patterns, Harmony
and think-before-coding rules, the feature-builder agent, the
new-feature, codex-verify, improve and deep-review skills (the
standards checks the 1.5.x lens file carries), the architecture,
patterns and decompiled-code guides, and the lifecycle engine doc.
The ADR text these lines cite
(ADR-002, ADR-007, ADR-008) is the maintainer's commit on this branch.

Ported-from: 9c29d73242e05b33428970ae54bd439dba099b26
Ported-from: d4e6273a198f576cd91f938de6401cc043cb24cf
Ported-from: e809f258c0cc104254c27a27022de0eb0f46deac
Ported-from: 6a2ce8cb3df4b239571346b795183ddea66d8542

#### docs(enlistment): v2.0.28 - battle exits, discharged menu and raids

`0e2f1fa4`

The enlistment feature doc records the four fixes ported from the
1.5.x line: hidden battle exits (#827), the discharged-menu exit
(#828), raids left to the column (#831) and relief lords (#954).

Ported-from: 2d1e5f3385f871c5a829611641230266b87fb763
Ported-from: b24e8549e88e8b0b465bc1615f4335855a3486d9
Ported-from: 7a912dcd544c1c3560b52de2df571f406131a782
Ported-from: c04ba3fe58a39c2538dbb5a0708393e63d21b1e9

#### docs(plans): v2.0.28 - D26, the in-game test moves to the end

`b18e9c8b`

Make 127 to 137, 139, 141 and 144 runnable on the overnight branch. Data
and .claude steps that need the public Armory, WP102 or WP103 stay deferred,
and no paid translation runs.

#### docs(plans): v2.0.28 - overnight report and review doc corrections

`38bedf6f`

Add the per-WP report with deferred items and owed in-game checks, and
correct the doc-only findings from the integrated review: the old plan
003 row, the co-op excluded count, the profiler category count and three
citations of files that exist only on the 1.5.x line.

#### docs(plans): v2.0.28 - record WP125

`659671d9`

Row 125 done, integrator record line added.

#### docs(plans): v2.0.28 - record WP119

`41228b09`

Row 119 and the old plan 001 row closed, integrator record line added.

#### docs(plans): v2.0.28 - record WP124

`f54e9b52`

Row 124 set to DONE and the merge recorded under Integrator record:
11881 total, 11875 passed, 6 skipped, 0 failed.

#### docs(plans): v2.0.28 - record WP123

`618d2a49`

Row 123 is DONE at c09f5876f and the integrator record has its line.

#### docs(plans): v2.0.28 - record WP117

`e6b9d3ea`

Row 117 is DONE at merge 066f72428; the integrator record line carries
the test counts, the resolved conflicts and what was deferred.

#### docs(perf): v2.0.28 - drop WP117's until WP118 notes

`e1ae44d4`

WP117's docs were written before Patch89, [MapLoad] and the load-time
stamps reached this line. WP118 is merged, so the sentences saying they
would arrive later are removed, and the map profiler page now links
map-load-diagnostics.md. Step C1.4 of plan 118, run on the merged tree.

#### docs(plans): v2.0.28 - record WP118

`7c23c625`

Row 118 is DONE at merge b6cd14309 and the integrator record carries
the test line, the settings pins and what was deferred.

#### docs(mission-perf): v2.0.28 - frame instruments doc and engine page

`53512fbd`

Adds the feature doc for the [MissionPerf] heartbeat, the hitch probe
and the tick profiler, and the engine page on the battle frame, its
threads and native costs, both carried from the 1.5.x line with a
section that maps their engine citations to v1.4.8. Registry entries
for Patch97 and Patch98, the MCM restart note, the reflection rows,
and a provenance row for the yotthani engine notes the page restates.
The toolchain doc no longer says the game runs on Mono: v1.4.8 ships
three runtime hosts, and the [PerfContext] line names the CLR.

Research: v1.4.8 Mission.cs lines re-read for the frame table;
installed bin folder hosts per the port's runtime check; player log
CLR line (taom_debug 2026-11-01)
Ported-from: af990ca769da52f408b8e804d127c446b515a379
Ported-from: 6eb2e1b90c2fe42d6cda851434c0ff6ec1fdcd1c
Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db
Ported-from: 43ca92ee99c064467113e603c19c58a5901b9093
Ported-from: 685c6a1e03f799567d5c5c5e93eae052900f767b

#### docs(plans): v2.0.28 - record WP120

`64830cd1`

Row 120 is DONE at merge cb32ec0d6; the Integrator record line carries
the tests, the recounted settings pins and what was deferred.

#### docs(plans): v2.0.28 - record WP111

`313bcfaa`

#### docs(plans): v2.0.28 - record WP113

`ade38d8e`

#### docs(plans): v2.0.28 - record WP109

`eb0a299b`

Row 109 set to DONE and the merge recorded under Integrator record.

#### docs(plans): v2.0.28 - record WP112

`6a6e0846`

Row 112 DONE at merge 2f524c519, old plan 002 DONE (its mutation half
landed in Step C2), and the integrator record line for the merge.

#### docs(ai): v2.0.28 - pair Harmony prefix and finalizer via __state

`df41af6c`

A prefix and finalizer pair must link through Harmony's __state,
never a static flag: Harmony reruns every finalizer when a later one
throws, and PatchShield strips prefixes but never finalizers. The
mission-start guard's canary relies on it. One line in the Harmony
rule file, which loads with every patch file.

Research: Dependencies/Foundation/PatchShield.cs unpatch scope
Not-tested: rule text only
Ported-from: 83ad1eab56dc147c1d6259f69c3480a3e3a368c8

#### docs(plans): v2.0.28 - record WP107

`8ec28c69`

Row 107 is DONE; the integrator record carries the merge sha, the test
counts and what was deferred.

#### docs(battle-load): v2.0.28 - read a render-wait stall honestly

`349bf9b3`

The feature doc now says a silent render-wait stream is a frozen count
and that only the engine's compile_shader lines tell a compile from a
wedge, and it gains player guidance for a battle stuck on the loading
screen. The bug template asks for the full rgl_log and a dump.

Shader pre-compilation stays ACTIVE under D4, with the evidence written
down: 1.5.x parked it for no runtime defect, and on 1.4.x it is the only
warm-up. The claim that reordering mods clears the shader cache is
corrected: v1.4.8 sorts the module ids before comparing them.

A polls=1 load stall is no longer attributed to the #352 wait, in the
patch comment, the docs, the registry and the triage note: on v1.4.8
WaitForMeshesToBeLoaded runs from OnSceneRenderingStarted, after loading
has finished.

Research: D3 disassembly (sorted module list), V02 guidance, 25995dc91 RCA; v1.4.8 MissionState.cs:221-233, WaitForMeshesToBeLoaded callers per plan 108

#### docs(patchshield): v2.0.28 - shim exclusion, method list, repeat counts

`b6e2e254`

The TAOM.Dependencies maintenance doc now covers the rethrow stack
preserver, the callback-shim exclusion, the method-level list, the
seen and attached pass counts, repeat counting and the stack lookup.
The crash-report performance notes and the arena doc's #331 entry
follow plans 007 and 034. Class counts and engine facts are re-cited
to v1.4.8.

Ported-from: c79a585218ad22841c8db1f273e013385512b332
Ported-from: 73792944591e214595515f7c68bbb7c3265b0e83
Ported-from: 578ac7d617f1fd0ba62827c9f6960611708c4cb4
Ported-from: 31a31f16b4ca3ac8384066b23968beac26e38afd
Ported-from: 0bf2409e668db7382e02ea3d0ce2162ddb65cacb
Ported-from: 900920dca4daadbc2684c80e2f5c997f2235b395
Ported-from: 88408869fbbfcc7d1aa68c145b71fa9dd8645dd4
Ported-from: 5ee49d05f1d24e060ecf597cdd75724f1d545618

#### docs(memory): v2.0.28 - steps to find what a second game keeps

`11393bbf`

A second campaign in one game process starts gigabytes higher than
the first, and the .NET heap often stays four to six times its cold
size at the main menu. The session resets ported in this plan fix
correctness; on the 1.5.x line the memory stayed after them. The new
investigation doc says what to capture (heap snapshots at a cold menu
and after one and two campaigns), how to compare them, which holders
code reading already names, and what each result means for a
follow-up fix.

Not-tested: the procedure itself (maintainer run owed)

#### docs(plans): v2.0.28 - record WP114

`fbd16725`

Row 114 is DONE at merge 6ab12f4f9; the integrator record carries the
test totals, the baseline movement and what was deferred.

#### docs(crash-report): v2.0.28 - document the allowlist and live toggles

`faf04c5a`

The crash-report feature doc, the MCM doc, the Patch37 registry entry,
the reflection-site catalogue, the patch-target snapshot and the
SubModule lifecycle page now describe crash capture after the plan 006
port: five lifecycle finalizers, 16 allowlisted callback shims, live
toggles, off-main-thread captures and the throw site kept on every
hand-back. Engine facts are re-cited to v1.4.8.

Ported-from: 6fe83bcad7be5c9c97fbdfc717a774453b3af6ed
Ported-from: 65b691b6a745d622afa66eca8e39a3751219a047
Ported-from: 70727529052be7948305405087633d600a08ca0e
Ported-from: 42624b95bc1c065a456e72fec4b715e39c90410a
Ported-from: 8e6b0935cab35e31b127d5877d1b1aa93a98d932
Ported-from: 8c84fa20e972521ddd79c67f9153cb0ceb8d9fa2
Ported-from: 9e8c99bdb21de8f31e1d5ecb2bc94bb1c3784e1a
Ported-from: 5f6a96d2eb678143444b6113d5b91534a6fd931f

#### docs(plans): v2.0.28 - plan 119 Step A4 expects CS1061, not CS0122

`fb69c052`

Step A4 predicted CS0122 for the test's call to the private
OnNewGameCreated. The build prints CS1061 for those two calls, same
file and same member, so the RED check stopped the first run with the
work unfinished. Only the expected code was wrong. The file and subject
counts in the done criteria move by one for this commit.

#### docs(plans): v2.0.28 - record WP126

`6aa49032`

Row 126 is DONE at merge 5fc3fa1fe and the Integrator record has its
line: 9425 tests, 9423 passed, 2 skipped, 0 failed. The race gates'
verdict on the public Armory stays owed (D25).

#### docs(plans): v2.0.28 - record WP110

`7457d79f`

Row 110 is DONE and the Integrator record names the merge, the test
totals and what stays deferred.

#### docs(hero-race): v2.0.28 - the session-launch race capture is a backup

`84735fa1`

The race restore's session-launch capture was documented as covering
a co-op save transfer that never raises the save event. A review on
the 1.5.x line found the co-op module's transfer save does raise it.
Co-op is not supported on this line and that was not re-checked for a
v1.4.8 co-op build, so the comment, its test comment and the hero race
doc now call the capture a backup for any transfer that skips the
event. No code change.

Not-tested: none (comments and docs only)
Ported-from: 0f7902684ae00959b3cff49f53710025159af175

#### docs(plans): v2.0.28 - record WP108

`8eb471e6`

Row 108 is PARTIAL at merge b16c846d2 and the Integrator record has
its line: 9295 tests, 0 failed. Steps B4 and B5 stay deferred.

#### docs(diagnostics): v2.0.28 - SaveCollected carries no strings= token

`8f836372`

The feature doc, the investigation's probe table, the patch registry
and plan 114 Step B2 all described a string count on SaveCollected. The
engine fills that list after the probe runs, so only LoadCounts carries
strings=. The plan's test list gains the test that pins this.

#### docs(plans): v2.0.28 - record WP106

`2de1ed88`

Row 106 is DONE at merge d72c30727; the integrator record carries the
test count and what was deferred.

#### docs(mission): v2.0.28 - tick handshake lesson and swallow-site notes

`b5c26946`

Records why a swallow inside a mission tick must not become a
rethrow. SaveShield's mission-start swallow and the tournament end
guard both sit inside an OnMissionTick, after Mission.OnTick cleared
tickCompleted, and every catcher above them swallows. Adds the
lesson, the comments at both sites and the harmony rule sentence,
re-cited to the v1.4.8 engine. Comments only: no behaviour changes,
so no paired TAOM.Dependencies release is needed.

The harmony rule's thread table also gains a row for the callbacks
the engine marks multi-thread callable, among them the defend
collision callback that reaches the combat mechanics crush roll. Its
MBRandom draw stays unlocked: concurrent draws only duplicate or skip
values.

Research: v1.4.8 dump TournamentBehavior.cs 149-155, 247-260;
DefaultBattleMissionAgentSpawnLogic.cs 171, 532-642, 707;
MissionBattleSideSpawnContext.cs 230, 373; taom-src Mission.cs
6455-6456, MBRandom.cs 22 and 32, MBFastRandom.cs 22-32 and 186
Ported-from: bd1ac829249a8333d7198b7a433071a2c1cecc52

#### docs(diagnostics): v2.0.28 - save-growth repro matrix and probe docs

`d39acdcf`

Records what is known about saves growing after an in-game reload, how
to read the new [SaveLoad] lines, the repro matrix to run in game and
what each result means. No fix yet: a follow-up plan is written once a
census names the carrier. The player advice stays to what the evidence
supports: restart rather than reload inside a session, and no server GC
or .exe.config edit. The toolchain note no longer claims that managed
code runs on Mono; the phase=Runtime line reports the host per session.

Not-tested: the matrix itself (maintainer, in game).

#### docs(plans): v2.0.28 - record WP105

`41321899`

Row 105 is DONE at merge 983bdf8f3: 9225 total, 9222 passed, 3
skipped, 0 failed. The composition root ADR and the in-game checks
stay owed.

#### docs(wotr): v2.0.28 - #764 and #772 in diplomacy and momentum docs

`c551063c`

The diplomacy, War of the Ring and momentum feature docs now describe
the per-campaign reset (#764), the stored-stance guard and the load
repair (#772), the four War of the Ring phases including WarEnded, the
skipped scripted war that names an unknown or eliminated kingdom, and
the momentum load of an absent chunk count without a warning.

Ported-from: cf7486cba5999c88324bc2ba5ee609e7758896b1

#### docs(composition): v2.0.28 - how to port a 1.5.x feature module

`07d0ca46`

Adds docs/features/composition-root.md: what the feature-module
runner does on the 1.4.x line, where each runner call sits, and the
rules for porting a 1.5.x feature module (copy XModule.cs, append one
FeatureModules.All line, delete any hand wiring of the same feature,
keep the 1.5.x tests) together with the cases that must stop instead.
wanderer-allegiance.md links to it.

#### docs(engine): v2.0.28 - a load can repeat the save's day (#793)

`205c13fe`

Corrects the engine notes for Bannerlord v1.4.8: after a load the
daily tick waits only the part of the day already played, so a save
made in the first half of a day runs that day's daily events twice.
The War of the Ring phase-ordering RCA said the opposite and now
carries a correction. The notes also record that one SyncData string
over 32,763 UTF-8 bytes corrupts a save (the lesson said 32,767), and
that a clan changing kingdom moves its fiefs without an owner-change
event.

Research: taom-src v1.4.8 Campaign.CreateCampaignEvents (:1002-1026),
MBCampaignEvent ctor (:28-34), DefaultCampaignTimeModel (:7),
SaveContext.SaveStringTo and GetStringSizeInBytes (:308-321),
ChangeKingdomAction.ApplyInternal (:65, :76, :95-98, :161).
Not-tested: the repeated daily tick in game (#793).
Ported-from: 573dc155fb5f6e5fe1c39e8d8d46c5cf0770ec3e
Ported-from: a66b5754a69db17062a7987834a95338daa00aa7
Ported-from: e8504a62b63be99a3843670b546e4f1faafd7954
Ported-from: ae1a6b55327d8777725fddcda91263eeab4b434b

#### docs(rules): v2.0.28 - reset per-campaign state in the constructor

`94807a10`

The session-reset rule for process-lifetime singletons now says to
reset in the constructor of a behavior built fresh for every campaign,
never in OnSessionLaunched, OnNewGameCreated or RegisterEvents, and to
pin the per-campaign construction with a source-text test. The
OnSessionLaunched reset it used to prescribe ran too late for the War
of the Ring service (#764): campaign-event listeners run newest-first.

Ported-from: 9ab3952a7d6298044e09f3ab61b9feb598c253e7

#### docs(plans): v2.0.28 - record WP104

`63ce2f56`

#### docs(harmony): v2.0.28 - correct Patch37 coverage, name the guard

`0e30377c`

The crash-report doc said Patch37 is registered first to maximise
coverage of TAOM's own init. Its OnSubModuleLoad finalizer patches
the base method, so it never covers TAOM's own override; the doc now
says so, states that v1.4.8's Module.InitializeSubModuleBases has no
catch, and names TryPatchCategory and PatchCategoryIndex. The
lifecycle reference, the architecture tree, the battle-load reading
trap and the triage tool now point at the [PatchApply] line instead
of the Patch43 warning the guard removed.

Not-tested: none (docs and a tool hint)
Ported-from: 9da9b5b92dd12a86572335e98684b3689da44744
Ported-from: bdf7d515c49740c60d0204770c503bdb2b5436dc
Ported-from: 4c728dac0f2a4ec2b410692ecd08931abaaced4f
Ported-from: 7912fdd8fa31d323d5b31db0b9c3a2014768ab50
Ported-from: 27fd23bbb7c4ad8b79947d3621f3b6989f778c71

#### docs(plans): v2.0.28 - commit the 1.4.8 port plans and their baseline

`1ab48c21`

Adds the 2026-11-04 port programme to git so every work-package
worktree carries it: plans 101 to 145, their index in plans/README.md
and the planning record under plans/_audit/2026-11-04-port148/. The
record's data/brief-workflow3-decisions.md holds decisions D1 to D25,
which the maintainer delegated to the orchestrator on 2026-11-04.

The index gains the integrator's baseline after WP101: 9154 tests,
9151 passed, 3 skipped, 0 failed; hook and tool test counts. The data
half is provisional under D25: 3768 ModuleData errors, 3768 of them
BROKEN_ITEM_REF, on the laptop's installed v2.0.26 Armory, owed until
the public Armory is installed. Later plans compare against these
numbers.

#### docs(machines): v2.0.28 - provisional laptop baseline, Armory v2.0.26

`142a7703`

The laptop's LOTRLOME_Armory is a v2.0.26 copy from 2026-09-07 with
no ladder bows, so every item verdict made there describes an Armory
no player runs. The public Armory is not on this machine, so under
decision D25 this run proceeds on v2.0.26 and the data half of the
baseline is provisional, owed until the public Armory is installed.
On v2.0.26 validate_moduledata.py reports 3768 BROKEN_ITEM_REF on the
merged trunk against 3423 on 1b00d1acd before the merge. The merged
tree names 123 ladder ids this Armory lacks, so these two numbers do
not measure what the merge does against the public Armory.

development-machines.md records the re-measured laptop state as
provisional, and the CLAUDE.md "Two machines" row points readers at
that baseline instead of calling the laptop incomplete. The full
baseline table (build, tests, data, hook and tool tests) goes into
plans/README.md with the programme record.

Not-tested: no game run; this records data and test results only.

#### docs(data): v2.0.28 - review record for the 1.4.8 troop tree (#697)

`8df90444`

CHANGELOG entry, RCA and two lessons for fix/public-armory-ids-145.
92a30ba4 took the 1.5.x troop tree so the 1.4.8 public release matches
the 2026-09-29 Armory; 10a52fb3 removed the rows that need 1.5.x code
(howdah crew, hill troll troop), put the two Mirkwood elk riders back
on horses, and restored gondor_ring_peasant for old saves (#670).

Research: deep review on the laptop's 1.4.8 install; ported files
checked against the installed NPCCharacters.xsd and EquipmentRosters.xsd
and CharacterObject.Deserialize.
Not-tested: in-game smoke and an old save near Glanhir (owed, #697).

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>

#### docs(player-switcher): v2.0.28 - #550 changelog, trap row, review record

`ed3f4279`

The six hunks that belong to the two #550 commits below but lived in
files other sessions were editing on bannerlord-1.5.x, where they landed
inside a mixed sweep commit (ef6afeb6). Ported here on their own so main
carries exactly the #550 change and nothing else:

- CHANGELOG entry for the fix, under a new 2026-09-15 heading
- the CLAUDE.md trap row "A frozen game may be a stuck kingdom vote",
  now naming both causes as guarded
- REVIEW-LOG Review 113 (5-agent deep review + Codex gpt-6-astra ultra)
- the Codex-strengths bullet in .ai/review-reference.md
- the Patch80 registry entry: target line, seam D and seam E text
- the CoopVetoClassificationTests row marking
  DecisionItemBaseVM_ExecuteDone_Patch ReviewedSafe (view-model only);
  without it EveryBoolPrefix_HasACoopDisposition is red

Not-tested: main does not compile on this desktop (its code targets
  Bannerlord 1.4.8, the installed engine is 1.5.3; six errors in files
  this change never touches), so the suite could not be run here

#### docs(changelog): v2.0.28 - patreon copy swept, #562 and #597 closed

`adc027e4`

The patreon release copy under E:/LOTRAOM_Releases carried the same 164
sidecars and two scene Backups folders as the install (452 MB); swept
into its own quarantine (module_bak_sweep_2026-09-14_patreon) because
the date-stamped default already held every one of those relative paths
from the morning run. 0 orphans, 0 mismatches, 0 remaining.

Mike rebuilt the distance cache in game (08:21), the last owed step on
the two village issues, and asked for them closed: #562 (nine Isengard
villages, all ids verified live) and #597 (Serelond, Methir, Framsburg;
no FORTIFICATION_WITHOUT_VILLAGE finding). The new-campaign smoke is
recorded on each as the one unverified item.

### Chores

#### chore(armoury): v2.0.28 - audit the public 1.4.8 Armory

`2b14ffcf`

The first committed run of tools/audit_armory_refs.py on this line,
against LOTRLOME_Armory v2.0.29.5 as the public 1.4.8 build ships it
(installed cooked, so catalogue drift is skipped). Verdict: CLEAN, 1
warning (generator drift), catalogue drift not checked: cooked
install. 161 tpacs, 16,487 metameshes, 8,264 physics shapes, 0 packs
unparsed. Missing bodies: 0. Missing meshes: 0. Dead item ids: 0
(differs from the WP101 BROKEN_ITEM_REF baseline of 3768, which was
taken on the v2.0.26 laptop Armory and is void under D20).
Generators: generate_starter_kit.py --verify reports no drift;
generate_ranged_ladder_items.py --verify exits 1 with 260 problems
against 130 planned items, because tools/ranged_ladders.json plans
band-suffixed ids (ladder_gondor_bow_e) while the public Armory ships
tier-suffixed ones (ladder_gondor_bow_t4). No item is broken by it.

The stale check named all three troll entries, so
KNOWN_DEAD_MESHES is empty again and losing that art errors.

validate_moduledata.py --code MISSING_COLLISION_BODY --code
COLLISION_BODY_BORROWED exits 0 on this Armory, and the full
validator prints 0 errors, 112 warnings. Warm full run 12.3 to 16.6 s,
the hook's 27-code command 12.6 to 13.8 s, under its 45 s bound.

Ported-from: d5986f87f853e0a1d8f36b54a88add9e4212571c

#### chore(harness): v2.0.28 - settings drop dead hooks; harness facts

`357e77d8`

settings.json loses the seven deleted hooks' registrations and the
experimental teams flag, and registers check-feedback-memory.sh under
PostToolUse Edit|Write. Applied under D31 from the plan's generated file;
nothing else in settings changes, and Serena's edit-tool denies stay.

harness-facts.md takes the corrected rows: a skill's hooks keep running for
the rest of the session once invoked, the visibility row, env reaching
hooks, subagents receiving MEMORY.md, path rules not firing in a sibling
worktree, and the skill listing returning after /compact. hooks-catalog.md,
release-process.md, the provenance register, rule-provenance.md,
hook-authoring.md, agent-teams.md and .gitattributes stop naming the
deleted hooks.

Left out: the mcp-servers.md and provenance-register.md Serena edits (D3),
and hook-authoring.md's prefilter section and the 434d1d795 trims, whose
base this line does not have.

Ported-from: 9cfcadd07e61e8b21f9c0a296995901990855d02
Ported-from: b6012355be4f21100a890e62a7774cf7e82e3391
Ported-from: 434d1d795465fe9b78c06decd31db5a26558132c

#### chore(harness): v2.0.28 - remove dead hooks, fix the gates

`ec1cbd43`

Seven hooks go. post-compact and pre-compact print to the debug log on
an automatic compaction, so nothing read them. check-deep-review was muted
by any reviewer in any session. check-version-tagged repeats what
session-start says at every startup. session-stop wrote a log nothing
reads. mcp-health-check refused a server's calls for 60 s after one
failure, and mcp-health-mark only fed it. Their settings entries go in the
next harness commit.

check-claude-files-tracked keeps only its gitignored half: the untracked
half denied git add and git commit in one command. check-polearm-shield-
parity audits the tree the edit is in, and check-verification-evidence
judges the worktree the session is in. config-protection starts no Python
for an unrelated edit, and every ADR stays protected. check-moduledata-
validation stops saying findings are in staged files and says UNCHECKED
when the validator dies with no findings. session-start warns when
core.hooksPath does not name .githooks and loses its TODO count. A new
check-feedback-memory.sh asks where a new feedback card's rule belongs; it
only informs.

Serena is kept by decision (D3). Left out: config-protection's README and
new-ADR allowance, career kits, upstream-watch, graphify. The block-no-verify
rows land with their gate.

Not-tested: live harness checks owed (maintainer)
Ported-from: 9cfcadd07e61e8b21f9c0a296995901990855d02
Ported-from: 434d1d795465fe9b78c06decd31db5a26558132c

#### chore(mcp): v2.0.28 - pin Serena, ElevenLabs and the Codex MCP servers

`68a6f5ac`

Each auto-fetched server now runs one exact version. Serena is pinned
to 7a2968335f, the commit WP103's 16-entry deny list was derived from,
so a new editing tool cannot slip in unpinned. The launch strings live
in .mcp.json, .codex/config.toml and the kingdom-voices snippet, and
mcp-servers.md says all three move together on a pin bump.

WP116 already removed git and filesystem from .mcp.json, so only Codex
still runs those two. Its pins are in .codex/config.toml.
mcp-servers.md names ~/.claude.json for user-scope servers, the file
that doc already calls the one Claude Code reads.

Not-tested: MCP start in Claude Code and Codex after a restart (maintainer live check)
Ported-from: 470ee64e92a2170cbf44ef354d0f685b78585866
Ported-from: 7d13adc369490cf6d7e866c6a44a4dcc76c85f68
Ported-from: 1ce8c370e36a1af37a7479b7dd5f49e88029e7b9
Ported-from: 35f212c60fe9b981be5eb80ff22d41405606144b

#### chore(security): v2.0.28 - untrack per-user settings, pickle and crashz

`db1cd304`

Three tracked things did not belong in git. settings.local.json holds
personal allow rules, desktop folders and the MCP trust list, so every
clone trusted every project server; the trust list is now per user. A
37 MB pickle that nothing reads could run code when loaded. A player's
crash bundle stays readable with
git show 1b00d1acd:crashz/report.json.

Merging this deletes .claude/settings.local.json from each checkout
that merges it, so it has to be copied back (WP142 M2).

Not-tested: Claude Code start with no tracked settings.local.json (maintainer check after merge)
Ported-from: 470ee64e92a2170cbf44ef354d0f685b78585866
Ported-from: 1ce8c370e36a1af37a7479b7dd5f49e88029e7b9
Ported-from: 35f212c60fe9b981be5eb80ff22d41405606144b

#### chore(harness): v2.0.28 - record WP138 trailers git could not parse

`701805c0`

A wrapped Research or Not-tested line in three earlier WP138 commits
keeps git from reading their trailer blocks, so the Ported-from lines of
those commits are invisible to %(trailers). Their bodies still list them.
This empty commit repeats those 12 lines so the plan's check reads them,
without rewriting history.

Ported-from: 6ce20f6edde1df897ed0032927dfcb43b35d756b
Ported-from: 1c622732ecfce085fab27c56d3c7f39e3d59f2c5
Ported-from: ae7c4edbf3f2ec958bbf50ef009ca5bca609f6fa
Ported-from: fa8c06f82fbc5ddd4f23ebe78157f5f2144439d2
Ported-from: 0a8d61a4eb8f4607f531170bf073567f256782b1
Ported-from: 9a2a8dae9c8893626521134855b964c77fb6c2a2
Ported-from: 9d327f43e405e57af6065857b94c6bd22f30503e
Ported-from: ce7f8d6ba0ec9d90315942f5d9bdd6434d2f3fdc
Ported-from: d3fc3c4d038f6ccdafe8ec28a3893d737c534ffe
Ported-from: 099840f33a63eb4d3b1cc0d9b46c6e1a4faeaf7f
Ported-from: 43fe5ebe8ea8f7b20b9ab337072d3468e3674103
Ported-from: f382ff6fdc6c11de2d51ceae6d68fdd91588cc82

#### chore(harness): v2.0.28 - align the harness with Claude Code 2.1.292

`35c0efd2`

harness-facts.md records what the docs and CHANGELOG say about Claude
Code through 2.1.293, each with its source: Explore runs on the main
model, the Agent tool's effort beats a definition's, background agents
and the spawn depth, the compaction window, stricter PreToolUse handling,
when a path rule loads, the unattended background time limit, and the
missing Todo tools. Facts measured on the 1.5.x machine say so. The MCP
and PowerShell reference docs follow, and the adoption record comes
along.

Left out: the CLAUDE.md hunks of both commits; this CLAUDE.md has none
of those lines, and the facts live in harness-facts.md.

Ported-from: b84dbaeb2a713301908c69b8366a2a8b639e6ede
Ported-from: 2ca35d6e2edcdaf38c057a53b0da11e1c141c0ad
Ported-from: 2c737d97cc7c22df1d1b3fc59135da9cf96facf2

#### chore(harness): v2.0.28 - /deep-review senior lenses and risk tiers

`b8363cc8`

/deep-review was one 567-line file launching five core agents. The
lenses are now files run by a pinned read-only deep-reviewer agent (Opus
5.5, max); a risk tier picks the lenses and the Codex effort, and a
second wave runs only on HIGH or CRITICAL. check-deep-review.sh already
looked for this agent. Agent 5 item 10 no longer asks for a
DependedModule between TAOM modules, which module-dependencies.md
contradicts for singleplayer.

Earlier WP edits to the old file are all carried by the new one:
WP127's check 1 and check 6 wording (K10 and K11 true, so both stay),
WP102's CHANGELOG.md row and its Known limitation paragraph.

Adaptations from the plan's table: the Codex trigger follows the paid
dispatch policy and reports CODEX: NOT RUN without a request; the dead
agent-budget-discipline citation is gone; every Fords of War and
MonsterSize citation is gone; Lens 1 H5 quotes the CLAUDE.md budget
line; Lens 2 cites Mission.AfterStart at :3811 and :3815 for v1.4.8 and
drops the 1.5.x example; Lens 5 drops Client/ and the Patch89 and preview
batch examples; Lens 7 points at the CLAUDE.md Traps table;
deep-reviewer says the audit writes its report, which is not tracked
here. "decision 55" in Lens 1 reads ADR-007, as WP127's wording did.

Graphify stays: K13 is true on this line (WP141), so the blast radius
text keeps graphify_taom.py and the plan's [G] rows are not applied.
Riding hunks of other WPs' commits inside these source states:
6e4bfedb7, 30b54df75, bdf7d515c, c79a58521, 332f2ac71, 2a5363e90,
affcee2ea, 9c29d7324, d4e6273a1, 6a2ce8cb3, 48d66b59a, c1ce78207,
bc30b76da, d51e43028, 69b47d00b, b8833474e, e6745a492, 62157c7e7,
709649c3f, 4520af802, fdf9f38cb, 88e805450, c8d69a0dc, 03d9db8dc,
d021426b0. Also: the two-edit test line in tests.md, /ship Phase 1 item
2, the operating manual's item 6, and the context-mode record and row.

Research: taom-src v1.4.8 Mission.AfterStart (:3799-3815);
docs/modding/module-dependencies.md:120-131.
Not-tested: a live tiered review (live check L4).
Ported-from: fa8c06f82fbc5ddd4f23ebe78157f5f2144439d2
Ported-from: 0a8d61a4eb8f4607f531170bf073567f256782b1
Ported-from: ef9559b14f849f43477e5b9a55628fb6f2314ea7
Ported-from: b84dbaeb2a713301908c69b8366a2a8b639e6ede
Ported-from: 2fb9d914abd7c93e3e11c7553a8d34de687e1382
Ported-from: 9a2a8dae9c8893626521134855b964c77fb6c2a2
Ported-from: 9d327f43e405e57af6065857b94c6bd22f30503e
Ported-from: c237c84e48d8b51a3c2e94508adbf99c3343e456
Ported-from: a87bdc3a95ed7bab1766e5523401ea7aa6437964
Ported-from: 6ce20f6edde1df897ed0032927dfcb43b35d756b
Ported-from: ce7f8d6ba0ec9d90315942f5d9bdd6434d2f3fdc
Ported-from: d3fc3c4d038f6ccdafe8ec28a3893d737c534ffe
Ported-from: 2ca35d6e2edcdaf38c057a53b0da11e1c141c0ad
Ported-from: 2c737d97cc7c22df1d1b3fc59135da9cf96facf2
Ported-from: 099840f33a63eb4d3b1cc0d9b46c6e1a4faeaf7f
Ported-from: 9e9f04df40c4a2350fd705b9dc2c0c158aee49c6

#### chore(mcp): v2.0.28 - drop the unused github, git and filesystem servers

`b4e7d207`

No skill, agent or rule called them. The filesystem server pointed at
desktop E:\ folders and failed to start on the laptop. The git server's
write tools bypassed every gate and were held shut only by deny rules.
The github plugin declares the same server, so it is disabled as well:
that one-line settings diff is M2 from plans/116-hook-gate-end-state.md,
applied under D31.

The nine deny entries in .claude/settings.local.json now name servers
that are gone. They are inert and left for the maintainer.

Not-tested: /mcp after a restart (live check L9)
Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005

#### chore(hooks): v2.0.28 - drop three silent hooks, mark failed builds

`2d1fbb16`

suggest-compact, notify-test-results and notify-csharp-edit printed
only to stderr on exit 0, which Claude never sees, and suggest-compact
ran on every tool call. All three are gone.

mark-verification-run is now registered for the PowerShell tool and on
PostToolUseFailure: a red build raises that event, not PostToolUse, so
it never counted as verification. validate-push's registration is 10 s
because the judge it gets next keeps its own 3.0 s deadline.

The settings diff is M1 from plans/116-hook-gate-end-state.md, applied
under D31.

Not-tested: that Claude Code fires the new registrations (live check L1)
Ported-from: 43e6780e4421fb785ac9bf1c4ee5bc53e8b69a68
Ported-from: 18961c1ec6b92c54b54bd1d25e6cd3ec066c6b03
Ported-from: fff6d66e9bd5a2db11c0ef20ab167225efbd1a22
Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005

#### chore(changelog): v2.0.28 - archive the hand-written CHANGELOG

`5fb04942`

The hand-written CHANGELOG.md moves unchanged (the same blob, old
header included) to
docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md, next to the
2026-H1 archive. lint_docs.py already treats that folder as verbatim
history, so its old links and version numbers raise no findings.

The new CHANGELOG.md is only a header: it says the file is generated
at each release by tools/changelog_from_commits.py, that the commit
body is the changelog entry, and links both archives. It stays a
header until the v2.4.0 /release writes the first generated section.
release-process.md now points its v2.0.12 crash-report link at the
archive.

Ported-from: ef5b7ff4e4b25620617fc9453b5971642c88f485

#### chore(hooks): v2.0.28 - retire the two CHANGELOG hooks and their rules

`e411ecfa`

/release now generates CHANGELOG.md from commit subjects and bodies,
so the rule that every session hand-edits the file is gone, and the
two hooks that enforced it go with it.

check-changelog-changed.sh (PreToolUse on Bash) denied a git commit
that staged anything under .claude/, CLAUDE.md or AGENTS.md without
CHANGELOG.md. check-changelog-updated.sh (Stop) printed a reminder
when source files were dirty and CHANGELOG.md was not. Both scripts
are deleted, and their two registrations are gone from
.claude/settings.json; an agent made that edit under the session
override the maintainer chose for the protected files.

session-start.sh no longer prints the newest dated CHANGELOG section.
Its degraded-toolchain banner now names all five python-only gates by
file name: it used to name the retired gate and leave out
check-commit-subject-version. tools/test_hooks.sh 5b2 fails when a
gate with no jq path is missing from that banner. Comments that named
a retired hook are trimmed, hooks-catalog.md and CLAUDE.md are
recounted (27 scripts, 26 settings.json registrations across 9
events, 31 with the five skill frontmatter registrations), and
harness-facts.md and hook-authoring.md stop citing the retired gate.

Not-tested: a session started on the new settings.json. Sessions that
  are already running keep calling the two deleted scripts until they
  restart.
Rejected: keeping check-changelog-changed.sh as a gate against hand
  edits. The generator already refuses a hand-written section, so a
  rewritten commit gate adds code for no new protection.
Ported-from: c91bb95f8d3d1d1008df0569b12cd23ff49d7775
Ported-from: 6e4bfedb7769bdc1d45d3cf8f90f1564851b9015
Ported-from: 971d8e97581cf6a43d7f9671eb9b19b25cb07c76
Ported-from: 5b242cee660b1675f2c3f582f91130bd9a85cd35

#### chore(hooks): v2.0.28 - one --code per line for the new validator codes

`2f808526`

The hook named two codes on one line in two places, so a line count of
the new codes (plan 136, Part B done criteria) read 3. Layout only.

Not-tested: none

#### chore(tools): v2.0.28 - validator gates files against the engine's XSD

`12931e9e`

The engine validates a registered file against its XSD on load, but
only prints a failure and loads the file anyway, so a clan with no
initial_home_settlement reached players. validate_moduledata.py now
runs tools/validate_xml_schemas.py over the repo module as the
SCHEMA_INVALID pass, and the commit hook names the code, so a file the
engine's own schema rejects or a registration that loads nothing is an
error. Without lxml or the engine's XmlSchemas folder the pass prints
one NOT-verified warning, never a silent pass.
check_external_loc_coverage.py drops its private copy of the
engine-style parser and imports the tool's.

The sort of issues moves out of the install branch, so a run with no
install sorts as well. The tool itself landed earlier in this WP; this
is the validator half, which waited for WP108 Step B4.

Research: v1.4.8 MBObjectManager.GetMergedXmlForManaged, HandleXsltList
Research: v1.4.8 LoadXmlWithValidation, XmlResource, ModuleHelper
Not-tested: none (offline)
Constraint: D15, taom_body_length stays a reported failure
Ported-from: fa8c06f82fbc5ddd4f23ebe78157f5f2144439d2
Ported-from: 48d66b59a7454eee36ed3f989f5776dce4e74a0a
Ported-from: 4644bf4931335b07970583488eca397631f3027a
Ported-from: c1ce782072e2da7de8c8b6f0e8231b1ff7c3f305
Refs #621 #974 #980

#### chore(rules): v2.0.28 - destructible damage stages move forward only

`5affa519`

The usable machines page and the adapters rule now say that the engine
does not keep a DestructableComponent's shown stage forward: a heal can
step the object back to an earlier model, or above the first line read
_destructionStates[-1] and leave it invisible and unhittable. Preventive
on this line, where no TAOM code writes a destructible's HitPoint.

Research: taom-src v1.4.8 DestructableComponent.cs:87, :105-124,
:399-412, :489-498; CastleGate.cs:208, :1054
Not-tested: docs and rule only
Ported-from: c2c3a6f285ba83814a45bd9aaf1c9c029c3fadd8
Ported-from: db8f7c10e7772612fbe20563b75b9e36c3d8e7b7

#### chore(localization): v2.0.28 - cache wave-4 and name translations

`d876e8cd`

Rows with identical English on the 1.5.x line take its translation in
the cache: 29,016 entries over 12 languages, the 320 wanderer rows per
language among them. The language rows stay English until the translator
fills them; that pass is owed, and it now needs the model for only about
94 rows per language.

Not-tested: in game, see the plan's in-game list

#### chore(tools): v2.0.28 - strict mount parity, skill check, module flag

`927e7a44`

audit_mount_parity.py gains --strict, which exits non-zero when a
behaviour-tree mount's usage sets are not total (spider, elephant and
mumakil all read total here). apply_culture_skills_traits.py gains a
read-only --check that compares the generated SkillSets with
taom_lord_skill_sets.xml (145 sets match on this tree).
translate_with_claude.py now requires --module, because the old default
of all also writes the live Armory; a test proves the parser refuses
both forms without it. The Uruk-hai comments and the validator rule
stop saying the skirt sits in the Cape slot, which stopped being true
when the data moved it to Body. The RCA behind that change comes along
with a port note.

Not-tested: none
Ported-from: f1675c0581a472fcc72fb3c8da7f1566912255fe
Ported-from: 569f6750d79e41b5045624ff05c9ef25d529fd4b
Refs #1056

#### chore(tools): v2.0.28 - check every engine-loaded file against its XSD

`be0f1a8e`

The engine validates module XML on load but only prints a failure and
loads the file anyway, so nothing on the 1.4.8 line checked a file
against the schema the engine reads it with. tools/validate_xml_schemas.py
mirrors the engine's file and schema resolution and reports each
violation by line; --live adds TAOM_Map and the Armory. On this tree the
repo module passes 94 files; --live fails only on three family_type rows
in the installed v2.0.26 Armory, which that Armory's vintage explains.

taom_body_length stays a reported failure (a test pins it), since D15
reads it as an Armory pairing signal. This commit is the tool alone: the
SCHEMA_INVALID validator pass waits for WP108 Step B4, which that pass
is built on and which has not landed on this line.

Research: v1.4.8 MBObjectManager.GetMergedXmlForManaged, HandleXsltList,
  LoadXmlWithValidation, XmlResource, ModuleHelper
Not-tested: none (offline)
Constraint: D15, taom_body_length stays a reported failure
Ported-from: fa8c06f82fbc5ddd4f23ebe78157f5f2144439d2
Ported-from: 48d66b59a7454eee36ed3f989f5776dce4e74a0a
Ported-from: 4644bf4931335b07970583488eca397631f3027a
Ported-from: c1ce782072e2da7de8c8b6f0e8231b1ff7c3f305
Refs #621 #974 #980

#### chore(tools): v2.0.28 - tpac tools default to TpacTool v0.5.1

`fc314657`

Three tpac tools pointed at a TpacTool 0.4.0 folder that no longer
exists. They now default to the v0.5.1 fork Audit-MeshRefs.ps1 already
used, and the fork has its provenance row. Desktop-only tools.

Not-tested: a run of the three tools with v0.5.1 (desktop; skeleton
 positions are Vector4 there)
Ported-from: 776054c0cac5db933d9035ec412efb809a6fc226

#### chore(debug): v2.0.28 - managed-only default launch profile

`c604d3c6`

The Bannerlord profile attaches only the managed debugger; Bannerlord
(mixed native debugger) keeps managed plus native for native crash work.
The speed-up on Harmony patching (PatchShield pass 2 in diag.log) is
UNVERIFIED and is the maintainer's next Play to check.

Not-tested: a Visual Studio launch
Ported-from: 37ff41e4afb0b426aa874462e7b0de4c26b6afc6

#### chore(ai): v2.0.28 - a local fear effect floors morale or says so

`ea5f7818`

csharp-patterns.md gains one anti-pattern from the dread aura fix: a
local fear effect that drains morale below 0.01 must floor the morale
or say in its feature doc that its routs spread, because each rout
passes a morale loss to up to 10 allies, from the whole formation when
fewer stand within 4 m. .ai/scopes.md links csharp-patterns.md from the
managed lane so other AI clients read it too.

Not-tested: docs only
Ported-from: 1e62adeb2029dc9e00a86551cb3d01432db81f50

#### chore(harmony): v2.0.28 - silence the Patch88 BHA0001 false positive

`4df1e12e`

The build printed one analyzer warning: BHA0001 on the Patch88 scope
class, naming LordPartyComponent's nested InitializationArgs without
its containing type. BUTR.Harmony.Analyzer 1.0.1.50 does not resolve
nested target types; the target exists on v1.4.8 and Harmony binds it
at runtime. A scoped pragma on that one attribute silences the false
positive, so the next real BHA warning is not lost in the noise.

Research: taom-src LordPartyComponent (:14 nested class, :29 method)
Not-tested: nothing in game; the change is build output only

#### chore(ci): v2.0.28 - remove the GitHub Actions workflow

`ccef172a`

build.yml (Build & Test: hook harness, Python tool tests, XML and
CI checks) is deleted; at removal it had passed 57 of its 186 runs on
this branch. Verification runs locally instead: /verify builds and runs
the C# suite, and now also runs the Python tool tests (python -m
unittest discover -s tools/tests -t .), which nothing else ran
automatically. tools/test_hooks.sh covers the hooks and the commit hooks
keep gating. References to the workflow in the rules, the verification
guide, the feature map and the NativeSkinFixes doc are updated. The same
change landed on bannerlord-1.5.x as c8d69a0d.

#### chore(release): v2.0.28 - drop the tracked sp_battle_scenes sidecar

`ea035cf6`

Pre-release backup sweep for the public build: the Cloudflare
distribution rejects .bak, so tools/sweep_module_backups.ps1 -Apply
moved 182 files (455 MB) out of TAOM, TAOM_Map, LOTRLOME_Armory and the
repo's Main/_Module into the dated quarantine under E:/Bannerlord_Backups
with a SHA256 manifest. No orphans, 0 mismatches on the re-hash sample,
0 sidecars left on the re-scan.

sp_battle_scenes.xml.bak_scenes was the one sidecar git tracked (added in
0ed2cf38, before the *.bak* ignore rule), so every checkout put it back
under Main/_Module and CopyModule redeployed it. Removed from the index.

Not-tested: the upload itself; the packager gap that let dated suffixes
through is the next commit.

### build

#### build: v2.0.28 - solution lists the TAOM.Dependencies project

`c9e18cdd`

Visual Studio now shows and builds TAOM.Dependencies as a solution
project instead of only through TAOM's reference. It builds as Any CPU,
as before. This does not explain E3's BuildStamp mismatch (the reference
already built it).

Rejected: the 1.5.x x64 mapping (changes the Dependencies output folder
 for no gain)
Not-tested: a Visual Studio solution rebuild
Ported-from: 1118bc2e8a930ab47fb53017f2c7b629607b6172

### Commits without the version label

#### fix(troops): add gondor_ring_peasant and update retired troop compatibility

`10a52fb3`

#### chore(changelog): update to reflect closure of 63 issues and findings from tracker sweep

`6bab10ae`

#### feat(commit-hooks): implement check-commit-subject-version.sh to enforce versioned commit subjects

`8dabf4a6`

- Added a new pre-commit hook script `check-commit-subject-version.sh` that ensures every commit subject follows the format `<type>[(scope)]: vX.Y.Z - <description>`, where `vX.Y.Z` matches the `<Version>` in `Main/_Module/SubModule.xml`.
- Updated `.claude/settings.json` to register the new hook with a timeout of 10 seconds.
- Modified documentation in `CLAUDE.md`, `SKILL.md`, and `release-process.md` to reflect the new commit subject requirements.
- Enhanced `CHANGELOG.md` to document the introduction of the versioned subject rule and the associated hook.
- Updated `tools/test_hooks.sh` to include tests for various commit subject cases against the new hook.

#### Implement fortification validation and enhance settlement economy records

`84faba01`

- Added a new validation check to ensure every town and castle has at least one bound village, preventing issues with fortifications starting without village trade or rural notables.
- Updated the settlement economy records to include a 'bound' field for villages, linking them to their respective fortifications.
- Introduced unit tests to verify the new validation logic and ensure proper handling of fortifications without villages.
- Enhanced existing tests to cover scenarios related to village bindings and fortification warnings.

#### fix: resolve warg bite clip issue on cavalry mounts and address stale agent handles

`0c323437`

- Introduced a new reference-identity cache for agent adapters to prevent stale handle issues.
- Implemented eviction and reuse telemetry in the agent adapter factory.
- Ensured that all `SetActionChannel` calls check for valid action clips to avoid incorrect behavior.
- Updated the creature behavior trees to run on the main thread, preventing asynchronous execution issues.
- Enhanced the spatial grid management to rebuild by replacement, ensuring stale entries are removed correctly.
- Added comprehensive tests to cover new functionality and prevent regressions.
- Documented changes and added lessons learned to improve future development practices.

#### fix(map-ui): low plate opacities keep the name opaque (#596)

`c8f72bf1`

Second deep review and a Codex gpt-6-astra ultra pass on fe266439
agreed on one defect: the text curve kept the name, banner, ring and
icons at full alpha until the plate fell under vanilla's 0.35, which
was the fade's job in #591; the new opacity sliders reach 10, so a 10%
plate gave a 29% name at close range on every value from 10 to 34.
TextAlphaFor now takes the plate's configured resting opacity and
anchors on it when it is below 0.35; tracked plates, unknown relations
and missing settings keep vanilla's anchor, so the default curves are
unchanged. SettlementPlatePresenter.RestingAlpha picks the anchor.

Codex also held the provider to the config-validation rule: an invalid
hand-edited TAOM.json value (MCM's slider clamps, its JSON loader does
not) reverts to the default with one warning per property, never per
read. Two RCA statements corrected: MCM's slider clamps, and MCM does
raise a save-time settings event; the per-frame compare stays because
Cancel undoes values without saving.

13 new tests (76 in the feature), suite 9058 green (2 skipped).
RCA: docs/reviews/rca-settlement-nameplate-relation-2026-09-13.md

Not-tested: the sliders in the live MCM screen and the repaint on the
open map

#### feat(map-ui): MCM controls for nameplate colour and opacity (#596)

`fe266439`

Four live controls in Map UI / Settlement Nameplates: Colour Nameplates
by Relation (off paints every plate neutral), Relation Tint Strength
(0 to 100%, blends the palette toward parchment and black text, one
slider in place of twelve colour fields MCM cannot edit), Neutral Plate
Opacity (default vanilla's 35) and Coloured Plate Opacity (default 50
for own, enemy and allied plates). Tracked plates keep vanilla's 80 and
the distance fade still multiplies the chosen value.

NameplateRelationSettingsProvider turns the percentages into fractions,
reverts NaN and out-of-range values to the compiled defaults, and caches
TaomSettings.Instance on first successful read (the container is built
before MCM creates it). The alpha service returns the configured target
by relation; the engine-constructed plate widget reads the toggle and
strength through a static captured in the IoC end block and repaints
when the effective strength differs from the one it last painted.

17 new tests (63 in the feature), fingerprint pin 229, suite 9027 green.

Not-tested: the sliders in the live MCM screen and the repaint on the
open map
Save-compat: none, per-user MCM file only

#### test(tools): re-record the external loc coverage baseline

`9c78197e`

The ratchet on the two live modules (TAOM_Map, LOTRLOME_Armory) had
been red since the nine Isengard villages (#562) landed: their names
are kept unchanged by the model in every language, and the checker
counts a row identical to its English as untranslated. After the
2026-09-13 backlog run the baseline is 2,928 identical rows across the
12 languages, every one a proper-noun name the model returned as-is
(0 descriptions), and Polish's TAOM_Map and Armory counts drop from
1,110 and 2,782 to 798 and 2. The checker passes again.

Also records in the CHANGELOG the second issue sweep that the run
satisfied: #446 closed, translation checkboxes on #513, #558, #575 and
#540.

#### localization: clear the translator backlog

`df0fa43f`

One translator run per language, all three modules, with an API key in
the shell for the first time since August: 216 model entries in each of
the 12 languages, zero validation failures, $3.71 in total. Every row
that still held English is translated, and a dry run now reports 0 rows
needing the model in every language.

Covers the wanderer refusal lines and the encyclopedia badge keys
(#579), the two career keybind strings (#534), the 161 camps-port keys
(#508), the special-resource tooltip and toast rows, the Blue Craig feat
name, and the 130 ranged-ladder item names in the live Armory. The 18
career survival pips that still said troop regeneration (#498) were
reset to the current English and evicted from all 12 caches before the
run, since the tool only refills rows that hold English and its cache
answers by id; the RU cache is not in the tool's canonical layout, so
its lines were removed one by one and the result proven equal to the
original minus those keys.

Not on this branch: #478's Advanced Starting Options keys live on
bannerlord-1.5.x, and its female-notable names are inline NPC names
outside the pipeline, as are all troop names (#572 assumed stale rows
that never existed). Both issues stay open.

Closes #579
Closes #534
Closes #508
Closes #498

Not-tested: in-game language switch; the localization gate (34 tests)
  and an XML parse of all 468 language files stand in for it
Research: tools/translate_with_claude.py discovery and cache gates

#### feat: resource badge on the encyclopedia troop tree

`fc07d925`

Players asked the encyclopedia troop tree to mark troops that need a
special resource. Every troop whose cost row carries an upgrade cost, a
recruit cost or a daily upkeep (27 today) shows its own resource icon in
the bottom-right corner of its card; hovering lists the resource, then
Upgrade, Recruit, Upkeep per day and Elite Emissary price where set, and
names the player's own resource when it differs, since those charges
land in the player's resource. The 50 emissary-only rows stay unmarked:
they are ordinary tree troops the emissary happens to sell.

The badge is a widget in TAOM's existing clone of
EncyclopediaUnitTreeNodeItem.xml, fed by a UIExtenderEx mixin on
EncyclopediaUnitVM that reads the troop from the private _character
field once (pinned in ReflectionSiteBindingTests). The row's resource_id
picks the icon through ISpecialResourceConfigProvider.GetById, so it is
load-bearing for the first time and TroopResourceCostDataTests gates it.
Six taom_res_badge_* strings, seeded as English in all 12 languages.

Deep-reviewed by 5 agents, no findings. Full suite 8970 green, 18 new
tests. Verified in game: Gems on the Ironpass Ram-Breaker, nothing on
an emissary-only row.

Closes #590

Research: EncyclopediaUnitVM, EncyclopediaTroopTreeNodeVM, HintWidget,
  BasicTooltipViewModel, EncyclopediaUnitTreeNodeItemBrushWidget
Rejected: Harmony postfix on the node constructor to stash the troop;
  one reflected field read plus a binding-gate row costs less
Not-tested: translator run for the six keys (no API key in the shell)
Save-compat: no save data touched

#### feat(map-ui): colour settlement nameplates by relation (#591)

`3a289751`

Players could not tell an enemy town from a friendly one. Vanilla tints
the plate through NameplateItem.Color and fades it through the item
widget's AlphaFactor, but TAOM's diamond prefab moved every visual onto
child widgets that Widget.Render never propagates to, so both signals
have landed on nothing since April: opaque, uncoloured plates and a
distance fade with no target.

TaomSettlementPlateWidget replaces the SettlementNameplateLayout
container in the three prefabs, binds @Relation and paints the bar,
name text and diamond frame from a palette (neutral is identity), and
each late update mirrors the item's alpha and colour factor onto bar
and frame, taking text, banner, tracked ring and party and event icons
down with it under the fade. Enemy and allied plates are raised from
0.35 to 0.5 through the Patch38 postfix ahead of the fade multiplier.
Colours are #RRGGBBAA attributes on the widget; no MCM knob.

Deep review (3 findings fixed) and Codex gpt-6-astra ultra (1 P2, 2 P3
and a test gap fixed). 46 new tests, suite 8970 green (2 skipped).
RCA: docs/reviews/rca-settlement-nameplate-relation-2026-09-13.md

Research: SettlementNameplateWidget.SetNameplateRelationType and
  UpdateNameplateTransparencyAndBrightness, Widget.Render/OnRender,
  WidgetExtensions.SetWidgetAttributeFromString, GauntletView.RefreshBinding
Not-tested: the widget's render and the postfix body (live game)
Save-compat: none, UI only

#### docs: update CHANGELOG and README for recent fixes and features

`f8a25a4a`

#### docs: restore the #583 Codex follow-up hunks dropped by 94552135

`dc2d13a5`

94552135 committed the worktree copies of CHANGELOG.md,
docs/reviews/lessons/build-tooling-workflow.md and tools/README.md,
which were stale reconstructions from another session, and so undid
the three shared-file hunks of 4bc5e859: the "fix(tools): the
kingdom-cap curve after its Codex pass (#583)" CHANGELOG entry, the
two Codex lessons, and the two README row updates (reserve routed by
item id, the invariant proves the primary stat only). Re-applied on
top of HEAD from the index, nothing else touched.

The same commit also dropped the ranged-ladders session's Review 108,
its CHANGELOG entries (#582, #588, the ranged-troops page, the militia
step), its two lessons, the RANGED_MOUNT_USAGE rule row and seven
lines of .ai/review-reference.md; those are not restored here.

#### Refactor Rhûn troop generation and validation scripts

`94552135`

- Updated `generate_rhun_troops.py` to reflect the current troop tree and item references, ensuring compatibility with the latest Armory changes.
- Replaced retired item IDs with their current counterparts in weapon and armor assignments for various troop types.
- Introduced `check_generator_item_refs.py` to validate that item IDs generated by scripts exist in the live install, preventing issues with missing references.
- Enhanced `validate_moduledata.py` to report unresolved item IDs generated by troop scripts, improving error handling and validation processes.
- Added unit tests for the new generator check functionality to ensure reliability and correctness.
- Adjusted starter armor generation in `generate_starter_armor.py` to use updated boot references.
- Modified career starter rosters in `generate_career_starter_rosters.py` to align with the latest item definitions.

#### docs: RCA cites the amended review-fix hash (#582)

`b9fea46e`

#### docs: ranged ladder in the restat recipe (#582)

`47d906a6`

The handbook's restat recipe named the armour ladder and not the ranged one: a level change across a band boundary moves the troop's ladder_* cell, and the roster tool moves it, never a hand-picked bow. The feature-map row names the RANGED_MOUNT_USAGE gate beside the inversion gate.

#### docs(cavalry): CHANGELOG and review-log entries, Patch31b in the rule, skill output note (#586)

`031847f7`

The CHANGELOG entry and the Review 107 entry were written with the feature commits (8206a62d, 7c1d3253) but held back while another session had staged hunks in both files; they land here on their own, built from HEAD plus the entries alone. The harmony-patches rule names the Patch31b sibling in the shared MovementOrder category. The review-codex skill now tells Codex to return its report as the final message instead of writing the path the dispatcher is redirecting the transcript into, which lost the report on 2026-09-13.

#### fix(data): riders draw their ladder bows (#582)

`7b105a8a`

Codex (gpt-6-astra, ultra) on the seven ranged-ladder commits: four P2,
one P3, all confirmed, zero false positives. Seven horse archers (Harad
R and V, Rohan E, Dunland R and V) had been rostered into cells cloned
from a long_bow donor; Native's long_bow usage set is base_set="bow"
plus requires_no_mount and requires_no_shield, so the bow spawned on
their back and was never drawn. All seven carried a vanilla steppe bow
before the rewrite.

The fix keeps each kingdom's own mesh the way the Armory does it
(wm_mirkwood_bow_a02 "LongBow II - Horse" is the a01 mesh with
item_usage="bow"): a per-line "usage" override in ranged_ladders.json
for Harad and Dunland, Rohan's E band on the line's composite_steppe_bow.
The generator writes the usage onto the clone and --verify checks it.
130 items regenerated in the live Armory and the mirror, 0 roster edits.

Guards: Launcher.usage, RangedTroop.mounted, mount_barred_usages() read
from the install's item_usage_sets.xml (None without it, every caller
says it skipped), planned_edits(barred=) refuses a mounted troop in a
barred cell, validator warning RANGED_MOUNT_USAGE (0 today, 7 before).
Also from the pass: the inversion rule judges the higher troop by its
slowest set; an unparseable troop file is a (file) finding; a valid
whole-file-before-prefix order is accepted; launchers joins the
suspect-registry floors; the feature doc's loot claim corrected
(NotMerchandise is skipped by casualty loot, the items are troop-only by
construction). 47 to 68 tests, 1458 tools tests green, validator 0
errors and no new warnings. RCA, review 108, lessons, CHANGELOG, README,
rule and CLAUDE.md rows.

Research: Native item_usage_sets.xml long_bow; CampaignUIHelper
RequiresNoMount tooltip; DefaultBattleRewardModel.GetRandomItem:125,153;
ItemObject.NotMerchandise; spnpccharacters.xml Wolfskins (long_bow beside
a shield, native-authored, so requires_no_shield is not gated)
Not-tested: a Harad horse archer drawing from the saddle in a Custom
Battle (full restart owed)

#### docs(cavalry): feature doc v2, registry, RCA, lessons, trap row (#586)

`7c1d3253`

Rewrites docs/features/smart-cavalry-ai.md for the v2 state machine (per-
state order table, the engine facts the design rests on with installed-DLL
citations, the geometry trade-off, the in-game smoke checklist, 106 tests).
The Patch31 registry section gains the player-commanded gate, the re-issue
prefix, the Patch31b sibling and the Formation.Tick substitute path; the
patch-target snapshot gains Patch31b; the feature-map row is updated.

RCA docs/reviews/rca-smart-cavalry-2026-09-13.md records why v1 shipped
four months without forming a line (a mocked adapter hid a metric that
could never be true; a Stop was assumed to hold a line), the deep-review
and re-check findings, and the Codex pass, whose three P1s share one shape:
the state an engine call leaves behind. Lessons appended to
lessons/adapters-taleworlds-api.md (Stop holds each rider in place; shape a
formation with a Move), lessons/testing-qa.md (a computed verdict behind an
adapter needs a numeric test) and lessons/harmony-il.md (enumerate every
caller of a patched setter; tell a re-issue from a new order). CLAUDE.md
gains one trap row; .ai/review-reference.md and the Codex track record gain
the post-condition lesson.

The CHANGELOG entry and the REVIEW-LOG entry are written but left unstaged:
both files carry another session's staged hunks, so they land in a
follow-up commit once those are in.

#### fix(cavalry): the line charge forms, rides through and comes again (#586)

`8206a62d`

Players with Smart Cavalry AI on saw cavalry stand still, stop inside the
enemy, and need F3 twice. The v1 port could not work: its line-up issued a
Stop, which the engine implements as each rider holding its own position
(Formation.cs:1262), so no line formed; its alignment check measured spread
along the line and asked for 1.5 m of it, impossible for more than two
riders; and its double-tap guard left the second F3 to vanilla.

The state machine is rewritten. Forming is a Move into a line 5 m ahead
(SetPositioning plus a facing order, because Formation.Tick re-applies the
retained FacingOrder every tick). Alignment is the mean rider-to-slot
distance read from the arrangement (LineAlignment, 10/4.4/2 m at strictness
0/0.7/1). Charging is ChargeToTarget plus SetTargetFormation, because every
SetMovementOrder clears the native target on its way out. Contact is
measured along the charge direction frozen at line time; the reform point
sits past the live enemy's depth along that axis; Reforming starts the next
cycle at the nearest live enemy, rerouting around friendlies like the first.
Every hold state has a dwell budget and every give-up hands the formation
back to a vanilla Charge, so no exit leaves riders standing. A second F3
charges at once. Any other player order cancels the cycle; AI-commanded
formations are never entered; a formation that empties or dismounts leaves
the machine. Patch31 gained a prefix that captures the previous order so an
engine re-issue (a banner bearer dying) is not read as a player order, and
Patch31b postfixes SetTargetFormation because the player's targeted charge
is a plain Charge followed by the target. New MCM knob Max Line-Up Seconds
(default 4); the settings-fingerprint pins move to 225/180.

Reviewed by a 5-agent deep review, a re-check of its fixes, and a Codex
gpt-6-astra ultra pass (3 P1, 4 P2, all confirmed and fixed). 8825 tests
green in an isolated worktree; 106 SmartCavalryAI tests.

Research: Formation.SetMovementOrder/GetOrderPositionOfUnit/Tick/SetTargetFormation/RemoveUnit, MovementOrder.MovementState/OnApply/IsApplicable/AreOrdersPracticallySame, OrderController.SetOrderWithFormation, BannerBearerLogic.RepositionFormation, FormationAI.TickOccasionally, IFormationArrangement.GetWorldPositionOfUnitOrDefault (installed v1.4.8)
Rejected: keeping Stop for the line-up (the engine ignores slots under StandGround); a Charging timeout (a charge order never leaves riders standing)
Not-tested: the Harmony prefix and postfixes and the MissionBehavior entry point (game-tested per ADR-008); in-game smoke owed before the toggle defaults on
Save-compat: none; mission-scoped state and one new MCM property

#### docs(armour): balance-levers and black-numenorean follow the kingdom-cap curve (#583)

`c94bef6d`

Both still named SLOT_BASELINES and CULTURAL_MODS as the armour curve's
source of truth and md_num as excluded from the restat. Since #583 the
curve is KINGDOM_CAPS with slot and band ratios, CULTURAL_MODS carries
weight and the legacy proportions only, the Black Numenorean generator
passes item_id so it authors at the line's 57 cap, and the md_num
exclusion is lifted. Held back from the #583 docs commit while another
session had the files open.

#### docs(supply-lines): CHANGELOG entry, feature-map row, review 106 (#587)

`8c8c40a2`

Shared files, landed on their own after the other sessions' hunks in the
same files reached HEAD, so each commit carries one session's work.

#### fix(tools): the kingdom-cap curve after its Codex pass (#583)

`4bc5e859`

Codex (gpt-6-astra, ultra) on e4de8a78..b41721ab: no P1, four P2,
three P3, all confirmed against the source, the live Armory and the
installed DLLs; zero false positives.

check_kingdom_curve_invariant proves the PRIMARY stat only; the
docstring, the feature doc and the CHANGELOG had promoted it to "no
legendary roll passes the elite piece". A secondary keeps its item's
own ratio, and where it is small the flat bonus outgrows the gap (a
legendary medium Isengard pauldron rolls arm 5 + 5 against elite 7);
the live sweep lists 50 roster-backed cases, all inherited. Prose
corrected, the cases go to the roster pass.

tier_from_value takes item_id and routes through calculate_stats, so
the overview's reserve list and the --weights-only path judge a
sub-line item on its own cap: 62 Dol Guldur items in the rhun folder
had read as Rhun heavy and left Rhun's reserve (114, now 176).

The overview labels its curve view as the generic benchmark it is and
ends with two observation tables for the roster pass: kit off the
culture's line (162 rows over 33 troops; mordor_num_ and mordor_uruk_
troops are held to their own lines as ranged_ladders.json routes
them) and uncurved vanilla kit above the culture's elite slot value
(11 rows). Docs: the display tier steers workshop production
(DefaultItemCategorySelector, WorkshopsCampaignBehavior); four stale
How-To rows and one loader comment corrected; the per-apply rounding
convention recorded.

Tests: 4 new, tools suite 1,458 green. RCA section, Review 104
paragraph, track-record essay, two lessons.

Research: DefaultItemCategorySelector.GetItemCategoryForItem,
WorkshopsCampaignBehavior production loop (v1.4.8 dump)

#### feat(supply-lines): search every market from the order screen (#587)

`8b457cb6`

The supply order screen made the player pick one settlement and read
its goods; finding who stocks a good meant clicking through close to a
thousand rows. A text box above the source column now swaps the
settlement list for hits, one per good at a source, nearest first then
cheapest, with stock, price and distance; a click selects that source
with the good at the top of its list and the goods pane reset.

The box is vanilla's EditableTextWidget, TAOM's first. It writes each
keystroke into the VM's public setter; the VM notifies the raw value
back (an echoed trimmed string re-enters the widget mid-update and
drifts its visible text from its real one). Matching is the
encyclopedia's rule: diacritics stripped, case ignored, substring. The
catalogue is built once per screen open from the orderable settlement
rows and reused, since campaign time is frozen under the pushed state;
SupplyGoodsSearch is pure and stable, capped at 60 with the total in a
status line. A pending quantity locks every hit: a hit from the
selected source would repopulate the goods and wipe the order. Typing
never runs Recompute. IsSearchActive is get-only because both IsHidden
and IsVisible bindings write back through public setters.

Underneath: GetGoods lists every IsTradeGood item (the port had kept
the donor's food-only filter) and drops the top-14 cut that would have
hidden the very good a search found; only unmodified stacks are listed.
The consume counts and deducts on the same unmodified element and
records the take only when AddToCounts returns an index (it returns -1
and removes nothing when the stack is absent).

Reviews: a Plan-agent stress test before implementation removed four
plan defects; the five-agent deep review found one MED (the consume
parity) and two efficiency MEDs; Codex (gpt-6-astra, ultra) found the
wheel momentum that survives a repopulate (the screen now hands the VM
the pane's ResetTweenSpeed) and a wrong doc claim about vanilla's
Escape handling (TAOM now takes vanilla's IsFocusedOnInput guard).

Six taom_sl_search_* / taom_sl_hit_* keys registered and seeded in the
12 languages; the paid translation rides #508.

Research: ItemRoster.AddToCounts/FindIndexOfElement, EquipmentElement
.IsEqualTo, ScrollablePanel.UpdateScrollablePanel, EditableTextWidget
.Text, GauntletView.RefreshBinding, WorkshopsCampaignBehavior
.GetRandomItemAux, ItemObject.IsTradeGood (installed 1.4.8)
Not-tested: rendering of the text box, the hide/show swap, the hit rows
and the goods scroll landing; the first-keystroke build on the full map
Save-compat: none, no persisted field changed
RCA: docs/reviews/rca-supply-search-2026-09-13.md

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### fix(tools): sync the clan_heraldry specs from the live files; applying them is a no-op

`66ecc715`

generate_clan_heraldry.py replaces a whole <MBPartyTemplate> from
clan_heraldry/<culture>.json, and the specs had not moved since
August: 176 of 192 rosters and six Gondor template_ids were behind the
live files (the 2026-08-14 and 2026-09-01 retargets, the Black
Numenorean houses, the ladders and #584 all edited the templates
directly), so the guard widened in #584 refused 19 of 21 specs.
build_clan_specs.py composes rosters from scratch, so it is not the
way back.

New tools/sync_clan_specs_from_live.py copies each clan's live binding
into template_id and the live stacks into roster, leaves colours alone
(current on all 192 clans), keeps each JSON's line endings, trailing
newline and escaping, stamps a _rosters provenance note, and refuses a
clan with no live faction, a faction that binds no template, or a
binding to no template. Applied: 182 clan changes across 19 files;
bandits and khand untouched. Two generator defects surfaced on the
way and are fixed: render_template joined with LF inside the CRLF
party file, and write() put a BOM on characters/clans.xml, which has
none. Proof: --all --apply is byte-identical on all three live files,
and tools/tests/test_clan_heraldry_specs.py fails on the first
spec-vs-live divergence or the first non-no-op apply. Docs now state
the direction of flow (clan-heraldry.md, doc-lookup, INDEX,
feature-map, README).

Closes #589

Not-tested: nothing in game changes (JSON specs and tooling only)
Research: Clan.DefaultPartyTemplate falls back to the culture template (1.4.8 Clan.cs:112-122)

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### docs: CHANGELOG, gate row, reviews 103 and 104, lessons, README (#581 #583)

`b41721ab`

The shared files, staged as HEAD plus this session's hunks only:
CHANGELOG.md (the #583 entry under 2026-09-13, the #581 entry under
2026-09-12), the CROSS_CULTURE_ARMOUR_INVERSION row in the
moduledata-validation rule, REVIEW-LOG.md reviews 103 and 104, two
lessons in build-tooling-workflow.md, and the tools/README.md rows for
analyze_kingdom_armour.py, rebalance_armor.py, derive_armor_tiers.py,
analyze_armor_balance.py and the Black Numenorean generator.

#### docs(armour): the kingdom-cap curve, the gate and two reviews (#581 #583)

`44c718d5`

armor-balance.md: the kingdom-cap curve (cap table, slot and band
ratios, routing, anchor precedence, how to restat and re-derive, the
decisions taken), the cross-culture ladder section from #581 with the
measured findings, tests. moduledata-validation.md: the
CROSS_CULTURE_ARMOUR_INVERSION section with the item-level reference
cap scaling, validator test count. items-armor.md step 4 points at
KINGDOM_CAPS. gondor-ithilien-ranger.md: the troop is level 51 / tier
10 on disk. gondor-armor-revamp.md: stale-for-values note.
new-culture-authoring.md: the tier cap line names the armour cap as a
separate decision.

Reviews: rca-kingdom-armour-2026-09-12.md (#581 deep review, two
MEDIUMs in the report) and rca-kingdom-cap-curve-2026-09-13.md (#583
deep review, six findings, all fixed), plus the Codex prompt for the
#583 adversarial pass.

#### fix(data): ladder repair after the kingdom-cap restat (#583)

`c0efbbae`

The re-curve exposed 58 promotion edges where a child wore kit
anchored low by a shared wearer (UPGRADE_ARMOUR_REGRESSION).
fix_upgrade_armour_regressions.py --apply resolved all 58 with 197
slot swaps in 12 troop files; the following derive + restat iteration
moved 7 items and the next was stable. Equipment ids only, no other
attribute touched.

Capstone medians after: Gondor tier 9 251 (was 194), Erebor 335, the
elves 384, Mordor 270, Rhun 288, Dol Guldur 243, Gundabad 248,
Isengard 209, Rohan 203, Dale 190, Dunland 165.

Not-tested: in game (owed: a full restart and the party screen)

#### feat(armour): kingdom-cap curve, kingdom overview and gate (#581 #583)

`e4de8a78`

#581: nothing compared kingdoms with each other. New read-only
tools/analyze_kingdom_armour.py puts every culture's troops side by
side by engine tier (the four engine regions summed over the five
armour slots as Equipment.Get*ArmorSum does, averaged over battle
sets): culture x tier matrices, cross-culture inversions, Armory
ceilings and the unworn reserve, a curve view and the validator's own
verdict. CROSS_CULTURE_ARMOUR_INVERSION (warning) holds a culture's
tier median against the other kingdoms' medians two tiers lower; the
rule is one pure function both callers share. Loader changes are
additive (load_item_armour keeps stats, Type and name; load_troops
keeps name, group and has_level); the mumak rider is a mount rider.

#583: the maintainer replaced the curve's design. rebalance_armor.py
now carries one chest cap per kingdom (KINGDOM_CAPS, Erebor 70 to
Thenn 35), slot ratios (helmet .9, bracer and pauldron .6, greaves .5)
and band ratios (.40, .64, .84, 1.0, lord = elite); sub-lines sharing a
folder are routed by id prefix (the three Mordor lines, sk_dg_ in the
rhun folder, the misfiled uruk scouts, Ardunian kit). --tier-source
roster-first bands a worn item by its lowest battle-set wearer even
over an id keyword; civilian sets and ladder-exempt troops never
anchor; the Black Numenorean exclusion is lifted (the line has its own
cap) and the Khamul-named Dol Guldur line is not hero kit. Secondaries
keep their ratio, weights and material types stay, the arm, leg and
shoulder loot tables share a medium-at-cloth ladder so no legendary
roll passes the elite piece (check_kingdom_curve_invariant). The writer
is a binary round-trip with one .bak-<tag> per file that skips
commented copies. derive_armor_tiers is anchor first like the writer;
the gate and the overview scale every item to the 57 reference cap of
its own line through Registries.item_folder and item_cap_for. The BN
generator passes item_id so it authors at 57, not the orc 38.
armor_roster_tiers.json regenerated anchor first.

Applied to both Armory trees 2026-09-13 (2,510 items, then 7); a dry
run afterwards plans nothing.

Research: Equipment.GetHeadArmorSum and siblings, ArmorComponent,
ItemModifier.ModifyArmor, DefaultItemValueModel.CalculateArmorTier on
the installed 1.4.8 DLLs
Not-tested: the party screen on the capstones (needs a full restart)

#### docs(review): RCA, review 105 and two lessons for the Black Numenorean confinement and the weight band

`2e7ebdc6`

Six-agent deep review (one MEDIUM: the heraldry spec guard went blind
once the ids it keyed on were deleted) and a Codex pass at ultra (no P1
or P2 in the changeset, four wrong numbers, one pre-existing P2 in the
float subtraction behind the party-size penalty). Lessons: a guard on a
file is keyed on the invariant it protects and is re-run when a change
deletes data; a test asserts in the frame the consumer reads, on the
values the game ships. The raw Codex transcript stays in the ignored
docs/reviews/raw folder.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### feat(balance): troop weight 3.0 for every level 41+ elite

`661a1012`

troop_weights.xml is a flat per-id lookup with no level table; 3.0
covered ten level-46/51 capstones and 93 troops from level 11 to 46
all paid 2.0, with the file arguing that mounted branches never
escalate with tier. The rule is now level-based: a listed troop at
level 41 or above pays 3.0, below that 2.0, the two mount packages
outside it. 41 rows moved (15 at level 46, 26 at level 41), so the file
holds 52 at 2.0 and 51 at 3.0. An all-level-41+ party fields base/3
raw troops instead of base/2. TroopWeightLevelBandTests pins the band
from both sides and fails on a weighted id that resolves to no troop.
The MCM hint names the bands.

Codex (review 105) found a pre-existing defect the larger weights
reach sooner: SubtractResultFramePenalty divides the penalty by
1 + SumOfFactors and the engine truncates ResultNumber, and
(B - p/s) * s is not B*s - p in float, so Gondor's 0.025 feat read an
intended cap of 70 as 69 and the promised floor of 1 as 0. The
subtraction is probed on a copy of the struct and lifted by 0.001 in
the result frame only when it would undershoot the intended integer;
ResultFramePenaltyTests asserts the truncated cap on the four
reproduced cases and that a fractional base keeps its fraction.

Closes #585

Not-tested: the party screen's weighted frame in game (full restart owed)
Research: TaleWorlds.CampaignSystem.ExplainedNumber.Add / ResultNumber, PartyBase.PartySizeLimit
Save-compat: weights load at process start; no save field changes
RCA: docs/reviews/rca-black-numenorean-confinement-2026-09-13.md

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### fix(data): Black Numenoreans field only for the two houses, Sauron and the vassal reward

`1ede207b`

Every Mordor orc and uruk lord spawned a token handful of Black
Numenoreans: the 13 mordor_num_* stacks sat in all thirteen non-house
clan templates and in the culture default (the pre-2026-08-25 "4%
sprinkle" the two-houses change left in place), plus one infantry in
the level-3 patrol. All 183 stacks are gone. The line now spawns in
exactly four templates: clan_empire_south_1 Dolgubeth and
clan_empire_south_9 Wawrim (unchanged), Sauron's own (#580), and
vassal_reward_troops_mordor (the player's route into it).
rebalance_party_template_maxes.py --apply pulled the fourteen edited
lord templates back to the 260 ceiling; min_value untouched.

BlackNumenoreanPartyTemplateTests pins the four templates, the two
houses' full 13-id roster, and the id list against troops_mordor.xml.
wire_black_numenorean_troops.py adds any stack a template lacks and
would have re-sprinkled on the next run; it now targets the two houses
and the reward. Deep review found the confinement blinded a second
guard: generate_clan_heraldry.py refused a stale spec only when it
would drop a troop id, and for 13 templates the live id set now equals
the stale mordor.json; it also refuses a spec whose max_value sum is
below the live template's (19 of 21 specs, all pre-retarget), with the
tool's first tests.

Closes #584

Not-tested: in-game spawn composition (new campaign smoke owed)
Research: DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty, PartyTemplateObject.Deserialize, Clan.DefaultPartyTemplate
Save-compat: templates shape new lord parties only; saved rosters persist
RCA: docs/reviews/rca-black-numenorean-confinement-2026-09-13.md

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### fix(tools): ranged page review, militia cache

`0e371c6b`

Six review agents on the two #582 follow-ups, no HIGH, eight fixes.

The page's Spread column used the bow's skill factor for crossbows too;
DefaultSkillEffects sets CrossbowAccuracy at -0.0005 per level against
BowAccuracy's -0.0009, so every crossbow row read 6 to 9% too tight
(ACCURACY_FACTOR per class, a test per class, page text names both).

docs/reference/ranged-troops.html carried a wall clock, so every
regenerate dirtied the repo, and had no document skeleton or charset,
unlike every other tracked HTML under docs/: now a full document with
<meta charset>, no clock, byte-identical across runs, and pinned LF in
.gitattributes. A row's ammo now comes from the sets that field the
chosen launcher instead of the strongest quiver anywhere on the troop
(inert on today's 227 rows). militia_troop_ids re-reads when the elite
cache lacks its entry instead of leaving elite_militia_troop_ids to
raise KeyError; a troop file that does not parse is reported, not
dropped. Stale prose corrected: "flat by design" in the monotonicity
test and troop-skill-balance.md, the C# test's regex-lockstep comment
(comment only; TAOM.Tests builds, 0 errors), the page's `difficulty`
wording and the ammo comment in ranged_ladder.py.

Deferred: six earlier subjects exceed 50 chars; a rebase over unpushed
history is off limits while another session holds uncommitted work
here. RCA: docs/reviews/rca-ranged-troops-report-2026-09-13.md

#### fix(data): veteran militia stand 15 above the basic militia on every skill

`d5e43caf`

Every militia slot a culture binds takes the level-21 baseline whatever
the troop's level, on purpose, but the four slots shared it exactly:
a culture's militia archer (level 11) and veteran militia archer
(level 16) were the same eight numbers in all 15 cultures, and the
promotion changed nothing on the party screen.

rebalance_troops.py keeps the elite ids (*_elite_militia_troop, both
binding encodings) apart from the basic ones and adds
MILITIA_ELITE_BONUS (15; the user asked for 10 to 20) to every skill
before cultural modifiers. --fix-monotonicity --restat over the 30
elite ids rewrote exactly those 30 troops in 15 files, +15 per skill
(+120 a troop, +115 where a skill had been floored at 0), nothing else.
docs/reference/ranged-troops.html regenerated for the 15 veteran
archers.

Not-tested: in game; the values are the curve's, applied by the
established writer, and validate_moduledata.py reports 0 errors.

#### feat(tools): the ranged roster as a tracked HTML document (#582)

`b2e058e8`

rebalance_ranged_ladders.py now writes REPORT.html beside REPORT.md and
a tracked copy at docs/reference/ranged-troops.html (tools/reports/ is
gitignored): every troop that carries a bow or crossbow, per kingdom in
ladder rank order, with the skill it brings beside the weapon it fields.
Speed, drag-model reach, accuracy, WeaponInaccuracy spread, AiShootFreq
cadence, the mounted open-fire distance, bow plus ammo damage and shot
count, each column sortable, a search box and class filters, both
themes. ranged_ladder gains index_ammo (arrow and bolt damage adds to
the bow's at launch, Mission.cs:4932) and the troop display name.

Research: SandboxAgentStatCalculateModel.GetWeaponInaccuracy,
AgentStatCalculateModel.SetAiRelatedProperties, Mission.cs:4932

#### docs(ranged-ladders): feature doc, gate row, RCA, lessons, CHANGELOG (#582)

`29291bbd`

docs/features/ranged-ladders.md: the engine facts (what range is and
is not), the two rules, the 18 lines, the grid and its knobs, the item
naming and loc rows, the roster rewrite, the trade-offs the ranking
imposed, the smoke checklist, the owed translator run.

RANGED_LADDER_INVERSION rows in the moduledata-validation rule and
feature doc; tools/README rows for both tools; feature-map and INDEX
entries; a CLAUDE.md trap row (the 130 items live in the unversioned
Armory, a reinstall drops them and archers spawn bowless, no error).

docs/reviews/rca-ranged-ladders-2026-09-12.md: the six-agent review,
five confirmed findings (validator cost, band_base parse, files
tokens, prefix overlap, MaxCharacterTier attribution) and one doc
coupling, with why each agent missed what. Lessons: reach is
missile_speed and a ladder needs a gate (data/content); a spec that
names external things is validated against them (tooling).

#### feat(validate): RANGED_LADDER_INVERSION, the two ladder rules as a gate (#582)

`1083a8b0`

Registries.launchers indexes every Bow and Crossbow item over the same
item roots as the armour index (build_launchers, empty without the
install, so the check is skipped, never faked). The gate reads
tools/ranged_ladders.json and calls ranged_ladder.inversions, the
same pure function the report and the roster tool use, so the three
cannot disagree: one warning per (rule, class, scope) naming the worst
pair. A spec that cannot be read, contradicts the install, or claims a
troop file that is not on disk, and a ranged troop no line claims, are
findings rather than silence. _RANGED_LADDER_EXEMPT holds off-ladder
troops with a reason each (empty today).

Pre-rewrite run: 1,741 pairs; post-rewrite run: none. Warning, not
error, because the Armory the speeds come from is unversioned.

#### feat(data): every troop bow and crossbow is its ladder cell (#582)

`f46abb62`

python tools/rebalance_ranged_ladders.py --apply: 258 launcher slot
edits over 227 troops, 559 id lines in 16 troop files, every
Item0..Item3 bow or crossbow now ladder_<line>_<bow|xbow>_<band>.
Ammo slots untouched, no class swapped, civilian sets skipped, every
rewritten set still carries ammo of the matching class (data-flow
review, 559 of 559). Inversions 1,741 to 0; the tool's rerun reports
0 pending edits; validate_moduledata.py 0 errors.

The ranking moved some lines a long way on purpose: Dol Guldur 85-92
down to 62-78, Rohan 70-85 down to 60-72, Dunland's Dragon crossbows
97 down to 66; Isengard's uruk crossbows 60 up to 82-90, Dale's royal
crossbowman 65 up to 92, the Moon Guard 85 up to 104.

The 130 items the rosters name live in the unversioned live Armory
(LOTRLOME_items/<folder>/ranged_ladder.xml, 13 folders) and the
lotraom-assets mirror; generate_ranged_ladder_items.py --verify is
the reversion gate. A new item file loads only at process launch.

Save-compat: no save field changes. Regular troops read their rosters
from XML at every launch, so an existing save fields the ladder bows
in its next battle; a save loaded on an install without the 130 items
spawns those troops bowless.

#### feat(tools): ranged range ladders, one grid for every troop bow (#582)

`d20838e4`

An archer's reach is the launcher's missile_speed and nothing else:
Mission.cs:4943 launches at the wielded bow's speed, the Sandbox stat
model pins MissileSpeedMultiplier at 1 for bows, and Bow skill feeds
accuracy, cadence and AI error only. The 227 troops carrying a bow or
crossbow had no order: six trees handed a higher tier a slower bow,
seven kingdoms' militia carried vanilla noble_long_bow over their own
regulars, Dol Guldur outranged Gondor. 1,741 inverted pairs.

tools/ranged_ladders.json holds the grid: bands by engine tier (E T0-2
to C T9-10), 18 kingdom lines in rank order (Mirkwood first, Dunland
last; Ithil Guard, Ithilien Ranger and Blackroot Vale a Gondor special
line, Black Numenoreans and Black Uruks their own Mordor lines, Lindon
shares Rivendell, Umbar under Rhun), donors per class and per band.
speed = band_base[band] + rank_step * (18 - rank).

tools/ranged_ladder.py is the shared library (tier, band, line, grid,
launcher index, troop loader, the two rules as one pure inversions()
function, planners, drag-model reach). generate_ranged_ladder_items.py
writes one ladder_<line>_<bow|xbow>_<band> item per cell, 130 in all,
each a verbatim clone of the line's donor with id, name, missile_speed
and is_merchandise=false changed, plus English name rows in the
Armory's loc_<folder>.xml, into the live Armory and the assets mirror
(--apply, --verify, --revert). rebalance_ranged_ladders.py reports the
grid, both rules' inversions and every ranged troop's reach before and
after, and --apply moves every launcher slot onto its cell through
fix_upgrade_armour_regressions.write_changes.

Deep review (six agents, no HIGH): the launcher index parses only
files whose bytes mention a bow or crossbow (1.2 s to 0.18 s on the
validator's roots); a non-numeric band_base is reported, not raised;
a files token naming no troop file and prefixes overlapping across
lines are spec findings.

Research: Mission.OnAgentShootMissile, SandboxAgentStatCalculateModel,
AgentStatCalculateModel.SetAiRelatedProperties,
ItemObject.GetAirFrictionConstant, DefaultCharacterStatsModel.GetTier
Not-tested: the native GetMissileRange (reach figures are a drag-model
estimate for reports only, never a gate input)

#### docs(memory): three live runs fill the audit's Phase 2, the menu ladder and the graphics diff

`255daa88`

The commit-attribution runbook's Phase 2 table read _pending_ since
2026-08-05. Three sessions on 2026-09-12 fill it and settle the questions it
was written for.

Run 1 (45 min, dose-response protocol): the engine's own accounting has no
category breakdown; the map baseline is 85% Private Data and 2% mapped file,
so pack splitting cannot move it; the encyclopedia, saving and the paused map
are refuted as sources; every mover is a bounded first-touch cost that ten
repeats did not grow; the running campaign plateaus at ~15 MB/min.

Run 2 (new campaign, Minas Tirith, reload): a VMMap inside the town diffs the
map's large regions out at 4,264 MB, one of them named (the 16K vista) and
~90 of them load-time generated; geometry is 363 MB of the map and 929 MB of
the town; the [SaveLoad] phases split a load into 26 s deserializing, 25.6 s
of CPU with +1 MB, and 7 s / +1.76 GB of map-screen construction; the
inventory's 2.2 GB of managed heap is rooted across the engine's collections.

The menu ladder (five launches, verified by engine-log module counts): the
6.6 GB floor is engine and vanilla ~4.4 GB, the Armory ~0.97, TAOM main ~0.7,
TAOM_Map ~0.5; the sprite atlases' eager load is not measurable in it. The
graphics diff: foliage, terrain and texture quality at their minimums drop the
settled map 12,787 to 9,734 MB on a live switch, split texture 2.3 GB,
foliage 0.77, terrain 0.2; the largest lever found and it needs no content
change.

Also: the deep review and Codex pass of the instruments (REVIEW-LOG 101, RCA
with three findings, two lessons), the Codex prompt, the review-reference
lesson, and a launchSettings.json profile that starts the game without TAOM's
main module for ladder rungs.

RCA: docs/reviews/rca-memory-instruments-2026-09-12.md

#### feat(tools): audit_map_scene_memory.py weighs the campaign map's asset payload

`e49fd45e`

Offline manifest of what the Main_map scene references: textures, meshes,
materials, collision bodies, prefabs and flora, decoded from references.txt,
scene.xscene, atmosphere.xml, flora.bin and the tpac tables of contents across
TAOM_Map, Native and SandBox, with resident bytes per asset and flags for
editor-segment bloat and mip chains present only in EmAssetPackages. Main_map
references 8.39 GB (TAOM_Map 2.34 GB of textures and 1.73 GB of meshes)
against ~4.4 GB measured resident, so the engine streams or drops part of it.

Its sizes fingerprint VMMap: a Private Data region of (mip chain + 43,664 B)
is one texture held in system memory. The 341 MB region on the map is
16K_Vista_02, and the regions of 22,413,312 B are 4096-square 13-mip textures.

Not-tested: which mesh segment the engine keeps resident (both are reported,
the question is marked UNKNOWN); texture-header decoding is pinned on real
metadata blobs and verified against the pixel segment on 3,582 of 3,583
checkable textures

#### feat(diagnostics): probe splits mesh from texture, save-load phases carry memory

`196f6fe1`

taom.print_memory now reads Utilities.GetVertexBufferChunkSystemMemoryUsage
(bytes: 363 MB on the map, 929 MB in Minas Tirith) and the GetGPUMemoryStats
split (all zeros in the shipping client), rendered as raw engine units, and
reports a GPU dump path only once the file exists: the engine call is void and
writes nothing, so the old line claimed a file that never existed.

ProcessMemoryTokens is the gc/heapMB/privMB/wsMB tail extracted byte-for-byte
from BattleLoadDiagnosticsService.MemStats, now also the detail of two new
[SaveLoad] phases stamped from the MBSubModuleBase overrides: GameLoaded
(saved campaigns only, before LoadBehaviorData) and GameInitializationFinished
(every loading type, after RegisterEvents), the bisecting stamps #509 asked
for. The first live run measured 26 s deserializing, 25.6 s of CPU with +1 MB,
then 7 s and +1.76 GB of map-screen construction.

A heap release on OnPopScreen for the inventory, party and character screens
was built in the same pass and removed before this commit: the Codex review
showed GameStateManager.OnPopState and OnPushState already end with
Common.MemoryCleanupGC(), so it doubled the engine's own collection, and the
2.2 GB it targeted survives those collections, so it is rooted, not garbage.

Research: Campaign.OnInitialize (installed v1.4.8, lines 1420-1452),
GameStateManager.OnPopState/OnPushState (278/306), the Utilities memory statics
Not-tested: the SubModule wiring and the engine statics behind the reader
Rejected: GetGpuMemoryOfAllocationGroup, the native binary has no group names

RCA: docs/reviews/rca-memory-instruments-2026-09-12.md

#### feat(lords): per-hero party templates (#580)

`6bf93e51`

The engine binds a lord's party template to his clan, never to him:
InitializeLordPartyProperties (v1.4.8 LordPartyComponent.cs:38) and the
new-game top-up in HeroSpawnCampaignBehavior.SpawnLordParty (:265) both
read Clan.DefaultPartyTemplate. Faramir shares his clan with Denethor
and Boromir, Sauron shares his with Herumarth and Naktharil, so a clan
rebinding would have changed four other lords.

New feature LordPartyTemplates: lord_party_templates.json maps a hero
id to a template id; Patch88 postfixes the clan getter, live only while
a lord spawn is in flight, marked by prefix and finalizer scope patches
on SpawnLordParty and InitializeLordPartyProperties (__state save and
restore, because they nest). The finalizers return the exception
untouched so Patch65's conditional swallow on the same method keeps
deciding. The decision is pure; an unresolvable template leaves vanilla
in place and warns once per hero.

Data: kingdom_hero_party_gondor_faramir_template (max 200: 100 Ithil
Guard ranged with the Ithilien Ranger, 60 Ithil Guard foot, 40 Anorien
horse) and kingdom_hero_party_mordor_sauron_template (max 260: 104 low,
78 mid, 78 high by level, Uruks and Black Numenoreans). Both unbound by
any clan or culture; a test enforces it. Clanmates keep their rosters.

Deep review, five agents: one MEDIUM fixed (a closure per in-scope
read), RCA docs/reviews/rca-lord-party-templates-2026-09-12.md. Suite
8,771 green, validator 0 errors. In-game smoke owed before #580 closes.

Research: Clan.DefaultPartyTemplate, LordPartyComponent.InitializationArgs, HeroSpawnCampaignBehavior.SpawnLordParty, MobileParty.CreateParty
Not-tested: Harmony __state hand-off from prefix to finalizer, and the swapped template reaching FindAppropriateInitialRosterForMobileParty (live game)
Save-compat: no new saved state; an existing party keeps the roster it has
Rejected: rebinding clan_empire_west_1 or clan_empire_south_3 (changes four other lords); a new clan for Faramir; a transpiler on the top-up read

#### fix(gamemodels): update total count of GameModel overrides to 48 fix(skill): clarify hints for fast-forward multipliers in settings docs(feature-map): update TimeAcceleration and Enlistment feature descriptions

`428709ee`

#### feat(wanderers): wanderers refuse to serve an opposed-alignment player (#575)

`c6896322`

Players reported Aragorn, Legolas and Gimli ending up in evil clans. No
v1.4.8 path moves a wanderer into an AI clan: every AddCompanionAction
call site passes Clan.PlayerClan, the AI commander picker requires
Occupation.Lord, heirs exclude IsWanderer, governors come from AliveLords,
and TAOM's own companion minting targets the player clan too. The evil
clan is the player's own, entered through the vanilla hire dialogue,
which never asked which side either party was on.

A wanderer now refuses across the Free/Evil line, keyed on the hero's
culture; the player's side is the kingdom they serve, then clan culture.
Symmetric, Neutral serves anyone, hire-block only. No Harmony patch: two
condition-gated NPC lines on vanilla's companion_hire token at priority
110 pre-empt the priority-100 reply, because GetSentenceOptions returns
the first true NPC line in priority order and that token has exactly one
producer and one consumer. MCM group World/Wanderer Allegiance (master
toggle, scope dropdown for the 17 named companions).

Also: co-op fingerprint pins and docs moved for the two new settings;
stale alignment-consumer counts corrected in two docs; backlinks footers
regenerated; Review 100 and a lessons entry (a singleton behavior does
not accumulate listeners across campaigns: CampaignEvents is per
campaign).

Research: LordConversationsCampaignBehavior.AddHeroGeneralConversations,
  ConversationManager.GetSentenceOptions/SortLastSentence,
  HeroSpawnCampaignBehavior.GetBestAvailableCommander, Clan.GetHeirApparents
Not-tested: the dialogue itself (live conversation); seven smokes listed
  in docs/features/wanderer-allegiance.md
Save-compat: no persisted state

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### fix(enlistment): hold the party while its battle encounter is open (#577)

`fe4cd4a6`

At x64 fast-forward the reconciler could park the enlisted party while the
battle's PlayerEncounter was still open. A park clears AttachedTo, which
pulls an active party off its MapEventSide, and "Send troops" then indexes
BattleSimulation.SelectedTroops[-1]. Assess admitted EnlistedBattle and fell
through to AttachRequired in the loot and aftermath window; the trigger was
TAOM's own army disband raising the "army left" edge the reconciler
subscribes, from inside the join or inside PlayerEncounter.Finish.

Assess now returns Blocked(BattleEncounterOpen) whenever the state is
EnlistedBattle and an encounter is live, above commander fitness and the
settlement exit. IArmyMembershipAdapter.IsMutating stops the reconciler
re-entering from our own join or leave. The deep review then found that the
save coercion (EnlistedBattle persists as EnlistedAttached) defeated every
gate keyed on the state after a reload; EnlistmentLoadNormalizer restores
EnlistedBattle at load when the party is in a map event or its battle
encounter is still open. Two pre-existing detach paths are recorded on the
issue rather than fixed. Feature doc rewritten for both fixes; RCA at
docs/reviews/rca-enlistment-captaincy-hold-2026-09-12.md; follow-up #578
for the campaign-time thresholds that collapse at high multipliers.

Research: MobileParty.SetAttachedToInternal, BattleSimulation, PartyBase.Side,
Army.DisperseInternal, Campaign.TickMapTime, MBCampaignEvent, GameMenu.StartWait
Not-tested: the reload shape and the x64 join in game; smokes owed in the
feature doc; the reporting player's diagnostics log is still wanted
Save-compat: none, no persisted field changed

#### fix(enlistment): the soldier never sees the deployment screen (#576)

`86f47526`

The 2026-08-12 army join (#443) made MapEvent.IsPlayerSergeant() true and
only its first consumer was audited. The second is the engine's deployment
gate, so the Order of Battle screen opened at every rank, the sergeant-choice
view auto-selected the player as captain, and AssignSergeant set
Formation.PlayerOwner, which turns that formation's AI off while the role
strip refuses the order UI. Nobody could command it.

TaomBattleInitializationModel keeps the screen shut through the same
BattleCommandPolicy gate as the role strip (false when the soldier fights in
someone else's battle, else base). The Sergeant carve-out and the dead
OnDeploymentFinished belt are deleted: SetPlayerRole has two engine call
sites, both before TAOM's AfterStart. With the screen shut, vanilla's own
captain and general assignment still moves the player, so
EnlistedSoldierPlacement.ReclaimAfterDeployment clears any captaincy handed
to him and re-places him after every OnDeploymentFinished handler.

Also regenerates the API snapshot, which had not picked up Patch87 (#566).
Docs for the feature file land with the #577 commit.

Research: SandboxBattleInitializationModel, BattleInitializationModel,
DeploymentMissionController, AssignPlayerRoleInTeamMissionController,
GeneralsAndCaptainsAssignmentLogic, Formation, Team (installed 1.4.8)
Rejected: SetBypassPlayerDeployment (process-global static); a prefix on
AssignSergeant (fixes the freeze, leaves the screen open)
Not-tested: the mission-time hooks and the model override (engine boundary);
in-game smokes owed in docs/features/enlistment.md "Testing"
Save-compat: none, no persisted field changed

#### fix(time): map bar Extra Fast Forward applies the multiplier (#574)

`a00713b5`

The button bound vanilla ExecuteTimeControlChange(2) directly, which sets
the time mode and never writes Campaign.SpeedUpMultiplier, the only value
TickMapTime scales by. Players saw the MCM slider do nothing; the E key,
which goes through the service, worked. #168 had shipped this in May as
"Option A, redundant with vanilla".

Both map bar fast-forward buttons now bind mixin commands that call
ITimeAccelerationService (EnterExtraFastForward / EnterFastForward,
behind vanilla's own menu gate via the new IsWaitMenuActive adapter
member) and then the vanilla call. Rebinding vanilla's button closes the
sticky hole where one E press left the extra value in place. The lit
state hooks MapTimeControlVM.Tick (RefreshValues is constructor and
gamepad change only). The provider floors extra at fast.

27 new tests; full suite 8629 green; five-agent deep review clean.
CHANGELOG entry, TaomSettings hint texts, feature-map row and the
deep-review skill item follow in a separate commit: those files hold
other sessions' hunks right now.

Research: Campaign.TickMapTime, MapTimeControlVM, MapBarVM.Tick,
  ViewModel.ExecuteCommand, UIExtenderEx ViewModelComponent (v1.4.8)
Not-tested: in-game click, per-frame refresh hook, wait-menu parity
Save-compat: none, SpeedUpMultiplier is runtime-only state
RCA: docs/reviews/rca-time-acceleration-button-2026-09-12.md

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### docs(changelog): add fortnight changelog for 29 Aug to 12 Sep releases

`612cb8a2`

#### chore(docs): restore LF endings on data-content-cultures.md

`ad9ba54f`

The previous commit appended three lessons but stored the whole file
with CRLF endings, which the checkout had and git did not normalise,
so history showed every line changed. Same content, LF blob again.

#### test(cc): pin the starter-kit invariant, harden the generator (#569)

`55baaed5`

StarterKitCoverageTests pins, with no allowlist, that every weapon and
armour slot in the three player roster files is a starter_ item, that
both equipment sets are rewired, that the non-player rosters are not,
that every vanilla-mapped title has an override roster, and that every
override roster carries _replaceWhileMerging on the roster element.
PlayerStartCoverageTests reads the override file and drops the
vanilla-six exclusion.

Deep review (six agents) found one HIGH in generate_starter_kit.py: it
read its donors from the rosters its own wiring step rewrites, so a
re-run after wiring planned nothing and --apply would have emptied the
marker blocks. Starter ids now map back to their donors in place, a
re-run over the wired rosters is a byte-identical no-op, and every
writer refuses to drop a starter id already on disk (per template for
the stylesheets) without --allow-shrink. Unparsable item files are
reported. RCA: docs/reviews/rca-starter-kit-2026-09-12.md.

Docs: starting-equipment-tuning.md rewritten; career-system.md's wrong
FillFrom claim corrected; character-creation.md, file-catalogue.md,
items-armor.md, load-order-and-dependencies.md, party-templates.md,
lord-identity-reconciliation.md, feature-map, INDEX, doc-lookup, a
CLAUDE.md trap row, four lessons.

Not-tested: in game (owed: full restart, four new campaigns)

#### docs(camps): sweep the stale docs after the #567 wait-menu fix and its Codex pass

`72fec2e5`

refuge.md carries the founding flow as it is now (paused picker,
context revalidation, exit to the map, then the deposit screen) and
the owed in-game walks. enlistment.md names the #573 gap the Codex
pass found in its own wait menu. doc-lookup gains a row for "a TAOM
menu with no options" and points the enlistment row at #573. The
yotthani RCA checklist records the Class 12 artifacts. The review-log
header was a hand count from 2026-09-01 (91 entries, four gaps); a
scan over every heading level and table row gives 93 of 99 with six.

#### data(cc): the player start hands out the starter kit (#569)

`537465d6`

Every player-start roster now names the starter_ twin instead of the
culture's real item: 2,230 ids across the 256 culture-default rosters,
234 across the 78 career rosters, no leftovers. The six vanilla-mapped
cultures get taom_player_start_vanilla_override.xml (70 rosters, same
ids as vanilla's, _replaceWhileMerging on the roster), registered in
SubModule.xml after the career node, so a Rohan player no longer starts
in a Calradian sword and hemp tunic. The parity ratchet's eight Mordor
entries move to the starter polearm id; the pair is unchanged in kind.

Gates: validate_moduledata 0 errors (every starter id resolves in the
live Armory), audit_polearm_shield_parity PASS, Python tools 1284 OK,
dotnet test over CharacterCreation, BannerBearers, Equipment and Troop
1208 passed.

Not-tested: in game; new item XML loads only at process launch
Save-compat: none, rosters are read at character creation only

#### feat(tools): rewire player-start rosters to the starter kit (#569)

`49858589`

wire_starter_kit_rosters.py repoints every Item0-3, Body, Leg, Cape,
Head and Gloves id in the 256 player_char_creation rosters (battle and
civilian sets) and the 78 player_career rosters at the starter_ twin
generate_starter_kit.py authors, by in-place attribute substitution so
the culture file's one-attribute-per-line formatting survives. Horse
and HorseHarness slots, the childhood, education and show stages and
the parents' rosters are left alone. The id rule and the roster filter
are imported from the generator, so the two tools cannot disagree.

It also writes taom_player_start_vanilla_override.xml: rosters with the
same ids as vanilla's for the six vanilla-mapped cultures, each roster
carrying _replaceWhileMerging="true". EquipmentRosters.xsd keys only
EquipmentRoster/@id, so a plain same-id roster would append a second
battle set and the adapter would keep taking vanilla's first; on the
roster element the engine drops vanilla's attributes and children and
takes ours whole. The kit is the mapped culture's career kit by title;
Khand borrows Rhun's until it has one (#571).

Research: MBObjectManager.cs 799-875, 1092 (1.4.8); EquipmentRosters.xsd
Not-tested: in game

#### fix(camps): pause the warden picker and revalidate its menu before founding (#567)

`2f4ce878`

Codex review 99 (GPT-6-Astra at ultra) on 4d2ea82b found the exit it
added had a second possible target. The warden picker opened with
campaign time running (ShowMultiSelectionInquiry defaults
pauseGameActiveState to false), and the query layer stays usable over a
map conversation, so an enemy reaching the camp could replace
taom_fc_camp with encounter_meeting under the picker; confirming then
ran Found and ExitToLast against the enemy's menu, and the
conversation's end had no menu to switch into the encounter from.

The picker now pauses the game (vanilla's alley-leader picker does the
same), and OnWardenChosen refuses, before ResolveWarden can promote a
soldier, unless the current menu is still the camp sub-menu and no
encounter is live. The two source pins from 4d2ea82b accepted a
commented-out exit and `args => true && false` (Codex O1); slices are
comment-stripped now and pin the whole argument or statement. Codex's
second P2 is pre-existing in Enlistment and filed as #573.

Suite: 8599 passed / 2 skipped / 0 failed. Review 99 in REVIEW-LOG,
Class 12 Codex table in rca-yotthani-camps-2026-08-23.md.

Research: MBInformationManager.ShowMultiSelectionInquiry, GauntletQueryManager.HandleQueryCreated/Closed, GameStateManager disable requests, AlleyHelper, all on installed 1.4.8
Not-tested: the in-game founding walk with an enemy arriving under the picker
Save-compat: none

#### docs(reviews): log review 98 for Return to Army

`08e40c20`

Entry 98 and the metrics in REVIEW-LOG.md, the essay at the top of
codex-track-record.md with the sixth-oldest essay rotated into the archive,
the reviewer reference's "what Codex does well" bullet and its review count,
and the dispatch prompt for the Codex pass.

#### fix(army): Return to Army exits the town (#566)

`b7a7f0a1`

Vanilla's "Return to Army" switches to the army wait menu and leaves the
settlement only for a village, and "Leave" is hidden for every army member
who is not the leader. A member who is in the army but not merged into it
(Army set, AttachedTo null: a lord marching to join, which a Player Switcher
takeover inherits untouched) therefore has no exit from a town or castle.
The wait menu's first tick re-routes to "You are waiting in X" in a
foreign-faction town or a wait nothing ends in an own-faction one.

Patch87_ReturnToArmy prefixes the consequence. A pure ReturnToArmyRules.Decide
runs vanilla for every row but one and, for an unattached member in a town or
castle, replicates vanilla's own Leave: gate position, LeaveSettlement,
Finish, SetMoveModeHold, autosave signal. Nothing writes Army. A throw up to
the gate write defers to vanilla; from LeaveSettlement on it skips vanilla,
because vanilla's own body dereferences the CurrentSettlement the leave nulls.
Time ends up stopped because Finish calls GameMenu.ExitToLast, which writes
Stop unconditionally (single-player; CoopNightly patches those writes out).

Deep review (5 agents): no HIGH or MEDIUM, two LOW fixed in-session. Codex
review 98 (GPT-6-Astra at ultra): no P1 or P2, two P3 taken. RCA:
docs/reviews/rca-return-to-army-2026-09-12.md. 12 tests plus a co-op veto
classification; filtered 17/17, full suite 8597 green bar the known
banner-bearer data failure.

The CHANGELOG entry, the docs/INDEX.md line, the campaign-mechanics lesson
and the CLAUDE.md category count stay in the working tree: those files also
carry another session's staged hunks, and the CHANGELOG hook couples
CLAUDE.md to CHANGELOG.md.

Research: PlayerTownVisitCampaignBehavior, PlayerArmyWaitBehavior, DefaultEncounterGameMenuModel.GetGenericStateMenu, PlayerEncounter.Finish, LeaveSettlementAction, Army.Tick, GameMenu.ExitToLast (installed v1.4.8)
Rejected: auto-marching to the leader via SetMoveEngageParty (declined by the reporter); restoring Leave for members (changes the attached case too)
Not-tested: the leave itself needs a live PlayerEncounter; five in-game smokes are listed in docs/features/return-to-army.md
Save-compat: none, no persisted state

#### feat(tools): starter-kit generator, weak twins of start items (#569)

`feae9143`

The player-start rosters named the cultures' real weapons: the Gondor
starter sword swings above the one-handed-sword median, the Rivendell,
Harad and Rohan starters sit near the top of the whole game, and career
bows run 83 to 99 against vanilla's hunting bow at 40. Troops keep the
real items, so every item a player-start roster hands out gets a
starter_<donor> duplicate with the same meshes and stats floored to a
per-class anchor, never above the donor.

A crafted weapon's damage is geometry times the blade's damage_factor,
so the clone is one new blade piece (tier 1, hidden from the designer)
on the donor's own fittings, registered in every stylesheet block the
donor blade appears in, Native's base files included. Plain items are
floored directly. Clones are not merchandise; crafted twins carry an
explicit value because the price model weighs the fittings' tiers and
iron grades at 40 percent.

Applied to the live Armory and the lotraom-assets mirror: 127 items in
14 starter_kit.xml files, 39 blades, 23 stylesheet blocks (two created).
Nothing references them yet; the roster rewiring follows.

Research: Crafting.cs 134-135, 363-381, 406-421; DefaultItemValueModel
41-49, 171-215; ItemObject.cs 425-482; CraftingPiece.cs 201
Not-tested: in game (new item XML loads only at process launch)
Save-compat: none, only new ids are added

#### fix(data): port the Gondor dead-ref repair (#568)

`782534fb`

The 2026-09-01 Gondor sword rebuild deleted 26 item ids from the live
Armory and left 238 dangling references in ten ModuleData files: every
one of those slots spawned empty, and the ModuleData commit gate has
been blocking on them since. Erkam repointed all 26 in the assets-repo
mirror of this module (lotraom-assets d7d5f75b; 66ac53be was the first
attempt, c89bc6d3 its revert). This ports that commit: eight files
verbatim, npcs_gondor.xml and taom_spcultures.xml three-way merged so
the skill_template, face and comment edits HEAD gained after his
snapshot survive. The same commit carries his Gondor troop re-roster
(Cair Andros, Minas Tirith and Pinnath Gelin armour, green shields,
crossbows, bastard swords, skill tweaks, two renames) and the Rhun
light-to-heavy plate swaps that match the #541 fix already here.

Two Gondor troop names changed under existing loc keys (#572).

Research: LOTRAOM_weapons.xml.bak-swordrebuild-20260901 holds the
deleted definitions; git merge-file dry run of the four stale-base files
Not-tested: in-game spawn of the re-rostered Gondor troops
Save-compat: none, only references changed and no id was removed

Co-Authored-By: Erkam <erkamk.y@gmail.com>

#### fix(camps): refuge founding no longer strands the player in a dead wait menu

`4d2ea82b`

Players who founded a refuge and deposited troops into its garrison came
back to "Choose how to make camp here." with no options: time controls
alive, party unable to move, and a save written there loaded straight
back into it (#567, every release from v2.0.24 to v2.0.28).

GameMenu.GetMenuOptionConditionsHold (installed 1.4.8) ANDs a wait
menu's condition delegate with every option's own condition, Leave
included: it is a visibility gate over the whole panel, not a
menu-validity hook, and nothing exits the menu when it turns false. The
camp sub-menu (a wait menu since 811a3429) registered that delegate as
PlayerCamp != null. Refuge founding is the one option on that menu that
removes the camp and stays: Found breaks the camp, the garrison deposit
screen opens over the still-open menu, and closing it refreshes a panel
whose every option fails the gate.

The wait condition is now true, which also rescues saves written in the
stuck state (MapState re-enters the persisted menu on load; the panel
renders Leave again). OnWardenChosen exits the camp menu before opening
the deposit screen, the landing Establish and Break camp already use;
the hold-nearby pin in RefugeService.FrameTick keeps the company at the
raise. Two wiring tests pin both halves and were red on HEAD. Players
on a current build can run taom.rescue_time from the console.

Suite: 8597 passed / 2 skipped / 1 failed, the failure being the
pre-existing banner-bearer live-Armory drift (wm_gondor_sword_a04),
untouched here. Deep review: five agents, no code findings; RCA Class
12 in docs/reviews/rca-yotthani-camps-2026-08-23.md.

Research: GameMenu, GameMenuManager, MenuContext, MapState, PartyScreenHelper, PartyState on installed 1.4.8
Not-tested: the in-game founding walk and a stuck player save on the fix build
Save-compat: no persisted shape changes; existing stuck saves recover on load

#### fix(fiefs): reward siege participation, not a stamp (#565)

`174893ba`

Players won towns and castles from sieges they never joined. The #458
rebalance had shipped without a smoke test, and its only participation
signal was Town.LastCapturedBy: one clan stamp, written to the assault
leader's clan, never cleared. Army members got nothing and a clan that
stormed a place months ago kept a 2.5x claim on every re-election. The
player exemption shipped on, vanilla already pays the player a flat +30
and lets landless clans dominate, and the King's Vote cap had removed
the override that used to hide the merit ranking.

A campaign behavior now records every winning-side clan's share of the
engine's own ContributionToBattle at MapEventEnded, which the engine
dispatches before the BySiege owner change in the same finalize. The
top contributor gets the full bonus, others get it scaled by share, and
absent clans get a new Absent From Siege Factor (0.50). The rules for
what is recorded live in FiefSiegeCaptureRules, one method per engine
site: only battle types KingdomManager.SiegeCompleted transfers
ownership for, only clans that can appear on the ballot, and only when
vanilla will open a claim at all. The exemption knob is renamed so its
flipped default reaches existing installs.

Review: 5-agent deep review (two gaps, each fixed through /investigate
with a RED-then-GREEN regression), Codex on GPT-6-Astra at ultra (4 P2,
2 P3, all verified and fixed). RCA and lessons:
docs/reviews/rca-fiefgranting-participation-2026-09-11.md.

Commit 140bad85 (#564) carried this change's hunks in TaomSettings.cs,
SubModule.cs, SettingsFingerprintTests.cs, the patch registry and both
co-op docs ahead of this commit, so HEAD did not compile in between.

Research: MapEvent.FinalizeEventAux, KingdomManager.SiegeCompleted,
  ChangeOwnerOfSettlementAction, SettlementClaimantDecision,
  SettlementClaimantCampaignBehavior, SiegeEvent.CanPartyJoinSide,
  KingdomDecision.DetermineSupportOption, MCM BaseSettingsJsonConverter
Rejected: reusing the dead _capturerHero field or Town.LastCapturedBy
  (one identity where a share needs every clan); OnSiegeAftermathApplied
  (fires after the owner change for a player-led siege)
Save-compat: new SyncData key _taomFiefSiegeParticipation as
  Dictionary<string,string>; a pre-#565 save loads as an empty record
Not-tested: the four in-game smoke tests in docs/features/fief-granting.md

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### chore(data): dedupe the spclans.xslt bandit-clan strip, fix a stale comment

`ec5ded82`

spclans.xslt carried the five vanilla hideout-bandit clan strips
(sea_raiders, mountain_bandits, forest_bandits, desert_bandits,
steppe_bandits) twice: once at the top of the file and once verbatim at
the end. The trailing copy is removed. The transform over vanilla
spclans.xml is byte-identical to HEAD's (checked with lxml: the five
clans still stripped, looters still kept), and check_external_xslt.py
passes all 17 stylesheets.

The wave-2 comment in taom_spcultures.xml said Gondor Soldiers, Erebor
Warriors and Mirkwood Stalkers were inert until a bandit clan and
hideouts existed. They have had a clan each in characters/clans.xml
and 10 hideouts each in the live TAOM_Map since 2026-05-28. The comment
now says so and states the inert/crash rule in both directions.

Found during the #564 research. No behaviour change.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### data(bandits): cut boss party templates to vanilla shape (#564)

`53f39114`

The eight *_boss_party_template's were one 1/1 boss plus three or four
soldier stacks at max 18-32, sums 67-97, solved by
rebalance_template_power.py against a 105 power budget. Vanilla's are
boss + chief + 2-4 raiders. They are now one 1/1 boss plus soldier
stacks summing to min 3 / max 4 (1/2 + 1/1 + 1/1; Gundabad drops its
fourth guard stack). The size matters because
ArrangeHideoutTroopCountsForMission never trims a boss party, so a fat
one overflowed every cap into the boss phase; the FIGHT itself is now
sized by Patch86 in the previous commit.

- HideoutBossPartyTemplateTests pins the shape: one pinned stack that
  is the culture's bandit_boss, soldier sums 3 / 4, every bandit
  culture binds exactly one existing template.
- rebalance_template_power.py drops boss from DEFAULT_BUDGETS,
  BANDIT_KINDS and TEMPLATE_KIND_RES (solve_flat cannot express
  1/2 + 1/1 + 1/1); 42 templates in scope, dry-run is a fixed point.
  raise_party_template_maxes.py (retired) narrows its regex to raiders
  so a re-run cannot push the stacks back to 50.
- Docs: caravan-bandit-parity, party-template-sizing, the modding
  handbook chapter, tools/README.

Not-tested: in game; validate_moduledata.py reports zero troop or
party-template ref errors for the eight templates.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### feat(bandits): hideout boss fight is boss + N bodyguards (#564)

`140bad85`

The final hideout fight was routinely boss plus 20 to 40, and boss
plus ~90 on the night route at the endgame density cap. The boss
party template never sized it: the engine draws the fight from the
whole hideout. Assault: MapEventHelper.GetPriorityListForHideoutMission
sends min(floor(0.8 * total), FirstFightMax) to phase 1 and the rest
to the boss, and ArrangeHideoutTroopCountsForMission never trims a
boss party (:615). Sneak-in: HideoutAmbushMissionController spawns
every troop that was not a sentry; its Clamp(pop/2, 4, 20) is a floor.

- IHideoutBossFightService (pure, 33 tests) owns the maths.
- Patch86_HideoutBossFight: one category, two prefixes. Assault
  replaces GetPriorityListForHideoutMission (skip original, fails
  open to vanilla); sneak-in trims the injected _allEnemyTroops on
  SpawnRemainingTroopsForBossFight and lets the original run.
- TaomBanditDensityModel.NumberOfMaximumTroopCountForBossFightInHideout
  is 1 + N unconditionally; its only consumer is the campaign trim.
- MCM "Hideout Boss Bodyguards" (0-10, default 4), independent of the
  master toggle. "Boss Fight Curve" scales the first fight only now
  and is relabelled; the property name is unchanged.
- Binding tests pin both targets by parameter NAME plus the out flag,
  the three injected field types, and the three-place registration.
  Both prefixes classified ReviewedSafe for co-op.
- Docs: feature doc section, Patch86 registry entry, API snapshot
  regenerated (227 patch targets; it had sat at 220 since Patch82),
  reflection-sites rows, settings counts 235/176, CLAUDE.md category
  count re-measured (92; it had read 86). RCA and two lessons.

The template cut and the tools that own its numbers land in the
next commit.

Research: MapEventHelper.GetPriorityListForHideoutMission, HideoutCampaignBehavior.ArrangeHideoutTroopCountsForMission, HideoutMissionController.SpawnBossAndBodyguards, HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight, PlayerEncounter.DoEnd
Rejected: GameModel-only spawn-percentage override (exact only at the cap, boss + 1..3 under it, blind to a fat boss party on an existing save)
Save-compat: no new saved fields; a boss party spawned from the old template keeps its size until that hideout is cleared once and lands in phase 1, never in the boss fight
Not-tested: the two prefixes in a live mission (binding + IL tests only); the six in-game smokes are listed in docs/features/bandit-management.md

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### Implement provider-neutral review packet and advisory evidence validation tool

`a87e7848`

- Added `reviewctl.py` for managing review packets, including creation, validation, and reporting.
- Introduced functions for handling JSON loading, digesting, and Git operations.
- Implemented routing contract validation and packet creation logic.
- Developed a command-line interface for linting, preparing, and validating review packets.
- Created unit tests for documentation links and review workflow processes.
- Added tests for packet creation, validation, and assessment of review reports and checks.

#### Add script to manage village entries in settlements.xml and corresponding loc files

`c02a6a88`

- Introduced `add_map_villages.py` to automate the addition of village entries to the LIVE TAOM_Map/settlements.xml and their respective localization rows.
- Implemented functionality to check for missing entities, validate positions, and ensure idempotency.
- Added unit tests in `test_add_map_villages.py` to cover various scenarios including block rendering, position resolution, and insertion logic.
- Ensured compatibility with existing village types and cultures, raising errors for unknown types.

#### docs(shaders): sweep stale text, ignore .tmp (#560)

`1700156b`

Statements the re-enable made wrong, outside historical records:

- docs/migration/v1.4.8-impact.md: the shader row no longer calls the
  hint a known issue and the owed list no longer describes the walk as
  headless (re-enabled by #560, cold walk still owed).
- docs/features/battle-load-diagnostics.md: the walk's rationale is no
  longer "PARKED"; the b18f3441 bundle is what un-parked it.
- docs/features/shader-precompilation.md: the runner and SubModule rows
  name TryClaimMission instead of the old gate.
- The 2026-06-17 and 2026-07-11 shader RCAs carry a status line naming
  what closed their findings, and that the coverage mechanism was the
  opposite of what the first RCA assumed (preload walks both rosters).
- precompile_scenes.txt says it is read only when the scene-pass toggle
  is on.
- Three code comments (decider caps, the runner's toggle note, the
  banner-bearer skip) stop describing an all-characters battle and a
  headless walk. Comments only; build clean.

Also ignores .tmp/: reviewer sessions and Codex runs drop snapshots,
decompiles and capture logs there (44 MB, 2,977 files today), which
polluted git status and gave the docs linter ~1,900 false dash hits.
Nothing durable lives there; the record is the committed prompt, RCA
and review-log entry. The three folders were deleted with their
owners' agreement.

AGENTS.md still lists Patch21 as active, which is now true; left alone
because another session has it mid-rewrite.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### docs(lessons): name the mechanism behind the carried-hunk lesson

`6613f60d`

The Prevent bullet now states why the status check proved nothing
(git add and update-index --add stage the whole worktree file), the
check that does work (git diff on the path immediately before staging),
the safe construction when a peer holds hunks in the same file (a
scratch GIT_INDEX_FILE read from HEAD, then update-index --cacheinfo
with a blob built from git show HEAD:path plus your own change), and the
cross-reference to CLAUDE.md's rule that a reconstructed shared file goes
into the index, never onto disk. Wording refined with the #559 session,
which built its three commits the same way.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### docs: attribute two hunks carried by 8948437d, add the lesson

`784822c1`

8948437d took docs/reference/feature-map.md and
docs/modding/configs-balance.md from the worktree, minutes after a
status check had shown them clean. Another session had edited both in
between, so that pushed commit carries its hunks under my message: the
BanditManagement row (5 to 8 cultures) and the MCM settings count (219
value knobs across 56 groups). Both are correct; only the attribution
was missing. The CHANGELOG entry now names them.

The lesson goes to docs/reviews/lessons/build-tooling-workflow.md: a
worktree-sourced blob is whatever the worktree holds at commit time, so
a file another session may hold open is committed as HEAD plus your own
textual change, or re-diffed against HEAD immediately before it is
added, never trusted from an earlier status check.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### docs(mcm): settings posture, stale counts, index

`38176150`

Documentation pass after #559. Every number below was re-measured
today from the committed files; the measured comments carry the
commands and the date.

- docs/features/mcm.md gains a "Settings posture" section: the
  RequireRestart mechanism, the posture test and its three-entry
  allowlist, why a moved default reaches fresh TAOM.json files only,
  why a JSON behind an MCM knob is dead (#561), and the SaveTriggered
  loop the flag closes. The handbook said nothing in the repo held
  these rules; now something does, and balance-levers.md and
  open-questions.md say so.
- docs/INDEX.md had no entry for bandit-management.md at all (the doc
  is from May); added, and the mcm entry names the posture section.
- docs/reference/doc-lookup.md: a row for "an MCM setting a player
  changed did not apply".
- The handbook's "432 [SettingProperty declarations across 55 groups"
  (measured 2026-09-05) is 440 lines today: 220 group labels, 219
  value knobs, 1 button, 56 groups. balance-levers.md, file-catalogue.md
  and open-questions.md corrected; configs-balance.md and
  feature-map.md were corrected in the same pass but landed in
  8948437d, which committed the shared blobs while both sessions had
  hunks staged in them.
- bandit-management.md said 5 bandit cultures, 10 party templates and
  5 hideout descriptions since the wave-2 cultures arrived in May: 8
  cultures with 8 clan rows, 16 templates, and 6 descriptions.
  gondor_soldiers and mirkwood_stalkers have no taom_hideout_desc_*
  string and still show vanilla's placeholder; stated as a coverage
  gap in the doc and in the Patch40 registry row, not fixed here.
- Backlink footers regenerated for the three docs whose inbound links
  changed (mcm, bandit-management, balance-levers); the generator was
  run on a scratch copy of docs/ so it could not touch a peer's
  settlements.md, whose own footer is still pending on their side.

Verified: tools/lint_docs.py clean on every file here;
  lint_handbook.py 40 chapters 0 findings; check_handbook_attributes.py
  152 markers 0 findings
Not-tested: none; documentation only

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### docs(specres): correct the stale claims in the feature doc

`8948437d`

docs/features/special-resources.md still said the Gondor resource was
"Caster", that every resource shared one set of rates, that 12 Mordor
troops were costed, that icons live under MapBar/, that the behavior
hooks 8 events and that the load path clamps to the cap. None of that
has been true for months.

The rates are now a per-resource table read from the config; the cost
file is described as it is (77 rows: 27 with upkeep, 50 merchant-only);
the icon folder and sprite id form match the shipped files; the event
list is the 12 the behavior registers; the load path repairs non-finite
entries and does not clamp; the tier test classes are listed; the status
line separates what is verified in-game from what is not; the component
diagram and the performance notes include the 2026-09-11 pieces.

Also: the feature map row, the handbook balance chapter (a merchant-only
row never deserts), the console reference row for
taom.print_special_resources, and the review-codex skill's completion
note (max vs ultra timing).

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### feat(shaders): re-enable the walk for 1.4.8 (#560)

`1e654021`

The Pre-compile Shaders main-menu walk returns after the 2026-08-20
park. Player bundle b18f3441 (TAOM v2.0.26, Bannerlord 1.4.8) sat 305 s
one frame short of playable while ~700 cold character shaders compiled,
which is the cost this walk prepays.

Coverage: the CustomBattle preload view feeds every roster character of
both combatants to PreloadHelper, which compiles every equipment mesh's
shaders, so coverage is roster membership; the old 3,000/side cap with
soldiers added twice dropped TAOM's own troops. The roster (unreadable
at the main menu) is discovered inside the first battle and planned in
batches of 1,000, every character once.

Battle shape: a one-character player party against the batch, so the
engine flags the PlayerTroop as the player agent (the one writer of
Mission._initialPlayerAgent, which SetupTeams derefs unconditionally
on 1.4.8) and deployment auto-finishes. The 1.4.7 guard stays as a
player-team-only fallback.

Scene passes: EnableScenePassPrecompilation renamed to
EnableShaderPrecompileScenePasses, default off; a json2-persisted
default cannot be flipped for existing installs.

Review fixes (5-agent deep review + Codex gpt-6-astra ultra, all
verified): 10-minute start bound and an INCOMPLETE state when batch 1
never re-plans; every skip path counts as aborted; EndGame() is never
called without a game and cancellation runs through the teardown
state; a teardown timeout stops the walk instead of stacking a game;
the runner claims one mission per item and stands down if the player
starts another; {newline} bound for the cold main menu; honest
coverage wording; the OoB profile-write rationale withdrawn.

Text: hint and inquiry rewritten (re-run after mod-list changes); rows
seeded in English in all 12 languages, translation run owed.

Constraint: MBObjectManager.Instance exists only inside a Game, so the
roster cannot be read at the main menu
Rejected: raising the roster cap alone (every Armory item resident at
once, #385 history); flipping the scene-pass default (never reaches an
existing install); deleting scene passes (#448 blocks per-scene sacks)
Not-tested: the cold in-game walk on 1.4.8 (the only proof for the
boundary code), the cancel, teardown-timeout and foreign-battle paths
Research: PreloadHelper, MissionCustomBattlePreloadView,
CustomBattleHelper.StartGame, Mission.SpawnTroop and BuildAgent,
DeploymentMissionController, DefaultBattleMissionAgentSpawnLogic,
MBGameManager.EndGame and StartNewGame, OrderOfBattleVM, GameTexts,
MCMv5 JsonSettingsFormat, all on the installed 1.4.8
Save-compat: none; MCM json2 keeps the orphan key until the next save

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### docs(mcm): Codex corrections for #559

`2a8ba271`

Codex (GPT-6-Astra at ultra) reviewed 2652fe2f and 37411388: P1 0,
P2 0, P3 3, every claim with a pasted decompile or a listed grep, and
an independent inventory of all 231 value attributes matching ours. It
confirmed the empty-Cancel-delegate mechanism the sweep rests on and
disputed four of the six suspects it was handed, including the natural
attrition hypothesis. All three P3s were prose past its evidence; each
was re-verified against the live map and the committed text before it
was accepted, and each is reworded here.

- "The file was never read": the old provider constructor loaded it,
  and its one JSON-only field, MinPartiesToInfest, did apply. Only the
  six MCM-backed values were dead. The override is retired (constant
  1), which the doc now says.
- "112 hideouts against vanilla's 42": three bandit factions own only
  10 hideout locations and FillANewHideoutWithBandits no-ops with no
  candidate left, so 14 filled at most 5 x 14 + 3 x 10 = 100; vanilla
  has five settlement-capable bandit factions (looters have none), so
  35. Hint, doc and CHANGELOG corrected; the live map holds 159.
- "Turning it off changes nothing about a campaign in progress":
  vanilla's 2-party infestation minimum is restored live, so a
  one-party camp stops counting at once. What holds is that nothing
  deletes a party or culls a hideout. Hint and doc reworded.

Also from its observations: "for the rest of the session" is now "in
session, until the options page's own Cancel undoes it", and the
2026-05-29 upgrade-caveat paragraph is labelled historical.

RCA gains a Codex section and two lessons (an absolute in prose is a
claim about every path; a per-faction target times the faction count
is not a hideout count). REVIEW-LOG: Review 96. The AGENTS.md lessons
update is deferred: another session has that file mid-rewrite in the
working tree; the two entries to add are in the RCA.

Verified: Main builds and the BanditManagement + Mcm filter is 105/105
  on a clean worktree at HEAD with the hint blob overlaid
Not-tested: in-game (unchanged from #559)

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### chore(codex): pin the reviewer to GPT-6-Astra at ultra

`711fb2df`

The repo config had pinned gpt-5.6-sol at max since 2026-09-05. The
local model cache (~/.codex/models_cache.json, fetched 2026-09-11)
lists gpt-6-astra with a reasoning ladder of low, medium, high, xhigh,
max and ultra, so the pin moves to gpt-6-astra at ultra and the
review-codex skill's note says so. The user config still names the old
pin; the project config wins for this trusted checkout.

First run on the new pin: review 95 (#558), 3 MEDIUM, 0 false
positives.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### fix(specres): report real debits, guard the countdown (#558)

`8bd8917e`

Three MEDIUM findings from the Codex pass on GPT-6-Astra at ultra
(review 95), each verified against the source before the change.

The spend and recruit toasts reported the nominal cost. The storage
floors at zero and the party screen's prisoner recruit is not gated by
recruit_cost (#563), so a spider recruited at a balance of 2 would have
announced a debit of 5. ChargeRecruitCost and CommitSession now return
before minus after read from the store; the return-value tests moved
from a substitute storage, which could only prove a request, to the
real one, with two new floored cases.

DaysUntilDepleted cast Math.Ceiling(balance / -Net) to int unchecked. A
net below the balance's float resolution (one town at +0.7 against
0.2 + 0.2 + 0.3 of upkeep nets about -6e-8) never moves the stored
value, yet divides into 1.7e10 days, and the cast rendered
"Depleted in -2147483648 days". No countdown when the next tick would
not lower the stored float, the division in double, a range check
before the cast; the float-cast rule in csharp-architecture.md is
widened from NaN to finite overflow, with a lesson.

The zero-balance notice claimed troops were deserting whenever upkeep
troops were present, but the tick adds the net before it tests the
balance, so with income covering upkeep nothing deserts. The notice now
requires a non-positive net and states the rule; the 13 English rows
are reworded to match.

Also: the Net-matches-projection test now asserts an independent
expected value, the review log gains Review 95 and the Codex track
record records what the new model did well. AGENTS.md is left alone:
another session is rewriting it in the worktree.

Research: RecruitPrisonersCampaignBehavior.OnMainPartyPrisonerRecruited,
TroopRoster.AddToCounts, the tooltip renderer for OnlyShowWhenExtended
(installed v1.4.8, via Codex and re-read here)
Not-tested: the reworded notice and the toasts in-game
Save-compat: none

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### fix(mcm): review corrections for #559

`37411388`

A second /deep-review pass on 2652fe2f (six agents, one of them a
numeric-claims audit that re-derived every count from the committed
files and re-ran the suite on a clean checkout: 8406 passed, 1
pre-existing Armory-data failure, 2 skipped) found six things, none
behavioural. Five are fixed here; the sixth is the 68-character subject
of 2652fe2f, which stays because two other sessions have committed on
top of it.

- "218 value settings" was 217: the count had included the one
  [SettingPropertyButton], which carries no value and which the posture
  test itself skips. CHANGELOG and issue #559 corrected.
- "18 tests deleted" was 16: relayed from an exploration agent's report,
  never counted. Same two places.
- docs/features/bandit-management.md "What gets scaled" still said
  default 14 and default 3 fifteen lines above the table the commit
  updated to 7 and 6.
- BanditPartySizeCurve's hint promised "up to 2.5x" without the
  stack.MaxValue ceiling Patch39 applies per troop template; sentence
  added.
- SettingRequireRestartPostureTests keyed its allowlist on the bare
  property name across four classes; a same-named property on another
  class would have inherited an exemption. Keyed on Class.Property in
  the allowlist and its self-check.

Also disputed and left alone: an agent flagged the committed .cs blobs
as LF; git cat-file shows 0 CR bytes in both the pre- and post-commit
blobs, so that is the repository's normal LF storage, not churn.

RCA second-pass section and one lesson (build-tooling-workflow):
docs/reviews/rca-bandit-scaling-mcm-2026-09-11.md

Verified: filtered BanditManagement + Mcm run 105/105 on a clean
  worktree at HEAD with these blobs overlaid
Not-tested: in-game (unchanged from #559; the hint text is the only
  runtime-visible change here)

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### balance(mordor): rescale Black Numenorean upkeep

`e80949b7`

The 13 rows carried daily_upkeep 1.0 to 3.0 per troop against 0.05 to
0.3 for every other Mordor elite, and merchant_cost 1 to 5 against the
file's own 10 to 45 band. They reach any faction's player through
prisoner recruitment and the vassal reward, and upkeep is charged in the
player's own resource, so forty of them cost 60 a day against a top
battle payout of 28: the likeliest single source of the "wiped after
every battle" reports (#558). Now 0.05 / 0.1 / 0.15 / 0.2 / 0.3 by rung
and 6 / 8 / 12 / 18 / 28 for the emissary, the uruk line's ratio; the
two rungs under the documented merchant band are an extrapolation.

Save-compat: data only; existing saves pick it up at the next daily tick
Not-tested: in-game

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### fix(specres): surface every outflow (#558)

`9e78afd2`

Players could not see what upkeep cost and reported balances "wiped
after every battle". The map-bar tooltip had passed an empty troop list
into the upkeep calculation since the feature's first commit, so it
never showed an upkeep line, and no outflow (daily upkeep, upgrade
commit, recruit charge, floor at zero) produced a message; only earnings
did. A balance earned in a battle and zeroed at the next daily tick read
as a bug in the battle.

One GetDailyBreakdown now feeds the tick, the tooltip, the daily toast,
the map-bar warning flag and taom.print_special_resources; upkeep lines
carry the career modifier per troop so they sum to the total shown.
GetDailyEarning and GetDailyUpkeep leave the interface. CommitSession
and ChargeRecruitCost return the amount debited so the behavior can say
it. Desertion, the upkeep lines and both warnings key on daily_upkeep
above zero: the Elite Emissary's 50 merchant-only rows had made ordinary
tree troops desert at zero balance. The storage refuses non-finite
writes and repairs a poisoned save entry to zero.

Twenty taom_res_* keys registered and seeded as English in all twelve
language files; the translator run is owed. #558 stays open until the
in-game smoke. RCA: docs/reviews/rca-special-resources-outflow-2026-09-11.md

Research: PartyScreenLogic.DoneLogic, RecruitPrisonersCampaignBehavior,
MapInfoVM.UpdatePlayerInfo, TooltipProperty ctors (installed v1.4.8)
Not-tested: tooltip rows, the four toasts, HasWarning colour,
PartyUpkeepReader against a live MobileParty
Save-compat: no new saved state; a NaN balance in an old save loads as 0
Rejected: a DailyTickResult record (the behavior reads the balance before
and after the tick, as OnMapEventEnded already does); a roster-version
cache for the per-refresh breakdown (cost class equals vanilla's own
gold rundown on the same refresh)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### fix(mcm): flag 166 settings live, delete the dead bandit JSON (#559)

`2652fe2f`

A player switched Bandit Scaling off in MCM, saw the value land in
TAOM.json, still had bandits everywhere, and concluded the game reads
ModuleData/bandit_management/bandit_scaling_config.json instead. Wrong
mechanism, real symptom, three defects underneath.

The restart flag. MCM's BaseSettingPropertyAttribute defaults
requireRestart to true and 157 of 218 TaomSettings values plus 12 in the
three small settings classes omitted RequireRestart = false. The
2026-09-06 sweep called that cosmetic. It is not: in the decompiled
ModOptionsVM.ExecuteDone the restart prompt's Cancel branch is an empty
delegate followed by return, so nothing reaches TAOM.json, while the
undo stack has already written the change into the live instance. The
setting works until the next launch, then reverts. Flag added to all
166; three keep true with a reason in the new
SettingRequireRestartPostureTests, which reflects over the four classes
and fails on any new setting that omits it.

The JSON. Every knob read SettingClamp.Clamp(TaomSettings.Instance?.X,
json, ...) and Clamp is value ?? default, so the file applied only with
Instance null, which GlobalSettings<T>.Instance never is while MCM is
loaded. Never read, never pinned, looked authoritative. Deleted with its
config provider and 18 tests; the constants live in the settings
provider and BanditScalingSettingsProviderTests pins them to
new TaomSettings().

The defaults. InitialHideoutsPerFaction is read once at world-gen and
vanilla never removes a hideout above the max, so 8 factions x 14 put
112 hideouts on a fresh map that switching scaling off could not take
back. Now 7. MaxPartiesPerHideout shipped at 3, equal to vanilla's
base, and Cap() takes max(base, cap) as the ceiling, so Density Curve
never moved it. Now 6. Both reach fresh TAOM.json files only; the hints
say so.

Deep review: 5 agents, one MED finding (the hints lacked the persisted
value caveat), fixed. RCA docs/reviews/rca-bandit-scaling-mcm-2026-09-11.md.

Research: MCMv5 BaseSettingPropertyAttribute, GlobalSettings<T>.Instance;
  MBOptionScreen ModOptionsVM.ExecuteDone, UndoRedoStack.Do; v1.4.8
  BanditSpawnCampaignBehavior lines 143 and 283
Save-compat: no save fields touched; hideouts already in a save stay
Not-tested: in-game. Owed: change a bandit curve, press Done, no prompt
  and TAOM.json updates without quitting; fresh campaign shows 7
  infested hideouts per faction on day one
Verified: full suite 8406/1/2 in an isolated worktree (the 1 is the
  pre-existing wm_gondor_sword_a04 Armory drift); filtered 105/105

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>

#### feat: Implement Patch84 to guard against null _besiegerParty in siege aftermath menus

`1e561a95`

- Added Patch84_SiegeAftermathMenuGuard to prevent crashes in siege aftermath menus when _besiegerParty is null.
- Introduced two Harmony patches for menu initialization: Patch84_ParticipantMenuGuard and Patch84_ArmyMemberMenuGuard.
- Implemented decision logic to determine menu behavior based on the presence of besiegerParty and settlement.
- Created unit tests for Patch84 to verify binding and decision logic.
- Documented the rationale and findings from the review of the patch in RCA report.

#### Implement Stale Character Repair feature with comprehensive tests and documentation

`fe0aa403`

- Added `Patch83StaleCharacterRepairBindingTests` to verify binding integrity and method signatures for the repair process.
- Introduced `StaleCharacterRepairServiceTests` to validate the functionality of the stale character repair logic, including various scenarios and outcomes.
- Created detailed documentation for the Stale Character Repair feature, outlining its purpose, crash causes, repair mechanisms, and expected player experience.
- Conducted a root cause analysis (RCA) on the stale character repair implementation, identifying key findings and lessons learned for future improvements.

#### Add documentation for development machines and RCA reviews

`bd5bf0a5`

- Created a new document detailing the setup and usage of development machines, emphasizing the authoritative role of the desktop and the limitations of the laptop.
- Added RCA review documents for several issues, including:
  - Alignment-aware execution fall-through, detailing findings and preventive actions.
  - Javelin troop misclassification, outlining the root causes and corrective measures.
  - Map event observer changeset review, highlighting findings and the importance of verifying claims in documentation.

#### Add unit tests for rebalance_template_power and baseline voice clips

`1707f52e`

- Introduced comprehensive unit tests for the rebalance_template_power module, covering various functionalities including tier calculation, power scaling, and document safety.
- Added a new baseline file for voice clips that are accepted despite exceeding length limits, documenting exceptions and reasons for acceptance.

#### fix(modding): 42 corrections from the Codex review, each re-verified before it was applied

`4c815c02`

Codex found 3 CRITICAL, 11 HIGH and 4 MEDIUM against the finished
handbook. Every finding was re-checked against the engine before a word
changed, and one was rejected: a cited line had drifted since the
reviewed commit and now sits inside a verbatim XML block, though its
substance was confirmed and applied elsewhere in that chapter.

All three CRITICAL findings were in the from-zero walkthrough. Monsters
were described as project.mbproj only, when Game.cs:307 registers the
managed type and the live Armory carries eight XmlName Monsters rows
against four soln_monsters; register only the native way and the race has
no Monster object, surfacing as a null reference during agent build. The
XSLT rule said keep apply-templates last, when TAOM's own stylesheet
copies @* first and passes through only unreplaced children. And the
Check commands were presented as proving the reader's new module when
they default to TAOM's paths, so Stage 16 could print clean immediately
before the crash that stage warns about.

The largest correction is auto-resolve in balance-levers.md, which cited
a method simulated combat never calls. It now names MilitaryPowerModel,
records that TAOM overrides it and ships it on, and adds what the review
did not ask for: the measured gap between vanilla's curve extended to ten
tiers and TAOM's four flat numbers, 5 percent at tier 7 to 18 at tier 10.

Worth keeping: both gates pass on all 18 faulted chapters. Mechanical
checkers and adversarial readers catch disjoint classes of error.

Check: python tools/lint_handbook.py (40 chapters, 0); python tools/check_handbook_attributes.py (152 markers, 0); pytest (93 passed); lint_docs 0 dead links, 0 stale, 0 dashes
Not-tested: no in-game action; nobody has built a module from the corrected recipe

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(reviews): the Codex adversarial review of the handbook, with its findings verified

`7d0c4b24`

3 CRITICAL, 11 HIGH, 4 MEDIUM against HEAD 60d9761, gpt-5.6-sol at max
effort. Five load-bearing findings were re-verified against the engine
before anything was written down: managed Monster registration exists
(Game.cs:307,437 and eight XmlName rows in the live Armory, so the
recipe's "project.mbproj only" is wrong), party-template stacks carry
AlwaysPreferMerge so a same-id template appends rather than replaces,
TAOM does override MilitaryPowerModel (SubModule.cs:968) so the
auto-resolve formula and its worked numbers are wrong,
TaomVolunteerModel falls through to vanilla so a new culture does not
need C# for ordinary recruitment, and Items.xsd merges throughout so a
vanilla item can be overridden from a later module instead of by editing
SandBoxCore.

Worth recording: both handbook gates pass on every one of these chapters.
They prove an attribute table matches its deserializer and that the
contract holds; they cannot judge a sentence about engine behaviour,
which is where all 18 findings live. Mechanical gates and an adversarial
reader catch disjoint classes of error.

Fixes land next, per finding, each re-checked against the file it cites.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### feat(lint_handbook): gate the Takes effect answer against the four the engine offers

`60d97611`

Every recipe tells the reader when the edit reaches the game, and there
are exactly four answers because there are four ways the engine picks
data up: full game restart, new campaign only, next save load, live. A
qualifier may follow, and most of the 126 trailers carry a useful one
("new campaign only, because Clan.Color is [SaveableProperty]"), but an
invented answer is unactionable and nothing was checking for one.

All 126 existing trailers already pass. This stops the next one drifting.

Check: python -m pytest tools/tests/test_lint_handbook.py (11 passed); python tools/lint_handbook.py (40 chapters, 0 findings)

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): reword a version note that read as a stale target claim

`9e1ad4cd`

The line warns that another doc's line numbers came from an older dump,
which is accurate and worth keeping. Written in the present tense it
tripped lint_docs' stale-version check, which looks for a version
presented as the current target. Same meaning, no false positive.

Check: python tools/lint_docs.py --summary (stale_versions 0)

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### ci(handbook): run both handbook gates on every push

`ebc029fa`

A gate nobody runs is not a gate, which is why the doc-graph ratchet sits
in this same job. lint_handbook.py needs only the checkout.
check_handbook_attributes.py normally needs the v1.4.8 decompile, which
CI does not have, so its manifest is now committed and the gate falls
back to it: 37 markers recording what each cited deserializer reads. That
still catches the failure that matters here, a table row added or renamed
without re-deriving it from the engine.

Refresh the manifest with --update on a machine that has the dump, in the
same change as the table it describes.

Check: python tools/lint_handbook.py (exit 0); python tools/check_handbook_attributes.py (exit 0, and exit 0 against the manifest with the dump root pointed at a missing folder)

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### fix(handbook-gates): mask code spans, drop the CRLF check, and keep the two research lists

`4408f489`

Both gates were wrong in ways that only appeared once there were chapters
to run them on.

check_handbook_attributes.py scanned raw text, so the hub's own
explanation of the marker syntax parsed as a claim and failed. Escaping
the delimiter got past the gate and rendered the entity literally to the
reader, since a code span does not decode entities, so the escape was not
a fix. The gate now masks fenced blocks and inline code spans before
scanning and a chapter can document its own conventions. Four regression
tests, including one that proves a real marker beside an illustrated one
is still found and keeps its line number.

lint_handbook.py flagged CRLF. core.autocrlf is true here, so git stores
LF and writes CRLF into the worktree on checkout by design; every chapter
failed the moment it was committed. The check is removed and its test now
asserts the opposite with the reason attached.

Also lands the two lists the research produced and would otherwise have
died in a temp folder: 119 questions the handbook could not answer, and
43 claims in TAOM's own docs that the files contradict.

Check: python -m pytest tools/tests/test_lint_handbook.py tools/tests/test_check_handbook_attributes.py (91 passed); both gates 0 findings over 40 docs

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### chore(docs): refresh two generated audit artifacts

`5527b1ca`

Side effects of running the audits the handbook documents, not hand
edits. equipment-roster-coverage.csv gained 48 rows, purely additive:
goblin and mistymountainorcs have lord, ruler, child and teen roster
coverage now and the committed copy predates them. _clan_registry.json
was rewritten by clan_registry.py from current data.

Kept out of the handbook commits so the doc set stays reviewable on its
own, and landed rather than reverted because both files are regenerated
from what ships today.

Check: python tools/audit_equipment_roster_coverage.py; python tools/clan_registry.py

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): the hub, the index entries, and the backlinks that connect it

`14817786`

README.md routes on intent rather than on file name: an "I want to..."
table of jobs in the artist's own words, each pointing at one chapter and
one heading, plus the reading order for someone starting from an empty
Modules folder. Both indexes now carry the handbook, and
build_backlinks.py has written the reverse links, so a developer doc that
the handbook drew on now points back at the chapter a content author
should read instead.

The handbook is complete at 39 chapters and both gates are clean:
check_handbook_attributes.py verifies 152 engine-table and example
markers against the v1.4.8 decompile with 0 findings, lint_handbook.py
reports 0 contract findings, and repo-wide dead links are 0.

Also lands the Codex review prompt used for the adversarial pass, which
tells Codex to treat TAOM's own docs as suspect: the research found 43
claims in them that the files contradict.

Check: python tools/check_handbook_attributes.py; python tools/lint_handbook.py; python tools/lint_docs.py --quick
Not-tested: KEYforce has not yet walked a recipe end to end, recorded as owed in the hub

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): the from-zero walkthrough, the symptom table, and two rechecked drafts

`ac0d1840`

recipe-new-mod-from-zero.md is the chapter KEYforce asked for in his own
words: start with an empty Modules folder and end where TAOM is today.
It orders the work the way the engine forces, registration before
existence, culture before kingdom, clan before lord, settlement before
anyone can spawn, and each stage ends in the validator that proves it,
what the change needs to take effect, and whether it needs code. Where
TAOM never documented a step, the Modding Kit wizard, navmesh painting
and heightmap import among them, it says so and names the file to open
instead of inventing a procedure.

troubleshooting.md is one table from symptom to cause to chapter: naked
troop, T-pose, weapon never drawn, harness that will not equip, crash on
new game, crash on the daily tick, infinite mission load, Calradian
garrison, Hired Pike in a Mordor tavern.

file-catalogue.md and modules-overview.md were drafted by agents that ran
out of credits before self-checking, so both were re-measured and
re-gated here rather than trusted.

Check: python tools/lint_handbook.py; python tools/check_handbook_attributes.py
Not-tested: nobody has yet built a module from these steps

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): the race and retire recipes, the balance levers, the tools table

`12f72f21`

balance-levers.md walks one troop from its level through tier, wage,
recruit cost and auto-resolve power, and one armour piece from its four
numbers through tier to price, each hop with the formula and the model
that owns it. That is the chapter a balance pass starts from, because it
says which number the player actually feels.

validation-and-testing.md sorts the tools by safety rather than by name:
read-only validators, analyzers to run before their writing twins,
rebalancers that default to a dry run, generators that rewrite a whole
file, and the ten scripts that write with no preview at all, which are
listed as ask a developer first. It closes with the diagnosis that costs
the most time: a green validator with a naked troop in game means the new
file has not been loaded, because the engine registers item directories
at launch and never hot-reloads.

recipe-retire-content.md is the save-safety chapter: ids are permanent,
retire with a stub or an orphan, and never delete a settlement.

Check: python tools/check_handbook_attributes.py (0 rows for these four)
Not-tested: no in-game action

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): the map and armoury modules, and the culture and kingdom recipes

`46d0b1bc`

module-map.md is the campaign-map anatomy: SceneObj, terrain, how a
Settlement id binds to an entity in the scene, the distance cache, and
the prefab entity cap whose checker counts one module while the engine's
queue is global. Where TAOM never wrote something down, and map authoring
has several such gaps, the chapter says so and names the file to open
rather than inventing a procedure.

module-armory.md covers the tree the engine actually loads. The Armory
ships no cooked packs at all, so the loose Assets tree is the source of
truth, and the project.mbproj soln ids must be standard values or the
rows are inert with no warning.

The two recipes are pointer chains with the fatal steps first: a culture
that is defined but has no settlement crashes the daily clan tick, and a
kingdom needs its culture, its owner's clan and its capital before the
seven configs that enumerate kingdom ids will hold together.

Check: python tools/check_handbook_attributes.py (0 rows for these four)
Not-tested: no in-game action, no map built from these steps

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): banners, strings, and the two config chapters

`c0cdbfc9`

banners-and-heraldry.md decodes the banner_key grammar layer by layer, so
a key stops being an opaque number string, and states the tell that a key
under about 100 characters is a placeholder that renders as a plain block.

The two config chapters quote partial, parseable JSON with the shipped
config path on the line above each fence, which puts them inside
lint_docs.py's config-example drift check. That check now covers them, so
a knob renamed in the code and left stale in the handbook fails a commit
rather than misleading a reader. python tools/lint_docs.py --fail-on-drift
exits 0 with these four in place.

strings-and-localization.md carries the trap that costs the most time:
the translation cache is keyed on string id alone, so editing an English
string that is already translated leaves twelve stale translations and
silently reverts the edit on the next rebuild.

Check: python tools/check_handbook_attributes.py; python tools/lint_docs.py --fail-on-drift
Not-tested: no in-game action

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): party templates, clans, kingdoms, settlements

`05903cac`

The campaign layer. party-templates.md answers the question that has
caused four retunes: max_value is the spawn ceiling, not the party size.
The engine draws one ratio per party and fills every stack to
min + (max - min) * r, and the steady-state size belongs to a different
model entirely, so raising a template does not raise the army the player
fights.

settlements.md is the live-module chapter. It opens with the warning that
the repo copy of settlements.xml is a stale shadow, 863 entries against
the live 988, and that the game reads the one in the game install. It
also separates what an edit reaches: owner and prosperity are new-campaign
only, while culture is re-read from XML and therefore lands on saves.

Check: python tools/check_handbook_attributes.py (0 rows for these four)
Not-tested: no in-game action

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): lords and heroes, skill sets, body properties, cultures

`268009dc`

A named lord is two records sharing one id: an NPCCharacter in lords.xml
and a Hero in heroes.xml. The chapter states what happens when the pair
breaks, which is the most commonly missed file in a new kingdom, and it
states it from Hero.Deserialize rather than from folklore.

cultures.md is the largest table in the handbook and the one where a
mistake is quietest: a culture attribute left unbound in an XSLT block
inherits Calradia rather than erroring, and the caravan child lists union
instead of replacing, so emitting yours without excluding vanilla's rolls
Calradian roughly half the time. body-properties.md covers both parsers
and their differing defaults, and says plainly that no validator catches
a dangling BodyProperty reference.

Check: python tools/check_handbook_attributes.py (0 rows for these four)
Not-tested: no in-game action

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): troops, equipment rosters, notables and wanderers

`367877ed`

The character chapters. troops.md owns the merged BasicCharacterObject
plus CharacterObject table and the level ladder that decides tier, wage
and recruit cost; the other three list only their delta, so a reader
adding a notable is not handed the whole troop table again.

Two rules a roster editor has to know are stated where they bite. A
non-hero troop never wears one of its equipment sets: the engine draws
each slot from an independently chosen set, so a slot filled in some sets
and not others can spawn empty. And the two civilian spellings are not
interchangeable, equipmentType="Civilian" on a standalone roster,
civilian="true" inline, both of which the engine parses.

Check: python tools/check_handbook_attributes.py (0 rows for these four)
Not-tested: no in-game action

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): the four item chapters, the artist's daily work

`cfa52d24`

Armour, crafted weapons and single-piece bows, shields, and the data side
of a mount. Each carries the ItemObject table plus its own component
delta (ArmorComponent, WeaponComponentData, HorseComponent, Monster),
a worked example lifted from the shipped Armory, and Add / Modify /
Delete recipes ending in the validator that proves the change.

The shields chapter states the grip rule as the engine reads it:
item_usage hand_shield pairs with ForceAttachOffHandPrimaryItemBone and
shield with the secondary bone, never both, and the two body_name values
that look mistyped must not be corrected. The weapons chapter states the
first-match usage rule that has shipped a silent bug three times: a
polearm absent from OneHandedPolearm resolves requires_no_shield, and a
shield-carrying troop then never draws it.

Check: python tools/check_handbook_attributes.py (0 rows for these four)
Not-tested: no in-game action

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### docs(modding): the first eight chapters of the modder handbook, and the two gates that keep them honest

`e5c3b1cf`

KEYforce wants to add, remove and rebalance content himself, and a new
modder should be able to get from an empty folder to what TAOM is today.
docs/modding/ is that handbook: one chapter per file family, each with an
engine-sourced attribute table, a worked example lifted from a shipped
file, and Add / Modify / Delete recipes that end in a real validator
command, what the change needs (restart, new campaign, save load) and
whether it needs code.

Landing here: editing-safely, submodule-and-registration,
load-order-and-dependencies, id-cheatsheet, file-catalogue,
modules-overview, module-taom, module-dependencies. The other 31 chapters
are written against the same contract and manifest.

check_handbook_attributes.py is the fabrication gate: it opens the cited
deserializer, extracts every attribute string the method actually reads,
and fails on one the table invents or one the table omits. Its first run
over the finished chapters found four real defects, including a
DependedModuleMetadatas row presented as engine-parsed when
LoadWithFullPath has no branch for it. lint_handbook.py covers the rest
of the contract: dashes, drive paths, dead links, the skeleton, recipe
trailers, AI vocabulary, live-path warnings, BOM and CRLF.

Research: 14 v1.4.8 deserializers, every ModuleData file in the three
modules, 325 docs classified, 43 existing doc claims checked and found wrong
Not-tested: no in-game action; KEYforce has not yet walked a recipe end to end

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>

#### fix(codex): the repo pin was inert until the path was trusted

`0bd80bd4`

A project .codex/config.toml loads only when ~/.codex/config.toml trusts
that checkout path. The user config trusted the old source/repos/taom
path, so on this machine the repo pin and its three MCP servers never
loaded (codex doctor: user config path, MCP servers 0). The path is now
trusted (MCP servers 3), the CHANGELOG entry from 1b6b5cf5 is corrected
(it claimed the repo pin outranked the user config), and the skill's
CODEX_HOME explanation is replaced by the trust rule plus the step a new
laptop needs.

Research: codex doctor before/after; learn.chatgpt.com config-basic
Not-tested: a full /review-codex run under the loaded project config

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### chore(codex): pin gpt-5.6-sol at max, CLI 0.153.4

`1b6b5cf5`

The trusted-project .codex/config.toml outranks ~/.codex/config.toml, so
the repo's gpt-5.5/xhigh pin was what every Codex review actually ran,
whatever the user config said. Now gpt-5.6-sol at max, verified by codex
doctor and a smoke exec (model: gpt-5.6-sol, reasoning effort: max).
The filesystem MCP also reaches the live TAOM_Map and LOTRLOME_Armory
ModuleData so a data-layer review can open what it judges.
review-codex/SKILL.md corrected on where the pin lives.

Research: learn.chatgpt.com/docs/config-file/config-basic (precedence)
Not-tested: a full /review-codex run on the new pin (smoke exec only)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>

#### Add script to fix upgrade armour regressions and corresponding unit tests

`b8ef3800`

- Implemented `fix_upgrade_armour_regressions.py` to ensure troop upgrades do not lower armour values.
- The script evaluates upgrade paths and adjusts troop equipment accordingly, maintaining visual identity while adhering to the armour ladder rule.
- Added unit tests in `test_fix_upgrade_armour_regressions.py` to validate functionality, including edge cases for militia, bodyless troops, and hero kit demotion.
- Tests cover various scenarios to ensure robustness and correctness of the armour adjustment logic.

#### feat: Implement War Ram feature with adjustments to attack mechanics and troop progression

`b54f845c`

- Introduced the War Ram as a new mount for Dwarven cavalry, including a detailed attack system with cooldowns and damage adjustments.
- Adjusted troop tree to include the War Ram cavalry branch, allowing for recruitment and progression through various tiers.
- Enhanced the recruitment system to ensure the War Ram units are available in volunteer pools, addressing previous limitations.
- Added console commands for debugging enlistment states and rescuing stranded players, improving user experience during encounters.
- Documented changes in feature and game model references, ensuring clarity on the War Ram's mechanics and integration.
- Conducted a thorough review of the enlistment system, identifying and resolving potential deadlocks and improving overall stability.

#### docs(ai-party-size): sweep what four retunes left behind

`7097f434`

The AI Party Size defaults moved four times on 2026-09-01 (10.0/300, 5.0/150,
2.5/75, neutral) and the party templates twice more. Several documents were
updated at an intermediate step and describe a state that never shipped. Eight
stale claims, found by sweeping docs/, tools/README.md, CLAUDE.md, AGENTS.md and
.claude/ rather than by spot-checking:

- tools/README.md told anyone adding Black Numenorean stacks to retarget against
  "Mordor's 3500 target" and listed the old 4500/3500/2000/1500/1000 band as
  current. Both now read the shipped band.
- black-numenorean.md gave two templates as 3225/3500 and claimed the retarget
  tool "stays a no-op" on them. They are 241/260 and it rescaled them; the BN
  share held at 93% because the scale is proportional.
- party-template-sizing.md called the r = 0.69 worked example the "typical fill
  ratio". It is one point on a uniform distribution, and it is the exact ratio
  behind the 2423-man lord that started this. Mean figures added beside it, and
  a present-tense paragraph about the August pass marked as history.
- startup-resources.md warned K = 100 "goes stale" if the knobs are retuned, in
  future tense. They were retuned. It says so now.
- lessons/data-content-cultures.md held the last live claim that templates ship
  raised and the #461 symptom is present by default. Marked resolved; the lesson
  stands, because it was found by asking what the feature does at its defaults.
- party-template-sizing.md gained a section on the floor bug, since the doc's
  subject is how to run that retarget and it did not mention the failure mode.
- The RCA gained a fourth pass, including the part worth keeping: the
  verification run at the time (every template on band, largest sum 320) was
  true and useless, because a sum landing on target says nothing about what
  stopped existing to get there.

ai-party-size.md was checked end to end and needed nothing. Harness memory
updated separately; its index line still described the August behaviour.

The habit worth keeping: when a value is being retuned, cite the constant by
name rather than the number. AiPartySizeService.DefaultLordFactor does not go
stale. "10.0" did, four times.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
