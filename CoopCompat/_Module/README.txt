TAOM.CoopCompat
===============

This module connects Tales From the Age of Men (TAOM) to the BannerlordCoop
co-op mod. You need it only if you play TAOM in co-op. In a single-player
game it does nothing, and you can leave it disabled.


WHAT IT DOES
------------

1. Settings check when you join. Your game compares your TAOM build and your
   TAOM settings with the host's. If they differ, you see a message. It only
   warns you; it never disconnects you.
2. War of the Ring. The host's War of the Ring momentum is sent to every
   player, so your War of the Ring screen shows the host's war.
3. Elite Emissary and armoury for guests. A guest can buy elite troops from
   the Elite Emissary and use a town armoury. The host does the purchase for
   you, and your troops, gold and items reach you through co-op.
4. Fief elections. When a TAOM fief election starts, every player sees the
   same candidates and can vote.


WHAT YOU NEED
-------------

- Bannerlord v1.5.7.
- The full TAOM download. It includes TAOM, TAOM.Dependencies, TAOM_Map,
  LOTRLOME_Armory, DualWield and this module (TAOM.CoopCompat).
- BannerlordCoop, from BannerlordCoop's own release, for Bannerlord v1.5.7.
  This TAOM build was tested with Coop v0.1.8.1. The TAOM download does not
  include Coop.

Every player and the host must use the same TAOM download and the same Coop
version. If one player has a different version, Coop refuses the join.


HOW TO INSTALL
--------------

1. Install TAOM as usual. The TAOM.CoopCompat folder goes in your Modules
   folder, next to the TAOM folder.
2. Install Coop into your Modules folder.
3. In the launcher, enable TAOM.CoopCompat.
4. Put TAOM.CoopCompat below TAOM, TAOM_Map and LOTRLOME_Armory in the load
   order. Its place relative to Coop does not matter.

If you run a dedicated server, add TAOM.CoopCompat to the server's module
list after TAOM.


IMPORTANT: OLDER TAOM.CoopCompat BUILDS
---------------------------------------

An older TAOM.CoopCompat (version 0.3.25, for Bannerlord 1.4.8) uses the same
module name and folder. Some third-party launchers and server tools install
that old build with an "Install or repair TAOM" button. On Bannerlord v1.5.7,
that button replaces this module with the old one, and Coop then refuses your
join.

If that happens, delete the Modules/TAOM.CoopCompat folder. Then copy it again
from the TAOM download.


MESSAGES YOU CAN SEE
--------------------

"Your TAOM build is not the host's build."
    You and the host have different TAOM downloads. Install the same TAOM
    download as the host, and make sure TAOM.CoopCompat came from that same
    download.

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


REPORTING A PROBLEM
-------------------

Report TAOM co-op problems to the TAOM team. Include:

- your TAOM version and your Coop version,
- whether you are the host, a guest, or on a dedicated server,
- the Coop log. For a guest or a host, Coop_client.log or Coop_server.log
  is in the bin/Win64_Shipping_Client folder of your Bannerlord install. On
  a dedicated server, Coop_server.log is in the logs folder of the server's
  data folder. Copy the log before you start the game again, because a new
  start overwrites it.


CREDITS
-------

Based on Sternab's TAOM.CoopCompat for Bannerlord 1.4.8. Guest trade is based
on minimalcash63's TAOM.CoopEmissaryArmory. Both are used with permission.
