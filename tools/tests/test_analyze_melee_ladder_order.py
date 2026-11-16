"""The melee ladder report does not depend on string hash order (#1379).

`Troop.items` is a frozenset, so a loop over it runs in hash order, which changes with
PYTHONHASHSEED from one process to the next. Where two weapons tie, the report picked the first
one seen, so the same data gave a different report on another run. The check runs a small fixture
in fresh processes under several seeds and requires one answer from all of them.
"""
from __future__ import annotations

import os
import subprocess
import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]

# Four equal weapons on a low troop, a weaker one on a troop three tiers up: the inversion row
# names one of the four, and place() lists all five.
PROBE = r"""
import sys
from types import SimpleNamespace
sys.path.insert(0, sys.argv[1])
import analyze_melee_ladder as ml

def priced(iid, dps):
    return SimpleNamespace(item=SimpleNamespace(id=iid, template="TwoHandedAxe"), best_dps=dps, best_damage=10)

ties = ["axe_a", "axe_b", "axe_c", "axe_d"]
table = {i: priced(i, 50.0) for i in ties}
table["axe_old"] = priced("axe_old", 10.0)
low = ml.Troop(id="low", name="Low", level=6, culture="c", file="f", items=frozenset(ties))
high = ml.Troop(id="high", name="High", level=31, culture="c", file="f", items=frozenset(["axe_old"]))
rows = ml.tier_inversions(table, [low, high])
print([(r.high.id, r.low.id, r.low_item) for r in rows])
print(list(ml.place(table, [low, high])))
"""


def run(seed: int) -> str:
    env = dict(os.environ, PYTHONHASHSEED=str(seed))
    done = subprocess.run([sys.executable, "-c", PROBE, str(TOOLS)], env=env, capture_output=True,
                          text=True, timeout=120)
    if done.returncode:
        raise AssertionError(f"seed {seed}: exit {done.returncode}\n{done.stderr}")
    return done.stdout


class HashOrderTests(unittest.TestCase):
    def test_ties_resolve_the_same_under_every_hash_seed(self):
        outputs = {seed: run(seed) for seed in range(6)}
        self.assertEqual(len(set(outputs.values())), 1, outputs)
        # The tie goes to the first id in sorted order, so a reader can predict it.
        self.assertIn("'axe_a'", outputs[0].splitlines()[0])


if __name__ == "__main__":
    unittest.main()
