using Content.Server.DeltaV.EnvyClone.Components;
using Content.Server.DetailExaminable;
using Content.Server.GenericAntag;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Inventory;
using Content.Server.Psionics;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Abilities.Psionics;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Diagnostics.CodeAnalysis;
using Content.Server.Consent;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.DeltaV.EnvyClone.Systems;

/// <summary>
/// 90% of the work is done by exterminator since its a reskin.
/// All the logic here is spawning since thats tricky.
/// </summary>
public sealed class EnvyCloneSystem : EntitySystem
{
    [Dependency] private readonly ConsentSystem _consent = default!;
    [Dependency] private readonly GenericAntagSystem _genericAntag = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRole = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly PsionicsSystem _psionics = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly ServerInventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IComponentFactory _compFact = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnvyCloneSpawnerComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<EnvyCloneSpawnerComponent> ent, ref MeleeHitEvent args)
    {

        foreach (var hit in args.HitEntities)
        {
            if (hit == args.User)
                continue;

            if (!TryComp<HumanoidAppearanceComponent>(hit, out var humanoid))
                continue;

            if (humanoid.LastProfileLoaded is not { } profile)
                continue;

            if (!_proto.TryIndex<SpeciesPrototype>(humanoid.Species, out var species))
                continue;

            // if (_consent.HasConsent(hit, "NoClone"))
            //     continue;

            // Spawn the twin.
            var destination = Transform(args.User);
            var spawned = Spawn(species.Prototype, destination.Coordinates);

            // Copy the details and rotation
            _transform.SetLocalRotation(spawned, destination.LocalRotation);
            _humanoid.LoadProfile(spawned, profile);
            _metaData.SetEntityName(spawned, Name(hit));

            // if (TryComp<DetailExaminableComponent>(hit, out var detail))
            // {
            //     var detailCopy = EnsureComp<DetailExaminableComponent>(spawned);
            //     detailCopy.Content = detail.Content;
            // }

            // // TODO: In a future PR, make it so that the Paradox Anomaly spawns with a completely 1:1 clone of the victim's entire PsionicComponent.
            // if (HasComp<PsionicComponent>(hit))
            //     EnsureComp<PsionicComponent>(spawned);

            // Copy specified components over
            foreach (var compName in ent.Comp.CopiedComponents)
            {
                if (!_compFact.TryGetRegistration(compName, out var reg)
                    || !EntityManager.TryGetComponent(hit, reg.Idx, out var comp))
                    continue;

                var copy = _serialization.CreateCopy(comp, notNullableOverride: true);
                copy.Owner = spawned;
                AddComp(spawned, copy, true);
            }

            _inventory.TransferEntityInventories(args.User, spawned);
            foreach (var hand in _hands.EnumerateHeld(args.User))
            {
                _hands.TryDrop(args.User, hand, checkActionBlocker: false);
                _hands.TryPickupAnyHand(spawned, hand);
            }

            if (_mind.TryGetMind(args.User, out var mindId, out var mind))
                _mind.TransferTo(mindId, spawned, mind: mind);

            // Remove the original character entity
            QueueDel(args.User);

            break;
        }
    }
}
