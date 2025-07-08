using Content.Server.GameTicking;
using Content.Shared.Chemistry.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mood;
using Robust.Shared.Timing;
using FavoriteDrinkComponent = Content.Server._Floof.Preferences.FavoriteDrinkComponent;


namespace Content.Server._Floof.Traits;

public sealed class FavoriteDrinkSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.Profile.FavoriteDrink is not { } favoriteDrink)
            return;

        var comp = EnsureComp<FavoriteDrinkComponent>(args.Mob);
        comp.ReagentId = favoriteDrink;
    }

    public void MaybeFavoriteDrinkReaction(EntityUid target, Solution solution)
    {
        if (!TryComp(target, out FavoriteDrinkComponent? favoriteDrink))
            return;

        if (!solution.TryGetReagent(new(favoriteDrink.ReagentId, null), out var quantity))
            return;

        // minimum threshold for mood effect (same as ice cream)
        // notable difference here is that ice cream's effect occurs when metabolized
        // whereas this occurs when ingested, so 5 is also the maximum amount you can drink at once
        if (quantity.Quantity < 5)
            return;

        // raise after a timeout so the flytext doesn't overlap
        Timer.Spawn(1000, () => RaiseLocalEvent(target, new MoodEffectEvent("FavoriteDrink")));
    }
}
