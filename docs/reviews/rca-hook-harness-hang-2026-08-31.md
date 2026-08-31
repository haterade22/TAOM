# RCA: the hook harness hung for 20 minutes per Bash call, then two commit gates died fixing it

**Date:** 2026-08-31
**Severity:** HIGH. Sessions unusable overnight; afterwards three gates silently inert.
**Scope:** `.claude/hooks/`, `.claude/skills/{freeze,investigate,context-budget,skill-stocktake}/`,
`.claude/settings.json`, `docs/reference/hooks-catalog.md`, `tools/audit-review-counter.sh`,
`Dependencies/NativeSkinFixes.NativeHooks/Build.ps1`.

## What happened

Overnight sessions stalled in blocks of very close to 20.0 minutes. A first diagnosis blamed
rate-limit exhaustion from a large parallel workflow. That was wrong, and the precision of the
quantization was the tell: network backoff varies, and this did not.

A second session found the mechanism. On this machine `python3` resolves only to
`C:\Users\mikew\AppData\Local\Microsoft\WindowsApps\python3`, a Microsoft Store App Execution
Alias. Executed from Git Bash it prints nothing, never exits, and ignores SIGTERM. It survived
`timeout 20`, a 120 s tool budget, and only gave up as a background task after roughly ten
minutes with exit 126.

TAOM's hooks parsed their stdin JSON with `jq`, falling back to `python3`. `jq` is not installed.
So every JSON-parsing hook took the poisoned branch and never returned.

That session fixed the hang (a new `_pybin.sh` that refuses any interpreter resolving under
`WindowsApps`, plus explicit timeouts on all 27 registrations) and asked for a review. This RCA
covers both the original defect and what the repair introduced.

## Root cause

Not the interpreter. **A sentence in our own documentation.**

`docs/reference/hooks-catalog.md` recorded on **2026-08-20** that "`jq` is not on PATH in this Git
Bash install" and then prescribed the remedy: *"Use the python3 fallback (`block-dangerous-git.sh`
is the model)."* Eleven days later that instruction had been followed into every JSON-parsing hook,
which is exactly what it asked for. The trap was documented before it was sprung, and the document
was still recommending it when this repair began.

Two contributing conditions turned a bad fallback into a 20-minute stall:

1. **No registration carried a `timeout`.** The harness default for a command hook is **600 s**,
   not the 60 s assumed in the first write-up. Matching hooks run in parallel, so a wedged Bash
   call paid one 600 s PreToolUse batch and then one 600 s PostToolUse batch: 1200 s, which is the
   20.0 minutes observed. The original "17 hooks x 60 s" arithmetic landed near the right answer
   for the wrong reason.
2. **`command -v` cannot detect an App Execution Alias.** It succeeds, because a file really does
   exist at that path. Every guard in the codebase was written against "is it installed", and the
   actual question was "is it safe to execute".

## What the repair broke

Fixing a hang by adding timeouts is correct. Sizing those timeouts against each hook's **fast**
path is not, and it converted a loud failure into a silent one. A harness timeout kill **discards
the hook's output and surfaces nothing**, so for a gate "killed" and "passed clean" are the same
observable event.

Measured 2026-08-31 with each hook's exact command:

| Hook | Registered | Actual | Effect |
|---|---|---|---|
| `check-moduledata-validation.sh` | 5 s | **27.0 s** | dead. Lost `BROKEN_ITEM_REF`, `BROKEN_TROOP_REF`, `UNKNOWN_CULTURE`, `LANDLESS_CULTURE`, `MOUNTED_DWARF`, duplicate-id: the CTD classes |
| `check-doc-config-drift.sh` | 5 s | **7.8 s** | dead, while the tree carried real drift it would have blocked on |
| `check-polearm-shield-parity.sh` | 5 s | 2.9 s plus overhead | killed intermittently |

Separately, `validate-push.sh` had its `source "_pybin.sh"` inserted at line 46 while `"$PYBIN"`
is used at line 16. Proven live: a `git push --force origin bannerlord-1.4.5` payload returned
rc=0 with no output. The CLAUDE.md-mandated force-push block on the trunk was unreachable.

Five more registrations were missed entirely: `/freeze` (3) and `/investigate` (2) register
`check-freeze.sh` from SKILL.md frontmatter with no `timeout`, and that script still ran the
original `for PY in python3 python py` loop. The skill this repo mandates for every "this is
broken" report carried the landmine that makes debugging hang.

## Why it was missed

- **The audit scoped itself to `.claude/hooks/`.** Three of six hang sites were skill-owned, and
  the five untimed registrations live in skill frontmatter. A hook is not only a file in the hooks
  directory; it is anything the harness registers.
- **The fix was verified by "does it still run fast", not "does it still gate".** A gate that
  returns instantly because it was killed looks identical to one that passed. Nobody made the
  ModuleData gate deny something after the change.
- **A `source` line inserted mechanically into 15 files landed in the wrong place in one of them,**
  and nothing caught it because the failure mode is silence. `hook-authoring.md` already names this
  shape: treating a sibling as a *detection* template rather than a *full behavioural* one.
- **The 600 s default was never checked.** Assuming 60 s made the stall arithmetic look plausible
  and removed the pressure to look further.

## Fix

- `_pybin.sh`: rejects `*WindowsApps*` on the resolved path, tries `python` before `python3`,
  validates an inherited `TAOM_PYBIN` instead of trusting it, and uses `timeout -k 1 5` (without
  `-k`, GNU `timeout` sends SIGTERM and then waits, which is precisely the failure being guarded).
- `validate-push.sh`: `source` moved above first use. Verified: rc=2 on `--force`, `-f`, and a
  quoted branch name; silent on a feature-branch push.
- Timeouts sized from measurement: ModuleData 60 s, doc-drift 30 s, polearm 20 s. `pe_inspect`
  measured 52 ms, so `check-native-dll-crt` correctly keeps 5 s.
- Every hook that shells out now bounds that work **inside** the script, below the registered
  timeout, and reports rc 124 as `permissionDecision: "ask"` rather than as a pass.
- Six hang sites closed. Outside a hook the spelling is plain `python`; a portable candidate list
  may keep `python3` only behind a WindowsApps rejection.
- `check-freeze.sh` routed through `_pybin.sh`; all five skill-frontmatter registrations timed.
- `mark-verification-run.sh`: the `COMMAND="$INPUT"` raw-payload fallback removed. Since `jq` is
  absent it was the only path taken, so any Bash call whose payload merely mentioned `dotnet test`
  or `build.ps1` muted `check-verification-evidence.sh`. Confirmed against the old logic.
- `.gitattributes` added with `*.sh text eol=lf`. `core.autocrlf` is true and there was no
  attributes file, so one `git checkout` of `.claude/hooks/` would have written CRLF and killed
  every hook at the shebang, simultaneously and silently.
- `session-start.sh` announces a degraded toolchain instead of quietly failing open.
- `docs/reference/hooks-catalog.md` rewritten at the source of the propagation.
- `tools/test_hooks.sh` added and wired into CI.

## Prevention

`tools/test_hooks.sh` fails the build when a registration has no timeout, when a hook runs an
external tool with no inner bound, when an inner bound is not strictly below its registered
timeout, when any hook hangs or breaks the exit-code/JSON contract, and when `python3` appears
unguarded. Proven to fail: a deliberately broken canary hook produced 7 failures across three
checks, and removing it returned the suite to 174 passed / 0 failed.

The durable rules are in `.claude/rules/hook-authoring.md`: *measure the slow path*, *bound
external work inside the script*, *never spell it `python3`*, and *check skill frontmatter too*.

**Residual, not fixed here:** `mark-verification-run.sh` still matches `dotnet test` as a
substring of the parsed command, so a command that greps for that string still marks verification
as run. Narrower than the payload-wide match it replaced, and left alone deliberately to keep this
change scoped.

## Lesson

A gate the harness kills is not a gate. The existing lesson "a gate sitting in an unmerged PR is
not a gate" covered gates that never ran; this is its sibling, a gate that runs, is killed, and
reports nothing, which is indistinguishable from success. Timeouts are part of a gate's contract,
not deployment trivia.

## Handoff: the same audit for LOTRAOM

LOTRAOM was deliberately left untouched by this pass so TAOM could be verified end to end. The
same session edited hooks in both repos, so assume every defect class below is present there until
checked. Run in that repo:

| Check | Command / question |
|---|---|
| A `source` below its first use | `grep -n 'PYBIN\|source .*_pybin' <hook>` for every hook. Line number of `source` must be lower than every `"$PYBIN"`. This is what silently killed TAOM's force-push gate. |
| Timeouts sized to the fast path | For each hook that shells out, `time <the exact command>`, then compare against its registered `timeout`. Anything where runtime approaches the registration is a dead gate, not a slow one. |
| Registrations with no timeout | Check skill frontmatter (`hooks:` blocks in `SKILL.md`) as well as `settings.json`. TAOM had 5 untimed registrations living only in frontmatter, inheriting the 600 s default. |
| Unguarded `python3` | Anywhere outside `.github/workflows/`. Note LOTRAOM's `json-lib.sh` guards Store stubs by checking the candidate's **exit status**, which cannot work against a process that never exits. |
| Missing `.gitattributes` | `git config core.autocrlf` and `ls .gitattributes`. If autocrlf is true with no attributes file, one checkout writes CRLF and kills every hook at the shebang. |
| The two genuinely broken hooks already found there | `check-changelog.sh` (`grep -c` prints `0` and exits 1, so `|| echo "0"` made the value `"0\n0"`, producing the arithmetic error in the logs) and `set-session-title.sh` (missing `hookEventName`; `sessionTitle` itself is valid, only that field was absent). |

`tools/test_hooks.sh` is repo-relative and mostly portable: checks 1, 4 and 5 should run in LOTRAOM
unchanged, while checks 2 and 3 read `.claude/settings.json` and a hardcoded hook list that would
need adjusting to that repo's hooks.
