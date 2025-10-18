using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Clothing.StorageBlockedByClothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StorageBlockedByClothingComponent : Component
{
    /// <summary>
    /// Slots that block storage access when occupied.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SlotFlags Slots = SlotFlags.NONE;

    /// <summary>
    /// If set to true, the entity can always access its own storage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SelfCanAccess = true;

    /// <summary>
    /// Items with any of these tags will not block storage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<TagPrototype>> AllowedTags = new();
}
