---
paths:
  - "tools/**/*.py"
  - "tools/**/*.ps1"
---

## Writing a data-mutating script? Read `tools/README.md` "XML I/O convention" FIRST

A script that edits XML follows the XML I/O convention (`tools/README.md`).

**The two sanctioned idioms — pick one, never mix them:**

```python
# A. utf-8-sig decode + explicit BOM re-prepend
had_bom = path.read_bytes().startswith(b"\xef\xbb\xbf")
text = path.read_text(encoding="utf-8-sig")
path.write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))

# B. full binary round-trip (BOM survives inside the string)
text = open(path, "rb").read().decode("utf-8")
open(path, "wb").write(text.encode("utf-8"))
```

**Forbidden:** the mixed shape — plain `utf-8` text read plus a text-mode write. It silently strips a
BOM *and* normalises CRLF→LF, turning a two-attribute edit into a whole-file rewrite.

Also mandatory for any script that writes outside the repo: a backup before the destructive write
(**never** a `*.xml` extension — these folders are globbed, and an `.xml` backup injects duplicate
item ids), a dry-run default with an explicit `--apply`, idempotency on re-run, and exact-token
comparison rather than substring containment when deriving a target set from a report or index.

**Parse before writing.** Any script that transforms XML runs the result through `ElementTree` and refuses a document that no longer parses. Restore masked text by indexed token, never by recorded offset: a length-changing edit invalidates every offset.

No lint: one was rejected on 2026-08-06 ([lesson](../../docs/reviews/lessons/build-tooling-workflow.md), "Data-mutating XML scripts must round-trip byte-faithfully"). Backstop: the deep-review tooling lens.

## Writing a gate, a `--check` or an exemption? Break each value it owns

A gate that passed on the state you just made has proved nothing yet. Before it ships, give it one test per value
it owns, each broken alone: the row is there with the WRONG value; the row sits inside an XML comment; the exempt
pair moved to the place the exemption does not cover; the excluded clip bound where it must be judged; the
excused item failing in a second way, with other numbers than the list names, or gone from every set that binds it. Write some
expectations as literals, not from the tool's own tables. A tool that edits text byte-faithfully verifies its
result as its READER reads it: parsed, and with what the reader merges or inherits (another block of the same id,
a child set that overrides). A check that guards a write runs before the write, dry run included. A change that
takes a verdict out of a gate names, for each test it deletes, the test that still holds what that test held, or
the reason the check is dropped
([lesson](../../docs/reviews/lessons/build-tooling-workflow.md), #1541; the gate RCAs of 2026-11-20, 2026-11-22
and 2026-11-24).

## Hand edit or script

A judgment call (Mike, 2026-08-21): script when it repeats or needs replay, hand edit for a one-off. Keep the id, the gate and the doc note either way. A tool's activity count never describes what a file lacked.

## Reading what a troop carries? Model the deserializer, not the markup

A script or test that decides what a troop wears or rides builds each battle set the way
`BasicCharacterObject.Deserialize` does: a roster's own slots, then every `<equipment>` written directly under
`<Equipments>` laid over that slot in every set, civilian included (an empty id clears it), with any battle set able
to supply any slot. Reading only `EquipmentRoster` children, or only the first one, misses the troops mounted that way
([lesson](../../docs/reviews/lessons/xslt-moduledata.md), "Troop-level `<equipment>` overrides beat roster slots").
