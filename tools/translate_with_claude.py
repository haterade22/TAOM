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
import xml.etree.ElementTree as ET

# Force UTF-8 stdout/stderr so we can print translated Russian/Japanese/Chinese error messages
# without crashing on Windows cp1252 console encoding
if hasattr(sys.stdout, "buffer"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace", line_buffering=True)
if hasattr(sys.stderr, "buffer"):
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace", line_buffering=True)
from dataclasses import dataclass, field
from pathlib import Path


# ── Configuration ──────────────────────────────────────────────────────────────

REPO_ROOT = Path(__file__).resolve().parent.parent
GAME_ROOT = Path(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules")

TAOM_LANG_DIR = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "Languages"
TAOM_MAP_LANG_DIR = GAME_ROOT / "TAOM_Map" / "ModuleData" / "Languages"
ARMORY_LANG_DIR = GAME_ROOT / "LOTRLOME_Armory" / "ModuleData" / "Languages"

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
    "SP":  ("spa-LA", "Latin American Spanish"),
    "TR":  ("tur-TR", "Turkish"),
}

MODEL = "claude-sonnet-4-5"  # claude-sonnet-4-5 (decisions from plan)
PRICE_INPUT_PER_MTOK = 3.0
PRICE_OUTPUT_PER_MTOK = 15.0

BATCH_SIZE = 40  # entries per API call — small enough to keep output reliable
MAX_RETRIES = 3


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
            ("taom_wanderer_strings.xml",                     "std_taom_wanderer_strings_{locale}.xml"),
            ("named_companions/named_companion_strings.xml",  "std_taom_named_companion_strings_{locale}.xml"),
            ("taom_cc_strings.xml",                           "std_taom_cc_strings_{locale}.xml"),
            ("taom_career_strings.xml",                       "std_taom_career_strings_{locale}.xml"),
            ("taom_messenger_strings.xml",                    "std_taom_messenger_strings_{locale}.xml"),
            ("taom_xslt_strings.xml",                         "std_taom_xslt_strings_{locale}.xml"),
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

    # TAOM_Map module — settlements.xml has inline {=KEY}default; we use the populated target as English source
    if module_filter in ("TAOM_Map", "all"):
        target_file = TAOM_MAP_LANG_DIR / lang / "loc_settlements.xml"
        # English source: extract inline keys from the settlements.xml itself
        source_xml = GAME_ROOT / "TAOM_Map" / "ModuleData" / "settlements.xml"
        if target_file.exists() and source_xml.exists():
            entries.extend(_diff_against_settlement_source(source_xml, target_file))

    # LOTRLOME_Armory module — root has English files; per-language dir mirrors them
    if module_filter in ("Armory", "all"):
        armory_root = GAME_ROOT / "LOTRLOME_Armory" / "ModuleData" / "Languages"
        target_dir = armory_root / lang
        if target_dir.exists():
            for src_file in armory_root.glob("loc_*.xml"):
                target_file = target_dir / src_file.name
                if target_file.exists():
                    entries.extend(_diff_files(src_file, target_file))

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

OUTPUT FORMAT:
You MUST respond with ONLY a JSON array, no other text. Schema:
[{{"id": "the_id_from_input", "translated": "your translation"}}, ...]

Every input entry MUST have an output entry with the SAME id. Do not skip entries."""


def call_claude(client, target_language: str, batch: list[Entry]) -> dict[str, str]:
    """Translate a batch via the Claude API. Returns {id: translated_text}."""
    user_payload = [{"id": e.string_id, "text": e.english_text} for e in batch]
    user_msg = (
        f"Translate these {len(batch)} entries to {target_language}. "
        f"Respond with the JSON array only.\n\n"
        f"INPUT:\n{json.dumps(user_payload, ensure_ascii=False, indent=2)}"
    )

    for attempt in range(MAX_RETRIES):
        try:
            response = client.messages.create(
                model=MODEL,
                max_tokens=8192,
                system=SYSTEM_PROMPT.format(target_language=target_language),
                messages=[{"role": "user", "content": user_msg}],
            )
            usage = (response.usage.input_tokens, response.usage.output_tokens)
            text = response.content[0].text.strip()
            # Strip code fences if present
            if text.startswith("```"):
                text = re.sub(r"^```(?:json)?\n?", "", text)
                text = re.sub(r"\n?```$", "", text)
            data = json.loads(text)
            # Tolerate per-item key issues — skip bad items rather than fail the batch
            result_map = {}
            for item in data:
                if isinstance(item, dict) and "id" in item and "translated" in item:
                    result_map[item["id"]] = item["translated"]
            return result_map, usage
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


# ── Write back ─────────────────────────────────────────────────────────────────

def write_back(file_path: Path, translations: dict[str, str], language_tag: str) -> int:
    """Update the <string id=X text="..."/> entries in file_path with new translations.
    Returns number of entries written.
    """
    if not file_path.exists():
        return 0
    with open(file_path, encoding="utf-8") as f:
        content = f.read()
    # Update <tag language="..."/> if present
    content = re.sub(
        r'<tag\s+language="[^"]*"\s*/>',
        f'<tag language="{language_tag}" />',
        content,
    )
    written = 0
    for sid, new_text in translations.items():
        # Escape for XML attribute
        escaped = (new_text.replace("&", "&amp;").replace('"', "&quot;")
                            .replace("<", "&lt;").replace(">", "&gt;"))
        # Match: <string id="sid" text="..."  (preserving any whitespace) />
        # Use a callback to avoid re-injecting backreferences
        pattern = re.compile(
            r'(<string\s+id=")' + re.escape(sid) + r'(")(\s+text=")[^"]*(")'
        )
        new_content, n = pattern.subn(lambda m: m.group(1) + sid + m.group(2) + m.group(3) + escaped + m.group(4), content)
        if n > 0:
            content = new_content
            written += 1
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(content)
    return written


# ── Main orchestration ────────────────────────────────────────────────────────

def estimate_cost(entry_count: int) -> float:
    """Rough cost estimate. ~50 tokens input + ~50 tokens output per entry on average."""
    in_tokens = entry_count * 80   # includes prompt overhead
    out_tokens = entry_count * 60
    return (in_tokens / 1_000_000 * PRICE_INPUT_PER_MTOK
            + out_tokens / 1_000_000 * PRICE_OUTPUT_PER_MTOK)


def main():
    p = argparse.ArgumentParser(description="TAOM AI Translation Tool")
    p.add_argument("--lang", required=True, choices=list(LANGUAGES.keys()),
                   help="Target language code")
    p.add_argument("--module", default="all", choices=["TAOM", "TAOM_Map", "Armory", "all"])
    p.add_argument("--dry-run", action="store_true", help="Preview without writing")
    p.add_argument("--apply", action="store_true", help="Write translations and call API")
    p.add_argument("--max-entries", type=int, default=None, help="Cap API entries for testing")
    args = p.parse_args()

    if not args.dry_run and not args.apply:
        p.error("Specify either --dry-run or --apply")

    lang = args.lang
    locale, lang_name = LANGUAGES[lang]
    overrides = load_overrides(lang)
    cache = load_cache(lang)

    print(f"\n[TAOM Translator] Target: {lang_name} ({lang} / {locale})")
    print(f"  Overrides loaded: {len(overrides)}")
    print(f"  Cache loaded: {len(cache)}")
    print(f"  Module filter: {args.module}")
    print(f"  Mode: {'DRY RUN' if args.dry_run else 'APPLY'}")

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

    api_cost = estimate_cost(len(need_llm))
    print(f"  Estimated API cost: ~${api_cost:.2f}")

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
        # Import here so --dry-run doesn't need anthropic SDK
        import anthropic
        client = anthropic.Anthropic()

        print(f"\n  Calling Claude API ({MODEL}) in batches of {BATCH_SIZE}...")
        for i in range(0, len(need_llm), BATCH_SIZE):
            batch = need_llm[i:i + BATCH_SIZE]
            try:
                translated_map, (in_tok, out_tok) = call_claude(client, lang_name, batch)
            except Exception as exc:
                print(f"    Batch {i // BATCH_SIZE + 1} FAILED: {exc}")
                for e in batch:
                    result.failed.append(e.string_id)
                continue

            result.api_input_tokens += in_tok
            result.api_output_tokens += out_tok

            batch_ok = 0
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
                batch_ok += 1
            print(f"    Batch {i // BATCH_SIZE + 1}/{(len(need_llm) + BATCH_SIZE - 1) // BATCH_SIZE}: {batch_ok}/{len(batch)} ok  (in={in_tok} out={out_tok})", flush=True)
            # Save cache after every batch — resumable on interruption
            save_cache(lang, cache)
        actual_cost = (result.api_input_tokens / 1_000_000 * PRICE_INPUT_PER_MTOK
                       + result.api_output_tokens / 1_000_000 * PRICE_OUTPUT_PER_MTOK)
        print(f"\n  Actual cost: ${actual_cost:.3f} "
              f"(in={result.api_input_tokens} out={result.api_output_tokens})")

    # Write back
    print(f"\n  Writing {sum(len(d) for d in by_file.values())} translations to {len(by_file)} files...")
    total_written = 0
    for file_path, translations in by_file.items():
        n = write_back(file_path, translations, lang_name)
        total_written += n
        rel = file_path.name
        print(f"    {rel}: {n} entries")

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
