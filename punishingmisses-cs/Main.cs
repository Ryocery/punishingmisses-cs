using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Microsoft.Extensions.Logging;

namespace punishingmisses_cs;

[MinimumApiVersion(80)]
public class Main : BasePlugin {
    public override string ModuleName => "PunishingMisses";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Ryocery";
    public override string ModuleDescription => "Makes it so you take damage equal to your gun upon missing a shot.";

    private readonly Dictionary<int, ShotInfo> _recentShots = new();

    public override void Load(bool hotReload) {
        base.Load(hotReload);
        
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventBulletImpact>(OnBulletImpact);
    
        if (hotReload) {
            Logger.LogInformation("Plugin hot reloaded successfully!");
        }
    }
    
    public override void Unload(bool hotReload) {
        base.Unload(hotReload);
    
        if (hotReload) {
            Logger.LogInformation("Plugin unloaded for hot reload.");
        }
    }
    
    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info) {
        CCSPlayerController? player = @event.Userid;
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null || player.UserId == null) return HookResult.Continue;
        
        CBasePlayerWeapon? activeWeapon = player.PlayerPawn.Value.WeaponServices?.ActiveWeapon.Value;
        if (activeWeapon == null) return HookResult.Continue;
        
        CCSWeaponBase? weapon = activeWeapon as CCSWeaponBase;
        if (weapon == null) return HookResult.Continue;
        
        int weaponDamage = GetWeaponDamage(weapon);
        
        _recentShots[(int) player.UserId] = new ShotInfo {
            TimeStamp = DateTime.Now,
            Damage = weaponDamage,
            DidHit = false
        };
        
        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info) {
        CCSPlayerController? attacker = @event.Attacker;
        if (attacker == null || !attacker.IsValid || attacker.UserId == null) return HookResult.Continue;
        
        if (_recentShots.ContainsKey((int) attacker.UserId)) {
            _recentShots[(int) attacker.UserId].DidHit = true;
        }
        
        return HookResult.Continue;
    }

    private HookResult OnBulletImpact(EventBulletImpact @event, GameEventInfo info) {
        CCSPlayerController? player = @event.Userid;
        if (player == null || !player.IsValid || player.UserId == null) return HookResult.Continue;
        
        if (_recentShots.ContainsKey((int) player.UserId)) {
            ShotInfo? shot = _recentShots[(int) player.UserId];
            
            if ((DateTime.Now - shot.TimeStamp).TotalMilliseconds > 50 && !shot.DidHit) {
                ApplyMissPenalty(player, shot.Damage);
                _recentShots.Remove((int) player.UserId);
            }
        }
        
        return HookResult.Continue;
    }
    
    private void ApplyMissPenalty(CCSPlayerController player, int damage) {
        if (player.PlayerPawn.Value == null) return;
        
        player.PlayerPawn.Value.Health -= damage;
        
        if (player.PlayerPawn.Value.Health <= 0) {
            player.CommitSuicide(false, true);
        }
        
        Logger.LogInformation("{PlayerPlayerName} missed and took {Damage} damage!", player.PlayerName, damage);
    }

    private int GetWeaponDamage(CCSWeaponBase weapon) {
        CCSWeaponBaseVData? vData = weapon.VData;
        return vData?.Damage ?? 25;
    }
}