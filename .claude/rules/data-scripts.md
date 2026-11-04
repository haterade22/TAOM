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

No lint: 92 of 124 XML-writing scripts trip a naive heuristic (rejected 2026-08-06). Backstop: the deep-review tooling lens.

## Hand edit or script

A judgment call (Mike, 2026-08-21): script when it repeats or needs replay, hand edit for a one-off. Keep the id, the gate and the doc note either way. A tool's activity count never describes what a file lacked.

## Reading what a troop carries? Model the deserializer, not the markup

A script or test that decides what a troop wears or rides builds each battle set the way
`BasicCharacterObject.Deserialize` does: a roster's own slots, then every `<equipment>` written directly under
`<Equipments>` laid over that slot in every set, civilian included (an empty id clears it), with any battle set able
to supply any slot. Reading only `EquipmentRoster` children, or only the first one, misses the troops mounted that way
([lesson](../../docs/reviews/lessons/xslt-moduledata.md), "Troop-level `<equipment>` overrides beat roster slots").
