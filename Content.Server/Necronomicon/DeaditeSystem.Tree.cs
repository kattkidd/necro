using Content.Server.Necronomicon.Components;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Necronomicon;

/// <summary>
/// The Deadite progression tree, bought by the demon in the Skill Store. Every tier applies to all of the demon's
/// Deadites, present and future, because each effect is looked up from the demon's store purchases when it is used.
/// <list type="bullet">
/// <item>Infiltrator: silent disguised bites (I); slower exorcisms and a Conceal action to hide again (II).</item>
/// <item>Brute: heavier revealed blows and faster recovery (I); damage resistance and tougher necks (II).</item>
/// <item>Stalker: faster revealed movement and longer bite reach (I); bites slow the victim (II).</item>
/// <item>Screamer: an area shriek that frightens the crew (I); the shriek also knocks nearby crew down (II).</item>
/// </list>
/// Prerequisites, costs and the two-path limit are store conditions (validated server-side by StoreSystem).
/// </summary>
public sealed partial class DeaditeSystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly EntProtoId ScreechEffect = "EffectScreech";

    private static readonly SoundSpecifier ScreamShriek =
        new SoundCollectionSpecifier("DeaditeScreams", AudioParams.Default.WithVolume(6f).WithMaxDistance(20f));

    private void InitializeTree()
    {
        SubscribeLocalEvent<DeaditeComponent, DeaditeScreamActionEvent>(OnScream);
        SubscribeLocalEvent<DeaditeComponent, DeaditeConcealActionEvent>(OnConceal);
    }

    /// <summary>Whether this Deadite's demon has bought <paramref name="upgrade"/>.</summary>
    public bool HasTree(DeaditeComponent deadite, NecroUpgrade upgrade)
    {
        return _necro.TryGetDemon(deadite.Demon, out var demon) && _necro.HasUpgrade(demon.Value, upgrade);
    }

    /// <summary>
    /// Tree purchases waiting to be pushed to existing Deadites. The store only counts a purchase after raising its
    /// product event, so the refresh happens on the next tick when <see cref="HasTree"/> can see it.
    /// </summary>
    private readonly List<(EntityUid Demon, NecroUpgrade Upgrade)> _pendingTree = new();

    /// <summary>
    /// Called when the demon buys a tree tier: hands any new actions to every Deadite it already owns.
    /// </summary>
    public void OnTreeUpgrade(Entity<NecroDemonComponent> demon, NecroUpgrade upgrade)
    {
        _pendingTree.Add((demon.Owner, upgrade));
    }

    private void UpdateTree()
    {
        if (_pendingTree.Count == 0)
            return;

        foreach (var (demon, upgrade) in _pendingTree)
        {
            var query = EntityQueryEnumerator<DeaditeComponent>();
            while (query.MoveNext(out var uid, out var deadite))
            {
                if (deadite.Demon != demon)
                    continue;

                GrantTreeActions((uid, deadite));
                _speed.RefreshMovementSpeedModifiers(uid);

                _popup.PopupCursor(Loc.GetString($"deadite-tree-gained-{upgrade.ToString().ToLowerInvariant()}"),
                    uid, PopupType.Medium);
            }
        }

        _pendingTree.Clear();
    }

    /// <summary>Gives a Deadite whichever tree actions it is entitled to and doesn't have yet.</summary>
    private void GrantTreeActions(Entity<DeaditeComponent> ent)
    {
        if (HasTree(ent.Comp, NecroUpgrade.ScreamerI) && ent.Comp.ScreamActionEntity == null)
            _actions.AddAction(ent, ref ent.Comp.ScreamActionEntity, ent.Comp.ScreamAction);

        // Conceal only makes sense while revealed; it's swapped with Reveal back and forth.
        if (ent.Comp.Revealed && HasTree(ent.Comp, NecroUpgrade.InfiltratorII) && ent.Comp.ConcealActionEntity == null)
        {
            _actions.AddAction(ent, ref ent.Comp.ConcealActionEntity, ent.Comp.ConcealAction);
            // No instant reveal-then-hide: the mask takes a while to grow back.
            _actions.StartUseDelay(ent.Comp.ConcealActionEntity);
        }
    }

    #region Screamer

    private void OnScream(Entity<DeaditeComponent> ent, ref DeaditeScreamActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_necro.TryGetDemon(ent.Comp.Demon, out var demon) || !_necro.CanUsePowers(demon.Value, ent))
            return;

        if (!HasTree(ent.Comp, NecroUpgrade.ScreamerI))
            return;

        // A disguised Deadite can't shriek without giving itself away, so it has to reveal first.
        // (A severed head can still scream.)
        if (!ent.Comp.Revealed)
        {
            _popup.PopupCursor(Loc.GetString("deadite-scream-not-revealed"), ent, PopupType.SmallCaution);
            return;
        }

        if (!_mobState.IsAlive(ent))
            return;

        args.Handled = true;

        var coords = Transform(ent).Coordinates;
        _audio.PlayPvs(ScreamShriek, ent);
        Spawn(ScreechEffect, coords);
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), true, 10f, 8f);
        _popup.PopupEntity(Loc.GetString("deadite-scream", ("user", Identity.Entity(ent, EntityManager))),
            ent, PopupType.LargeCaution);

        var knockdown = HasTree(ent.Comp, NecroUpgrade.ScreamerII);
        foreach (var victim in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, ent.Comp.ScreamRange))
        {
            var uid = victim.Owner;
            if (uid == ent.Owner || _necro.IsKandarian(uid) || !_mobState.IsAlive(uid))
                continue;

            _jitter.DoJitter(uid, TimeSpan.FromSeconds(3), true, 8f, 6f);
            _popup.PopupCursor(Loc.GetString("deadite-scream-victim"), uid, PopupType.MediumCaution);

            if (!knockdown || _necro.IsMindshielded(uid))
                continue;

            if (!_transform.InRange(coords, Transform(uid).Coordinates, ent.Comp.ScreamKnockdownRange))
                continue;

            _stun.TryKnockdown(uid, ent.Comp.ScreamKnockdownTime);
            // Being flattened by it is worse than hearing it.
            _necro.AddFear(demon.Value, uid, NecroFearSource.Attacked);
        }

        _necro.AddFearAround(demon.Value, ent, ent.Comp.ScreamRange, NecroFearSource.Supernatural);
    }

    #endregion

    #region Infiltrator

    private void OnConceal(Entity<DeaditeComponent> ent, ref DeaditeConcealActionEvent args)
    {
        if (args.Handled || !ent.Comp.Revealed)
            return;

        if (!HasTree(ent.Comp, NecroUpgrade.InfiltratorII))
            return;

        if (ent.Comp.Severed || !_mobState.IsAlive(ent))
        {
            _popup.PopupCursor(Loc.GetString("deadite-conceal-fail"), ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        Conceal(ent);
    }

    /// <summary>Puts the human mask back on: original colours, normal voice, normal name, normal speed.</summary>
    public void Conceal(Entity<DeaditeComponent> ent)
    {
        if (!ent.Comp.Revealed)
            return;

        ent.Comp.Revealed = false;
        RestoreOriginalLook(ent.Comp, ent);

        _nameMod.RefreshNameModifiers(ent.Owner);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);

        _actions.RemoveAction(ent.Owner, ent.Comp.ConcealActionEntity);
        ent.Comp.ConcealActionEntity = null;
        _actions.RemoveAction(ent.Owner, ent.Comp.TauntActionEntity);
        ent.Comp.TauntActionEntity = null;
        _actions.RemoveAction(ent.Owner, ent.Comp.SwallowActionEntity);
        ent.Comp.SwallowActionEntity = null;
        _actions.AddAction(ent, ref ent.Comp.RevealActionEntity, ent.Comp.RevealAction);

        _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), true, 6f, 4f);
        _popup.PopupEntity(Loc.GetString("deadite-conceal", ("target", Identity.Entity(ent, EntityManager))),
            ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
        _popup.PopupCursor(Loc.GetString("deadite-conceal-self"), ent, PopupType.Medium);
    }

    /// <summary>Restores the skin and eye colours captured before the first reveal.</summary>
    private void RestoreOriginalLook(DeaditeComponent comp, EntityUid uid)
    {
        if (comp.OriginalProfiles is not { } original)
            return;

        _visualBody.ApplyProfiles(uid, new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>(original));
    }

    #endregion
}
