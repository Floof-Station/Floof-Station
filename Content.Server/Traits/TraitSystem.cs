using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Systems;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Customization.Systems;
using Content.Shared.Database;
using Content.Shared.Players;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Traits;

public sealed class TraitSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly CharacterRequirementsSystem _characterRequirements = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly AdminSystem _adminSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    // When the player is spawned in, add all trait components selected during character creation
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // This method has mostly been rewritten on floofstation.
        var pointsTotal = _configuration.GetCVar(CCVars.GameTraitsDefaultPoints);
        var traitSelections = _configuration.GetCVar(CCVars.GameTraitsMax);

        JobPrototype? jobPrototype = null;
        if (args.JobId is not null && _prototype.TryIndex(args.JobId, out jobPrototype) && !jobPrototype.ApplyTraits)
            return;
        jobPrototype ??= _prototype.Index<JobPrototype>("Passenger"); // Fallback

        // Step 1. Figure out which traits will actually apply.
        var sortedTraits = new List<TraitPrototype>();
        var discardedTraits = new List<TraitPrototype>();
        foreach (var traitId in args.Profile.TraitPreferences)
        {
            if (!_prototype.TryIndex<TraitPrototype>(traitId, out var traitPrototype))
            {
                DebugTools.Assert($"No trait found with ID {traitId}!");
                return;
            }

            if (!_characterRequirements.CheckRequirementsValid(
                traitPrototype.Requirements,
                jobPrototype,
                args.Profile, _playTimeTracking.GetTrackerTimes(args.Player), args.Player.ContentData()?.Whitelisted ?? false, traitPrototype,
                EntityManager, _prototype, _configuration,
                out _))
            {
                discardedTraits.Add(traitPrototype);
                continue;
            }

            sortedTraits.Add(traitPrototype);
        }

        // Step 2. sort by points added so that we never go into negative balance unless the player already has a negative total.
        // Eliminate any trait that would cause us to go into negative balance.
        sortedTraits.Sort((a, b) => a.Points.CompareTo(b.Points));
        for (int i = sortedTraits.Count - 1; i >= 0; i--)
        {
            var traitPrototype = sortedTraits[i];
            if (pointsTotal + traitPrototype.Points < 0 || traitSelections - traitPrototype.Slots < 0)
            {
                // Note: this mandates reverse iteration.
                sortedTraits.RemoveSwap(i);
                discardedTraits.Add(traitPrototype);
                Log.Debug($"Eliminating trait {traitPrototype.ID} at index {i}");
                continue;
            }

            pointsTotal += traitPrototype.Points;
            traitSelections -= traitPrototype.Slots;
        }

        // Step 3. finally apply all the traits that passed this trial by fire, sorting by their programmatic priority.
        sortedTraits.Sort();
        foreach (var traitPrototype in sortedTraits)
            AddTrait(args.Mob, traitPrototype);

        // This is just so I can know if I fucked up again and broke someone's character.
        if (pointsTotal < 0 || traitSelections < 0 || discardedTraits.Count > 0)
        {
            Log.Warning($"Player {args.Player.Name} tried to spawn with a negative balance: {discardedTraits.Count} discarded, {pointsTotal} points, {traitSelections} selections.");

            var msg = $"Warning: {discardedTraits.Count} of your traits failed to apply due to insufficient trait balance or missing requirements: " +
                $"{string.Join(", ", discardedTraits.Select(t => t.ID))}.";
            _chatManager.ChatMessageToOne(
                ChatChannel.Server,
                msg, msg,
                EntityUid.Invalid,
                false,
                args.Player.Channel,
                Color.Orange);
        }
    }

    /// <summary>
    ///     Adds a single Trait Prototype to an Entity.
    /// </summary>
    public void AddTrait(EntityUid uid, TraitPrototype traitPrototype)
    {
        foreach (var function in traitPrototype.Functions)
            function.OnPlayerSpawn(uid, _componentFactory, EntityManager, _serialization);
    }

    /// <summary>
    ///     On a non-cheating client, it's not possible to save a character with a negative number of traits. This can however
    ///     --- Floofstation note: YES IT IS VERY MUCH POSSIBLE.
    ///     trigger incorrectly if a character was saved, and then at a later point in time an admin changes the traits Cvars to reduce the points.
    ///     Or if the points costs of traits is increased.
    /// </summary>
    [Obsolete("Do not use. Edit the system to skip any new traits if their addition results in the character going below 0 trait points/selections.", error: true)] // Floof
    private void PunishCheater(EntityUid uid)
    {
        _adminLog.Add(LogType.AdminMessage, LogImpact.High,
            $"{ToPrettyString(uid):entity} attempted to spawn with an invalid trait list. This might be a mistake, or they might be cheating");

        if (!_configuration.GetCVar(CCVars.TraitsPunishCheaters)
            || !_playerManager.TryGetSessionByEntity(uid, out var targetPlayer))
            return;

        // For maximum comedic effect, this is plenty of time for the cheater to get on station and start interacting with people.
        var timeToDestroy = _random.NextFloat(120, 360);

        Timer.Spawn(TimeSpan.FromSeconds(timeToDestroy), () => VaporizeCheater(targetPlayer));
    }

    /// <summary>
    ///     https://www.youtube.com/watch?v=X2QMN0a_TrA
    /// </summary>
    private void VaporizeCheater (Robust.Shared.Player.ICommonSession targetPlayer)
    {
        // Floof - M3739 | Get NetUserID of the targetPlayer.
        var targetUser = targetPlayer.UserId;
        _adminSystem.Erase(targetUser);

        var feedbackMessage = $"[font size=24][color=#ff0000]{"You have spawned in with an illegal trait point total. If this was a result of cheats, then your nonexistence is a skill issue. Otherwise, feel free to click 'Return To Lobby', and fix your trait selections."}[/color][/font]";
        _chatManager.ChatMessageToOne(
            ChatChannel.Emotes,
            feedbackMessage,
            feedbackMessage,
            EntityUid.Invalid,
            false,
            targetPlayer.Channel);
    }
}
