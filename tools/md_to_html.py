#!/usr/bin/env python3
"""Dependency-free Markdown -> standalone HTML converter for previewing TAOM docs.

Handles the GitHub-flavored subset these docs use: ATX headings, paragraphs,
pipe tables, fenced code blocks, blockquotes, unordered/ordered lists, horizontal
rules, and inline `code` / **bold** / *italic* / [links]. Emits one self-contained
.html (embedded CSS) per input file, next to the source.

Usage: python tools/md_to_html.py <file.md> [file2.md ...]
Cross-links between the converted files are rewritten .md -> .html so they work in a browser.
"""
import sys
import re
import html
import os

# Basenames whose intra-doc links should be rewritten to .html (set per run).
CONVERTED = set()

CSS = """
:root { color-scheme: light dark; }
* { box-sizing: border-box; }
body { max-width: 1100px; margin: 0 auto; padding: 2rem 1.25rem 6rem;
  font: 16px/1.65 -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
  color: #1b1f24; background: #fbfbfa; }
@media (prefers-color-scheme: dark) { body { color: #d6dde3; background: #11151a; } }
h1, h2, h3, h4 { line-height: 1.25; font-weight: 650; margin: 1.8em 0 .6em; }
h1 { font-size: 1.9rem; border-bottom: 2px solid #d8a657; padding-bottom: .3em; }
h2 { font-size: 1.4rem; border-bottom: 1px solid #c9cdd2; padding-bottom: .25em; }
@media (prefers-color-scheme: dark) { h2 { border-color: #2b333c; } }
h3 { font-size: 1.15rem; } h4 { font-size: 1rem; }
p { margin: .7em 0; }
a { color: #1f6feb; text-decoration: none; } a:hover { text-decoration: underline; }
code { font: .88em ui-monospace, "Cascadia Code", Consolas, monospace;
  background: rgba(127,127,127,.16); padding: .12em .38em; border-radius: 4px; }
pre { background: #0d1117; color: #d6dde3; padding: 1rem 1.1rem; border-radius: 8px;
  overflow-x: auto; line-height: 1.5; }
pre code { background: none; padding: 0; font-size: .85rem; color: inherit; }
blockquote { margin: 1em 0; padding: .4em 1.1em; border-left: 4px solid #d8a657;
  background: rgba(216,166,87,.10); border-radius: 0 6px 6px 0; }
blockquote p { margin: .4em 0; }
ul, ol { margin: .6em 0; padding-left: 1.6em; } li { margin: .28em 0; }
hr { border: none; border-top: 1px solid #c9cdd2; margin: 2em 0; }
@media (prefers-color-scheme: dark) { hr { border-color: #2b333c; } }
.table-wrap { overflow-x: auto; margin: 1.1em 0; border: 1px solid #d0d4d9; border-radius: 8px; }
@media (prefers-color-scheme: dark) { .table-wrap { border-color: #2b333c; } }
table { border-collapse: collapse; width: 100%; font-size: .86rem; }
th, td { border: 1px solid #e1e4e8; padding: .42em .7em; text-align: left; vertical-align: top; }
@media (prefers-color-scheme: dark) { th, td { border-color: #2b333c; } }
th { background: #f1f3f5; font-weight: 650; position: sticky; top: 0; }
@media (prefers-color-scheme: dark) { th { background: #1b2128; } }
tbody tr:nth-child(even) { background: rgba(127,127,127,.06); }
td code, th code { font-size: .92em; }
"""


def _rewrite_href(url):
    frag = ""
    if "#" in url:
        url, frag = url.split("#", 1)
        frag = "#" + frag
    base = os.path.basename(url)
    if base in CONVERTED and url.endswith(".md"):
        url = url[:-3] + ".html"
    return url + frag


def inline(text):
    codes = []

    def stash(m):
        codes.append(html.escape(m.group(1)))
        return f"\x00C{len(codes) - 1}\x00"

    text = re.sub(r"`([^`]+)`", stash, text)
    text = html.escape(text)
    text = re.sub(
        r"\[([^\]]+)\]\(([^)\s]+)\)",
        lambda m: f'<a href="{_rewrite_href(m.group(2))}">{m.group(1)}</a>',
        text,
    )
    text = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", text)
    text = re.sub(r"(?<![\*\w])\*([^*\n]+)\*(?![\*\w])", r"<em>\1</em>", text)
    text = re.sub(r"\x00C(\d+)\x00", lambda m: f"<code>{codes[int(m.group(1))]}</code>", text)
    return text


def _is_block_start(s):
    """True if the stripped line begins a non-paragraph block."""
    return (
        s == ""
        or s.startswith("#")
        or s.startswith(">")
        or s.startswith("```")
        or s.startswith("|")
        or re.match(r"^(-{3,}|\*{3,})$", s) is not None
        or re.match(r"^[-*+]\s+", s) is not None
        or re.match(r"^\d+[.)]\s+", s) is not None
    )


def _cells(row):
    row = row.strip()
    if row.startswith("|"):
        row = row[1:]
    if row.endswith("|"):
        row = row[:-1]
    return [c.strip() for c in row.split("|")]


def convert(md):
    lines = md.split("\n")
    out = []
    i = 0
    n = len(lines)
    while i < n:
        line = lines[i]
        stripped = line.strip()

        # fenced code
        if stripped.startswith("```"):
            i += 1
            buf = []
            while i < n and not lines[i].strip().startswith("```"):
                buf.append(html.escape(lines[i]))
                i += 1
            i += 1  # skip closing fence
            out.append("<pre><code>" + "\n".join(buf) + "</code></pre>")
            continue

        # table: a pipe row followed by a separator row
        if stripped.startswith("|") and i + 1 < n and re.match(r"^\s*\|?[\s:|-]+\|[\s:|-]*$", lines[i + 1]):
            header = _cells(line)
            i += 2  # header + separator
            body = []
            while i < n and lines[i].strip().startswith("|"):
                body.append(_cells(lines[i]))
                i += 1
            t = ["<div class=\"table-wrap\"><table>", "<thead><tr>"]
            t += [f"<th>{inline(c)}</th>" for c in header]
            t.append("</tr></thead><tbody>")
            for r in body:
                t.append("<tr>" + "".join(f"<td>{inline(c)}</td>" for c in r) + "</tr>")
            t.append("</tbody></table></div>")
            out.append("".join(t))
            continue

        # heading
        m = re.match(r"^(#{1,6})\s+(.*)$", stripped)
        if m:
            lvl = len(m.group(1))
            out.append(f"<h{lvl}>{inline(m.group(2))}</h{lvl}>")
            i += 1
            continue

        # horizontal rule
        if re.match(r"^(-{3,}|\*{3,})$", stripped):
            out.append("<hr>")
            i += 1
            continue

        # blockquote
        if stripped.startswith(">"):
            buf = []
            while i < n and lines[i].strip().startswith(">"):
                buf.append(re.sub(r"^\s*>\s?", "", lines[i]))
                i += 1
            inner = " ".join(s for s in buf if s.strip())
            out.append(f"<blockquote><p>{inline(inner)}</p></blockquote>")
            continue

        # unordered list
        if re.match(r"^[-*+]\s+", stripped):
            items = []
            while i < n and re.match(r"^[-*+]\s+", lines[i].strip()):
                items.append(re.sub(r"^[-*+]\s+", "", lines[i].strip()))
                i += 1
            out.append("<ul>" + "".join(f"<li>{inline(it)}</li>" for it in items) + "</ul>")
            continue

        # ordered list
        if re.match(r"^\d+[.)]\s+", stripped):
            items = []
            while i < n and re.match(r"^\d+[.)]\s+", lines[i].strip()):
                items.append(re.sub(r"^\d+[.)]\s+", "", lines[i].strip()))
                i += 1
            out.append("<ol>" + "".join(f"<li>{inline(it)}</li>" for it in items) + "</ol>")
            continue

        # blank line
        if stripped == "":
            i += 1
            continue

        # paragraph — always consume the current line first (guarantees forward
        # progress so a leading * / - that is NOT a block start can't loop forever),
        # then gather following lines until a real block start.
        buf = [stripped]
        i += 1
        while i < n and not _is_block_start(lines[i].strip()):
            buf.append(lines[i].strip())
            i += 1
        out.append(f"<p>{inline(' '.join(buf))}</p>")

    return "\n".join(out)


def main(paths):
    for p in paths:
        CONVERTED.add(os.path.basename(p))
    for p in paths:
        with open(p, encoding="utf-8") as f:
            md = f.read()
        title = os.path.basename(p)
        m = re.search(r"^#\s+(.+)$", md, re.M)
        if m:
            title = re.sub(r"`", "", m.group(1)).strip()
        body = convert(md)
        doc = (
            "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n"
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n"
            f"<title>{html.escape(title)}</title>\n<style>{CSS}</style>\n</head>\n<body>\n"
            f"{body}\n</body>\n</html>\n"
        )
        out_path = os.path.splitext(p)[0] + ".html"
        with open(out_path, "w", encoding="utf-8") as f:
            f.write(doc)
        print(f"wrote {out_path}  ({len(doc):,} bytes)")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit("usage: python tools/md_to_html.py <file.md> [...]")
    main(sys.argv[1:])
