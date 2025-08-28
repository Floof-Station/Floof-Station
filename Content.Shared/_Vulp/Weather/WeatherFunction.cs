using Content.Shared.Weather;


namespace Content.Shared._Vulp.Weather;



[ImplicitDataDefinitionForInheritors, Serializable]
public abstract partial class WeatherFunction
{
    /// <summary>
    ///     Whether this function should be invoked when the same node is reached twice or more in a row.
    ///     Aka when the clear weather transitions into clear weather, or the like.
    /// </summary>
    public virtual bool InvokeOnRepeatedTraversal => true;

    public abstract void Invoke(EntityManager entMan, Entity<WeatherComponent> ent, float updateTimeSeconds);
}
