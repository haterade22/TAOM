# Upstream review

## Overview

TAOM adopts engine knowledge, fixes and measurements from yotthani's repos (`yotthani/bannerlord`,
`yotthani/MithrilForge`), which change every day. This feature keeps each review to the delta: a tracked state file holds
the last reviewed SHA of every watched repo, a tool reports how far each has moved and writes per-area briefs, a
SessionStart hook prints `UPSTREAM AHEAD` when there is something new, and the `/upstream-review` skill runs the review
with every gate kept. Developer tooling only; players see nothing.

## Why This Exists

- **Before:** each review was a one-off `/adopt-external` run. The 2026-10-15 review was out of date the next morning
  (295 new commits), and nothing said so.
- **Requirement (the maintainer, 2026-10-16):** "create a process that automatically does this full workflow".
- **Without it:** a review's baseline lives only in its record, a new delta is noticed by chance, and the steps and the
  traps of the last run are re-learned each time.

## Architecture

### Design Challenge

The check needs the network, and a timed-out hook loses all of its output. Inside `session-start.sh` it shared one 10 s
registration with the game-version and Armory drift checks, so a slow GitHub could cost those banners. The review itself must never push, post or pay without the
maintainer's word.

### Solution Approach

- `docs/reviews/upstream-watch.json`: per repo, `reviewed` (the full SHA of the last reviewed head), `reviewed_on`,
  `record` (the adoption record) and `feature_paths` (the author's features: engine facts only, never the feature).
- `tools/upstream_watch.py status`: the GitHub compare from `reviewed` to the head (page 2 of the response, which carries
  the count without the file list). The answer is cached outside the repo for 20 hours, so a session start pays a live
  read at most once a day. `--brief` is the hook's mode: one `UPSTREAM AHEAD` line per repo with new commits, nothing when
  all are current, an `UNCHECKED` note when gh is missing, logged out, offline or slow. Repos under `releases` get one
  latest-release read each and an `UPSTREAM RELEASE` line when the tag differs from the recorded `seen` (today
  BannerlordCoop, recorded in [coop-interop.md](coop-interop.md)); `seen` records the new tag.
- `.claude/hooks/upstream-watch.sh` runs the check in a SessionStart registration of its own (10 s, the maintainer's
  request of 2026-10-16; SessionStart hooks run in parallel), with a deadline of 8.5 s from its first command, so a slow
  GitHub can cost the upstream line but never the drift banners.
- `areas`: a blobless clone (no file contents, nothing checked out or run), the changed paths per area labelled `feature`
  or `in scope`, the commit subjects, and the full pinned head SHA that the review cites and later advances to.
- `advance`: moves `reviewed` to the pinned head once the review's record lands (the record must be a file in the repo).
- `/upstream-review` ([skill](../../.claude/skills/upstream-review/SKILL.md)): detect, a read-only review (readers per
  area, an Opus verifier, a completeness critic), a plan that stops for approval, issues, build and `/ship`, the
  knowledge batch with its find-and-verify doc review, the advance, and the landing.

## Key Files

| File | Purpose |
|---|---|
| `docs/reviews/upstream-watch.json` | The watched repos and their last reviewed SHAs |
| `tools/upstream_watch.py` | `status`, `areas`, `advance`, `seen` ([tools/README.md](../../tools/README.md)) |
| `.claude/hooks/upstream-watch.sh` | The `UPSTREAM AHEAD` and `UPSTREAM RELEASE` SessionStart hook, registered in `.claude/settings.json` ([hooks catalog](../reference/hooks-catalog.md)) |
| `.claude/skills/upstream-review/SKILL.md` | The review procedure and its gates |

## Tests

- `tools/tests/test_upstream_watch.py`: the state file, the cache, the gh and git runners (injected), the brief, the
  advance validation and the fail-open paths.
- `tools/test_hooks.sh` 7l: `upstream-watch.sh` with a stub tool (ahead, current, a resume, a failing tool, a hang) and
  its registration.

## How to Run a Review

Say "review yotthani's repos" or answer the banner with `/upstream-review`. A daily unattended run of the read-only part
is possible but unproven; the skill's last section says how to try it.

## Changelog

- 2026-10-16: added, from the second yotthani review
  ([adopt-yotthani-2026-10-16.md](../reviews/adopt-yotthani-2026-10-16.md)).

## GitHub Issue

Not filed yet (draft 3 in the 2026-10-16 record).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
