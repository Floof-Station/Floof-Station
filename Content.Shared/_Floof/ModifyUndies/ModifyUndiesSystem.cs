using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.FloofStation.ModifyUndies;

/// <summary>
/// System that controls removing and adding underwear to humanoid entities through a verb.
/// </summary>
public sealed class ModifyUndiesSystem : EntitySystem
{
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly INetManager _net = default!;

    public static readonly VerbCategory UndiesCat = new("verb-categories-undies", "/Textures/Interface/VerbIcons/undies.png");

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModifyUndiesComponent, GetVerbsEvent<Verb>>(AddModifyUndiesVerb);
        SubscribeLocalEvent<ModifyUndiesComponent, ModifyUndiesDoAfterEvent>(ToggleUndies);
    }

    private void AddModifyUndiesVerb(Entity<ModifyUndiesComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(args.Target, out var humApp))
            return;

        if (args.User != args.Target && _inventory.TryGetSlotEntity(args.Target, "jumpsuit", out _))
            return; // mainly so people cant just spy on others undies *too* easily

        var user = args.User;
        var target = args.Target;
        var isMine = user == target;

        // okay go through their markings, and find all the undershirts and underwear markings
        // <marking_ID>, list:(localized name, bodypart enum, isvisible)
        foreach (var marking in humApp.MarkingSet.Markings.Values.SelectMany(markingLust => markingLust))
        {
            if (!_markingManager.TryGetMarking(marking, out var mProt))
                continue;

            // check if the Bodypart is in the component's BodyPartTargets
            if (!ent.Comp.BodyPartTargets.Contains(mProt.BodyPart))
                continue;

            var localizedName = Loc.GetString($"marking-{mProt.ID}");
            var partSlot = mProt.BodyPart;
            var isVisible = !humApp.HiddenMarkings.Contains(mProt.ID);

            if (mProt.Sprites.Count < 1)
                continue; // no sprites means its not visible means its kinda already off and you cant put it on

            var underwearIcon = partSlot switch
            {
                HumanoidVisualLayers.Undershirt => new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bra.png")),
                HumanoidVisualLayers.Underwear => new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/underpants.png")),
                _ => new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/undies.png"))
            };

            // add the verb
            Verb verb = new()
            {
                Text = Loc.GetString(
                    "modify-undies-verb-text",
                    ("undies", localizedName),
                    ("isVisible", isVisible),
                    ("isMine", isMine),
                    ("target", Identity.Entity(target, _entMan))
                ),

                Icon = underwearIcon,
                Category = UndiesCat,
                Act = () =>
                {
                    var ev = new ModifyUndiesDoAfterEvent(marking, localizedName, isVisible);

                    var doAfterArgs = new DoAfterArgs(
                        _entMan,
                        user,
                        TimeSpan.FromSeconds(2),
                        ev,
                        target,
                        target,
                        used: user)
                    {
                        Hidden = false,
                        MovementThreshold = 0,
                        RequireCanInteract = true,
                        BlockDuplicate = true
                    };

                    if (isMine)
                    {
                        var selfString = isVisible ? "undies-removed-self-start" : "undies-equipped-self-start";
                        var selfPopup = Loc.GetString(selfString, ("undie", localizedName));
                        _popupSystem.PopupClient(selfPopup, target, target, PopupType.Medium);
                    }
                    else
                    {
                        // to the user
                        var userString = isVisible ? "undies-removed-user-start" : "undies-equipped-user-start";
                        var userPopup = Loc.GetString(userString, ("undie", localizedName));
                        _popupSystem.PopupClient(userPopup, user, user, PopupType.Medium);

                        // to the target
                        var targetString = isVisible ? "undies-removed-target-start" : "undies-equipped-target-start";
                        var targetPopup = Loc.GetString(targetString, ("undie", localizedName), ("user", Identity.Entity(user, _entMan)));
                        _popupSystem.PopupClient(targetPopup, target, target, PopupType.MediumCaution);
                    }

                    if (_net.IsServer)
                        _audio.PlayEntity(ent.Comp.Sound, Filter.Entities(user, target), target, false);

                    _doAfterSystem.TryStartDoAfter(doAfterArgs);
                },

                Disabled = false,
                Message = null
            };

            args.Verbs.Add(verb);
        }
    }

    private void ToggleUndies(Entity<ModifyUndiesComponent> ent, ref ModifyUndiesDoAfterEvent args)
    {
        if (!_markingManager.TryGetMarking(args.Marking, out var mProt))
            return;

        if (!TryComp<HumanoidAppearanceComponent>(args.Target, out var humApp))
            return;

        var isVisible = args.IsVisible;
        var localizedName = args.MarkingPrototypeName;

        var user = args.User;
        var target = args.Target.Value;
        var isMine = user == target;

        _humanoid.SetMarkingVisibility(ent.Owner, humApp, mProt.ID, !isVisible);

        if (isMine)
        {
            var selfString = isVisible ? "undies-removed-self" : "undies-equipped-self";
            var selfPopup = Loc.GetString(selfString, ("undie", localizedName));
            _popupSystem.PopupClient(selfPopup, target, target, PopupType.Medium);
        }
        else
        {
            // to the user
            var userString = isVisible ? "undies-removed-user" : "undies-equipped-user";
            var userPopup = Loc.GetString(userString, ("undie", localizedName));
            _popupSystem.PopupClient(userPopup, user, user, PopupType.Medium);

            // to the target
            var targetString = isVisible ? "undies-removed-target" : "undies-equipped-target";
            var targetPopup = Loc.GetString(targetString, ("undie", localizedName), ("user", Identity.Entity(user, _entMan)));
            _popupSystem.PopupClient(targetPopup, target, target, PopupType.MediumCaution);
        }

        if (_net.IsClient)
            return;

        // and then play a sound!
        _audio.PlayEntity(ent.Comp.Sound, Filter.Entities(user, target), target, false);
    }
}
