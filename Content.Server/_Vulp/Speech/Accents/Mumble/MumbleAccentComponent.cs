using Content.Shared._Vulp.Speech.Accents.Mumble;
using Robust.Shared.Prototypes;


namespace Content.Server._Vulp.Speech.Accents.Mumble;


[RegisterComponent]
public sealed partial class MumbleAccentComponent : Component
{
    /// <summary>
    ///     The accent to apply to the entity. Do not modify directly unless the component is non-map-initialized.
    /// </summary>
    [DataField(required: true), Access(typeof(MumbleAccentSystem), Other = AccessPermissions.Read)]
    public ProtoId<MumbleAccentPrototype> Accent = "BasicMuzzle";

    /// <summary>
    ///     The loaded accent prototype.
    /// </summary>
    [NonSerialized, ViewVariables]
    public MumbleAccentPrototype? AccentPrototype;
}
