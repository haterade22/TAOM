# Plan 005: Scrub a vendored GitHub Packages credential, pin MCP servers, and close two python-source injection sites

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**:
> `git diff --stat 141b749..HEAD -- .mcp.json .codex/config.toml tools/process_faction_map.py tools/audit_claude_config.py`
> (the `nuget.config` is gitignored, so it won't show in a git diff — verify it
> by hand against the Step 2 excerpt instead.)
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P2 (credential scrub portion is P1 — do it FIRST)
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: security
- **Planned at**: commit `141b749`, 2026-06-13
- **Issue**: create before implementation lands — orchestrator (TAOM issue-first mandate)

## Why this matters

Three independent security-hygiene items, all confirmed by reading the real files this session:

1. **A GitHub Packages username + ClearTextPassword (PAT-shaped) credential pair sits in a vendored `nuget.config`** under `Dependencies/.vendor-source/`. It is NOT a TAOM secret — it is BUTR's own token shipped inside their UIExtenderEx 2.13.2 release source, and the file is **gitignored / untracked** in TAOM (verified below). The risk is that this gitignored reference copy is the kind of file a future `/adopt-external` or port could accidentally drag into the tracked tree. The TAOM action is to **scrub the local copy and add a vet-checklist grep** so it can never ride a port into the public repo; rotation is upstream's call (see Step 2 "Rotation caveat").
2. **The MCP servers in `.mcp.json` and `.codex/config.toml` launch upstream code with no version/rev pin** — every session executes whatever HEAD/latest resolves to, with broad filesystem access (the repo, the entire Bannerlord Modules dir, `E:\LOTRAOMAssets`, `E:\Decompiled_Bannerlord`). A compromised or typosquatted upstream release runs arbitrary code on the dev machine. `.mcp.json`'s `filesystem` server is the pre-existing MED `/security-scan` already reports (`mcp-npx-unpinned`); the serena/git servers and the entire `.codex/config.toml` are not caught by the existing gate today.
3. **`tools/process_faction_map.py` interpolates file paths into Python source that is then run via `python -c`.** A path containing a single quote terminates the raw-string literal and the remainder executes as Python in the child interpreter. The paths are currently TAOM-internal, so this is breakage/injection-by-quote rather than a live RCE — but it's a trivial, correct fix (pass paths as argv, not interpolated source).

When this lands: no live-looking credential on disk in a port-reachable location, MCP servers pinned to known revisions, and the faction-map tool immune to quote-in-path.

## Current state

### Item A — vendored credential (gitignored, untracked)

- `Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2/src/nuget.config` — vendored upstream BUTR source reference copy; contains a `<packageSourceCredentials>` block for the `nuget.pkg.github.com/BUTR` feed.

The credential lives at lines 8–13. **DO NOT decode or print the value.** The relevant block (token value shown only as a placeholder here — the real file has HTML-entity-encoded characters you must NOT reproduce):

```xml
  <packageSourceCredentials>
    <butr.github>
      <add key="Username" value="<HTML-entity-encoded username>" />
      <add key="ClearTextPassword" value="<HTML-entity-encoded PAT — DO NOT REPRODUCE>" />
    </butr.github>
  </packageSourceCredentials>
```

Verified tracking status this session:
- `git ls-files --error-unmatch <path>` → `did not match any file(s) known to git` (NOT tracked).
- `git check-ignore -v <path>` → `.gitignore:112:Dependencies/.vendor-source/` (gitignored — the whole vendor-source tree is ignored; downloaded fresh from BUTR tags each session per `docs/migration/dr3-mcm-internalization-plan.md`).

Consequence: the credential is **not in TAOM's git history**, so there is nothing to scrub from history. Scrubbing the working-tree copy + a vet-checklist guard is the complete TAOM-side fix.

### Item B — unpinned MCP servers

`.mcp.json` (real current lines):

- `filesystem` (lines 22–33) — `npx -y @modelcontextprotocol/server-filesystem ...`, no `@version`. **This is the one the audit tool catches.**
```json
    "filesystem": {
      "type": "stdio",
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/server-filesystem",
        "C:\\Users\\mikew\\source\\repos\\TAOM",
        "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules",
        "E:\\LOTRAOMAssets\\LOTRAOM_Jan_1_Patreon\\Modules\\LOTRAOM\\ModuleData"
      ],
      "env": {}
    },
```
- `serena` (lines 3–17) — `uvx --from git+https://github.com/oraios/serena serena start-mcp-server ...`, no `@<tag/sha>` rev pin. **NOT caught by the audit tool** (it only matches `npx -y`).
```json
    "serena": {
      "type": "stdio",
      "command": "uvx",
      "args": [
        "--from",
        "git+https://github.com/oraios/serena",
        "serena",
        "start-mcp-server",
        ...
```
- `git` (lines 34–39) — `uvx mcp-server-git`, no `==version`. **NOT caught by the audit tool.**
```json
    "git": {
      "type": "stdio",
      "command": "uvx",
      "args": ["mcp-server-git"],
      "env": {}
    },
```

`.codex/config.toml` (real current lines) — **the audit tool does NOT scan this file at all**:

- `filesystem` (lines 12–21) — `npx -y @modelcontextprotocol/server-filesystem ...`, no pin:
```toml
[mcp_servers.filesystem]
command = "npx"
args = [
  "-y",
  "@modelcontextprotocol/server-filesystem",
  "C:\\Users\\mikew\\source\\repos\\TAOM",
  "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBox\\ModuleData",
  "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBoxCore\\ModuleData",
  "E:\\Decompiled_Bannerlord"
]
```
- `git` (lines 23–25) — `uvx mcp-server-git`, no pin:
```toml
[mcp_servers.git]
command = "uvx"
args = ["mcp-server-git"]
```
- Context for why this is sharp: lines 9–10 set `[shell_environment_policy] inherit = "all"`, so child MCP processes receive the user's full environment. (Leave that line alone — it's out of scope; just understand the blast radius.)

### Item C — python-into-`python -c` injection

`tools/process_faction_map.py` — two subprocess sites build a child Python program as an f-string and run it via `[sys.executable, "-c", f"""..."""]`, interpolating paths into the source.

Site 1 — `find_alpha_bbox`, subprocess starts line 160, f-string opens line 161, the path is interpolated at **line 260**:
```python
        result = subprocess.run(
            [sys.executable, "-c", f"""
import struct, zlib, io

def read_png_rgba(path):
    ...
read_png_rgba(r'{filepath}')
"""],
            capture_output=True, text=True, timeout=120
        )
```
(The `r'{filepath}'` at line 260 is the injection point — a single quote in `filepath` ends the raw string and the rest of the path is parsed as Python.)

Site 2 — `crop_png_to_bbox`, subprocess starts line 283, f-string opens line 284, paths interpolated at **line 383**:
```python
    result = subprocess.run(
        [sys.executable, "-c", f"""
import struct, zlib
def crop_and_save(input_path, output_path, cx, cy, cw, ch, max_w):
    ...
crop_and_save(r'{input_path}', r'{output_path}', {x}, {y}, {w}, {h}, {max_width})
"""],
        capture_output=True, text=True, timeout=120
    )
```
(`r'{input_path}'` and `r'{output_path}'` at line 383 are the injection points; the integer args `{x},{y},{w},{h},{max_width}` are safe — they're cast `int`s and a constant.)

> **Note (finding line-number corrections):** the harvest listed Site-2 as "lines 282–284" for the subprocess and gave the interpolation as part of that range. The actual interpolation line for Site 2 is **383** (`crop_and_save(...)`), and the subprocess block opens at **283**. Site-1's interpolation is **260**, subprocess at **160**. Use the line numbers in this plan, not the harvest's.

### Conventions that apply

- This is a config/tooling change — **no C# is touched, so no TDD / no `dotnet build` / no `dotnet test`** is required for this plan. (That is unusual for TAOM; it's correct here because every in-scope file is config or a standalone Python tool, none of which compiles into `TAOM.dll`.)
- The house security gate is `tools/audit_claude_config.py` (run by `/security-scan`). It scans `.mcp.json`, `.claude/`, `CLAUDE.md`, `AGENTS.md`. Verified this session: its MCP rule flags only `npx -y` without `@<digit>` (rule id `mcp-npx-unpinned`, MED, `tools/audit_claude_config.py:249-252`); it does **not** flag `uvx --from git+`, and it does **not** read `.codex/config.toml`. So the audit tool is a partial gate — see Step 5's two-part verification.
- The vendor-source tree is intentionally gitignored and re-downloaded fresh each session (`.gitignore:112`, `docs/migration/dr3-mcm-internalization-plan.md`). Do not un-ignore it.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Security gate (machine) | `python tools/audit_claude_config.py` | no `mcp-npx-unpinned` finding for the `filesystem` server in `.mcp.json` (exit code may be non-zero from *other* pre-existing findings — read the finding list, don't trust exit code alone) |
| Credential scrub check | `grep -rn "packageSourceCredentials\|ClearTextPassword" "Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2/src/nuget.config"` | no matches (or the file is gone) |
| Repo-wide credential sweep | `grep -rln "packageSourceCredentials" Dependencies/` | no matches |
| MCP pin check (.mcp.json) | `grep -n "@modelcontextprotocol/server-filesystem\|git+https://github.com/oraios/serena\|mcp-server-git" .mcp.json` | every match carries a pin (`@<ver>`, `@<sha/tag>`, or `==<ver>`) |
| MCP pin check (.codex) | `grep -n "@modelcontextprotocol/server-filesystem\|mcp-server-git" .codex/config.toml` | every match carries a pin |
| Injection-site check | `grep -n "sys.executable" tools/process_faction_map.py` then read each block | no path is interpolated into the `-c` source string |

(No build/test commands — see "Conventions". Do NOT run `./build.ps1`, `dotnet build`, or any `tools/` script in write mode for this plan.)

## Scope

**In scope** (the only files you should modify):
- `Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2/src/nuget.config` (gitignored — edit on disk; it won't appear in `git status`)
- `.mcp.json`
- `.codex/config.toml`
- `tools/process_faction_map.py`
- `docs/ai-includes/external-repo-adoption.md` (add one vet-checklist line — see Step 3; if this file does not exist, add the line to `.claude/skills/adopt-external/SKILL.md` instead and note it)

**Out of scope** (do NOT touch, even though they look related):
- `tools/audit_claude_config.py` — extending its scan scope to `.codex/config.toml` + `uvx` is a *separate* improvement (the harvest's SEC-01 "Optionally extend" note). Do NOT change the gate in this plan; recommend it in Maintenance notes.
- `.codex/config.toml` line 9–10 `inherit = "all"` — known blast-radius amplifier, but changing env inheritance can break Codex; leave it, flag it.
- `.gitignore` — do NOT un-ignore the vendor-source tree.
- Any C# file. This plan compiles nothing.

## Git workflow

- Branch: work in the dispatched worktree's branch; do NOT push or open a PR.
- The gitignored `nuget.config` edit will NOT show in `git status` / `git add` — that is expected and correct (it stays untracked). Note it in the commit body so a reviewer knows it was done.
- Commits: 50/72 rule, imperative, no AI attribution. Suggested:
  - `chore(security): scrub vendored BUTR credential + add vet-checklist grep`
  - `chore(security): pin MCP servers in .mcp.json and .codex/config.toml`
  - `fix(tools): pass paths as argv to process_faction_map child interpreters`
  - Optional trailers: `Save-compat: none (config + tooling only)`, `Not-tested: MCP startup + faction-map render (no harness; manual)`.

## Steps

> **Urgency order: credential FIRST.** A live-looking PAT on disk is the highest-severity item even though it's gitignored.

### Step 1: Scrub the vendored credential from the local nuget.config

Open `Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2/src/nuget.config`. Delete the entire `<packageSourceCredentials>...</packageSourceCredentials>` block (lines 8–13 in the current file). Leave the `<packageSources>` block intact (the public feed URLs are not secret). The result should look like:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="butr.github" value="https://nuget.pkg.github.com/BUTR/index.json" />
  </packageSources>
</configuration>
```

(Deleting the whole file is also acceptable — TAOM.sln does not build the vendor-source projects, so nothing references this nuget.config. If unsure, prefer scrubbing the credential block only, to keep the reference copy faithful.)

**Rotation caveat — read and report, do not act on it from TAOM:** the brief framed this as "rotate the token." Per the evidence this session, this token belongs to **BUTR (upstream)**, not TAOM — it is shipped in *their* release source, and TAOM cannot rotate someone else's GitHub PAT. The correct external action is to **notify BUTR that their UIExtenderEx 2.13.2 source ships a live GitHub Packages credential** so they can rotate it. In your final report to the orchestrator, surface this as a recommended courtesy disclosure to BUTR — do NOT attempt to log into GitHub or rotate anything yourself (that would be acting on a third party's account; out of scope and not possible).

**Verify**:
`grep -rn "packageSourceCredentials\|ClearTextPassword" "Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2/src/nuget.config"` → no matches (or "No such file" if you deleted it).

### Step 2: Sweep the rest of the vendor tree for the same pattern

Confirm no other vendored drop carries the same credential shape.

`grep -rln "packageSourceCredentials" Dependencies/` → expected: no matches. If any other file matches, STOP and report the paths (do NOT reproduce the values) — that's an out-of-plan finding the orchestrator should fold into the issue.

**Verify**: command above returns nothing.

### Step 3: Add a vet-checklist guard so this can't ride a future port in

Add one line to the `/adopt-external` security-vet checklist so future vendored drops are swept for inline credentials before any file is ported into the tracked tree.

- Open `docs/ai-includes/external-repo-adoption.md`. Find the security-vet / secret-scan checklist section. Add a bullet:
  > - Grep every vendored drop for inline package credentials before porting any file: `grep -rln "packageSourceCredentials\|ClearTextPassword\|<password\|<token" <vendor-dir>` — scrub any hit (and notify upstream if it's their token). See plan 005 / harvest SEC-02.
- If `docs/ai-includes/external-repo-adoption.md` does not exist, add the same bullet to `.claude/skills/adopt-external/SKILL.md` under its security-vet steps, and note the substitution in your report.

**Verify**: `grep -rn "packageSourceCredentials" docs/ai-includes/external-repo-adoption.md .claude/skills/adopt-external/SKILL.md` → at least one match (the new checklist line).

### Step 4: Pin the MCP servers

You must pin to a **known-good** version/rev. Do NOT invent version numbers. Determine the currently-resolving version for each before pinning (read-only — these are status/inspection commands, not write operations):

- npm filesystem server: `npm view @modelcontextprotocol/server-filesystem version` → use that exact version (or a recent published one you confirm exists with `npm view @modelcontextprotocol/server-filesystem versions`).
- `mcp-server-git` (PyPI): `pip index versions mcp-server-git` (or `uvx mcp-server-git --help` won't give a version; check PyPI via `pip index versions`). Use a confirmed published version.
- serena (git): `git ls-remote https://github.com/oraios/serena HEAD` → pin to that commit SHA (or a tagged release if `git ls-remote --tags https://github.com/oraios/serena` shows one). A pinned SHA is the strongest pin for a `git+` source.

> If any of these network commands fail (offline / proxy), this is an **environment failure** per `.claude/rules/environment-failures.md` — STOP and report "couldn't resolve pin version for X, network unavailable"; do NOT guess a version.

Then edit:

**`.mcp.json`** —
- `filesystem`: change the arg `"@modelcontextprotocol/server-filesystem"` → `"@modelcontextprotocol/server-filesystem@<resolved-ver>"`.
- `serena`: change `"git+https://github.com/oraios/serena"` → `"git+https://github.com/oraios/serena@<resolved-sha-or-tag>"`.
- `git`: change `["mcp-server-git"]` → `["mcp-server-git@<resolved-ver>"]` (uvx accepts `pkg@version` syntax).

**`.codex/config.toml`** —
- `filesystem`: same `@<resolved-ver>` pin on the `@modelcontextprotocol/server-filesystem` arg.
- `git`: same `mcp-server-git@<resolved-ver>` pin.

**Verify**:
- `grep -n "@modelcontextprotocol/server-filesystem\|git+https://github.com/oraios/serena\|mcp-server-git" .mcp.json` → every line carries a pin.
- `grep -n "@modelcontextprotocol/server-filesystem\|mcp-server-git" .codex/config.toml` → every line carries a pin.

### Step 5: Run the security gate and confirm the npx finding is resolved

`python tools/audit_claude_config.py`

Read the finding list. The `filesystem` server in `.mcp.json` must **no longer** produce a `mcp-npx-unpinned` finding.

> Two important truths the executor must not get wrong:
> 1. The audit tool **only** catches the `npx -y` case. It will NOT confirm the serena `uvx` rev pin or anything in `.codex/config.toml` — those are verified by the Step 4 greps, not by this tool. Do not expect this gate to vouch for them.
> 2. The tool may still exit non-zero because of **other pre-existing findings** unrelated to this plan (permissions, injection-style doc text, etc.). Success here = "the `mcp-npx-unpinned` finding for `.mcp.json` filesystem is gone," not "exit 0."

**Verify**: `python tools/audit_claude_config.py` output contains no `mcp-npx-unpinned` finding referencing `.mcp.json`.

### Step 6: Close the python-into-`python -c` injection (Site 1, line 160/260)

Refactor `find_alpha_bbox` so the path is passed as an **argv** to the child interpreter rather than interpolated into the source. The child reads it from `sys.argv[1]`.

Target shape:
```python
        result = subprocess.run(
            [sys.executable, "-c", """
import struct, zlib, io, sys

def read_png_rgba(path):
    ...   # body UNCHANGED — only the trailing call changes
read_png_rgba(sys.argv[1])
""", filepath],
            capture_output=True, text=True, timeout=120
        )
```
Key edits, all inside the existing block (do NOT rewrite the PNG-parsing body):
- Change the triple-quoted string from an f-string (`f"""`) to a plain string (`"""`) so `{...}` is no longer interpolated. (Watch the existing `{{...}}` escapes in `print(f"...")` lines INSIDE the child source — once the outer string is no longer an f-string, those doubled braces must become single braces, because they were only doubled to survive the outer f-string. Verify the child's own `print(f"{min_x},...")` renders correctly.)
- Add `sys` to the child's imports.
- Replace `read_png_rgba(r'{filepath}')` with `read_png_rgba(sys.argv[1])`.
- Append `, filepath` as the next list element after the `-c` source string.

> **Brace caveat (load-bearing):** the child source contains `print(f"{{min_x}},{{min_y}},...")` (line 258) — the braces are doubled ONLY because the outer string is currently an f-string. When you drop the outer `f`, change those back to single braces: `print(f"{min_x},{min_y},...")`. If you forget, the child will print literal `{min_x}` text and the parse in the parent (`output.split(",")` → `int(parts[0])`) will throw. Test mentally: outer not-f-string + inner f-string with single braces = correct.

**Verify**: `grep -n "filepath}" tools/process_faction_map.py` → no match for the interpolation `r'{filepath}'`; and the `subprocess.run([sys.executable, "-c", ...` for Site 1 ends with `, filepath]`.

### Step 7: Close the python-into-`python -c` injection (Site 2, line 283/383)

Same treatment for `crop_png_to_bbox`. The child takes the two paths as argv; the integer args stay interpolated (they're safe — cast ints + a constant) OR move them to argv too for cleanliness (your call; paths are the security-critical ones).

Target shape (paths via argv, ints kept as before is acceptable):
```python
    result = subprocess.run(
        [sys.executable, "-c", """
import struct, zlib, sys
def crop_and_save(input_path, output_path, cx, cy, cw, ch, max_w):
    ...   # body UNCHANGED
crop_and_save(sys.argv[1], sys.argv[2], """ + f"{x}, {y}, {w}, {h}, {max_width})" + """
""", input_path, output_path],
        capture_output=True, text=True, timeout=120
    )
```
> The split-string concatenation above is awkward. Cleaner: keep the integers in argv too and make the whole child source a plain string:
```python
    result = subprocess.run(
        [sys.executable, "-c", """
import struct, zlib, sys
def crop_and_save(input_path, output_path, cx, cy, cw, ch, max_w):
    ...   # body UNCHANGED
crop_and_save(sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]),
              int(sys.argv[5]), int(sys.argv[6]), int(sys.argv[7]))
""", input_path, output_path, str(x), str(y), str(w), str(h), str(max_width)],
        capture_output=True, text=True, timeout=120
    )
```
Prefer the second form — it removes ALL interpolation from the child source, so the same brace caveat as Step 6 applies: drop the outer `f`, and the child's own `print(f"{{out_w}}x{{out_h}}")` (line 381) must become `print(f"{out_w}x{out_h}")` (single braces). The body of `crop_and_save` is otherwise UNCHANGED — only the imports (`+ sys`), the trailing call, and the argv list change.

**Verify**: `grep -n "input_path}\|output_path}" tools/process_faction_map.py` → no match for `r'{input_path}'` / `r'{output_path}'`; the Site-2 `subprocess.run` ends with the path/int argv elements.

### Step 8: Confirm the tool still parses (read-only syntax check)

You may not RUN the tool in write mode (it deploys faction-map assets). A byte-compile is read-only and proves you didn't break syntax:

`python -m py_compile tools/process_faction_map.py` → exit 0, no output.

**Verify**: command above exits 0.

## Test plan

This plan touches only config + a standalone tool, so there is no `TAOM.Tests` work and no C# build/test.

- **Item A (credential)**: verified by `grep` (Step 1, 2) — no credential remains; vet-checklist line present (Step 3).
- **Item B (MCP pins)**: verified by `grep` on both config files (Step 4) + the audit gate for the one server it covers (Step 5).
- **Item C (injection)**: verified by `grep` showing no path interpolation (Steps 6, 7) + `python -m py_compile` proving the tool still parses (Step 8).
- **Structurally untestable (name for the `Not-tested:` commit trailer)**: live MCP server startup with the new pins (no harness — would require launching Claude/Codex), and a real `process_faction_map.py` render run (write-mode, deploys assets — out of bounds for an executor). Note both in the commit body.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `grep -rn "packageSourceCredentials\|ClearTextPassword" "Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2/src/nuget.config"` → no matches (or file removed)
- [ ] `grep -rln "packageSourceCredentials" Dependencies/` → no matches
- [ ] `grep -rn "packageSourceCredentials" docs/ai-includes/external-repo-adoption.md .claude/skills/adopt-external/SKILL.md` → ≥1 match (vet-checklist line added)
- [ ] `grep -n "@modelcontextprotocol/server-filesystem\|git+https://github.com/oraios/serena\|mcp-server-git" .mcp.json` → every match carries a pin
- [ ] `grep -n "@modelcontextprotocol/server-filesystem\|mcp-server-git" .codex/config.toml` → every match carries a pin
- [ ] `python tools/audit_claude_config.py` → no `mcp-npx-unpinned` finding referencing `.mcp.json`
- [ ] `grep -n "filepath}\|input_path}\|output_path}" tools/process_faction_map.py` → no path-interpolation matches
- [ ] `python -m py_compile tools/process_faction_map.py` → exit 0
- [ ] No files outside the in-scope list are modified (`git status`; remember the `nuget.config` edit is intentionally invisible to git)
- [ ] `plans/README.md` status row updated

## STOP conditions

Stop and report back (do not improvise) if:

- The `nuget.config` credential block doesn't match the Step 1 excerpt, OR the file is now tracked by git (`git ls-files --error-unmatch <path>` succeeds) — if it's tracked, this is a HIGHER-severity case (credential IS in history); report it so the orchestrator escalates (history-scrub + forced upstream disclosure) rather than treating it as the gitignored case this plan assumes.
- `grep -rln "packageSourceCredentials" Dependencies/` finds matches in files other than the one named — report the paths (NOT the values).
- A pin-resolution network command in Step 4 fails (offline/proxy) — environment failure, report and stop; do NOT guess a version number.
- After dropping the outer `f` in Step 6/7, you're unsure whether a `{...}` inside the child source should be single or double braces — STOP and report rather than guessing; a wrong brace silently corrupts the child's stdout and the parent's parse.
- `python -m py_compile` fails twice after a reasonable fix attempt.
- Any step appears to require editing `tools/audit_claude_config.py`, `.gitignore`, or a C# file — all out of scope; report the need instead of acting.
- You discover the assumption "the MCP servers in `.codex/config.toml` aren't covered by the audit tool" is false (i.e., the tool DOES now scan it) — harmless, but note it so Maintenance notes can be updated.

## Maintenance notes

For the human/agent who owns this after the change lands:

- **The audit gate has a known blind spot this plan does NOT close.** `tools/audit_claude_config.py` only flags `npx -y` unpinned, and only scans `.mcp.json` (not `.codex/config.toml`), and ignores `uvx --from git+`. So the serena rev pin and both `.codex/config.toml` pins are protected only by this plan's one-time grep, not by an ongoing gate. **Recommended follow-up (deliberately deferred, harvest SEC-01 "Optionally extend"):** extend the audit tool to (a) scan `.codex/config.toml`, and (b) flag `uvx --from git+...` without an `@<rev>` and `uvx <pkg>` without `@<ver>`. That is its own plan — it edits the gate, which this plan kept out of scope.
- **Pins rot.** Pinned MCP versions/SHAs go stale silently. Add a bump-cadence note to `docs/migration/dr3-maintenance.md` or the `/security-scan` checklist so someone refreshes them (and re-checks the upstream SHA isn't a force-pushed/rewritten ref) periodically — the harvest's SEC-01 fix sketch calls for exactly this.
- **Upstream credential disclosure:** the BUTR token is theirs to rotate. The courtesy action is a heads-up to BUTR that UIExtenderEx 2.13.2's source ships a live GitHub Packages credential. Surfaced in the executor's report; the orchestrator/maintainer decides whether to file it upstream.
- **Reviewer focus (if a `/deep-review` is run despite this being config-only):** confirm the child-script brace conversion in `process_faction_map.py` (the doubled→single brace flip when the outer f-string is removed) — that's the one spot a silent runtime break could hide. Confirm the `inherit = "all"` line in `.codex/config.toml` was left untouched (out of scope).
- **Deferred out of this plan (and why):** changing `.codex/config.toml`'s `inherit = "all"` to a scoped env allowlist would shrink the MCP blast radius, but env-inheritance changes can break Codex tool startup and need their own test pass — not folded in here.
