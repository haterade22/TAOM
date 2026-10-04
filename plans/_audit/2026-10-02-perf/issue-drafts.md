# Issue drafts (2026-10-02-perf)

Public text: file on the maintainer's word, one issue per section. Each title follows TAOM's issue
conventions; labels are suggestions.

## 028

TITLE: perf: profile mission frame time and allocation per behaviour, and log slow frames
LABELS: enhancement, perf, diagnostics

### Summary
Battles log whole-frame times every five seconds (`[MissionPerf]`), but nothing says which part of the
mod or the engine a frame's time goes to, and recurring 0.6 to 1.1 second hitches in large battles are
unattributed. Add a default-off profiler that times every mission behaviour's tick (milliseconds and
main-thread allocation per behaviour type), the main thread's wait for the parallel agent tick, and the
agent tick itself. It writes a `[TickProfile]` line every five seconds, a `[Hitch]` breakdown for each
frame over a threshold (the first 100 of a mission in full, every one counted), a `[TickSummary]` line
at the end of each measured mission. Whether the profiler is on or off, every mission also gets one
`[PerfContext]` line (build, runtime, graphics options, memory load). Agent spawn work is timed by plan
041, not here.

### Status
Built and deep reviewed on branch `perf/028-mission-tick-profiler`; not merged. The profiler is off by
default; with it off, a player's log gains only one `[PerfContext]` line per mission and one status
line per game launch.

## 029

TITLE: tools: per-mission performance rows and A/B comparison from taom_debug logs
LABELS: tooling, perf

### Summary
There is no parser for `[MissionPerf]` lines, and the battle-load triage reads only the last mission in
a log. Add `tools/perf_runs.py`: one row per mission (load phases, steady-state frame times, GC rates,
the profiler's top costs and hitches) across any number of logs, a compare mode for A/B runs, and flags
for the things that invalidate a comparison (a frame cap outside the game, memory pressure, a dirty
build).

### Status
Built and deep reviewed on branch `perf/029-perf-runs-parser`; not merged.

## 030

TITLE: perf: cut the per-frame and per-hit cost of always-on mission diagnostics
LABELS: perf

### Summary
Several diagnostics run in every battle whether or not anything they watch is present: a per-agent
action-set census every frame for the first five seconds, troll scans every half second, creature
diagnostics on every hit, an empty per-frame patch, a per-agent colour store that is never read, and a
per-hit career log line. Stop paying for them where they have nothing to report: the census reads names
only for a combination it has not seen, the troll trackers wait for the first troll, the creature
diagnostics skip work they would not log, and the empty patch and the unread store are removed. The
information stays in the log, but some lines change: the career line is written once per hero, hit mask
and set of perks per battle, with a hit count summary at mission end; new INFO lines mark the census
window opening and closing and say whether a troll was built; a battle with no creature bandit ends
with one INFO line saying so; and the removed store's cleanup behaviour no longer appears among the
behaviours a battle adds.

### Status
Built and deep reviewed on branch `perf/030-mission-diagnostics-diet`; not merged.

## 031

TITLE: perf: read MCM settings once instead of per blow, per frame and per agent
LABELS: perf

### Summary
Each read of a setting through MCM costs two concurrent dictionary operations and a walk of the
settings containers. Several providers do this on every melee hit (about eight reads per hit), every
frame or every agent update. Cache the settings reference as the battle-balance provider already does,
so live edits in the MCM menu still apply, and move a few cheap checks ahead of the setting reads. Also
parse the Mixed Formations hotkey only when its setting changes (a value that names no key now logs one
warning), and look up localization ids in place instead of cutting each one into a new string.

### Status
Built and deep reviewed on branch `perf/031-settings-reads-off-hot-paths`; not merged.

## 032

TITLE: perf: make the worker-thread formation patch lock-free for formations without a layout
LABELS: perf, bug

### Summary
The Mixed Formations patch on `Formation.GetOrderPositionOfUnit` runs on the engine's parallel agent
workers, about twice a second for every AI unit in a formation. Each call in a field battle allocated an
adapter, and for a formation holding position it also took the service's global lock and read a
formation query that can re-evaluate the formation's class counts on a worker, even for formations that
have no TAOM layout. Answer a formation without a layout lock-free, with no allocation and no formation
query read. A laid-out formation reuses the adapter the main thread built and reads the cavalry flag the
main thread last evaluated, but its unit spacing (`UnitPitch`) still evaluates the formation's
class-ratio queries on the worker, as vanilla's own worker code does; making that path query-free is a
follow-up. Also rule a soldier out of the creature-bandit checks on three hot weapon getters by its
humanoid flags before reading its troop id.

### Status
Built and deep reviewed on branch `perf/032-worker-thread-formation-patch`; not merged.

## 033

TITLE: perf: cut allocations, native wrappers and full scans in creature battles
LABELS: perf

### Summary
Creature battles pay for a local-time clock read (a time-zone conversion) in the timing nodes of every
creature behaviour tree each frame, list allocations whenever a running tree re-enters a selector, boxed
event arguments on every agent removal, and a spatial grid rebuilt every two seconds in every battle,
creatures or not. Read the UTC clock, reuse the lists, skip the event work when no tree listens, and
reuse the grid's maps and skip a rebuild nobody read, without changing what any creature does. Two
candidates were examined and left as they are: the crewed elephants' per-frame skeleton read (no
provably equivalent change) and the creature-bandit target search (its per-agent reads are inlined
field reads, so the nearby-query version saved nothing).

### Status
Built and deep reviewed on branch `perf/033-creature-battle-allocations`; not merged.

## 034

TITLE: perf: stop PatchShield resolving the original method on every call
LABELS: perf, crash-safety

### Summary
PatchShield wraps 159 to 305 patched engine methods with a finalizer that took Harmony's
`__originalMethod`, which makes Harmony look up the original method on every call, including methods
that run per hit, per frame or per soldier. Measured in an isolated benchmark on .NET Framework 4.8,
that binding cost about 63 ns and 241 bytes per call on one thread and about 1.1 microseconds per call
with eight threads calling at once. The finalizers now take only the exception and find the shielded
method from the stack after a throw, with every crash-safety behaviour unchanged; a lookup that cannot
name the method logs one line and the session summary counts it. The frame-time effect in a live
battle is not measured yet.

### Status
Built and deep reviewed on branch `perf/034-patchshield-per-call-cost`; not merged.

## 035

TITLE: tools: report scene shader caches and DLL optimization in the release packager
LABELS: tooling, release

### Summary
Have the release packager list, per packaged scene, whether it carries a shader cache header and a
compressed shader cache, and the cache's format version, warn about a scene cache whose format differs
from the majority of the shipped scene caches, and report each packaged TAOM DLL's JIT optimization
state. Report only: it never blocks a release. Two limits: the format check compares the shipped scene
caches only with each other, so a set that lags as a whole raises nothing; and it does not read the
module-level `Shaders/D3D11` caches, which is where the stale caches in #448 were. Catching a repeat of
#448 needs both, and that is an open decision.

### Status
Built and deep reviewed on branch `perf/035-release-shader-cache-check`; not merged.

## 036

TITLE: diagnostics: log on-demand animation clip memory against the engine's 12 MiB budget
LABELS: diagnostics, perf, triage-needs-ingame

### Summary
The engine budgets on-demand animation clip data at 12 MiB: eviction appears to start once a load
takes the total past 15 MiB, and each pass frees idle clips toward 12 MiB. A clip that is evicted is
loaded again on its next use, and the loading blocks a worker of the parallel agent tick. Add a
read-only probe, on by default (the default is the maintainer's call before the first release), that
logs how much of that budget a battle uses (`[AnimMem]` lines), so the decision on resident clips or a
larger budget rests on a measurement.

### Status
Built and deep reviewed on branch `perf/036-anim-memory-probe`; not merged.

## 037

TITLE: perf: cut per-party and per-frame costs on the campaign map
LABELS: perf

### Summary
On the campaign map TAOM reads MCM settings per party per hour and many times per frame, does per-party
work before the cheap checks that would rule a party out, rebuilds each culture's marketplace item id
sets for every town every day, and walks rosters on every party speed recompute. Read the campaign
settings once (changes in Mod Options still apply at once), run the cheap filters first, build each
culture's id sets once and count a town's guaranteed items in one walk of its market, and walk a party's
roster for the Rohan infantry penalty only when its culture has that feat. No AI decision, recruitment,
desertion, speed or stock result changes. A hand-off that reused vanilla's distance in caravan trade
scoring was built and then dropped as not worth its complexity; it can return if the map profiler (plan
039) shows caravan re-thinks matter.

### Status
Built and deep reviewed on branch `perf/037-campaign-hot-paths`; not merged.

## 038

TITLE: tools: rank which equipment assets dominate a battle's memory
LABELS: tooling, memory

### Summary
Each battle takes two to three gigabytes of native memory and returns it, and no tool says which items
carry that weight. Add an offline audit that resolves every troop's equipment, the way the engine
preloads it, to meshes, materials, textures and collision bodies in the release packages, and ranks the
assets by their bytes in the packages (a floor, and an upper bound that adds mesh edit data), per troop,
culture and side. It attributes package bytes; it does not measure process memory.

### Status
Built and deep reviewed on branch `perf/038-battle-equipment-memory-audit`; not merged.

## 039

TITLE: diagnostics: attribute campaign-map frame time to TAOM's per-frame map code
LABELS: diagnostics, perf

### Summary
The battle profiler (plan 028) says which TAOM code costs frame time in a mission; nothing does the same
on the campaign map, where TAOM runs per-frame and per-party code for borders, field commissions, caravan
trade and party speed. Add a default-off map profiler that splits each map frame into the campaign tick,
the tick-event listeners (TAOM's and vanilla's), the map screen with TAOM's map views, TAOM's
application tick and the rest, with main-thread allocation. It writes a `[MapProfile]` line every five
seconds and a `[MapProfileSummary]` line at the end of a campaign session, following the battle
profiler's line conventions, so the campaign hot-path work is ranked by measurement.

### Status
Built and deep reviewed on branch `perf/039-campaign-map-frame-profiler`; not merged.

## 040

TITLE: diagnostics: stamp the load phases nobody times
LABELS: diagnostics, perf, triage-needs-ingame

### Summary
A new campaign's loading screen lasts about 50 seconds on a fast machine, and only part of it is
attributed today. Always write one `[LoadXml]` line per module XML type the engine loads (files, XSLTs,
merge and object-creation time) with a summary per game, and the phase totals of TAOM's patch
application. Behind a default-off toggle, add a line per patch group, the steps of TAOM's game-start and
game-initialization hooks, and the time of every campaign handler of a new game, a loaded save and the
session start (TAOM's, other mods' and the game's own, each naming its assembly), so every load can be
split into engine and TAOM work and the next fix is chosen by numbers.

### Status
Built and deep reviewed on branch `perf/040-load-time-stamps`; not merged.

## 041

TITLE: diagnostics: extend the battle profiler and turn the hitch probe on by default
LABELS: diagnostics, perf

### Summary
Battles show single frames of 0.6 to 1.1 seconds every 30 to 70 seconds. Extend the battle profiler with
the three attributions it lacks (agent spawn work, script components ticked outside the mission behaviours,
and on-demand animation clip loading) and keep a cheap hitch probe on by default, so the next hitch in any
player's log names its phase. Before the probe ships on by default, the cost of its clip-loading sample
is still to be timed in place.

### Status
Built and deep reviewed on branch `perf/041-profiler-extensions-and-hitch-probe`; not merged.

## 042

TITLE: perf: cut the module XML merge from every campaign load and custom battle start
LABELS: enhancement, perf, triage-needs-ingame

### Summary
About half of a campaign's loading time, and about 15 seconds of a custom battle's, is the engine merging
module XML one file at a time: for every file it builds a fresh schema set, compiles any XSLT again and
rebuilds the whole merged document. Run the same merge with the engine's own helpers in the same order,
but keep one document per type and compile each XSLT once per process. In a test harness on the
installed game, the four largest campaign types took 2.8 seconds against the engine path's 16.8 (the
engine path runs about twice as slowly in the test host as offline, so the fair ratio is about 27 to
35%), and every type's output was identical to the engine's, character for character. On a process's
first load most of what remains is `lords.xslt` (about 1.1 seconds). The in-game load time is not
measured yet.

### Status
Built and deep reviewed on branch `perf/042-xml-merge-load-time`; not merged.
