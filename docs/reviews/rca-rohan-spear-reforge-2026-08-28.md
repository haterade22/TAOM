# RCA: Rohan spear reforge deep review (2026-08-28)

Six review agents over a data + tooling changeset: 4 new crafting pieces replacing 11, 5 crafted
spears collapsing to 2, 8 troop rosters remapped, plus a new mutation script and its tests.

**Outcome: no defect reached the game data.** Every finding against the reforge itself was either
in the tooling or a content consequence of an approved design choice. The three highest-risk
questions (does it couch, does it stay shield-legal, does the missing collision body hang the game)
were all answered against decompiled engine code or by running the real XSLT transform rather than
by reasoning from documentation.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Newline picked by presence (`"\r\n" if "\r\n" in text`) rather than majority | Tooling | `tools/README.md` names this exact shape as the wrong test, with a worked example. I copied the idiom from my own earlier tool the same day instead of re-reading the convention. | Fixed: `dominant_newline()` uses `crlf > bare_lf`. Test pins a 100:1 mixed document both ways. |
| 2 | MED | Insert side logged the REQUEST size, not what was inserted. A no-op re-run printed `+4 pieces`; a silent anchor failure would also have printed `+4`. | Tooling | The removal side was written to report reality (and correctly surfaced 11-of-12). The insert side was written from the constants because they were in lexical scope. No test asserted a count. | Fixed: both insert functions return `(text, count)`; caller prints the real number. Three tests, including the missing-anchor case. |
| 3 | MED | `repoint_crafted_item`'s substitution ran over the whole `<CraftedItem>` block, not scoped to `<Piece>` | Tooling | I scoped the OUTER match to the item and then assumed the inner substitution inherited that scoping. Safe against today's data only because no sibling element carries the same `id="..." Type="..."` adjacency. | Fixed: pattern anchored on `<Piece\s+`. Test uses a `<Decoy id=... Type="Blade">` sibling. |
| 4 | MED | `rohan_edoras_golden_hall_supreme_rider` lost spear diversity: 3 rosters carried 2 distinct spears, now carry 1 | Content | I validated the remap per REFERENCE (does every ref resolve) and never asked the per-ENTITY question: does any single troop now hold the same item in two rosters? Reference-level validation cannot see it, and `validate_moduledata` passes. | Reported, not silently fixed: diversifying would smuggle a damage change into a top-tier troop. Owner decision. Lesson recorded below. |
| 5 | LOW | Troop-roster backups got an empty tag (`troops_rohan.xml.bak-`) | Tooling | The Armory call passed `tag`; the troop call passed `""`. Copy-paste. | Fixed: both pass `tag`. |
| 6 | LOW | Workflow doc's one-line summary says exclusion tokens are "unioned across all pieces", which reads as if the composed base is assembled from pieces | Docs | The canonical `item-usage-features.md` is correct (base comes from the WeaponDescription; pieces only subtract). Only the compressed summary is ambiguous. | Clarify the summary line. Low priority; the reference a reader is pointed at is right. |

Pre-existing, surfaced but not introduced here, and deliberately not fixed in this pass:
`wm_pelagir_spear_b`/`_c` reference crafting pieces (`spear_handle_6`, `spear_handle_27`) that exist
nowhere; 102 duplicate `AvailablePiece` registrations (2 in `OneHandedPolearm`, 100 in
`TwoHandedPolearm_Bracing`); a stale `docs/reference/lotrlome-armory-snapshot/weapon_descriptions.xslt`;
stale generated reports under `tools/reports/`; 182 orphaned localization strings.

## The finding that matters most, and why every agent nearly missed it

Finding 4 is the only one that changes what a player sees, and **it is invisible to every existing
gate**. `validate_moduledata` asks "does `Item.X` resolve"; it does. `audit_polearm_shield_parity`
asks "can the troop draw it"; it can. The data-flow agent traced piece → registration → item → troop
and found the chain clean, because it *is* clean. The defect only appears if you ask a question none
of them asks: **after a many-to-few remap, does any single entity now hold the same item twice?**

That question is structural to every collapse-style migration, not specific to spears. A remap that
folds N ids into M < N will silently reduce variety wherever one entity referenced two of the folded
ids, and nothing in the repo checks for it.

## Why each agent missed what it missed

- **Standards agent** (armoury conventions): correctly passed the pieces against the authoring doc.
  It had no remit over multi-roster composition, and the doc it audits against says nothing about it.
- **Data-flow agent**: traced reference reachability in both directions and was right that the chain
  is intact. Its rule set is about *broken* links, not *degenerate* ones. It did surface the
  pre-existing dangling `spear_handle_6/27` by extending its sweep file-wide, which is the behaviour
  we want.
- **Engine agent**: answered the three engine questions with decompiled evidence and correctly
  refuted my stated mechanism for `excluded_item_usage_features` (the base list comes from the
  WeaponDescription, not a union across pieces). It also declined to fabricate a `PreloadHelper`
  signature it could not resolve, and said so.
- **Tooling agent**: found 1, 2, 3 and 5, and corrected my premise that all four Armory files are
  CRLF+BOM (none carry a BOM; two are pure LF). This is why the tooling agent exists.
- **Completeness agent**: found the unregistered tool and the localization gap. It over-rated the
  localization as a defect; workflow Step M states Armory 12-language propagation is a separate
  follow-up.
- **Blast-radius agent**: found finding 4, by diffing per-troop rather than per-reference. It is the
  only agent that looked at composition rather than resolution.

## Process lesson, unrelated to the data

Twice this session a Python string literal containing `\n` escapes was injected through a Bash
heredoc and came back with real newlines, producing a syntax error both times: once appending
lessons, once appending these very regression tests. The second occurrence happened after the first
had already been diagnosed. **A heredoc is not a safe transport for source containing escape
sequences.** Use the Write/Edit tools for that, and reserve heredocs for data without backslashes.

## Verification after fixes

723 tests (25 in this script's suite), `audit_polearm_shield_parity` exit 0, `validate_moduledata`
PASS, all four Armory files parse, live and versioned copies byte-identical, and a no-op re-run of
the reforge now correctly reports `+0 / -0` where it previously claimed `+4`.

## Still blocking, and it is not a review finding

`bo_sm_ro_rohan_spear_blade_a` and `_b` exist in `Assets/` and in no cooked pack. An unresolvable
`body_name` makes `PreloadHelper.WaitForMeshesToBeLoaded` spin the main thread forever: no crash, no
log, mission never loads (#352, field-traced via ClrMD). **These spears must not ship before the
packs are re-cooked.**
