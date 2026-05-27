#!/usr/bin/env python3
"""TAOM backlinks generator.

Walks docs/ and generates "Referenced by" footers in every markdown file that has
inbound references. The footer is delimited by HTML comments so re-runs are idempotent.

Footer format:

    <!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->
    ## Referenced by
    - [INDEX.md](../INDEX.md)
    - [features/foo.md](./foo.md)
    <!-- backlinks-end -->

Usage:
    python tools/build_backlinks.py --dry-run     # report what would change, do not write
    python tools/build_backlinks.py               # write footer updates
    python tools/build_backlinks.py --verbose     # also list files with no changes

Re-uses helpers from tools/lint_docs.py for link parsing, code-fence skipping, and
exemption rules (TEMPLATE.md, codex transcripts, docs/archive/ are skipped).
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

# Import shared helpers from the sibling lint_docs.py.
sys.path.insert(0, str(Path(__file__).resolve().parent))
import lint_docs as ld  # noqa: E402

START_MARKER = "<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->"
END_MARKER = "<!-- backlinks-end -->"
# Detect any backlinks-start marker even if its prose differs (older runs may have written
# a different suffix). Used to strip the footer region before scanning outbound links.
START_MARKER_PREFIX = "<!-- backlinks-start"


def strip_backlinks_region(text: str) -> str:
    """Remove the auto-generated backlinks footer (if any) from text before scanning
    outbound links. Otherwise the footer's own links create a feedback loop where
    every re-run grows new inbound edges. Uses rfind so footer-shaped examples
    inside fenced code blocks (e.g. in docs/adrs/010-knowledge-base-architecture.md)
    don't shadow the real footer at the end of the file."""
    start = text.rfind(START_MARKER_PREFIX)
    end = text.rfind(END_MARKER)
    if start == -1 or end == -1 or end <= start:
        return text
    return text[:start] + text[end + len(END_MARKER):]


def file_is_eligible(p: Path) -> bool:
    """Skip exempt files (TEMPLATE.md, codex transcripts, docs/archive/) for both
    inbound-edge counting AND footer insertion."""
    if ld.is_dead_link_exempt(p):
        return False
    return True


def build_inbound_index(files: list[Path]) -> dict[Path, set[Path]]:
    """For each eligible markdown file, the set of eligible files that link to it.
    Mirrors lint_docs.build_inbound_reference_index but filters both sides via file_is_eligible."""
    index: dict[Path, set[Path]] = {}
    for f in files:
        if not file_is_eligible(f):
            continue
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        text = strip_backlinks_region(text)
        for _lineno, line in ld.iter_lines_outside_code_fences(text):
            scrubbed = ld.INLINE_CODE_RE.sub("", line)
            for match in ld.LINK_RE.finditer(scrubbed):
                target = match.group(2).strip()
                if not ld.looks_like_path_target(target):
                    continue
                resolved = ld.resolve_link(f, target)
                if resolved is None or not resolved.exists():
                    continue
                if resolved.suffix.lower() != ".md":
                    continue
                if not file_is_eligible(resolved):
                    continue
                # Don't self-link
                if resolved == f.resolve():
                    continue
                index.setdefault(resolved, set()).add(f.resolve())
    return index


def relative_link(from_file: Path, to_file: Path) -> str:
    """Posix relative path from from_file's directory to to_file. Uses ./ prefix
    when in the same directory; otherwise standard ../ chain."""
    from_dir = from_file.parent
    try:
        rel = Path(*to_file.relative_to(from_dir).parts).as_posix()
        return f"./{rel}" if "/" not in rel else rel
    except ValueError:
        # to_file isn't under from_dir — walk up.
        from_parts = from_dir.resolve().parts
        to_parts = to_file.resolve().parts
        common = 0
        for a, b in zip(from_parts, to_parts):
            if a.lower() != b.lower():
                break
            common += 1
        ups = len(from_parts) - common
        downs = to_parts[common:]
        if ups == 0:
            return Path(*downs).as_posix()
        return "/".join([".."] * ups + list(downs))


def render_footer(target: Path, inbound: set[Path]) -> str:
    """Render the backlinks footer block for target, listing sorted inbound refs."""
    ordered = sorted(inbound, key=lambda p: str(p).replace("\\", "/").lower())
    lines = [START_MARKER, "", "## Referenced by", ""]
    for src in ordered:
        link = relative_link(target, src)
        # Display label: repo-rooted posix path (clearer than just the basename)
        try:
            label = src.relative_to(ld.REPO_ROOT).as_posix()
        except ValueError:
            label = src.name
        lines.append(f"- [{label}]({link})")
    lines.append("")
    lines.append(END_MARKER)
    return "\n".join(lines)


def splice_footer(content: str, new_footer: str) -> tuple[str, bool]:
    """Replace existing footer block (or append if none). Returns (new_content, changed).
    Uses rfind so footer-shaped examples inside fenced code blocks don't shadow the
    real footer at the end of the file."""
    start_idx = content.rfind(START_MARKER_PREFIX)
    end_idx = content.rfind(END_MARKER)
    if start_idx != -1 and end_idx != -1 and end_idx > start_idx:
        # Existing block — replace.
        prefix = content[:start_idx].rstrip()
        suffix = content[end_idx + len(END_MARKER):]
        # Preserve a trailing newline on suffix if present, else add one
        new_content = f"{prefix}\n\n{new_footer}\n{suffix.lstrip()}".rstrip() + "\n"
    else:
        # No existing block — append.
        trimmed = content.rstrip()
        new_content = f"{trimmed}\n\n---\n\n{new_footer}\n"
    return new_content, new_content != content


def remove_footer(content: str) -> tuple[str, bool]:
    """Strip the backlinks block if present. Used when a file's inbound count drops to 0."""
    start_idx = content.rfind(START_MARKER_PREFIX)
    end_idx = content.rfind(END_MARKER)
    if start_idx == -1 or end_idx == -1 or end_idx <= start_idx:
        return content, False
    prefix = content[:start_idx].rstrip()
    suffix = content[end_idx + len(END_MARKER):].lstrip()
    # Also strip a trailing horizontal rule that was added when the footer was appended
    if prefix.endswith("---"):
        prefix = prefix[:-3].rstrip()
    new_content = (prefix + ("\n\n" + suffix if suffix else "\n")).rstrip() + "\n"
    return new_content, new_content != content


def main(argv: list[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8")
        except Exception:
            pass

    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dry-run", action="store_true", help="Report changes without writing")
    ap.add_argument("--verbose", action="store_true", help="Also list files unchanged")
    ap.add_argument("--summary", action="store_true", help="Emit a --- delimited grep-friendly summary block instead of the per-file change list")
    args = ap.parse_args(argv)

    files: list[Path] = list(ld.iter_markdown(ld.DOCS_DIR))
    inbound = build_inbound_index(files)

    changes: list[tuple[Path, str]] = []  # (path, action)
    for f in files:
        if not file_is_eligible(f):
            continue
        resolved = f.resolve()
        try:
            current = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        refs = inbound.get(resolved, set())
        if refs:
            new_footer = render_footer(f, refs)
            new_content, changed = splice_footer(current, new_footer)
            if changed:
                changes.append((f, f"update ({len(refs)} refs)"))
                if not args.dry_run:
                    f.write_text(new_content, encoding="utf-8")
        else:
            # No inbound refs — strip footer if one exists (was inbound, now isn't)
            new_content, changed = remove_footer(current)
            if changed:
                changes.append((f, "remove"))
                if not args.dry_run:
                    f.write_text(new_content, encoding="utf-8")

    # Report
    if args.summary:
        n_update = sum(1 for _, a in changes if a.startswith("update"))
        n_remove = sum(1 for _, a in changes if a == "remove")
        sys.stdout.write("\n".join([
            "---",
            f"changes:           {len(changes)}",
            f"footers_updated:   {n_update}",
            f"footers_removed:   {n_remove}",
            f"dry_run:           {'yes' if args.dry_run else 'no'}",
            "---",
            "",
        ]))
    elif changes:
        action_word = "Would change" if args.dry_run else "Changed"
        print(f"{action_word} {len(changes)} files:")
        for p, action in changes:
            print(f"  - {ld.rel(p)}  [{action}]")
    else:
        print("No changes needed.")

    if args.verbose and not args.summary:
        unchanged = [f for f in files if file_is_eligible(f)]
        unchanged = [f for f in unchanged if (f, "update") not in changes and (f, "remove") not in changes]
        print(f"\n{len(unchanged)} eligible files unchanged.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
