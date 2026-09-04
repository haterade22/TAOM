#!/usr/bin/env python3
"""Unit tests for retarget() in rebalance_party_template_maxes.py.

Every test below pins the silent-troop-deletion failure this tool shipped on 2026-09-01.

Retargeting Mordor from 3500 to 260 zeroed 45 stacks across 14 of its 16 lord templates, removing
six Black Numenorean troop types from the game. The tool reported success, the file parsed, the XML
was well formed, `validate_moduledata.py` passed, and 8000 unit tests passed, because nothing checks
that a stack which could previously spawn a troop still can.

Two properties make it worse than an ordinary rounding artefact:

  - A stack at min 0 / max 0 spawns nothing. Vanilla's initial roster fill draws
    `min + (max - min) * r`, and the new-game top-up weights each stack by `(min + max) / 2`, so a
    0/0 stack is unreachable from both.
  - It is permanent. The next retarget scales from the spread, and `0 * anything` is 0 at any future
    target, so the troop cannot be restored by re-running with a higher number. Only git can.

Mordor is the exposure because it carries 52 stacks against the same budget every other culture
spends on 12 to 27, so its thinnest stacks sat at 5 of 3500 and rounded straight to nothing.
"""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import rebalance_party_template_maxes as rb  # noqa: E402


class RetargetKeepsEveryTroopSpawnable(unittest.TestCase):
    """The core invariant: a stack that could spawn a troop must still be able to."""

    def test_thin_stack_is_not_rounded_out_of_existence(self):
        # The shipped case, reduced: one fat stack and one thin one sharing a 14x cut.
        mins = [0, 0]
        maxes = [3495, 5]

        new = rb.retarget(mins, maxes, 260)

        self.assertGreater(new[1], 0,
                           "a troop with a real max must keep a nonzero max, or it is deleted")
        self.assertEqual(sum(new), 260)

    def test_mordor_shaped_template_loses_no_troop(self):
        # 52 stacks against 260, the actual shape that failed. Six stacks at max 5 or 8.
        mins = [0] * 52
        maxes = [120] * 46 + [8, 8, 8, 5, 5, 5]

        new = rb.retarget(mins, maxes, 260)

        self.assertEqual(sum(1 for v in new if v == 0), 0,
                         "no stack may end at max 0 when it started with a real max")
        self.assertEqual(sum(new), 260)

    def test_every_stack_with_spread_keeps_at_least_one(self):
        mins = [2, 0, 7, 0]
        maxes = [900, 4, 700, 3]

        new = rb.retarget(mins, maxes, 150)

        for i, (mn, mx) in enumerate(zip(mins, maxes)):
            if mx > mn:
                self.assertGreater(new[i], mn,
                                   "stack %d had spread and must keep some of it" % i)

    def test_an_already_pinned_stack_is_left_pinned(self):
        # max == min going in is a deliberate fixed-size stack; the floor must not inflate it.
        mins = [0, 40]
        maxes = [500, 40]

        new = rb.retarget(mins, maxes, 200)

        self.assertEqual(new[1], 40, "a stack authored at max == min must stay there")


class RetargetContract(unittest.TestCase):
    """Properties the callers rely on, none of which caught the deletion on their own."""

    def test_min_is_never_touched_and_max_never_falls_below_it(self):
        mins = [5, 12, 0]
        maxes = [900, 600, 40]

        new = rb.retarget(mins, maxes, 200)

        for mn, v in zip(mins, new):
            self.assertGreaterEqual(v, mn, "max must never drop below its own min")

    def test_hits_the_target_exactly_on_an_ordinary_template(self):
        mins = [0] * 12
        maxes = [375] * 12

        self.assertEqual(sum(rb.retarget(mins, maxes, 320)), 320)

    def test_is_idempotent(self):
        mins = [0, 3, 10]
        maxes = [800, 400, 300]

        once = rb.retarget(mins, maxes, 260)
        twice = rb.retarget(mins, once, 260)

        self.assertEqual(once, twice, "re-running on its own output must be a no-op")

    def test_target_below_the_min_sum_is_refused(self):
        with self.assertRaises(ValueError):
            rb.retarget([50, 60], [100, 120], 40)

    def test_scaling_up_works_too(self):
        # The mistymountainorcs case: five templates sat far BELOW their band and were raised.
        mins = [5, 9]
        maxes = [16, 15]

        new = rb.retarget(mins, maxes, 260)

        self.assertEqual(sum(new), 260)
        self.assertTrue(all(v > mn for v, mn in zip(new, mins)))


if __name__ == "__main__":
    unittest.main()
