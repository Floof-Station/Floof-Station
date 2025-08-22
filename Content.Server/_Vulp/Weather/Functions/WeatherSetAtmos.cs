using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Vulp.Weather;
using Content.Shared.Atmos;
using Content.Shared.Weather;


namespace Content.Server._Vulp.Weather.Functions;


[DataDefinition, Serializable]
public sealed partial class WeatherSetAtmos : WeatherFunction
{
    [DataField]
    public GasMixture Mixture;

    public override void Invoke(EntityManager entMan, Entity<WeatherComponent> ent, float updateTimeSeconds)
    {
        // Don't want to accidentally apply a map atmosphere to a mob or something... Because SetMapAtmosphere would do that
        if (!entMan.HasComponent<MapAtmosphereComponent>(ent))
            return;

        entMan.System<AtmosphereSystem>().SetMapAtmosphere(ent, false, Mixture);
    }
}
