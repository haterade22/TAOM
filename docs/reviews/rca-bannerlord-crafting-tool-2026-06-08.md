# RCA — BannerlordCraftingTool deep-review (2026-06-08)

**Scope:** `/deep-review` of this session's overhaul of KEYforce's standalone WPF weapon-piece preview
tool (`E:\LOTRAOMAssets\BannerlordCraftingTool\`), porting Bannerlord's `WeaponDesign.CalculatePivotDistances`
/ `CalculateWeaponLength` faithfully. Tool lives **outside the TAOM repo**; this RCA is filed here because
the central lesson (fidelity over self-consistency when porting engine code) is TAOM-relevant for all
future engine-porting work. The TAOM-specific core-5 agents (ADR-007/Harmony/IoC/MCM/ModuleData/sprites)
did not apply; four tailored agents were substituted (algorithm fidelity, C# correctness, data-flow,
performance).

## Findings

| # | Sev | Finding | Category | Disposition |
|---|-----|---------|----------|-------------|
| 1 | HIGH (claimed) | `CalculateWeaponLength` max-accumulation "compares `ScaledDistanceToNextPiece` but stores `+ ScaledPieceOffset`" — flagged as a wrong-result bug by the C# correctness agent | False positive | **REJECTED** — verified against decompile; see below |
| 2 | GAP | `BladeWidth` parsed from `blade_width` but never consumed (dead field) | Aspirational parse-without-consume | **FIXED** — field + parse removed |
| 3 | MED | `CommitBoxes` silently skipped invalid `ScaleBox`/offset input (e.g. `50.5`, `abc`, `0`), leaving rejected text stale in the box | UX/state-sync | **FIXED** — `SetActivePiece(_active)` re-syncs boxes after commit |
| — | LOW | Build-order fallback to 4-piece sword order for an XSLT-only template not in the bundled/loaded table | By design | No fix — modder loads base `crafting_templates.xml` to override; documented |
| — | LOW | Perf polish (CalculatePivotDistances called twice/interaction; per-redraw brush allocs) | Pre-existing / negligible | No fix — O(4), no visible lag; brush pattern pre-dates this session |
| — | NOTE | 2D/3D pivots in raw cm units assume FBX meshes are cm-scale (engine assumes metres) | Pre-existing unit-coupling | No fix — pre-existing; uniform scaling preserves relative geometry; verify in-app if meshes ever shift 100× |

## Root-cause pattern — the agent disagreement (finding #1)

Two review agents reached **opposite conclusions on the same lines**:
- The **C# correctness agent** saw `if (dist > m) m = dist + offset` and called it a non-monotonic-max bug
  (compare-on-X, store-on-X+Y). It *correctly hedged*: "verify against the actual vanilla
  `CalculateWeaponLength` … since the comment claims it's a verbatim port."
- The **fidelity agent** read the decompile and confirmed the port reproduces the engine exactly.

Resolution by ground truth (`TaleWorlds.Core.WeaponDesign.CalculateWeaponLength`, v1.4.5, lines 277-292):
the engine itself does `if (weaponDesignElement.ScaledDistanceToNextPiece > num2) num2 =
weaponDesignElement.ScaledDistanceToNextPiece + scaledPieceOffset;`. **The "inconsistency" is in the
engine's own code.** For a tool whose entire purpose is to match the smithy bench, reproducing the
engine's quirk is the *correct* behavior; "fixing" it for self-consistency would make the preview
diverge from the game — the exact failure the tool exists to prevent.

**Lesson (generalizable to TAOM):** when porting engine code, *self-consistency is not the spec — the
engine is the spec.* A finding that an isolated method "looks internally inconsistent" must be verified
against the decompiled source before acting; if the engine is itself inconsistent, faithfulness wins.
This is the `evidence-over-claims` rule applied to a review disagreement: the more-confident agent was
wrong, and re-running the decompile settled it (cf. `feedback_codex_caught_api_misread` — same shape,
opposite direction). The disagreement itself was the valuable signal.

## Why each agent's result was right or wrong

- **Fidelity agent (taleworlds-researcher):** correct on all 7 checks — it read both the port and the
  decompile and diffed line-by-line. This is the agent that mattered for a port, and it earned its keep.
- **C# correctness agent:** found the two real fixes (#2 dead field via consumption trace, #3 stale
  text) AND surfaced #1. Its #1 was a false positive, but it *flagged the uncertainty* rather than
  asserting — that hedge is exactly what made the disagreement resolvable instead of a silent wrong fix.
- **Data-flow agent:** caught #2 (`BladeWidth` parsed-but-unused) — the canonical strength of the
  data-flow lens (declared-not-consumed), and verified all 7 README claims trace to real code.
- **Performance agent:** correctly rated everything LOW/negligible for a desktop tool; no false alarms.

## Preventive actions

1. **No new rule needed for #1** — `.claude/rules/evidence-over-claims.md` already mandates verifying a
   finding against source before implementing, and `feedback_codex_caught_api_misread.md` already
   records "when two agents disagree on an API, re-run the decompiler." This RCA is a fresh worked
   example of that rule applied to a *review* disagreement (not just an API one). No rule change.
2. **#2 is the recurring `no-aspirational-fields` pattern** (`feedback_no_aspirational_enum_values.md`,
   `simplicity-criterion.md`): a field parsed "because the plan mentioned it" but never wired is dead
   weight. The data-flow agent catches it every time — keep that agent in any port review. One-off fix,
   no new rule.
3. **#3** one-off UX fix; no systemic pattern.

## Verdict

READY. Build green (0/0). The one HIGH was a verified false positive (engine-faithful, do not change);
the two real findings (dead field, stale text) are fixed and rebuilt. The ported assembly math is
confirmed faithful to v1.4.5 across all 7 fidelity checks plus the 12-template build-order table.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
