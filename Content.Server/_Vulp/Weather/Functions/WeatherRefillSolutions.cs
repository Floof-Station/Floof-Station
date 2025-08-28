using Content.Server.Atmos.Components;
using Content.Shared._Vulp.Weather;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Maps;
using Content.Shared.Weather;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;


namespace Content.Server._Vulp.Weather.Functions;


// Similar to WeatherCleanPuddles, but doesn't remove existing reagents and is not limited to puddles
[DataDefinition, Serializable]
public sealed partial class WeatherRefillSolutions : WeatherFunction
{
    [DataField(required: true)]
    public float RefillChance;

    [DataField(required: true)]
    public int MaxRefilled;

    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    public override void Invoke(EntityManager entMan, Entity<WeatherComponent> ent, float updateTimeSeconds)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var solutions = entMan.System<SharedSolutionContainerSystem>();
        var maps = entMan.System<SharedMapSystem>();
        var tileMan = IoCManager.Resolve<ITileDefinitionManager>();

        var query = entMan.EntityQueryEnumerator<RefillableSolutionComponent, TransformComponent>();
        var gridQuery = entMan.GetEntityQuery<MapGridComponent>();
        var refilled = 0;
        var temperature = entMan.TryGetComponent(ent, out MapAtmosphereComponent? atmos) ? atmos.Mixture.Temperature : 293;

        while (query.MoveNext(out var uid, out var refillable, out var xform))
        {
            if (xform.MapUid != ent)
                continue;

            if (!gridQuery.TryComp(xform.MapUid, out var grid) && !gridQuery.TryComp(xform.GridUid, out grid))
                continue;

            var tile = maps.GetTileRef((ent.Owner, grid), xform.Coordinates);
            var tileDef = (ContentTileDefinition) tileMan[tile.Tile.TypeId];

            if (!tileDef.Weather
                || !random.Prob(RefillChance) // Chance is not multplied because the refill amount is.
                || !solutions.TryGetSolution(uid, refillable.Solution, out var solutionEnt))
                continue;

            var sol = solutionEnt.Value.Comp.Solution;
            if (sol.AvailableVolume <= FixedPoint2.Zero)
                continue;

            solutions.TryAddReagent(solutionEnt.Value, Reagent, Amount * updateTimeSeconds, temperature);
            if (++refilled >= MaxRefilled * updateTimeSeconds)
                break;
        }
    }
}
