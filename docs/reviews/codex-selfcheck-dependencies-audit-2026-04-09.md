# Codex Self-Check: Dependencies Audit Revert Verification

**Date:** 2026-04-09
**Reviewer:** Codex (via codex-companion task)
**Scope:** Verify revert of ConfigurableArrayPool.Bucket.Return() fix

## Context

Our audit found 7 bugs in vendored Dependencies code. Codex adversarial review (`codex-adversarial-dependencies-audit-2026-04-09.md`) caught that our `Bucket.Return()` fix was wrong — we had changed `--_index` to `_index++` (wrong direction for a stack push). We reverted to the original `--_index` pattern.

This self-check verifies the revert is correct.

## Verification Results

### CONFIRMED CORRECT

All 5 checks pass:

| # | Check | Result | Details |
|---|-------|--------|---------|
| 1 | `Rent()` uses `_index++` | PASS | Line 42: post-increment consumes current slot, advances cursor |
| 2 | `Return()` uses `--_index` | PASS | Line 72: pre-decrement retreats cursor, pushes slot back |
| 3 | `if (_index != 0)` guard | PASS | Line 70: correctly prevents underflow before pre-decrement |
| 4 | Stack semantics consistent | PASS | Rent: read-then-post-increment; Return: pre-decrement-then-write |
| 5 | No other code files changed | PASS | Only ConfigurableArrayPool.cs, CHANGELOG.md, review doc |

### Stack Semantics Explanation

The `_buffers` array acts as a stack with `_index` as the stack pointer:

- **Rent (pop):** Read `_buffers[_index]`, then `_index++` — slot consumed, pointer advances past it
- **Return (push):** `--_index` first, then write `_buffers[_index]` — pointer retreats to make room, slot filled

This is a standard array-backed stack where `_index` points to the next available (empty) slot. `_index == 0` means the stack is full (all buffers returned), and `_index == _buffers.Length` means the stack is empty (all buffers rented).

## Files in Revert Commit

- `Dependencies/ThirdParty/Harmony/System.Buffers/ConfigurableArrayPool.cs` — the revert
- `CHANGELOG.md` — updated
- `docs/reviews/codex-adversarial-dependencies-audit-2026-04-09.md` — review doc

No other executable code changed. No risk of collateral damage.
