"""validate-push.sh's judge in Python (#680): .claude/hooks/_pushjudge.py, run by _shellwords.py verdict.

The bash judge the hook ran before #680 cost about 25 microseconds a word, so under load a trunk
force push carrying 250 KB of quoted text holding `push` outran the 5 s registration it then had
(idle, 400 KB took 4.9 s) and ran unjudged. The port must give every verdict, and name every
target, the bash judge did. SEVEN_C holds every tools/test_hooks.sh 7c command of 143f0fa8 with the
verdict the bash judge gave it there, on a checkout of bannerlord-1.5.x or of a feature branch. PINS
hold one line per place where a natural Python choice reads a line differently from bash; each
verdict was read from the bash judge's own functions at 143f0fa8 with the current branch pinned.
GLOB_OVERBLOCK is the one designed difference of the port: a refspec pattern holding any bracket, ?,
backslash, parenthesis or bar is taken to match every protected name. ISSUE_689 holds the force
pushes the port still let through (#689), each now refused, and the neighbouring shapes that must
stay allowed; VALUE_PREFIXES holds the value options its review read by the same prefix rule."""
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest

HOOKS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".claude", "hooks")
sys.path.insert(0, HOOKS)
sys.dont_write_bytecode = True  # no __pycache__ folder inside .claude/hooks
import _pushjudge as pj  # noqa: E402
import _shellwords as sw  # noqa: E402

PROTECTED = ["bannerlord-1.5.x", "bannerlord-1.4.5", "main", "master"]
T1, T2 = PROTECTED[0], PROTECTED[1]
ALL = pj.BLOCK_ALL


def judge(line, branch="feature"):
    """(block, warn) after judge_command on one line, the current branch pinned and push.default
    unset, so these rows read no git config (CliTests runs git itself, in scratch repositories)."""
    j = pj.Judge(PROTECTED, branch=lambda: branch, push_default=lambda: "")
    j.judge_command(line)
    return j.block, j.warn


def verdict(cmd, tool, branch):
    """What the hook answers a command: the reader's candidate lines judged in order."""
    return pj.Judge(PROTECTED, branch=lambda: branch, push_default=lambda: "").run(sw.push_candidates(cmd, tool))


# (tool, current branch, command, kind, target)
SEVEN_C = [
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push origin +bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push origin +bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force-with-lease origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force-with-lease origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push -f origin bannerlord-1.4.5', 'block', 'bannerlord-1.4.5'),
    ('PowerShell', 'feature', 'git push -f origin bannerlord-1.4.5', 'block', 'bannerlord-1.4.5'),
    ('Bash', 'feature', 'git push --force origin main', 'block', 'main'),
    ('PowerShell', 'feature', 'git push --force origin main', 'block', 'main'),
    ('Bash', 'feature', 'cd /x\ngit push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'cd /x\ngit push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git status\ngit push origin feature\ngit push -f origin bannerlord-1.4.5', 'block', 'bannerlord-1.4.5'),
    ('PowerShell', 'feature', 'git status\ngit push origin feature\ngit push -f origin bannerlord-1.4.5', 'block', 'bannerlord-1.4.5'),
    ('Bash', 'feature', 'git push --force \\\n  origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force \\\n  origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force `\n  origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force `\n  origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push origin bannerlord-1.5.x', 'warn', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push origin bannerlord-1.5.x', 'warn', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.6.x', 'allow', ''),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.6.x', 'allow', ''),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.0-port', 'allow', ''),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.0-port', 'allow', ''),
    ('Bash', 'feature', 'git push --force origin improve/011-stop-reminders-and-trunk-guard', 'allow', ''),
    ('PowerShell', 'feature', 'git push --force origin improve/011-stop-reminders-and-trunk-guard', 'allow', ''),
    ('Bash', 'feature', 'cd /x\ngit push --force origin improve/011-stop-reminders-and-trunk-guard', 'allow', ''),
    ('PowerShell', 'feature', 'cd /x\ngit push --force origin improve/011-stop-reminders-and-trunk-guard', 'allow', ''),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x 2>&1', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x 2>&1', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x 2>&1 | tail -5', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x 2>&1 | tail -5', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x && echo done', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x && echo done', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x; git status', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x; git status', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x > /dev/null', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x > /dev/null', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x | Out-Null', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x | Out-Null', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x; if ($?) { "ok" }', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x; if ($?) { "ok" }', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git -C /e/repos/TAOM push -f origin bannerlord-1.4.5 2>/dev/null', 'block', 'bannerlord-1.4.5'),
    ('PowerShell', 'feature', 'git -C /e/repos/TAOM push -f origin bannerlord-1.4.5 2>/dev/null', 'block', 'bannerlord-1.4.5'),
    ('Bash', 'feature', 'bash -c "git push --force origin bannerlord-1.5.x; echo x"', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'bash -c "git push --force origin bannerlord-1.5.x; echo x"', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', '(git push --force origin bannerlord-1.5.x)', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', '(git push --force origin bannerlord-1.5.x)', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push -f origin bannerlord-1.4.5; git push origin feature', 'block', 'bannerlord-1.4.5'),
    ('PowerShell', 'feature', 'git push -f origin bannerlord-1.4.5; git push origin feature', 'block', 'bannerlord-1.4.5'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x feature', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x feature', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push origin +bannerlord-1.5.x feature', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push origin +bannerlord-1.5.x feature', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin HEAD:bannerlord-1.5.x 2>&1', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin HEAD:bannerlord-1.5.x 2>&1', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push origin "bannerlord-1.5.x" --force', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push origin "bannerlord-1.5.x" --force', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force --all origin', 'block', 'every branch, both trunks included (--all or --mirror)'),
    ('PowerShell', 'feature', 'git push --force --all origin', 'block', 'every branch, both trunks included (--all or --mirror)'),
    ('Bash', 'feature', 'git push --mirror origin', 'block', 'every branch, both trunks included (--all or --mirror)'),
    ('PowerShell', 'feature', 'git push --mirror origin', 'block', 'every branch, both trunks included (--all or --mirror)'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.4.5 # note', 'block', 'bannerlord-1.4.5'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.4.5 # note', 'block', 'bannerlord-1.4.5'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x\necho done', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x\necho done', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force -o "ci.skip;x" origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force -o "ci.skip;x" origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git -C "E:/R&D/TAOM" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git -C "E:/R&D/TAOM" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git -c "credential.helper=!f() { echo x; }; f" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git -c "credential.helper=!f() { echo x; }; f" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git -C "E:\\a;b" push -f origin bannerlord-1.4.5', 'block', 'bannerlord-1.4.5'),
    ('PowerShell', 'feature', 'git -C "E:\\a;b" push -f origin bannerlord-1.4.5', 'block', 'bannerlord-1.4.5'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x>/dev/null', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x>/dev/null', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x>/dev/null 2>&1', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x>/dev/null 2>&1', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push -f origin bannerlord-1.4.5>nul', 'block', 'bannerlord-1.4.5'),
    ('PowerShell', 'feature', 'git push -f origin bannerlord-1.4.5>nul', 'block', 'bannerlord-1.4.5'),
    ('Bash', 'feature', 'git push --force origin bannerlord-1.5.x>&2', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5.x>&2', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', '# don\'t push to the trunk\ngit -C "E:/R&D/TAOM" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', '# don\'t push to the trunk\ngit -C "E:/R&D/TAOM" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git commit -F - <<\'EOF\'\nDon\'t push yet\nEOF\ngit -C "E:/R&D/TAOM" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git commit -F - <<\'EOF\'\nDon\'t push yet\nEOF\ngit -C "E:/R&D/TAOM" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', "git commit -F - <<'EOF'\ngit push --force origin bannerlord-1.5.x\nEOF", 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "git commit -F - <<'EOF'\ngit push --force origin bannerlord-1.5.x\nEOF", 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin feature 2>&1 | tail -5', 'allow', ''),
    ('PowerShell', 'feature', 'git push --force origin feature 2>&1 | tail -5', 'allow', ''),
    ('Bash', 'feature', 'git push --force origin feature && git log --oneline bannerlord-1.5.x', 'allow', ''),
    ('PowerShell', 'feature', 'git push --force origin feature && git log --oneline bannerlord-1.5.x', 'allow', ''),
    ('Bash', 'feature', 'git push -f origin feature; git push origin bannerlord-1.5.x', 'warn', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push -f origin feature; git push origin bannerlord-1.5.x', 'warn', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push origin bannerlord-1.5.x 2>&1 | tail -3', 'warn', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push origin bannerlord-1.5.x 2>&1 | tail -3', 'warn', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --all origin', 'allow', ''),
    ('PowerShell', 'feature', 'git push --all origin', 'allow', ''),
    ('Bash', 'feature', 'git push --force-with-lease=bannerlord-1.5.x:abc origin feature', 'allow', ''),
    ('PowerShell', 'feature', 'git push --force-with-lease=bannerlord-1.5.x:abc origin feature', 'allow', ''),
    ('Bash', 'feature', 'echo push', 'allow', ''),
    ('PowerShell', 'feature', 'echo push', 'allow', ''),
    ('Bash', 'feature', "git push --force origin 'refs/heads/*'", 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "git push --force origin 'refs/heads/*'", 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', "git push origin '+refs/heads/*:refs/heads/*'", 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "git push origin '+refs/heads/*:refs/heads/*'", 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', "git push --force origin 'refs/heads/bannerlord-*'", 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "git push --force origin 'refs/heads/bannerlord-*'", 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin HEAD:heads/bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin HEAD:heads/bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', "git push --prune --force origin 'refs/heads/*:refs/heads/*'", 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "git push --prune --force origin 'refs/heads/*:refs/heads/*'", 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', "git push --force origin 'refs/heads/feature-*'", 'allow', ''),
    ('PowerShell', 'feature', "git push --force origin 'refs/heads/feature-*'", 'allow', ''),
    ('Bash', 'feature', 'git push --force origin refs/tags/v1', 'allow', ''),
    ('PowerShell', 'feature', 'git push --force origin refs/tags/v1', 'allow', ''),
    ('Bash', 'feature', 'GIT push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'GIT push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', '"/c/Program Files/Git/cmd/git.exe" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', '"/c/Program Files/Git/cmd/git.exe" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'x=`git push --force origin bannerlord-1.5.x`', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'x=`git push --force origin bannerlord-1.5.x`', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin feature # bannerlord-1.5.x later', 'allow', ''),
    ('PowerShell', 'feature', 'git push --force origin feature # bannerlord-1.5.x later', 'allow', ''),
    ('Bash', 'feature', 'git push --force -o bannerlord-1.5.x origin feature', 'allow', ''),
    ('PowerShell', 'feature', 'git push --force -o bannerlord-1.5.x origin feature', 'allow', ''),
    ('PowerShell', 'feature', 'git -c "user.name=a\\" -C "E:/R&D" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git -c "user.name=a\\" b" -C "E:/R&D" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'if ($true) {git push --force origin bannerlord-1.5.x}', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', '1..1 | ForEach-Object {git push --force origin bannerlord-1.5.x}', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "& 'C:\\Program Files\\Git\\cmd\\git.exe' push --force origin bannerlord-1.5.x", 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin bannerlord-1.5`.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', '$(git push --force origin bannerlord-1.5.x)', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push `\n  --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "git commit -m @'\nDon't force push the trunk\n'@; git push origin feature", 'allow', ''),
    ('PowerShell', 'feature', 'git push --force -o ci.skip origin feature', 'allow', ''),
    ('Bash', 'feature', 'echo $\'it\\\'s\'; echo "a #b"; git push --force origin bannerlord-1.5.x; echo done', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'cat <<EOF\nit\'s\nEOF\necho "a #b"; git push --force origin bannerlord-1.5.x; echo done', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git commit -F - <<\'EOF\'\nDon\'t stop\nEOF\ngit log --grep "fix #1"; git push --force origin bannerlord-1.5.x; git log -1', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'cat <<EOF\nit\'s\nEOF\ngit commit -m "x\n#1"; git push --force origin bannerlord-1.5.x; echo', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "Start-Process git -ArgumentList 'push','--force','origin','bannerlord-1.5.x' -Wait", 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "Start-Process git -ArgumentList 'push', '--force', 'origin', 'bannerlord-1.5.x'", 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "[Diagnostics.Process]::Start('git', 'push --force origin bannerlord-1.5.x')", 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', '& ("git") push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', '& (Get-Command git) push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'cat <<EOF\nsay "hi\nEOF\ngit -c "user.name=a #b" push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', "cat <<EOF\nDon't stop\nEOF\ngit push --force -o 'ci #1' origin bannerlord-1.5.x", 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', "echo $'it\\'s'\ngit push --force -o 'ci #1' origin bannerlord-1.5.x", 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin "feature"', 'allow', ''),
    ('Bash', 'feature', 'cat <<EOF\nsay "hi\nEOF\nX="a;b #c" git push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'cat <<EOF\nsay "hi\nEOF\nenv "X=a;b #c" git push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'cat <<EOF\nsay "hi\nEOF\nX="l1\n#2" git push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'cat <<EOF\nsay "hi\nEOF\nX="a|#c" git push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'cat <<EOF\nsay "hi\nEOF\nX="a&#c" git push --force origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', "cat <<EOF\nDon't stop\nEOF\nX='a;b #c' git push --force origin bannerlord-1.5.x", 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', "echo $'it\\'s'\nX='a;b #c' git push --force origin bannerlord-1.5.x", 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git commit -m "x"; git push --force origin feature # bannerlord-1.5.x later', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push --force -o ci.skip origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force -o ci.skip origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push --force --push-option ci.skip origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force --push-option ci.skip origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push --force origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push -o ci.skip origin', 'warn', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push -o ci.skip origin', 'warn', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force -o ci.skip origin', 'allow', ''),
    ('PowerShell', 'feature', 'git push --force -o ci.skip origin', 'allow', ''),
    ('Bash', 'feature', 'git push --force origin ("bannerlord-1.5.x")', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin ("bannerlord-1.5.x")', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force origin $("bannerlord-1.5.x")', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force origin $("bannerlord-1.5.x")', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git -C "E:\\R&D" push --force origin ("bannerlord-1.5.x")', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git -C "E:\\R&D" push --force origin ("bannerlord-1.5.x")', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git -C "E:\\R;D" push --force origin $("bannerlord-1.5.x")', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git -C "E:\\R;D" push --force origin $("bannerlord-1.5.x")', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push --force -o "" origin HEAD:bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force -o "" origin HEAD:bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push -fo ci.skip origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push -fo ci.skip origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push --force -uo ci.skip origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force -uo ci.skip origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push -uo ci.skip origin', 'warn', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push -uo ci.skip origin', 'warn', 'bannerlord-1.5.x'),
    ('Bash', 'feature', 'git push -fo ci.skip origin', 'allow', ''),
    ('PowerShell', 'feature', 'git push -fo ci.skip origin', 'allow', ''),
    ('Bash', 'bannerlord-1.5.x', 'git push --force --push-opt ci.skip origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force --push-opt ci.skip origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push --force --recurse-submodules check origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force --recurse-submodules check origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push --force --repo origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force --repo origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push --force --receive-pack git-receive-pack origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force --receive-pack git-receive-pack origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push --force --exec git-receive-pack origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force --exec git-receive-pack origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push --force -o "ci variable" origin', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force -o "ci variable" origin', 'block', 'bannerlord-1.5.x'),
    ('Bash', 'bannerlord-1.5.x', 'git push --force --push-option=ci.skip origin feature', 'allow', ''),
    ('PowerShell', 'bannerlord-1.5.x', 'git push --force --push-option=ci.skip origin feature', 'allow', ''),
    ('Bash', 'feature', 'git push -vfu origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push -vfu origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', "if (\u2018a #' -ne 'x\u2019) { git push --force origin bannerlord-1.5.x }", 'block', 'bannerlord-1.5.x'),
    ('PowerShell', 'feature', 'git push --force -o \u2018ci #1\u2019 origin bannerlord-1.5.x', 'block', 'bannerlord-1.5.x'),
]

# (trap, current branch, line, block, warn): lines as judge_command receives them.
PINS = [
    # Words split at space, tab and newline only (bash's IFS). str.split() also splits at \v, \f,
    # \x1c to \x1f, \x85, NBSP and the Unicode spaces, and the reader writes \x1f and \x1e inside a
    # word to keep an argument whole: splitting there let `-o "ci variable"` hand the remote a value.
    ("split", "feature", "git push --force origin\x0bbannerlord-1.5.x", "", ""),
    ("split", "feature", "git push --force origin\x0cbannerlord-1.5.x", "", ""),
    ("split", "feature", "git push --force origin\x1cbannerlord-1.5.x", "", ""),
    ("split", "feature", "git push --force origin\x1ebannerlord-1.5.x", "", ""),
    ("split", "feature", "git push --force origin\x1fbannerlord-1.5.x", "", ""),
    ("split", "feature", "git push --force origin\x85bannerlord-1.5.x", "", ""),
    ("split", "feature", "git push --force origin\xa0bannerlord-1.5.x", "", ""),
    ("split", "feature", "git push --force origin\u2003bannerlord-1.5.x", "", ""),
    ("split", "feature", "git push --force origin\u3000bannerlord-1.5.x", "", ""),
    ("split", "feature", "git push --force origin\rbannerlord-1.5.x", "", ""),
    ("split", "feature", "git push --force origin\tbannerlord-1.5.x", T1, ""),
    ("split", "feature", "git  push \t --force   origin  bannerlord-1.5.x", T1, ""),
    ("split", T1, "git push --force -o ci\x1fvariable origin", T1, ""),
    ("split", T1, "git push --force -o \x1e origin", T1, ""),
    # git is recognised after lowering A-Z only, as bash's ${tok,,} does under the hook's C.UTF-8
    # locale; a named UTF-8 locale would also lower U+0130 to i and read g(U+0130)t as git.
    ("git", "feature", "GIT push --force origin master", "master", ""),
    ("git", "feature", "Git.EXE push --force origin master", "master", ""),
    ("git", "feature", "C:\\x\\GIT.exe push --force origin master", "master", ""),
    ("git", "feature", "/usr/bin/Git push --force origin master", "master", ""),
    ("git", "feature", "\\git push --force origin master", "master", ""),
    ("git", "feature", "g\u0130t push --force origin master", "", ""),
    ("git", "feature", "G\u0130T push --force origin master", "", ""),
    ("git", "feature", "gitx push --force origin master", "", ""),
    ("git", "feature", "xgit push --force origin master", "", ""),
    ("git", "feature", "git PUSH --force origin master", "", ""),
    ("git", "feature", "push git --force origin master", "", ""),
    ("git", "feature", "don't push git push --force origin master", "master", ""),
    # CLEAN: the ASCII quotes, parentheses, braces and backtick become spaces, nothing else does.
    ("clean", "feature", "git push --force origin (bannerlord-1.5.x)", T1, ""),
    ("clean", "feature", "git push --force origin {bannerlord-1.5.x}", T1, ""),
    ("clean", "feature", "`git` push --force origin master", "master", ""),
    ("clean", "feature", "git push --force origin \u2018master\u2019", "", ""),
    # A redirection and its target are never refspecs; a word glued to its front is, unless it is
    # empty, ASCII digits (never str.isdigit(), which also takes superscript and other scripts'
    # digits) or `*`. The target of a token ending in < or > is skipped before the value test, so a
    # skipped target leaves an option's pending value for the next word.
    ("redirect", "feature", "git push --force origin bannerlord-1.5.x>/dev/null", T1, ""),
    ("redirect", "feature", "git push --force origin 2> bannerlord-1.5.x", "", ""),
    ("redirect", T1, "git push --force origin 2> bannerlord-1.5.x", T1, ""),
    ("redirect", "feature", "git push --force origin *>bannerlord-1.5.x", "", ""),
    ("redirect", T1, "git push --force origin 2>x", T1, ""),
    ("redirect", T1, "git push --force origin 12>x", T1, ""),
    ("redirect", T1, "git push --force origin \u00b2>x", "", ""),
    ("redirect", T1, "git push --force origin \u0663>x", "", ""),
    ("redirect", T1, "git push --force origin \uff11>x", "", ""),
    ("redirect", T1, "git push --force -o> f origin feature2", T1, ""),
    # Options: the force spellings, --all/--branches/--mirror, the value options and their
    # prefixes, every other long option ignored, and a short cluster forcing on any f. A prefix of
    # a force, --mirror, --all or --branches option is in ISSUE_689, not here, and a value option's
    # prefix that bash missed is in VALUE_PREFIXES. --expire is no push option (git refuses it as
    # unknown), but it starts like --exec, so it takes a value as bash read it: that only refuses more.
    ("option", "feature", "git push --force-with-lease=bannerlord-1.5.x:abc origin feature", "", ""),
    ("option", "feature", "git push --force-if-includes origin bannerlord-1.5.x", T1, ""),
    ("option", "feature", "git push --all --force origin", ALL, ""),
    ("option", "feature", "git push --branches -f origin", ALL, ""),
    ("option", "feature", "git push --mirror origin", ALL, ""),
    ("option", "feature", "git push --all origin", "", ""),
    ("option", T1, "git push --force --repo origin feature", T1, ""),
    ("option", T1, "git push --force --repo=x origin feature", "", ""),
    ("option", T1, "git push --force --pu ci origin", T1, ""),
    ("option", T1, "git push --force --ex x origin", T1, ""),
    ("option", T1, "git push --force --expire origin feature", T1, ""),
    ("option", T1, "git push --force --dry-run origin feature", "", ""),
    ("cluster", "feature", "git push -of origin bannerlord-1.5.x", T1, ""),
    ("cluster", "feature", "git push -vfu origin bannerlord-1.5.x", T1, ""),
    ("cluster", T1, "git push -f -o x origin", T1, ""),
    ("cluster", T1, "git push -f -oo x origin", "", ""),
    ("cluster", T1, "git push -f -uo x origin", T1, ""),
    ("cluster", T1, "git push -f -ou x origin", "", ""),
    ("cluster", T1, "git push -f - origin feature", "", ""),
    ("cluster", "feature", "git push -F origin bannerlord-1.5.x", "", T1),
    # The value skip: judged without the value (SKIP) and with it (KEEP); either blocks, and the
    # warning is the last one written, KEEP's when both warn.
    ("value", "feature", "git push --force -o \x1e origin HEAD:bannerlord-1.5.x", T1, ""),
    ("value", "feature", "git push --force -o origin HEAD:bannerlord-1.5.x", T1, ""),
    ("value", T1, "git push --force -o ci variable origin", "", ""),
    ("value", "feature", "git push origin master -o main", "", "main"),
    # The refspec: one leading + forces, the destination is the text after the last colon, then one
    # refs/ and one heads/ come off; empty, HEAD and @ mean the current branch.
    ("refspec", "feature", "git push --force origin a:b:bannerlord-1.5.x", T1, ""),
    ("refspec", "feature", "git push --force origin bannerlord-1.5.x:feature", "", ""),
    ("refspec", "feature", "git push origin HEAD:+bannerlord-1.5.x", "", ""),
    ("refspec", "feature", "git push origin +HEAD:bannerlord-1.4.5", T2, ""),
    ("refspec", "feature", "git push origin ++bannerlord-1.5.x", "", ""),
    ("refspec", "feature", "git push --force origin refs/heads/bannerlord-1.5.x", T1, ""),
    ("refspec", "feature", "git push --force origin heads/bannerlord-1.5.x", T1, ""),
    ("refspec", "feature", "git push --force origin refs/refs/heads/bannerlord-1.5.x", "", ""),
    ("refspec", "feature", "git push --force origin refs/heads/heads/bannerlord-1.5.x", "", ""),
    ("refspec", T1, "git push --force origin HEAD", T1, ""),
    ("refspec", T1, "git push --force origin @", T1, ""),
    ("refspec", T1, "git push --force origin refs/heads/", T1, ""),
    ("refspec", T1, "git push --force origin refs/HEAD", T1, ""),
    ("refspec", T1, "git push origin HEAD", "", T1),
    ("refspec", "feature", "git push --force origin HEAD", "", ""),
    ("refspec", "", "git push --force origin", "", ""),
    ("refspec", "feature", "git push --force origin main master", "main", ""),
    ("refspec", "feature", "git push origin main master", "", "master"),
    ("refspec", "feature", "git push --force origin :main", "main", ""),
    # A pattern of * and plain characters matches as bash does: * takes any run, the rest is
    # literal, the whole name must match, and the first protected name in list order wins.
    ("glob", "feature", "git push --force origin refs/heads/*", T1, ""),
    ("glob", "feature", "git push origin refs/heads/*", "", T1),
    ("glob", "feature", "git push --force origin refs/heads/bannerlord-*", T1, ""),
    ("glob", "feature", "git push --force origin refs/heads/*.4.*", T2, ""),
    ("glob", "feature", "git push --force origin refs/heads/m*", "main", ""),
    ("glob", "feature", "git push --force origin refs/heads/*r", "master", ""),
    ("glob", "feature", "git push --force origin refs/heads/ma*er", "master", ""),
    ("glob", "feature", "git push --force origin bannerlord-1.5.x*", T1, ""),
    ("glob", "feature", "git push --force origin +refs/heads/*:refs/heads/*", T1, ""),
    ("glob", "feature", "git push --force origin refs/heads/feature-*", "", ""),
    ("glob", "feature", "git push --force origin *a*a*", "", ""),
    ("glob", "feature", "git push --force origin m*n*a*", "", ""),
    ("glob", "feature", "git push --force origin **main", "main", ""),
]

# (current branch, line, block, warn): the bash judge's own answer is in the comment. Git refuses
# such a refspec, so reading it as every protected name can only refuse more.
GLOB_OVERBLOCK = [
    ("feature", "git push --force origin refs/heads/zz?*", T1, ""),               # bash: allowed
    ("feature", "git push --force origin refs/heads/ma[!i]*", T1, ""),            # bash: refused, master
    ("feature", "git push --force origin refs/heads/ma[s]ter*", T1, ""),          # bash: refused, master
    ("feature", "git push --force origin refs/heads/*\\x", T1, ""),               # bash: refused
    ("feature", "git push origin refs/heads/[z]*", "", T1),                       # bash: allowed
    ("feature", "git push --force origin refs/heads/a|b*", T1, ""),               # bash: allowed
]

# (current branch, line, block, warn): the value options read by the prefix rule #689 uses for force
# (#680 review), on top of the bash judge's table, which took a word starting --pu, --rep, --rece,
# --recu or --ex as one. That table missed --e, which git 2.55 runs as --exec (`-f --e
# git-receive-pack origin` force-pushed the checked-out trunk). git refuses --rec as ambiguous
# (--receive-pack or --recurse-submodules); read here as a value option, it only refuses more.
# --end-of-options takes no value, so origin stays the remote (a plain startswith("--e") would have
# taken it). The comment gives the bash judge's answer.
VALUE_PREFIXES = [
    (T1, "git push --force --e x origin", T1, ""),                      # bash: allowed
    (T1, "git push --force --rec x origin", T1, ""),                    # bash: allowed
    (T1, "git push --force --end-of-options origin feature", "", ""),   # bash: allowed
]

# #689: shapes git 2.55 runs as a forced push of a trunk that the port let through, each probed on
# a local bare repository. git reads an unambiguous prefix of a long option as that option
# (--force-w is --force-with-lease and force-updates the trunk it names; --mir and --m are --mirror,
# --al and --b are --all and --branches, which reach both trunks); a prefix of a force option with
# 3 characters or more counts as force, and git refuses the ambiguous ones (--f, --forc), so
# counting those only refuses what git refuses. --force-i alone is no forced push (git rejects a
# non-fast-forward), so counting a --force-if-includes prefix is a deliberate over-block. The
# refspec : (forced by a flag or by +:) and a forced push with no refspec under
# push.default=matching push every branch the remote also has. Each may only refuse more: the
# comment gives what the port answered. (current branch, command, kind, target), under both tools.
MATCHING = "every matching branch, both trunks included (a : refspec or push.default=matching)"
ISSUE_689 = [
    ("feature", "git push --force-w origin bannerlord-1.5.x", "block", T1),              # port: warn
    ("feature", "git push --force-with origin bannerlord-1.5.x", "block", T1),           # port: warn
    ("feature", "git push --force-i origin bannerlord-1.5.x", "block", T1),              # port: warn
    ("feature", "git push --force-w=bannerlord-1.5.x:abc origin bannerlord-1.5.x", "block", T1),  # warn
    ("feature", "git push --forc origin bannerlord-1.5.x", "block", T1),                 # port: warn
    ("feature", "git push --f origin bannerlord-1.5.x", "block", T1),                    # port: warn
    ("feature", "git push --force=x origin bannerlord-1.5.x", "block", T1),              # port: warn
    ("feature", "git push --mir origin", "block", ALL),                                   # port: allow
    ("feature", "git push --mi origin", "block", ALL),                                    # port: allow
    ("feature", "git push --m origin", "block", ALL),                                     # port: allow
    ("feature", "git push -f --al origin", "block", ALL),                                 # port: allow
    ("feature", "git push --force --b origin", "block", ALL),                             # port: allow
    ("feature", "git push --force --bra origin", "block", ALL),                           # port: allow
    ("feature", "git push --force origin :", "block", MATCHING),                          # port: allow
    ("feature", "git push origin +:", "block", MATCHING),                                 # port: allow
    ("feature", "git push -f origin :", "block", MATCHING),                               # port: allow
    ("feature", "git push --force origin feature :", "block", MATCHING),                  # port: allow
    ("bannerlord-1.5.x", "git push --force origin :", "block", MATCHING),                 # port: block, T1
    ("feature", "git -c push.default=matching push --force origin", "block", MATCHING),   # port: allow
    ("feature", "git -c push.default=matching push --force", "block", MATCHING),          # port: allow
    ("feature", "git -c PUSH.DEFAULT=matching push -f origin", "block", MATCHING),        # port: allow
    ("feature", 'git -c "push.default=matching" push --force -o ci.skip origin', "block", MATCHING),
    # Still allowed: no force, a force to a feature branch, a push option only spelled like one.
    ("feature", "git push origin :", "allow", ""),
    ("feature", "git push --follow-tags origin feature", "allow", ""),
    ("feature", "git push --follow-tags origin bannerlord-1.5.x", "warn", T1),
    ("feature", "git push --force-with-lease=feature:abc origin feature", "allow", ""),
    ("feature", "git -c push.default=matching push origin", "allow", ""),
    ("feature", "git -c push.default=matching push --force origin feature", "allow", ""),
    ("feature", "git -c push.default=simple push --force origin", "allow", ""),
    ("feature", "git push --force --prune origin feature", "allow", ""),
    ("feature", "git push --force --dry-run origin feature", "allow", ""),
]


class SevenCTests(unittest.TestCase):
    def test_every_7c_command_gets_the_bash_verdict_and_target(self):
        self.assertGreater(len(SEVEN_C), 150)
        for tool, branch, cmd, kind, target in SEVEN_C:
            with self.subTest(tool=tool, branch=branch, cmd=cmd):
                self.assertEqual(verdict(cmd, tool, branch), (kind, target))


class PinTests(unittest.TestCase):
    def test_each_divergence_trap(self):
        for trap, branch, line, block, warn in PINS:
            with self.subTest(trap=trap, branch=branch, line=line):
                self.assertEqual(judge(line, branch), (block, warn))

    def test_a_pattern_with_other_glob_characters_matches_every_protected_name(self):
        for branch, line, block, warn in GLOB_OVERBLOCK:
            with self.subTest(line=line):
                self.assertEqual(judge(line, branch), (block, warn))

    def test_value_options_are_read_by_the_prefix_rule(self):
        for branch, line, block, warn in VALUE_PREFIXES:
            with self.subTest(line=line):
                self.assertEqual(judge(line, branch), (block, warn))

    def test_issue_689_shapes_refuse_every_trunk(self):
        for branch, cmd, kind, target in ISSUE_689:
            for tool in ("Bash", "PowerShell"):
                with self.subTest(tool=tool, branch=branch, cmd=cmd):
                    self.assertEqual(verdict(cmd, tool, branch), (kind, target))

    def test_a_long_star_pattern_is_cheap(self):
        start = time.perf_counter()
        self.assertEqual(judge("git push --force origin " + "*" * 200000 + "z"), ("", ""))
        self.assertEqual(judge("git push --force origin " + "*a" * 100000 + "*z"), ("", ""))
        self.assertLess(time.perf_counter() - start, 1.0)


class RunTests(unittest.TestCase):
    """Judge.run, the hook's loop: lines in the reader's order, each once, stop at the first block."""

    def test_first_block_wins(self):
        lines = ["git push origin main", "git push --force origin bannerlord-1.4.5",
                 "git push --force origin bannerlord-1.5.x"]
        self.assertEqual(pj.Judge(PROTECTED, branch=lambda: "feature").run(lines), ("block", T2))

    def test_last_warning_wins(self):
        lines = ["git push origin master", "git push origin main"]
        self.assertEqual(pj.Judge(PROTECTED, branch=lambda: "feature").run(lines), ("warn", "main"))

    def test_nothing_to_report(self):
        self.assertEqual(pj.Judge(PROTECTED, branch=lambda: "feature").run([]), ("allow", ""))

    def test_lines_are_judged_as_bash_read_them(self):
        # bash's $( ) drops a NUL, and the reader writes a lone surrogate as ?.
        judge_ = pj.Judge(PROTECTED, branch=lambda: "feature")
        self.assertEqual(judge_.run(["git push --for\x00ce origin master"]), ("block", "master"))
        judge_ = pj.Judge(PROTECTED, branch=lambda: "feature")
        self.assertEqual(judge_.run(["git push --force origin refs/heads/bannerlord-1\ud800*"]), ("block", T1))

    def test_a_line_that_needs_the_branch_asks_git_once(self):
        calls = []
        judge_ = pj.Judge(PROTECTED, branch=lambda: calls.append(1) or "feature")
        lines = [f"git -C /x/r{i} push origin" for i in range(100)] + ["git push origin HEAD", "git push -f o @"]
        self.assertEqual(judge_.run(lines), ("allow", ""))
        self.assertEqual(len(calls), 1)

    def test_a_line_with_a_refspec_never_asks_git(self):
        calls = []
        judge_ = pj.Judge(PROTECTED, branch=lambda: calls.append(1) or "feature")
        self.assertEqual(judge_.run(["git push origin feature", "git push --force origin main"]), ("block", "main"))
        self.assertEqual(calls, [])


class PushDefaultTests(unittest.TestCase):
    """push.default from git's own config (#689): read in the hook's directory only for a forced push
    with no refspec, at most once a run, and `matching` there pushes every matching branch."""

    def judge_(self, value, calls):
        return pj.Judge(PROTECTED, branch=lambda: "feature",
                        push_default=lambda: calls.append(1) or value)

    def test_matching_refuses_a_forced_push_with_no_refspec(self):
        for line in ("git push --force origin", "git push -f", "git push --force -o ci.skip origin"):
            with self.subTest(line=line):
                self.assertEqual(self.judge_("matching", []).run([line]), ("block", MATCHING))

    def test_any_other_value_leaves_the_current_branch(self):
        for value in ("", "simple", "current", "upstream", "nothing"):
            with self.subTest(value=value):
                self.assertEqual(self.judge_(value, []).run(["git push --force origin"]), ("allow", ""))

    def test_asked_only_for_a_forced_push_with_no_refspec(self):
        calls = []
        lines = ["git push origin", "git push --force origin feature", "git push origin :",
                 "git -c push.default=matching push --force origin feature"]
        self.assertEqual(self.judge_("matching", calls).run(lines), ("allow", ""))
        self.assertEqual(calls, [])

    def test_asked_at_most_once_a_run(self):
        calls = []
        lines = [f"git -C /x/r{i} push --force origin" for i in range(100)]
        self.assertEqual(self.judge_("simple", calls).run(lines), ("allow", ""))
        self.assertEqual(len(calls), 1)

    def test_the_command_line_setting_needs_no_lookup(self):
        calls = []
        self.assertEqual(self.judge_("", calls).run(["git -c push.default=matching push --force origin"]),
                         ("block", MATCHING))
        self.assertEqual(calls, [])

    # tools/test_hooks.sh 7c runs these in vp-matching, a feature checkout whose config sets
    # push.default=matching, under both shell tools.
    SEVEN_C_MATCHING = [("git push --force origin", "block", MATCHING),
                        ("git push origin", "allow", ""),
                        ("git push --force origin feature", "allow", "")]

    def test_the_7c_rows_on_a_matching_checkout(self):
        for cmd, kind, target in self.SEVEN_C_MATCHING:
            for tool in ("Bash", "PowerShell"):
                with self.subTest(tool=tool, cmd=cmd):
                    lines = sw.push_candidates(cmd, tool)
                    self.assertEqual(self.judge_("matching", []).run(lines), (kind, target))


def run_cli(args, command, cwd, tool="Bash"):
    payload = json.dumps({"tool_name": tool, "tool_input": {"command": command}, "hook_event_name": "PreToolUse"})
    return subprocess.run([sys.executable, os.path.join(HOOKS, "_shellwords.py")] + args,
                          input=payload.encode("utf-8"), capture_output=True, cwd=cwd, timeout=60)


class CliTests(unittest.TestCase):
    """_shellwords.py verdict <protected...>: one line, block<TAB>target, warn<TAB>target, allow or
    unread. The verdict reads git's own push.default, so each scratch repository sets it in its local
    config, which wins over this machine's global and system settings."""

    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.repos = {}
        for branch in ("feature", T1):
            d = os.path.join(cls.tmp.name, branch)
            os.makedirs(d)
            subprocess.run(["git", "init", "-q", d], check=True, capture_output=True)
            subprocess.run(["git", "-C", d, "symbolic-ref", "HEAD", "refs/heads/" + branch], check=True)
            subprocess.run(["git", "-C", d, "config", "push.default", "simple"], check=True)
            cls.repos[branch] = d
        d = os.path.join(cls.tmp.name, "matching")      # a feature checkout with push.default=matching
        subprocess.run(["git", "init", "-q", d], check=True, capture_output=True)
        subprocess.run(["git", "-C", d, "symbolic-ref", "HEAD", "refs/heads/feature"], check=True)
        subprocess.run(["git", "-C", d, "config", "push.default", "matching"], check=True)
        cls.repos["matching"] = d

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def test_verdicts(self):
        for branch, command, out in (
                ("feature", "git push --force origin bannerlord-1.5.x", b"block\tbannerlord-1.5.x\n"),
                ("feature", "git push origin main", b"warn\tmain\n"),
                ("feature", "git push --force origin feature", b"allow\n"),
                ("feature", "ls", b"allow\n"),
                ("feature", "git push --force origin", b"allow\n"),
                (T1, "git push --force origin", b"block\tbannerlord-1.5.x\n"),
                (T1, "git push --force --all origin", ("block\t" + ALL + "\n").encode()),
                # #689: git's own push.default, read in the hook's directory
                ("matching", "git push --force origin", ("block\t" + MATCHING + "\n").encode()),
                ("matching", "git push -f", ("block\t" + MATCHING + "\n").encode()),
                ("matching", "git push origin", b"allow\n"),
                ("matching", "git push --force origin feature", b"allow\n")):
            with self.subTest(branch=branch, command=command):
                r = run_cli(["verdict"] + PROTECTED, command, self.repos[branch])
                self.assertEqual((r.returncode, r.stdout), (0, out))

    def test_protected_names_come_from_the_arguments(self):
        r = run_cli(["verdict", "release"], "git push --force origin release", self.repos["feature"])
        self.assertEqual(r.stdout, b"block\trelease\n")
        r = run_cli(["verdict"], "git push --force origin bannerlord-1.5.x", self.repos["feature"])
        self.assertEqual(r.stdout, b"allow\n")

    def test_powershell_command(self):
        r = run_cli(["verdict"] + PROTECTED, "if ($true) {git push --force origin bannerlord-1.5.x}",
                    self.repos["feature"], tool="PowerShell")
        self.assertEqual(r.stdout, b"block\tbannerlord-1.5.x\n")

    def test_candidate_lines_are_never_split_again(self):
        # Past the reader's re-split limit only the whole line holds this push, and \x1c is a line
        # break to str.splitlines(): split there, the refspec falls off the line.
        command = "git push --force origin\x1cx bannerlord-1.5.x " + "y" * (sw.WORDS_KEPT_MAX + 10)
        r = run_cli(["verdict"] + PROTECTED, command, self.repos["feature"])
        self.assertEqual(r.stdout, b"block\tbannerlord-1.5.x\n")

    def test_a_payload_that_does_not_parse_is_unread(self):
        # validate-push.sh sends `unread` down its no-verdict path, which asks on a force marker in
        # the raw text: the answer is never a silent allow (#680 review).
        for raw in (b'{"tool_name":"Bash","tool_input":{"command":"git push --force origin main"}', b"[1]"):
            with self.subTest(raw=raw):
                r = subprocess.run([sys.executable, os.path.join(HOOKS, "_shellwords.py"), "verdict"] + PROTECTED,
                                   input=raw, capture_output=True, cwd=self.repos[T1], timeout=60)
                self.assertEqual((r.returncode, r.stdout), (0, b"unread\n"))

    def test_usage_names_the_verdict_mode(self):
        r = subprocess.run([sys.executable, os.path.join(HOOKS, "_shellwords.py"), "bogus"],
                           input=b"{}", capture_output=True, timeout=60)
        self.assertEqual(r.returncode, 2)
        self.assertIn(b"verdict", r.stderr)

    def test_verdict_mode_writes_no_bytecode_into_the_hooks_folder(self):
        # Run from a fresh copy of the two modules: a cache the hooks folder already holds from an
        # earlier run made the old form of this test pass whatever the reader did. The variables that
        # would stop or move the cache are dropped, so only the reader's own setting can keep it out.
        env = {k: v for k, v in os.environ.items() if k not in ("PYTHONDONTWRITEBYTECODE", "PYTHONPYCACHEPREFIX")}
        with tempfile.TemporaryDirectory() as tmp:
            for name in ("_shellwords.py", "_pushjudge.py"):
                with open(os.path.join(HOOKS, name), "rb") as src, open(os.path.join(tmp, name), "wb") as dst:
                    dst.write(src.read())
            payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "git push origin main"}})
            r = subprocess.run([sys.executable, os.path.join(tmp, "_shellwords.py"), "verdict"] + PROTECTED,
                               input=payload.encode("utf-8"), capture_output=True, cwd=self.repos["feature"],
                               env=env, timeout=60)
            self.assertEqual(r.stdout, b"warn\tmain\n")
            self.assertEqual(sorted(os.listdir(tmp)), ["_pushjudge.py", "_shellwords.py"])


if __name__ == "__main__":
    unittest.main()
