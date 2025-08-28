using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Clothing.StorageBlockedByClothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StorageBlockedByClothingComponent : Component
{
    /// <summary>
    /// Slots that block storage access
    /// </summary>
    [DataField, AutoNetworkedField]
    public SlotFlags Slots = SlotFlags.NONE;

    [DataField, AutoNetworkedField]
    public bool SelfCanAccess = true;
}
