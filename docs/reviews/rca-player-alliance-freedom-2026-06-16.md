# RCA — Player Alliance Freedom (2026-06-16)

Deep-review (5 core agents) of the PlayerAllianceFreedom feature: player-founded kingdoms can now form alliances (initiate via dialog + receive AI offers + the vanilla Kingdom→Diplomacy button is unblocked). One confirmed HIGH finding, fixed in-session. The other agent flags were a duplicate ID (the HIGH), plus disputed/overstated items recorded below for completeness.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | Duplicate `<string id="taom_alliance_formed">` — a pre-existing entry (line 371, vanilla-harvested, `{KINGDOM1_LINK}/{KINGDOM2_LINK}`, key `{=0cdXddA9}`) collided with the new dialog-notification entry (line 820, `{PLAYER_KINGDOM}/{TARGET_KINGDOM}`). Duplicate ids shadow each other; the dialog message risked rendering the wrong text with unset variables. | Localization / Verify-Before-Reference | When adding the new key I did not grep the strings file for the chosen id first. The `taom_alliance_*` cluster *looked* free because the nearby keys (`taom_alliance_lore`/`_blocked`/`_war_blocked`) were unique — but `taom_alliance_formed` already lived 449 lines earlier in the file. | Fixed: renamed the new key to `taom_player_alliance_formed` (XML + code). Generalizable rule below (grep the string id before adding). |
| 2 | LOW (disputed) | ADR-002: `if (...) { reason = new TextObject(...); return false; }` in `TaomKingdomDecisionPermissionModel` + the `if (modifier != 0f)` label branch in `TaomAllianceModel`. | Standards | Agent 1 flagged inline `if` in GameModel bodies. | DISPUTED — not fixed. This is the established TaleWorlds permission-gate idiom (map a service bool → out `reason` + early return), identical to the file's pre-existing sibling methods (`IsWarDecisionAllowed`/`IsPeaceDecisionAllowed`) and not introduced by this change. The *decision* lives in `IDiplomacyService`; the model only formats reason/label text at the boundary, which ADR-002 permits. Agent 1 itself hedged ("debatable"). No restructure. |
| 3 | LOW (overstated) | `AllianceAdapter.FindKingdom` does `Kingdom.All.FirstOrDefault` (O(n)); `CanPlayerProposeAlliance` triggers up to 4 scans. | Efficiency | Agent 3 assumed `CanPlayerProposeAlliance` runs in AI daily-tick loops over many kingdom pairs. | DISPUTED severity — not fixed. The AI daily-tick path is the two GameModels, which use pure dictionary lookups (`GetAllianceScoreModifier`/`IsAllianceDecisionAllowed`), NOT `CanPlayerProposeAlliance`. The latter runs only from the dialog condition (per-conversation) + once on accept — ~80 comparisons per dialog refresh over ~20 kingdoms, negligible. `FindKingdom` is also pre-existing adapter code untouched by this change. Out of scope. |
| 4 | INFO (by design) | Dialog-initiated alliance costs 0 influence; the vanilla Kingdom-screen button costs ~200 influence for the same outcome. | Data flow / UX | Agent 5 flagged the asymmetry. | Accepted by design — the user explicitly chose "full freedom." Documented (not silenced) in `docs/features/diplomacy.md` + CHANGELOG so the asymmetry is intentional and discoverable, per the "no silent deferral" rule. |

## Root-cause pattern

Finding #1 is a **namespace-collision-by-prefix-assumption**: a new id was added to a 800+-line shared strings file on the assumption that a sensible-looking name in a thematic cluster was free. It was not — the same id existed far away in the file from an earlier vanilla-string harvest. This is the localization-file instance of the project's recurring "classify/verify by grep, not by assumption" lesson (cf. `feedback_classify_by_grep_not_by_assumption.md`). The CLAUDE.md "Verify Before Reference" rule already covers sprites (`read TAOMSpriteData.xml before Sprite="X"`) and prefab children; it did not explicitly extend to localization string ids.

## Why each deep-review agent missed / caught it

- **Agent 1 (Standards):** Out of scope — checks ADR compliance, not XML id uniqueness.
- **Agent 2 (Compatibility):** Out of scope — verifies TaleWorlds API signatures only.
- **Agent 3 (Efficiency):** Out of scope.
- **Agent 4 (Completeness):** Confirmed all 4 keys exist in the source XML and are referenced in code — but checked *presence*, not *uniqueness*. It correctly reported the keys present; it did not grep for duplicate ids.
- **Agent 5 (Data Flow):** **CAUGHT IT.** Its "string key → consumption" trace enumerated every `{=key}` against the XML and surfaced the two entries sharing `taom_alliance_formed`. This is the data-flow agent doing exactly what it is for — finding a cross-location gap (here, two XML lines 449 apart) that per-line review misses.

## Feedback memory to codify

`feedback_verify_string_id_unique_before_add.md` — Before adding a `<string id="X">` to a shared module-strings XML, grep the whole file for `id="X"` (and the `{=X}` key). Thematic-cluster proximity is not evidence the id is free; vanilla-harvested strings and earlier features scatter ids across the file. Duplicate ids shadow silently — no build error, no test failure, only a wrong-text-at-runtime bug that only the data-flow review or in-game testing catches. This is the localization extension of CLAUDE.md "Verify Before Reference" and a sibling of `feedback_classify_by_grep_not_by_assumption.md`.

---

## Codex adversarial review (2026-06-16)

Codex (gpt-5.5, xhigh) ran after the deep-review fix landed. It DISPUTED the core mechanic correctly (verified +1000 clears every `CanMakeAlliance` gate in both directions; confirmed the string-key fix complete; confirmed the 2-arg regression path byte-identical) and found **3 new findings**, all verified against source + v1.4.6 decompile and all confirmed real. Fixed in-session.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| C1 | HIGH | `InvolvesPlayerKingdom` (both GameModels) used `Clan.PlayerClan?.Kingdom` without checking the player *rules* it. A **vassal/mercenary** player's liege kingdom (AI-ruled) would get the +1000 score bonus AND the lore-Hostile permission bypass for its AI-driven alliance decisions — changing AI-vs-AI diplomacy, which the feature explicitly must NOT do. | Missing precondition / helper duplication drift | The dialog's `GetPlayerLedKingdom` had the correct `RulingClan == PlayerClan` check, but the two model helpers were written fresh and **diverged** from it. Deep-review Agent 5 verified `involvesPlayer` was symmetric + null-safe but accepted the "player's kingdom" definition at face value — it never asked "does this fire when the player is a vassal?" | Fixed: extracted a single `PlayerKingdomHelper.GetPlayerRuledKingdom()`/`InvolvesPlayerRuledKingdom()` (requires `RulingClan == PlayerClan`); both models + the dialog now call it. Lesson: when the same boundary predicate appears in N entry points, centralize it — divergent copies of an authorization check are a bug class. |
| C2 | MED | `GetConversationRulerKingdom` accepted any member of the ruling *clan*, not the kingdom **leader** — a non-leader royal could bind the whole realm to an alliance. | Missing precondition (membership vs rulership) | Deep-review confirmed the target gate excluded non-ruling *clans* and was null-safe, but did not distinguish ruling-clan-*member* from ruling-clan-*leader*. | Fixed: require `kingdom.Leader == hero`. Lesson: a "sovereign actor" check must compare against the leader hero, not clan membership. |
| C3 | LOW | The dialog showed the "alliance forged" message unconditionally after a `void FormPlayerAlliance`, which can no-op (eliminated/missing kingdom between condition and consequence) → false success message. | Missing post-condition verification | Happy-path modeling; the no-op-then-claim-success window wasn't traced. | Fixed: `FormPlayerAlliance` now returns `bool` (confirms `AreAllied` after the engine call); the dialog gates its message + a warning logs the no-op. Added 2 service tests (engine-forms vs engine-no-ops). |

### Root-cause pattern

C1 and C2 are the **same shape**: an identity/authorization predicate that is too loose — "is a member of the player's/ruling kingdom" instead of "*rules* it." C1's specific trigger was **helper duplication**: three boundary spots needed "does the player rule kingdom X," the dialog got it right, the two models got it wrong, and nothing forced them to agree. The fix (one shared `PlayerKingdomHelper`) removes the drift surface for both the bug and any future recurrence. This is the same lesson as the deep-review's string-id finding from a different angle: **a predicate/id that must be consistent across call sites should have exactly one definition.**

### Why deep-review's 5 agents didn't catch C1/C2

- Standards/Compatibility/Efficiency agents are out of scope for authorization semantics.
- Completeness verified tests exist for the service methods (which are correct) — the bug is in the *model boundary* helpers (entry points, not unit-tested per ADR-008).
- Data-flow (the closest) verified `involvesPlayer` was symmetric + null-safe and that AI-vs-AI was unchanged *for the AI-vs-AI argument path* — but it reasoned about "the player's kingdom" as the founded-kingdom happy case and didn't enumerate the vassal/mercenary player state. The miss is a **state-enumeration gap** on the player's own faction role (ruler vs vassal vs mercenary), analogous to the entity-state-matrix rule but applied to the player's political status rather than a Hero lifecycle.

### Note on test coverage for C1/C2

The fixed predicates live in `PlayerKingdomHelper` / the dialog behavior, both of which read `Clan.PlayerClan` / `Hero.OneToOneConversationHero` (campaign statics, not mockable in the unit harness). Per ADR-008 these boundary checks are verified in-game, not unit-tested — so the preventive value is the single-source-of-truth helper, not a new unit test. In-game check added to the verification list: confirm a *vassal* player does NOT see/trigger the freedom bypass, and the dialog line appears only when talking to the actual kingdom leader.

