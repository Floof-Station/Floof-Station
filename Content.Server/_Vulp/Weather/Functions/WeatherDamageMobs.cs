using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Vulp.Weather;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Content.Shared.Weather;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;


namespace Content.Server._Vulp.Weather.Functions;


/// <summary>
///     Damages all mobs on the affected maps. Only affects mobs standing on weathered tiles.
/// </summary>
[DataDefinition, Serializable]
public sealed partial class WeatherDamageMobs : WeatherFunction
{
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    [DataField]
    public float Chance = 1f;

    /// <summary>
    ///     Whether to ignore NPCs, because those dummies won't seek shelter.
    /// </summary>
    [DataField]
    public bool IgnoreNpcs = true;

    /// <summary>
    ///     Whether to ignore entities that are using internals.
    /// </summary>
    [DataField]
    public bool IgnoreInternalBreathers = false;

    public override void Invoke(EntityManager entMan, Entity<WeatherComponent> ent, float updateTimeSeconds)
    {
        var query = entMan.EntityQueryEnumerator<MobStateComponent, DamageableComponent, TransformComponent>();
        var npcQuery = entMan.GetEntityQuery<ActiveNPCComponent>();
        var internalQuery = entMan.GetEntityQuery<InternalsComponent>();
        var gridQuery = entMan.GetEntityQuery<MapGridComponent>();

        var random = IoCManager.Resolve<IRobustRandom>();
        var maps = entMan.System<SharedMapSystem>();
        var tileMan = IoCManager.Resolve<ITileDefinitionManager>();
        var damageSystem = entMan.System<DamageableSystem>();
        var internalSystem = entMan.System<InternalsSystem>();

        while (query.MoveNext(out var uid, out var mobState, out var damageable, out var xform))
        {
            if (xform.MapUid != ent
                || IgnoreNpcs && npcQuery.HasComp(uid)
                || IgnoreInternalBreathers && internalQuery.TryComp(uid, out var internals) && internalSystem.AreInternalsWorking(internals)
                || !gridQuery.TryComp(xform.MapUid, out var grid) && !gridQuery.TryComp(xform.GridUid, out grid))
                continue;

            var tile = maps.GetTileRef((ent.Owner, grid), xform.Coordinates);
            var tileDef = (ContentTileDefinition) tileMan[tile.Tile.TypeId];

            if (!tileDef.Weather || !random.Prob(Chance * updateTimeSeconds))
                continue;

            var resultingDamage = Damage * updateTimeSeconds;
            damageSystem.TryChangeDamage(
                uid,
                resultingDamage,
                false,
                false,
                damageable,
                null,
                false,
                false,
                2f); // TODO: what is a good value for partMultiplier?
        }
    }
}
