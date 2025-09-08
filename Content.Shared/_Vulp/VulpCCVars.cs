using Robust.Shared;
using Robust.Shared.Configuration;


namespace Content.Shared._Vulp;


[CVarDefs]
public sealed class VulpCCVars
{
    /// <summary>
    ///     Whether to start a public vote before automatically sending the emergency shuttle.
    /// </summary>
    public static readonly CVarDef<bool> DoEvacVotes =
        CVarDef.Create("shuttle.emergency_do_evac_votes", true, CVar.SERVER);

    /// <summary>
    ///     The duration of the public vote before automatically sending the emergency shuttle.
    /// </summary>
    public static readonly CVarDef<float> EvacVoteDuration =
        CVarDef.Create("shuttle.emergency_vote_duration", 120f, CVar.SERVER);
}
