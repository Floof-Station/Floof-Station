using Content.Shared.Standing;
using Content.Shared.CCVar;
using Content.Shared.Input;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Server.Standing;

public sealed class LayingDownSystem : SharedLayingDownSystem
{
    // Floofstation - everything here was changed.
    [Dependency] private readonly INetConfigurationManager _netCfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LayingDownComponent, DownedEvent>(OnDowned);
    }

    private void OnDowned(Entity<LayingDownComponent> ent, ref DownedEvent args)
    {
        // The original system used to let CLIENTS force their auto get up state on ALL OTHER CLIENTS
        // And that's exactly what would happen. Any time someone gets downed, all clients who see it would raise an AutoGetUpEvent over the network, changing the downed person's auto getup state
        // The new system only sets it on the server.
        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var autoGetUp = _netCfg.GetClientCVar(actor.PlayerSession.Channel, CCVars.AutoGetUp);
        if (autoGetUp == ent.Comp.AutoGetUp)
            return;

        ent.Comp.AutoGetUp = autoGetUp;
        Dirty(ent);
    }
}
