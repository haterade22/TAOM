#!/usr/bin/env bash
# check-native-dll-crt.sh
# PreToolUse(Bash) hook: when `git commit` stages the vendored native
# TAOM.NativeSkinFixes.dll, run tools/pe_inspect.py and BLOCK the commit if the
# DLL links a DYNAMIC C runtime (imports vcruntime*/msvcp140*/ucrtbase*/
# api-ms-win-crt*). A dynamic/debug CRT is absent on players' machines without
# Visual Studio, so LoadLibrary fails with Win32 error 126 and the feature goes
# inert. The DLL MUST link a static CRT (Debug /MTd or Release /MT).
#
# Defense in depth: Build.ps1 already guards its own output, but this catches a
# hand-copied or stale debug DLL that bypasses Build.ps1. See
# docs/features/native-skin-fixes.md "Build & CRT requirement".
#
# Fail-open (per .claude/rules/harness-facts.md "TAOM hooks MUST fail open"):
# no python, no pe_inspect.py, DLL not staged, DLL absent on disk, or any
# internal error ALLOWS the commit. Only a confirmed dynamic-CRT import blocks.
#
# Returns: {} to allow, {"permissionDecision":"deny","message":"..."} to block.

set -uo pipefail

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)

# Extract the bash command from tool_input (mirrors check-moduledata-validation.sh).
COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
    print(d.get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)

# Two-stage git-commit matcher: handle `git -C/-c ... commit`; reject
# `git commit-tree` / `commit-graph` (incl. option-prefixed `git -C . commit-tree`).
# Per .claude/rules/harness-facts.md.
case "$COMMAND" in
    *"git commit-"* | *"git -"*" commit-"*) echo '{}'; exit 0 ;;
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;
    *) echo '{}'; exit 0 ;;
esac

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }

DLL="Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll"

# Only run when this commit stages the vendored DLL. No blanket --amend skip
# (amend is commonly "oops, add a file" -- include HEAD's files on amend).
STAGED=$(git diff --cached --name-only 2>/dev/null)
case "$COMMAND" in
    *"--amend"*)
        STAGED=$(printf '%s\n%s\n' "$STAGED" \
            "$(git show HEAD --name-only --pretty=format: 2>/dev/null)" | sort -u)
        ;;
esac

HAS_DLL=0
while IFS= read -r f; do
    [[ "$f" == "$DLL" ]] && { HAS_DLL=1; break; }
done <<< "$STAGED"
[[ $HAS_DLL -eq 0 ]] && { echo '{}'; exit 0; }

# Fail open if we can't run the check.
PY="$PYBIN"
[[ -z "$PY" ]] && { echo '{}'; exit 0; }
[[ -f tools/pe_inspect.py ]] || { echo '{}'; exit 0; }

# Validate the STAGED BLOB -- what the commit will actually contain -- not the
# on-disk working-tree file. They can differ: a rebuilt static DLL on disk while
# a stale dynamic blob is still what's staged (or vice versa). `git show :PATH`
# is the index version; extract it to a temp file since pe_inspect reads a path.
TMP=$(mktemp 2>/dev/null) || { echo '{}'; exit 0; }
if ! git show ":$DLL" > "$TMP" 2>/dev/null || [[ ! -s "$TMP" ]]; then
    rm -f "$TMP"; echo '{}'; exit 0
fi
# Inner bound, deliberately below this hook's registered 5s timeout. pe_inspect measured
# 52ms on 2026-08-31, so 3s is ~60x headroom rather than a live constraint, but every
# external-tool call in a hook gets one: a harness kill discards output, so an overrun
# would read as "CRT is fine" and vendor a DLL that fails LoadLibrary on players machines.
IMPORTS=$(timeout -k 2 3 "$PY" tools/pe_inspect.py "$TMP" 2>/dev/null)
RC=$?
rm -f "$TMP"
if [[ $RC -eq 124 ]]; then
    printf '%s\n' '{"permissionDecision":"ask","message":"[check-native-dll-crt] pe_inspect exceeded its 3s budget and was stopped, so the vendored native DLL is UNCHECKED for a dynamic CRT link. This is NOT a pass: a dynamic-CRT build fails LoadLibrary with error 126 on players machines. Run: python tools/pe_inspect.py <dll>"}'
    exit 0
fi
[[ -z "$IMPORTS" ]] && { echo '{}'; exit 0; }

# Dynamic CRT imports => the redistributable/debug runtime players lack. A
# static-CRT build (Debug /MTd or Release /MT) imports only MinHook.x64.dll +
# KERNEL32.dll (plus OS-guaranteed DLLs like SHELL32.dll / ole32.dll that the
# static CRT pulls in) and never matches this.
if ! printf '%s' "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then
    echo '{}'; exit 0
fi

# Build a JSON-escaped deny message with the import list (bounded).
MSG=$(printf '%s' "$IMPORTS" | "$PYBIN" -c '
import sys, json
lines = [l for l in sys.stdin.read().splitlines() if l.strip()][-12:]
print(json.dumps(
    "[check-native-dll-crt] git commit BLOCKED: the vendored "
    "Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll links a "
    "DYNAMIC C runtime (imports below). Players without Visual Studio lack those "
    "DLLs, so LoadLibrary fails with Win32 error 126 and NativeSkinFixes goes "
    "inert. Rebuild with a STATIC CRT (Debug /MTd or Release /MT) via "
    "pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1, then re-stage.\n\n"
    + "\n".join(lines)))
' 2>/dev/null)
[[ -z "$MSG" ]] && { echo '{}'; exit 0; }

printf '{"permissionDecision":"deny","message":%s}\n' "$MSG"
exit 0
