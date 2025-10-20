using Content.Server.Actions;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Carrying;
using Content.Server.Humanoid;
using Content.Server.Inventory;
using Content.Server.Mind.Commands;
using Content.Server.Nutrition;
using Content.Server.Polymorph.Components;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._DV.Polymorph; // DeltaV
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Buckle;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems; // DeltaV
using Content.Shared.Destructible;
using Content.Shared.Floofstation.Leash;
using Content.Shared.Floofstation.Leash.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Polymorph.Systems;

public sealed partial class PolymorphSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _compFact = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!; // DeltaV
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly ServerInventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly CarryingSystem _carrying = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!; // Floof
    [Dependency] private readonly BodySystem _body = default!; // Floof
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!; // Floof
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!; // Floof
    [Dependency] private readonly LeashSystem _leash = default!; // Floof

    private const string RevertPolymorphId = "ActionRevertPolymorph";

    public override void Initialize()
    {
        SubscribeLocalEvent<PolymorphableComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<PolymorphedEntityComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<PolymorphableComponent, PolymorphActionEvent>(OnPolymorphActionEvent);
        SubscribeLocalEvent<PolymorphedEntityComponent, RevertPolymorphActionEvent>(OnRevertPolymorphActionEvent);

        SubscribeLocalEvent<PolymorphedEntityComponent, BeforeFullyEatenEvent>(OnBeforeFullyEaten);
        SubscribeLocalEvent<PolymorphedEntityComponent, BeforeFullySlicedEvent>(OnBeforeFullySliced);
        SubscribeLocalEvent<PolymorphedEntityComponent, DestructionEventArgs>(OnDestruction);

        // Floof
        SubscribeLocalEvent<PolymorphingComponent, DownAttemptEvent>(OnDownAttempt);

        InitializeCollide();
        InitializeMap();
        InitializeProvider();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PolymorphedEntityComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Time += frameTime;

            if (comp.Configuration.Duration != null && comp.Time >= comp.Configuration.Duration)
            {
                Revert((uid, comp));
                continue;
            }

            if (!TryComp<MobStateComponent>(uid, out var mob))
                continue;

            if (comp.Configuration.RevertOnDeath && _mobState.IsDead(uid, mob) ||
                comp.Configuration.RevertOnCrit && _mobState.IsIncapacitated(uid, mob))
            {
                Revert((uid, comp));
            }
        }

        UpdateCollide();
    }

    private void OnComponentStartup(Entity<PolymorphableComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.InnatePolymorphs != null)
        {
            foreach (var morph in ent.Comp.InnatePolymorphs)
            {
                CreatePolymorphAction(morph, ent);
            }
        }
    }

    private void OnMapInit(Entity<PolymorphedEntityComponent> ent, ref MapInitEvent args)
    {
        var (uid, component) = ent;
        if (component.Configuration.Forced)
            return;

        if (_actions.AddAction(uid, ref component.Action, out var action, RevertPolymorphId))
        {
            action.EntityIcon = component.Parent;
            action.UseDelay = TimeSpan.FromSeconds(component.Configuration.Delay);
        }
    }

    private void OnPolymorphActionEvent(Entity<PolymorphableComponent> ent, ref PolymorphActionEvent args)
    {
        if (!_proto.TryIndex(args.ProtoId, out var prototype) || args.Handled)
            return;

        PolymorphEntity(ent, prototype.Configuration);

        args.Handled = true;
    }

    private void OnRevertPolymorphActionEvent(Entity<PolymorphedEntityComponent> ent,
        ref RevertPolymorphActionEvent args)
    {
        Revert((ent, ent));
    }

    private void OnBeforeFullyEaten(Entity<PolymorphedEntityComponent> ent, ref BeforeFullyEatenEvent args)
    {
        var (_, comp) = ent;
        if (comp.Configuration.RevertOnEat)
        {
            args.Cancel();
            Revert((ent, ent));
        }
    }

    private void OnBeforeFullySliced(Entity<PolymorphedEntityComponent> ent, ref BeforeFullySlicedEvent args)
    {
        var (_, comp) = ent;
        if (comp.Configuration.RevertOnEat)
        {
            args.Cancel();
            Revert((ent, ent));
        }
    }

    /// <summary>
    /// It is possible to be polymorphed into an entity that can't "die", but is instead
    /// destroyed. This handler ensures that destruction is treated like death.
    /// </summary>
    private void OnDestruction(Entity<PolymorphedEntityComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.Configuration.RevertOnDeath)
        {
            Revert((ent, ent));
        }
    }

    /// <summary>
    /// Floof: When the brain is removed from an entity, it receives the Debrained component and is forced to lay down.
    /// When we're switching the organs up, we add the Polymorphing component so we can cancel that event.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="args"></param>
    private void OnDownAttempt(EntityUid uid, PolymorphingComponent component, DownAttemptEvent args) => args.Cancel();

    /// <summary>
    /// Polymorphs the target entity into the specific polymorph prototype
    /// </summary>
    /// <param name="uid">The entity that will be transformed</param>
    /// <param name="protoId">The id of the polymorph prototype</param>
    public EntityUid? PolymorphEntity(EntityUid uid, ProtoId<PolymorphPrototype> protoId)
    {
        var config = _proto.Index(protoId).Configuration;
        return PolymorphEntity(uid, config);
    }

    /// <summary>
    /// Polymorphs the target entity into another
    /// </summary>
    /// <param name="uid">The entity that will be transformed</param>
    /// <param name="configuration">Polymorph data</param>
    /// <returns></returns>
    public EntityUid? PolymorphEntity(EntityUid uid, PolymorphConfiguration configuration)
    {
        // if it's already morphed, don't allow it again with this condition active.
        if (!configuration.AllowRepeatedMorphs && HasComp<PolymorphedEntityComponent>(uid))
            return null;

        // If this polymorph has a cooldown, check if that amount of time has passed since the
        // last polymorph ended.
        if (TryComp<PolymorphableComponent>(uid, out var polymorphableComponent) &&
            polymorphableComponent.LastPolymorphEnd != null &&
            _gameTiming.CurTime < polymorphableComponent.LastPolymorphEnd + configuration.Cooldown)
            return null;

        // mostly just for vehicles
        _buckle.TryUnbuckle(uid, uid, true);

        var targetTransformComp = Transform(uid);

        var child = Spawn(configuration.Entity, _transform.GetMapCoordinates(uid, targetTransformComp), rotation: _transform.GetWorldRotation(uid));

        // Floof: add Polymorphing component to mark that we're actively making changes to these entities.
        EnsureComp<PolymorphingComponent>(uid);
        EnsureComp<PolymorphingComponent>(child);

        // Copy specified components over
        foreach (var compName in configuration.CopiedComponents)
        {
            if (!_compFact.TryGetRegistration(compName, out var reg)
                || !EntityManager.TryGetComponent(uid, reg.Idx, out var comp))
                continue;

            var copy = _serialization.CreateCopy(comp, notNullableOverride: true);
            copy.Owner = child;
            AddComp(child, copy, true);
        }

        // Ensure the resulting entity is sentient (why? this sucks)
        MakeSentientCommand.MakeSentient(child, EntityManager);

        var polymorphedComp = _compFact.GetComponent<PolymorphedEntityComponent>();
        polymorphedComp.Parent = uid;
        polymorphedComp.Configuration = configuration;

        if (TryComp<LeashedComponent>(uid, out var leashed)
            && TryComp<LeashComponent>(leashed.Puller, out var leash)
            && TryComp<LeashAnchorComponent>(leashed.Anchor, out var anchor))
        {
            _leash.RemoveLeash(uid, leashed.Puller.Value);
            polymorphedComp.LeashAnchor = new(leashed.Anchor.Value, anchor); // save for later
            if (TryComp<LeashAnchorComponent>(child, out var childAnchor))
                // Use a timer to delay the leashing, otherwise we'll crash the client's prediction
                Timer.Spawn(0, () => _leash.DoLeash(new(child, childAnchor), new(leashed.Puller.Value, leash), child));
        }

        AddComp(child, polymorphedComp);

        var childXform = Transform(child);
        _transform.SetLocalRotation(child, targetTransformComp.LocalRotation, childXform);

        if (_container.TryGetContainingContainer((uid, targetTransformComp, null), out var cont))
            _container.Insert(child, cont);

        // if a held item polymorphs, drop it
        _transform.AttachToGridOrMap(uid, targetTransformComp);

        // if someone being carried polymorphs, drop them
        if (TryComp<BeingCarriedComponent>(uid, out var carried))
            _carrying.DropCarried(carried.Carrier, uid);

        var bloodstream = CompOrNull<BloodstreamComponent>(uid);
        var childBloodstream = CompOrNull<BloodstreamComponent>(child);

        //Transfers all damage from the original to the new one
        if (configuration.TransferDamage &&
            TryComp<DamageableComponent>(child, out var damageParent) &&
            _mobThreshold.GetScaledDamage(uid, child, out var damage) &&
            damage != null)
        {
            _damageable.SetDamage(child, damageParent, damage);

            // DeltaV - Transfer Stamina Damage
            var staminaDamage = _stamina.GetStaminaDamage(uid);
            _stamina.TakeStaminaDamage(child, staminaDamage);

            // Floof
            // childBloodstream.BloodSolution is always null when the entity is first spawned
            // but we know it'll be filled to maximum volume
            if (bloodstream is not null && _solution.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution)
                && childBloodstream is not null && _solution.ResolveSolution(child, childBloodstream.BloodSolutionName, ref childBloodstream.BloodSolution))
            {
                childBloodstream.BleedReductionAmount = bloodstream.BleedReductionAmount;
                _bloodstream.TryModifyBleedAmount(child, bloodstream.BleedAmount, childBloodstream);
                var scaledBloodLevel = childBloodstream.BloodMaxVolume * bloodstream.BloodSolution.Value.Comp.Solution.FillFraction;
                _bloodstream.TryModifyBloodLevel(
                    child,
                    scaledBloodLevel - childBloodstream.BloodSolution.Value.Comp.Solution.Volume,
                    childBloodstream,
                    false);
            }
        }

        // Floof
        if (configuration.TransferTemperature && TryComp<TemperatureComponent>(uid, out var temperature))
            _temperature.ForceChangeTemperature(child, temperature.CurrentTemperature);

        // Floof
        if (configuration.TransferChemicals
            && bloodstream is not null && _solution.ResolveSolution(uid, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution)
            && childBloodstream is not null && _solution.ResolveSolution(child, childBloodstream.ChemicalSolutionName, ref childBloodstream.ChemicalSolution))
        {
            childBloodstream.ChemicalSolution.Value.Comp.Solution.SetContents(bloodstream.ChemicalSolution.Value.Comp.Solution.Contents);
        }

        // Floof
        if (configuration.TransferOrgans && TryComp<BodyComponent>(uid, out var body) && TryComp<BodyComponent>(child, out var childBody))
        {
            var childOrgans = _body.GetBodyOrgans(child, childBody);
            foreach (var organ in _body.GetBodyOrgans(uid, body))
            {
                if (!childOrgans.TryFirstOrNull(childOrgan => childOrgan.Component.SlotId == organ.Component.SlotId, out var childOrgan))
                    continue;
                if (!_container.TryGetContainingContainer((childOrgan.Value.Id, null, null), out var container))
                    continue;
                _container.Remove(childOrgan.Value.Id, container);
                QueueDel(childOrgan.Value.Id);
                _container.Insert(organ.Id, container);
            }
        }

        // DeltaV - Drop MindContainer entities on polymorph
        var beforePolymorphedEv = new BeforePolymorphedEvent();
        RaiseLocalEvent(uid, ref beforePolymorphedEv);

        if (configuration.Inventory == PolymorphInventoryChange.Transfer)
        {
            _inventory.TransferEntityInventories(uid, child);
            foreach (var hand in _hands.EnumerateHeld(uid))
            {
                _hands.TryDrop(uid, hand, checkActionBlocker: false);
                _hands.TryPickupAnyHand(child, hand);
            }
        }
        else if (configuration.Inventory == PolymorphInventoryChange.Drop)
        {
            if (_inventory.TryGetContainerSlotEnumerator(uid, out var enumerator))
            {
                while (enumerator.MoveNext(out var slot))
                {
                    _inventory.TryUnequip(uid, slot.ID, true, true);
                }
            }

            foreach (var held in _hands.EnumerateHeld(uid))
            {
                _hands.TryDrop(uid, held);
            }
        }

        if (configuration.TransferName && TryComp<MetaDataComponent>(uid, out var targetMeta))
            _metaData.SetEntityName(child, targetMeta.EntityName);

        if (configuration.TransferHumanoidAppearance)
        {
            _humanoid.CloneAppearance(uid, child);
        }

        if (_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            _mindSystem.TransferTo(mindId, child, mind: mind);

        //Ensures a map to banish the entity to
        EnsurePausedMap();
        if (PausedMap != null)
            _transform.SetParent(uid, targetTransformComp, PausedMap.Value);

        // Raise an event to inform anything that wants to know about the entity swap
        var ev = new PolymorphedEvent(uid, child, false);
        RaiseLocalEvent(uid, ref ev);

        // Floof: this entity is now completely polymorphed!
        RemComp<PolymorphingComponent>(child);

        return child;
    }

    /// <summary>
    /// Reverts a polymorphed entity back into its original form
    /// </summary>
    /// <param name="uid">The entityuid of the entity being reverted</param>
    /// <param name="component"></param>
    public EntityUid? Revert(Entity<PolymorphedEntityComponent?> ent)
    {
        var (uid, component) = ent;
        if (!Resolve(ent, ref component))
            return null;

        if (Deleted(uid))
            return null;

        var parent = component.Parent;
        if (Deleted(parent))
            return null;

        var uidXform = Transform(uid);
        var parentXform = Transform(parent);

        _transform.SetParent(parent, parentXform, uidXform.ParentUid);
        _transform.SetCoordinates(parent, parentXform, uidXform.Coordinates, uidXform.LocalRotation);

        if (TryComp<LeashedComponent>(uid, out var leashed) && leashed.Puller is not null)
        {
            _leash.RemoveLeash(uid, leashed.Puller.Value);

            if (ent.Comp is not null && ent.Comp.LeashAnchor is null && _inventory.TryGetSlots(parent, out var slots))
            {
                foreach (var slot in slots)
                {
                    if (!_inventory.TryGetSlotEntity(parent, slot.Name, out var item))
                        continue;
                    if (!TryComp<LeashAnchorComponent>(item, out var anchor))
                        continue;

                    ent.Comp.LeashAnchor = new(item.Value, anchor);
                    break;
                }
            }

            if (ent.Comp?.LeashAnchor is not null)
                // Use a timer to delay the leashing, otherwise we'll crash the client's prediction
                Timer.Spawn(0, () =>
                    _leash.DoLeash(ent.Comp.LeashAnchor.Value, new(leashed.Puller.Value, Comp<LeashComponent>(leashed.Puller.Value)), parent));
        }

        // Floof: copy specified components back to parent, or remove if they've been removed from the child
        if (component.Configuration.SyncComponents)
        {
            foreach (var compName in component.Configuration.CopiedComponents)
            {
                if (!_compFact.TryGetRegistration(compName, out var reg))
                    continue;

                // Only sync back components that came from the parent, so we don't end up inheriting completely
                // new components from a polymorph
                if (!HasComp(parent, reg.Type))
                    continue;

                if (!EntityManager.TryGetComponent(uid, reg.Idx, out var comp))
                {
                    RemComp(parent, reg.Type);
                    continue;
                }

                var copy = _serialization.CreateCopy(comp, notNullableOverride: true);
                copy.Owner = parent;
                AddComp(parent, copy, true);
            }
        }

        // Floof
        var bloodstream = CompOrNull<BloodstreamComponent>(uid);
        var parentBloodstream = CompOrNull<BloodstreamComponent>(parent);

        if (component.Configuration.TransferDamage &&
            TryComp<DamageableComponent>(parent, out var damageParent) &&
            _mobThreshold.GetScaledDamage(uid, parent, out var damage) &&
            damage != null)
        {
            _damageable.SetDamage(parent, damageParent, damage);

            // DeltaV - Transfer Stamina Damage
            var staminaDamage = _stamina.GetStaminaDamage(uid);
            _stamina.TakeStaminaDamage(parent, staminaDamage);

            // Floof
            if (bloodstream?.BloodSolution is not null && parentBloodstream?.BloodSolution is not null)
            {
                parentBloodstream.BleedReductionAmount = bloodstream.BleedReductionAmount;
                _bloodstream.TryModifyBleedAmount(
                    parent,
                    bloodstream.BleedAmount - parentBloodstream.BleedAmount,
                    parentBloodstream);
                var scaledBloodLevel = parentBloodstream.BloodMaxVolume * bloodstream.BloodSolution.Value.Comp.Solution.FillFraction;
                _bloodstream.TryModifyBloodLevel(
                    parent,
                    scaledBloodLevel - parentBloodstream.BloodSolution.Value.Comp.Solution.Volume,
                    parentBloodstream,
                    false);
            }
        }

        // Floof
        if (component.Configuration.TransferTemperature && TryComp<TemperatureComponent>(uid, out var temperature))
            _temperature.ForceChangeTemperature(parent, temperature.CurrentTemperature);

        // Floof
        if (component.Configuration.TransferChemicals
            && bloodstream is not null && _solution.ResolveSolution(uid, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution)
            && parentBloodstream is not null && _solution.ResolveSolution(parent, parentBloodstream.ChemicalSolutionName, ref parentBloodstream.ChemicalSolution))
        {
            parentBloodstream.ChemicalSolution.Value.Comp.Solution.SetContents(bloodstream.ChemicalSolution.Value.Comp.Solution.Contents);
        }

        // Floof
        if (component.Configuration.TransferOrgans && TryComp<BodyComponent>(uid, out var body) &&
            TryComp<BodyComponent>(parent, out var parentBody))
        {
            if (_body.GetRootPartOrNull(parent, parentBody) is { } parentRoot)
            {
                foreach (var organ in _body.GetBodyOrgans(uid, body))
                {
                    foreach (var part in _body.GetBodyPartChildren(parentRoot.Entity, parentRoot.BodyPart))
                        if (_body.InsertOrgan(part.Id, organ.Id, organ.Component.SlotId, part.Component, organ.Component))
                            break;
                }
            }
        }

        if (component.Configuration.Inventory == PolymorphInventoryChange.Transfer)
        {
            _inventory.TransferEntityInventories(uid, parent);
            foreach (var held in _hands.EnumerateHeld(uid))
            {
                _hands.TryDrop(uid, held);
                _hands.TryPickupAnyHand(parent, held, checkActionBlocker: false);
            }
        }
        else if (component.Configuration.Inventory == PolymorphInventoryChange.Drop)
        {
            if (_inventory.TryGetContainerSlotEnumerator(uid, out var enumerator))
            {
                while (enumerator.MoveNext(out var slot))
                {
                    _inventory.TryUnequip(uid, slot.ID);
                }
            }

            foreach (var held in _hands.EnumerateHeld(uid))
            {
                _hands.TryDrop(uid, held);
            }
        }

        if (_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            _mindSystem.TransferTo(mindId, parent, mind: mind);

        if (TryComp<PolymorphableComponent>(parent, out var polymorphableComponent))
            polymorphableComponent.LastPolymorphEnd = _gameTiming.CurTime;

        // if a held item reverts, drop it
        _transform.AttachToGridOrMap(parent, parentXform);

        // if someone being carried reverts, drop them
        if (TryComp<BeingCarriedComponent>(uid, out var carried))
            _carrying.DropCarried(carried.Carrier, uid);

        // Raise an event to inform anything that wants to know about the entity swap
        var ev = new PolymorphedEvent(uid, parent, true);
        RaiseLocalEvent(uid, ref ev);

        _popup.PopupEntity(Loc.GetString("polymorph-revert-popup-generic",
                ("parent", Identity.Entity(uid, EntityManager)),
                ("child", Identity.Entity(parent, EntityManager))),
            parent);
        QueueDel(uid);

        // Floof: no longer mid-polymorph
        RemComp<PolymorphingComponent>(parent);

        return parent;
    }

    /// <summary>
    /// Creates a sidebar action for an entity to be able to polymorph at will
    /// </summary>
    /// <param name="id">The string of the id of the polymorph action</param>
    /// <param name="target">The entity that will be gaining the action</param>
    public void CreatePolymorphAction(ProtoId<PolymorphPrototype> id, Entity<PolymorphableComponent> target)
    {
        target.Comp.PolymorphActions ??= new();
        if (target.Comp.PolymorphActions.TryGetValue(id, out var actionBla))
        {
            _actions.AddAction(target, actionBla, target);
            return;
        }

        if (!_proto.TryIndex(id, out var polyProto))
            return;

        var entProto = _proto.Index(polyProto.Configuration.Entity);

        EntityUid? actionId = default!;
        if (!_actions.AddAction(target, ref actionId, RevertPolymorphId, target))
            return;

        target.Comp.PolymorphActions.Add(id, actionId.Value);

        var metaDataCache = MetaData(actionId.Value);
        _metaData.SetEntityName(actionId.Value, Loc.GetString("polymorph-self-action-name", ("target", entProto.Name)), metaDataCache);
        _metaData.SetEntityDescription(actionId.Value, Loc.GetString("polymorph-self-action-description", ("target", entProto.Name)), metaDataCache);

        if (!_actions.TryGetActionData(actionId, out var baseAction))
            return;

        baseAction.Icon = new SpriteSpecifier.EntityPrototype(polyProto.Configuration.Entity);
        if (baseAction is InstantActionComponent action)
            action.Event = new PolymorphActionEvent(id);
    }

    public void RemovePolymorphAction(ProtoId<PolymorphPrototype> id, Entity<PolymorphableComponent> target)
    {
        if (target.Comp.PolymorphActions == null)
            return;

        if (target.Comp.PolymorphActions.TryGetValue(id, out var val))
            _actions.RemoveAction(target, val);
    }
}
