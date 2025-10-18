using Content.Shared.Inventory.Events;
using Content.Shared.Clothing.Components;
using Content.Shared._Floof.Hypno;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Content.Server.Consent;
using Content.Server.Labels;
using Robust.Shared.Audio.Systems;
using Content.Server.Abilities.Psionics;
using Content.Shared.Labels.Components;


namespace Content.Server._Floof;
public sealed class HypnoClothingsystem : EntitySystem
{
    [Dependency] private readonly ConsentSystem _consent = default!;
    [Dependency] private readonly LabelSystem _labelSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PsionicHypnoSystem _psionichypnosystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HypnoClothingComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HypnoClothingComponent, GetVerbsEvent<Verb>>(HypnoLinkVerb);
    }

    private void HypnoLinkVerb(EntityUid uid, HypnoClothingComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        Verb verbHypnoLink = new()
        {
            Act = () => HypnoLink(uid, component, args.User),
            Text = Loc.GetString("hypno-link"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/_Floof/Interface/Actions/hypno.png")),
            Priority = 1
        };
        args.Verbs.Add(verbHypnoLink);
    }

    private void OnEquipped(EntityUid uid, HypnoClothingComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing)
            || !clothing.Slots.HasFlag(args.SlotFlags))
            return;

        if (component.Master == null || !_consent.HasConsent(args.Equipee, "Hypno"))
            return;

        _psionichypnosystem.Hypnotize(component.Master.Value, args.Equipee);
    }

    public void HypnoLink(EntityUid uid, HypnoClothingComponent component, EntityUid master)
    {
        EnsureComp<PsionicHypnoComponent>(master);
        _labelSystem.Label(uid, Loc.GetString("hypno-link-master", ("entity", master)));
        _audio.PlayPvs(component.LinkSound, uid);

        component.Master = master;
    }
}
