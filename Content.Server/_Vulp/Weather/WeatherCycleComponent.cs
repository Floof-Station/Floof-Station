using Content.Shared._Vulp.Weather;
using Content.Shared.Weather;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;


namespace Content.Server._Vulp.Weather;


[RegisterComponent]
public sealed partial class WeatherCycleComponent : Component
{
    [DataField(required: true)]
    public ProtoId<WeatherCyclePrototype> Prototype = default!;

    [DataField]
    public TimeSpan
        UpdateInterval = TimeSpan.FromSeconds(2),
        NextUpdate = TimeSpan.Zero,
        NextWeather = TimeSpan.Zero;

    [DataField]
    public WeatherCycleData? CurrentState = null;

    /// <summary>
    ///     For debug use only, makes the weather cycle go on faster or slower.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float TimeScale = 1f;

    // Accessibility
    [ViewVariables(VVAccess.ReadWrite), UsedImplicitly]
    private string _prototypeVV { get => Prototype; set => Prototype = value; }
}
