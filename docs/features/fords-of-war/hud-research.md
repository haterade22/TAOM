# Fords of War commander HUD: research brief

> **Status (2026-11-03): a research brief, not a description of the panel.** It informed the command panel, which is shipped and described under "The command panel" in [fords-of-war.md](../fords-of-war.md). The layout in section 3 was a recommendation, and the shipped panel differs: the orders (Z X C V, with V as Seize) sit in the command deck at the bottom centre beside the buy grid, the heroes are at the bottom right, the lane rows form a battle board at the bottom left, and there is a speed button at the top left and an upgrades panel.

Labels: SRC = what a fetched or searched source says. INF = my inference. Sources marked (2nd) are secondary: blogs, forums, reviews. Evidence is thinner than requested; gaps are listed in section 6.

## 1. Rules that matter most

1. **Every HUD element costs screen space and attention.** "More is not always better" in a space-limited UI; the Dreadnought team hid weapon names except during switches and freed about 60% of that component. SRC [S1]. INF: hide per-lane detail until a lane is selected or something changes.
2. **Corners are weak for noticing change.** Brains filter the edges of vision [S1]. Peripheral vision resolves fine form, colour and text poorly; use big, high-contrast shapes, and brightness change rather than fine detail [S2]. INF: a small number in a corner is the wrong carrier for "tower under attack"; use a large pulse near the lane.
3. **Do not rely on colour alone.** Colour is weak in peripheral vision [S2]; heavy edge effects hurt colour-blind players [S1]. Make neighbouring elements look distinct in shape [S2]. INF: every lane, faction and state needs a shape, icon or position cue as well as a hue.
4. **Prefer bars and icons over text for state.** Reading text outside central vision is "very difficult", so prefer bars over percentage strings [S2]. A numeric health readout was criticised because the player "can't tell immediately if ... is high or low" (2nd, forum) [S9]. INF: show numbers beside a bar, not instead of one.
5. **Show only the commands that are usable now.** SC2's command box "only shows commands that I can actually use" (2nd, single-author review) [S5]. Clash Royale greys unaffordable cards and fills a radial progress on each until it is playable [S4] (2nd; teardown at [S3]). INF: design three states for every button: ready, cooling down (radial sweep plus seconds), unaffordable or locked (dim plus reason).
6. **Recognition over recall.** NN/g: visible options give cues that make retrieval easier than recall [S6]. INF: the hotkey letter sits on every button face; the 12-language text never has to be memorised.
7. **Use a fixed grid for repeatable commands.** The SC2 command card is a 3x5 grid, bottom right (2nd, fan wikis and forum) [S7]. INF: map F1-F8 troop buys to a fixed 2x4 grid so key position equals button position.
8. **Fitts's law, adapted: signal strength can offset distance.** Dreadnought treated "amplitude" as how strongly an element signals change; closer placement plus stronger signalling gave about 400% lower difficulty index (their own figure) [S1]. Hotkeys remove pointer travel but depend on practice [S8] (2nd). INF: keep the always-needed buy and order targets large and adjacent; keep rare actions small.
9. **Hick's law: fewer simultaneous choices.** Choice time grows with option count, but practice matters and the law is often misused to justify one big menu [S8] (2nd). INF: the selected lane scopes the panel (orders apply to it), so the player never sees 3 lanes x 4 orders = 12 buttons.
10. **Colour as priority code, used sparingly.** Clash Royale uses yellow for key actions, green for secondary, red only for notifications; it also removed, then restored after complaints, the red "not enough resources" feedback [S4] (2nd). INF: reserve red for threats and one accent per faction for identity.
11. **Text must survive a busy scene and scaling.** AoE IV offers 100 to 150% HUD text scaling and a Strong Contrast mode [S10]. INF: panels get opaque or dark backing, never bare text over the 3D view.
12. **Surface information, minimise depth.** Clash Royale puts controls on the surface rather than in menus [S11] (2nd). Multi-level menus add navigation cost [S8].

## 2. Comparable games

| Game | Zones SRC/INF | Copy | Avoid |
|---|---|---|---|
| StarCraft II | Minimap bottom left, command card bottom right, resources top (2nd, forum comments) [S7][S5]. | Usable-only commands; fixed grid; minimap that shows both sides and accepts clicks [S5]. | Deselect-from-group feedback was unclear [S5]. |
| Age of Empires IV | Dense bottom panel. Official page confirms only text scaling and contrast mode [S10]. Fan complaints: icons "too much information to be digestible at a glance" (forum) [S12]. | Text scaling option. | Repeating one icon per queued unit instead of one icon with a count (forum suggestion) [S12]. |
| Clash Royale | Hand at bottom, one-hand reach [S3]. Next-card preview and the 8-card cycle exist (2nd, [S13]); layout detail UNVERIFIED. | Radial fill to playable; full colour vs greyscale affordability; glowing full bar [S3][S4]. | Short delay before a new card enters can leave the hand short [S13]: avoid unexplained gaps in offers. |
| Dota 2 / LoL | Minimap shows building status in real time under fog rules [S14]. Buyback and respawn UI: UNVERIFIED, no source found. | Minimap carrying building state. | Do not assume more; nothing confirmed. |
| Total War | Unit cards persist; complaints: art-heavy cards hide unit type, text too small (forum) [S9]. Shogun 2 praised for health via a ragged flag and morale via a flashing banner on one element (forum) [S9]. | State on the card itself, one element. | Portrait-only cards; numeric-only health [S9]. |
| Bannerlord | Formation cards show troop count, type, ammo bar, morale; an option slows time 75% while ordering; Hide Battle UI exists (2nd, patch notes summary) [S15]. | Per-formation card with bars; hide toggle. | Slider-heavy setup that players disliked pre-battle [S15]. |
| Company of Heroes, They Are Billions | UNVERIFIED. Searches found only console-port reviews for TAB; nothing usable for CoH. | None | |

## 3. Recommended layout (INF built on section 1; all positions approximate, 1920x1080 reference, anchored by percentage so 4K scales)

- **Top strip, about 5% height, always visible:** left, gold and income per tick (icon plus number); centre, match clock and a thin tug-of-war bar for total keep health; right, faction crest, hero slots (alive count, respawn rings).
- **Left edge or bottom left, about 15% width, always visible:** a vertical or schematic lane strip: three stacked rows, each showing tower pips (filled, damaged, down), capture points as shapes, a front-line marker, and camp icons. Selected lane has a bright frame and number. Doubles as the minimap; click to jump the camera.
- **Bottom centre, about 25% width by 14% height, always visible:** buy grid (F1-F8) as a fixed 2x4, hotkey letter on each button, price, cooldown or locked state.
- **Bottom right, about 15% width, always visible:** orders Z X C V as four large buttons with a shared cooldown ring, scoped to the selected lane.
- **On demand:** hero panel (H) expands upward from the top strip; unit detail on hover; the event feed shows at most 3 lines on the right edge under the top strip, fading after a few seconds.
- **Always keep free:** the central 60% by 60% of the screen, and the sky and ground bands where 300-unit lines are visible. Total permanent HUD stays near the screen perimeter.
- **Alerts:** a tower under attack pulses its lane row (brightness plus shape change) and plays a sound; no full-screen edge effects [S1].

## 4. Specific widgets

- **Rotating offer (Gondor, 30 s):** put the region name and crest as a header on the buy grid, with a thin timer bar along the grid's top edge that drains left to right, plus whole seconds beside it. Show the next region as a small ghost strip of 2 to 4 icons to the grid's right (Clash Royale's next-card idea, INF). At about 5 s left, pulse the bar and dim the grid slightly, so a purchase does not surprise the player. Keep each slot's position fixed across rotations so muscle memory holds; change only the icon and price. Fix the lag problem by locking in a purchase already begun [S13, INF].
- **Shared-cooldown row (Mordor creature call, 30 s):** one large button with a radial sweep and numeric seconds, the same visual language as the order ring. Ready state: bright, glowing border (full-elixir glow idea [S4]); not ready: dimmed with remaining seconds; unaffordable: dimmed plus a short reason. If orders share one cooldown, draw one ring around all four buttons, not four separate timers.
- **Lane state:** use the lane strip in section 3. One row per lane, shape-coded (square capture point, triangle camp, tower pip). Health reads as bar segments, not digits [S2][S9]. Enemy pressure is shown as a marker that advances along the row; INF, no source tested this.
- **Faction identity without clutter:** change only the frame texture, crest, accent colour and button silhouette; keep layout, positions and state colours identical across factions so the grid never moves. INF.

## 5. Mistakes to avoid

- Many small elements in the periphery, or text where a bar would do [S2].
- Colour as the only difference between lane, faction or state [S2][S1].
- Numeric-only health [S9]; portrait-only cards that hide unit type [S9].
- Hidden or unexplained affordability: Clash Royale's red text removal then restoration shows players need a reason shown [S4].
- Offer rotation that moves buttons, or a preview that disappears when the player needs it.
- Small low-contrast text in translated strings; no text scale option [S10]. INF: budget 40% extra width and test German-like length.
- Per-queued-unit icon repetition instead of counts [S12].
- Pulsing that is too weak to notice or strong enough to draw the eye constantly [S2].
- Permanent panels covering the battlefield.

## 6. Sources and gaps

- S1 https://gamedeveloper.com/design/key-user-experience-insights-gained-during-the-creation-of-dreadnought-s-combat-hud (fetched; practitioner article)
- S2 https://gamedeveloper.com/design/perceiving-without-looking-designing-huds-for-peripheral-vision (fetched)
- S3 https://www.therookies.co/blog/game-design-ux-best-practices-detailed-breakdown-of-clash-royale/ (fetched; blog, 2nd)
- S4 same page as S3, affordability and colour points
- S5 https://alienuidesignrules.blogspot.com/2018/08/chapter-3-how-do-other-games-do-it.html (fetched; one blogger, 2nd)
- S6 https://www.nngroup.com/videos/recognition-vs-recall/ (fetched)
- S7 https://wiki.hiveworkshop.com/index.php/UI_Frames and a Blizzard WC3 forum thread via search results (not fetched; 2nd)
- S8 https://gamedev.net/tutorials/game-design/game-design-and-theory/motor-skill-learning-in-user-interfaces-via-dis-r2230 (search snippet only; 2nd)
- S9 forum threads via search: https://steamcommunity.com/app/779340/discussions/0/1733216261947996227 ; https://redlib.ssps.io/r/homeworld/comments/1ilpfey/homeworld_3_overcollapsed_unit_info_panel (search snippets only; opinion)
- S10 https://www.ageofempires.com/age-iv-accessibility/ (fetched; official)
- S11 https://gamedeveloper.com/audio/6-examples-of-ui-design-that-every-game-developer-should-study (fetched)
- S12 https://forums.ageofempires.com/t/ui-icons-traffic-signs/134362 (search snippets only; fan opinion)
- S13 Prima Games and a Clash Royale overview via search snippets (https://primagames.com/eguides/mobile-gaming-eguide-tips-tricks-and-strategies-for-todays-hottest-mobile-games/clash-royale/elixir-fueled-battles; not fetched; 2nd)
- S14 https://liquipedia.net/dota2/Minimap (search snippet only; community wiki)
- S15 https://gamebanshee.com/xdk3d (search snippet only; patch notes summary, 2nd)

**Not verified or not found:** Dota 2 and LoL buyback and hero respawn displays; Company of Heroes layout; They Are Billions HUD; Game UI Database and Interface In Game pages (never reached); GDC talks; Hodent or Stout material; academic RTS HCI papers; colour-blind palette specifics (Game Accessibility Guidelines page returned only its index); any percentage-of-screen guideline for HUD coverage (none found, so the zone sizes in section 3 are my proposals, not sourced figures); SC2 and AoE IV exact zone positions beyond the forum-level claims above.
