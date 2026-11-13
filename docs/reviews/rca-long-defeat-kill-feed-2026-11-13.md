# RCA: The Long Defeat kill feed review (2026-11-13)

**Issue:** #1159. **Branch:** `fix/ld-kill-feed` from trunk `eeb664f4a`.
**Review:** `/deep-review` at risk tier 2 (UI that reads game state): Agent 5 Data flow
(`deep-reviewer`, effort high) and Codex at `xhigh` on the maintainer's word this session that
Codex has usage again (prompt `docs/reviews/codex-prereview-ld-killfeed-2026-11-13.prompt.md`).

## Top-line

Players asked for the kill feed in The Long Defeat. The mission's hand-built view list never added
vanilla's `ViewCreator.CreateSingleplayerMissionKillNotificationUIHandler()`, which all three Custom
Battle view lists add. The change adds that one call. Both reviewers found no runtime gap: the
Gauntlet handler reads only `OnAgentRemoved`, `Agent.Main`, `CombatLogManager` and the player's
options, and no TAOM code touches it. Agent 5 found two text defects, both fixed before the commit.
Codex found none; it called the doc sentence "loose but not a functional defect".

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM (Agent 5) | `long-defeat.md` step 3 said the feed "names who killed whom, with the player's own kills below it". The default `KillFeedVisualType` is 1, "Only Icons (Only in SP)" (`BannerlordConfig`, `DefaultKillFeedVisualType = 1`; Native `global_strings.xml:266`), and `SPGeneralKillNotificationItemVM.InitProperties` sets a name only when `KillFeedVisualType == 0` or the agent is a hero. The personal feed is bottom centre (`SingleplayerKillfeed.xml`, `HorizontalAlignment="Center" VerticalAlignment="Bottom" MarginBottom="150"`) and shows hits as well as kills. The sentence is the acceptance check of the owed in-game check, so a tester on default options would fail a feed that works. | Repeat: an engine effect written from a name | I read the item VM's constructor and saw `MurdererName` and `VictimName`, then wrote the sentence from those field names. I did not read the `_showNames` gate beside them, the option's default or the prefab. AGENTS.md "Research first" already says: quote the line that produces the effect, never infer it from a name. The same pattern was row 16 of `rca-long-defeat-split-2026-11-12.md` the same day. | Lesson added to `lessons/localization-ui.md`: a sentence about what the player sees names the option default and the prefab line that places it. |
| 2 | LOW (Agent 5) | `Client/Features/StoryMissions/StoryMissionViews.cs:21-22` said that the Long Defeat list leaves out the kill feed. The change made that false. | Stale cross-file claim | My search for other users was by identifier (`KillNotification`, `ViewCreator.`) in two folders. The Story Missions comment names the feed in prose ("the kill feed"), so neither the identifier grep nor `graphify affected` (no callers: the list is found by reflection) could reach it. | Same lesson: when a change adds a thing, grep its prose name repo-wide for "leaves out" or "has no" claims about it. |

**Not entered as a finding:** Agent 5's UNVERIFIED LOW, that at 10x speed the general feed has no
entry cap and can stack lines (`SingleplayerGeneralKillFeedWidget` only speeds up fades). It is a
visual question with no defect shown, so it is part of the owed in-game check on #1159.

## Disagreement

Codex judged the step 3 sentence correct "with these precise semantics" and "loose" on the layout.
Agent 5 judged it wrong on the names. I read `SPGeneralKillNotificationItemVM.InitProperties` and the
`KillFeedVisualType` default: at the default setting a troop's name is not shown, so Agent 5 is
right. Codex listed the option semantics correctly but did not hold them against the sentence's
word "names".

## Why each reviewer missed these

- **Codex** saw both facts behind finding 1 and did not hold the sentence to them. Its scope was the
  diff, so the Story Missions comment (finding 2) was outside what it read.
- **Agent 5** found both.

## Fixes

- `docs/features/long-defeat.md` step 3 now says what each option shows, where each feed sits, and
  which options set them.
- `StoryMissionViews.cs` no longer names the kill feed among the views the Long Defeat list leaves
  out; it says the Long Defeat list added it (#1159) and this list still has none.

## Feedback memories to codify

None. The rule exists in AGENTS.md; the lesson entry carries the prevention.
