using Content.Shared.Destructible.Thresholds;
using Content.Shared.Weather;
using Robust.Shared.Prototypes;


namespace Content.Shared._Vulp.Weather;


[Prototype]
public sealed partial class WeatherCyclePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Weighted list of weather types in this cycle.
    ///     Keys are identifiers unique within this cycle.
    /// </summary>
    [DataField(serverOnly: true)]
    public Dictionary<string, WeatherCycleData> Weathers = new();
}

[DataDefinition, Serializable]
public sealed partial class WeatherCycleData
{
    /// <summary>
    ///     ID of this state. Evaluated at runtime if this state is part of a weather cycle prototype.
    /// </summary>
    [NonSerialized]
    public string? StateId;

    [DataField(required: true)]
    public ProtoId<WeatherPrototype>? Proto;

    [DataField(required: true)]
    public float Weight;

    /// <summary>
    ///     Functions invoked when this part of the cycle begins.
    /// </summary>
    [DataField]
    public List<WeatherFunction> OnTransition = new();

    /// <summary>
    ///     Functions invoked on each tick of this weather.
    /// </summary>
    [DataField]
    public List<WeatherFunction> OnUpdate = new();

    /// <summary>
    ///     Which weather prototypes this one can transition to. If null, the list specified in the prototype is used instead.
    ///     Keys are step IDs, values are weights.
    /// </summary>
    [DataField]
    public Dictionary<string, float>? Transitions = null;

    [DataField]
    public MinMax DurationSeconds = new(600, 1200);

    [DataField, Obsolete("Use DurationSeconds instead")]
    public MinMax DurationMinutes
    {
        get => new(DurationSeconds.Min * 60, DurationSeconds.Max * 60);
        set => DurationSeconds = new(value.Min / 60, value.Max / 60);
    }

    public WeatherCycleData() {}
}
