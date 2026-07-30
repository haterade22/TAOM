#!/usr/bin/env python3
"""Unit tests for the Batches API path in tools/translate_with_claude.py.

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_translate_batching.py

No API calls: a fake client stands in for anthropic.Anthropic(). Each test maps to a
failure mode the batched path can hit that the sequential path cannot:

  - custom_id keying   -> Batches results arrive in ANY order; keying by position
                          silently assigns chunk B's translations to chunk A's entries
  - result.type        -> errored / expired / canceled results must not crash the run
  - shared builder     -> the batched and sequential paths must send the SAME request
                          (model, thinking, schema), or output drifts between them
  - shared validation  -> a batched translation must face the same placeholder gate,
                          or broken {VARIABLE} markup reaches the game via batching only
"""
import os
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import translate_with_claude as t  # noqa: E402


def entry(string_id: str, english: str) -> t.Entry:
    return t.Entry(file_path=Path("dummy.xml"), string_id=string_id,
                   english_text=english, current_text=english)


def message_result(custom_id: str, payload: str, in_tok: int = 10, out_tok: int = 5):
    """A succeeded batch result carrying `payload` as the model's text output."""
    return SimpleNamespace(
        custom_id=custom_id,
        result=SimpleNamespace(
            type="succeeded",
            message=SimpleNamespace(
                content=[SimpleNamespace(type="text", text=payload)],
                usage=SimpleNamespace(input_tokens=in_tok, output_tokens=out_tok),
            ),
        ),
    )


def failed_result(custom_id: str, kind: str, error_type: str = "invalid_request"):
    return SimpleNamespace(
        custom_id=custom_id,
        result=SimpleNamespace(type=kind, error=SimpleNamespace(type=error_type)),
    )


class FakeBatches:
    def __init__(self, results, statuses=None):
        self._results = results
        self._statuses = list(statuses or ["ended"])
        self.created_requests = None

    def create(self, requests):
        self.created_requests = requests
        return SimpleNamespace(id="msgbatch_fake", processing_status="in_progress")

    def retrieve(self, batch_id):
        status = self._statuses.pop(0) if len(self._statuses) > 1 else self._statuses[0]
        return SimpleNamespace(
            id=batch_id,
            processing_status=status,
            request_counts=SimpleNamespace(processing=0, succeeded=len(self._results), errored=0),
        )

    def results(self, batch_id):
        return iter(self._results)


class FakeClient:
    def __init__(self, results, statuses=None):
        self.messages = SimpleNamespace(batches=FakeBatches(results, statuses))


class BuildRequestTests(unittest.TestCase):
    def test_request_carries_model_thinking_and_schema(self):
        req = t.build_request("German", [entry("a", "Hello")])
        self.assertEqual(req["model"], t.MODEL)
        self.assertEqual(req["thinking"], {"type": "disabled"})
        self.assertEqual(req["output_config"]["format"]["schema"], t.TRANSLATION_SCHEMA)
        self.assertIn("German", req["system"])
        self.assertIn("Hello", req["messages"][0]["content"])

    def test_batched_path_sends_the_shared_builder_output(self):
        """Guards drift: the batch request params must be build_request's output verbatim."""
        batch = [entry("a", "Hello")]
        payload = '{"translations": [{"id": "a", "translated": "Hallo"}]}'
        client = FakeClient([message_result("chunk-0", payload)])

        t.call_claude_batched(client, "German", [batch], poll_seconds=0)

        sent = client.messages.batches.created_requests
        self.assertEqual(len(sent), 1)
        self.assertEqual(sent[0]["custom_id"], "chunk-0")
        self.assertEqual(sent[0]["params"], t.build_request("German", batch))


class BatchedResultTests(unittest.TestCase):
    def test_results_keyed_by_custom_id_not_arrival_order(self):
        chunk_a = [entry("a1", "One")]
        chunk_b = [entry("b1", "Two")]
        # Deliberately return chunk-1 BEFORE chunk-0.
        client = FakeClient([
            message_result("chunk-1", '{"translations": [{"id": "b1", "translated": "Zwei"}]}'),
            message_result("chunk-0", '{"translations": [{"id": "a1", "translated": "Eins"}]}'),
        ])

        per_chunk, usage = t.call_claude_batched(client, "German", [chunk_a, chunk_b], poll_seconds=0)

        self.assertEqual(per_chunk[0], {"a1": "Eins"})
        self.assertEqual(per_chunk[1], {"b1": "Zwei"})
        self.assertEqual(usage, (20, 10))

    def test_error_result_types_yield_no_translations(self):
        for kind in ("errored", "expired", "canceled"):
            with self.subTest(kind=kind):
                client = FakeClient([failed_result("chunk-0", kind)])
                per_chunk, usage = t.call_claude_batched(
                    client, "German", [[entry("a", "Hello")]], poll_seconds=0)
                self.assertEqual(per_chunk.get(0, {}), {})
                self.assertEqual(usage, (0, 0))

    def test_waits_until_processing_status_ended(self):
        payload = '{"translations": [{"id": "a", "translated": "Hallo"}]}'
        client = FakeClient([message_result("chunk-0", payload)],
                            statuses=["in_progress", "in_progress", "ended"])
        per_chunk, _ = t.call_claude_batched(client, "German", [[entry("a", "Hello")]],
                                            poll_seconds=0)
        self.assertEqual(per_chunk[0], {"a": "Hallo"})


class AbsorbTests(unittest.TestCase):
    """The validation gate both paths share."""

    def setUp(self):
        self.cache = {}
        self.queued = {}
        self.result = t.TranslationResult()

    def queue(self, e, translated):
        self.queued[e.string_id] = translated

    def test_good_translation_is_cached_and_queued(self):
        e = entry("greet", "Hello {RULER.NAME}")
        ok = t.absorb_translations([e], {"greet": "Hallo {RULER.NAME}"},
                                   self.result, self.cache, self.queue)
        self.assertEqual(ok, 1)
        self.assertEqual(self.queued, {"greet": "Hallo {RULER.NAME}"})
        self.assertEqual(self.cache, {"greet": "Hallo {RULER.NAME}"})
        self.assertEqual(self.result.from_llm, 1)
        self.assertEqual(self.result.failed, [])

    def test_dropped_placeholder_is_rejected_and_not_cached(self):
        e = entry("greet", "Hello {RULER.NAME}")
        ok = t.absorb_translations([e], {"greet": "Hallo"},
                                   self.result, self.cache, self.queue)
        self.assertEqual(ok, 0)
        self.assertEqual(self.queued, {})
        self.assertEqual(self.cache, {}, "a placeholder-broken translation must never be cached")
        self.assertEqual(self.result.failed, ["greet"])

    def test_missing_id_is_reported_as_failed(self):
        e = entry("greet", "Hello")
        ok = t.absorb_translations([e], {}, self.result, self.cache, self.queue)
        self.assertEqual(ok, 0)
        self.assertEqual(self.result.failed, ["greet"])


class WriteBackTests(unittest.TestCase):
    """write_back substitutes by id — an id absent from the target file used to be dropped
    silently, so a paid-for translation vanished with no warning (3 lost on the CNs run)."""

    HEADER = ('<?xml version="1.0" encoding="utf-8"?>\n<base type="string">\n'
              '  <tags>\n    <tag language="German" />\n  </tags>\n  <strings>\n')
    FOOTER = '  </strings>\n</base>\n'

    def _write(self, tmp: Path, ids, newline="\n"):
        body = "".join(f'    <string id="{i}" text="English" />\n' for i in ids)
        path = tmp / "std_taom_test_deu-DE.xml"
        path.write_text(self.HEADER + body + self.FOOTER, encoding="utf-8", newline=newline)
        return path

    def test_reports_ids_it_could_not_place(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            path = self._write(Path(d), ["present"])
            written, unplaced = t.write_back(
                path, {"present": "Vorhanden", "absent": "Fehlend"}, "German")
            self.assertEqual(written, 1)
            self.assertEqual(unplaced, ["absent"])

    def test_preserves_the_files_own_line_endings(self):
        import tempfile
        for newline in ("\n", "\r\n"):
            with self.subTest(newline=repr(newline)), tempfile.TemporaryDirectory() as d:
                path = self._write(Path(d), ["a"], newline=newline)
                before = path.read_bytes()
                t.write_back(path, {"a": "Eins"}, "German")
                after = path.read_bytes()
                self.assertEqual(before.count(b"\r\n"), after.count(b"\r\n"),
                                 "line-ending convention must survive a write")
                self.assertIn(b"Eins", after)


class SyncMissingIdsTests(unittest.TestCase):
    """A source key with no <string id=...> in the per-language file has nowhere to land, so its
    translation is paid for and discarded (400 ids across the 12 languages did exactly that).
    sync_missing_ids seeds them as English placeholders so the next run can fill them."""

    SRC = ('<?xml version="1.0" encoding="utf-8"?>\n<strings>\n'
           '\t<string id="a" text="{=key_a}Alpha" />\n'
           '\t<string id="b" text="{=key_b}Bravo &amp; Sons" />\n'
           '</strings>\n')
    TGT = ('<?xml version="1.0" encoding="utf-8"?>\n<base type="string">\n  <strings>\n'
           '    <string id="key_a" text="Alfa" />\n  </strings>\n</base>\n')

    def _pair(self, d: Path, newline="\n"):
        src, tgt = d / "src.xml", d / "tgt.xml"
        src.write_text(self.SRC, encoding="utf-8", newline=newline)
        tgt.write_text(self.TGT, encoding="utf-8", newline=newline)
        return src, tgt

    def test_appends_missing_id_with_escaped_english_text(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            src, tgt = self._pair(Path(d))
            added = t.sync_missing_ids(src, tgt)
            self.assertEqual(added, ["key_b"])
            body = tgt.read_text(encoding="utf-8")
            self.assertIn('<string id="key_b" text="Bravo &amp; Sons" />', body)
            self.assertIn('<string id="key_a" text="Alfa" />', body,
                          "existing translations must not be disturbed")

    def test_is_idempotent(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            src, tgt = self._pair(Path(d))
            t.sync_missing_ids(src, tgt)
            first = tgt.read_bytes()
            self.assertEqual(t.sync_missing_ids(src, tgt), [])
            self.assertEqual(tgt.read_bytes(), first)

    def test_preserves_line_endings(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            src, tgt = self._pair(Path(d), newline="\r\n")
            t.sync_missing_ids(src, tgt)
            raw = tgt.read_bytes()
            self.assertNotIn(b"\r\r\n", raw)
            self.assertEqual(raw.count(b"\n"), raw.count(b"\r\n"),
                             "every newline should stay CRLF")


if __name__ == "__main__":
    unittest.main(verbosity=2)
