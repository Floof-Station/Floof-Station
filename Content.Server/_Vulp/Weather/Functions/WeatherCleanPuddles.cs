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


[DataDefinition, Serializable]
public sealed partial class WeatherCleanPuddles : WeatherFunction
{
    [DataField(required: true)]
    public float CleanChance;

    [DataField(required: true)]
    public int MaxCleaned;

    [DataField(required: true)]
    public FixedPoint2 CleanAmount;

    [DataField]
    public ProtoId<ReagentPrototype> CleanReagent = "Water";

    public override void Invoke(EntityManager entMan, Entity<WeatherComponent> ent, float updateTimeSeconds)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var solutions = entMan.System<SharedSolutionContainerSystem>();
        var maps = entMan.System<SharedMapSystem>();
        var tileMan = IoCManager.Resolve<ITileDefinitionManager>();

        var query = entMan.EntityQueryEnumerator<PuddleComponent, TransformComponent>();
        var gridQuery = entMan.GetEntityQuery<MapGridComponent>();
        var cleaned = 0;
        var temperature = entMan.TryGetComponent(ent, out MapAtmosphereComponent? atmos) ? atmos.Mixture.Temperature : 293;

        while (query.MoveNext(out var uid, out var puddle, out var xform))
        {
            if (xform.MapUid != ent)
                continue;

            if (!gridQuery.TryComp(xform.MapUid, out var grid) && !gridQuery.TryComp(xform.GridUid, out grid))
                continue;

            var tile = maps.GetTileRef((ent.Owner, grid), xform.Coordinates);
            var tileDef = (ContentTileDefinition) tileMan[tile.Tile.TypeId];

            // Chance is not multplied because the amount of cleaning is
            if (!tileDef.Weather || !random.Prob(CleanChance))
                continue;

            var solution = puddle.Solution;
            if (solution == null)
                continue;

            var excess = solutions.SplitSolutionWithout(solution.Value, CleanAmount * updateTimeSeconds, CleanReagent);
            solutions.TryAddReagent(solution.Value, CleanReagent, excess.Volume, temperature);

            if (++cleaned >= MaxCleaned * updateTimeSeconds)
                break;
        }
    }
}
