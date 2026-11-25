TAOM.CoopCompat: play TAOM in co-op
===================================

This guide shows every step to play Tales From the Age of Men (TAOM) with
friends in one shared campaign. Do the steps in order. Do not skip a step.

One player is the HOST. The host's PC runs the shared campaign.
The other players are GUESTS. Guests join the host's campaign.

Every player, host and guests, does Parts 1 to 5 on their own PC.
Only the host does Part 6. Only the guests do Part 7.


WHAT THIS MODULE DOES
---------------------

TAOM.CoopCompat connects TAOM to the co-op mod, "Bannerlord Co-op" (Coop).
Without it, TAOM still runs in co-op, but you do not get these features:

1. Settings check. When you join, your game compares your TAOM build and
   TAOM settings with the host's. If they differ, you see a message. It
   only warns you. It never disconnects you.
2. War of the Ring. Every player sees the host's War of the Ring.
3. Elite Emissary and town armoury. A guest can buy elite troops and use a
   town armoury. The host's game does the purchase for you.
4. Fief elections. Every player sees the same candidates and can vote.
5. Load order check. When you join, the host compares your load order of
   TAOM and of the mods that add items, troops or factions with its own.
   If they differ, you see a message. It only warns you. It never
   disconnects you.
6. Careers. The host keeps your TAOM career in its save. Your career is
   still there when you join again, and the other players see it.
7. Lords' Ladder. A town armoury shows your rung of the lord's gear
   ladder. The host keeps your ladder, takes the lord's materials and fits
   the piece you claim. Battles you win can give you lord's materials.

In a single-player game this module does nothing.


WHAT YOU NEED
-------------

Every player needs all of these:

- Mount & Blade II: Bannerlord, game version v1.5.7.
- The TAOM download. Every player must use the SAME TAOM version.
- The TAOM.CoopCompat download (TAOM.CoopCompat.zip) for that TAOM
  version. This download is for TAOM v2.0.42.
- Bannerlord Co-op v0.1.8, from the Coop team's GitHub page (Part 2).
  Every player must use the SAME Coop version.

If one player has a different TAOM version or a different Coop version,
Coop refuses that player.


PART 1: FIND YOUR MODULES FOLDER
--------------------------------

You copy files into this folder in Parts 2 and 3.

1. Open Steam.
2. Go to your Library.
3. Right-click "Mount & Blade II: Bannerlord".
4. Click "Manage", then click "Browse local files".
   A folder window opens.
5. Double-click the folder "Modules".

This window is your Modules folder. Keep it open.

The usual location is:
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules


PART 2: INSTALL BANNERLORD CO-OP v0.1.8
---------------------------------------

Step A: Remove the Steam Workshop version of Coop.

The Steam Workshop version of Coop can be older than v0.1.8. Two copies of
Coop at the same time break the join.

1. In Steam, click "Workshop" (or open the Bannerlord page, then click
   "Workshop").
2. Search for "Bannerlord Co-op".
3. If you see a green "Subscribed" button, click it. It changes to
   "Subscribe". Now Steam removes that copy.

Step B: Download Coop v0.1.8.

1. Open this page in your web browser:
   https://github.com/Bannerlord-Coop-Team/BannerlordCoop/releases/tag/v0.1.8
2. Under "Assets", click "BannerlordCoop-v0.1.8.zip".
   The file goes to your Downloads folder.

Step C: Unblock the zip file.

Windows can block files that come from the internet. Bannerlord then cannot
load them. Do this BEFORE you extract the zip.

1. Open your Downloads folder.
2. Right-click "BannerlordCoop-v0.1.8.zip".
3. Click "Properties".
4. At the bottom of the "General" tab, look for "Unblock".
5. If you see it, put a tick in the "Unblock" box.
6. Click "OK".

Step D: Put Coop in the Modules folder.

1. If your Modules folder already has a folder named "Coop", delete that
   "Coop" folder first.
2. Right-click "BannerlordCoop-v0.1.8.zip".
3. Click "Extract All...".
4. Click "Extract". A new folder opens. It holds one folder, "Coop".
5. Drag the "Coop" folder into your Modules folder.

Step E: Check the folder.

Open Modules, then Coop. You must see the file "SubModule.xml" directly
inside "Coop":

  Modules\Coop\SubModule.xml          <- correct
  Modules\Coop\Coop\SubModule.xml     <- WRONG: move the inner "Coop" up

Do NOT use the Coop "nightly" installer for TAOM. The nightly installs a
module called "CoopNightly". TAOM.CoopCompat needs the module "Coop", so
the launcher does not load TAOM.CoopCompat with the nightly.


PART 3: INSTALL TAOM AND TAOM.CoopCompat
----------------------------------------

1. Install TAOM the usual way. When you finish, your Modules folder has
   these folders:
     TAOM
     TAOM.Dependencies
     TAOM_Map
     LOTRLOME_Armory
     DualWield
     Bannerlord.Harmony
     Bannerlord.ButterLib
     Bannerlord.UIExtenderEx
     Bannerlord.MBOptionScreen
   The four "Bannerlord." folders are small helper modules that come
   with TAOM. Keep them.
2. Extract the download "TAOM.CoopCompat.zip". It holds one folder,
   "TAOM.CoopCompat". Copy that folder into your Modules folder, next to
   the "TAOM" folder.
3. Check the folder. You must see this file:

     Modules\TAOM.CoopCompat\SubModule.xml

Use the TAOM.CoopCompat download for YOUR TAOM version. A
TAOM.CoopCompat from a different TAOM version does not match.


PART 4: SET UP THE LAUNCHER
---------------------------

1. Start Bannerlord from Steam. The Bannerlord launcher opens.
2. Click "Singleplayer" at the top, then click "Mods".
3. Turn these OFF (no tick). Coop refuses any player who has them on:
   - War Sails (NavalDLC)
   - Birth and Aging Options (BirthAndDeath)
   - Fast Mode (FastMode)
4. Turn these ON (tick):
   - TAOM.Dependencies
   - Native, SandBoxCore, Sandbox, StoryMode, CustomBattle
   - TAOM_Map
   - LOTRLOME_Armory
   - TAOM
   - Bannerlord Co-op
   - TAOM.CoopCompat
   - Dual Wield, if you use it in your normal TAOM game.
5. Put them in this order, top to bottom. Drag a mod to move it.

     TAOM.Dependencies      (at the very top, above Native)
     Native
     SandBoxCore
     Sandbox
     StoryMode
     CustomBattle
     TAOM_Map
     LOTRLOME_Armory
     TAOM                   (below TAOM_Map and LOTRLOME_Armory)
     Dual Wield             (if you use it)
     Bannerlord Co-op
     TAOM.CoopCompat        (at the bottom)

6. If the launcher shows Harmony, ButterLib, UIExtenderEx or Mod Options
   (the TAOM helper modules), leave them as they are.
7. Turn off every other mod. Every player must have exactly the same mods
   ticked. If one player has one extra mod, Coop refuses that player.

Some launchers move TAOM above TAOM_Map by themselves. Check the order
again before every game.


PART 5: CHECK THAT IT LOADS
---------------------------

1. In the launcher, click "Play".
2. Wait for the main menu.
3. You must see the buttons "Host Co-op Sandbox" and "Join Co-op Sandbox".

If you do not see them, Coop did not load. Go back to Part 2, Step E.


PART 6: HOST A GAME (HOST ONLY)
-------------------------------

You need a TAOM save before you can host.

Step A: Make the campaign save (one time only).

1. On the main menu, start a new TAOM campaign the same way you start a
   single-player game.
2. Make your character.
3. Wait until you see the campaign map.
4. Save the game. Give the save a name you can remember.
5. Exit to the main menu.

Step B: Start hosting.

1. On the main menu, click "Host Co-op Sandbox".
2. Pick your save.
3. Choose who can find your game in "Steam Lobbies":
   - "Public": everyone.
   - "Friends Only": your Steam friends.
   - "None": not listed. Steam invites and direct joins still work.
4. Type a password if you want one. You can leave it empty.
5. Click "Host".

A SECOND window opens. That window is the SERVER. It runs the shared
campaign. Then your own game joins the server by itself. You play in your
own game window, not in the server window.

- Do NOT close the server window while your friends play.
- When you finish, close the server window.

Step C: Tell your friends how to join.

- Easiest: invite them through Steam (Shift+Tab, then your friends list,
  then "Invite to game"), or tell them to look in "Steam Lobbies".
- Direct join over the internet: see "PORT FORWARDING" below. Then give
  your friends your public IP address. To find it, search the web for
  "what is my IP".
- Direct join on the same home network: give your friends your PC's local
  IP address (for example 192.168.1.105).

Step D: Keep the .json file with every save.

Every save made while you host has two files with the same name, in
Documents\Mount and Blade II Bannerlord\Game Saves
(it can be OneDrive\Documents\... if OneDrive backs up your Documents):

- name.sav
- name.json  (each player's character and co-op progress)

Always copy, move or back up BOTH files together. Steam Cloud copies only
the .sav file.

If Windows asks if Bannerlord (or "BannerlordStarter") can use the network,
tick your network type and click "Allow". The message can open behind the
game: press Alt+Tab to find it.


PART 7: JOIN A GAME (GUESTS ONLY)
---------------------------------

1. Do Parts 1 to 5 first.
2. On the main menu, click "Join Co-op Sandbox".
3. Find the host's game in ONE of these ways:
   - Steam invite: accept the host's invite.
   - "Steam Lobbies": find the host's game in the list.
   - "Direct": in "Server Address", delete what is there, then type ONLY
     the host's address (for example 203.0.113.5). Do not type a port.
4. If the host set a password, type it in "Password". Capital letters
   matter.
5. Join.

The first time you join a save, the game can ask you to make a character.
Make it the same way as in single player.


PORT FORWARDING (HOST ONLY, DIRECT JOINS OVER THE INTERNET)
------------------------------------------------------------

Steam invites and "Steam Lobbies" usually need no port forwarding.

For a direct join over the internet, forward this port on the host's
router to the host's PC:

  Protocol: UDP
  Port:     4200
  Target:   the local IP address of the host's PC

Every router is different. Search the web for "port forward" and your
router's model name. Guests do not forward anything.


MESSAGES FROM TAOM.CoopCompat
-----------------------------

"Your TAOM build is not the host's build."
    You and the host have different TAOM versions. Install the same TAOM
    version as the host. Use the TAOM.CoopCompat folder from that same
    TAOM version.

"Your TAOM settings differ from the host's in: ..."
    Some of your TAOM mod settings are not the same as the host's. The
    message names the settings groups. Open the mod options and change
    those settings to match the host's.

"The host has not answered your last request yet."
    (Elite Emissary or armoury.) The host's game is still working on your
    last request. Wait a few seconds.

"The host did not answer your last request. Try again."
    (Elite Emissary or armoury.) The host's game did not reply within 15
    seconds. Your first request can still go through late. Check your gold,
    troops and items BEFORE you click again.

"The host cannot trade for you here: ..."
    (Elite Emissary or armoury.) Your party must be in the settlement, and
    not in a battle or a siege. Nothing was charged.


MESSAGES YOU CAN SEE
--------------------

"Your TAOM build is not the host's build."
    You and the host have different TAOM downloads. Install the same TAOM
    download as the host, and the TAOM.CoopCompat download for that TAOM
    version.

"Your TAOM settings differ from the host's in: ..."
    Some of your TAOM mod settings are not the same as the host's. The
    message names the settings groups. Change your settings in the mod
    options to match the host's, or co-op can behave differently for you.

"The host has not answered your last request yet."
    (Elite Emissary or armoury.) The host is still working on your last
    request. Wait a few seconds.

"The host did not answer your last request. Try again."
    (Elite Emissary or armoury.) The host did not reply within 15 seconds.
    The first request can still go through late, so check your gold, troops
    and items before you click again. If it happens every time, the host may
    have a different TAOM build.

"The host cannot trade for you here: ..."
    (Elite Emissary or armoury.) Your party must be in the settlement, and
    not in a battle or a siege. Nothing was charged.

"Your mod load order differs from the host's: on the host ... loads before
..."
    Two of your mods load in the opposite order to the host's. A mod that
    loads later replaces items and troops from earlier ones, so you can see
    different gear or troops from the host's. Put your mods in the host's
    order in the launcher, then restart the game.

"The co-op server did not accept that career change and keeps the career
it has saved for you."
    The host checked your career change against the career rules and did
    not take it. You keep the career the host has saved for you.

"The co-op server did not accept your career and has none saved for you.
You keep it for now, but it is gone when you join again."
    The host checked your career against the career rules and did not take
    it, and it has no career saved for you. Your career still works until
    you leave. When you join again, pick a career the rules allow.

"Your lord's gear has changed since you looked. Visit the armoury again."
    (Lords' Ladder.) Your ladder changed on the host while the armoury was
    open. Leave the armoury and open it again.

"The armourer cannot fit that piece."
    (Lords' Ladder.) That piece is not one you can claim now. Open the
    armoury again and pick one of the pieces it lists.

"The co-op server has turned off the Lords' Ladder for co-op players."
    (Lords' Ladder.) The host has the ladder switched off for players.
    Ask the host if you want it on.


IF YOU CANNOT JOIN
------------------

Coop's own list of join errors is in this file:
  Modules\Coop\Server Instructions.md
Open it with Notepad. Read the part "If a Player Cannot Join".

The usual causes:
- A player has a different TAOM version or Coop version.
- A player has one more or one less mod ticked (Part 4).
- War Sails, Birth and Aging Options or Fast Mode is on (Part 4, step 3).
- The mod order is wrong (Part 4, step 5).
- A Windows firewall message is hidden behind the game (Alt+Tab).


OLDER TAOM.CoopCompat BUILDS
----------------------------

An older TAOM.CoopCompat (version 0.3.25, for Bannerlord 1.4.8) uses the
same folder name. Some third-party launchers and server tools have an
"Install or repair TAOM" button that installs that old build. On
Bannerlord v1.5.7, the old build makes Coop refuse your join.

If that happens:
1. Delete the folder Modules\TAOM.CoopCompat.
2. Copy TAOM.CoopCompat again from the TAOM.CoopCompat.zip download
   (Part 3).


DEDICATED SERVER
----------------

A dedicated server is a Coop server that runs without a game window,
for example on a rented server.

We tested TAOM on the Coop v0.1.8 dedicated server on 21 November 2026.
A player joined and played on the campaign map. But the server download
from Coop does not yet do all the setup that TAOM needs. Without that
setup, the server stops when it starts. It can also fill your C: drive
with crash files of 1 to 2 GB each.

Do not set up a TAOM dedicated server by hand. Use the Coodsz TAOM
tools when their version for Bannerlord 1.5.7 is out. The tools do the
extra setup for you.

On a dedicated server, put TAOM.CoopCompat in the server's module list,
after TAOM. Every player who joins needs the same TAOM version and the
same TAOM.CoopCompat as the server.


REPORT A PROBLEM
----------------

Report TAOM co-op problems to the TAOM team. Send:

- your TAOM version and your Coop version,
- if you are the host, a guest, or on a dedicated server,
- the Coop log file.

Where the Coop log is:
- Host or guest: Coop_client.log or Coop_server.log, in the folder
  Mount & Blade II Bannerlord\bin\Win64_Shipping_Client
- Dedicated server: the newest coop-server-<date>-<time>.log file, in the
  "logs" folder of the server's data folder.

Copy the log BEFORE you start the game again. A new start writes over it.


CREDITS
-------

Based on Sternab's TAOM.CoopCompat for Bannerlord 1.4.8. Guest trade is
based on minimalcash63's TAOM.CoopEmissaryArmory. Both are used with
permission. Bannerlord Co-op is made by the Bannerlord Co-op team.
