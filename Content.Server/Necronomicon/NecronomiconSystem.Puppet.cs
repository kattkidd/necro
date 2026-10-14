using Content.Server.Chat.Systems;
using Content.Server.Necronomicon.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.Station.Systems;
using Content.Shared.Throwing;

namespace Content.Server.Necronomicon;

/// <summary>
/// Harmless demonic possession ("puppeting"), separate from Deadite conversion.
/// The host collapses and cannot act or speak for themselves; everything the demon says comes out of the host's mouth.
/// While puppeting, the demon can turn one sentence into a station announcement signed with the host's name.
/// No damage, no conversion, no charges used.
/// </summary>
public sealed partial class NecronomiconSystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private StationSystem _station = default!;

    /// <summary>Speech captured from demons, sent through their hosts on the next tick (never re-entrantly from chat code).</summary>
    private readonly List<(EntityUid Demon, EntityUid Host, string Message, bool Announce)> _puppetSpeech = new();

    private void InitializePuppet()
    {
        SubscribeLocalEvent<NecroDemonComponent, NecroPuppetActionEvent>(OnPuppetAction);
        SubscribeLocalEvent<NecroDemonComponent, NecroPuppetAnnounceActionEvent>(OnPuppetAnnounceAction);
        SubscribeLocalEvent<NecroDemonComponent, TransformSpeechEvent>(OnDemonTransformSpeech);

        // The host is incapacitated: can't move, stand, act or talk for themselves.
        SubscribeLocalEvent<NecroPuppetHostComponent, UpdateCanMoveEvent>(OnHostBlock);
        SubscribeLocalEvent<NecroPuppetHostComponent, StandAttemptEvent>(OnHostBlock);
        SubscribeLocalEvent<NecroPuppetHostComponent, UseAttemptEvent>(OnHostBlock);
        SubscribeLocalEvent<NecroPuppetHostComponent, AttackAttemptEvent>(OnHostBlock);
        SubscribeLocalEvent<NecroPuppetHostComponent, PickupAttemptEvent>(OnHostBlock);
        SubscribeLocalEvent<NecroPuppetHostComponent, ThrowAttemptEvent>(OnHostBlock);
        SubscribeLocalEvent<NecroPuppetHostComponent, SpeakAttemptEvent>(OnHostBlock);
        SubscribeLocalEvent<NecroPuppetHostComponent, InteractionAttemptEvent>(OnHostInteract);
        SubscribeLocalEvent<NecroPuppetHostComponent, MobStateChangedEvent>(OnHostMobState);
        SubscribeLocalEvent<NecroPuppetHostComponent, EntityTerminatingEvent>(OnHostTerminating);
    }

    private void OnPuppetAction(Entity<NecroDemonComponent> ent, ref NecroPuppetActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        var target = args.Target;

        // Targeting your current host lets them go.
        if (ent.Comp.PuppetHost == target)
        {
            args.Handled = true;
            EndPuppet(ent);
            return;
        }

        if (ent.Comp.PuppetHost != null)
        {
            _popup.PopupEntity(Loc.GetString("necro-puppet-busy"), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        if (_timing.CurTime < ent.Comp.NextPuppet)
        {
            _popup.PopupEntity(Loc.GetString("necro-puppet-cooldown",
                    ("seconds", (int) (ent.Comp.NextPuppet - _timing.CurTime).TotalSeconds)),
                args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        // Anyone with a body will do: mindshields don't stop this, it is only temporary.
        if (!HasComp<HumanoidProfileComponent>(target) || IsKandarian(target) || !_mobState.IsAlive(target)
            || HasComp<NecroPuppetHostComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("necro-target-invalid"), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!_transform.InRange(Transform(args.Performer).Coordinates, Transform(target).Coordinates, ent.Comp.PuppetRange))
        {
            _popup.PopupEntity(Loc.GetString("necro-too-far"), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!TrySpendSoulPower(ent, ent.Comp.PuppetCost, args.Performer))
            return;

        args.Handled = true;
        StartPuppet(ent, target);
    }

    public void StartPuppet(Entity<NecroDemonComponent> demon, EntityUid host)
    {
        var hostComp = EnsureComp<NecroPuppetHostComponent>(host);
        hostComp.Demon = demon;
        demon.Comp.PuppetHost = host;
        demon.Comp.PuppetPossessions++;
        demon.Comp.PuppetEndAt = _timing.CurTime + demon.Comp.PuppetDuration;
        demon.Comp.NextPuppet = _timing.CurTime + demon.Comp.PuppetCooldown;
        demon.Comp.PuppetAnnounceNext = false;

        _standing.Down(host, dropHeldItems: true, force: true);
        _blocker.UpdateCanMove(host);
        _jitter.DoJitter(host, TimeSpan.FromSeconds(3), true, 10f, 6f);

        _popup.PopupEntity(Loc.GetString("necro-puppet-start-others", ("target", Identity.Entity(host, EntityManager))),
            host, Robust.Shared.Player.Filter.PvsExcept(host), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("necro-puppet-start-host"), host, host, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("necro-puppet-start-demon",
                ("target", Identity.Entity(host, EntityManager)),
                ("seconds", (int) demon.Comp.PuppetDuration.TotalSeconds)),
            demon, demon, PopupType.Medium);

        AddFearAround(demon, host, demon.Comp.WitnessRange, NecroFearSource.Supernatural, exclude: host);
    }

    public void EndPuppet(Entity<NecroDemonComponent> demon, bool popup = true)
    {
        if (demon.Comp.PuppetHost is not { } host)
            return;

        demon.Comp.PuppetHost = null;
        demon.Comp.PuppetAnnounceNext = false;

        if (TerminatingOrDeleted(host))
            return;

        RemComp<NecroPuppetHostComponent>(host);
        _blocker.UpdateCanMove(host);

        if (_mobState.IsAlive(host))
            _standing.Stand(host);

        if (!popup)
            return;

        _popup.PopupEntity(Loc.GetString("necro-puppet-end-host"), host, host, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("necro-puppet-end-demon"), demon, demon, PopupType.Medium);
    }

    private void OnPuppetAnnounceAction(Entity<NecroDemonComponent> ent, ref NecroPuppetAnnounceActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        if (ent.Comp.PuppetHost == null)
        {
            _popup.PopupEntity(Loc.GetString("necro-puppet-not-possessing"), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        if (_timing.CurTime < ent.Comp.NextPuppetAnnounce)
        {
            _popup.PopupEntity(Loc.GetString("necro-puppet-announce-cooldown",
                    ("seconds", (int) (ent.Comp.NextPuppetAnnounce - _timing.CurTime).TotalSeconds)),
                args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        // Paid when the announcement is actually made.
        args.Handled = true;
        ent.Comp.PuppetAnnounceNext = !ent.Comp.PuppetAnnounceNext;
        _popup.PopupEntity(Loc.GetString(ent.Comp.PuppetAnnounceNext ? "necro-puppet-announce-armed" : "necro-puppet-announce-disarmed"),
            args.Performer, args.Performer, PopupType.Medium);
    }

    /// <summary>
    /// While puppeting, the demon's own voice is swallowed and the words are queued to come out of the host instead.
    /// </summary>
    private void OnDemonTransformSpeech(Entity<NecroDemonComponent> ent, ref TransformSpeechEvent args)
    {
        if (ent.Comp.PuppetHost is not { } host || string.IsNullOrWhiteSpace(args.Message))
            return;

        _puppetSpeech.Add((ent.Owner, host, args.Message, ent.Comp.PuppetAnnounceNext));
        ent.Comp.PuppetAnnounceNext = false;
        args.Message = string.Empty; // nothing is said from the demon's position
    }

    private void UpdatePuppets(TimeSpan now)
    {
        // Deliver queued speech first, so the last line before the timer runs out still gets said.
        if (_puppetSpeech.Count > 0)
        {
            var queued = new List<(EntityUid Demon, EntityUid Host, string Message, bool Announce)>(_puppetSpeech);
            _puppetSpeech.Clear();

            foreach (var (demonUid, host, message, announce) in queued)
            {
                if (!TryComp<NecroDemonComponent>(demonUid, out var demon) || demon.PuppetHost != host
                    || TerminatingOrDeleted(host))
                    continue;

                if (announce && TrySpendSoulPower((demonUid, demon), demon.PuppetAnnounceCost, demonUid))
                {
                    demon.NextPuppetAnnounce = now + demon.PuppetAnnounceCooldown;
                    AnnounceAs(host, message);
                }
                else
                {
                    // Spoken aloud by the host. Bypasses their incapacitation; nothing else can.
                    _chat.TrySendInGameICMessage(host, message, InGameICChatType.Speak, ChatTransmitRange.Normal,
                        checkRadioPrefix: false, ignoreActionBlocker: true);
                }
            }
        }

        var query = EntityQueryEnumerator<NecroDemonComponent>();
        while (query.MoveNext(out var uid, out var demon))
        {
            if (demon.PuppetHost is not { } host)
                continue;

            if (demon.Banished || now >= demon.PuppetEndAt || TerminatingOrDeleted(host) || !_mobState.IsAlive(host))
                EndPuppet((uid, demon));
        }
    }

    /// <summary>A station-wide announcement, signed with the host's apparent name.</summary>
    private void AnnounceAs(EntityUid host, string message)
    {
        var sender = Identity.Name(host, EntityManager);

        if (_station.GetOwningStation(host) is { } station)
            _chat.DispatchStationAnnouncement(station, message, sender, colorOverride: Color.Gold);
        else
            _chat.DispatchGlobalAnnouncement(message, sender, colorOverride: Color.Gold);
    }

    private void OnHostBlock<T>(Entity<NecroPuppetHostComponent> ent, ref T args) where T : CancellableEntityEventArgs
    {
        args.Cancel();
    }

    private void OnHostInteract(Entity<NecroPuppetHostComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnHostMobState(EntityUid uid, NecroPuppetHostComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive || !TryGetDemon(comp.Demon, out var demon))
            return;

        EndPuppet(demon.Value);
    }

    private void OnHostTerminating(Entity<NecroPuppetHostComponent> ent, ref EntityTerminatingEvent args)
    {
        if (TryGetDemon(ent.Comp.Demon, out var demon) && demon.Value.Comp.PuppetHost == ent.Owner)
            demon.Value.Comp.PuppetHost = null;
    }
}
