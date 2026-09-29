# RCA: Wild Troll bands and the twenty-brood cap (#694), deep-review 2026-09-28

Eight-lens `/deep-review` of the uncommitted `feat/troll-bandits` worktree (base `6df36909`) before any commit. No
CRITICAL or HIGH; five MEDIUM and a set of LOW findings, each re-read against the worktree, the installed v1.5.3
decompile or the live modules before it was entered here. Three behaviour choices went to Mike (2026-09-28): leave
broods and bands out of the bandit join path, keep bands trolls only, spawn out of the player's sight. All findings
below are fixed in this change; the not-applied proposals and follow-ups close the report.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | With the Roguery perk Partners in Crime, "serve under my command" to a talked-down bandit party recruits every bandit party joining the encounter (`BanditInteractionsCampaignBehavior.OpenRosterScreenAfterBanditEncounter`, v1.5.3 :420-460), so a nearby troll band or brood joined the player; `CanTroopBeTakenPrisoner` is never asked. Open for spiders since #692. | Engine path bypassing a model gate | "Never recruitable" was checked against the one gate that guards capture, not against every way a party's troops change hands. The join path moves members, not prisoners, and the no-parley patch only sees the party the player talks to. The #692 review had the same blind spot. | `Patch94_CreatureBandNoJoin` drops both clans from the join list (the list the destroy loop also walks); `NoJoin_TargetsTheBanditJoinRoster_AndDropsBothClans` binds it. Lesson in `campaign-mechanics.md`. |
| 2 | MED | The brood anchors claimed "every Mirkwood and Dol Guldur settlement" but listed 23 of the 47: the 24 `castle_village_*` ids were missing. | Completeness claim vs source of truth | My check of the live map grepped `[a-z]*_(M|DG)`, which cannot match the two-word prefix `castle_village_`; the unit test then compared the config with a hand copy of itself, so it could never find a gap. | 47 anchors; `CreatureBanditLiveDataTests` derives the set from the live `settlements.xml` by authored culture. Lesson in `testing-qa.md`. |
| 3 | MED | The one-band-per-kingdom policy (living kingdoms, which kingdom a band covers) was computed inline in the behavior, untested beyond a source-text check; the capture case the rule's comment describes was never exercised. | Architecture (ADR-002) | Only the final count gate was moved into the rules; the derivation felt like plumbing. Repeat of #692 finding 4 (the stray rule inline). | `CreatureBanditRules.KingdomsOwedATrollBand` over kingdom ids (`Clan.MapFaction` is the clan when kingdomless), nine DataRows including capture, kingdomless home and elimination. |
| 4 | MED | A troll band that beats a party holding bandit prisoners takes them into its roster (`DefaultBattleRewardModel.GetLootPrisonerChances`, :253-275), so a band stops being trolls only. | Engine data flow | The band-size proof covered spawn-time rolls and Patch39 growth, not post-battle loot. The brood had the same behaviour and it was only documented. | Mike chose trolls-only bands: `TaomBattleRewardModel.GetLootPrisonerChances` drops brood and band winners and renormalises the rest (`WithoutRefusedWinners`, tested). |
| 5 | MED | The daily census wrote one flushed INFO line per brood and per band: up to 44 synchronous writes a day after the cap rose to 20 and the troll bands arrived. | Diagnostics cost | The census was sized for 4 broods; raising the cap did not re-cost it. | One line per census with the parties as a list. |
| 6 | LOW | A brood or band could spawn inside the player's sight; the troll spawner makes it likelier (a killed band is replaced in the same kingdom). | Vanilla parity | The extracted spawn helper copied vanilla's creation steps but not `GetSpawnPositionAroundSettlement`'s retries. | Up to 15 retries for a point outside the player's sight (Mike, 2026-09-28); pinned by a wiring test, proven in game by the spawn line. |
| 7 | LOW | The troll map icon depends on the unversioned Armory's `as_cave_troll_map` / `as_hill_troll_map`; `MBGlobals.GetActionSet` throws on a missing set and nothing on the map catches it. | Unversioned data | The sets exist today; no gate named them. | `CreatureBanditLiveDataTests.TrollMapActionSets_Exist`. |
| 8 | LOW | Once the diagnostics are stripped, a partly renamed anchor set would go unreported. | Observability | The per-id report lived only in the temporary diagnostics. | A one-time warning in the brood spawner names the missing ids. |
| 9 | LOW | The troll MCM hint said "this version" (stale at the next release) and did not say the options below it tune spiders only. | Player text | Written before the release number was known. | Reworded: "Troll bands need a new campaign. The options below tune the spiders only." |
| 10 | LOW | Wrong comments: the clan's "stone-grey banner" (the key is the brood's), the SubModule node's reason for CustomGame (the real one is the culture and template that name the twins), `CreatureBandParties` claiming both spawners count by home, stale prisoner comments. | Docs drift | Written from intent, not from the code they describe. | Corrected; the SubModule claim is pinned by `TrollFile_LoadsInTheCampaign`. |
| 11 | LOW | Dead or redundant code: `Spawn`'s unused return value, the `IsAnchor` filter (`Kingdom.Settlements` holds only towns, castles and villages), the troll spawner's session reset (a new behavior is built at every game start). | Simplicity | Carried over from the extraction and written defensively. | Removed. |
| 12 | LOW | The campaign diagnostics' battle and destruction lines keyed on the brood clan only, and troll prisoner refusals could be crowded out of the 10-line cap. | Diagnostics | Written for #692. | Both clans; the first refusal of each troop always logs. |
| 13 | LOW | Documentation duty: the model registry rows, the landless-culture allowlist mirrors, INDEX, troll-race, bandit-management, the roadmap's translation line and the in-game checklist lacked the troll bands. | Completeness | The feature doc was updated; its satellites were not swept. | All updated; checklist added to the feature doc. |

## Codex pass (2026-09-28)

Codex (xhigh, on `b9fdaaf7`; raw output `docs/reviews/raw/codex-adversarial-troll-bandits-2026-09-28.md`) confirmed no
defect and disputed all six suspects with engine code (the join-list prefix, hero prisoners and the StoryMode and
NavalDLC wrappers, the kingdom count, the bandit-occupation twins, the 47 anchors). One LOW observation, O1: the
out-of-sight retry resampled around the anchor and compared straight-line distance, where vanilla retries around
its first point with reachable points and the path distance, and keeps the first point when every retry fails. Fixed:
`CreatureBandParties.OutOfPlayerSight` is vanilla's `GetSpawnPositionAroundSettlement` step for step. Why missed:
finding 6's fix copied the rule's intent from the lens's summary rather than from the vanilla method itself.

## Root-cause pattern

Findings 1 and 4 share one shape: a contract ("never recruited", "trolls only") was proved at the gate the author
knew about, while vanilla has other paths that move troops between rosters. Finding 2 is the same shape for data: a
completeness claim was proved against a copy, not the source. The prevention is the same: enumerate from the source
(every roster transfer the engine performs; the live map's own settlement list), then pin the enumeration in a test.

## Why each agent missed or caught these

- The builder (this session) wrote findings 1 to 5.
- Lens 5 (data flow) and lens 2 (engine) both found finding 1 independently by walking `IsBandit` consumers; lens 5
  found finding 4. The #692 review's lenses did not walk the encounter joiners because the no-parley patch seemed to
  close the encounter surface.
- Lens 7 (XML) found finding 2 by parsing the live map instead of grepping ids.
- Lens 1 found finding 3 from the #692 precedent; lens 3 found finding 5 by costing the logger.

## Not applied

- Rename `IsCreatureBandClan` (lens 1 LOW): it reads as "a Creature Bandits clan", and a rename ripples into the
  Harmony class names and their tests. Kept.
- Remove `band.ActualClan = clan` (lens 2 nit): a no-op today, kept for parity with vanilla's own looter steps.

## Follow-ups (pre-existing code, not this change)

- A seed-only mode for `tools/translate_with_claude.py` (`--sync-only`); each feature hand-writes a wrapper today.
- Co-op: the brood and troll spawners create parties in a global daily tick with no authority gate, like Refuge and
  SupplyLines; the runtime effect is unverified.
- `LandlordNeedsAccessToVillageCommonsIssueBehavior` picks the first looter-shaped clan; it is vanilla `looters` today.
- The brood spawner's session reset of its log flags is dead for the same reason as the troll one, and the spider
  SubModule node's comment gives the console reason.

## Feedback memories to codify

None beyond the two lessons: both are review-time rules, not session preferences.
