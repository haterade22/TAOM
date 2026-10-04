"""Probe merge conflicts between the perf branches without touching any ref (scratch, read-only).

Usage: merge_probe.py [<repo, default .>]
Folds the branches in a proposed order with `git merge-tree --write-tree` (which writes tree objects
only) and reports, at each step, the conflicted paths; then probes every pair on its own.
"""
import itertools
import subprocess
import sys

REPO = sys.argv[1] if len(sys.argv) > 1 else "."
BASE_BRANCH = "perf/engine-performance"
ORDER = [
    "perf/041-profiler-extensions-and-hitch-probe",   # contains 028
    "perf/029-perf-runs-parser",
    "perf/036-anim-memory-probe",
    "perf/030-mission-diagnostics-diet",
    "perf/031-settings-reads-off-hot-paths",
    "perf/032-worker-thread-formation-patch",
    "perf/033-creature-battle-allocations",
    "perf/034-patchshield-per-call-cost",
    "perf/042-xml-merge-load-time",
    "perf/040-load-time-stamps",   # built after the first probe; meets 042 and the settings counts
]


def git(*args):
    return subprocess.run(["git", "-C", REPO, *args], capture_output=True, text=True, encoding="utf-8")


def merge_tree(ours, theirs):
    p = git("merge-tree", "--write-tree", "--name-only", "--no-messages", ours, theirs)
    lines = [ln for ln in p.stdout.splitlines() if ln.strip()]
    tree = lines[0] if lines else ""
    conflicts = lines[1:] if p.returncode == 1 else []
    return p.returncode, tree, conflicts


existing = [b for b in ORDER if git("rev-parse", "--verify", "--quiet", b).returncode == 0]
print("branches:", existing)
# Fold: build a synthetic commit after each clean step so the next merge sees the union.
cur = BASE_BRANCH
for b in existing:
    rc, tree, conflicts = merge_tree(cur, b)
    if rc == 0:
        c = git("commit-tree", tree, "-p", git("rev-parse", cur).stdout.strip(), "-p",
                git("rev-parse", b).stdout.strip(), "-m", f"probe merge {b}")
        cur = c.stdout.strip()
        print(f"fold + {b}: clean")
    else:
        print(f"fold + {b}: CONFLICTS ({len(conflicts)}): {conflicts[:12]}")
print("\npairs:")
for a, b in itertools.combinations(existing, 2):
    rc, _, conflicts = merge_tree(a, b)
    if rc != 0:
        print(f"  {a.split('/')[-1][:28]} x {b.split('/')[-1][:28]}: {conflicts[:8]}")
print("done (tree and commit objects only; no ref was changed)")
