using System.Linq;
using Content.Shared._Floof.Traits.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Slippery;
using Content.Shared.Tag;

namespace Content.Shared._Floof.Traits;

public sealed class GenitalStorageSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly MarkingManager _marking = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenitalStorageComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<GenitalStorageComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
        SubscribeLocalEvent<GenitalStorageComponent, SlippedEvent>(OnSlipped);
    }

    private void OnSlipped(Entity<GenitalStorageComponent> ent, ref SlippedEvent args)
    {
        if (!AreGenitalsAccessible(ent))
            return;

        foreach (var slot in ent.Comp.SlotNames)
            _itemSlots.TryEject(ent, slot, null, out _);
    }

    private void OnInsertAttempt(Entity<GenitalStorageComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (!AreGenitalsAccessible(ent))
            args.Cancelled = true;
    }

    private void OnEjectAttempt(Entity<GenitalStorageComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (!AreGenitalsAccessible(ent))
            args.Cancelled = true;
    }

    private bool AreGenitalsAccessible(EntityUid uid) =>
        // inaccessible if wearing a jumpsuit (but not a skirt)
        (!_inventory.TryGetSlotEntity(uid, "jumpsuit", out var jumpsuit) || _tag.HasTag(jumpsuit.Value, "Skirt"))
        // and not wearing underwear
        && (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid)
            || !humanoid.MarkingSet.Markings.Values.SelectMany(markings => markings)
                .Any(marking => !humanoid.HiddenMarkings.Contains(marking.MarkingId)
                    && _marking.TryGetMarking(marking, out var proto) && proto.BodyPart == HumanoidVisualLayers.Underwear));
}
