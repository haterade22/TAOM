#!/usr/bin/env python3
import importlib.util
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "quad_remesh_job.py")

_spec = importlib.util.spec_from_file_location("quad_remesh_job", SCRIPT)
qr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(qr)


class PathAndSettingsTests(unittest.TestCase):
    def test_bridge_dir_uses_the_official_job_location(self):
        root = os.path.join("tmp", "bridge-root")
        self.assertEqual(
            qr.bridge_dir(root),
            os.path.join(root, "Exoside", "QuadRemesher", "Blender"),
        )

    def test_settings_include_host_version_and_thread_limit(self):
        text = qr.settings_text(
            "in.fbx", "out.fbx", "progress.txt", 8000,
            host_app_version="5.2.3 LTS", max_threads=6,
        )
        settings = dict(line.split("=", 1) for line in text.splitlines())
        self.assertEqual(settings["HostApp"], "Blender")
        self.assertEqual(settings["HostAppVer"], "5.2.3 LTS")
        self.assertEqual(settings["TargetQuadCount"], "8000")
        self.assertEqual(settings["ExactQuadCount"], "1")
        self.assertEqual(settings["MaxThreadCount"], "6")

    def test_settings_reject_invalid_values(self):
        for kwargs in (
            {"target_quads": 0},
            {"target_quads": 100, "adaptive_size": 101},
            {"target_quads": 100, "symmetry": "w"},
            {"target_quads": 100, "max_threads": -1},
        ):
            with self.subTest(kwargs=kwargs), self.assertRaises(ValueError):
                args = dict(file_in="i", file_out="o", progress_file="p", target_quads=100)
                args.update(kwargs)
                qr.settings_text(**args)


class ProgressTests(unittest.TestCase):
    def test_non_finite_progress_is_partial_not_terminal(self):
        self.assertIsNone(qr.parse_progress("nan"))
        self.assertIsNone(qr.parse_progress("inf"))
        self.assertIsNone(qr.parse_progress("-inf"))


class ArtifactTests(unittest.TestCase):
    def test_artifact_directories_are_unique(self):
        with tempfile.TemporaryDirectory() as root:
            first = qr.make_artifact_dir(root)
            second = qr.make_artifact_dir(root)
            self.assertNotEqual(first, second)
            self.assertTrue(os.path.isdir(first))
            self.assertTrue(os.path.isdir(second))

    def test_bridge_files_restore_previous_job_and_keep_small_artifacts(self):
        with tempfile.TemporaryDirectory() as root:
            bridge = os.path.join(root, "bridge")
            artifacts = os.path.join(root, "artifacts")
            os.makedirs(bridge)
            for name in qr.BRIDGE_FILES:
                Path(bridge, name).write_text(f"old-{name}")
            with qr.preserve_bridge_files(artifacts, bridge) as active:
                self.assertEqual(active, bridge)
                for name in qr.BRIDGE_FILES:
                    self.assertFalse(os.path.exists(os.path.join(bridge, name)))
                    Path(bridge, name).write_text(f"new-{name}")
            for name in qr.BRIDGE_FILES:
                self.assertEqual(Path(bridge, name).read_text(), f"old-{name}")
            self.assertEqual(Path(artifacts, "retopo.fbx").read_text(), "new-retopo.fbx")
            self.assertEqual(Path(artifacts, "progress.txt").read_text(), "new-progress.txt")
            self.assertEqual(Path(artifacts, "RetopoSettings.txt").read_text(), "new-RetopoSettings.txt")
            self.assertFalse(os.path.exists(os.path.join(artifacts, "inputMesh.fbx")))

    def test_failed_job_keeps_input_for_reproduction_and_restores_previous_job(self):
        with tempfile.TemporaryDirectory() as root:
            bridge = os.path.join(root, "bridge")
            artifacts = os.path.join(root, "artifacts")
            os.makedirs(bridge)
            for name in qr.BRIDGE_FILES:
                Path(bridge, name).write_text(f"old-{name}")
            with self.assertRaisesRegex(RuntimeError, "failed"):
                with qr.preserve_bridge_files(artifacts, bridge):
                    for name in qr.BRIDGE_FILES:
                        Path(bridge, name).write_text(f"new-{name}")
                    raise RuntimeError("failed")
            for name in qr.BRIDGE_FILES:
                self.assertEqual(Path(bridge, name).read_text(), f"old-{name}")
                self.assertEqual(Path(artifacts, name).read_text(), f"new-{name}")

    def test_new_bridge_files_do_not_leak_into_the_official_job(self):
        with tempfile.TemporaryDirectory() as root:
            bridge = os.path.join(root, "bridge")
            artifacts = os.path.join(root, "artifacts")
            with qr.preserve_bridge_files(artifacts, bridge):
                os.makedirs(bridge, exist_ok=True)
                for name in qr.BRIDGE_FILES:
                    Path(bridge, name).write_text(name)
            for name in qr.BRIDGE_FILES:
                self.assertFalse(os.path.exists(os.path.join(bridge, name)))

    @unittest.skipUnless(os.name == "nt", "Windows byte-range lock semantics")
    def test_bridge_lock_excludes_another_handle(self):
        with tempfile.TemporaryDirectory() as root:
            path = os.path.join(root, "job.lock")
            with qr.bridge_lock(path, timeout_s=0.2):
                with self.assertRaises(qr.QuadRemeshError):
                    with qr.bridge_lock(path, timeout_s=0.01, poll_s=0.01):
                        pass


class ReapTests(unittest.TestCase):
    class Proc:
        def __init__(self, running=True):
            self.running = running
            self.terminated = False
            self.killed = False
            self.waits = []

        def poll(self):
            return None if self.running else 0

        def terminate(self):
            self.terminated = True

        def kill(self):
            self.killed = True
            self.running = False

        def wait(self, timeout=None):
            self.waits.append(timeout)
            if self.terminated and not self.killed:
                raise subprocess.TimeoutExpired("xremesh", timeout)
            self.running = False
            return 0

    def test_reap_waits_for_a_successful_engine(self):
        proc = self.Proc()
        qr.reap_process(proc)
        self.assertFalse(proc.terminated)
        self.assertEqual(proc.waits, [5])

    def test_reap_terminates_after_an_error_and_kills_if_needed(self):
        proc = self.Proc()
        qr.reap_process(proc, terminate=True)
        self.assertTrue(proc.terminated)
        self.assertTrue(proc.killed)
        self.assertEqual(proc.waits, [5, 5, 5])


if __name__ == "__main__":
    unittest.main()
