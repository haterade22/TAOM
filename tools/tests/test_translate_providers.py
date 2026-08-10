#!/usr/bin/env python3
"""Tests for the provider layer in tools/translate_with_claude.py.

Run:  python -m unittest discover -s tools/tests -p "test_*.py"

The pipeline was Anthropic-only, so a contributor without an Anthropic key could
not run the workflow TRANSLATOR_GUIDE.md asks them to run. `--provider` adds the
/chat/completions shape that OpenRouter and DeepSeek both serve.

Two properties matter more than the plumbing:

  * **Anthropic is untouched by default.** Every existing invocation must build
    the same request body it built before, or the two paths silently diverge for
    the provider everyone is already using.
  * **The same prompt reaches every provider.** SYSTEM_PROMPT carries the rules
    that keep {PLACEHOLDER} tokens intact; a shape that drops it would produce
    translations that break the game rather than merely read oddly.

No test here touches the network.
"""
import json
import os
import sys
import unittest
from types import SimpleNamespace

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import translate_with_claude as twc  # noqa: E402


def _entries(n=2):
    return [twc.Entry(file_path=None, string_id=f"id{i}",
                      english_text=f"text {i}", current_text=f"text {i}")
            for i in range(n)]


class AnthropicIsUnchanged(unittest.TestCase):

    def test_the_default_provider_is_anthropic(self):
        self.assertEqual("anthropic", twc.DEFAULT_PROVIDER)

    def test_the_body_is_the_messages_api_shape_it_always_was(self):
        body = twc.build_request("German", _entries())
        self.assertEqual(twc.MODEL, body["model"])
        self.assertIn("system", body)                      # top-level, not a message
        self.assertEqual(twc.THINKING, body["thinking"])
        self.assertEqual(twc.OUTPUT_CONFIG, body["output_config"])
        self.assertEqual(["user"], [m["role"] for m in body["messages"]])

    def test_omitting_the_provider_matches_asking_for_anthropic(self):
        self.assertEqual(twc.build_request("German", _entries()),
                         twc.build_request("German", _entries(),
                                           twc.resolve_provider("anthropic")))


class OpenAiCompatibleShape(unittest.TestCase):

    def test_the_body_is_chat_completions_and_carries_no_anthropic_fields(self):
        for name in ("deepseek", "openrouter"):
            with self.subTest(provider=name):
                body = twc.build_request("German", _entries(), twc.resolve_provider(name))
                self.assertEqual(["system", "user"], [m["role"] for m in body["messages"]])
                self.assertEqual({"type": "json_object"}, body["response_format"])
                for absent in ("system", "thinking", "output_config"):
                    self.assertNotIn(absent, body,
                                     f"{absent} is Anthropic-only and would be rejected")

    def test_the_system_prompt_reaches_the_model_on_every_provider(self):
        # Its rules are what keep {PLACEHOLDER} tokens intact. Losing it does not
        # produce a visible failure — it produces translations that break the game.
        marker = "VIOLATIONS BREAK THE GAME"
        anthropic_body = twc.build_request("German", _entries())
        self.assertIn(marker, anthropic_body["system"])
        for name in ("deepseek", "openrouter"):
            with self.subTest(provider=name):
                body = twc.build_request("German", _entries(), twc.resolve_provider(name))
                self.assertIn(marker, body["messages"][0]["content"])

    def test_every_entry_in_the_batch_reaches_the_request(self):
        for name in ("anthropic", "deepseek"):
            with self.subTest(provider=name):
                body = twc.build_request("German", _entries(3), twc.resolve_provider(name))
                user = body["messages"][-1]["content"]
                for i in range(3):
                    self.assertIn(f"id{i}", user)


class ProviderResolution(unittest.TestCase):

    def test_the_model_and_prices_can_be_overridden(self):
        cfg = twc.resolve_provider("deepseek", model="deepseek-v4-pro",
                                   price_in=0.435, price_out=0.87)
        self.assertEqual("deepseek-v4-pro", cfg["model"])
        self.assertEqual(0.435, cfg["price_in"])

    def test_resolving_does_not_mutate_the_table(self):
        before = dict(twc.PROVIDERS["deepseek"])
        twc.resolve_provider("deepseek", model="something-else", price_in=99.0)
        self.assertEqual(before, twc.PROVIDERS["deepseek"])

    def test_only_anthropic_advertises_the_batches_api(self):
        self.assertTrue(twc.PROVIDERS["anthropic"]["batch"])
        for name in ("deepseek", "openrouter"):
            self.assertFalse(twc.PROVIDERS[name]["batch"],
                             f"{name} has no Batches API; offering it would 404 mid-run")

    def test_the_estimate_follows_the_provider(self):
        cheap = twc.estimate_cost(100, twc.resolve_provider("deepseek"))
        dear = twc.estimate_cost(100, twc.resolve_provider("anthropic"))
        self.assertLess(cheap, dear)
        self.assertGreater(cheap, 0, "a cheap provider is not a free one")


class KeyHandling(unittest.TestCase):

    def setUp(self):
        self._saved = {v: os.environ.get(v)
                       for v in ("ANTHROPIC_API_KEY", "DEEPSEEK_API_KEY", "OPENROUTER_API_KEY")}

    def tearDown(self):
        for k, v in self._saved.items():
            if v is None:
                os.environ.pop(k, None)
            else:
                os.environ[k] = v

    def test_a_missing_key_exits_2_naming_the_variable(self):
        os.environ.pop("DEEPSEEK_API_KEY", None)
        from io import StringIO
        from contextlib import redirect_stderr
        err = StringIO()
        with redirect_stderr(err):
            with self.assertRaises(SystemExit) as ctx:
                twc.require_key(twc.resolve_provider("deepseek"))
        self.assertEqual(2, ctx.exception.code)
        self.assertIn("DEEPSEEK_API_KEY", err.getvalue())

    def test_a_blank_key_counts_as_missing(self):
        # An exported-but-empty variable would otherwise send "Bearer " and get a 401
        # that reads like a bad key rather than an unset one.
        os.environ["DEEPSEEK_API_KEY"] = "   "
        from io import StringIO
        from contextlib import redirect_stderr
        with redirect_stderr(StringIO()):
            with self.assertRaises(SystemExit):
                twc.require_key(twc.resolve_provider("deepseek"))


class ResponseParsing(unittest.TestCase):

    def test_the_openai_response_envelope_is_read_without_the_network(self):
        # The shape both OpenRouter and DeepSeek return, with the same tolerated
        # payload drift _parse_response_text already handles for Anthropic.
        captured = {}

        def fake_post(url, headers, payload, timeout=180):
            captured["url"] = url
            captured["auth"] = headers["Authorization"]
            return {
                "choices": [{"message": {"content": json.dumps(
                    {"translations": [{"id": "id0", "translated": "Text null"},
                                      {"id": "id1", "translated": "Text eins"}]})}}],
                "usage": {"prompt_tokens": 11, "completion_tokens": 7},
            }

        os.environ["DEEPSEEK_API_KEY"] = "test-key-not-real"
        original, twc._post_json = twc._post_json, fake_post
        try:
            out, usage = twc.call_openai_compatible(
                twc.resolve_provider("deepseek"), "German", _entries())
        finally:
            twc._post_json = original
            os.environ.pop("DEEPSEEK_API_KEY", None)

        self.assertEqual({"id0": "Text null", "id1": "Text eins"}, out)
        self.assertEqual((11, 7), usage)
        self.assertTrue(captured["url"].endswith("/chat/completions"))
        self.assertTrue(captured["auth"].startswith("Bearer "))


class TheModelOverrideReachesTheWire(unittest.TestCase):
    """--model must reach the request on every provider, the default one included.

    AnthropicIsUnchanged above asserts build_request in isolation, and that is exactly
    why it could not see this: both Anthropic call sites called build_request WITHOUT
    the provider, so `--model X` was accepted by argparse, printed in the run header and
    used to price the estimate, then dropped before the request was built. The run said
    one model and sent another.

    A test one level up -- on what the CALLER hands the SDK -- is the only level where
    that gap is visible, so these assert there.
    """

    def _recording_client(self):
        """A stand-in that records the kwargs call_claude hands messages.create."""
        class Messages:
            def __init__(self):
                self.sent = None

            def create(self, **kwargs):
                self.sent = kwargs
                return SimpleNamespace(
                    content=[SimpleNamespace(text='{"translations": []}')],
                    usage=SimpleNamespace(input_tokens=1, output_tokens=1))

        return SimpleNamespace(messages=Messages())

    def test_the_sequential_path_sends_the_model_the_cli_asked_for(self):
        client = self._recording_client()
        twc.call_claude(client, "German", _entries(1),
                        twc.resolve_provider("anthropic", model="claude-sonnet-5"))
        self.assertEqual("claude-sonnet-5", client.messages.sent["model"],
                         "--model was printed and priced but not sent")

    def test_the_batched_path_sends_the_model_the_cli_asked_for(self):
        class Batches:
            def __init__(self):
                self.created_requests = None

            def create(self, requests):
                self.created_requests = requests
                return SimpleNamespace(id="msgbatch_fake", processing_status="in_progress")

            def retrieve(self, batch_id):
                return SimpleNamespace(
                    id=batch_id, processing_status="ended",
                    request_counts=SimpleNamespace(processing=0, succeeded=0, errored=0))

            def results(self, batch_id):
                return iter(())

        client = SimpleNamespace(messages=SimpleNamespace(batches=Batches()))
        twc.call_claude_batched(
            client, "German", [_entries(1)], poll_seconds=0,
            provider=twc.resolve_provider("anthropic", model="claude-sonnet-5"))

        sent = client.messages.batches.created_requests
        self.assertEqual("claude-sonnet-5", sent[0]["params"]["model"],
                         "--batch ignored --model and sent the packaged default")

    def test_without_the_flag_both_paths_still_send_the_packaged_default(self):
        # The other half of the guard: threading the provider through must not change
        # what an ordinary run sends, which is every run anyone is making today.
        client = self._recording_client()
        twc.call_claude(client, "German", _entries(1), twc.resolve_provider("anthropic"))
        self.assertEqual(twc.MODEL, client.messages.sent["model"])

        bare = self._recording_client()
        twc.call_claude(bare, "German", _entries(1))
        self.assertEqual(twc.MODEL, bare.messages.sent["model"])

if __name__ == "__main__":
    unittest.main()
