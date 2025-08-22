using Content.Shared.Weather;


namespace Content.Shared._Vulp.Weather;



[ImplicitDataDefinitionForInheritors, Serializable]
public abstract partial class WeatherFunction
{
    public abstract void Invoke(EntityManager entMan, Entity<WeatherComponent> ent, float updateTimeSeconds);
}
