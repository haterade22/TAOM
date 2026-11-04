# RCA: the RCA follow-up fixes (2026-11-04)

`/deep-review` of branch `chore/follow-ups`, range `9edc3a88b..35aa37439` and then its fixes, at tier 1: the branch deleted a Harmony patch and changed two GameModels and the warg's mission-tick tree. First pass: Codex `ultra`, lenses 5, 2, 7 and Tooling at max effort, a fresh review of plan 032, an adversarial warg trace and a closure audit. Because that pass found HIGHs: a Codex pass at `high` on the fix diff, lenses 1, 3, 4 and 6 and lens 5 again at `high`, and one convergence pass. The re-review found two more HIGHs in the first fix, which round three fixed; Codex did not run a third time.

**Top line:** the fixes did what each RCA line asked and missed what sat beside it. Fixing one warg line left a rage that stopped its own tree, proven in two game logs. A clean-looking extraction changed an evaluation order. `tools/tests` was red on the branch for hours, because each builder ran its own tests. A tool the branch widened still wrote by default.

## Findings

| # | Sev | Finding | Found by | Category | Why missed | Fix |
|---|---|---|---|---|---|---|
| 1 | HIGH | A rage whose hitter was already dead threw in the pass that took the formation: `Selector.Prepare` evaluates every child, "enemy died" nulls the hitter, and `WargAiControlledIsNotFacingEnemy` read it. The tree stopped for the battle. Logs `taom_debug_2026-11-04_08-08-14.log:13466-13467` and `_01-24-35.log:54592-54593` | Warg trace | Lifecycle | The builder traced the line's one case, a rider who leaves, and check 12 counted these rages as restored | `7a21239dc`, a guard and a test |
| 2 | HIGH | A hit in the second after a rage's last attack starts the next rage before the first closes, and the store wrote null over the real formation | Warg trace, lens 2, lens 5 | Lifecycle | Same; no log showed it | `7a21239dc` |
| 3 | HIGH | With no enemy within 60 m the "no enemy close" branch wins every pass, so an open rage never closed | Codex, lens 5, warg trace | Lifecycle | The new task covered only the no-rider branch | `7a21239dc`, `CloseOpenRage` |
| 4 | HIGH | The rage end preferred the live rider over the stored one. The first fix then gave a new rider the first rider's formation, and a second rage overwrote the first rider's record | Codex twice, lens 5 | Lifecycle | The record has one owner, and two rounds treated a change of rider as a data problem, not as the end of the rage | `4a7b7164d`, `CloseRageOnNewRider` |
| 5 | HIGH | `test_live_map_writers_main` failed 5 tests from `c30c4e618` on | Tooling | Verification gap | The builder ran the tool's own tests. Nobody ran the full tools suite at the work-item boundary, which `evidence-over-claims.md` B asks for | `ad8f13d00` |
| 6 | HIGH | `rebuild_translation_files` wrote on a plain `--lang` run and could not leave TAOM out, and `44d4dba46` widened it from 10 to 23 files per language | Tooling | Unsafe default | The line asked for one source list, and that fix raised the stakes of the default | `570fa0e85` |
| 7 | MED | `TaomPregnancyModel` read `hero.Clan` in the arguments of the call that held the guards, and floored a negative chance to 0 | Codex, lenses 2, 5, closure audit | Evaluation order | The extraction was called behaviour-preserving; a call's arguments run before its body | `174a7e898` |
| 8 | MED | `OnWargDied` used the stored rider with no slot check, and nothing cleared him, so a warg death long after a rage cancelled movement on a recycled slot. It also moved a never-raged first-scan rider back, and gave a player no formation back | Lenses 2, 5, Codex | Stale handle | The check was added at the new call site only | `7a21239dc`, `4a7b7164d` |
| 9 | MED | Two decorators threw on a riderless warg, because a mount has no Team | Warg trace | Engine fact | The fact is in `lessons/adapters-taleworlds-api.md`; these lines predate it | `7a21239dc` |
| 10 | MED | Check 12 counted a rider stranded before the reset could land: 2 s first, then 30 mission s, against a 9 s wall lag at 10x | Warg trace, lens 1, Codex | Two clocks | The first fix sized the limit for 4x and did not read the runner's speeds | `7a21239dc`, `4a7b7164d` |
| 11 | MED | `check_release_pair` could pass a channel missing a home that `spclans.xslt` writes as text, or a map with no `settlements.xslt` or a broken file, and a crash exited 1 | Codex, Tooling | Gate reach | Its fixtures named only XML attributes | `334c187e4`, `0385cc041` |
| 12 | MED | `BROKEN_ROSTER_REF` accepted a roster only the incompatible NavalDLC defines | Codex | Wrong root list | The list was copied from the item roots | `334c187e4` |
| 13 | MED | `dump_crash` reported failure on every written dump, because procdump v12.01 exits 0xFFFFFFFE either way, and its 180 s limit did not hold through the child's inherited pipe | Tooling, which ran it on a dummy process | Faked test | The test faked `returncode=0` | `91185bdc5` |
| 14 | MED | `audit_mount_parity` section G could never fail a gate | Tooling | Gate that cannot fail | It printed and returned nothing to `--strict` | `b6dc611dd` |
| 15 | MED | The map writers checked the table's scenes and not the live row's | Codex | Partial gate | The line asked for a writer guard | `ad8f13d00` |
| 16 | MED | The career docs told an artist to drop a PNG the code would ignore; recipes still said twelve templates; other lines stayed false | Lens 7, closure audit | Doc sweep | `completion-workflow.md` step 6, added on this branch, was not applied to this branch | `35044f583`, `0385cc041` |
| 17 | LOW | The `secret-nuget-cleartext` rule missed reversed, split, single-quoted and spaced forms | Codex twice | Narrow regex | Written for the one form on file | `285c5a1e8`, `0385cc041` |
| 18 | LOW | `8afe46d39` stopped the hook's raw fallback from over-blocking and lost eight refusals, among them `bash -c` and `eval` | Tooling | Wrong direction | The follow-up treated over-blocking as the fault on a path where it is the safe side | `000bcf1d1`, `0385cc041` |
| 19 | LOW | The two new C# gates had holes: whitespace in a member access, and a `VisualState` or `Constant` that kept a definition alive | Codex twice | Gate that passes with the fault | Each gate was proven on one fault | `ed3f911eb`, `0385cc041` |
| 20 | LOW | The rebuild wrote CR CR LF, a riderless first-scan rider was moved back, a panicked rider was written back into a formation, the MCP server held 200 MB, `find_references` line numbers drifted, and smaller items | Lenses 1, 2, Tooling | Mixed | Older code the review read | `b6dc611dd`, `334c187e4`, `0385cc041`, `7a21239dc` |
| 21 | LOW | Commit bodies claimed too much: `17b071c7a` (a restore that already worked), `79d90c08f` ("both mean no pregnancy"), `14fcc5f33` ("cannot bring them back"), `7a21239dc` (`open=0`) | Warg trace, lens 2, Tooling | Claim stronger than its proof | Written from intent | Corrected in later bodies; `17b738aa6` makes the third true |

**Rejected, with reason:**
- Codex's finding that the pair check misses `xsl:value-of` and `Culture.` in XSLT: no shipped file uses the first, and TAOM's XSLT ships with TAOM's cultures, so the second is no pair disagreement.
- The stale MCP registries (Codex twice): real, but a snapshot from server start by design, and the CLI and hooks read fresh. Stated in `docs/reference/mcp-servers.md`.
- A pure `WargRageRules` class (lenses 1 and 4), and an adapter for the pregnancy model (lens 1): the pins now read the exact conditions, and neither fix earns a new type.
- The shared scan helper (lens 6): `WargTickCostTests` pins each decorator's own scan in its IL.
- Lens 6's merge of `RestoreDismountedRider` into `CloseOpenRage`: it waits on the native question in the follow-ups below.

## Root cause

A follow-up line names one symptom, and a builder fixes that symptom. Findings 1 to 4 and 8 sit in one state machine whose whole lifecycle nobody listed before the first edit, so each fix met the next order of events. Findings 5, 6, 13 and 16 are the same habit in tools and docs: the check stopped at the file the line named. Three gates were proven only on the fault they were written for (11, 14, 19), so they passed with their neighbours.

## Why each agent missed these

- **The builders** had one line each, and were told to test first on that line.
- **The closure audit** read 29 of 34 lines as holding, and was right on the letter of each. It was not asked about the state machine.
- **Lens 5** saw findings 2, 3 and 8 and rated them pre-existing LOW or MEDIUM. Only the trace with a port of the tree found finding 1, because only it read the game logs.
- **Lens 7** passed the data and found the doc residue. It was right that nothing named a removed id.
- **Codex** found findings 3, 4, 7, 11 and 12, and missed finding 1, which needed the logs.

## Process breach

Tooling ran `check_release_pair.py` read-only on the three release channels, against `release-process.md`: those folders are Mike's. My brief forbade writing and not reading, so the brief was wrong. Later briefs forbade both. Its results: testing, patreon and public pass; dev is empty and exits 2.

## Fresh review of plan 032

This closes `rca-worker-thread-formation-patch-2026-10-02.md:140`. The last round, the evaluating cavalry gate, is sound. Its report found no CRITICAL or HIGH. The text items went into `35044f583`. The rest is below for Mike, as is what closing #714 needs: a session with the feature on, logs that show a layout assigned, and the body corrected.

## Follow-ups

- FOLLOW-UP no issue: filing needs Mike's word. Owed in game: an unattended Fords match with check 12 reading stranded=0 and no "Exception running the behavior tree" line, a normal battle smoke, and a real crash under the Watchdog for `dump_crash`. With no issue, `triage-needs-ingame` does not list them.
- FOLLOW-UP no issue: needs a log line or a native read, then Mike's call. Whether a mount's `IsPlayerControlled` is ever true decides if the "you lost control" branch in `PrepareOnFirstCall` ever runs.
- FOLLOW-UP no issue: balance, Mike's call. `_random.Next(2, 3)` always gives 2 attacks, and `SetRageAttackTimer` restarts the timer every pass, so the 6 s timeout never ends a chase.
- FOLLOW-UP no issue: Mixed Formations findings for Mike. A dead unit's slot returns at corpse deletion and not at death, a laid-out formation ignores shape orders, and the cavalry gate suspends a layout with no log line. `TaomSettings.cs:801`'s hint needs a new loc key.
- FOLLOW-UP no issue: harness hardening needs Mike's word (`simplicity-criterion.md`). `_shellwords.py` passes `"$(git commit --no-verify)"`, backticks and wrappers such as `setsid`; nothing stops a deliberate bypass.
- FOLLOW-UP no issue: hand-authored, so Mike's call. `mordor_num_*_template_lord_arc` and `kingdom_hero_party_gondor_{ithilien,belfalas}_template` are bound by nothing.
- FOLLOW-UP no issue: pre-existing, LOW. `AgentHitBy` is held across frames with no slot check, a rider who moves to a second warg within a second loses his formation, and `party-templates.md` about :260 holds stale dry-run numbers.
- At the next deploy, delete `Modules/TAOM/GUI/Brushes/GoldIcon.xml` from the install: `package_release.py` refuses a stale module file.

## Feedback to codify

Lessons went to `lessons/state-lifecycle-save.md`, `lessons/gamemodels-services.md` and `lessons/build-tooling-workflow.md`. No new rule: `evidence-over-claims.md` B already asks for the full suite at each work-item boundary, and the fault was not following it.
