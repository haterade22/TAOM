"""One generator-owned marker region in a ModuleData file: replace it in place, add it once, or refuse.

A generator writes its rows between `<!-- MARKER:BEGIN -->` and `<!-- MARKER:END -->` so a re-run can find them.
Two rules came out of #947 and #975:

- Replace the region where it stands. Deleting it and re-appending it at the closing tag moves it below any block
  added after it, which the generator's --check then reports as drift.
- Refuse a damaged file: two regions, a lone BEGIN or END, or no closing tag. The engine loads every row in
  document order and a repeated id is deserialized again over the same object, so a stale copy would win.

The caller builds the whole block (markers, indentation, newline) in its file's own style.
"""
import re


def replace_region(text, marker, block, close_tag):
    """`text` with the one `marker` region replaced by `block`, or `block` inserted before the last `close_tag`
    when there is none. Raises RuntimeError on a damaged file."""
    if close_tag not in text:
        raise RuntimeError(f"{marker}: closing tag {close_tag} not found")
    begin, end = f"<!-- {marker}:BEGIN -->", f"<!-- {marker}:END -->"
    pattern = r"[ \t]*" + re.escape(begin) + r".*?" + re.escape(end) + r"\r?\n?"
    begins, ends = text.count(begin), text.count(end)
    # A function replacement, so a backslash in the block is written as it is.
    text, found = re.subn(pattern, lambda _: block, text, flags=re.DOTALL)
    if not (found == begins == ends <= 1):
        raise RuntimeError(f"{marker} before {close_tag}: {begins} BEGIN and {ends} END marker(s), "
                           f"{found} complete region(s); expected at most one")
    if found:
        return text
    idx = text.rfind(close_tag)
    return text[:idx] + block + text[idx:]
