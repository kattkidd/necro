using Content.Server.Mind;
using Content.Server.Necronomicon.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Jittering;
using Content.Shared.Mindshield;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Chat;
using Robust.Shared.Timing;

namespace Content.Server.Necronomicon;

/// <summary>
/// Deadites (possessed crew) and the regeneration shared with Demonspawn.
/// </summary>
public sealed partial class DeaditeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MindShieldSystem _mindShield = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private NameModifierSystem _nameMod = default!;
    [Dependency] private NecronomiconSystem _necro = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ReplacementAccentSystem _replacementAccent = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public static readonly EntProtoId DeaditeMindRole = "MindRoleDeadite";
    public const string SumerianChannel = "Sumerian";

    private static readonly SoundSpecifier RevealSound = new SoundCollectionSpecifier("DeaditeLaughs");
    private static readonly SoundSpecifier SnarlSound = new SoundCollectionSpecifier("DeaditeSnarls");
    private static readonly SoundSpecifier ScreamSound = new SoundCollectionSpecifier("DeaditeScreams");
    private static readonly SoundSpecifier TalkSound = new SoundCollectionSpecifier("DeaditeTalk");
    private static readonly SoundSpecifier LaughSound = new SoundCollectionSpecifier("DeaditeLaughs");
    private static readonly SoundSpecifier PossessedSound = new SoundPathSpecifier("/Audio/Necronomicon/deadite_deaditepossessed.ogg");
    private static readonly SoundSpecifier ShieldMeltSound = new SoundPathSpecifier("/Audio/Effects/desecration-03.ogg");
    private static readonly SoundSpecifier BiteSound = new SoundPathSpecifier("/Audio/Effects/bite.ogg");

    /// <summary>Dead threshold used so ordinary damage never kills a Deadite.</summary>
    private static readonly FixedPoint2 UndyingThreshold = 100000;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeaditeComponent, DeaditeRevealActionEvent>(OnReveal);
        SubscribeLocalEvent<DeaditeComponent, DeaditeBiteActionEvent>(OnBite);
        SubscribeLocalEvent<DeaditeComponent, DeaditeReadPassageActionEvent>(OnReadPassage);
        SubscribeLocalEvent<DeaditeComponent, DeaditeReadPassageDoAfterEvent>(OnReadPassageDoAfter);

        SubscribeLocalEvent<DeaditeComponent, RefreshNameModifiersEvent>(OnRefreshName);
        SubscribeLocalEvent<DeaditeComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<DeaditeComponent, MeleeHitEvent>(OnDeaditeMeleeHit);
        SubscribeLocalEvent<DeaditeComponent, DamageChangedEvent>(OnDeaditeDamaged);
        SubscribeLocalEvent<DeaditeComponent, BeforeDamageChangedEvent>(OnDeaditeBeforeDamage);

        // Severed head: no moving, using, attacking, picking up or throwing - but still talking.
        SubscribeLocalEvent<DeaditeComponent, UpdateCanMoveEvent>(OnSeveredBlock);
        SubscribeLocalEvent<DeaditeComponent, UseAttemptEvent>(OnSeveredBlock);
        SubscribeLocalEvent<DeaditeComponent, AttackAttemptEvent>(OnSeveredBlock);
        SubscribeLocalEvent<DeaditeComponent, PickupAttemptEvent>(OnSeveredBlock);
        SubscribeLocalEvent<DeaditeComponent, ThrowAttemptEvent>(OnSeveredBlock);
        SubscribeLocalEvent<DeaditeComponent, InteractionAttemptEvent>(OnSeveredInteract);

        SubscribeLocalEvent<DeaditeComponent, EntitySpokeEvent>(OnDeaditeSpoke);

        SubscribeLocalEvent<KandarianRegenComponent, MobStateChangedEvent>(OnRegenMobState);

        SubscribeLocalEvent<NecronomiconBurnedEvent>(OnBookBurned);

        SubscribeLocalEvent<NecroDemonRoleComponent, GetBriefingEvent>(OnDemonBriefing);
        SubscribeLocalEvent<DeaditeRoleComponent, GetBriefingEvent>(OnDeaditeBriefing);
        SubscribeLocalEvent<DemonspawnRoleComponent, GetBriefingEvent>(OnDemonspawnBriefing);
    }

    private void OnDemonBriefing(Entity<NecroDemonRoleComponent> ent, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString("necro-briefing-character"));
    }

    private void OnDeaditeBriefing(Entity<DeaditeRoleComponent> ent, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString("deadite-briefing"));
    }

    private void OnDemonspawnBriefing(Entity<DemonspawnRoleComponent> ent, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString("demonspawn-briefing"));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Revealed Deadites cackle now and then.
        var laughers = EntityQueryEnumerator<DeaditeComponent>();
        while (laughers.MoveNext(out var laugher, out var deadite))
        {
            if (!deadite.Revealed || deadite.Severed || now < deadite.NextLaugh || !_mobState.IsAlive(laugher))
                continue;

            deadite.NextLaugh = now + TimeSpan.FromSeconds(_random.Next(40, 90));
            _audio.PlayPvs(LaughSound, laugher);
        }

        var query = EntityQueryEnumerator<KandarianRegenComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var regen, out var mob))
        {
            if (TryComp<DeaditeComponent>(uid, out var deadite) && deadite.Severed)
                continue;

            if (mob.CurrentState == MobState.Critical || mob.CurrentState == MobState.Dead)
            {
                regen.ReviveAt ??= now + regen.ReviveDelay;
                if (now < regen.ReviveAt)
                    continue;

                regen.ReviveAt = null;
                _damageable.ClearAllDamage(uid);
                _mobState.ChangeMobState(uid, MobState.Alive, mob);
                _audio.PlayPvs(ScreamSound, uid);
                _popup.PopupEntity(Loc.GetString("deadite-rises", ("target", Identity.Entity(uid, EntityManager))),
                    uid, PopupType.LargeCaution);

                if (TryComp<KandarianEntityComponent>(uid, out var kandarian)
                    && _necro.TryGetDemon(kandarian.Demon, out var demon))
                {
                    _necro.AddFearAround(demon.Value, uid, demon.Value.Comp.WitnessRange, NecroFearSource.Supernatural);
                }

                continue;
            }

            regen.ReviveAt = null;
            if (now < regen.NextTick)
                continue;

            regen.NextTick = now + TimeSpan.FromSeconds(1);
            if (_damageable.GetTotalDamage(uid) > FixedPoint2.Zero)
                _damageable.HealEvenly(uid, -regen.PassiveHeal);
        }
    }

    #region Conversion

    /// <summary>
    /// Turns a crew member into a disguised Deadite. Nothing visible changes until they choose to reveal.
    /// </summary>
    public void MakeDeadite(EntityUid target, Entity<NecroDemonComponent> demon)
    {
        var deadite = EnsureComp<DeaditeComponent>(target);
        deadite.Demon = demon;
        EnsureComp<KandarianEntityComponent>(target).Demon = demon;
        EnsureComp<KandarianRegenComponent>(target);

        if (_thresholds.TryGetDeadThreshold(target, out var dead))
        {
            deadite.OriginalDeadThreshold = dead;
            _thresholds.SetMobStateThreshold(target, UndyingThreshold, MobState.Dead);
        }

        // Possession heals and stands them back up, wherever they were.
        _damageable.ClearAllDamage(target);
        _mobState.ChangeMobState(target, MobState.Alive);

        GrantSumerian(target);

        _actions.AddAction(target, ref deadite.RevealActionEntity, deadite.RevealAction);
        _actions.AddAction(target, ref deadite.BiteActionEntity, deadite.BiteAction);
        _actions.AddAction(target, ref deadite.ReadPassageActionEntity, deadite.ReadPassageAction);

        if (_mind.TryGetMind(target, out var mindId, out _))
            _roles.MindAddRole(mindId, DeaditeMindRole);

        // Only the victim notices.
        _popup.PopupEntity(Loc.GetString("deadite-converted"), target, target, PopupType.LargeCaution);
        _audio.PlayGlobal(PossessedSound, target);
    }

    /// <summary>Gives the private, unlimited-range Ancient Sumerian channel.</summary>
    public void GrantSumerian(EntityUid uid)
    {
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(uid);
        transmitter.Channels.Clear();
        transmitter.Channels.Add(SumerianChannel);

        EnsureComp<IntrinsicRadioReceiverComponent>(uid);
        var active = EnsureComp<ActiveRadioComponent>(uid);
        active.Channels.Add(SumerianChannel);
    }

    #endregion

    #region Reveal

    private void OnReveal(Entity<DeaditeComponent> ent, ref DeaditeRevealActionEvent args)
    {
        if (args.Handled || ent.Comp.Revealed || ent.Comp.Severed)
            return;

        args.Handled = true;
        Reveal(ent);
    }

    public void Reveal(Entity<DeaditeComponent> ent)
    {
        if (ent.Comp.Revealed)
            return;

        ent.Comp.Revealed = true;
        ent.Comp.NextLaugh = _timing.CurTime + TimeSpan.FromSeconds(45);

        // Pale skin, completely white eyes.
        if (_visualBody.TryGatherMarkingsData(ent.Owner, null, out var profiles, out _, out _))
        {
            var pale = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>();
            foreach (var (category, profile) in profiles)
            {
                pale[category] = profile with { SkinColor = ent.Comp.SkinColor, EyeColor = ent.Comp.EyeColor };
            }
            _visualBody.ApplyProfiles(ent, pale);
        }

        // Loud and vulgar.
        EnsureComp<AllCapsAccentComponent>(ent);
        _replacementAccent.ApplyAccent(ent, ent.Comp.Accent);

        _nameMod.RefreshNameModifiers(ent.Owner);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);

        // The reveal action is spent.
        _actions.RemoveAction(ent.Owner, ent.Comp.RevealActionEntity);
        ent.Comp.RevealActionEntity = null;

        _audio.PlayPvs(RevealSound, ent);
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(3), true, 15f, 8f);
        _popup.PopupEntity(Loc.GetString("deadite-reveal", ("target", Identity.Entity(ent, EntityManager))),
            ent, PopupType.LargeCaution);

        if (_necro.TryGetDemon(ent.Comp.Demon, out var demon))
        {
            _necro.AddSoulPower(demon.Value, ent.Comp.RevealReward);
            _necro.AddFearAround(demon.Value, ent, demon.Value.Comp.WitnessRange, NecroFearSource.Reveal);
        }
    }

    private void OnDeaditeSpoke(Entity<DeaditeComponent> ent, ref EntitySpokeEvent args)
    {
        // Disguised Deadites sound normal. Radio (Sumerian) is silent.
        if (!ent.Comp.Revealed || args.Channel != null)
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextTalkSound)
            return;

        ent.Comp.NextTalkSound = now + TimeSpan.FromSeconds(2);
        _audio.PlayPvs(TalkSound, ent);
    }

    private void OnRefreshName(Entity<DeaditeComponent> ent, ref RefreshNameModifiersEvent args)
    {
        // Disguised Deadites keep their name; the format only appears once revealed.
        if (ent.Comp.Revealed)
            args.AddModifier("deadite-name-format");
    }

    private void OnRefreshSpeed(Entity<DeaditeComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Revealed && !ent.Comp.Severed)
            args.ModifySpeed(ent.Comp.RevealedSpeed, ent.Comp.RevealedSpeed);
    }

    private void OnDeaditeMeleeHit(Entity<DeaditeComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.Revealed || args.Weapon != args.User || args.HitEntities.Count == 0)
            return;

        args.BonusDamage += ent.Comp.RevealedDamage;
    }

    #endregion

    #region Bite

    private void OnBite(Entity<DeaditeComponent> ent, ref DeaditeBiteActionEvent args)
    {
        if (args.Handled || ent.Comp.Severed)
            return;

        var target = args.Target;
        if (!_necro.TryGetDemon(ent.Comp.Demon, out var demon) || !_necro.CanUsePowers(demon.Value, ent))
            return;

        if (!_transform.InRange(Transform(ent).Coordinates, Transform(target).Coordinates, ent.Comp.BiteRange))
            return;

        // Mindshields repel the infection (the bite still hurts).
        var shielded = _necro.IsMindshielded(target);
        if (!shielded && !_necro.CanBecomeDeadite(target, ent))
            return;

        args.Handled = true;

        _damageable.TryChangeDamage(target, ent.Comp.BiteDamage, origin: ent);
        _audio.PlayPvs(BiteSound, target);
        _audio.PlayPvs(SnarlSound, ent);
        _popup.PopupEntity(Loc.GetString("deadite-bite",
                ("user", Identity.Entity(ent, EntityManager)),
                ("target", Identity.Entity(target, EntityManager))),
            target, PopupType.MediumCaution);

        _necro.AddFear(demon.Value, target, NecroFearSource.Attacked);
        _necro.AddFearAround(demon.Value, target, demon.Value.Comp.WitnessRange, NecroFearSource.Witness, exclude: target);

        if (shielded)
        {
            _popup.PopupEntity(Loc.GetString("deadite-bite-shielded"), ent, ent, PopupType.SmallCaution);
            return;
        }

        _necro.Prepare(demon.Value, target);
        _popup.PopupEntity(Loc.GetString("deadite-bite-prepared"), target, target, PopupType.LargeCaution);
    }

    #endregion

    #region Mindshield ritual

    private void OnReadPassage(Entity<DeaditeComponent> ent, ref DeaditeReadPassageActionEvent args)
    {
        if (args.Handled || ent.Comp.Severed)
            return;

        var target = args.Target;
        if (!ValidateRitual(ent, target, true))
            return;

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.ReadPassageTime, new DeaditeReadPassageDoAfterEvent(), ent, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = ent.Comp.ReadPassageRange + 0.5f,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("deadite-ritual-start",
                ("user", Identity.Entity(ent, EntityManager)),
                ("target", Identity.Entity(target, EntityManager))),
            ent, PopupType.LargeCaution);
        _jitter.DoJitter(target, ent.Comp.ReadPassageTime, true, 6f, 4f);
    }

    /// <summary>
    /// Ritual requirements: a revealed Deadite, a mindshielded crew member, and the manifested book near both.
    /// </summary>
    private bool ValidateRitual(Entity<DeaditeComponent> ent, EntityUid target, bool popup)
    {
        string? reason = null;

        if (!_necro.TryGetDemon(ent.Comp.Demon, out var demon) || demon.Value.Comp.Banished)
            return false;

        if (!demon.Value.Comp.Awakened)
            reason = "necro-dormant";
        else if (!ent.Comp.Revealed)
            reason = "deadite-ritual-not-revealed";
        else if (!HasComp<HumanoidProfileComponent>(target) || _necro.IsKandarian(target) || _mobState.IsDead(target))
            reason = "necro-target-invalid";
        else if (!_necro.IsMindshielded(target))
            reason = "deadite-ritual-not-shielded";
        else if (!_necro.IsBookManifested(demon.Value, out var book))
            reason = "necro-book-must-be-manifested";
        else if (!_transform.InRange(Transform(book.Value).Coordinates, Transform(target).Coordinates, ent.Comp.ReadPassageRange)
                 || !_transform.InRange(Transform(book.Value).Coordinates, Transform(ent).Coordinates, ent.Comp.ReadPassageRange))
            reason = "deadite-ritual-book-far";

        if (reason == null)
            return true;

        if (popup)
            _popup.PopupEntity(Loc.GetString(reason, ("target", Identity.Entity(target, EntityManager))), ent, ent, PopupType.SmallCaution);

        return false;
    }

    private void OnReadPassageDoAfter(Entity<DeaditeComponent> ent, ref DeaditeReadPassageDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (!ValidateRitual(ent, target, true))
            return;

        MeltMindshield(target);
    }

    /// <summary>Removes every mindshield implant from the victim.</summary>
    public void MeltMindshield(EntityUid target)
    {
        if (TryComp<ImplantedComponent>(target, out var implanted))
        {
            var implants = new List<EntityUid>(implanted.ImplantContainer.ContainedEntities);
            foreach (var implant in implants)
            {
                if (!HasComp<MindShieldComponent>(implant))
                    continue;

                // Removes and deletes the implant; raises ImplantRemovedEvent.
                _implants.ForceRemove((target, implanted), implant);
            }
        }

        _mindShield.RefreshMindshieldStatus(target);

        _audio.PlayPvs(ShieldMeltSound, target);
        _jitter.DoJitter(target, TimeSpan.FromSeconds(5), true, 20f, 10f);
        _popup.PopupEntity(Loc.GetString("deadite-ritual-melt", ("target", Identity.Entity(target, EntityManager))),
            target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("deadite-ritual-melt-victim"), target, target, PopupType.LargeCaution);
    }

    #endregion

    #region Durability

    private void OnRegenMobState(EntityUid uid, KandarianRegenComponent regen, MobStateChangedEvent args)
    {
        // Ordinary death is not allowed: fall back to critical and regenerate.
        // (Severed Deadites stay put; burning the book lifts this.)
        if (args.NewMobState != MobState.Dead)
            return;

        if (TryComp<DeaditeComponent>(uid, out var deadite) && deadite.Severed)
            return;

        regen.ReviveAt ??= _timing.CurTime + regen.ReviveDelay;
    }

    /// <summary>
    /// While critical, a Deadite ignores further punishment (so a beating can't gib the body),
    /// except a heavy slash, which severs the head. Severed Deadites can't be hurt further.
    /// </summary>
    private void OnDeaditeBeforeDamage(Entity<DeaditeComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Severed)
        {
            args.Cancelled = true;
            return;
        }

        if (!_mobState.IsCritical(ent))
            return;

        if (args.Damage.DamageDict.TryGetValue("Slash", out var slash) && slash >= ent.Comp.SeverThreshold)
            return;

        // Healing is always allowed.
        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        args.Cancelled = true;
    }

    private void OnDeaditeDamaged(EntityUid uid, DeaditeComponent comp, DamageChangedEvent args)
    {
        if (comp.Severed || args.DamageDelta == null || !args.DamageIncreased)
            return;

        if (!_mobState.IsCritical(uid))
            return;

        if (!args.DamageDelta.DamageDict.TryGetValue("Slash", out var slash) || slash < comp.SeverThreshold)
            return;

        Sever((uid, comp));
    }

    /// <summary>
    /// Head severed: the Deadite can no longer move or act, does not regenerate, and still cannot die.
    /// It can still talk (and scream).
    /// </summary>
    public void Sever(Entity<DeaditeComponent> ent)
    {
        if (ent.Comp.Severed)
            return;

        ent.Comp.Severed = true;

        if (TryComp<KandarianRegenComponent>(ent, out var regen))
            regen.ReviveAt = null;

        // Bring them back to "alive" so they can speak, but they cannot do anything else.
        _damageable.ClearAllDamage(ent.Owner);
        _mobState.ChangeMobState(ent, MobState.Alive);
        _audio.PlayPvs(ScreamSound, ent);
        _blocker.UpdateCanMove(ent);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);

        _popup.PopupEntity(Loc.GetString("deadite-severed", ("target", Identity.Entity(ent, EntityManager))),
            ent, PopupType.LargeCaution);
    }

    private void OnSeveredBlock<T>(Entity<DeaditeComponent> ent, ref T args) where T : CancellableEntityEventArgs
    {
        if (ent.Comp.Severed)
            args.Cancel();
    }

    private void OnSeveredInteract(Entity<DeaditeComponent> ent, ref InteractionAttemptEvent args)
    {
        if (ent.Comp.Severed)
            args.Cancelled = true;
    }

    #endregion

    #region Cleanup

    private void OnBookBurned(ref NecronomiconBurnedEvent args)
    {
        var demon = args.Demon;

        var deadites = EntityQueryEnumerator<DeaditeComponent>();
        var toRelease = new List<EntityUid>();
        while (deadites.MoveNext(out var uid, out var comp))
        {
            if (comp.Demon == demon || demon == null)
                toRelease.Add(uid);
        }

        foreach (var uid in toRelease)
            Release(uid);

        var spawn = EntityQueryEnumerator<DemonspawnComponent, KandarianEntityComponent>();
        var toDelete = new List<EntityUid>();
        while (spawn.MoveNext(out var uid, out _, out var kandarian))
        {
            if (kandarian.Demon == demon || demon == null)
                toDelete.Add(uid);
        }

        foreach (var uid in toDelete)
        {
            _popup.PopupEntity(Loc.GetString("demonspawn-banished"), uid, PopupType.LargeCaution);
            QueueDel(uid);
        }
    }

    /// <summary>
    /// The curse lifts: the body collapses, dead, as an ordinary person again. All Kandarian traits are removed so
    /// the corpse can be handled (and revived) normally.
    /// </summary>
    public void Release(EntityUid uid)
    {
        if (!TryComp<DeaditeComponent>(uid, out var comp))
            return;

        if (comp.OriginalDeadThreshold is { } original)
            _thresholds.SetMobStateThreshold(uid, original, MobState.Dead);

        _actions.RemoveAction(uid, comp.RevealActionEntity);
        _actions.RemoveAction(uid, comp.BiteActionEntity);
        _actions.RemoveAction(uid, comp.ReadPassageActionEntity);

        RemComp<KandarianRegenComponent>(uid);
        RemComp<KandarianEntityComponent>(uid);
        RemComp<IntrinsicRadioTransmitterComponent>(uid);
        RemComp<IntrinsicRadioReceiverComponent>(uid);
        if (TryComp<ActiveRadioComponent>(uid, out var active))
        {
            active.Channels.Remove(SumerianChannel);
            if (active.Channels.Count == 0)
                RemComp<ActiveRadioComponent>(uid);
        }

        if (comp.Revealed)
        {
            RemComp<AllCapsAccentComponent>(uid);
            RemComp<ReplacementAccentComponent>(uid);
        }

        if (_mind.TryGetMind(uid, out var mindId, out _))
            _roles.MindRemoveRole<DeaditeRoleComponent>(mindId);

        RemComp<DeaditeComponent>(uid);
        _nameMod.RefreshNameModifiers(uid);
        _speed.RefreshMovementSpeedModifiers(uid);
        _blocker.UpdateCanMove(uid);

        _popup.PopupEntity(Loc.GetString("deadite-released", ("target", Identity.Entity(uid, EntityManager))),
            uid, PopupType.LargeCaution);
        _mobState.ChangeMobState(uid, MobState.Dead);
    }

    #endregion
}
