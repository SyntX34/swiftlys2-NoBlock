#nullable disable
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Scheduler;

namespace NoBlock;

[PluginMetadata(
    Id          = "NoBlock",
    Version     = "1.0.0",
    Name        = "NoBlock",
    Author      = "+SyntX34",
    Description = "Global noblock for X seconds on command + grenade noblock + ladder support."
)]
public partial class NoBlock : BasePlugin
{
    private IOptionsMonitor<NoBlockConfig> _configMonitor;
    private NoBlockConfig Config => _configMonitor?.CurrentValue ?? new NoBlockConfig();

    private DateTime _noBlockExpiry  = DateTime.MinValue;
    private DateTime _cooldownExpiry = DateTime.MinValue;

    private bool NoBlockActive  => DateTime.UtcNow < _noBlockExpiry;
    private bool CooldownActive => DateTime.UtcNow < _cooldownExpiry;

    private bool _expiryCheckedThisTick;
    private bool _wasActive;

    private readonly Dictionary<ulong, bool> _wasOnLadder = new();

    public NoBlock(ISwiftlyCore core) : base(core) { }

    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<NoBlockConfig>("config.jsonc", "NoBlock").Configure(builder =>
            builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true));

        var services = new ServiceCollection();
        services.AddSwiftly(Core)
                .AddOptionsWithValidateOnStart<NoBlockConfig>()
                .BindConfiguration("NoBlock");

        _configMonitor = services.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<NoBlockConfig>>();

        Core.Event.OnEntitySpawned      += OnEntitySpawned;
        Core.Event.OnPlayerPawnPostThink += OnPlayerPawnPostThink;

        Core.Logger.LogInformation("[NoBlock] Loaded.");
    }

    public override void Unload()
    {
        Core.Event.OnEntitySpawned      -= OnEntitySpawned;
        Core.Event.OnPlayerPawnPostThink -= OnPlayerPawnPostThink;

        Core.Scheduler.NextTick(ResetAll);

        _wasOnLadder.Clear();
        _noBlockExpiry  = DateTime.MinValue;
        _cooldownExpiry = DateTime.MinValue;
        _wasActive      = false;

        Core.Logger.LogInformation("[NoBlock] Unloaded.");
    }

    [EventListener<EventDelegates.OnEntitySpawned>]
    public void OnEntitySpawned(IOnEntitySpawnedEvent @event)
    {
        var entity = @event.Entity;
        if (entity is not CBaseEntity baseEntity) return;

        var name = entity.DesignerName;

        if (IsGrenadeEnabled(name))
        {
            Core.Scheduler.NextTick(() =>
            {
                if (!baseEntity.IsValid) return;
                baseEntity.Collision.CollisionGroup = (byte)CollisionGroup.Debris;
                baseEntity.CollisionRulesChanged();
            });
        }
    }

    private bool IsGrenadeEnabled(string name) => name switch
    {
        "hegrenade_projectile"    => Config.HEGrenade,
        "flashbang_projectile"    => Config.Flashbang,
        "smokegrenade_projectile" => Config.SmokeGrenade,
        "molotov_projectile"      => Config.Molotov,
        "incendiary_projectile"   => Config.Molotov,
        "decoy_projectile"        => Config.Decoy,
        _ => false
    };

    [EventListener<EventDelegates.OnPlayerPawnPostThink>]
    public void OnPlayerPawnPostThink(IOnPlayerPawnPostThinkHookEvent @event)
    {
        var pawn = @event.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;

        if (!_expiryCheckedThisTick)
        {
            _expiryCheckedThisTick = true;
            Core.Scheduler.NextWorldUpdate(() => _expiryCheckedThisTick = false);

            bool nowActive = NoBlockActive;

            if (_wasActive && !nowActive)
            {
                _wasActive = false;
                OnNoBlockExpired();
            }
            else if (!_wasActive && nowActive)
            {
                _wasActive = true;
                ApplyNoblocksAll();
            }
        }

        if (!Config.Ladder) return;

        var controllerHandle = pawn.Controller;
        if (!controllerHandle.IsValid) return;

        var controller = controllerHandle.Value;
        if (controller == null) return;

        var player = Core.PlayerManager.GetPlayerFromController(controller);
        if (player == null || !player.IsValid) return;

        var steamId   = player.SteamID;
        bool onLadder = pawn.MoveType == MoveType_t.MOVETYPE_LADDER;

        _wasOnLadder.TryGetValue(steamId, out bool wasOnLadder);

        if (onLadder && !wasOnLadder)
        {
            pawn.Collision.CollisionGroup = (byte)CollisionGroup.Debris;
            pawn.CollisionRulesChanged();
        }
        else if (!onLadder && wasOnLadder && !NoBlockActive)
        {
            pawn.Collision.CollisionGroup = (byte)CollisionGroup.Player;
            pawn.CollisionRulesChanged();
        }

        _wasOnLadder[steamId] = onLadder;
    }

    [Command("noblock")]
    public void OnNoBlockCommand(ICommandContext ctx)
    {
        if (!ctx.IsSentByPlayer || ctx.Sender == null) return;

        var player = ctx.Sender;

        if (CooldownActive)
        {
            int secsLeft = (int)Math.Ceiling((_cooldownExpiry - DateTime.UtcNow).TotalSeconds);
            SendLocalizedChat(player, "noblock.cooldown", secsLeft);
            return;
        }

        if (NoBlockActive)
        {
            SendLocalizedChat(player, "noblock.already_active");
            return;
        }

        if (!player.IsAlive) return;

        ActivateGlobalNoBlock(player);
    }

    private void ActivateGlobalNoBlock(IPlayer triggerer)
    {
        float duration = Config.NoBlockTimer;
        _noBlockExpiry = DateTime.UtcNow.AddSeconds(duration);

        SendLocalizedChat(triggerer, "noblock.activated", (int)duration);
        Core.Logger.LogDebug("[NoBlock] {0} triggered global noblock for {1}s.", triggerer.Name, duration);
    }

    private void OnNoBlockExpired()
    {
        _cooldownExpiry = DateTime.UtcNow.AddSeconds(Config.NoBlockCooldownTimer);

        Core.Logger.LogDebug("[NoBlock] Expired. Cooldown {0}s.", Config.NoBlockCooldownTimer);

        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (p?.IsValid != true || !p.IsAlive) continue;

            var pawn = p.PlayerPawn;
            if (pawn == null || !pawn.IsValid) continue;

            bool onLadder = Config.Ladder && pawn.MoveType == MoveType_t.MOVETYPE_LADDER;
            if (onLadder) continue;

            pawn.Collision.CollisionGroup = (byte)CollisionGroup.Player;
            pawn.CollisionRulesChanged();
        }

        foreach (var weapon in Core.EntitySystem.GetAllEntitiesByClass<CCSWeaponBase>())
        {
            if (!weapon.IsValid) continue;
            if (weapon.Collision.CollisionGroup == (byte)CollisionGroup.Weapon) continue;
            weapon.Collision.CollisionGroup = (byte)CollisionGroup.Weapon;
            weapon.CollisionRulesChanged();
        }
    }

    private void ApplyNoblocksAll()
    {
        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (p?.IsValid != true || !p.IsAlive) continue;

            var pawn = p.PlayerPawn;
            if (pawn == null || !pawn.IsValid) continue;

            pawn.Collision.CollisionGroup = (byte)CollisionGroup.Debris;
            pawn.CollisionRulesChanged();

            var inventory = pawn.WeaponServices?.MyWeapons;
            if (inventory == null) continue;

            foreach (var handle in inventory)
            {
                if (!handle.IsValid) continue;
                var w = handle.Value;
                if (w == null || !w.IsValid) continue;
                if (w.Collision.CollisionGroup == (byte)CollisionGroup.Debris) continue;
                w.Collision.CollisionGroup = (byte)CollisionGroup.Debris;
                w.CollisionRulesChanged();
            }
        }

        foreach (var weapon in Core.EntitySystem.GetAllEntitiesByClass<CCSWeaponBase>())
        {
            if (!weapon.IsValid) continue;
            if (weapon.Collision.CollisionGroup == (byte)CollisionGroup.Debris) continue;
            weapon.Collision.CollisionGroup = (byte)CollisionGroup.Debris;
            weapon.CollisionRulesChanged();
        }
    }

    private void ResetAll()
    {
        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (p?.IsValid != true) continue;
            var pawn = p.PlayerPawn;
            if (pawn == null || !pawn.IsValid) continue;
            pawn.Collision.CollisionGroup = (byte)CollisionGroup.Player;
            pawn.CollisionRulesChanged();
        }

        foreach (var weapon in Core.EntitySystem.GetAllEntitiesByClass<CCSWeaponBase>())
        {
            if (!weapon.IsValid) continue;
            weapon.Collision.CollisionGroup = (byte)CollisionGroup.Weapon;
            weapon.CollisionRulesChanged();
        }
    }

    private void SendLocalizedChat(IPlayer player, string key, params object[] args)
    {
        if (player.IsFakeClient) return;
        var loc = Core.Translation.GetPlayerLocalizer(player);
        player.SendChat(Config.ChatPrefix + loc[key, args]);
    }
}