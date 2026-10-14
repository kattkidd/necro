using Content.Server.Necronomicon.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;

namespace Content.Server.Necronomicon;

public sealed partial class NecronomiconSystem
{
    [Dependency] private DeaditeSystem _deadite = default!;

    private void InitializePossession()
    {
        SubscribeLocalEvent<NecroDemonComponent, NecroJoinUsActionEvent>(OnJoinUs);
        SubscribeLocalEvent<NecroDemonComponent, NecroPossessActionEvent>(OnPossessAction);
        SubscribeLocalEvent<NecroDemonComponent, NecroPossessDoAfterEvent>(OnPossessDoAfter);
    }

    /// <summary>
    /// Shared validation for anything that would turn <paramref name="target"/> into a Deadite.
    /// </summary>
    public bool CanBecomeDeadite(EntityUid target, EntityUid? popupUser, bool checkMindshield = true)
    {
        string? reason = null;

        if (!IsPossessableBody(target))
            reason = "necro-target-invalid";
        else if (HasComp<DeaditeComponent>(target) || HasComp<NecroBeingPossessedComponent>(target))
            reason = "necro-target-already";
        else if (checkMindshield && IsMindshielded(target))
            reason = "necro-target-mindshielded";

        if (reason == null)
            return true;

        if (popupUser is { } user)
            _popup.PopupEntity(Loc.GetString(reason, ("target", Identity.Entity(target, EntityManager))), user, user, PopupType.SmallCaution);

        return false;
    }

    /// <summary>Checks the manifested book is close enough to the target.</summary>
    private bool BookNear(Entity<NecroDemonComponent> demon, EntityUid target, float range, EntityUid user)
    {
        if (!IsBookManifested(demon, out var book))
        {
            _popup.PopupEntity(Loc.GetString("necro-book-must-be-manifested"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_transform.InRange(Transform(book.Value).Coordinates, Transform(target).Coordinates, range))
        {
            _popup.PopupEntity(Loc.GetString("necro-book-too-far"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    /// <summary>Marks a victim as prepared for possession.</summary>
    public void Prepare(Entity<NecroDemonComponent> demon, EntityUid victim)
    {
        var prepared = EnsureComp<NecroPreparedComponent>(victim);
        prepared.ExpiresAt = _timing.CurTime + demon.Comp.PreparedDuration;
    }

    private void OnJoinUs(Entity<NecroDemonComponent> ent, ref NecroJoinUsActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        var target = args.Target;
        if (!CanBecomeDeadite(target, args.Performer))
            return;

        var range = HasUpgrade(ent, NecroUpgrade.FarWhisper) ? ent.Comp.FarWhisperJoinUsRange : ent.Comp.JoinUsRange;
        if (!BookNear(ent, target, range, args.Performer))
            return;

        Prepare(ent, target);
        args.Handled = true;

        // The whisper (only the victim hears it) and the shudder.
        _audio.PlayGlobal(ent.Comp.WhisperSound, target);
        _popup.PopupEntity(Loc.GetString("necro-join-us-victim"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("necro-join-us-shudder", ("target", Identity.Entity(target, EntityManager))),
            target, Robust.Shared.Player.Filter.PvsExcept(target), true, PopupType.Medium);
        _jitter.DoJitter(target, TimeSpan.FromSeconds(4), true, 8f, 6f);
        _popup.PopupEntity(Loc.GetString("necro-join-us-success", ("target", Identity.Entity(target, EntityManager))),
            args.Performer, args.Performer, PopupType.Medium);

        AddFear(ent, target, NecroFearSource.Supernatural);
    }

    private void OnPossessAction(Entity<NecroDemonComponent> ent, ref NecroPossessActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        var target = args.Target;

        if (ent.Comp.PossessionCharges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("necro-no-charges"), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!CanBecomeDeadite(target, args.Performer))
            return;

        if (!HasComp<NecroPreparedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("necro-target-not-prepared"), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!BookNear(ent, target, ent.Comp.PossessRange, args.Performer))
            return;

        var duration = HasUpgrade(ent, NecroUpgrade.SwiftPossession) ? ent.Comp.SwiftPossessDuration : ent.Comp.PossessDuration;
        var doAfter = new DoAfterArgs(EntityManager, args.Performer, duration, new NecroPossessDoAfterEvent(), ent, target)
        {
            RequireCanInteract = false,
            BreakOnMove = false,
            DistanceThreshold = 6f,
            BlockDuplicate = true,
            CancelDuplicate = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        EnsureComp<NecroBeingPossessedComponent>(target);
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("necro-possess-start-victim"), target, target, PopupType.LargeCaution);
        _jitter.DoJitter(target, duration, true, 12f, 8f);
    }

    private void OnPossessDoAfter(Entity<NecroDemonComponent> ent, ref NecroPossessDoAfterEvent args)
    {
        if (args.Target is not { } target)
            return;

        RemComp<NecroBeingPossessedComponent>(target);

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // Re-validate everything at completion: state may have changed during the do-after.
        if (ent.Comp.Banished || ent.Comp.PossessionCharges <= 0)
            return;

        if (!CanBecomeDeadite(target, args.User) || !HasComp<NecroPreparedComponent>(target))
            return;

        if (!BookNear(ent, target, ent.Comp.PossessRange, args.User))
            return;

        ConvertToDeadite(ent, target);
    }

    /// <summary>Consumes a charge and turns the victim into a disguised Deadite.</summary>
    public bool ConvertToDeadite(Entity<NecroDemonComponent> demon, EntityUid target)
    {
        if (demon.Comp.PossessionCharges <= 0 || HasComp<DeaditeComponent>(target))
            return false;

        demon.Comp.PossessionCharges--;
        demon.Comp.Conversions++;
        RemComp<NecroPreparedComponent>(target);

        _deadite.MakeDeadite(target, demon);
        AddSoulPower(demon, demon.Comp.ConversionReward);

        _popup.PopupEntity(Loc.GetString("necro-possess-success",
                ("target", Identity.Entity(target, EntityManager)),
                ("charges", demon.Comp.PossessionCharges)),
            demon, demon, PopupType.Large);

        if (demon.Comp.Manifestation is { } form)
            _popup.PopupEntity(Loc.GetString("necro-possess-success",
                    ("target", Identity.Entity(target, EntityManager)),
                    ("charges", demon.Comp.PossessionCharges)),
                form, form, PopupType.Large);

        var ev = new NecroSoulConsumedEvent(demon, target);
        RaiseLocalEvent(ref ev);
        return true;
    }

    private void UpdatePrepared(TimeSpan now)
    {
        var query = EntityQueryEnumerator<NecroPreparedComponent>();
        while (query.MoveNext(out var uid, out var prepared))
        {
            if (now < prepared.ExpiresAt)
                continue;

            RemCompDeferred<NecroPreparedComponent>(uid);
        }
    }
}

/// <summary>Raised broadcast whenever a crew member becomes a Deadite.</summary>
[ByRefEvent]
public readonly record struct NecroSoulConsumedEvent(EntityUid Demon, EntityUid Victim);
