#!/usr/bin/env python3
"""
TAOM AI Translation Tool — produces first-draft translations via Claude API.

Walks TAOM, TAOM_Map, and LOTRLOME_Armory language XML files; finds entries
that still contain English text; translates via Claude API with a 4-tier
fallback chain (override -> cache -> LLM -> keep English).

Usage:
    # Preview cost and counts (no API calls):
    python tools/translate_with_claude.py --lang RU --dry-run

    # Pilot a small batch (50 entries):
    python tools/translate_with_claude.py --lang RU --module TAOM --max-entries 50 --apply

    # Full run for a language (all 26 files):
    python tools/translate_with_claude.py --lang RU --apply

    # Override files: tools/translation_overrides/<lang>.json (hand-curated, git-tracked)
    # Cache files:    tools/translation_cache/<lang>.json     (auto-written, git-tracked)
"""

import argparse
import io
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET

# Force UTF-8 stdout/stderr so we can print translated Russian/Japanese/Chinese error messages
# without crashing on Windows cp1252 console encoding
if hasattr(sys.stdout, "buffer"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace", line_buffering=True)
if hasattr(sys.stderr, "buffer"):
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace", line_buffering=True)
from dataclasses import dataclass, field
from pathlib import Path

from _gamedir import ensure_exists, game_modules


# ── Configuration ──────────────────────────────────────────────────────────────

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GAME_ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

TAOM_LANG_DIR = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "Languages"


def game_modules_root():
    """The Modules folder, resolved per call rather than at import.

    Per call because this module replaces sys.stdout/sys.stderr with UTF-8
    wrappers at import time (below), so reloading it to pick up a changed
    variable closes the real streams — a module-level constant would be
    untestable. Everything else here follows the #416 helper.
    """
    return game_modules(DEFAULT_GAME_ROOT)


def taom_map_lang_dir():
    """TAOM_Map's per-language folder in the install."""
    return game_modules_root() / "TAOM_Map" / "ModuleData" / "Languages"


def armory_lang_dir():
    """LOTRLOME_Armory's per-language folder in the install."""
    return game_modules_root() / "LOTRLOME_Armory" / "ModuleData" / "Languages"


OVERRIDES_DIR = REPO_ROOT / "tools" / "translation_overrides"
CACHE_DIR = REPO_ROOT / "tools" / "translation_cache"

# Per-language: (locale_suffix, language_name)
LANGUAGES = {
    "BR":  ("por-BR", "Portuguese (Brazilian)"),
    "CNs": ("zho-CN", "Simplified Chinese"),
    "CNt": ("zho-HK", "Traditional Chinese"),
    "DE":  ("deu-DE", "German"),
    "FR":  ("fre-FR", "French"),
    "IT":  ("ita-IT", "Italian"),
    "JP":  ("jpn-JP", "Japanese"),
    "KO":  ("kor-KO", "Korean"),
    "PL":  ("pol-PL", "Polish"),
    "RU":  ("rus-RU", "Russian"),
    "SP":  ("spa-LA", "Spanish (LA)"),
    "TR":  ("tur-TR", "Turkish"),
}

MODEL = "claude-opus-5"
PRICE_INPUT_PER_MTOK = 5.0
PRICE_OUTPUT_PER_MTOK = 25.0

# Opus 5 runs adaptive thinking when the `thinking` field is omitted, and max_tokens caps
# thinking + response text together — a batch could burn its budget thinking and return
# truncated JSON, failing every entry in it. Translation is mechanical, so thinking is off
# (legal at effort "high" or below) and effort is low.
THINKING = {"type": "disabled"}

# Shape the response with a schema instead of hoping the model returns the prescribed JSON.
# The installed SDK (0.49.0) predates the typed `output_config` parameter, so both this and
# `effort` ride in via extra_body.
TRANSLATION_SCHEMA = {
    "type": "object",
    "properties": {
        "translations": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "id": {"type": "string"},
                    "translated": {"type": "string"},
                },
                "required": ["id", "translated"],
                "additionalProperties": False,
            },
        },
    },
    "required": ["translations"],
    "additionalProperties": False,
}

OUTPUT_CONFIG = {
    "effort": "low",
    "format": {"type": "json_schema", "schema": TRANSLATION_SCHEMA},
}

BATCH_SIZE = 40  # entries per API call — small enough to keep output reliable
MAX_RETRIES = 3


# Providers. Anthropic stays the default and its path is unchanged — this exists so a
# contributor without an Anthropic key can still run the pipeline, which is what the
# TRANSLATOR_GUIDE asks people to do.
#
# `api` selects the request/response shape, not the vendor: "anthropic" is the Messages API
# (system as a top-level field, content blocks back), "openai" is the /chat/completions shape
# that both OpenRouter and DeepSeek serve. The openai path speaks HTTP directly through
# urllib, so those two providers need no SDK installed at all.
#
# Prices are per million tokens, and they date. Anthropic's are the file's originals; DeepSeek's
# were published rates on 2026-07-25. OpenRouter charges per underlying model and adds a margin,
# so its entry is an estimate for its default model — pass --price-in/--price-out for anything
# else. Every one of these only feeds the printed estimate; none of them affects what is billed.
PROVIDERS = {
    "anthropic": {
        "api": "anthropic",
        "model": MODEL,
        "key_env": "ANTHROPIC_API_KEY",
        "base_url": None,               # SDK default
        "price_in": PRICE_INPUT_PER_MTOK,
        "price_out": PRICE_OUTPUT_PER_MTOK,
        "batch": True,                  # Batches API, 50% price
        "batch_size": BATCH_SIZE,       # unchanged for the default provider
    },
    "deepseek": {
        "api": "openai",
        "model": "deepseek-v4-flash",
        "key_env": "DEEPSEEK_API_KEY",
        "base_url": "https://api.deepseek.com/v1",
        "price_in": 0.14,
        "price_out": 0.28,
        "batch": False,
        "batch_size": 20,
    },
    "openrouter": {
        "api": "openai",
        "model": "deepseek/deepseek-v4-flash",
        "key_env": "OPENROUTER_API_KEY",
        "base_url": "https://openrouter.ai/api/v1",
        "price_in": 0.14,
        "price_out": 0.28,
        "batch": False,
        "batch_size": 20,
    },
}
DEFAULT_PROVIDER = "anthropic"


def resolve_provider(name, model=None, price_in=None, price_out=None):
    """The provider config, with the CLI overrides applied. Copied, not mutated in place."""
    cfg = dict(PROVIDERS[name])
    cfg["name"] = name
    if model:
        cfg["model"] = model
    if price_in is not None:
        cfg["price_in"] = price_in
    if price_out is not None:
        cfg["price_out"] = price_out
    return cfg



# ── Domain types ───────────────────────────────────────────────────────────────

@dataclass
class Entry:
    file_path: Path
    string_id: str
    english_text: str
    current_text: str  # what's currently in the target lang file (might equal english_text if untranslated)


@dataclass
class TranslationResult:
    total_entries: int = 0
    from_overrides: int = 0
    from_cache: int = 0
    from_llm: int = 0
    skipped_translated: int = 0  # already in target language (non-English)
    failed: list[str] = field(default_factory=list)
    api_input_tokens: int = 0
    api_output_tokens: int = 0


# ── Discovery ──────────────────────────────────────────────────────────────────

def english_source_files(module: str) -> list[tuple[Path, str]]:
    """Return (english_file, basename_of_target_file_template) per module.
    For TAOM, English source is the per-module XML at ModuleData root.
    For TAOM_Map and Armory, the English source is the inline {=KEY}default in the data XML;
    we read these from the target-language file (it was populated as an English template earlier).
    """
    pairs = []
    if module in ("TAOM", "all"):
        # TAOM source XMLs at ModuleData root
        for src_name, tgt_template in [
            ("taom_module_strings.xml",                       "std_taom_module_strings_{locale}.xml"),
            ("global_strings.xml",                            "std_taom_keybind_strings_{locale}.xml"),
            ("taom_wanderer_strings.xml",                     "std_taom_wanderer_strings_{locale}.xml"),
            ("named_companions/named_companion_strings.xml",  "std_taom_named_companion_strings_{locale}.xml"),
            ("taom_cc_strings.xml",                           "std_taom_cc_strings_{locale}.xml"),
            ("taom_career_strings.xml",                       "std_taom_career_strings_{locale}.xml"),
            ("taom_messenger_strings.xml",                    "std_taom_messenger_strings_{locale}.xml"),
            ("taom_xslt_strings.xml",                         "std_taom_xslt_strings_{locale}.xml"),
            ("taom_wotr_strings.xml",                         "std_taom_wotr_strings_{locale}.xml"),
            ("taom_lotr_issue_strings.xml",                   "std_taom_lotr_issue_strings_{locale}.xml"),
            ("taom_emissary_strings.xml",                     "std_taom_emissary_strings_{locale}.xml"),
            ("taom_enlistment_strings.xml",                   "std_taom_enlistment_strings_{locale}.xml"),
            ("taom_player_switcher_strings.xml",              "std_taom_player_switcher_strings_{locale}.xml"),
        ]:
            src = REPO_ROOT / "Main" / "_Module" / "ModuleData" / src_name
            if src.exists():
                pairs.append(("TAOM", src, tgt_template))
    return pairs


def discover_entries(lang: str, module_filter: str) -> list[Entry]:
    """Find translatable entries for a given language."""
    locale, _ = LANGUAGES[lang]
    entries: list[Entry] = []

    # TAOM module
    if module_filter in ("TAOM", "all"):
        taom_lang_dir = TAOM_LANG_DIR / lang
        for _, source_xml, tgt_template in english_source_files("TAOM"):
            target_file = taom_lang_dir / tgt_template.format(locale=locale)
            entries.extend(_diff_files(source_xml, target_file))

    # Only these two read the install. The TAOM module above is repo-only, so
    # asking for it alone must not require a game at all.
    if module_filter in ("TAOM_Map", "Armory", "all"):
        modules = ensure_exists(game_modules_root(), "the Bannerlord Modules folder")

    # TAOM_Map module — settlements.xml has inline {=KEY}default; we use the populated target as English source
    if module_filter in ("TAOM_Map", "all"):
        target_file = taom_map_lang_dir() / lang / "loc_settlements.xml"
        # English source: extract inline keys from the settlements.xml itself
        source_xml = modules / "TAOM_Map" / "ModuleData" / "settlements.xml"
        # A module present in the install but missing this file is a real state
        # (an install without TAOM_Map), so it is reported rather than fatal —
        # but never in silence, which is what produced "0 entries, $0.00, exit 0".
        if target_file.exists() and source_xml.exists():
            entries.extend(_diff_against_settlement_source(source_xml, target_file))
        else:
            missing = target_file if not target_file.exists() else source_xml
            print(f"  WARNING: TAOM_Map skipped — not found: {missing}", file=sys.stderr)

    # LOTRLOME_Armory module — root has English files; per-language dir mirrors them
    if module_filter in ("Armory", "all"):
        armory_root = armory_lang_dir()
        target_dir = armory_root / lang
        if target_dir.exists():
            for src_file in armory_root.glob("loc_*.xml"):
                target_file = target_dir / src_file.name
                if target_file.exists():
                    entries.extend(_diff_files(src_file, target_file))
        else:
            print(f"  WARNING: Armory skipped — not found: {target_dir}", file=sys.stderr)

    return entries


def _diff_files(source: Path, target: Path) -> list[Entry]:
    """Return entries where target text still matches source text (not translated)."""
    src_map = _parse_string_xml(source, strip_keys=True)  # for TAOM source XMLs with {=KEY}prefix
    tgt_map = _parse_string_xml(target, strip_keys=False) if target.exists() else {}

    entries = []
    for sid, eng_text in src_map.items():
        cur_text = tgt_map.get(sid, eng_text)
        if cur_text == eng_text:  # not yet translated
            entries.append(Entry(file_path=target, string_id=sid, english_text=eng_text, current_text=cur_text))
    return entries


def _diff_against_settlement_source(settlements_xml: Path, target: Path) -> list[Entry]:
    """Special handler for TAOM_Map settlements: source XML has inline {=KEY}default."""
    with open(settlements_xml, encoding="utf-8") as f:
        content = f.read()
    pattern = re.compile(r'\w+="\{=([^}]+)\}([^"]*)"')
    src_map = {}
    for m in pattern.finditer(content):
        key, default = m.groups()
        if key not in src_map:
            src_map[key] = default

    tgt_map = _parse_string_xml(target, strip_keys=False)
    entries = []
    for sid, eng_text in src_map.items():
        cur_text = tgt_map.get(sid, eng_text)
        if cur_text == eng_text:
            entries.append(Entry(file_path=target, string_id=sid, english_text=eng_text, current_text=cur_text))
    return entries


def _parse_string_xml(path: Path, strip_keys: bool) -> dict[str, str]:
    """Parse a <strings> XML file. If strip_keys, treat text="{=KEY}value" -> id=KEY, value=value."""
    if not path.exists():
        return {}
    try:
        tree = ET.parse(path)
    except ET.ParseError:
        return {}
    result = {}
    for el in tree.iter("string"):
        sid = el.get("id", "")
        text = el.get("text", "")
        if strip_keys:
            m = re.match(r"^\{=([^}]+)\}(.*)$", text, re.DOTALL)
            if m:
                sid, text = m.group(1), m.group(2)
        if sid:
            result[sid] = text
    return result


# ── Overrides + Cache ──────────────────────────────────────────────────────────

def load_overrides(lang: str) -> dict[str, str]:
    f = OVERRIDES_DIR / f"{lang.lower()}.json"
    if f.exists():
        with open(f, encoding="utf-8") as fh:
            return json.load(fh)
    return {}


def load_cache(lang: str) -> dict[str, str]:
    f = CACHE_DIR / f"{lang.lower()}.json"
    if f.exists():
        with open(f, encoding="utf-8") as fh:
            return json.load(fh)
    return {}


def save_cache(lang: str, cache: dict[str, str]) -> None:
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    f = CACHE_DIR / f"{lang.lower()}.json"
    with open(f, "w", encoding="utf-8") as fh:
        json.dump(cache, fh, ensure_ascii=False, indent=2, sort_keys=True)


# ── Placeholder validation ─────────────────────────────────────────────────────

# Variable placeholders: {ANYTHING_UPPER_DOTS}
VAR_PATTERN = re.compile(r"\{[A-Z_][A-Z0-9_.]*\}")
# Conditional structures: {?VAR}...{?}...{\?} OR {?VAR}...{\?}
CONDITIONAL_OPEN = re.compile(r"\{\?[A-Z_][A-Z0-9_.]*\}")
CONDITIONAL_MID = re.compile(r"\{\?\}")
CONDITIONAL_CLOSE = re.compile(r"\{\\\?\}")


def extract_placeholders(text: str) -> dict[str, int]:
    """Return counts of each placeholder/structural token."""
    counts = {}
    for m in VAR_PATTERN.findall(text):
        counts[m] = counts.get(m, 0) + 1
    counts["__cond_open__"] = len(CONDITIONAL_OPEN.findall(text))
    counts["__cond_mid__"] = len(CONDITIONAL_MID.findall(text))
    counts["__cond_close__"] = len(CONDITIONAL_CLOSE.findall(text))
    return counts


def placeholders_match(english: str, translated: str) -> tuple[bool, str]:
    """Return (ok, reason) — checks variable + conditional preservation."""
    e = extract_placeholders(english)
    t = extract_placeholders(translated)
    if e != t:
        diffs = []
        all_keys = set(e) | set(t)
        for k in sorted(all_keys):
            ec, tc = e.get(k, 0), t.get(k, 0)
            if ec != tc:
                diffs.append(f"{k}: en={ec} tr={tc}")
        return False, "; ".join(diffs)
    return True, ""


# ── LLM batch translation ──────────────────────────────────────────────────────

SYSTEM_PROMPT = """You are an expert translator for the Lord of the Rings video game mod "TAOM" (Tales From the Age of Men), a Bannerlord total conversion. Translate English game text into {target_language} for in-game display.

CRITICAL RULES — VIOLATIONS BREAK THE GAME:

1. Variable placeholders like {{RULER.NAME}}, {{COUNT}}, {{FACTION_NAME}}, {{TOWN_NAME}}, {{HERO.NAME}} MUST appear in your translation EXACTLY as in the input — same name, same braces, same count. Never invent new ones, never rename.

2. Gender/conditional structures like {{?RULER.GENDER}}Lady{{?}}Lord{{\\?}} MUST be preserved structurally. Translate only the inner words ("Lady"/"Lord"), keeping the {{?VAR}}, {{?}}, and {{\\?}} tokens EXACTLY as in the input.

3. Bracket prefixes like [Gondor], [Mordor] in equipment names are culture tags — keep the brackets and either keep the inner word or transliterate it appropriately for your language's conventions.

4. Tolkien proper nouns (place names, character names, faction names) should follow established translations in your language's published Tolkien editions. If unsure, transliterate phonetically rather than invent.

5. Maintain the original tone — most entries are narrative game flavor text. Match register (formal/casual) to the English.

6. Never add commentary, never translate to a different language, never refuse — if a string can't be translated cleanly, transliterate or copy English as a last resort.

7. Do not include internal or system XML tags in your response.

OUTPUT FORMAT:
You MUST respond with ONLY a JSON object, no other text. Schema:
{{"translations": [{{"id": "the_id_from_input", "translated": "your translation"}}, ...]}}

Every input entry MUST have an output entry with the SAME id. Do not skip entries."""


def _extract_translations(data) -> dict[str, str]:
    """Map a parsed API response to {id: translated}, tolerating the shapes the model
    actually emits. Prescribed form is [{"id":.., "translated":..}]; it sometimes returns
    an alternate value key, a single-key wrapper object, or the bare {id: translated}
    mapping — accepting all of them keeps a shape drift from wiping a 40-entry batch to 0."""
    value_keys = ("translated", "translation", "text", "value", "output")
    # Unwrap a single-key wrapper like {"translations": [...]}.
    if isinstance(data, dict) and len(data) == 1:
        only = next(iter(data.values()))
        if isinstance(only, list):
            data = only
    result: dict[str, str] = {}
    if isinstance(data, list):
        for item in data:
            if not isinstance(item, dict) or "id" not in item:
                continue
            for vk in value_keys:
                val = item.get(vk)
                if isinstance(val, str):
                    result[str(item["id"])] = val
                    break
    elif isinstance(data, dict):
        if "id" in data and any(vk in data for vk in value_keys):
            for vk in value_keys:
                val = data.get(vk)
                if isinstance(val, str):
                    result[str(data["id"])] = val
                    break
        else:
            for sid, val in data.items():
                if isinstance(val, str):
                    result[str(sid)] = val
    return result


def build_request(target_language: str, batch: list[Entry], provider: dict = None) -> dict:
    """The request body for one batch. Sequential and batched paths BOTH build from here —
    if they diverge, the two paths silently produce different translations.

    Same prompt and same entries for every provider; only the envelope differs. The system
    prompt carries the rules that keep placeholders intact, so it must reach the model on
    both shapes — as the top-level `system` field on Anthropic, as the first message on the
    /chat/completions shape.
    """
    provider = provider or resolve_provider(DEFAULT_PROVIDER)
    user_payload = [{"id": e.string_id, "text": e.english_text} for e in batch]
    user_msg = (
        f"Translate these {len(batch)} entries to {target_language}. "
        f"Respond with the JSON object only.\n\n"
        f"INPUT:\n{json.dumps(user_payload, ensure_ascii=False, indent=2)}"
    )
    system = SYSTEM_PROMPT.format(target_language=target_language)

    if provider["api"] == "anthropic":
        return {
            "model": provider["model"],
            "max_tokens": 8192,
            "thinking": THINKING,
            "output_config": OUTPUT_CONFIG,
            "system": system,
            "messages": [{"role": "user", "content": user_msg}],
        }

    # /chat/completions. `json_object` rather than a json_schema: OpenRouter routes to many
    # models and schema support is uneven, while the object mode is universal and the prompt
    # already specifies the schema. _parse_response_text tolerates the shape drift either way.
    return {
        "model": provider["model"],
        "max_tokens": 8192,
        "temperature": 0,
        "response_format": {"type": "json_object"},
        "messages": [
            {"role": "system", "content": system},
            {"role": "user", "content": user_msg},
        ],
    }


def _parse_response_text(text: str) -> dict[str, str]:
    """Strip code fences, parse, and map to {id: translated}. Raises json.JSONDecodeError."""
    text = text.strip()
    if text.startswith("```"):
        text = re.sub(r"^```(?:json)?\n?", "", text)
        text = re.sub(r"\n?```$", "", text)
    # Tolerate the response shapes the model actually emits (alternate value key,
    # single-key wrapper, or the bare {id: text} object) — a shape drift used to
    # wipe an entire 40-entry batch to 0/40 even though the JSON parsed fine.
    return _extract_translations(json.loads(text))


def call_claude(client, target_language: str, batch: list[Entry],
                provider: dict = None) -> dict[str, str]:
    """Translate a batch via the Claude API. Returns {id: translated_text}.

    `provider` is threaded through so --model reaches the request. Omitting it here made
    build_request fall back to the packaged default, so `--model X` printed X in the run
    header, priced the estimate as X, and sent the hardcoded MODEL.
    """
    req = build_request(target_language, batch, provider)
    # The installed SDK predates the typed output_config parameter — send it via extra_body.
    output_config = req.pop("output_config")

    for attempt in range(MAX_RETRIES):
        try:
            response = client.messages.create(
                **req, extra_body={"output_config": output_config})
            usage = (response.usage.input_tokens, response.usage.output_tokens)
            text = response.content[0].text.strip()
            return _parse_response_text(text), usage
        except json.JSONDecodeError as e:
            if attempt == MAX_RETRIES - 1:
                # Truncate raw response and stay safe — don't crash the entire run on a malformed batch
                safe_raw = (text[:300] if text else "(empty)").replace("\n", " ")
                print(f"    [batch_json_fail] {e}; raw: {safe_raw}...", flush=True)
                # Return empty result — caller will mark all entries in this batch as failed
                return {}, (0, 0)
            time.sleep(2 ** attempt)
        except Exception as e:
            if "rate_limit" in str(e).lower() or "overloaded" in str(e).lower():
                wait = 5 * (2 ** attempt)
                print(f"  Rate-limited / overloaded — sleeping {wait}s...")
                time.sleep(wait)
            else:
                raise


def require_key(provider: dict) -> str:
    """The provider's API key, or exit 2 naming the variable that is missing."""
    key = os.environ.get(provider["key_env"])
    if not key or not key.strip():
        print(f"ERROR: ${provider['key_env']} is not set — required for --provider "
              f"{provider['name']}.", file=sys.stderr)
        raise SystemExit(2)
    return key


def _post_json(url: str, headers: dict, payload: dict, timeout: int = 180) -> dict:
    """POST JSON, return the parsed body. Separated so a test can stand in for the network."""
    data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    with urllib.request.urlopen(req, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def call_openai_compatible(provider: dict, target_language: str,
                           batch: list[Entry]) -> tuple[dict[str, str], tuple[int, int]]:
    """Translate a batch through a /chat/completions endpoint. Returns ({id: text}, usage).

    Mirrors call_claude's failure handling deliberately: a batch whose JSON will not parse
    after MAX_RETRIES comes back empty so the caller marks its entries failed, rather than
    taking the whole run down over one bad response.
    """
    req = build_request(target_language, batch, provider)
    url = provider["base_url"].rstrip("/") + "/chat/completions"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {require_key(provider)}",
    }
    text = ""
    for attempt in range(MAX_RETRIES):
        try:
            body = _post_json(url, headers, req)
            usage = body.get("usage") or {}
            tokens = (usage.get("prompt_tokens", 0), usage.get("completion_tokens", 0))
            choice = body["choices"][0]
            text = (choice["message"]["content"] or "").strip()
            # A reply that ran out of output budget arrives as truncated JSON, which then fails
            # to parse — and "could not read the JSON" sends the reader looking at the wrong
            # thing entirely. Name the real cause and what to do about it.
            if choice.get("finish_reason") == "length":
                print(f"    Batch of {len(batch)} hit the model's output limit "
                      f"({tokens[1]} tokens) and was truncated — retry with a smaller "
                      f"batch_size for {provider['name']}.", file=sys.stderr)
                return {}, tokens
            return _parse_response_text(text), tokens
        except json.JSONDecodeError as e:
            if attempt == MAX_RETRIES - 1:
                safe_raw = (text[:300] if text else "(empty)").replace("\n", " ")
                print(f"    [batch_json_fail] {e}; raw: {safe_raw}...", flush=True)
                return {}, (0, 0)
            time.sleep(2 ** attempt)
        except urllib.error.HTTPError as e:
            # 429 and 5xx are worth waiting out; a 400 or 401 is not going to fix itself.
            if e.code == 429 or e.code >= 500:
                wait = 5 * (2 ** attempt)
                print(f"  HTTP {e.code} from {provider['name']} — sleeping {wait}s...")
                time.sleep(wait)
            else:
                detail = e.read().decode("utf-8", errors="replace")[:300]
                print(f"    HTTP {e.code} from {provider['name']}: {detail}", file=sys.stderr)
                raise
    return {}, (0, 0)


def call_model(provider: dict, client, target_language: str,
               batch: list[Entry]) -> tuple[dict[str, str], tuple[int, int]]:
    """One batch through whichever provider is selected."""
    if provider["api"] == "anthropic":
        return call_claude(client, target_language, batch, provider)
    return call_openai_compatible(provider, target_language, batch)


def call_claude_batched(
        client, target_language: str, chunks: list[list[Entry]], poll_seconds: int = 30,
        provider: dict = None) -> tuple[dict[int, dict[str, str]], tuple[int, int]]:
    """Translate many chunks through the Batches API (50% of standard price).

    Returns ({chunk_index: {id: translated}}, (input_tokens, output_tokens)). A chunk that
    errored, expired, or was canceled comes back absent — the caller marks its entries failed.
    """
    requests = [{"custom_id": f"chunk-{i}",
                 "params": build_request(target_language, chunk, provider)}
                for i, chunk in enumerate(chunks)]
    batch = client.messages.batches.create(requests=requests)
    print(f"    Batch submitted: {batch.id} ({len(requests)} requests)", flush=True)

    while True:
        status = client.messages.batches.retrieve(batch.id)
        if status.processing_status == "ended":
            break
        counts = getattr(status, "request_counts", None)
        pending = getattr(counts, "processing", "?") if counts else "?"
        print(f"    {status.processing_status} — {pending} processing...", flush=True)
        if poll_seconds:
            time.sleep(poll_seconds)

    per_chunk: dict[int, dict[str, str]] = {}
    in_tok = out_tok = 0
    for res in client.messages.batches.results(batch.id):
        # Results arrive in ANY order — key by custom_id, never by position.
        idx = int(str(res.custom_id).rsplit("-", 1)[-1])
        kind = res.result.type
        if kind != "succeeded":
            err = getattr(getattr(res.result, "error", None), "type", kind)
            print(f"    [chunk {idx}] {kind}: {err}", flush=True)
            continue
        msg = res.result.message
        in_tok += msg.usage.input_tokens
        out_tok += msg.usage.output_tokens
        try:
            per_chunk[idx] = _parse_response_text(msg.content[0].text)
        except json.JSONDecodeError as e:
            print(f"    [chunk {idx}] json_fail: {e}", flush=True)
    return per_chunk, (in_tok, out_tok)


def absorb_translations(batch: list[Entry], translated_map: dict[str, str],
                        result: "TranslationResult", cache: dict, queue) -> int:
    """Validate one batch's translations, cache and queue the good ones, return the OK count.

    Both the sequential and batched paths call this, so a batched translation faces the same
    placeholder gate — otherwise broken {VARIABLE} markup could reach the game via batching only.
    """
    ok_count = 0
    for e in batch:
        tr = translated_map.get(e.string_id)
        if tr is None:
            result.failed.append(e.string_id)
            continue
        ok, why = placeholders_match(e.english_text, tr)
        if not ok:
            print(f"    [skip] {e.string_id}: placeholder mismatch ({why})", flush=True)
            result.failed.append(e.string_id)
            continue
        cache[e.string_id] = tr
        queue(e, tr)
        result.from_llm += 1
        ok_count += 1
    return ok_count


# ── Id sync ────────────────────────────────────────────────────────────────────

def sync_missing_ids(source_path: Path, target_path: Path) -> list[str]:
    """Seed the per-language file with any {=KEY} the English source declares but it lacks.

    write_back substitutes by id, so a key with no <string id="KEY"> element has nowhere to
    land and its translation is silently discarded. Appending the key with its English text
    gives the next run somewhere to write. Returns the ids added; idempotent.
    """
    if not target_path.exists():
        return []
    src_map = _parse_string_xml(source_path, strip_keys=True)
    tgt_map = _parse_string_xml(target_path, strip_keys=False)
    missing = [sid for sid in src_map if sid not in tgt_map]
    if not missing:
        return []

    raw = target_path.read_text(encoding="utf-8", newline="")
    nl = "\r\n" if "\r\n" in raw else "\n"
    lines = raw.split(nl)

    string_lines = [i for i, l in enumerate(lines) if l.lstrip().startswith("<string ")]
    close = next((i for i, l in enumerate(lines) if "</strings>" in l), None)
    if close is None:
        return []
    if string_lines:
        anchor = string_lines[-1]
        indent = re.match(r"\s*", lines[anchor]).group(0)
        blank_separated = anchor + 1 < len(lines) and lines[anchor + 1].strip() == ""
        insert_at = anchor + (2 if blank_separated else 1)
    else:  # empty stub — indent one level past </strings>
        indent = re.match(r"\s*", lines[close]).group(0) + "  "
        blank_separated = False
        insert_at = close

    block = []
    for sid in missing:
        text = (src_map[sid].replace("&", "&amp;").replace('"', "&quot;")
                            .replace("<", "&lt;").replace(">", "&gt;"))
        block.append(f'{indent}<string id="{sid}" text="{text}" />')
        if blank_separated:
            block.append("")

    lines[insert_at:insert_at] = block
    target_path.write_text(nl.join(lines), encoding="utf-8", newline="")
    return missing


# ── Write back ─────────────────────────────────────────────────────────────────

def write_back(file_path: Path, translations: dict[str, str],
               language_tag: str) -> tuple[int, list[str]]:
    """Update the <string id=X text="..."/> entries in file_path with new translations.

    Returns (entries_written, ids_not_present_in_the_file). Substitution is by id, so an id
    the file doesn't declare has nowhere to land — those used to vanish silently, discarding a
    translation we paid for. The caller reports them.
    """
    if not file_path.exists():
        return 0, sorted(translations)
    # newline="" on BOTH handles: without it, universal-newline translation rewrites an
    # LF-stored file as CRLF on Windows and every line shows as changed — a 6000-line diff
    # for one edited string.
    with open(file_path, encoding="utf-8", newline="") as f:
        content = f.read()
    # Update <tag language="..."/> if present
    content = re.sub(
        r'<tag\s+language="[^"]*"\s*/>',
        f'<tag language="{language_tag}" />',
        content,
    )
    if not translations:
        with open(file_path, "w", encoding="utf-8", newline="") as f:
            f.write(content)
        return 0, []
    # Single-pass replacement: compile ONE regex matching any of the ids we're updating,
    # then resolve each match via dictionary lookup. Was: N-regex compile + N subn calls
    # which scaled badly on large files (1431-entry XSLT file × 12 langs ≈ 5min wasted).
    escaped_translations = {
        sid: (new_text.replace("&", "&amp;").replace('"', "&quot;")
                       .replace("<", "&lt;").replace(">", "&gt;"))
        for sid, new_text in translations.items()
    }
    id_alternation = "|".join(re.escape(sid) for sid in escaped_translations.keys())
    pattern = re.compile(
        r'(<string\s+id=")(' + id_alternation + r')(")(\s+text=")[^"]*(")'
    )
    written = 0
    placed: set[str] = set()
    def _replace(m):
        nonlocal written
        sid = m.group(2)
        written += 1
        placed.add(sid)
        return m.group(1) + sid + m.group(3) + m.group(4) + escaped_translations[sid] + m.group(5)
    content = pattern.sub(_replace, content)
    with open(file_path, "w", encoding="utf-8", newline="") as f:
        f.write(content)
    return written, sorted(set(escaped_translations) - placed)


# ── Main orchestration ────────────────────────────────────────────────────────

def estimate_cost(entry_count: int, provider: dict = None) -> float:
    """Rough cost estimate. ~50 tokens input + ~50 tokens output per entry on average."""
    provider = provider or resolve_provider(DEFAULT_PROVIDER)
    in_tokens = entry_count * 80   # includes prompt overhead
    out_tokens = entry_count * 60
    return (in_tokens / 1_000_000 * provider["price_in"]
            + out_tokens / 1_000_000 * provider["price_out"])


def main():
    p = argparse.ArgumentParser(description="TAOM AI Translation Tool")
    p.add_argument("--lang", required=True, choices=list(LANGUAGES.keys()),
                   help="Target language code")
    p.add_argument("--module", default="all", choices=["TAOM", "TAOM_Map", "Armory", "all"])
    p.add_argument("--dry-run", action="store_true", help="Preview without writing")
    p.add_argument("--apply", action="store_true", help="Write translations and call API")
    p.add_argument("--max-entries", type=int, default=None, help="Cap API entries for testing")
    p.add_argument("--sync-ids", action="store_true",
                   help="Seed per-language TAOM files with any {=KEY} the English source declares "
                        "but they lack, so translations for those keys have somewhere to land")
    p.add_argument("--batch", action="store_true",
                   help="Submit via the Batches API at 50%% price (async, up to 24h; "
                        "worth it for bulk runs, not for a handful of entries)")
    p.add_argument("--provider", default=DEFAULT_PROVIDER, choices=sorted(PROVIDERS),
                   help="Which API to translate through (default: %(default)s). "
                        "openrouter and deepseek need no SDK installed.")
    p.add_argument("--model", default=None,
                   help="Override the provider's default model id")
    p.add_argument("--price-in", type=float, default=None,
                   help="Override input price per Mtok, for the printed estimate only")
    p.add_argument("--price-out", type=float, default=None,
                   help="Override output price per Mtok, for the printed estimate only")
    args = p.parse_args()

    if not args.dry_run and not args.apply:
        p.error("Specify either --dry-run or --apply")

    provider = resolve_provider(args.provider, args.model, args.price_in, args.price_out)
    if args.batch and not provider["batch"]:
        p.error(f"--batch is the Anthropic Batches API; {provider['name']} has no equivalent. "
                f"Drop --batch to run sequentially.")

    lang = args.lang
    locale, lang_name = LANGUAGES[lang]
    overrides = load_overrides(lang)
    cache = load_cache(lang)

    print(f"\n[TAOM Translator] Target: {lang_name} ({lang} / {locale})")
    print(f"  Overrides loaded: {len(overrides)}")
    print(f"  Cache loaded: {len(cache)}")
    print(f"  Module filter: {args.module}")
    print(f"  Mode: {'DRY RUN' if args.dry_run else 'APPLY'}")

    if args.sync_ids:
        seeded = 0
        for _, src, tpl in english_source_files("TAOM"):
            target = TAOM_LANG_DIR / lang / tpl.format(locale=locale)
            added = sync_missing_ids(src, target)
            if added:
                print(f"  +{len(added):>4} ids seeded into {target.name}")
                seeded += len(added)
        print(f"  Ids seeded: {seeded}")

    entries = discover_entries(lang, args.module)
    print(f"\n  Untranslated entries discovered: {len(entries)}")

    # Categorize
    via_override, via_cache, need_llm = [], [], []
    for e in entries:
        if e.string_id in overrides:
            via_override.append(e)
        elif e.string_id in cache:
            via_cache.append(e)
        else:
            need_llm.append(e)

    print(f"    from overrides: {len(via_override)}")
    print(f"    from cache:     {len(via_cache)}")
    print(f"    needs LLM:      {len(need_llm)}")

    if args.max_entries and len(need_llm) > args.max_entries:
        print(f"  --max-entries cap applied: {args.max_entries}/{len(need_llm)} LLM entries")
        need_llm = need_llm[:args.max_entries]

    api_cost = estimate_cost(len(need_llm), provider)
    print(f"  Provider: {provider['name']} ({provider['model']})")
    # Enough decimals that a cheap provider does not round to "$0.00", which reads as free.
    print(f"  Estimated API cost: ~${api_cost:.2f}" if api_cost >= 0.01
          else f"  Estimated API cost: ~${api_cost:.4f}")

    if args.dry_run or not need_llm and not via_override and not via_cache:
        print("\n  (dry run — no files written)" if args.dry_run else "  Nothing to do.")
        return 0

    # Translate
    result = TranslationResult(total_entries=len(entries))
    by_file: dict[Path, dict[str, str]] = {}

    def queue(e: Entry, translated: str):
        by_file.setdefault(e.file_path, {})[e.string_id] = translated

    for e in via_override:
        queue(e, overrides[e.string_id])
        result.from_overrides += 1
    for e in via_cache:
        queue(e, cache[e.string_id])
        result.from_cache += 1

    if need_llm:
        client = None
        if provider["api"] == "anthropic":
            # Import here so --dry-run doesn't need the anthropic SDK — and so the
            # openai-compatible providers never need it at all.
            import anthropic
            client = anthropic.Anthropic(api_key=require_key(provider))
        else:
            require_key(provider)   # fail before the first request, not after

        # Per provider: 40 entries fit Claude's reply, but measured against deepseek-v4-flash a
        # 40-entry Polish batch stops at finish_reason=length having spent all 8192 output
        # tokens, so the JSON arrives truncated and the whole batch is lost. 30 fit, 40 did not.
        size = provider.get("batch_size", BATCH_SIZE)
        chunks = [need_llm[i:i + size] for i in range(0, len(need_llm), size)]

        if args.batch:
            print(f"\n  Calling Claude Batches API ({provider['model']}) — "
                  f"{len(chunks)} requests of up to {size} entries (50% price)...")
            per_chunk, (in_tok, out_tok) = call_claude_batched(
                client, lang_name, chunks, provider=provider)
            result.api_input_tokens += in_tok
            result.api_output_tokens += out_tok
            for idx, chunk in enumerate(chunks):
                chunk_ok = absorb_translations(chunk, per_chunk.get(idx, {}), result, cache, queue)
                print(f"    Chunk {idx + 1}/{len(chunks)}: {chunk_ok}/{len(chunk)} ok", flush=True)
            save_cache(lang, cache)
        else:
            print(f"\n  Calling {provider['name']} ({provider['model']}) "
                  f"in batches of {size}...")
            for idx, batch in enumerate(chunks):
                try:
                    translated_map, (in_tok, out_tok) = call_model(
                        provider, client, lang_name, batch)
                except Exception as exc:
                    print(f"    Batch {idx + 1} FAILED: {exc}")
                    for e in batch:
                        result.failed.append(e.string_id)
                    continue

                result.api_input_tokens += in_tok
                result.api_output_tokens += out_tok
                batch_ok = absorb_translations(batch, translated_map, result, cache, queue)
                print(f"    Batch {idx + 1}/{len(chunks)}: {batch_ok}/{len(batch)} ok  "
                      f"(in={in_tok} out={out_tok})", flush=True)
                # Save cache after every batch — resumable on interruption
                save_cache(lang, cache)

        rate = 0.5 if args.batch else 1.0  # Batches API bills at 50%
        actual_cost = rate * (result.api_input_tokens / 1_000_000 * provider["price_in"]
                              + result.api_output_tokens / 1_000_000 * provider["price_out"])
        print(f"\n  Actual cost: ${actual_cost:.3f} "
              f"(in={result.api_input_tokens} out={result.api_output_tokens}"
              f"{', batched 50%' if args.batch else ''})")

    # Write back
    print(f"\n  Writing {sum(len(d) for d in by_file.values())} translations to {len(by_file)} files...")
    total_written = 0
    all_unplaced: list[str] = []
    for file_path, translations in by_file.items():
        n, unplaced = write_back(file_path, translations, lang_name)
        total_written += n
        all_unplaced.extend(unplaced)
        rel = file_path.name
        suffix = f"  ({len(unplaced)} ids not in file)" if unplaced else ""
        print(f"    {rel}: {n} entries{suffix}")
    if all_unplaced:
        print(f"  WARNING: {len(all_unplaced)} translation(s) had no matching id in the target "
              f"file and were discarded — add the id to the per-language file first: "
              f"{all_unplaced[:10]}")

    print(f"\n  Summary:")
    print(f"    Total entries:    {result.total_entries}")
    print(f"    Written to files: {total_written}")
    print(f"    From overrides:   {result.from_overrides}")
    print(f"    From cache:       {result.from_cache}")
    print(f"    From LLM:         {result.from_llm}")
    print(f"    Failed:           {len(result.failed)}")
    if result.failed:
        print(f"    Failed IDs (first 10): {result.failed[:10]}")
    return 0 if not result.failed else 1


if __name__ == "__main__":
    sys.exit(main())
