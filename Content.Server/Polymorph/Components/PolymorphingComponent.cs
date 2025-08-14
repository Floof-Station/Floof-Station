namespace Content.Server.Polymorph.Components;

/// <summary>
/// Denotes whether the entity is currently mid-polymorph.
/// Allows for cancelling certain events like DownAttemptEvent when the brain is removed.
/// </summary>
[RegisterComponent]
public sealed partial class PolymorphingComponent : Component;
