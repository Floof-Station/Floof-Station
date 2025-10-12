using System.Linq;
using Content.Server.Weather;
using Content.Shared._Vulp.Weather;
using Content.Shared.Weather;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;


namespace Content.Server._Vulp.Weather;


public sealed class WeatherCycleSystem : EntitySystem
{
    [Dependency] private readonly WeatherSystem _weather = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        _protoMan.PrototypesReloaded += ValidatePrototypes;
        ValidatePrototypes(null);
    }

    private void ValidatePrototypes(PrototypesReloadedEventArgs? args)
    {
        if (args != null && !args.WasModified<WeatherCyclePrototype>())
            return;

        // TODO: should this be an integration test?
        foreach (var proto in _protoMan.EnumeratePrototypes<WeatherCyclePrototype>())
            ValidatePrototype(proto);
    }

    /// <summary>
    ///     Validates the prototype data and sets up state IDs (WeatherCycleData.StateId). Logs errors to the console.
    /// </summary>
    public void ValidatePrototype(WeatherCyclePrototype proto)
    {
        foreach (var (id, data) in proto.Weathers)
        {
            data.StateId = id;
            if (data.Transitions is null)
                continue;

            foreach (var (refId, _) in data.Transitions)
            {
                if (proto.Weathers.ContainsKey(refId))
                    continue;

                Log.Error($"Weather prototype {proto.ID} contains an unresolved transition {refId} in its state {id}.");
            }
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<WeatherCycleComponent, MapComponent>();

        while (query.MoveNext(out var uid, out var weatherCycle, out var map))
        {
            if (_timing.CurTime < weatherCycle.NextUpdate)
                continue;

            weatherCycle.NextUpdate = _timing.CurTime + weatherCycle.UpdateInterval;
            var elapsed = weatherCycle.UpdateInterval;

            var weather = EnsureComp<WeatherComponent>(uid);
            ProcessWeather((uid, weatherCycle, weather), elapsed);
        }
    }

    public void ProcessWeather(Entity<WeatherCycleComponent, WeatherComponent> ent, TimeSpan elapsedTime)
    {
        if (!_protoMan.TryIndex(ent.Comp1.Prototype, out var proto))
            return;

        if (ent.Comp1.CurrentState is not {} current)
        {
            if (proto.Weathers.Count >= 1)
                SetState(ent, proto.Weathers.Values.MaxBy(it => it.Weight)!);
            return;
        }

        if (_timing.CurTime > ent.Comp1.NextWeather)
        {
            AdvanceState(ent, proto);
            return;
        }

        // If the current weather has fully started, begin executing its update functions
        if (current.Proto != null
            && (!ent.Comp2.Weather.TryGetValue(current.Proto.Value, out var data)))
            return;

        var updateTimeSeconds = (float) elapsedTime.TotalSeconds;
        foreach (var func in current.OnUpdate)
            func.Invoke(EntityManager, (ent.Owner, ent.Comp2), updateTimeSeconds);
    }

    public void AdvanceState(Entity<WeatherCycleComponent, WeatherComponent> ent, WeatherCyclePrototype cycle)
    {
        if (ent.Comp1.CurrentState is not { } current || cycle.Weathers.Count < 1)
            return;

        var newId = current.Transitions is not null
            ? current.Transitions.ToList().WeightedRandom(_random, it => it.Value).Key
            : cycle.Weathers.ToList().WeightedRandom(_random, it => it.Value.Weight).Key;

        if (!cycle.Weathers.TryGetValue(newId, out var newState))
        {
            Log.Error($"Encountered invalid weather state reference: {newId} in weather cycle {cycle.ID}.");
            newState = cycle.Weathers.Values.MaxBy(it => it.Weight)!;
        }

        ent.Comp1.Prototype = cycle.ID; // Just in case adminbus changed it
        SetState(ent, newState);
    }

    public void SetState(Entity<WeatherCycleComponent, WeatherComponent> ent, WeatherCycleData state)
    {
        var oldState = ent.Comp1.CurrentState;
        var isRepeatedTraversal = state == oldState;

        ent.Comp1.NextWeather = _timing.CurTime + TimeSpan.FromSeconds(state.DurationSeconds.Next(_random) * ent.Comp1.TimeScale);
        ent.Comp1.CurrentState = state;

        var proto = state.Proto == null ? null : _protoMan.TryIndex(state.Proto, out var weather) ? weather : null;
        if (Transform(ent).MapID is { } map)
            _weather.SetWeather(map, proto, ent.Comp1.NextWeather);

        // Run any transition functions on the new state
        foreach (var func in state.OnTransition)
            if (!isRepeatedTraversal || func.InvokeOnRepeatedTraversal)
                func.Invoke(EntityManager, (ent.Owner, ent.Comp2), 1f);
    }
}
