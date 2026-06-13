# RCA — War Chariot port deep-review findings (2026-06-12, issue #279)

**Top line:** 6 reviewers (5 core + upstream-fidelity) + adversarial verification + the `/new-creature-mount`
parity audit produced 4 fixed defects (1 HIGH, 1 MEDIUM, 2 LOW), 1 process item, and 1 notable
**false-positive HIGH that survived adversarial verification** and was killed only by direct evidence.
The dominant root cause: **"verbatim from the upstream pack" inherits the upstream pack's own bugs and trims** — fidelity
review is structurally blind to upstream defects; only a vanilla-baseline comparison catches them.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `as_chariot` mapped `act_horse_jump_forward` → `"horse_jump_forwards"` (trailing s) — clip doesn't exist in vanilla packs (the upstream pack shipped it in its own packs; we don't ship them). 1.4.6 strict-lookup crash class (spider RCA #2/#3). | upstream-typo inheritance | Port authoring verified a SAMPLE of vanilla anim refs (7 names), not the full enumeration; the fidelity agent diffs vs the upstream pack and it *has* the typo — fidelity is by design blind to upstream bugs. | When porting external data that references vanilla assets, ENUMERATE and verify EVERY referenced asset name — sampling is how upstream typos ship. The data-flow agent's full-enumeration rule caught it; keep that rule mandatory. Memory: `feedback_ported_data_upstream_bugs_vanilla_baseline.md`. |
| 2 | MED | chariot `monster_usage_falls` had 7 rows vs vanilla horse's 9 (missing front/back light-hit) AND `as_chariot` lacked 5 fall-chain mappings (`fall_left/right_continue`, `fall_backwards`, `fall_jump_roll[_continue]`) — unmapped death-path actions, the exact 1.4.6 crash family. | upstream trim inheritance | The upstream pack flattened vanilla's `base_set` inheritance and trimmed rows; lenient 1.2 tolerated it. "upstream-verbatim" framing made the trim look intentional. Found by the horse-baseline parity audit, NOT by the 6 reviewers (data-flow checks refs RESOLVE, not coverage-vs-baseline). | Parity-audit-before-battle-testing (the `/new-creature-mount` law) is the right gate and it worked — but `tools/audit_mount_parity.py` is hardcoded to spider/warg/elephant; follow-up: parameterize so new creatures get the audit without ad-hoc scripts. For RIDDEN mounts the baseline is the vanilla horse, not the warg. |
| 3 | MED | chariot `monster_usage_upper_body_movements` missing vanilla's `pace=1 direction=back` row (riderless idle — chariots are remountable, riderless chariots WILL exist). | upstream trim inheritance | Same as #2. Independently found by both the data-flow agent and the parity audit (good signal convergence). | Covered by #2's preventive action. Row pointed at `act_chariot_stand_1` (already-mapped) so the lookup always resolves. |
| 4 | LOW | `as_chariot_town_and_village`/`_map` omitted `skeleton=` unlike every other creature-mount derived set in the Armory (spider/elephant carry it; vanilla omits it — engine treats as optional). | intra-module consistency | Ported the upstream pack's shape, which mirrors vanilla. Cosmetic/defensive only. | Fixed for in-module parity. No rule change. |
| 5 | LOW | `as_chariot` uses `act_horse_*` types for 6 gallop/backward strafe cells. | (NOT A BUG) | Verified correct: no `act_chariot_*` equivalents are registered anywhere; the usage set references exactly these names; the upstream pack is identical. | None — documented as the known pattern. |
| 6 | LOW | Issue #279 body describes the original mount-lock design, deleted same-day by maintainer decision (remountable, upstream-pack parity). | process | Design changed after issue creation. | Closing comment on #279 documenting the final no-C# design (at close-out). |

## The false positive worth remembering

The data-flow agent flagged **"chariot jumps cover only 2 of 9 directions → 1.4.6 sentinel-deref AV"**
as HIGH, and the adversarial verifier **CONFIRMED it**. Direct key-by-key diff refuted it: the chariot's
10 jump rows are identical to the vanilla horse's (full start/loop/end chains for `front` + `none`).
The 9-direction bar comes from the spider's 1.4.6 fix — for **BT creatures that turn mid-jump**; ridden
mounts never do, and vanilla horses ship exactly this profile on 1.4.6.

**Lesson:** an adversarial verifier inherits the finder's BASELINE. Both agents validated the evidence
("only 2 directions present" — true) against the wrong reference class (BT creature instead of ridden
mount). Verification prompts must require the verifier to challenge the baseline/reference choice, not
just reproduce the evidence. (This is the review-side mirror of memory
`feedback_codex_caught_api_misread.md` — when agents agree confidently, the shared premise can still be wrong.)

## Why each reviewer missed what it missed

- **standards / efficiency / completeness** — out of scope by design (C# diff was comment-only); all passed correctly.
- **api (taleworlds-researcher)** — schema-level: attribute names vs 1.4.6 loaders. Found #4/#5; clip-name
  existence is a pack-content question, outside schema scope. 2 of its 5 findings were refuted as
  upstream-pack/elephant-precedent-consistent (correct refutations).
- **fidelity** — passed everything because the port IS faithful to the upstream pack. Structural blindness: fidelity
  review can never catch bugs the upstream itself shipped (#1) or trims the upstream made (#2, #3).
  **Fidelity and vanilla-baseline parity are complementary, never substitutes.**
- **data-flow** — caught #1 (the only agent that could) + #3; missed #2's falls dimension because its
  rules verify that references RESOLVE, not that coverage matches a baseline. The parity audit covers
  that axis.

## Feedback memories to codify

One new memory: `feedback_ported_data_upstream_bugs_vanilla_baseline.md` — "Upstream worked" ≠ "upstream
is correct": ported data inherits upstream typos (missing-asset refs) and upstream trims (coverage gaps
vs vanilla). A 1-for-1 port needs BOTH a fidelity diff (against the source) AND a baseline parity audit
(against the closest vanilla equivalent) — fidelity catches transcription drift, parity catches inherited
defects. The chariot's baseline is the vanilla horse (ridden, no BT), not the warg.

## Fixes applied (all verified post-fix: falls 9=9, upper-body 13=13, jumps 10=10 identical, fall-chain
mappings complete, `horse_jump_forwards` zero live refs, both files well-formed)

All in `LOTRLOME_Armory/ModuleData/`: `action_sets.xml` (+5 fall mappings, jump-clip typo fix,
`skeleton=` on 2 derived sets), `monster_usage_sets.xml` (+2 fall rows, +1 upper-body row).
