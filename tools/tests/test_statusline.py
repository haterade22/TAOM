"""Tests for .claude/statusline.sh: the usage line and the context reading (#1012).

The script reads Claude Code's status line payload on stdin. Each test pipes a payload through the
real script with a fixed clock (TAOM_STATUSLINE_NOW) and a working directory outside any repository,
so the branch segment is the stable "?".
"""
import json
import os
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / ".claude" / "statusline.sh"
BASH = shutil.which("bash") or "C:/Program Files/Git/bin/bash.exe"
NOW = 1_800_000_000


def payload(cwd, rate_limits=True, context_first=True, cache_warm=True):
    ctx = {"context_window": {"context_window_size": 200000, "used_percentage": 45,
                              "remaining_percentage": 55, "current_usage": {"input_tokens": 9}}}
    limits = {"rate_limits": {"five_hour": {"used_percentage": 25, "resets_at": NOW + 5400},
                              "seven_day": {"used_percentage": 85, "resets_at": NOW + 100800}}}
    data = {"model": {"display_name": "Opus 5.5"}, "workspace": {"current_dir": cwd}, "cwd": cwd}
    parts = [ctx, limits] if context_first else [limits, ctx]
    for p in parts:
        if p is limits and not rate_limits:
            continue
        data.update(p)
    if rate_limits:
        data["prompt_cache"] = {"warm": cache_warm, "expires_at": NOW + 1800 if cache_warm else None}
    return json.dumps(data, separators=(",", ":"))


class StatusLineTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.dir, True)

    def run_line(self, text):
        env = dict(os.environ, TAOM_STATUSLINE_NOW=str(NOW))
        p = subprocess.run([BASH, str(SCRIPT)], input=text.encode("utf-8"), capture_output=True,
                           env=env, cwd=self.dir, timeout=30)
        self.assertEqual(p.returncode, 0, p.stderr.decode("utf-8", "replace"))
        return p.stdout.decode("utf-8", "replace").rstrip("\n").split("\n")

    def test_usage_line_shows_both_windows_with_countdowns(self):
        lines = self.run_line(payload(self.dir.replace("\\", "/")))
        self.assertEqual(lines[0], "ctx: 45% | Opus 5.5 | ?")
        self.assertEqual(len(lines), 2, lines)
        self.assertIn("5h: 25% (resets 1h30m)", lines[1])
        self.assertIn("week: 85% (!) (resets 1d4h)", lines[1])
        self.assertRegex(lines[1], r"cache: warm until \d\d:\d\d$")

    def test_no_rate_limits_keeps_the_single_line(self):
        self.assertEqual(self.run_line(payload(self.dir.replace("\\", "/"), rate_limits=False)),
                         ["ctx: 45% | Opus 5.5 | ?"])

    def test_ctx_reads_the_context_window_whatever_the_key_order(self):
        lines = self.run_line(payload(self.dir.replace("\\", "/"), context_first=False))
        self.assertEqual(lines[0], "ctx: 45% | Opus 5.5 | ?")

    def test_cold_cache(self):
        lines = self.run_line(payload(self.dir.replace("\\", "/"), cache_warm=False))
        self.assertTrue(lines[1].endswith("cache: cold"), lines)

    def test_an_empty_payload_still_prints_one_line(self):
        self.assertEqual(self.run_line(""), ["ctx: -- | ? | ?"])


if __name__ == "__main__":
    unittest.main()
