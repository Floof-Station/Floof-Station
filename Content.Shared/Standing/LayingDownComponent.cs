using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Standing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LayingDownComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan StandingUpTime = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public float LyingSpeedModifier = 0.35f,
                 CrawlingUnderSpeedModifier = 0.5f;

    // Floofstation note - this is set by the server when the client lays down.
    [DataField, AutoNetworkedField]
    public bool AutoGetUp;

    /// <summary>
    ///     If true, the entity is choosing to crawl under furniture. This is purely visual and has no effect on physics.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsCrawlingUnder = false;

    [DataField, AutoNetworkedField]
    public int NormalDrawDepth = (int) DrawDepth.DrawDepth.Mobs,
               CrawlingUnderDrawDepth = (int) DrawDepth.DrawDepth.SmallMobs;
}

[Serializable, NetSerializable]
public sealed class ChangeLayingDownEvent : CancellableEntityEventArgs;

// Floofstation - removed. This is ChatGPT shitcode that never deserved to exist. A single client would set the AutoGetUp state of all other clients.
// Do not port. All usages of this have been removed.
// [Serializable, NetSerializable]
// public sealed class CheckAutoGetUpEvent(NetEntity user) : CancellableEntityEventArgs
// {
//     public NetEntity User = user;
// }
