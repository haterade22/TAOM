#!/usr/bin/env python3
"""Bring clan_heraldry/<culture>.json back in line with the live files (#589).

`generate_clan_heraldry.py` REPLACES a whole <MBPartyTemplate> from a spec's roster and
rewrites the clan's default_party_template binding, so a spec that has fallen behind the
live files is authority to revert whatever landed since: the 2026-08-14 and 2026-09-01
party-size retargets, the Black Numenorean houses, the tier and ranged ladders, #584.
Measured 2026-09-13: 176 of 192 spec rosters and 6 Gondor bindings were behind, and the
tool's guard (rightly) refused 19 of the 21 specs.

`build_clan_specs.py` is not the way back: it composes rosters from scratch by archetype,
so "regenerate then --all --apply" would overwrite the live templates with invented
rosters. The live files are the truth. This tool copies them INTO the specs:

  - template_id  <- the clan's live binding (characters/clans.xml for source "xml",
                    the clan's spclans.xslt override for source "xslt"); written as an
                    explicit key only when it differs from the default
                    kingdom_hero_party_<clan_id>_template, or the key was already there.
  - roster       <- the live template's stacks, in file order.
  - color/color2 are left alone (measured current on all 192 clans; the spec is the
                    source for colours, the live file is the source for rosters).

A clan with a roster whose live faction or bound template cannot be found is refused,
never guessed. Each JSON keeps its own line endings, trailing-newline state and ASCII
escaping. Read-only unless --apply; idempotent (a second run reports nothing).

Usage:
    python tools/sync_clan_specs_from_live.py            # dry-run: report per spec
    python tools/sync_clan_specs_from_live.py --apply
    python tools/sync_clan_specs_from_live.py --spec mordor --apply
"""
import argparse
import glob
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_clan_heraldry as gh  # noqa: E402

PREFIX = "PartyTemplate."
STACK_RE = re.compile(r'<PartyTemplateStack min_value="(\d+)" max_value="(\d+)" troop="NPCCharacter\.([^"]+)"')


class LiveFactionMissing(Exception):
    """A spec clan has a roster but no faction in the live file its source names."""


class LiveTemplateMissing(Exception):
    """A spec clan's live binding names a template the live party file does not define."""


class LiveBindingMissing(Exception):
    """A spec clan has a roster but its live faction binds no default_party_template.

    The engine then falls back to the CULTURE's template (`Clan.DefaultPartyTemplate`, 1.4.8
    `Clan.cs:112-122`), never to a `kingdom_hero_party_<clan_id>_template` that happens to exist,
    so guessing that id would write a roster the clan never fields. Refuse instead."""


class LiveState:
    """The three live texts plus the lookups the sync needs, parsed once."""

    def __init__(self, clans, xslt, party):
        self.clans, self.xslt, self.party = clans, xslt, party
        self.templates = {}
        for m in re.finditer(r'<MBPartyTemplate id="([^"]+)">(.*?)</MBPartyTemplate>', party, re.S):
            self.templates[m.group(1)] = [
                {"troop": t, "min": int(lo), "max": int(hi)} for lo, hi, t in STACK_RE.findall(m.group(2))]

    def binding(self, clan):
        """(found, template_id or None): the clan's live default_party_template, prefix stripped."""
        cid = re.escape(clan["id"])
        if clan["source"] == "xml":
            m = re.search(r'<Faction\b[^>]*?\bid="%s"[^>]*?>' % cid, self.clans, re.S)
            if not m:
                return False, None
            a = re.search(r'\bdefault_party_template="([^"]*)"', m.group(0))
        else:
            m = re.search(r'<xsl:template\s+match="Faction\[@id=\'%s\'\]"\s*>(.*?)</xsl:template>' % cid, self.xslt, re.S)
            if not m:
                return False, None
            a = re.search(r'<xsl:attribute name="default_party_template">([^<]*)</xsl:attribute>', m.group(1))
        if not a:
            return True, None
        value = a.group(1)
        return True, value[len(PREFIX):] if value.startswith(PREFIX) else value


def live_clan(live, clan):
    """What the live files say for a clan that carries a roster: its template id and stacks."""
    found, bound = live.binding(clan)
    if not found:
        raise LiveFactionMissing("%s: no live <Faction> / xslt override for source %r" % (clan["id"], clan["source"]))
    if not bound:
        raise LiveBindingMissing("%s: its live faction binds no default_party_template, so the engine uses the "
                                 "culture default; bind it in the live file first or drop the spec roster" % clan["id"])
    tid = bound
    if tid not in live.templates:
        raise LiveTemplateMissing("%s: bound to %s, which taom_partyTemplates.xml does not define" % (clan["id"], tid))
    return {"template_id": tid, "roster": live.templates[tid]}


ROSTERS_NOTE = ("template_id and roster are synced FROM the live files (the clan's default_party_template "
                "binding and the template's stacks) by tools/sync_clan_specs_from_live.py; the live files are "
                "the source, so edit them and re-sync rather than editing rosters here. Colours are authored here.")


def sync_spec(spec, live):
    """Return (new spec, [(clan_id, what changed)]). Colour-only clans pass through untouched."""
    out = json.loads(json.dumps(spec))
    changes = []
    has_rosters = any(c.get("roster") for c in out["clans"])
    old_note = "rosters archetype-composed from troops_*.xml"
    if has_rosters and old_note in out.get("_note", ""):
        # build_clan_specs.py's header described the rosters as its own; true once, now the
        # colour half only. Reworded so a reader who stops at _note is not misled.
        out["_note"] = out["_note"].replace(old_note, "rosters were originally archetype-composed from troops_*.xml")
        out["_note"] = out["_note"].rstrip(".") + "; see _rosters for their current source."
        changes.append(("(file)", "_note reworded"))
    if has_rosters and out.get("_rosters") != ROSTERS_NOTE:
        # Stamped right after the header keys so the file says where its rosters come from; the
        # "_generated_by" / "_note" lines still describe the COLOUR half, which build_clan_specs.py made.
        items = list(out.items())
        pos = next((i for i, (k, _) in enumerate(items) if k == "clans"), len(items))
        items = [kv for kv in items if kv[0] != "_rosters"]
        items.insert(pos, ("_rosters", ROSTERS_NOTE))
        out = dict(items)
        changes.append(("(file)", "_rosters provenance note"))
    for clan in out["clans"]:
        if not clan.get("roster"):
            continue
        expected = live_clan(live, clan)
        default_id = "kingdom_hero_party_%s_template" % clan["id"]
        before_tid = gh.template_id_for(clan)
        if expected["template_id"] != default_id or "template_id" in clan:
            clan["template_id"] = expected["template_id"]
        if before_tid != expected["template_id"]:
            changes.append((clan["id"], "binding %s -> %s" % (before_tid, expected["template_id"])))
        if clan["roster"] != expected["roster"]:
            changes.append((clan["id"], "roster %d stacks (max sum %d) -> %d stacks (max sum %d)" % (
                len(clan["roster"]), sum(s["max"] for s in clan["roster"]),
                len(expected["roster"]), sum(s["max"] for s in expected["roster"]))))
            clan["roster"] = expected["roster"]
    return out, changes


def serialize(obj, original):
    """json.dumps in the shape the original bytes used: its line ending, trailing newline, escaping."""
    text = original.decode("utf-8")
    eol = "\r\n" if text.count("\r\n") > text.count("\n") - text.count("\r\n") else "\n"
    ascii_only = "\\u" in text
    out = json.dumps(obj, indent=2, ensure_ascii=ascii_only)
    if text.endswith("\n"):
        out += "\n"
    return out.replace("\n", eol).encode("utf-8")


def main():
    ap = argparse.ArgumentParser(description="Sync clan_heraldry specs from the live files")
    ap.add_argument("--spec", help="one culture spec name (without .json); default every spec")
    ap.add_argument("--apply", action="store_true", help="write the specs; default is a dry run")
    args = ap.parse_args()

    live = LiveState(gh.read(gh.CLANS_XML), gh.read(gh.SPCLANS_XSLT), gh.read(gh.PARTY_TMPL))
    paths = [os.path.join(gh.SPEC_DIR, args.spec + ".json")] if args.spec else \
        sorted(glob.glob(os.path.join(gh.SPEC_DIR, "*.json")))

    print("DRY RUN" if not args.apply else "APPLYING")
    total_changes = 0
    written = 0
    for path in paths:
        original = open(path, "rb").read()
        spec = json.loads(original.decode("utf-8"))
        try:
            new, changes = sync_spec(spec, live)
        except (LiveFactionMissing, LiveTemplateMissing) as e:
            raise SystemExit("ERROR: %s: %s\nNothing was written." % (os.path.basename(path), e))
        out = serialize(new, original)
        assert json.loads(out.decode("utf-8")) == new, "serialized spec does not round-trip"
        name = os.path.basename(path)
        if not changes:
            # A hand-laid-out spec (bandits.json) is not rewritten for style alone.
            print("  %-24s current" % name)
            continue
        if serialize(spec, original) != original:
            print("  %-24s (hand layout; rewritten in the standard indent-2 shape)" % name)
        for cid, what in changes:
            print("  %-24s %-24s %s" % (name, cid, what))
        total_changes += len(changes)
        if args.apply:
            with open(path, "wb") as f:
                f.write(out)
            written += 1
    print("\n%d clan change(s) across %d spec(s)%s" % (
        total_changes, len(paths), (", %d file(s) written" % written) if args.apply else ""))
    if not args.apply and total_changes:
        print("Re-run with --apply to write; then `python tools/generate_clan_heraldry.py --all` must refuse nothing.")


if __name__ == "__main__":
    main()
