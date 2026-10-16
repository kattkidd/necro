using System.Numerics;
using Content.Server.Necronomicon.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Necronomicon;

/// <summary>
/// Ether powers taken from the films and the series, bought in the Skill Store:
/// <list type="bullet">
/// <item><b>The Force</b> (The Evil Dead): the demon's charge across the station, through doors, bowling people over.</item>
/// <item><b>The Trees</b> (The Evil Dead): roots and branches burst through the floor and hold a victim fast.</item>
/// <item><b>The Hand</b> (Evil Dead II): a victim's own hand turns on them.</item>
/// <item><b>Baal's Deceit</b> (Ash vs Evil Dead): a victim sees a friend's eyes flash white.</item>
/// <item><b>Dead by Dawn</b> (Evil Dead II): the whole station hears it, and every Deadite surges back up.</item>
/// </list>
/// </summary>
public sealed partial class NecronomiconSystem
{
    private static readonly EntProtoId RootSlowEffect = "DeaditeBiteSlowdownStatusEffect";
    private static readonly EntProtoId DawnSpeedEffect = "ReagentSpeedStatusEffect";
    private static readonly SoundSpecifier ForceSound =
        new SoundCollectionSpecifier("DeaditeScreams", AudioParams.Default.WithVolume(4f).WithMaxDistance(20f));
    private static readonly SoundSpecifier RootsSound = new SoundPathSpecifier("/Audio/Effects/desecration-02.ogg");
    private static readonly SoundSpecifier DawnSound = new SoundCollectionSpecifier("DeaditeLaughs");

    private void InitializePowers()
    {
        SubscribeLocalEvent<NecroDemonComponent, NecroRushActionEvent>(OnRush);
        SubscribeLocalEvent<NecroDemonComponent, NecroGraspingRootsActionEvent>(OnGraspingRoots);
        SubscribeLocalEvent<NecroDemonComponent, NecroPossessedHandActionEvent>(OnPossessedHand);
        SubscribeLocalEvent<NecroDemonComponent, NecroDeceitActionEvent>(OnDeceit);
        SubscribeLocalEvent<NecroDemonComponent, NecroDeadByDawnActionEvent>(OnDeadByDawn);
    }

    /// <summary>Common checks for a power aimed at a living person.</summary>
    private bool ValidLivingTarget(EntityUid target, EntityUid user)
    {
        if (HasComp<HumanoidProfileComponent>(target) && !IsKandarian(target) && _mobState.IsAlive(target))
            return true;

        _popup.PopupCursor(Loc.GetString("necro-target-invalid"), user, PopupType.SmallCaution);
        return false;
    }

    #region The Force

    private void OnRush(Entity<NecroDemonComponent> ent, ref NecroRushActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        var start = _transform.GetMapCoordinates(args.Performer);
        var target = _transform.ToMapCoordinates(args.Target);
        if (start.MapId != target.MapId)
            return;

        var delta = target.Position - start.Position;
        var length = delta.Length();
        if (length < 0.5f)
            return;

        if (length > ent.Comp.RushMaxDistance)
        {
            delta = delta / length * ent.Comp.RushMaxDistance;
            length = ent.Comp.RushMaxDistance;
        }

        if (!TrySpendSoulPower(ent, ent.Comp.RushCost, args.Performer))
            return;

        args.Handled = true;

        var end = new MapCoordinates(start.Position + delta, start.MapId);
        var middle = new MapCoordinates(start.Position + delta / 2, start.MapId);
        var direction = delta / length;

        _audio.PlayPvs(ForceSound, args.Performer);

        // Everything along the path: lights flicker, doors and lockers bang, people are bowled over.
        foreach (var uid in _lookup.GetEntitiesInRange(middle, length / 2 + ent.Comp.RushWidth))
        {
            if (uid == args.Performer)
                continue;

            var position = _transform.GetWorldPosition(uid);
            var along = Math.Clamp(Vector2.Dot(position - start.Position, direction), 0f, length);
            var closest = start.Position + direction * along;
            if ((position - closest).Length() > ent.Comp.RushWidth)
                continue;

            _ghost.DoGhostBooEvent(uid, GhostBooIntensity.Normal);

            if (!HasComp<HumanoidProfileComponent>(uid) || IsKandarian(uid) || !_mobState.IsAlive(uid))
                continue;

            _stun.TryKnockdown(uid, TimeSpan.FromSeconds(2));
            _popup.PopupCursor(Loc.GetString("necro-rush-victim"), uid, PopupType.LargeCaution);
            AddFear(ent, uid, NecroFearSource.Attacked);
        }

        _transform.SetMapCoordinates(args.Performer, end);
        _transform.AttachToGridOrMap(args.Performer);
    }

    #endregion

    #region The Trees

    private void OnGraspingRoots(Entity<NecroDemonComponent> ent, ref NecroGraspingRootsActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer) || !ValidLivingTarget(args.Target, args.Performer)
            || TryWard(args.Target, args.Performer))
            return;

        if (!TrySpendSoulPower(ent, ent.Comp.RootsCost, args.Performer))
            return;

        args.Handled = true;
        var target = args.Target;

        _movementMod.TryUpdateMovementSpeedModDuration(target, RootSlowEffect, ent.Comp.RootsDuration, 0.05f);
        _jitter.DoJitter(target, ent.Comp.RootsDuration, true, 6f, 4f);
        _audio.PlayPvs(RootsSound, target);
        _popup.PopupEntity(Loc.GetString("necro-roots", ("target", Identity.Entity(target, EntityManager))),
            target, Filter.PvsExcept(target), true, PopupType.MediumCaution);
        _popup.PopupCursor(Loc.GetString("necro-roots-victim"), target, PopupType.LargeCaution);

        AddFear(ent, target, NecroFearSource.Supernatural);
        AddFearAround(ent, target, ent.Comp.WitnessRange, NecroFearSource.Witness, exclude: target);
    }

    #endregion

    #region The Hand

    private void OnPossessedHand(Entity<NecroDemonComponent> ent, ref NecroPossessedHandActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer) || !ValidLivingTarget(args.Target, args.Performer)
            || TryWard(args.Target, args.Performer))
            return;

        var target = args.Target;
        if (IsMindshielded(target))
        {
            _popup.PopupCursor(Loc.GetString("necro-target-mindshielded", ("target", Identity.Entity(target, EntityManager))),
                args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!TrySpendSoulPower(ent, ent.Comp.HandCost, args.Performer))
            return;

        args.Handled = true;

        foreach (var held in new List<EntityUid>(_hands.EnumerateHeld(target)))
        {
            _hands.TryDrop(target, held, checkActionBlocker: false);
        }

        _damageable.TryChangeDamage(target, ent.Comp.HandDamage, origin: target);
        _jitter.DoJitter(target, TimeSpan.FromSeconds(3), true, 10f, 8f);
        _popup.PopupEntity(Loc.GetString("necro-hand", ("target", Identity.Entity(target, EntityManager))),
            target, Filter.PvsExcept(target), true, PopupType.MediumCaution);
        _popup.PopupCursor(Loc.GetString("necro-hand-victim"), target, PopupType.LargeCaution);

        AddFear(ent, target, NecroFearSource.Attacked);
        AddFearAround(ent, target, ent.Comp.WitnessRange, NecroFearSource.Witness, exclude: target);
    }

    #endregion

    #region Baal's Deceit

    private void OnDeceit(Entity<NecroDemonComponent> ent, ref NecroDeceitActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer) || !ValidLivingTarget(args.Target, args.Performer)
            || TryWard(args.Target, args.Performer))
            return;

        var target = args.Target;
        if (IsMindshielded(target))
        {
            _popup.PopupCursor(Loc.GetString("necro-target-mindshielded", ("target", Identity.Entity(target, EntityManager))),
                args.Performer, PopupType.SmallCaution);
            return;
        }

        // Pick someone near the victim to frame (it might even be a real Deadite).
        var candidates = new List<EntityUid>();
        foreach (var other in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(Transform(target).Coordinates, ent.Comp.DeceitRange))
        {
            if (other.Owner != target && _mobState.IsAlive(other.Owner) && !HasComp<NecroManifestationComponent>(other.Owner))
                candidates.Add(other.Owner);
        }

        if (candidates.Count == 0)
        {
            _popup.PopupCursor(Loc.GetString("necro-deceit-nobody"), args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!TrySpendSoulPower(ent, ent.Comp.DeceitCost, args.Performer))
            return;

        args.Handled = true;

        var framed = _random.Pick(candidates);
        _popup.PopupEntity(Loc.GetString("necro-deceit-vision", ("target", Identity.Entity(framed, EntityManager))),
            framed, target, PopupType.LargeCaution);
        _audio.PlayGlobal(ent.Comp.WhisperSound, target);
        _popup.PopupCursor(Loc.GetString("necro-deceit-sent",
                ("target", Identity.Entity(target, EntityManager)), ("framed", Identity.Entity(framed, EntityManager))),
            args.Performer, PopupType.Small);

        AddFear(ent, target, NecroFearSource.Supernatural);
    }

    #endregion

    #region Dead by Dawn

    private void OnDeadByDawn(Entity<NecroDemonComponent> ent, ref NecroDeadByDawnActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        args.Handled = true;

        _chat.DispatchGlobalAnnouncement(Loc.GetString("necro-dead-by-dawn-announcement"), Name(ent),
            announcementSound: DawnSound, colorOverride: Color.DarkRed);

        var query = EntityQueryEnumerator<DeaditeComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var deadite, out var mob))
        {
            if (deadite.Demon != ent.Owner || deadite.Severed)
                continue;

            if (mob.CurrentState != MobState.Alive)
                _mobState.ChangeMobState(uid, MobState.Alive, mob);

            _damageable.ClearAllDamage(uid);
            _movementMod.TryUpdateMovementSpeedModDuration(uid, DawnSpeedEffect, ent.Comp.DeadByDawnDuration,
                ent.Comp.DeadByDawnSpeed);
            _popup.PopupCursor(Loc.GetString("necro-dead-by-dawn-deadite"), uid, PopupType.LargeCaution);
            SupernaturalBurst(Transform(uid).Coordinates, 3f, 1);
        }
    }

    #endregion
}
