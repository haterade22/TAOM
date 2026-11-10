# Paste-ready Discord messages: TAOM v2.0.37 changelog, 28 October to 10 November 2026

Post these in order as consecutive messages. Each is under Discord's 4,000-character Nitro cap.
Do NOT paste the separator lines themselves. Items are grouped by category, not by build.
The commit subjects in this window still carry v2.0.36 because the tag moved to v2.0.37 only on 10 Nov.

======== MESSAGE 1 of 2 ========

# TAOM v2.0.37: everything since 28 October
Covers 28 Oct to 10 Nov, the work after the v2.0.36 note (27 Oct). **Requires Bannerlord v1.5.7 (Steam beta). Start a new campaign.** Nobody had played a campaign on 1.5.7 when this was written, so the first report we need is: do you reach the map?

**Engine and install**
• Bannerlord v1.5.7: a new campaign hung on the loading screen, because the game changed how it asks for a party's size limit and the first patrol threw an error every frame. Fixed. (#1112)
• The v1.5.6 hotfix (changeset 125270) is in as well. (#1036)
• Install into a clean `Modules\TAOM` folder. The build no longer ships eight screen files copied from the base game, and copying over an old install leaves them behind. (#1051)
• Two settings start again at their default, because each is stored under a new name: Tournament bet cap per round is now 15,000 (was unlimited), and "Allow Sauron and the Nazgul" in Player Switcher is on.
• Co-op: do not start a co-op session on this build yet. The co-op mod must be rebuilt for v1.5.7 first.

**New battle modes**
• **Fords of War** (main menu, no campaign): you are a commander with a free top-down camera and a command panel. Spend gold on troop packs, heroes and army upgrades, give each of the 3 lanes Push, Hold, Fall back or Seize, take over one soldier with P, and destroy the enemy keep. Each side has towers that shoot back, fall and take at most 50 damage a second. Walls wear down in a long match, a free hero arrives every five minutes, and at minute 40 the stronger side wins. Match speed 2x, 4x, 10x (the speed key moved from T to L). Faction feats include Sentries, Stone and a tower range bonus, and some arrows are poisoned. Spear packs and pierce blows (double damage to cavalry) counter riders; Rohan fields cheap riders often, Mordor sends a swarm, the mûmak costs 1,000 gold. Factions: Rohan, Rivendell, Erebor, Dale, Gundabad, Dol Guldur, Mordor, Harad, Easterlings. AI lanes gather 70 units before an assault and regroup under their own tower. Five balance rounds of AI-versus-AI matches are on record; the mode is a work in progress.
• **The Long Defeat** (main menu): wave survival at Helm's Deep, under vanilla siege AI. Earn gold and buy defenders and fortifications between waves. Pick 1 of 3 boons every third wave. Waves carry affixes (Hammer, Winged Terror) and enemy commanders with summons, Morgul Blades credit a real owner per kill, and the endless scale runs past the authored waves. Three foot classes and a Cavalry class (a mounted sally), the Elven Volley, archers on the high towers and a high-tower volley, gate levels and gate repair, field care. The garrison rests between waves, new defenders muster in the open field, and a run is lost when the last few defenders fall. Speed 1x, 2x, 4x, 10x. No person has played a full run yet. (#1079)
• **Staged Siege**: castle assaults, in the campaign and in Custom Battle, are fought in stages. The attackers take capture points A to D in order, and each one brings fresh troops from both armies. Point E is in the lord's hall, where vanilla's hall fight follows. (#1054)
• **Seat of Seeing**: one key (Q, rebindable) lifts your view off the hero over about 0.7 seconds into a free commander camera, in any mission with a screen. The hero stands fast, or fights on under AI (setting, off by default). If the hero falls, you take a soldier. The view can follow a soldier or a company, and mission time runs while you look.
• **Marshal's Word**: richer orders. Hold Left Shift to queue an order after the current one, with flags on the ground. Several companies keep their array and arrive together. Archers hold a drawn bow until the word (Comma to cycle fire mode, M to give the word, Massed fire gives it by itself). Left Ctrl marks a target. Throwers stand and throw, then charge. Hold the line has its own driver.

======== MESSAGE 2 of 2 ========

**Map and world**
• New realms: Ered Luin, Dorwinion and South Rhovanion (5 towns, 8 castles, 48 villages), plus the Neutral Vales realm of Anduin Vale (3 towns, 8 castles, 30 villages). Angáladh is a work in progress. (#1062, #1053, #1057)
• Feral Orcs: an orc bandit faction with 23 dens across the map. (#1063)
• Elegost, a Ranger of the North, waits in Bree. Named companions now tell their story on first talk. (#1060, #1061)
• Lothlorien fields its own troop trees, armour ladder, villager and lord kits, and Thandirion leads the Lorien custom-battle commanders. Gundabad gets the Snow Orcs of Mount Gram. (#1085)
• Four new Arnor elite troops, from player feedback. Black Uruk weapon duplicates and lance kits fixed. Harad characters wear Yotthani's face paint.
• Cardolan has its banner-green border, Cardolan and Lindon banner colours are fixed, and banner sheets are packed from 20 to 10. castle_K4 belongs to Khand, castle_FH8 to Abanissa. (#1047)
• Faction screens and the encyclopedia have a new theme. Named faction cards play the real hero, not a new clan. (#1067)
• Gondor career forks: hard either/or choices for each kingdom's careers. Cloned careers show their source's ability icon. (#769)
• Elite troops can cost gold only, with no special resource, and siege, raid and battle payouts are flat. Elite Emissary gold sales stop at the party limit. (#1108)
• Clan, inventory and trade, party and quest log screens open faster. (#851 to #854)
• Supply Lines: clicking a caravan no longer ends the encounter. (#1104)
• Each armour-ladder quest gets its own journal entry. The nameplate fade no longer pins its saved Mod Options defaults when the game starts.
• Cleanup: 46 unused party templates and 22 unused rosters removed, including the unbound Lindon and Blue Craig rosters.
• **Arachnophobia Mode** (Accessibility, off by default): spiders leave the game and spider riders ride wargs. (#1110)

**Creatures and combat**
• **Dire wolves**: warg-sized wolves ridden by Gundabad and the goblin tree (Goblin-town, Misty Mountain Orcs, Blue Craig), each with a three-step rider line. (#1081)
• Dwarves fight like dwarves: a head butt that staggers and a drop kick that knocks down unless a shield blocks. (#1087)
• Every creature rider dismounts before death, and a hit no longer knocks a creature rider off. (#1101)
• Elephant and mûmak crews stop at the engine's 2,040-agent limit, which crashed Custom Battle. (#1107)
• A raised shield counts only toward the attacker for scripted blows. (#1089)
• Crash fixes: a stale target after a corpse delete (#1096), a sally-out meeting with an empty party (#1099), childbirth with a null culture (#1076), the encyclopedia "Last seen" page (#1049), creature spawns on co-op clients (#1084).

**Armoury and map art (13 commits)**
• Erkam: Lorien armours and troop trees, the Snow Orcs, Thandirion, a material fix. Solus: Helm's Deep visual overhaul part 1 and a hotfix. Kysaro: map icons. haterade22: dwarf animations, the dire wolves, two art updates.

**Co-op, tools and open work**
• Co-op: the v1.5.6 merge is in review as PR #4230. Guests no longer write shared world state. The Co-op team added War Sails naval modules and a translated dedicated server manager.
• For modders: quad-remeshing, Hair and garment retopology toolkits for Blender; in-game `[Check]` log lines with a reader tool (#1069); unattended game runs; about 150 docs corrected. The repo is open to outside pull requests.
• Open: Field Ballista (#1102), Story Missions (#1105), Hero Defence (#1106), lords across the Free/Evil line (#1111), War Pledge (#1113), wanderer marriage (#1066), 42 blank career portraits (#1058).

**By the numbers:** 486 commits on the TAOM trunk and 13 in the Armoury repo. 68 issues opened since 28 Oct and 64 closed. Tag v2.0.37 (10 Nov).
**Note: this list is not exhaustive.**

======== END ========
---

## Sources (not for Discord)

Written 2026-11-10. `git log --since=2026-10-28` on the TAOM trunk `bannerlord-1.5.x`: 486 commits
after dropping `chore(release)` subjects. `lotraom-assets` log since 28 Oct (13 commits, authors
haterade22, Erkam, Solus, Kysaro). `BannerlordCoop` and `BannerlordCoop.DedicatedServer` logs read for
the co-op bullet only; their commit counts were not computed. `gh issue list`: 68 created and 64
closed since 28 Oct (two searches, limits not hit). Feature docs read for the player wording:
fords-of-war, long-defeat, seat-of-seeing, marshals-word, staged-siege, dire-wolf, dwarf-attacks,
arachnophobia-mode. `docs/releases/v2.0.37-discord.md` supplied the 1.5.7 install notes.

Not verified in game: everything on 1.5.7. Fords of War is parked per the maintainer's memory card.
The Long Defeat "no person has played a full run" line comes from `docs/features/long-defeat.md`
(Phase 1 text) and may be stale. The new-realm list mixes the v2.0.37 note (Ered Luin, Dorwinion,
South Rhovanion) with #1053 (Anduin Vale); check whether Anduin Vale shipped unparked before posting.
