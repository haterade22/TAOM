# verify-b-batch-05: adversarial check of DX-L6-06, DOCS-L6-07, DOCS-L6-08

Checker: fresh adversarial pass, read-only. Baseline `b2e387db` read through `git show b2e387db:<path>` (working-tree HEAD is
`4b5662b2`, one commit later, and `CHANGELOG.md` is another session's dirty file, so every read below is the committed blob).

## DX-L6-06: the four Stop reminders print to stderr and exit 0

- **Outcome**: CONFIRMED (core mechanism), with three corrections.
- **Re-read**: `git show b2e387db:.claude/hooks/<h>.sh | cat -n` for all four. Every cited line holds:
  `check-verification-evidence.sh:46` (`echo "REMINDER: ..." >&2`) and `:56` (`exit 0`); `check-deep-review.sh:33,36`;
  `check-version-tagged.sh:49,54`; `check-changelog-updated.sh:35,44`. All four are the Stop registrations at
  `.claude/settings.json:204-229` (no other Stop or SubagentStop hook exists; `session-stop.sh` is on SessionEnd, `:231-240`).
- **Harness link, checked against the vendor doc this run**: WebFetch of `https://code.claude.com/docs/en/hooks` returned
  the verbatim sentences "Stderr from a hook that exits 0 goes to the debug log only, never the transcript, and Claude never
  sees it" and "For most events, Claude Code writes stdout to the debug log ... The exceptions are `UserPromptSubmit`,
  `UserPromptExpansion`, `SessionStart`, and `PostModelSwitch`". That matches `.claude/rules/harness-facts.md:60` and
  `docs/reference/hooks-catalog.md:53-56`. The repo's own rule (`harness-facts.md:60`, third column) already says "A reminder
  printed anywhere else is silent: make it a gate, or move it to a visible channel", so this is not a decided tradeoff.
- **Supporting incident**: commit `3156da88` (2026-08-13) and `release-process.md:139` record that
  `check-version-tagged.sh` "did fire" for v2.0.20, but that was inferred from the marker file's mtime
  (`.version-tag-reminded` stamped 14:36 against a 14:35:52 commit), and the tag was then missed for 33 commits. The hook
  fired and nobody saw it, which is the finding's mechanism.
- **Refutation attempts that failed**: no wrapper aggregates the four into a visible channel; `tools/test_hooks.sh:753`
  captures the deep-review reminder with `2>&1 >/dev/null`, i.e. the test pins stderr as the channel, it does not cover
  visibility; `.claude/rules/hook-authoring.md:128` still advises "write to stderr for an advisory hook", a second stale
  instruction feeding the same class.
- **Corrections**:
  1. "Each also writes a mute marker when it fires" is wrong for `check-deep-review.sh`: it has no marker; it mutes only on a
     `agent_type=deep-reviewer` line in `.claude/logs/agent-audit.log` within 8 h (`:9-23`), so it re-emits into the debug log
     on every Stop. Three of four write a marker.
  2. "The only machine backstops for ... 'tag the release' ... are silent" overstates: `session-start.sh:176-190` re-asserts
     an untagged `<Version>` on every startup via SessionStart stdout, which does reach Claude (added by `3156da88` for exactly
     this reason). The verification and deep-review reminders have no such visible twin.
  3. Delta: `check-verification-evidence.sh`, `check-deep-review.sh` and `check-changelog-updated.sh` all exist at `141b749`
     and are registered on Stop there (`git show 141b749:.claude/settings.json`, `"Stop"` block). Only
     `check-version-tagged.sh` is introduced. The defect is pre-existing for three of four.
- **Impact today**: MED. Claude never receives the build and review reminders the docs present as active backstops.

## DOCS-L6-07: generate CHANGELOG at /release instead of hand-editing it

- **Outcome**: CONFIRMED (every measurement reproduces; the recommendation itself is an owner decision item, since it
  replaces a standing rule).
- **Re-measured this run**:
  - Merges: `git log --all --merges` gives 29; a loop over them with `git show --cc --format= <m> -- CHANGELOG.md | grep '^@@@'`
    gives exactly the seven named (`9c532b5e` 08-12, `07a54efe`, `828bf941`, `38246fea`, `2c9aeeee`, `6ebbbd48` all 08-08,
    `f89ef618` 07-02). Five on 2026-08-08 holds. (Reachable from `b2e387db` alone: 27 merges.)
  - Incidents: `dc2d13a5`, `a035a2d3` and `41d4afbe` bodies say what the finding quotes (`94552135` wrote stale
    reconstructions and dropped #582/#588 entries, not restored; CRLF rewrite, 60-line entry to a 22,907-line diff, content
    unaffected; two #434 entries swept into an enlistment commit). `docs/ai-includes/git-and-commits.md:56,60,61` carry the
    three CHANGELOG incident rules as quoted (`:55` is a fourth that names CHANGELOG).
  - `git rev-list --count --since=2026-07-01 b2e387db` = 809; with `-- CHANGELOG.md` = 545.
  - Split: `git merge-base b2e387db bannerlord-1.4.5` = `a4c6d7c3`; 95 commits since on 1.5.x. A scratchpad script over
    `git show b2e387db:CHANGELOG.md` counts 97 `### ` headings in sections dated 2026-09-14 or later (all of them above the
    first older section), 37 of them matching `^type(scope): vX.Y.Z - `; 10 of the 95 subjects miss the format.
  - Rule lines: `AGENTS.md:34,40,80`, `CLAUDE.md:56`, `.claude/rules/external-skill-ports.md:123,132`; 17 skills match
    `git grep -l -i changelog -- '.claude/skills/*/SKILL.md'`; `hook-authoring.md` 3 mentions; hook sizes 5,177 B and
    1,797 B. `release-process.md:79-82` is the `git log --grep 'vX.Y.Z - '` step.
- **By-design check**: no ADR decides a hand-written CHANGELOG over generation. `docs/adrs/010-knowledge-base-architecture.md:36`
  says only "No conversion of CHANGELOG.md into wiki form. It stays as a chronological release log", and ADR-011:72 files it
  in the Archive tier; a generated per-release log satisfies both. The standing rule to change is `AGENTS.md:80`.
- **Corrections**:
  1. Size: `git cat-file -s b2e387db:CHANGELOG.md` = 1,752,857 B, not 1,759,092 B. No committed blob matches the lane's
     figure (`4b5662b2` 1,754,279 B; `c79a5852` 1,750,502 B); the dirty working-tree copy is 1,760,514 B now and growing, so
     the lane most likely measured that moving copy (UNVERIFIED). Still about 1.75 MB.
  2. "The body already is the entry" is only partly true. The top entry (CHANGELOG line 7) is a richer rewrite of the
     `b2e387db` body: it adds the padded `ExcludedRaces` case, the three native sink names and the test timings. Generating
     from bodies would drop that detail unless bodies grow.
  3. The dependents list misses `.claude/hooks/session-start.sh:198-205`, which prints the latest CHANGELOG section into
     every session's context; with a release-time generator it would show the last release, not recent work.
- **Impact today**: MED (process only, no player effect): the file is touched by 545 of 809 commits and has at least
  eight merge or loss incidents between 2026-07-02 and 2026-09-13.

## DOCS-L6-08: README and one agent command table wrong on version, branch, test command and setup

- **Outcome**: CONFIRMED, with scope corrections.
- **Re-read at `b2e387db`** (`git show b2e387db:README.md | cat -n`):
  - Versions: `:3` "Bannerlord v1.4.8", `:37` "v1.4.8 installed", `:175` "v1.5.2 is required". Pin
    `.claude/pinned-game-version.txt` = `v1.5.3`; `Main/_Module/SubModule.xml:32` Native `v1.5.3.*`. Three answers, none right
    for this branch.
  - Branch: `:19` names `bannerlord-1.4.5` the active development branch. `git merge-base b2e387db bannerlord-1.4.5` =
    `a4c6d7c3`; 95 commits since on 1.5.x, 4 on `origin/bannerlord-1.4.5` (tip `c5b84fb4`, 2026-09-15); `v2.0.29`
    (`a12fec9b`) and `v2.0.30` (`96f17fec`) are ancestors of `b2e387db` and not of the 1.4.5 tip. `gh api` confirms the
    GitHub default is `bannerlord-1.4.5`, so that half of the sentence is true.
  - Test command: `:58` `dotnet test TAOM.Tests  # tests only`. Deploy mechanism re-read in the package itself:
    `~/.nuget/packages/bannerlord.buildresources/1.1.0.129/build/Basic.targets:64` runs `CopyModule` AfterTargets
    `PostBuildEvent` when `$(ModuleId) != ''` and `$(GameFolder)` exists, ignoring `DisableModuleCopy`; `Main/TAOM.csproj:7`
    defaults `ModuleId` to the project name. So the README command deploys into the game.
  - `docs/ai-includes/agent-operating-manual.md:43` gives the binding gate with no flags; `:41-42` carry the "does not stop
    deployment" caveat, `:43` does not.
  - Setup: `README.md:55` says the script configures "BANNERLORD_GAME_DIR + dependencies"; `setup-dev-env.ps1` (25 lines)
    only reads a path (`:7`), checks for `Bannerlord.exe` and sets the user variable (`:20`). No dependency step.
  - Counts: `xml.etree` over `git show b2e387db:.../career_system/taom_careers.xml` gives 67 `Career` elements, against "50"
    at `README.md:16,125` and `docs/features/career-system.md:148,173,174,290`. `git grep -o "class Taom[A-Za-z]*Model\b"` over
    `Main/*.cs` gives 50 distinct classes, against "39" at `README.md:15,149`.
  - Linter blind spot: `tools/lint_docs.py:40` scopes to `docs/`; the pin check (`:634-668`) reads the `Target:` line of
    AGENTS.md and CLAUDE.md; the model-count gate (`:1101-1104`) matches only "TAOM has N GameModel overrides" and
    "Existing Overrides (N total)", not the README's wording.
- **Corrections**:
  1. Scope of the version defect: the GitHub front page renders the default branch's README, where `:3` and `:37` say
     v1.4.8 and that branch's `SubModule.xml:26` pins Native `v1.4.8.*`, so they are right there, and the `v1.5.2` install
     line does not exist there (`git diff origin/bannerlord-1.4.5 b2e387db -- README.md` shows `:175` was added only on
     1.5.x). The three-answer contradiction is the 1.5.x README's; the test command (`:58`) and the branch sentence (`:19`)
     are wrong on both lines.
  2. The stale branch claim is wider than README: `docs/localization/TRANSLATOR_GUIDE.md:376` ("Open a PR targeting the active
     branch (currently `bannerlord-1.4.5`)") and the comments at `tools/lint_docs.py:99-105` and
     `tools/tests/test_lint_docs.py:526` assert the same.
  3. Minor: README is 207 lines, not 200; the pin check also reads the API snapshot headers (`lint_docs.py:669-670`).
- **Impact today**: MED. A contributor copying `:58` overwrites the installed module (or fails the build while the game holds
  the DLL); the version and branch errors mislead readers of the 1.5.x tree.

## What I did not cover

- No `dotnet build` or `dotnet test` (brief). No live Stop hook was triggered: DX-L6-06 rests on the vendor hooks page
  (fetched this run, verbatim quotes) plus the scripts' channel, not on an observed missing reminder.
- The Stop decision-control JSON in DX-L6-06's fix sketch (`{"decision":"block","reason":...}` and `stop_hook_active`) was
  not verified: the fetched hooks page was truncated before that section.
- DOCS-L6-07: git-cliff and towncrier details, and the claim that the 10 unformatted commits were IDE commits, were not
  checked. The towncrier-versus-generation choice is an owner preference and was not judged.
- DOCS-L6-08: whether Steam still offers a v1.5.2 beta (so whether "installs the wrong beta" can actually happen) was not
  checked. The other README counts (F8) were not re-measured.
