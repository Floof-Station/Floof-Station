using Robust.Shared.GameStates;

namespace Content.Shared.Strip.Components;

/// <summary>
/// Give this to an entity when you want to decrease stripping times
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ThievingComponent : Component
{
    /// <summary>
    /// How much the strip time should be shortened by
    /// </summary>
    [DataField]
    public TimeSpan StripTimeReduction = TimeSpan.FromSeconds(0.5f);

    /// <summary>
    ///  A multiplier coefficient for strip time
    /// </summary>
    [DataField]
    public float StripTimeMultiplier = 1f;

    /// <summary>
    /// Should it notify the user if they're stripping a pocket?
    /// </summary>
    [DataField]
    public ThievingStealth Stealth = ThievingStealth.Hidden;

    /// <summary>
    ///  Should the user be able to see hidden items? (i.e pockets)
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IgnoreStripHidden;

    /// <summary>
    /// Floof: The color of the hidden entity silhouette
    /// The default is based on the window background color, value-adjusted for contrast with all themes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color HiddenEntityColor = Color.FromHex("#AAAAAF");
}
