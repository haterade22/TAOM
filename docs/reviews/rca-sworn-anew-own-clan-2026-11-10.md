# RCA: Sworn Anew sides a captured lord by their own clan (#1114, deep-review 2026-11-10)

## Top-line

`/deep-review` tier 1 ran on the uncommitted #1114 change (trunk `E:/repos/TAOM`, base `99d914b88`, Bannerlord v1.5.7): Lens 5 (data flow), Lens 2 (engine compatibility) and one Codex pass, lowered from `ultra` to `xhigh` because the code diff is one line plus doc comments. The change follows the maintainer's decision that Sworn Anew and Kingdom Allegiance (#1111) share one definition of a lord's side, the lord's own clan (`FactionSides.OfOwnClan`). No reviewer found a defect in the behaviour: Lens 2 verified 35 engine usages on v1.5.7 with 0 incompatible, and Lens 5 traced 8 flows with 0 gaps. One MEDIUM and seven LOW findings were about the tests and the docs; all were fixed in the same change.

## Findings and root cause

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The new `FactionSidesTests` pin for `OfOwnClan` was a source-text check: it caught a revert to `MapFaction`, but not a wrong input such as `Clan.PlayerClan`, and it failed on a rename that keeps the behaviour. Codex ran both mutations. | Weak test | The class summary said a `Hero` cannot be built in the test process, and the pin copied its siblings. `Hero.Clan` (`CompanionOf ?? _clan`), `Clan.Kingdom` (`_kingdom`), `Clan.Culture` and `Hero.Culture` are all field-backed, and two FiefGranting tests already stage an uninitialized `Clan`. | Four behavioural tests on bare engine objects: an orc clan serving Gondor reads its own pair, the same lord through `OfHero` reads the realm, a clanless hero reads their culture, null is `None`. A revert to `MapFaction` fails the first, proven by a mutation run. The adapter keeps a source pin, because `Read` needs a campaign. Lesson in `testing-qa.md`. |
| 2 | LOW | `OfHero_ReadsTheClansMapFaction_NeverTheHeroesMapFaction` sliced the source from `OfHero(` to `OfCharacter(`. Since #1111 put `OfOwnClan` between them, two of its assertions matched `OfOwnClan`'s copy of the same text, so a broken clanless branch in `OfHero` stayed green. Lens 5 F3. Pre-existing since `85e8788dd`. | Weak test | A method inserted between two headers widened a window nobody reread. | The window now ends at `OfOwnClan(`. A mutation of `OfHero`'s clanless branch fails the test. Same lesson. |
| 3 | LOW | Checklist step 6 picked a Neutral lord by realm ("a lord of Shaghana or Abanissa"). With the new side, a Harad clan serving Shaghana reads Evil. Codex, Lens 5 F1. | Doc against code | The checklist was not reread after the side changed. All 17 starting clans of the two realms do carry a Neutral culture. | "A lord of a Shaghana or Abanissa clan, whatever realm they serve." |
| 4 | LOW | The doc did not say that a Neutral lord serving an Evil or Free realm now reads Neutral and is offered to every player, a change in the permissive direction. Step 17 needed an old save. Lens 5 F2. | Doc incomplete | The doc named only the two cases the decision was about. | The Sides bullet names the case; step 17 sets up all three cases in a fresh campaign with `campaign.set_clan_culture` (v1.5.7 `CampaignCheats.cs:329-358`, which matches an id first, then a name). |
| 5 | LOW | A clan the game creates during play takes its settlement's culture (`Clan.CreateCompanionToLordClan`, `Clan.CreateSettlementRebelClan`, v1.5.7 `Clan.cs:1349, 1379`), so its lord is sided by the settlement's people. Lens 2 F1, Lens 5 F5. | Edge of the definition | The definition was chosen for authored clans, which all carry their realm's culture at a new game. | Known Limits line in both feature docs. A per-hero fallback fails the simplicity criterion: the case needs a created clan to leave the player's realm and its lord to be captured later. |
| 6 | LOW | `kingdom-allegiance.md` said the two gates "agree" on such a lord. They side the lord the same way, but their verdicts differ for a realm the player rules. Lens 2 F2. | Doc wording | Written from the shared definition, not the two verdict tables. | "The two features side such a lord the same way", plus the verdict difference. |
| 7 | LOW | `SideIds.cs:9` called the FactionId "the entity's map faction"; `alignment-aware-execution.md` listed the `FactionSides` members without `OfOwnClan` and gave the file 57 lines (it has 71); `sworn-anew.md:98` said the alignment service gives "the side of a realm". Lens 5 F4, drift from #1111. | Doc drift | #1111 added `OfOwnClan` without a grep for the docs that list `FactionSides` members (`csharp-patterns.md` "Renames, moves and deletes" covers renames, not additions). | All three corrected; the line count carries its date. |

## Disagreements

- **Can a behavioural test reach `OfOwnClan`?** Lens 5 accepted the source pin as the cost of "a `Hero` or `Clan` cannot be built in the test process"; Codex asked for behavioural assertions. Codex was right for `FactionSides` (finding 1). Lens 5 was right for the adapter, whose `Read` returns null without `Campaign.Current`.

## Not applied

- **The other lord-facing callers that still side by realm** (Lord Courtesies, Tokens of Esteem, Good Deeds, Hard Questions, tournaments, execution, prisoner-recruitment morale; Codex finding 6, Lens 5 T6, Lens 2 aside). The maintainer's decision covers Sworn Anew and Kingdom Allegiance only. FOLLOW-UP no issue: whether the definition should extend to those features is the maintainer's decision; Hard Questions and Good Deeds side the same captive by realm in the same conversation, which is the strongest case.
- **`MarriageClanPoolStamp.cs:18-22`** says nothing reassigns an existing clan's culture; the cheat and `MinorClanMigrationAdapter.cs:48` do (Lens 5, outside scope). FOLLOW-UP no issue: a comment fix for the next edit of that file.
- **The `OfOwnClan` behavioural tests inherit the class's `RequiresGame` tag** (Lens 5): they stage engine types, so the tag is correct for them.

## Why each agent missed what it missed

- **Lens 2** had the engine brief; findings 3, 4 and 7 are doc checks outside it.
- **Lens 5** accepted the brief's test shape and the class summary's claim (finding 1).
- **Codex** did not reread the older `OfHero` test (finding 2) or the `SideIds` and execution docs (finding 7).
- **The implementer** (this session) copied the sibling source pins and the class summary's claim without checking which getters are field-backed (findings 1, 2), and edited the checklist only for the new case (findings 3, 4).
