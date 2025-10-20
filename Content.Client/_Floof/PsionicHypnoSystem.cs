using Content.Shared._Floof.Hypno;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using Robust.Client.Player;
using Content.Client.Overlays;

namespace Content.Client._Floof;

public sealed class PsionicHypnoSystem : EquipmentHudSystem<PsionicHypnoComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HypnotizedComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, HypnotizedComponent component, ref GetStatusIconsEvent args)
    {
        if (!IsActive || args.InContainer)
            return;

        if (_playerManager.LocalEntity is not { Valid: true } player
            || !TryComp<PsionicHypnoComponent>(player, out var hypnoComp)
            || component.Master != player)
            return;

        if (_prototype.TryIndex<StatusIconPrototype>(hypnoComp.SubjectIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
