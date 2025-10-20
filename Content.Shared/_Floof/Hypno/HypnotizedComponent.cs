using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Hypno;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HypnotizedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Master;
}
