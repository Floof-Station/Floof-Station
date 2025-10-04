using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;


namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat;

public sealed partial class UnPullOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!; // Floofstation
    [Dependency] private readonly IGameTiming _timing = default!; // Floofstation

    private PullingSystem _pulling = default!;

    private EntityQuery<PullableComponent> _pullableQuery;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    ///     Floofstation - base chance an npc will try to get un-pulled, the effective probability scales with other factors (hard pulls, etc.)
    /// </summary>
    [DataField]
    public float BreakChance { get; private set; } = 0.5f;

    /// <summary>
    ///     Floofstation - blackboard key used to store the time until next un-pull attempt.
    /// </summary>
    [DataField]
    public string UnpullTimeKey = "TimeUntilUnpull";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pulling = sysManager.GetEntitySystem<PullingSystem>();
        _pullableQuery = _entManager.GetEntityQuery<PullableComponent>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        // Floofstation - moved the code down
        base.Startup(blackboard);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        // Floofstation - edited this to not spam unpull attempts
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_pulling.IsPulled(owner))
            return HTNOperatorStatus.Finished;

        var lastPull = blackboard.GetValueOrDefault<TimeSpan>(UnpullTimeKey, _entManager);
        if (_timing.CurTime - lastPull <= TimeSpan.FromSeconds(1))
            return HTNOperatorStatus.Continuing;

        blackboard.SetValue(UnpullTimeKey, _timing.CurTime);
        if (_random.Prob(BreakChance))
            _pulling.TryStopPull(owner, _pullableQuery.GetComponent(owner), owner);

        return HTNOperatorStatus.Continuing;
        // Floofstation section end
    }
}
