#!/usr/bin/env python3
"""Unit tests for the voice clip-length gate (tools/audit_voice_clip_lengths.py).

Run:  python -m unittest discover -s tools/tests -t .
  or:  python tools/tests/test_audit_voice_clip_lengths.py

Most tests build a SYNTHETIC module tree with generated WAVs, so the gate's contract is pinned
independently of the real dwarf audio. That matters here: the gate exists to notice when the real
data is edited back into a broken state, so its tests must not assume the real data is good.

Each test maps to a way this gate could be wrong and still exit 0. The three marked (false PASS)
were demonstrated against the first version of the tool during the 2026-09-06 deep review and are
the reason this file exists:

  - detects_long_clip_on_grunt          -> the reported defect itself
  - passes_when_clip_is_short           -> the fix is recognised (no permanent red)
  - unknown_voice_type_is_a_finding     -> (false PASS) `type="grunt"` escaped every length rule
  - baseline_pin_blocks_a_regrown_clip  -> (false PASS) a baselined clip re-cut longer stayed green
  - missing_registered_file_exits_two   -> (false PASS) a renamed file silently checked less
  - zero_bindings_exits_two             -> a run that inspects nothing must not read as clean
  - baseline_without_pin_exits_two      -> an unpinned exemption is a blank cheque
  - baseline_pin_still_exempts          -> the ratchet does not fire on the pinned length
  - missing_category_is_a_finding       -> Native: an invalid category is never played
  - unknown_category_is_a_finding       -> same, for a typo
  - duplicate_module_sound_is_a_finding -> last-one-wins would measure the wrong file
  - real_* (3)                          -> the three decoders, against real repo assets
"""
import os
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
TOOL = REPO / "tools" / "audit_voice_clip_lengths.py"
SOUNDS = REPO / "Main" / "_Module" / "ModuleSounds"

sys.path.insert(0, str(REPO / "tools"))
import audit_voice_clip_lengths as gate  # noqa: E402


def write_wav(path: Path, seconds: float, rate: int = 44100, bits: int = 16, channels: int = 1):
    """A minimal PCM WAV of a given duration. Silent; only the header maths matters here."""
    path.parent.mkdir(parents=True, exist_ok=True)
    byte_rate = rate * channels * bits // 8
    data = b"\x00" * int(round(seconds * byte_rate))
    fmt = struct.pack("<HHIIHH", 1, channels, rate, byte_rate, channels * bits // 8, bits)
    body = b"WAVE" + b"fmt " + struct.pack("<I", len(fmt)) + fmt \
        + b"data" + struct.pack("<I", len(data)) + data
    path.write_bytes(b"RIFF" + struct.pack("<I", len(body)) + body)


class Tree:
    """A throwaway module tree the gate can be pointed at."""

    def __init__(self, root: Path):
        self.root = root
        self.md = root / "ModuleData"
        self.snd = root / "ModuleSounds"
        self.md.mkdir(parents=True, exist_ok=True)
        self.snd.mkdir(parents=True, exist_ok=True)
        self.defs = ["ModuleData/voices_voice_def.xml"]

    def sounds(self, body: str):
        (self.md / "module_sounds.xml").write_text(
            '<?xml version="1.0" encoding="utf-8"?>\n<base type="module_sound">\n'
            "  <module_sounds>\n" + body + "\n  </module_sounds>\n</base>\n",
            encoding="utf-8")

    def voices(self, body: str, name: str = "voices_voice_def.xml"):
        (self.md / name).write_text(
            '<?xml version="1.0" encoding="utf-8"?>\n<voice_definitions>\n'
            '  <voice_definition name="test_01" sound_and_collision_info_class="human">\n'
            + body + "\n  </voice_definition>\n</voice_definitions>\n",
            encoding="utf-8")

    def mbproj(self):
        rows = "".join(
            '  <file id="soln_voice_definitions" name="{}" type="voice_definitions" />\n'.format(d)
            for d in self.defs)
        (self.md / "project.mbproj").write_text(
            '<?xml version="1.0" encoding="utf-8"?>\n<base type="solution">\n' + rows
            + '  <file id="soln_module_sound" name="ModuleData/module_sounds.xml" '
              'type="module_sound" />\n</base>\n', encoding="utf-8")

    def run(self, baseline: str = ""):
        self.mbproj()
        proc = subprocess.run(
            [sys.executable, str(TOOL), "--module-data", str(self.md),
             "--sounds", str(self.snd), "--baseline", baseline],
            capture_output=True, text=True)
        return proc.returncode, proc.stdout + proc.stderr


class VoiceClipGateTests(unittest.TestCase):

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.t = Tree(Path(self._tmp.name))

    def _one_clip(self, seconds, slot="Grunt", category="mission_voice", name="clip.wav"):
        write_wav(self.t.snd / name, seconds)
        self.t.sounds('    <module_sound name="G" sound_category="{}">\n'
                      '      <variation path="{}" weight="1" />\n'
                      "    </module_sound>".format(category, name))
        self.t.voices('    <voice type="{}" path="G" face_anim="grunt" />'.format(slot))

    # --- the defect itself -------------------------------------------------------------

    def test_detects_long_clip_on_grunt(self):
        self._one_clip(8.0)
        rc, out = self.t.run()
        self.assertEqual(rc, 1, out)
        self.assertIn("must stay under", out)

    def test_passes_when_clip_is_short(self):
        self._one_clip(0.55)
        rc, out = self.t.run()
        self.assertEqual(rc, 0, out)
        self.assertIn("OK:", out)

    def test_yell_rejects_a_speech_length_take(self):
        # (false PASS) `Yell` was absent from the bar set, and its `mission_voice_shout` category
        # allows 8s, so re-binding the 5.4 to 7.1s warcries to Yell passed cleanly. That is half
        # the reported bug and the gate said OK.
        self._one_clip(6.98, slot="Yell", category="mission_voice_shout")
        rc, out = self.t.run()
        self.assertEqual(rc, 1, out)
        self.assertIn("must stay under 3.5s", out)

    def test_yell_accepts_a_real_bark(self):
        # The dwarf Battlecries bank tops out at 3.09s and must stay green.
        self._one_clip(3.09, slot="Yell", category="mission_voice_shout")
        rc, out = self.t.run()
        self.assertEqual(rc, 0, out)

    def test_long_clip_on_a_rare_slot_is_fine(self):
        # Charge fires once per order, on one agent. A 7s line there is the intended design.
        self._one_clip(7.0, slot="Charge", category="mission_voice_shout")
        rc, out = self.t.run()
        self.assertEqual(rc, 0, out)

    # --- false PASS #1: slot names were never validated ---------------------------------

    def test_unknown_voice_type_is_a_finding(self):
        # Lowercase `grunt` is not a declared voice type. It is a dead binding in engine, and in
        # the first version of this tool it also slipped past SHORT_SLOTS and shipped an 8s clip.
        self._one_clip(8.0, slot="grunt", category="mission_voice_shout")
        rc, out = self.t.run()
        self.assertEqual(rc, 1, out)
        self.assertIn("not one of the engine's 62 declared voice types", out)

    def test_declared_type_with_wrong_case_does_not_normalise(self):
        self._one_clip(0.5, slot="PAIN")
        rc, out = self.t.run()
        self.assertEqual(rc, 1, out)

    # --- false PASS #2: a baseline entry was a blank cheque ------------------------------

    def _baseline(self, text):
        p = Path(self._tmp.name) / "baseline.txt"
        p.write_text(text, encoding="utf-8")
        return str(p)

    def test_baseline_pin_still_exempts(self):
        self._one_clip(8.0)
        rc, out = self.t.run(self._baseline("Grunt|clip.wav@8.00\n"))
        self.assertEqual(rc, 0, out)
        self.assertIn("ACCEPTED", out)

    def test_baseline_pin_blocks_a_regrown_clip(self):
        # Accepted at 4.11s, since re-cut to 8s. The exemption must not follow it.
        self._one_clip(8.0)
        rc, out = self.t.run(self._baseline("Grunt|clip.wav@4.11\n"))
        self.assertEqual(rc, 1, out)
        self.assertIn("has since grown", out)

    def test_baseline_without_pin_exits_two(self):
        self._one_clip(8.0)
        rc, out = self.t.run(self._baseline("Grunt|clip.wav\n"))
        self.assertEqual(rc, 2, out)
        self.assertIn("no @<seconds> pin", out)

    # --- false PASS #3: a run that checked nothing looked clean --------------------------

    def test_missing_registered_file_exits_two(self):
        self._one_clip(0.5)
        self.t.defs.append("ModuleData/renamed_voice_def.xml")
        rc, out = self.t.run()
        self.assertEqual(rc, 2, out)
        self.assertIn("registers a voice definition that is missing", out)

    def test_zero_bindings_exits_two(self):
        write_wav(self.t.snd / "clip.wav", 0.5)
        self.t.sounds('    <module_sound name="G" sound_category="mission_voice">\n'
                      '      <variation path="clip.wav" weight="1" />\n    </module_sound>')
        self.t.voices("")           # a definition with no <voice> children
        rc, out = self.t.run()
        self.assertEqual(rc, 2, out)
        self.assertIn("measured 0 clips", out)

    # --- category validity (Native: an invalid category is never played) -----------------

    def test_missing_category_is_a_finding(self):
        write_wav(self.t.snd / "clip.wav", 0.5)
        self.t.sounds('    <module_sound name="G">\n'
                      '      <variation path="clip.wav" weight="1" />\n    </module_sound>')
        self.t.voices('    <voice type="Grunt" path="G" face_anim="grunt" />')
        rc, out = self.t.run()
        self.assertEqual(rc, 1, out)
        self.assertIn("no sound_category", out)

    def test_unknown_category_is_a_finding(self):
        self._one_clip(0.5, category="mision_voice")     # typo
        rc, out = self.t.run()
        self.assertEqual(rc, 1, out)
        self.assertIn("not one of the 21", out)

    def test_duplicate_module_sound_is_a_finding(self):
        write_wav(self.t.snd / "a.wav", 8.0)
        write_wav(self.t.snd / "b.wav", 0.5)
        self.t.sounds('    <module_sound name="G" sound_category="mission_voice">\n'
                      '      <variation path="a.wav" weight="1" />\n    </module_sound>\n'
                      '    <module_sound name="G" sound_category="mission_voice">\n'
                      '      <variation path="b.wav" weight="1" />\n    </module_sound>')
        self.t.voices('    <voice type="Grunt" path="G" face_anim="grunt" />')
        rc, out = self.t.run()
        self.assertEqual(rc, 1, out)
        self.assertIn("more than once", out)

    def test_unresolvable_path_is_a_finding(self):
        write_wav(self.t.snd / "clip.wav", 0.5)
        self.t.sounds('    <module_sound name="G" sound_category="mission_voice">\n'
                      '      <variation path="clip.wav" weight="1" />\n    </module_sound>')
        self.t.voices('    <voice type="Grunt" path="NoSuchGroup" face_anim="grunt" />')
        rc, out = self.t.run()
        self.assertEqual(rc, 1, out)
        self.assertIn("no <module_sound> of that name", out)

    def test_vanilla_fmod_event_is_skipped(self):
        write_wav(self.t.snd / "clip.wav", 0.5)
        self.t.sounds('    <module_sound name="G" sound_category="mission_voice">\n'
                      '      <variation path="clip.wav" weight="1" />\n    </module_sound>')
        self.t.voices('    <voice type="Grunt" path="G" face_anim="grunt" />\n'
                      '    <voice type="UseLadders" '
                      'path="event:/voice/combat/male/04/commands/use_ladders" face_anim="a" />')
        rc, out = self.t.run()
        self.assertEqual(rc, 0, out)

    def test_empty_data_chunk_is_a_finding(self):
        self._one_clip(0.0)
        rc, out = self.t.run()
        self.assertEqual(rc, 1, out)
        self.assertIn("empty data chunk", out)


class DecoderTests(unittest.TestCase):
    """The three decoders, against real repo assets. Skipped if the assets move."""

    def _need(self, rel):
        p = SOUNDS / rel
        if not p.is_file():
            self.skipTest("asset not present: {}".format(rel))
        return p

    def test_real_pcm_wav(self):
        p = self._need("LOTR/Dwarf/D1_Grunts1.wav")
        self.assertAlmostEqual(gate.duration_of(p), 7.93, places=1)

    def test_real_ima_adpcm_wav_does_not_raise(self):
        # Python's `wave` raises "unknown format: 17" on these; the byte-rate path must not.
        p = self._need("LOTR/Isengard/Voice/Uruk/AAAh.wav")
        secs = gate.duration_of(p)
        self.assertTrue(1.0 < secs < 1.3, secs)

    def test_real_cbr_mp3(self):
        p = self._need("LOTR/Dwarf/dwarf_grunt.mp3")
        self.assertAlmostEqual(gate.duration_of(p), 0.55, places=1)

    def test_real_vbr_mp3_uses_xing_frame_count(self):
        # 38 files in this tree carry a Xing/Info frame. Measured by nominal first-frame bitrate
        # these read long or short; the frame count is exact.
        p = self._need("Native/Ambient/Fireworks 1.mp3")
        secs = gate.duration_of(p)
        self.assertTrue(3.5 < secs < 4.5, "expected ~4.03s from the Xing frame count, got %s" % secs)

    def test_unsupported_extension_is_a_clip_error(self):
        with self.assertRaises(gate.ClipError):
            gate.duration_of(Path("nope.flac"))


class ShippedDataTests(unittest.TestCase):
    """The real repo data must pass its own gate."""

    def test_repo_is_green(self):
        proc = subprocess.run([sys.executable, str(TOOL)], capture_output=True, text=True)
        self.assertEqual(proc.returncode, 0, proc.stdout + proc.stderr)

    def test_baseline_does_not_mask_the_dwarf_defect(self):
        text = (REPO / "tools" / "voice-clip-baseline.txt").read_text(encoding="utf-8")
        for line in text.splitlines():
            line = line.split("#", 1)[0].strip()
            if not line:
                continue
            slot, _, rest = line.partition("|")
            if rest.startswith("LOTR/Dwarf/"):
                self.assertNotIn(slot, {"Grunt", "Yell", "Focus", "Death", "Victory"},
                                 "the fixed dwarf defect must never be baselined: " + line)


if __name__ == "__main__":
    unittest.main()
