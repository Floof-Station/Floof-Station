using Content.Server.Chat.Managers;
using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.Weather;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;


namespace Content.Server._Vulp.Weather.Functions;


[DataDefinition, Serializable]
public sealed partial class WeatherDispatchAnnouncement : WeatherFunctionWithDelay
{
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Dataset;

    [DataField]
    public Color? ColorOverride;

    public override bool InvokeOnRepeatedTraversal => false;

    protected override void Fire(
        EntityManager entMan,
        Entity<WeatherComponent, WeatherCycleComponent> ent,
        float updateTimeSeconds)
    {
        if (!entMan.TransformQuery.TryGetComponent(ent, out var xform))
            return;

        var actBlocker = entMan.System<ActionBlockerSystem>();
        var filter = Filter
            .BroadcastMap(xform.MapID)
            .RemoveWhereAttachedEntity(it => !actBlocker.CanConsciouslyPerformAction(it)); // Gotta see it after all

        if (!IoCManager.Resolve<IPrototypeManager>().TryIndex(Dataset, out var messages))
            return;

        var message = Loc.GetString(IoCManager.Resolve<IRobustRandom>().Pick(messages.Values));
        IoCManager.Resolve<IChatManager>()
            .ChatMessageToManyFiltered(filter, ChatChannel.Notifications, message, message, EntityUid.Invalid, false, true, ColorOverride);
    }
}
