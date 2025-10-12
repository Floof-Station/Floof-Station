using System.Threading;
using Content.Shared._Vulp.Weather;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.Weather;
using Robust.Shared.Random;


namespace Content.Server._Vulp.Weather.Functions;


[ImplicitDataDefinitionForInheritors, Serializable]
public abstract partial class WeatherFunctionWithDelay : WeatherFunction
{
    [DataField]
    public MinMax DelaySeconds = new(3, 10);

    protected CancellationTokenSource? cts;

    public override void Invoke(EntityManager entMan, Entity<WeatherComponent> ent, float updateTimeSeconds)
    {
        if (!entMan.TryGetComponent<WeatherCycleComponent>(ent, out var cycle))
            return;

        var startingWeather = cycle.CurrentState?.Proto;
        if (cts is not null && !cts.IsCancellationRequested)
            cts.Cancel();

        cts = new CancellationTokenSource();
        var delay = DelaySeconds.Next(IoCManager.Resolve<IRobustRandom>());

        // FIXME: Timers are getting obsoleted, replace this with a custom timer implementation
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(delay),
            () =>
            {
                if (entMan.Deleted(ent) || cycle.CurrentState?.Proto != startingWeather)
                    return;

                Fire(entMan, (ent.Owner, ent.Comp, cycle), updateTimeSeconds);
                cts = null;
            });
    }

    protected abstract void Fire(
        EntityManager entMan,
        Entity<WeatherComponent, WeatherCycleComponent> ent,
        float updateTimeSeconds
    );
}
