using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Necronomicon.Components;
using Content.Server.Preferences.Managers;
using Content.Server.RoundEnd;
using Content.Shared.Antag;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Rules;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Necronomicon;
using Robust.Shared.Player;
using Content.Shared.Station.Systems;
using Robust.Shared.Audio;

namespace Content.Server.Necronomicon;

/// <summary>
/// Round-start rule for the Necronomicon antagonist.
/// One player becomes a Kandarian Demon. An old recording of the Necronomicon reading is already playing somewhere
/// aboard when the round starts; the demon wakes immediately and the book appears beside the recorder. Crew win by burning the book,
/// the Necronomicon wins when no eligible soul is left aboard the station.
/// </summary>
public sealed partial class NecronomiconRuleSystem : GameRuleSystem<NecronomiconRuleComponent>
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DeaditeSystem _deadite = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private NecronomiconSystem _necro = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Role loadout holding the player's chosen demon name (Character Setup → Antags → Loadout).</summary>
    public const string DemonLoadoutId = "AntagNecronomicon";
    public const string AppearanceGroupId = "NecroDemonAppearance";

    private static readonly SoundSpecifier VictorySound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecronomiconRuleComponent, AntagSelectLocationEvent>(OnSelectLocation);
        SubscribeLocalEvent<NecronomiconRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);

        SubscribeLocalEvent<NecroRecordingPlayedEvent>(OnRecordingPlayed);
        SubscribeLocalEvent<NecronomiconBurnedEvent>(OnBookBurned);
        SubscribeLocalEvent<NecroSoulConsumedEvent>(OnSoulConsumed);
    }

    private void OnSelectLocation(Entity<NecronomiconRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        // Chosen lazily so the station exists. Cached because selection may raise this more than once.
        if (ent.Comp.SpawnCoords == null && _station.TryFindRandomTile(out _, out _, out _, out var coords))
            ent.Comp.SpawnCoords = coords;

        if (ent.Comp.SpawnCoords is { } spawn)
            args.Coordinates.Add(_transform.ToMapCoordinates(spawn));
    }

    private void OnAntagSelected(Entity<NecronomiconRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var demonUid = args.EntityUid;
        if (!TryComp<NecroDemonComponent>(demonUid, out var demon))
        {
            Log.Error($"Necronomicon antag {ToPrettyString(demonUid)} has no {nameof(NecroDemonComponent)}");
            return;
        }

        ent.Comp.Demon = demonUid;

        ApplyLoadout(args.Session, (demonUid, demon));

        ent.Comp.DemonName = Name(demonUid);
        ent.Comp.DemonUsername = args.Session?.Name ?? string.Empty;
        EnsureComp<KandarianEntityComponent>(demonUid).Demon = demonUid;
        _deadite.GrantSumerian(demonUid);

        _necro.EnsureBook((demonUid, demon));

        // Lore: the old recording is already playing when the round starts, right where the demon is summoned.
        // The book stays in the ether until the demon chooses to manifest it.
        var recording = Spawn(ent.Comp.RecordingPrototype, Transform(demonUid).Coordinates);
        _transform.AttachToGridOrMap(recording);
        ent.Comp.Recording = recording;

        _necro.PlayRecordingAtRoundStart(recording);
        _necro.Awaken((demonUid, demon));
    }

    /// <summary>
    /// Reads the player's Kandarian Demon loadout: the alias (name) and the appearance.
    /// "My Character" makes the demon "Character Name (Alias)" and uses that character as the physical form.
    /// </summary>
    private void ApplyLoadout(ICommonSession? session, Entity<NecroDemonComponent> demon)
    {
        if (session == null || _prefs.GetPreferencesOrNull(session.UserId)?.SelectedCharacter is not { } profile)
            return;

        if (!profile.Loadouts.TryGetValue(DemonLoadoutId, out var loadout))
            return;

        // Alias: the chosen name, or the random one already rolled.
        if (!string.IsNullOrWhiteSpace(loadout.EntityName))
            _metaData.SetEntityName(demon, loadout.EntityName.Trim());

        var alias = Name(demon);

        if (!loadout.SelectedLoadouts.TryGetValue(AppearanceGroupId, out var picks) || picks.Count == 0)
            return;

        if (!ProtoMan.TryIndex(picks[0].Prototype, out var pickProto)
            || pickProto.DummyEntity is not { } dummyId
            || !ProtoMan.TryIndex(dummyId, out var dummy)
            || !dummy.TryComp<NecroAppearanceOptionComponent>(out var option, _factory))
            return;

        _appearance.SetData(demon, NecroDemonVisuals.Form, $"{option.Rsi}|{option.State}");

        if (option.UseCharacter)
        {
            demon.Comp.UseCharacterForm = true;
            demon.Comp.CharacterProfile = profile;
            _necro.SetCharacterGhost(demon, profile);
            _metaData.SetEntityName(demon, Loc.GetString("necro-character-name", ("character", profile.Name), ("alias", alias)));
        }
        else if (option.Form is { } form)
        {
            demon.Comp.SelectedForm = form;
        }
    }

    private void OnRecordingPlayed(ref NecroRecordingPlayedEvent args)
    {
        var query = EntityQueryEnumerator<NecronomiconRuleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out _, out var rule, out _))
        {
            if (rule.Outcome != NecroOutcome.Ongoing || !_necro.TryGetDemon(rule.Demon, out var demon))
                continue;

            if (demon.Value.Comp.Awakened)
                continue;

            _necro.Awaken(demon.Value);
        }
    }

    protected override void ActiveTick(EntityUid uid, NecronomiconRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.Outcome != NecroOutcome.Ongoing)
            return;

        var now = Timing.CurTime;

        if (now < component.NextCheck)
            return;

        component.NextCheck = now + component.CheckInterval;
        CheckNecronomiconVictory((uid, component));
    }

    #region Victory

    private void OnSoulConsumed(ref NecroSoulConsumedEvent args)
    {
        // Check promptly after each conversion instead of waiting for the next interval.
        var query = EntityQueryEnumerator<NecronomiconRuleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var uid, out var rule, out _))
        {
            if (rule.Demon == args.Demon)
                rule.NextCheck = TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Counts the eligible souls left aboard the station. Disconnected players still count (their bodies can be possessed),
    /// ghosted/abandoned and dead bodies don't, and minds are followed through transfers because we look at whoever
    /// currently owns each body.
    /// </summary>
    public int CountRemainingSouls()
    {
        var count = 0;
        var query = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (!_necro.IsEligibleSoul(uid))
                continue;

            if (_station.GetOwningStation(uid) == null)
                continue;

            count++;
        }

        return count;
    }

    private void CheckNecronomiconVictory(Entity<NecronomiconRuleComponent> rule)
    {
        if (!_necro.TryGetDemon(rule.Comp.Demon, out var demon))
            return;

        // Victory requires the demon to have actually consumed souls, the book to still exist, and nobody left.
        if (!demon.Value.Comp.Awakened || demon.Value.Comp.Banished || demon.Value.Comp.Conversions == 0)
            return;

        if (CountRemainingSouls() > 0)
            return;

        SetOutcome(rule, NecroOutcome.NecronomiconVictory);
    }

    private void OnBookBurned(ref NecronomiconBurnedEvent args)
    {
        var query = EntityQueryEnumerator<NecronomiconRuleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var uid, out var rule, out _))
        {
            if (rule.Demon != args.Demon)
                continue;

            SetOutcome((uid, rule), NecroOutcome.CrewVictory);
        }
    }

    /// <summary>The first outcome wins; later (simultaneous) ones are ignored.</summary>
    private void SetOutcome(Entity<NecronomiconRuleComponent> rule, NecroOutcome outcome)
    {
        if (rule.Comp.Outcome != NecroOutcome.Ongoing)
            return;

        rule.Comp.Outcome = outcome;

        if (_necro.TryGetDemon(rule.Comp.Demon, out var demon))
            SnapshotStats(rule, demon.Value);

        if (outcome == NecroOutcome.CrewVictory)
        {
            _chat.DispatchGlobalAnnouncement(Loc.GetString("necro-crew-victory-announcement"),
                Loc.GetString("necro-announcement-sender"), colorOverride: Color.Gold);

            // The demon is cast out: its player becomes a ghost.
            if (demon != null)
                CastOut(demon.Value);

            if (!_roundEnd.IsRoundEndRequested())
                _roundEnd.DoRoundEndBehavior(rule.Comp.CrewVictoryBehavior, rule.Comp.EvacShuttleTime);
        }
        else
        {
            _chat.DispatchGlobalAnnouncement(Loc.GetString("necro-victory-announcement"),
                Loc.GetString("necro-announcement-sender"), announcementSound: VictorySound, colorOverride: Color.DarkRed);

            _roundEnd.DoRoundEndBehavior(rule.Comp.NecroVictoryBehavior, rule.Comp.EvacShuttleTime);
        }
    }

    private void SnapshotStats(Entity<NecronomiconRuleComponent> rule, Entity<NecroDemonComponent> demon)
    {
        rule.Comp.FinalConversions = demon.Comp.Conversions;
        rule.Comp.FinalConsumedNames = new List<string>(demon.Comp.ConsumedNames);
        rule.Comp.FinalDemonspawn = demon.Comp.DemonspawnSummoned;
        rule.Comp.FinalFear = demon.Comp.TotalFearEarned;
        rule.Comp.FinalPuppetPossessions = demon.Comp.PuppetPossessions;
        rule.Comp.FinalDemonspawnMatured = demon.Comp.DemonspawnMatured;
        rule.Comp.FinalRituals = demon.Comp.RitualsPerformed;
        rule.Comp.FinalExorcisms = demon.Comp.Exorcisms;
        rule.Comp.FinalExorcismAttempts = demon.Comp.ExorcismAttempts;
        rule.Comp.FinalSoulPower = demon.Comp.TotalSoulPowerEarned;
        rule.Comp.FinalBanished = demon.Comp.Banished;

        if (demon.Comp.AwakenedAt is { } awake)
        {
            var end = demon.Comp.BanishedAt ?? Timing.CurTime;
            rule.Comp.FinalTimeSurvived = end > awake ? end - awake : TimeSpan.Zero;
        }
    }

    private void CastOut(Entity<NecroDemonComponent> demon)
    {
        var body = demon.Comp.Manifestation ?? demon.Owner;
        if (_mind.TryGetMind(body, out var mindId, out var mind))
            _ghost.SpawnGhost((mindId, mind), body);

        if (demon.Comp.Manifestation is { } form)
            QueueDel(form);

        QueueDel(demon);
    }

    #endregion

    protected override void AppendRoundEndText(Entity<NecronomiconRuleComponent> rule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(rule, ref args);

        // Use live numbers if the demon is still around, otherwise the snapshot taken when the round was decided.
        if (_necro.TryGetDemon(rule.Comp.Demon, out var demon))
            SnapshotStats(rule, demon.Value);

        var c = rule.Comp;
        var outcome = c.Outcome switch
        {
            NecroOutcome.CrewVictory => "necro-round-end-crew-victory",
            NecroOutcome.NecronomiconVictory => "necro-round-end-necro-victory",
            _ => "necro-round-end-ongoing",
        };

        args.AddLine(Loc.GetString("necro-round-end-header"));
        args.AddLine(Loc.GetString(outcome));
        args.AddLine(Loc.GetString("necro-round-end-demon", ("name", c.DemonName), ("username", c.DemonUsername)));

        var survived = c.FinalTimeSurvived;
        args.AddLine(Loc.GetString(c.FinalBanished ? "necro-round-end-time-banished" : "necro-round-end-time-survived",
            ("minutes", (int) survived.TotalMinutes),
            ("seconds", survived.Seconds)));

        args.AddLine(Loc.GetString("necro-round-end-stat-souls", ("count", c.FinalConversions)));
        args.AddLine(Loc.GetString("necro-round-end-stat-possessions",
            ("count", c.FinalConversions + c.FinalPuppetPossessions),
            ("deadite", c.FinalConversions),
            ("puppet", c.FinalPuppetPossessions)));
        foreach (var name in c.FinalConsumedNames)
        {
            args.AddLine(Loc.GetString("necro-round-end-deadite", ("name", name)));
        }

        args.AddLine(Loc.GetString("necro-round-end-stat-demonspawn",
            ("count", c.FinalDemonspawn),
            ("matured", c.FinalDemonspawnMatured)));
        args.AddLine(Loc.GetString("necro-round-end-stat-fear",
            ("fear", c.FinalFear.Int()),
            ("sp", c.FinalSoulPower.Int())));
        args.AddLine(Loc.GetString("necro-round-end-stat-rituals",
            ("rituals", c.FinalRituals),
            ("exorcisms", c.FinalExorcisms),
            ("attempts", c.FinalExorcismAttempts)));
        args.AddLine(Loc.GetString("necro-round-end-survivors", ("count", CountRemainingSouls())));
        args.AddLine("");
    }
}
