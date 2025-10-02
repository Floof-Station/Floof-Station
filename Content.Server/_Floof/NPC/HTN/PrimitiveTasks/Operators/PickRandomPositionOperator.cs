using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Random;


namespace Content.Server._Floof.NPC.HTN.PrimitiveTasks.Operators;


using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;


/// <summary>
/// Chooses a nearby coordinate and puts it into the resulting key.
/// </summary>
public sealed partial class PickRandomPositionOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    private TransformSystem _xforms = default!;
    private SharedMapSystem _maps = default!;

    [DataField("rangeKey", required: true)]
    public string RangeKey = string.Empty;

    [DataField("targetCoordinates")]
    public string TargetCoordinates = "TargetCoordinates";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _xforms = sysManager.GetEntitySystem<TransformSystem>();
        _maps = sysManager.GetEntitySystem<SharedMapSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        blackboard.TryGetValue<float>(RangeKey, out var maxRange, _entManager);

        if (maxRange == default)
            maxRange = 10f;

        var xform = _entManager.GetComponent<TransformComponent>(owner);
        var currentGrid = xform.GridUid;
        var currentMap = xform.MapUid;

        MapGridComponent? grid = null;
        if (currentMap is not {Valid: true} || currentGrid is not null && !_entManager.TryGetComponent<MapGridComponent>(currentGrid, out grid))
            return (false, null);

        // Make 5 attempts to move to a point on the same grid. If it fails, stay where we are.
        for (int i = 0; i < 5; i++)
        {
            var offset = _random.NextVector2(maxRange);
            var target = xform.Coordinates.Offset(offset);

            // If we're not on a grid, then wandering around is fine.
            if (currentGrid == null)
                return (true, new() { { TargetCoordinates, _xforms.WithEntityId(target, currentMap.Value) } });

            target = _xforms.WithEntityId(target, currentGrid.Value);
            var indices = _maps.CoordinatesToTile(currentGrid.Value, grid!, target);

            // If there's a valid tile where we pointed, hooray.
            if (_maps.TryGetTile(grid!, indices, out var tile) && !tile.IsEmpty)
                return (true, new() { { TargetCoordinates, target } });
        }

        return (false, null);
    }
}
