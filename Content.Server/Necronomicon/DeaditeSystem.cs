using Content.Server.Chat.Systems;
using Content.Server.Emoting.Systems;
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
using Content.Shared.Chat.Prototypes;
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
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;

    public static readonly EntProtoId DeaditeMindRole = "MindRoleDeadite";
    public const string SumerianChannel = "Sumerian";

    private static readonly ProtoId<EmoteSoundsPrototype> DeaditeEmoteSounds = "DeaditeEmotes";
    private const string LaughEmote = "Laugh";
    private const string ScreamEmote = "Scream";
    private const string SnarlEmote = "Snarl";
    private static readonly SoundSpecifier SnarlSound = new SoundCollectionSpecifier("DeaditeSnarls");
    private static readonly SoundSpecifier ScreamSound = new SoundCollectionSpecifier("DeaditeScreams");
    private static readonly SoundSpecifier TalkSound = new SoundCollectionSpecifier("DeaditeTalk");
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
        SubscribeLocalEvent<DeaditeComponent, DamageModifyEvent>(OnDeaditeDamageModify);

        // Severed head: no moving, using, attacking, picking up or throwing - but still talking.
        SubscribeLocalEvent<DeaditeComponent, UpdateCanMoveEvent>(OnSeveredBlock);
        SubscribeLocalEvent<DeaditeComponent, UseAttemptEvent>(OnSeveredBlock);
        SubscribeLocalEvent<DeaditeComponent, AttackAttemptEvent>(OnSeveredBlock);
        SubscribeLocalEvent<DeaditeComponent, PickupAttemptEvent>(OnSeveredBlock);
        SubscribeLocalEvent<DeaditeComponent, ThrowAttemptEvent>(OnSeveredBlock);
        SubscribeLocalEvent<DeaditeComponent, InteractionAttemptEvent>(OnSeveredInteract);

        SubscribeLocalEvent<DeaditeComponent, EntitySpokeEvent>(OnDeaditeSpoke);
        SubscribeLocalEvent<DeaditeComponent, EmoteEvent>(OnDeaditeEmote,
            before: new[] { typeof(VocalSystem), typeof(BodyEmotesSystem) });

        SubscribeLocalEvent<KandarianRegenComponent, MobStateChangedEvent>(OnRegenMobState);

        SubscribeLocalEvent<NecronomiconBurnedEvent>(OnBookBurned);

        SubscribeLocalEvent<NecroDemonRoleComponent, GetBriefingEvent>(OnDemonBriefing);
        SubscribeLocalEvent<DeaditeRoleComponent, GetBriefingEvent>(OnDeaditeBriefing);
        SubscribeLocalEvent<DemonspawnRoleComponent, GetBriefingEvent>(OnDemonspawnBriefing);

        InitializeExorcism();
        InitializeTree();
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

        UpdateTree();
        UpdateExorcism(now);

        // Revealed Deadites snarl every 10-20 seconds.
        var laughers = EntityQueryEnumerator<DeaditeComponent>();
        while (laughers.MoveNext(out var laugher, out var deadite))
        {
            if (!deadite.Revealed || deadite.Severed || now < deadite.NextLaugh || !_mobState.IsAlive(laugher))
                continue;

            deadite.NextLaugh = now + TimeSpan.FromSeconds(_random.Next(10, 21));
            // A real emote: it shows in chat and plays through the Deadite voice below.
            _chat.TryEmoteWithChat(laugher, _random.Prob(0.35f) ? LaughEmote : SnarlEmote,
                ignoreActionBlocker: true, forceEmote: true);
        }

        var query = EntityQueryEnumerator<KandarianRegenComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var regen, out var mob))
        {
            if (TryComp<DeaditeComponent>(uid, out var deadite) && deadite.Severed)
                continue;

            if (mob.CurrentState == MobState.Critical || mob.CurrentState == MobState.Dead)
            {
                // Holy words pin the body down: no rising while a chaplain is mid-rite.
                if (HasComp<NecroBeingExorcisedComponent>(uid))
                {
                    regen.ReviveAt = null;
                    continue;
                }

                regen.ReviveAt ??= now + GetReviveDelay(uid, regen);
                if (now < regen.ReviveAt)
                    continue;

                regen.ReviveAt = null;
                _damageable.ClearAllDamage(uid);
                _mobState.ChangeMobState(uid, MobState.Alive, mob);
                _chat.TryEmoteWithChat(uid, ScreamEmote, ignoreActionBlocker: true, forceEmote: true);
                _popup.PopupEntity(Loc.GetString("deadite-rises", ("target", Identity.Entity(uid, EntityManager))),
                    uid, PopupType.LargeCaution);

                if (TryComp<KandarianEntityComponent>(uid, out var kandarian)
                    && _necro.TryGetDemon(kandarian.Demon, out var demon))
                {
                    _necro.AddFearAround(demon.Value, uid, demon.Value.Comp.WitnessRange, NecroFearSource.Supernatural);
                    _necro.SupernaturalBurst(Transform(uid).Coordinates, demon.Value.Comp.SupernaturalRange, 2);
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
        GrantTreeActions((target, deadite));

        if (_mind.TryGetMind(target, out var mindId, out _))
            _roles.MindAddRole(mindId, DeaditeMindRole);

        // Only the victim notices.
        _popup.PopupCursor(Loc.GetString("deadite-converted"), target, PopupType.LargeCaution);
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
        ent.Comp.NextLaugh = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(10, 21));

        // Remember the victim's own colours (once) so an exorcism or Conceal can put them back.
        if (ent.Comp.OriginalProfiles == null
            && _visualBody.TryGatherMarkingsData(ent.Owner, null, out var original, out _, out _))
        {
            ent.Comp.OriginalProfiles = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>(original);
        }

        // Pale skin, completely white eyes.
        ApplyDeaditeLook(ent.Owner, ent.Comp.SkinColor, ent.Comp.EyeColor);

        // Revealed Deadites still talk normally; their voice is in the laughs, snarls and screams (emotes).

        _nameMod.RefreshNameModifiers(ent.Owner);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);

        // The reveal action is spent.
        _actions.RemoveAction(ent.Owner, ent.Comp.RevealActionEntity);
        ent.Comp.RevealActionEntity = null;

        _chat.TryEmoteWithChat(ent, LaughEmote, ignoreActionBlocker: true, forceEmote: true);
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(3), true, 15f, 8f);
        _popup.PopupEntity(Loc.GetString("deadite-reveal", ("target", Identity.Entity(ent, EntityManager))),
            ent, PopupType.LargeCaution);

        if (_necro.TryGetDemon(ent.Comp.Demon, out var demon))
        {
            // Paid once: concealing and revealing again only frightens people.
            if (!ent.Comp.RevealRewarded)
            {
                ent.Comp.RevealRewarded = true;
                _necro.AddSoulPower(demon.Value, ent.Comp.RevealReward);
            }

            _necro.AddFearAround(demon.Value, ent, demon.Value.Comp.WitnessRange, NecroFearSource.Reveal);
            _necro.SupernaturalBurst(Transform(ent).Coordinates, demon.Value.Comp.SupernaturalRange, 3);
        }

        // Infiltrator II can slip the mask back on.
        GrantTreeActions(ent);
    }

    /// <summary>Corpse-pale skin and milky white eyes on any humanoid body.</summary>
    public void ApplyDeaditeLook(EntityUid uid, Color? skin = null, Color? eyes = null)
    {
        if (!_visualBody.TryGatherMarkingsData(uid, null, out var profiles, out _, out _))
            return;

        var pale = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>();
        foreach (var (category, profile) in profiles)
        {
            pale[category] = profile with
            {
                SkinColor = skin ?? Color.FromHex("#d8d8cf"),
                EyeColor = eyes ?? Color.White,
            };
        }

        _visualBody.ApplyProfiles(uid, pale);
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

    /// <summary>
    /// A revealed Deadite's laughs, screams and snarls (typed or automatic) come out in the Deadite voice.
    /// Disguised Deadites sound like their host.
    /// </summary>
    private void OnDeaditeEmote(Entity<DeaditeComponent> ent, ref EmoteEvent args)
    {
        if (args.Handled || !ent.Comp.Revealed || !args.Emote.Category.HasFlag(EmoteCategory.Vocal))
            return;

        args.Handled = _chat.TryPlayEmoteSound(ent, ProtoMan.Index(DeaditeEmoteSounds), args.Emote);
    }

    private void OnRefreshName(Entity<DeaditeComponent> ent, ref RefreshNameModifiersEvent args)
    {
        // Disguised Deadites keep their name; the format only appears once revealed.
        if (ent.Comp.Revealed)
            args.AddModifier("deadite-name-format");
    }

    private void OnRefreshSpeed(Entity<DeaditeComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Revealed || ent.Comp.Severed)
            return;

        var speed = HasTree(ent.Comp, NecroUpgrade.StalkerI) ? ent.Comp.StalkerSpeed : ent.Comp.RevealedSpeed;
        args.ModifySpeed(speed, speed);
    }

    private void OnDeaditeMeleeHit(Entity<DeaditeComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.Revealed || args.Weapon != args.User || args.HitEntities.Count == 0)
            return;

        args.BonusDamage += HasTree(ent.Comp, NecroUpgrade.BruteI)
            ? ent.Comp.RevealedDamage * ent.Comp.BruteDamageMultiplier
            : ent.Comp.RevealedDamage;
    }

    private void OnDeaditeDamageModify(EntityUid uid, DeaditeComponent comp, DamageModifyEvent args)
    {
        // Brute II: revealed Deadites shrug off a quarter of every blow.
        if (!comp.Revealed || comp.Severed || !HasTree(comp, NecroUpgrade.BruteII))
            return;

        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        args.Damage *= comp.BruteDamageTaken;
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

        var range = HasTree(ent.Comp, NecroUpgrade.StalkerI) ? ent.Comp.StalkerBiteRange : ent.Comp.BiteRange;
        if (!_transform.InRange(Transform(ent).Coordinates, Transform(target).Coordinates, range))
        {
            _popup.PopupCursor(Loc.GetString("necro-too-far"), ent, PopupType.SmallCaution);
            return;
        }

        // Mindshields repel the infection (the bite still hurts).
        var shielded = _necro.IsMindshielded(target);
        if (!shielded && !_necro.CanBecomeDeadite(target, ent))
            return;

        args.Handled = true;

        _damageable.TryChangeDamage(target, ent.Comp.BiteDamage, origin: ent);

        // Infiltrator I: a disguised Deadite's bite is a quiet nip only the victim notices.
        if (!ent.Comp.Revealed && HasTree(ent.Comp, NecroUpgrade.InfiltratorI))
        {
            _popup.PopupCursor(Loc.GetString("deadite-bite-silent-victim"), target, PopupType.SmallCaution);
            _popup.PopupCursor(Loc.GetString("deadite-bite-silent-user",
                    ("target", Identity.Entity(target, EntityManager))),
                ent, PopupType.Small);
        }
        else
        {
            _audio.PlayPvs(BiteSound, target);
            _audio.PlayPvs(SnarlSound, ent);
            _popup.PopupEntity(Loc.GetString("deadite-bite",
                    ("user", Identity.Entity(ent, EntityManager)),
                    ("target", Identity.Entity(target, EntityManager))),
                target, PopupType.MediumCaution);
            _necro.AddFearAround(demon.Value, target, demon.Value.Comp.WitnessRange, NecroFearSource.Witness, exclude: target);
        }

        _necro.AddFear(demon.Value, target, NecroFearSource.Attacked);

        // Stalker II: the wound drags at their legs.
        if (HasTree(ent.Comp, NecroUpgrade.StalkerII))
        {
            _movementMod.TryUpdateMovementSpeedModDuration(target, ent.Comp.StalkerSlowEffect,
                ent.Comp.StalkerSlowDuration, ent.Comp.StalkerSlowMultiplier);
        }

        if (shielded)
        {
            _popup.PopupCursor(Loc.GetString("deadite-bite-shielded"), ent, PopupType.SmallCaution);
            return;
        }

        _necro.Prepare(demon.Value, target);
        _popup.PopupCursor(Loc.GetString("deadite-bite-prepared"), target, PopupType.LargeCaution);
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
            _popup.PopupCursor(Loc.GetString(reason, ("target", Identity.Entity(target, EntityManager))), ent, PopupType.SmallCaution);

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
        _necro.Chant(target, 5);

        if (_necro.TryGetDemon(ent.Comp.Demon, out var demon))
        {
            demon.Value.Comp.RitualsPerformed++;
            _necro.SupernaturalBurst(Transform(target).Coordinates, demon.Value.Comp.SupernaturalRange, 3);
        }
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
        _popup.PopupCursor(Loc.GetString("deadite-ritual-melt-victim"), target, PopupType.LargeCaution);
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

        regen.ReviveAt ??= _timing.CurTime + GetReviveDelay(uid, regen);
    }

    /// <summary>Brute I Deadites get back up sooner.</summary>
    private TimeSpan GetReviveDelay(EntityUid uid, KandarianRegenComponent regen)
    {
        if (TryComp<DeaditeComponent>(uid, out var deadite) && HasTree(deadite, NecroUpgrade.BruteI))
            return regen.ReviveDelay * deadite.BruteReviveMultiplier;

        return regen.ReviveDelay;
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

        if (args.Damage.DamageDict.TryGetValue("Slash", out var slash) && slash >= GetSeverThreshold(ent.Comp))
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

        if (!args.DamageDelta.DamageDict.TryGetValue("Slash", out var slash) || slash < GetSeverThreshold(comp))
            return;

        Sever((uid, comp));
    }

    /// <summary>Brute II necks are thicker.</summary>
    private FixedPoint2 GetSeverThreshold(DeaditeComponent comp)
    {
        return HasTree(comp, NecroUpgrade.BruteII) ? comp.SeverThreshold + comp.BruteSeverBonus : comp.SeverThreshold;
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
        _chat.TryEmoteWithChat(ent, ScreamEmote, ignoreActionBlocker: true, forceEmote: true);
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

        StripDeadite(uid, comp);

        _popup.PopupEntity(Loc.GetString("deadite-released", ("target", Identity.Entity(uid, EntityManager))),
            uid, PopupType.LargeCaution);
        _mobState.ChangeMobState(uid, MobState.Dead);
    }

    /// <summary>
    /// Removes everything that made <paramref name="uid"/> a Deadite (thresholds, actions, Sumerian, mind role,
    /// the component itself) and refreshes name, speed and movement. Leaves the mob state alone.
    /// </summary>
    private void StripDeadite(EntityUid uid, DeaditeComponent comp)
    {
        if (comp.OriginalDeadThreshold is { } original)
            _thresholds.SetMobStateThreshold(uid, original, MobState.Dead);

        _actions.RemoveAction(uid, comp.RevealActionEntity);
        _actions.RemoveAction(uid, comp.BiteActionEntity);
        _actions.RemoveAction(uid, comp.ReadPassageActionEntity);
        _actions.RemoveAction(uid, comp.ScreamActionEntity);
        _actions.RemoveAction(uid, comp.ConcealActionEntity);

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

        if (_mind.TryGetMind(uid, out var mindId, out _))
            _roles.MindRemoveRole<DeaditeRoleComponent>(mindId);

        RemComp<DeaditeComponent>(uid);
        RemComp<NecroPreparedComponent>(uid);
        _nameMod.RefreshNameModifiers(uid);
        _speed.RefreshMovementSpeedModifiers(uid);
        _blocker.UpdateCanMove(uid);
    }

    #endregion
}
