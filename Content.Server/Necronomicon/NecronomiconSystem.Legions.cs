using Content.Server.Necronomicon.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Necronomicon;

/// <summary>
/// The demon's servants beyond possession, and the wards against it:
/// <list type="bullet">
/// <item><b>From the Grave</b> (The Evil Dead): a corpse rises as a Deadite. A ghost takes it if its owner is gone.</item>
/// <item><b>The Wicked Dead</b>: one of the wicked dead is dragged up out of Hell into a body of flesh, ghost-controlled.</item>
/// <item><b>Army of Darkness</b> (Army of Darkness): a squad of deadite skeletons.</item>
/// <item><b>Brujo talismans</b>: the demon's powers can't touch anyone near one.</item>
/// </list>
/// </summary>
public sealed partial class NecronomiconSystem
{
    private void InitializeLegions()
    {
        SubscribeLocalEvent<NecroDemonComponent, NecroRiseAgainActionEvent>(OnRiseAgain);
        SubscribeLocalEvent<NecroDemonComponent, NecroSummonWickedDeadEvent>(OnSummonWickedDead);
        SubscribeLocalEvent<NecroDemonComponent, NecroRaiseArmyEvent>(OnRaiseArmy);
    }

    /// <summary>Where the demon's servants come from: the book if it's out, otherwise wherever the demon is.</summary>
    private EntityCoordinates ServantSpawnCoords(Entity<NecroDemonComponent> demon)
    {
        return IsBookManifested(demon, out var book)
            ? Transform(book.Value).Coordinates
            : Transform(demon.Comp.Manifestation ?? demon.Owner).Coordinates;
    }

    #region Talismans

    /// <summary>
    /// If a brujo talisman protects <paramref name="target"/>, burns one of its charges, tells everyone and returns true.
    /// </summary>
    public bool TryWard(EntityUid target, EntityUid? user)
    {
        var targetPos = _transform.GetMapCoordinates(target);
        var query = EntityQueryEnumerator<NecroTalismanComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var talisman, out var xform))
        {
            var pos = _transform.GetMapCoordinates(uid, xform);
            if (pos.MapId != targetPos.MapId || (pos.Position - targetPos.Position).Length() > talisman.Range)
                continue;

            talisman.Charges--;
            _popup.PopupEntity(Loc.GetString("necro-talisman-wards", ("target", Identity.Entity(target, EntityManager))),
                target, PopupType.MediumCaution);
            if (user is { } u)
                _popup.PopupCursor(Loc.GetString("necro-talisman-blocked"), u, PopupType.SmallCaution);

            if (talisman.Charges <= 0)
            {
                _popup.PopupEntity(Loc.GetString("necro-talisman-crumbles"), uid, PopupType.MediumCaution);
                Spawn("Ash", _transform.GetMoverCoordinates(uid));
                QueueDel(uid);
            }

            return true;
        }

        return false;
    }

    #endregion

    #region From the Grave

    private void OnRiseAgain(Entity<NecroDemonComponent> ent, ref NecroRiseAgainActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        var target = args.Target;
        if (!HasComp<HumanoidProfileComponent>(target) || IsKandarian(target) || !_mobState.IsDead(target))
        {
            _popup.PopupCursor(Loc.GetString("necro-rise-invalid"), args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!_transform.InRange(Transform(args.Performer).Coordinates, Transform(target).Coordinates, ent.Comp.RiseRange))
        {
            _popup.PopupCursor(Loc.GetString("necro-too-far"), args.Performer, PopupType.SmallCaution);
            return;
        }

        if (TryWard(target, args.Performer))
            return;

        if (!TrySpendSoulPower(ent, ent.Comp.RiseCost, args.Performer))
            return;

        args.Handled = true;

        // Possession heals the body and stands it up; it comes back already revealed.
        _deadite.MakeDeadite(target, ent);
        if (TryComp<DeaditeComponent>(target, out var deadite))
        {
            deadite.RevealRewarded = true;
            _deadite.Reveal((target, deadite));
        }

        // Nobody home: let a ghost take the risen body.
        if (!_mind.TryGetMind(target, out _, out var mind) || mind.UserId == null)
            EntityManager.AddComponents(target, ProtoMan.Index(ent.Comp.RisenGhostRole), removeExisting: false);

        ent.Comp.RisenDead++;
        Chant(target, 0);
        _popup.PopupEntity(Loc.GetString("necro-rise", ("target", Identity.Entity(target, EntityManager))),
            target, PopupType.LargeCaution);
        SupernaturalBurst(Transform(target).Coordinates, ent.Comp.SupernaturalRange, 3);
        AddFearAround(ent, target, ent.Comp.WitnessRange, NecroFearSource.Reveal);
    }

    #endregion

    #region The Wicked Dead and the Army of Darkness

    private void OnSummonWickedDead(Entity<NecroDemonComponent> ent, ref NecroSummonWickedDeadEvent args)
    {
        if (!ValidatePurchase(ent))
            return;

        var coords = ServantSpawnCoords(ent);
        var risen = Spawn(ent.Comp.WickedDeadPrototype, coords);
        _transform.AttachToGridOrMap(risen);

        _deadite.MakeDeadite(risen, ent);
        if (TryComp<DeaditeComponent>(risen, out var deadite))
        {
            // Older and stronger than any possessed crew member, and much harder to cut apart.
            deadite.RevealRewarded = true;
            deadite.RevealedDamage = new Content.Shared.Damage.DamageSpecifier
            {
                DamageDict = new() { { "Slash", 12 }, { "Blunt", 8 } },
            };
            deadite.SeverThreshold = 25;
            _deadite.Reveal((risen, deadite));
        }

        _audio.PlayPvs(ent.Comp.DemonspawnSummonSound, risen);
        _popup.PopupEntity(Loc.GetString("necro-wicked-dead"), risen, PopupType.LargeCaution);
        SupernaturalBurst(coords, ent.Comp.SupernaturalRange, 4);
        AddFearAround(ent, risen, ent.Comp.WitnessRange, NecroFearSource.Supernatural);
    }

    private void OnRaiseArmy(Entity<NecroDemonComponent> ent, ref NecroRaiseArmyEvent args)
    {
        if (!ValidatePurchase(ent))
            return;

        var coords = ServantSpawnCoords(ent);
        for (var i = 0; i < ent.Comp.ArmySize; i++)
        {
            var offset = _random.NextVector2(0.5f, 1.5f);
            var skeleton = Spawn(ent.Comp.ArmyPrototype, coords.Offset(offset));
            _transform.AttachToGridOrMap(skeleton);
            EnsureComp<KandarianEntityComponent>(skeleton).Demon = ent;
            ent.Comp.ArmyRaised++;
        }

        Chant(ent.Comp.Manifestation ?? ent.Owner, 3);
        _popup.PopupCoordinates(Loc.GetString("necro-army-rises"), coords, PopupType.LargeCaution);
        SupernaturalBurst(coords, ent.Comp.SupernaturalRange, 4);
    }

    #endregion
}
