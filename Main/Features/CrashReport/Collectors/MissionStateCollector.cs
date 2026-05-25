using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

public sealed class MissionStateCollector
{
    public MissionSnapshot? Collect()
    {
        Mission? m;
        try { m = Mission.Current; } catch { return null; }
        if (m == null) return null;

        string? scene = SafeStr(() => m.SceneName);
        string? mode = SafeStr(() => m.Mode.ToString());
        float? timeOfDay = null; try { timeOfDay = m.Scene?.TimeOfDay; } catch { }
        // Weather isn't exposed on Scene publicly in v1.4.5 — leave null.
        string? weather = null;

        var teams = new List<TeamSnapshot>();
        try
        {
            var teamList = m.Teams?.ToList() ?? new List<Team>();
            foreach (var t in teamList)
            {
                int idx = SafeIntNonNull(() => t.TeamIndex);
                string side = SafeStr(() => t.Side.ToString()) ?? "?";
                int alive = SafeIntNonNull(() => t.ActiveAgents?.Count);
                int total = SafeIntNonNull(() => t.TeamAgents?.Count);
                bool isPlayerTeam = false;
                try { isPlayerTeam = t == m.PlayerTeam; } catch { }
                teams.Add(new TeamSnapshot(idx, side, alive, total, isPlayerTeam));
            }
        }
        catch { }

        var playerFormations = new List<FormationSnapshot>();
        try
        {
            var pt = m.PlayerTeam;
            if (pt != null)
            {
                foreach (var f in pt.FormationsIncludingEmpty ?? Enumerable.Empty<Formation>())
                {
                    if (f == null) continue;
                    string cls = SafeStr(() => f.FormationIndex.ToString()) ?? "?";
                    int alive = SafeIntNonNull(() => f.CountOfUnits);
                    int total = alive; // CountOfUnits is the only public unit-count surface on v1.4.5 Formation.
                    string? mov = SafeMovementOrderEnum(f);
                    string? fac = SafeStr(() => f.FacingOrder.OrderEnum.ToString());
                    string? arr = SafeStr(() => f.ArrangementOrder.OrderEnum.ToString());
                    playerFormations.Add(new FormationSnapshot(cls, alive, total, mov, fac, arr));
                }
            }
        }
        catch { }

        PlayerAgentSnapshot? agent = null;
        try
        {
            var a = m.MainAgent;
            if (a != null)
            {
                float health = 0f, maxHealth = 0f, px = 0f, py = 0f, pz = 0f;
                bool mounted = false; string? wielded = null;
                try { health = a.Health; } catch { }
                try { maxHealth = a.HealthLimit; } catch { }
                try { var p = a.Position; px = p.X; py = p.Y; pz = p.Z; } catch { }
                try { mounted = a.HasMount; } catch { }
                // WieldedWeapon is the cleanest read-only surface for "what's the player holding";
                // GetPrimaryWieldedItemIndex requires us to index into Equipment which can throw.
                try { wielded = a.WieldedWeapon.IsEmpty ? null : a.WieldedWeapon.Item?.StringId; } catch { }
                agent = new PlayerAgentSnapshot(health, maxHealth, px, py, pz, mounted, wielded);
            }
        }
        catch { }

        return new MissionSnapshot(scene, mode, timeOfDay, weather, teams, playerFormations, agent);
    }

    private static string? SafeStr(Func<string?> f) { try { return f(); } catch { return null; } }
    private static int SafeIntNonNull(Func<int?> f) { try { return f() ?? 0; } catch { return 0; } }

    // Helper for the ref-readonly MovementOrder access pattern — v1.4.5 Formation
    // hides _movementOrder behind GetReadonlyMovementOrderReference().
    private static string? SafeMovementOrderEnum(Formation f)
    {
        try { return f.GetReadonlyMovementOrderReference().OrderEnum.ToString(); }
        catch { return null; }
    }
}
