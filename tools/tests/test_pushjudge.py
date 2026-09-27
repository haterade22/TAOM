"""validate-push.sh's judge in Python (#680): .claude/hooks/_pushjudge.py, run by _shellwords.py verdict.

The bash judge the hook ran before #680 cost about 25 microseconds a word, so a trunk force push
carrying 250 KB or more of quoted text holding `push` outran the 5 s registration and ran unjudged.
The port must give every verdict, and name every target, the bash judge did. SEVEN_C holds every
tools/test_hooks.sh 7c command with the verdict the bash judge gave it at 143f0fa8, on a checkout of
bannerlord-1.5.x or of a feature branch. PINS hold one line per place where a natural Python choice
reads a line differently from bash; each verdict was read from the bash judge's own functions at
143f0fa8 with the current branch pinned. GLOB_OVERBLOCK is the one designed difference: a refspec
pattern holding any bracket, ?, backslash, parenthesis or bar is taken to match every protected name."""
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
    """(block, warn) after judge_command on one line, the current branch pinned."""
    j = pj.Judge(PROTECTED, branch=lambda: branch)
    j.judge_command(line)
    return j.block, j.warn


def verdict(cmd, tool, branch):
    """What the hook answers a command: the reader's candidate lines judged in order."""
    return pj.Judge(PROTECTED, branch=lambda: branch).run(sw.push_candidates(cmd, tool))


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
    # Options: the exact force spellings, --all/--branches/--mirror, --x=y ignored, the value options
    # and their prefixes, every other long option ignored as today (--force-w is a known gap git
    # reads as --force-with-lease), and a short cluster forcing on any f.
    ("option", "feature", "git push --force-with-lease=bannerlord-1.5.x:abc origin feature", "", ""),
    ("option", "feature", "git push --force=x origin bannerlord-1.5.x", "", T1),
    ("option", "feature", "git push --force-w origin bannerlord-1.5.x", "", T1),
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
    ("refspec", T1, "git push --force origin :", T1, ""),
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


def run_cli(args, command, cwd, tool="Bash"):
    payload = json.dumps({"tool_name": tool, "tool_input": {"command": command}, "hook_event_name": "PreToolUse"})
    return subprocess.run([sys.executable, os.path.join(HOOKS, "_shellwords.py")] + args,
                          input=payload.encode("utf-8"), capture_output=True, cwd=cwd, timeout=60)


class CliTests(unittest.TestCase):
    """_shellwords.py verdict <protected...>: one line, block<TAB>target, warn<TAB>target or allow."""

    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.repos = {}
        for branch in ("feature", T1):
            d = os.path.join(cls.tmp.name, branch)
            os.makedirs(d)
            subprocess.run(["git", "init", "-q", d], check=True, capture_output=True)
            subprocess.run(["git", "-C", d, "symbolic-ref", "HEAD", "refs/heads/" + branch], check=True)
            cls.repos[branch] = d

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
                (T1, "git push --force --all origin", ("block\t" + ALL + "\n").encode())):
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

    def test_bad_json_is_allowed(self):
        r = subprocess.run([sys.executable, os.path.join(HOOKS, "_shellwords.py"), "verdict"] + PROTECTED,
                           input=b'{"tool_name":', capture_output=True, cwd=self.repos[T1], timeout=60)
        self.assertEqual((r.returncode, r.stdout), (0, b"allow\n"))

    def test_usage_names_the_verdict_mode(self):
        r = subprocess.run([sys.executable, os.path.join(HOOKS, "_shellwords.py"), "bogus"],
                           input=b"{}", capture_output=True, timeout=60)
        self.assertEqual(r.returncode, 2)
        self.assertIn(b"verdict", r.stderr)

    def test_verdict_mode_writes_no_bytecode_into_the_hooks_folder(self):
        cache = os.path.join(HOOKS, "__pycache__")
        before = set(os.listdir(cache)) if os.path.isdir(cache) else set()
        run_cli(["verdict"] + PROTECTED, "git push origin main", self.repos["feature"])
        after = set(os.listdir(cache)) if os.path.isdir(cache) else set()
        self.assertFalse({n for n in after - before if n.startswith("_pushjudge")})


class ReaderTests(unittest.TestCase):
    def test_push_mode_prints_the_candidate_list(self):
        for cmd, tool in (("git push origin x; ls", "Bash"), ('git -C "E:/R&D" push -o "" x y', "Bash"),
                          ("if ($true) {git push --force origin T}", "PowerShell"), ("ls", "Bash")):
            with self.subTest(cmd=cmd):
                self.assertEqual(sw.push_lines(cmd, tool), "\n".join(sw.push_candidates(cmd, tool)))


if __name__ == "__main__":
    unittest.main()
