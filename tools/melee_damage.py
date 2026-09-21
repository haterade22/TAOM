#!/usr/bin/env python3
"""Melee weapon damage model: a faithful port of the engine's crafted-weapon physics.

Bannerlord does not store a crafted weapon's damage. It simulates it, every load, from the
four crafting pieces. This module reproduces that simulation so TAOM tooling can price a
melee weapon without launching the game.

Ported from the v1.5.3 decompile (`E:\\Decompiled_Bannerlord\\_categories_v1.5.3`):

    TaleWorlds.Core/CombatStatCalculator.cs   energy transfer -> blow magnitude
    TaleWorlds.Core/Crafting.cs               CraftingStats: assembly, speed sim, damage
    TaleWorlds.Core/CraftingPiece.cs          Deserialize: how XML becomes piece physics
    TaleWorlds.Core/WeaponDesignElement.cs    the Scaled* properties
    TaleWorlds.Core/WeaponDesign.cs           pivot distances -> crafted weapon length

The model is stable across the engine bumps TAOM has shipped: `CombatStatCalculator.cs` and
`WeaponDesign.cs` are byte-identical from v1.4.5 through v1.5.3, and `Crafting.cs` differs
only in one debug-export string. Re-diff those three files on the next bump before trusting
this; that check is the whole maintenance burden.

Two engine behaviours look like bugs and are NOT. Both are pinned by name in
`tools/tests/test_melee_damage.py`, so do not "fix" them:

  1. `Crafting.CraftingStats.ParallelAxis(WeaponDesignElement, ...)` (Crafting.cs:508-513)
     reads the piece's UNSCALED `Inertia` and `CenterOfMass` but a SCALED mass. A scaled
     piece therefore contributes its unscaled rotational inertia.
  2. `CalculateWeaponInertia` (Crafting.cs:278-293) advances its running offset by
     `+= ScaledLength` for every piece regardless of build-order sign, so a pommel
     (order -1) is walked forward from the blade rather than backward from the grip.

Units: the engine works in metres. `length` and the offsets are centimetres in XML and are
converted on parse, exactly as `CraftingPiece.Deserialize` does. `weight` is already kg.

Read-only. This module never writes XML.
"""

from __future__ import annotations

import math
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

# --- engine constants (CombatStatCalculator.cs:7-19) --------------------------------------

SWING_SPEED_CONST = 4.5454545
THRUST_SPEED_CONST = 11.764706
ARM_LENGTH = 0.5
ARM_WEIGHT = 2.5

# The numerators the speed simulations divide by (Crafting.cs:330, :360). They matter twice:
# they set the displayed speed, and because `raw_speed = CONST / simulated_time`, they invert
# back into the attack time the DPS model needs.
SWING_TIME_CONST = 20.8
THRUST_TIME_CONST = 3.8500000000000005

# Attacker recovery after a connecting blow, from Native/ModuleData/managed_core_parameters.xml
# (`StunPeriodAttackerSwing`, `StunPeriodAttackerThrust`) as shipped with v1.5.3. These are the
# defaults; `melee_catalogue.load_combat_parameters` reads the live values, because a module may
# override the file. The asymmetry is enormous and is the single biggest reason a thrust-only
# weapon loses on sustained damage: 0.67 s of recovery against 0.1 s.
STUN_PERIOD_SWING = 0.1
STUN_PERIOD_THRUST = 0.67

# The simulation timestep. The swing loop uses the float32 literal the decompile shows
# (0.009999999776482582, i.e. (double)(float)0.01); the thrust loop uses a plain 0.01.
# They are genuinely different in the engine and the difference is observable over ~100
# iterations, so keep them apart. (Crafting.cs:521-568)
SWING_DT = 0.009999999776482582
THRUST_DT = 0.01

# CraftingPiece.PieceTypes (CraftingPiece.cs:14-22). The order is load-bearing: the engine
# indexes UsedPieces by this enum, and Crafting.cs reads [0] for blade data and [2] for the
# handle's grip correction.
PIECE_TYPES = ("Blade", "Guard", "Handle", "Pommel")

# Armour reduction, per damage type: (blunt pass-through, armour threshold factor).
# Documented in docs/modding/balance-levers.md.
_ARMOUR_RULES = {
    "Cut": (0.1, 0.5),
    "Pierce": (0.25, 0.33),
    "Blunt": (0.6, 0.2),
}


class MeleeDamageError(Exception):
    """Raised when the inputs cannot describe a weapon the engine would build."""


# --- data model ---------------------------------------------------------------------------


@dataclass(frozen=True)
class BladeData:
    """`<BladeData>` on a Blade piece: the damage factors the physics is multiplied by."""

    swing_factor: float = 0.0
    swing_type: str = ""
    thrust_factor: float = 0.0
    thrust_type: str = ""
    blade_length: float = 0.0
    physics_material: str = ""


@dataclass(frozen=True)
class Piece:
    """One `<CraftingPiece>`, with the derived physics `CraftingPiece.Deserialize` computes."""

    id: str
    piece_type: str
    weight: float = 0.0
    length: float = 0.0
    center_of_mass: float = 0.0
    inertia: float = 0.0
    distance_to_next: float = 0.0
    distance_to_previous: float = 0.0
    piece_offset: float = 0.0
    previous_piece_offset: float = 0.0
    next_piece_offset: float = 0.0
    full_scale: bool = False
    tier: int = 1
    blade: BladeData | None = None
    flags: frozenset[str] = frozenset()


@dataclass(frozen=True)
class Element:
    """A piece as used by one weapon, i.e. a piece plus its `<Piece scale_factor="N">`.

    Mirrors `WeaponDesignElement` (WeaponDesignElement.cs:16-129). The engine short-circuits
    every Scaled* accessor when the scale is exactly 100, so an unscaled piece returns its
    raw value with no float round-trip; that is reproduced here rather than multiplying by 1.0.
    """

    piece: Piece
    scale_percentage: int = 100

    @property
    def scale_factor(self) -> float:
        return self.scale_percentage * 0.01

    @property
    def is_scaled(self) -> bool:
        return self.scale_percentage != 100

    def _scaled(self, value: float) -> float:
        return value if not self.is_scaled else value * self.scale_factor

    @property
    def scaled_weight(self) -> float:
        # A full_scale piece scales as a VOLUME: the factor is cubed. (WeaponDesignElement.cs:44)
        if not self.is_scaled:
            return self.piece.weight
        f = self.scale_factor
        return self.piece.weight * (f * f * f if self.piece.full_scale else f)

    @property
    def scaled_length(self) -> float:
        return self._scaled(self.piece.length)

    @property
    def scaled_center_of_mass(self) -> float:
        return self._scaled(self.piece.center_of_mass)

    @property
    def scaled_distance_to_next(self) -> float:
        return self._scaled(self.piece.distance_to_next)

    @property
    def scaled_distance_to_previous(self) -> float:
        return self._scaled(self.piece.distance_to_previous)

    @property
    def scaled_piece_offset(self) -> float:
        return self._scaled(self.piece.piece_offset)

    @property
    def scaled_previous_piece_offset(self) -> float:
        return self._scaled(self.piece.previous_piece_offset)

    @property
    def scaled_next_piece_offset(self) -> float:
        return self._scaled(self.piece.next_piece_offset)


@dataclass(frozen=True)
class WeaponFlags:
    """The resolved flag set for one weapon usage.

    `GenerateCraftedItem` ORs the primary `WeaponDescription`'s flags with the design's own
    accumulated piece flags (`Crafting.cs:601`), so build this from both, not the description
    alone.
    """

    values: frozenset[str] = frozenset()

    @property
    def is_melee(self) -> bool:
        return "MeleeWeapon" in self.values

    @property
    def two_handed(self) -> bool:
        # HasAllFlags(MeleeWeapon | NotUsableWithOneHand) -- both, not either.
        return self.is_melee and "NotUsableWithOneHand" in self.values

    @property
    def wide_grip(self) -> bool:
        return "WideGrip" in self.values

    @property
    def melee_wide_grip(self) -> bool:
        # The swing drag term tests HasAllFlags(MeleeWeapon | WideGrip). (Crafting.cs:526)
        return self.is_melee and self.wide_grip


@dataclass
class WeaponStats:
    """What the engine would show on the inventory screen, plus the physics behind it."""

    weight: float = 0.0
    reach: float = 0.0
    center_of_mass: float = 0.0
    inertia: float = 0.0
    inertia_around_shoulder: float = 0.0
    inertia_around_grip: float = 0.0
    raw_swing_speed: float = 0.0
    raw_thrust_speed: float = 0.0
    swing_speed: int = 0
    thrust_speed: int = 0
    swing_damage: int = 0
    thrust_damage: int = 0
    swing_type: str = ""
    thrust_type: str = ""
    swing_factor: float = 0.0
    thrust_factor: float = 0.0
    handling: int = 0
    flags: frozenset[str] = field(default_factory=frozenset)


# --- parsing ------------------------------------------------------------------------------


def _f(node, name, default=None):
    raw = node.get(name)
    if raw is None:
        return default
    try:
        return float(raw)
    except ValueError:
        return default


def parse_piece(node) -> Piece | None:
    """Turn one `<CraftingPiece>` element into a `Piece`, as `CraftingPiece.Deserialize` does."""
    pid = node.get("id")
    ptype = node.get("piece_type")
    if not pid or ptype not in PIECE_TYPES:
        return None

    weight = _f(node, "weight", 0.0) or 0.0

    # Length is either given directly, or implied by the two half-distances.
    # (CraftingPiece.cs:167-179)
    raw_length = node.get("length")
    if raw_length is not None:
        length = 0.01 * (_f(node, "length", 0.0) or 0.0)
        dist_next = length / 2.0
        dist_prev = length / 2.0
    else:
        dist_next = 0.01 * (_f(node, "distance_to_next_piece", 0.0) or 0.0)
        dist_prev = 0.01 * (_f(node, "distance_to_previous_piece", 0.0) or 0.0)
        length = dist_next + dist_prev

    # Uniform rod about its centre. (CraftingPiece.cs:180)
    inertia = (1.0 / 12.0) * weight * length * length
    com = length * (_f(node, "center_of_mass", 0.5) or 0.5)

    # full_scale defaults by piece type, not to false. (CraftingPiece.cs:202-203)
    raw_full = node.get("full_scale")
    full_scale = (raw_full == "true") if raw_full is not None else ptype in ("Guard", "Pommel")

    build = node.find("BuildData")
    piece_offset = prev_offset = next_offset = 0.0
    if build is not None:
        piece_offset = 0.01 * (_f(build, "piece_offset", 0.0) or 0.0)
        prev_offset = 0.01 * (_f(build, "previous_piece_offset", 0.0) or 0.0)
        next_offset = 0.01 * (_f(build, "next_piece_offset", 0.0) or 0.0)

    blade = None
    bd = node.find("BladeData")
    if ptype == "Blade" and bd is not None:
        swing = bd.find("Swing")
        thrust = bd.find("Thrust")
        blade = BladeData(
            swing_factor=(_f(swing, "damage_factor", 0.0) or 0.0) if swing is not None else 0.0,
            swing_type=(swing.get("damage_type") or "") if swing is not None else "",
            thrust_factor=(_f(thrust, "damage_factor", 0.0) or 0.0) if thrust is not None else 0.0,
            thrust_type=(thrust.get("damage_type") or "") if thrust is not None else "",
            blade_length=0.01 * (_f(node, "blade_length", 0.0) or (length * 100.0)),
            physics_material=bd.get("physics_material") or "",
        )

    flags_node = node.find("Flags")
    flags = (
        frozenset(f.get("name") for f in flags_node.findall("Flag") if f.get("name"))
        if flags_node is not None
        else frozenset()
    )

    tier = node.get("tier")
    return Piece(
        id=pid,
        piece_type=ptype,
        weight=weight,
        length=length,
        center_of_mass=com,
        inertia=inertia,
        distance_to_next=dist_next,
        distance_to_previous=dist_prev,
        piece_offset=piece_offset,
        previous_piece_offset=prev_offset,
        next_piece_offset=next_offset,
        full_scale=full_scale,
        tier=int(tier) if tier and tier.isdigit() else 1,
        blade=blade,
        flags=flags,
    )


def parse_crafting_pieces(path: Path | str) -> dict[str, Piece]:
    """Parse one crafting-pieces XML into `{piece id: Piece}`. Missing file -> empty dict."""
    path = Path(path)
    if not path.is_file():
        return {}
    out: dict[str, Piece] = {}
    for node in ET.parse(str(path)).getroot().iter("CraftingPiece"):
        piece = parse_piece(node)
        if piece is not None:
            out[piece.id] = piece
    return out


# --- physics ------------------------------------------------------------------------------


def parallel_axis(inertia_around_cm: float, mass: float, offset_from_cm: float) -> float:
    """I = I_cm + m*d^2. (Crafting.cs:516-519)"""
    return inertia_around_cm + mass * offset_from_cm * offset_from_cm


def strike_magnitude_swing(
    swing_speed: float,
    impact_point: float,
    weight: float,
    length: float,
    inertia: float,
    center_of_mass: float,
    extra_linear_speed: float = 0.0,
) -> float:
    """Rotational KE transferred into the target at one impact point.

    Verbatim `CombatStatCalculator.CalculateStrikeMagnitudeForSwing` (:21-36), including the
    bare `+ 0.5f` added to the transferred energy, which has no physical reading and is simply
    what the engine does.
    """
    if weight <= 0.0 or inertia <= 0.0:
        return 0.0
    r = length * impact_point - center_of_mass
    v = swing_speed * (ARM_LENGTH + center_of_mass) + extra_linear_speed
    ke_before = 0.5 * weight * v * v + 0.5 * inertia * swing_speed * swing_speed
    impulse = (v + swing_speed * r) / (1.0 / weight + r * r / inertia)
    v_after = v - impulse / weight
    w_after = swing_speed - impulse * r / inertia
    ke_after = 0.5 * weight * v_after * v_after + 0.5 * inertia * w_after * w_after
    return 0.067 * (ke_before - ke_after + 0.5)


def strike_magnitude_thrust(
    thrust_speed: float,
    weight: float,
    extra_linear_speed: float = 0.0,
    is_thrown: bool = False,
) -> float:
    """Linear KE of weapon plus arm. (`CombatStatCalculator.cs:38-51`)"""
    speed = thrust_speed + extra_linear_speed
    if speed <= 0.0:
        return 0.0
    if not is_thrown:
        weight += ARM_WEIGHT
    return 0.125 * (0.5 * weight * speed * speed)


def base_blow_magnitude_swing(
    angular_speed: float,
    reach: float,
    weight: float,
    inertia: float,
    center_of_mass: float,
    impact_point: float,
    extra_linear_speed: float = 0.0,
) -> float:
    """Best of 5 impact points sampled forward from `impact_point`. (`:64-83`)"""
    impact_point = min(impact_point, 0.93)
    if reach <= 0.0:
        return 0.0
    window = max(0.0, min(0.4 / reach, 1.0))
    best = 0.0
    for i in range(5):
        point = impact_point + (i / 4.0) * window
        if point >= 1.0:
            break
        mag = strike_magnitude_swing(
            angular_speed, point, weight, reach, inertia, center_of_mass, extra_linear_speed
        )
        best = max(best, mag)
    return best


def simulate_swing_layer(
    angle_span: float,
    usable_power: float,
    max_usable_torque: float,
    inertia: float,
    reach: float,
    melee_wide_grip: bool,
) -> tuple[float, float]:
    """One muscle layer accelerating the weapon through `angle_span`. (`Crafting.cs:521-542`)

    Returns (final speed, elapsed time). Drag rises with reach, and a wide grip pays it in
    full where a normal grip pays 30%.
    """
    if inertia <= 0.0:
        raise MeleeDamageError("swing layer needs a positive inertia")
    angle = 0.0
    speed = 0.01
    time = 0.0
    drag = 3.9 * reach * (1.0 if melee_wide_grip else 0.3)
    # The engine's loop has no iteration guard. A weapon heavy enough that drag cancels the
    # torque would spin it forever; bound it here so a malformed piece reports instead of hanging.
    for _ in range(1_000_000):
        if angle >= angle_span:
            break
        torque = usable_power / speed
        if torque > max_usable_torque:
            torque = max_usable_torque
        torque -= speed * drag
        speed += SWING_DT * torque / inertia
        angle += speed * SWING_DT
        time += SWING_DT
        if speed <= 0.0:
            raise MeleeDamageError("swing stalled: drag exceeds available torque")
    else:
        raise MeleeDamageError("swing simulation did not converge")
    return speed, time


def simulate_thrust_layer(
    distance: float, usable_power: float, max_usable_force: float, mass: float
) -> tuple[float, float]:
    """One muscle layer pushing the weapon `distance` forward. (`Crafting.cs:544-565`)"""
    if mass <= 0.0:
        raise MeleeDamageError("thrust layer needs a positive mass")
    pos = 0.0
    speed = 0.01
    time = 0.0
    for _ in range(1_000_000):
        if pos >= distance:
            break
        force = usable_power / speed
        if force > max_usable_force:
            force = max_usable_force
        speed += THRUST_DT * force / mass
        pos += speed * THRUST_DT
        time += THRUST_DT
    else:
        raise MeleeDamageError("thrust simulation did not converge")
    return speed, time


def swing_speed(inertia_around_shoulder: float, reach: float, flags: WeaponFlags) -> float:
    """Angular swing speed from three muscle layers. (`Crafting.cs:296-331`)"""
    i = 1.0 * inertia_around_shoulder + 0.9
    power1, power2 = 170.0, 90.0
    torque1, torque2, torque3 = 27.0, 15.0, 7.0

    if flags.two_handed:
        # The second hand buys torque, and a wide grip buys more of it.
        if flags.wide_grip:
            i += 1.5
            torque3 *= 4.0
            torque2 *= 1.7
            power2 *= 1.3
            power1 *= 1.15
        else:
            i += 1.0
            torque3 *= 2.4
            torque2 *= 1.3
            power2 *= 1.35
            power1 *= 1.15

    torque1 = max(1.0, torque1 - i)
    torque2 = max(1.0, torque2 - i)
    torque3 = max(1.0, torque3 - i)

    wg = flags.melee_wide_grip
    _, t1 = simulate_swing_layer(1.5, 200.0, torque1, 2.0 + i, reach, wg)
    _, t2 = simulate_swing_layer(1.5, power1, torque2, 1.0 + i, reach, wg)
    _, t3 = simulate_swing_layer(1.5, power2, torque3, 0.5 + i, reach, wg)
    return SWING_TIME_CONST / (0.33 * (t1 + t2 + t3))


def thrust_speed(weight: float, inertia_around_grip: float, flags: WeaponFlags) -> float:
    """Linear thrust speed from three muscle layers. (`Crafting.cs:333-361`)"""
    mass = 1.8 + weight + inertia_around_grip * 0.2
    power1, power2 = 170.0, 90.0
    force1, force2 = 24.0, 15.0

    if flags.two_handed and not flags.wide_grip:
        mass += 0.6
        force2 *= 1.9
        force1 *= 1.1
        power2 *= 1.2
        power1 *= 1.05
    elif flags.two_handed and flags.wide_grip:
        mass += 0.9
        force2 *= 2.1
        force1 *= 1.2
        power2 *= 1.2
        power1 *= 1.05

    _, t1 = simulate_thrust_layer(0.6, 250.0, 48.0, 4.0 + mass)
    _, t2 = simulate_thrust_layer(0.6, power1, force1, 2.0 + mass)
    _, t3 = simulate_thrust_layer(0.6, power2, force2, 0.5 + mass)
    return THRUST_TIME_CONST / (0.33 * (t1 + t2 + t3))


# --- assembly -----------------------------------------------------------------------------


def pivot_distances(
    elements: dict[str, Element], build_order: list[tuple[str, int]]
) -> tuple[dict[str, float], float, float]:
    """Place the pieces along the weapon axis.

    Verbatim `WeaponDesign.CalculatePivotDistances` + `CalculateWeaponLength`
    (WeaponDesign.cs:151-290). `bottom` runs toward the pommel, `top` toward the tip; the
    handle (order 0) is the anchor at the grip.

    Returns (pivot per piece type, crafted weapon length, hand-to-bottom length). The length
    is what the engine uses as weapon REACH, which is not the sum of the piece lengths, and
    the difference propagates into swing drag, the impact window and the lever arm.

    TAOM has a second, independent port of this in C# at
    `tools/BannerlordCraftingTool/MainWindow.xaml.cs:457-524`; the two must agree.
    """
    pivots: dict[str, float] = {}
    bottom = 0.0
    top = 0.0

    for piece_type, order in build_order:
        el = elements.get(piece_type)
        if el is None:
            pivots[piece_type] = math.nan
            continue

        sign = (order > 0) - (order < 0)
        if sign == 0:
            top += el.scaled_piece_offset
            bottom -= el.scaled_piece_offset
        elif sign < 0:
            bottom += el.scaled_distance_to_next
            bottom += el.scaled_piece_offset
            bottom -= el.scaled_next_piece_offset
        else:
            top += el.scaled_distance_to_previous
            top += el.scaled_piece_offset
            top -= el.scaled_previous_piece_offset

        pivots[piece_type] = sign * (bottom if sign < 0 else top) + (
            el.scaled_piece_offset if sign == 0 else 0.0
        )

        if sign == 0:
            bottom += el.scaled_distance_to_previous - el.scaled_previous_piece_offset
            top += el.scaled_distance_to_next - el.scaled_next_piece_offset
        elif sign < 0:
            bottom += el.scaled_distance_to_previous - el.scaled_previous_piece_offset
        else:
            top += el.scaled_distance_to_next - el.scaled_next_piece_offset

    length = math.nan
    blade = elements.get("Blade")
    blade_pivot = pivots.get("Blade", math.nan)
    if blade is not None and not math.isnan(blade_pivot):
        a = blade_pivot + blade.scaled_distance_to_next
        # Note the engine's own quirk: the running max is compared against
        # `ScaledDistanceToNextPiece` alone but ASSIGNED `+ ScaledPieceOffset`.
        # (WeaponDesign.cs CalculateWeaponLength)
        m = 0.0
        for el in elements.values():
            if el.scaled_distance_to_next > m:
                m = el.scaled_distance_to_next + el.scaled_piece_offset
        length = max(a, m)

    return pivots, length, bottom


def center_of_mass(
    elements: dict[str, Element], build_order: list[tuple[str, int]], total_weight: float
) -> float:
    """Weighted centre of mass relative to the grip. (`Crafting.cs:247-276`)"""
    if total_weight <= 0.0:
        raise MeleeDamageError("cannot take a centre of mass of a weightless weapon")
    moment = 0.0
    forward = 0.0
    backward = 0.0
    for piece_type, order in build_order:
        el = elements.get(piece_type)
        if el is None:
            continue
        w = el.scaled_weight
        if order < 0:
            moment -= (backward + (el.scaled_length - el.scaled_center_of_mass)) * w
            backward += el.scaled_length
        else:
            moment += (forward + el.scaled_center_of_mass) * w
            forward += el.scaled_length

    # The grip correction reads the HANDLE specifically (UsedPieces[2]), not the anchor piece.
    handle = elements.get("Handle")
    correction = (
        (handle.scaled_distance_to_previous - handle.scaled_piece_offset)
        if handle is not None
        else 0.0
    )
    return moment / total_weight - correction


def weapon_inertia(
    elements: dict[str, Element], build_order: list[tuple[str, int]], com: float
) -> float:
    """Total inertia about the centre of mass. (`Crafting.cs:278-293`)

    Preserves both engine quirks: unscaled piece inertia/CoM against a scaled mass, and an
    offset that advances forward for every piece whatever its build-order sign.
    """
    offset = -com
    total = 0.0
    for piece_type, _order in build_order:
        el = elements.get(piece_type)
        if el is None:
            continue
        total += parallel_axis(
            el.piece.inertia, el.scaled_weight, offset + el.piece.center_of_mass
        )
        offset += el.scaled_length
    return total


def compute(
    elements: dict[str, Element],
    build_order: list[tuple[str, int]],
    flags: WeaponFlags,
) -> WeaponStats:
    """Price one assembled weapon. Mirrors `CraftingStats.SetStats` (`Crafting.cs:112-147`)."""
    blade_el = elements.get("Blade")
    if blade_el is None or blade_el.piece.blade is None:
        raise MeleeDamageError("a crafted weapon needs a Blade piece carrying <BladeData>")

    weight = round(sum(el.scaled_weight for el in elements.values()), 2)
    if weight <= 0.0:
        raise MeleeDamageError("assembled weapon has no weight")

    _pivots, length, _bottom = pivot_distances(elements, build_order)
    if math.isnan(length):
        raise MeleeDamageError("could not resolve a crafted weapon length")
    reach = round(length, 2)

    com = center_of_mass(elements, build_order, weight)
    inertia = weapon_inertia(elements, build_order, com)
    if inertia <= 0.0:
        raise MeleeDamageError("assembled weapon has no inertia")

    shoulder = parallel_axis(inertia, weight, ARM_LENGTH + com)
    grip = parallel_axis(inertia, weight, com)

    raw_swing = swing_speed(shoulder, reach, flags)
    raw_thrust = thrust_speed(weight, grip, flags)

    blade = blade_el.piece.blade

    # Swing damage sweeps the impact point back from the sweet spot and keeps the best.
    best = 0.0
    point = 0.93
    while point > 0.5:
        best = max(best, base_blow_magnitude_swing(raw_swing, reach, weight, inertia, com, point))
        point -= 0.05
    swing_dmg = best * blade.swing_factor
    thrust_dmg = strike_magnitude_thrust(raw_thrust, weight) * blade.thrust_factor

    return WeaponStats(
        weight=weight,
        reach=reach,
        center_of_mass=com,
        inertia=inertia,
        inertia_around_shoulder=shoulder,
        inertia_around_grip=grip,
        raw_swing_speed=raw_swing,
        raw_thrust_speed=raw_thrust,
        # The engine floors the display speeds and rounds the display damages.
        swing_speed=math.floor(raw_swing * SWING_SPEED_CONST),
        thrust_speed=math.floor(raw_thrust * THRUST_SPEED_CONST),
        swing_damage=round(swing_dmg),
        thrust_damage=round(thrust_dmg),
        swing_type=blade.swing_type,
        thrust_type=blade.thrust_type,
        swing_factor=blade.swing_factor,
        thrust_factor=blade.thrust_factor,
        handling=math.floor(raw_thrust * THRUST_SPEED_CONST),
        flags=flags.values,
    )


# --- sustained damage -----------------------------------------------------------------------


def swing_cycle_time(raw_swing_speed: float, stun: float = STUN_PERIOD_SWING) -> float:
    """Seconds per swing: the simulated swing itself, plus the attacker's recovery.

    `CalculateSwingSpeed` returns `SWING_TIME_CONST / simulated_time` (Crafting.cs:330), so the
    time inverts straight back out of the speed. That inversion is exact by construction, which
    is what makes the RATIO between two weapons trustworthy: a weapon with twice the speed stat
    takes half as long to swing, whatever the engine's animation system does with the number.

    The ABSOLUTE seconds are a model. They assume the native attack animation runs for the time
    the muscle simulation produced, which is plausible (it yields roughly 1.0 to 1.7 s per swing)
    but is not established anywhere in managed code. Treat the output as a relative index.
    """
    if raw_swing_speed <= 0.0:
        raise MeleeDamageError("cannot take a cycle time of a non-positive swing speed")
    return SWING_TIME_CONST / raw_swing_speed + stun


def thrust_cycle_time(raw_thrust_speed: float, stun: float = STUN_PERIOD_THRUST) -> float:
    """Seconds per thrust. Same construction, and the same caveat on the absolute scale.

    The recovery term dominates here in a way it never does for a swing: 0.67 s of stun against
    a thrust that itself takes about 0.5 s.
    """
    if raw_thrust_speed <= 0.0:
        raise MeleeDamageError("cannot take a cycle time of a non-positive thrust speed")
    return THRUST_TIME_CONST / raw_thrust_speed + stun


def sustained_damage(damage: float, cycle_time: float) -> float:
    """Damage per second: one blow's damage over one full attack cycle.

    Why this is not double-counting speed. Swing damage already rises with speed, because the
    blow is kinetic energy and energy goes as v squared (`strike_magnitude_swing`). Speed then
    helps a SECOND time by shortening the cycle. Both are real and both are in the game, so a
    fast weapon genuinely compounds: it hits harder AND more often. Raw damage alone understates
    it, which is the whole reason this function exists.

    What it deliberately leaves out, because none of it is a property of the weapon: the
    agent's `SwingSpeedMultiplier` (skill and passives scale every weapon alike, so it cancels
    in a comparison), where in the swing arc the blow lands (`SpeedGraphFunction`), movement
    speed bonuses, and the target's armour. For armour, compose with `armour_reduction`.
    """
    if cycle_time <= 0.0:
        raise MeleeDamageError("cycle time must be positive")
    return max(0.0, damage) / cycle_time


# --- combat -------------------------------------------------------------------------------


def armour_reduction(damage_type: str, magnitude: float, armour: float) -> float:
    """Damage that survives `armour` points. Documented in docs/modding/balance-levers.md.

    Cut passes only 10% as blunt and is checked against half the armour, so it falls off
    fastest; blunt passes 60% and is checked against a fifth, so it barely cares.
    """
    if magnitude <= 0.0:
        return 0.0
    blunt_factor, threshold = _ARMOUR_RULES.get(damage_type, _ARMOUR_RULES["Cut"])
    armour = max(0.0, armour)
    scaled = magnitude * 50.0 / (50.0 + armour)
    subtracted = max(0.0, scaled - armour * threshold)
    return blunt_factor * scaled + (1.0 - blunt_factor) * subtracted
