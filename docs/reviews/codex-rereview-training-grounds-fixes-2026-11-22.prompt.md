# Codex re-review: fixes after the Training Grounds reviews (#1528, #1529), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply`, write nothing to the game install, start no game, run no build or test (another process
is building in this worktree), run no git command that changes anything. Report defects with
evidence (file, line, the input or state that fails). Engine facts come from the installed game
(`pwsh tools/taom-src.ps1 path <Type>`), never from a name. A claim you cannot prove is UNVERIFIED.

## What to review

The uncommitted change in this worktree (branch `feat/training-grounds`, HEAD af5ddb617) was
reviewed once. Fixes followed. Review the fixed code as new code: try to break each fix, and look
for what a fix opened. The earlier reports are in `docs/reviews/raw/`
(`codex-prereview-team-practice-2026-11-22.md`, `codex-prereview-training-waves-2026-11-22.md`);
read their findings sections only after you have formed your own view of the code.

The fixes, by file:

1. `Main/Features/TrainingGrounds/Waves/WaveBetService.cs`: the "too poor" refusal now compares the
   purse with the smallest stake on offer (`SmallestStake`); a refund or an aborted fight before any
   wave was cleared gives the town its day back (`GiveDayBackIfNoWaveWasCleared`,
   `WaveDayLedger.Forget`).
2. `Main/Features/TrainingGrounds/Waves/WaveFightDirector.cs`: a notice on every refund
   (`WaveAnnouncement.Refunded`), `_standingDown` set before the stand-down call, the fault path.
3. `Main/Adapters/ArenaWaveArena.cs`: the room check before the player's body is faded; the Empire
   practice set as the last weapon fallback.
4. `Main/Features/TrainingGrounds/Waves/Config/WaveTableReader.cs`: warnings for an absent `waves`
   key and for an unknown top-level key.
5. `Main/Features/TrainingGrounds/Teams/`: the mode needs the reflection access (`IsTeamMode`), the
   win debrief needs the player survived (`ShowsTeamWin`), `AnyOpponentStanding` moved into
   `TeamPracticeRules`, the outcome detail carries the mode.
6. `Main/Features/TaomSettings.cs` and `Main/_Module/ModuleData/taom_module_strings.xml`: the hint
   of the wave switch, the new notice row.
7. Tests under `TAOM.Tests/Features/TrainingGrounds/`, the manifests `tools/ingame_checks/1528.toml`
   and `1529.toml`, and the docs `docs/features/training-waves.md`, `team-practice.md`,
   `training-grounds.md`, `docs/reference/taleworlds-api-snapshot/reflection-sites.md`.

## Intent (the maintainer's decisions; do not relitigate them)

As in the two earlier prompts in `docs/reviews/`: team practice fights in towns of the Free Peoples,
and wave fights with a wager on each wave at the training master, inside vanilla's
"ArenaPracticeFight" mission, single-player only, with the hero never killed or wounded by a wave.

## What to try to disprove

1. That the day rule cannot be gamed: no sequence of stake, refund, fault, abort, leave and
   conversation end lets a player fight a wave and still have the day free, or run twice in a day.
2. That the smallest-stake rule is right in every state: wave 1 and later waves, the MCM tournament
   cap at 0, below the smallest stake and between two stake options, a perk factor above 1, and a
   purse between two stake options. The offer must never show with no stake a player can click,
   and the refusal text must name an amount that then works.
3. That every refund path shows exactly one notice, that a notice never shows when no stake came
   back, and that the fault path cannot refund twice, announce twice, or leave the player without a
   body or outside a conversation with no way to act.
4. That moving the room check and setting the flag early opened no state where a wave is marked as
   standing down while vanilla's fighters still stand, or the reverse.
5. That the Empire fallback cannot hand a fighter a kit that breaks the practice (a missing set,
   an empty equipment list, a mounted or ranged-only set).
6. That the reader's new warnings fire for the inputs they name and not for the shipped file, and
   that no input makes the reader throw.
7. That the team practice fixes are complete (see item 5 above) and that the win is still declared
   in every state where it was before.
8. That each new test can fail for the defect it names, and that the docs and the two manifests
   state only what the code does.

## Report format

Findings by severity (P1 blocks the commit, P2 should be fixed before merge, P3 note), each with the
file and line, the evidence you read, and the smallest input or state that shows the defect. Then a
list of what you checked and found correct, and a list of what you could not verify and why.
