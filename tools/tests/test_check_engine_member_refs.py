#!/usr/bin/env python3
"""Tests for tools/check_engine_member_refs.ps1 (needs pwsh 7 and the Bannerlord install).

Run:  python -m pytest tools/tests/test_check_engine_member_refs.py -q

The script resolves every engine MemberReference of a DLL against the engine assemblies. Pinned here:
  - negative control: the freshly built TAOM.dll resolves fully against the installed engine
  - positive control: the same DLL against the cached v1.5.3 reference assemblies has misses
    (v1.5.4 added a six-parameter TooltipProperty ctor that the 1.5.3.122374 build lacks)
  - baseline: misses that the baseline also lacks are PRE-EXISTING and do not fail the run
  - bad input exits 2
"""
import os
import shutil
import subprocess
from pathlib import Path

import pytest

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "tools" / "check_engine_member_refs.ps1"
PWSH = shutil.which("pwsh")
GAME = Path(os.environ.get("BANNERLORD_GAME_DIR") or r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
TAOM_DLL = REPO / "TAOM.Tests" / "bin" / "Debug" / "net472" / "TAOM.dll"
OLD_BUILD = "1.5.3.122374-beta"
OLD_PACKAGES = ("core", "native", "sandbox", "storymode", "custombattle")

pytestmark = [
    pytest.mark.skipif(PWSH is None, reason="pwsh is not installed"),
    pytest.mark.skipif(not (GAME / "bin" / "Win64_Shipping_Client").is_dir(), reason="Bannerlord install absent"),
]


def run(*args):
    cmd = [PWSH, "-NoProfile", "-File", str(SCRIPT), *map(str, args)]
    return subprocess.run(cmd, capture_output=True, text=True, timeout=600)


def old_ref_dirs():
    root = Path(os.environ.get("NUGET_PACKAGES") or Path(os.environ["USERPROFILE"]) / ".nuget" / "packages")
    dirs = [root / f"bannerlord.referenceassemblies.{p}" / OLD_BUILD / "ref" / "net472" for p in OLD_PACKAGES]
    if not all(d.is_dir() for d in dirs):
        pytest.skip(f"cached v{OLD_BUILD} reference assemblies absent under {root}")
    return dirs


@pytest.fixture(scope="module")
def taom_dll():
    if not TAOM_DLL.is_file():
        pytest.skip("TAOM.Tests build output absent")
    return TAOM_DLL


def test_fresh_build_resolves_against_installed_engine(taom_dll):
    r = run("-Dll", taom_dll)
    assert r.returncode == 0, r.stdout + r.stderr
    assert " 0 unresolved" in r.stdout


def test_old_engine_reports_missing_members(taom_dll):
    r = run("-Dll", taom_dll, "-EngineDir", ",".join(map(str, old_ref_dirs())))
    assert r.returncode == 1, r.stdout + r.stderr
    assert "MISSING" in r.stdout


def test_baseline_marks_shared_misses_pre_existing(taom_dll):
    joined = ",".join(map(str, old_ref_dirs()))
    r = run("-Dll", taom_dll, "-EngineDir", joined, "-Baseline", joined)
    assert r.returncode == 0, r.stdout + r.stderr
    assert "PRE-EXISTING" in r.stdout
    assert "MISSING NEW" not in r.stdout
    assert "(0 new)" in r.stdout


def test_missing_dll_exits_2():
    r = run("-Dll", REPO / "no_such_file.dll")
    assert r.returncode == 2, r.stdout + r.stderr
