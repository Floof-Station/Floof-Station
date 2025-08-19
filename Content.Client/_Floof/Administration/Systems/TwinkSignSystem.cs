using System.Numerics;
using Content.Client._Floof.Administration.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Floof.Administration.Systems;

// This is just a copy-pasted version of the kill sign system with some find and replace
public sealed class TwinkSignSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<TwinkSignComponent, ComponentStartup>(TwinkSignAdded);
        SubscribeLocalEvent<TwinkSignComponent, ComponentShutdown>(TwinkSignRemoved);
    }

    private void TwinkSignRemoved(EntityUid uid, TwinkSignComponent component, ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!sprite.LayerMapTryGet(TwinkSignKey.Key, out var layer))
            return;

        sprite.RemoveLayer(layer);
    }

    private void TwinkSignAdded(EntityUid uid, TwinkSignComponent component, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (sprite.LayerMapTryGet(TwinkSignKey.Key, out var _))
            return;

        var adj = sprite.Bounds.Height / 2 + ((1.0f/32) * 6.0f);

        var layer = sprite.AddLayer(new SpriteSpecifier.Rsi(new ResPath("_Floof/Objects/Misc/twinksign.rsi"), "sign"));
        sprite.LayerMapSet(TwinkSignKey.Key, layer);

        sprite.LayerSetOffset(layer, new Vector2(0.0f, adj));
        sprite.LayerSetShader(layer, "unshaded");
    }

    private enum TwinkSignKey
    {
        Key,
    }
}
