using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;


namespace Content.Server._Floof.Preferences;

[RegisterComponent]
public sealed partial class FavoriteDrinkComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<ReagentPrototype> ReagentId;
}
