using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Vulp.Weather;
using Content.Shared.Atmos;
using Content.Shared.Weather;
using Robust.Shared.Random;


namespace Content.Server._Vulp.Weather.Functions;


[DataDefinition, Serializable]
public sealed partial class WeatherSetAtmos : WeatherFunction
{
    [DataField(required: true)]
    public GasMixture Mixture = default!;

    /// <summary>
    ///     ±How much (in a fraction) the mixture can deviate from the target mixture.
    /// </summary>
    [DataField]
    public float MaxMolesDeviation = 0.01f;

    /// <summary>
    ///     By how many degrees (in Kelvin) the resulting mixture can deviate from the target mixture.
    /// </summary>
    [DataField]
    public float MaxTemperatureDeviation = 1f;

    public override void Invoke(EntityManager entMan, Entity<WeatherComponent> ent, float updateTimeSeconds)
    {
        // Don't want to accidentally apply a map atmosphere to a mob or something... Because SetMapAtmosphere would do that
        if (!entMan.HasComponent<MapAtmosphereComponent>(ent))
            return;

        // Small deviations in the contents of the resulting mixture
        var random = IoCManager.Resolve<IRobustRandom>();
        var resultMixture = new GasMixture(Mixture);
        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            if (resultMixture[i] <= 0.01f)
                continue;

            resultMixture.SetMoles(i, resultMixture[i] * random.NextFloat(1 - MaxMolesDeviation, 1 + MaxMolesDeviation));
        }

        resultMixture.Temperature += random.NextFloat(-MaxTemperatureDeviation, MaxTemperatureDeviation);
        resultMixture.MarkImmutable();

        entMan.System<AtmosphereSystem>().SetMapAtmosphere(ent, false, Mixture);
    }
}
