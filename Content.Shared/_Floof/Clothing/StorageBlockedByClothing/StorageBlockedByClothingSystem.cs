using Content.Shared._Floof.Clothing.SlotBlocker;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Tag;

namespace Content.Shared._Floof.Clothing.StorageBlockedByClothing;

public sealed class StorageBlockedByClothingSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SlotBlockerSystem _slotBlocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StorageBlockedByClothingComponent, ClothingDidEquippedEvent>(OnClothingEquipped);
        SubscribeLocalEvent<StorageBlockedByClothingComponent, StorageInteractAttemptEvent>(OnStorageInteractAttempt);
    }

    private void OnClothingEquipped(Entity<StorageBlockedByClothingComponent> ent, ref ClothingDidEquippedEvent args)
    {
        if (!TryComp(ent, out InventoryComponent? inventory))
            return;
        if (!TryComp(ent, out UserInterfaceComponent? ui))
            return;

        if (!IsBlocked(new(ent, inventory), ent.Comp))
            return;

        foreach (var actor in _ui.GetActors(new(ent, ui), StorageComponent.StorageUiKey.Key))
        {
            if (ent.Comp.SelfCanAccess && actor == ent.Owner)
                continue;

            _ui.CloseUi(new(ent, ui), StorageComponent.StorageUiKey.Key, actor);
        }
    }

    private void OnStorageInteractAttempt(Entity<StorageBlockedByClothingComponent> ent, ref StorageInteractAttemptEvent args)
    {
        if (ent.Comp.SelfCanAccess && args.User == ent.Owner)
            return;

        if (!TryComp(ent, out InventoryComponent? inventory))
            return;

        if (IsBlocked(new(ent, inventory), ent.Comp))
            args.Cancelled = true;
    }

    private bool IsBlocked(Entity<InventoryComponent> ent, StorageBlockedByClothingComponent comp)
    {
        // if our slots are obstructed
        if (_slotBlocker.IsSlotObstructed(
            ent,
            null,
            SlotBlockerSystem.CheckType.IgnoreBlockerPreference,
            comp.Slots,
            out _))
            return true;

        // or if they have non-allowed items
        for (var i = 0; i < ent.Comp.Slots.Length; i++)
        {
            if ((ent.Comp.Slots[i].SlotFlags & comp.Slots) != 0
                    && ent.Comp.Containers[i].ContainedEntity is { Valid: true } item
                    && !_tag.HasAnyTag(item, comp.AllowedTags))
                return true;
        }
        return false;
    }
}
