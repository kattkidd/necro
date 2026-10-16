using Content.Server.Necronomicon.Components;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Necronomicon;

/// <summary>
/// "I'll swallow your soul!" (Evil Dead II): a revealed Deadite feeds on a downed victim.
/// The Kandarian dagger: a downed Deadite struck with it is cut apart for good.
/// </summary>
public sealed partial class DeaditeSystem
{
    private void InitializeDeaditePowers()
    {
        SubscribeLocalEvent<DeaditeComponent, DeaditeSwallowSoulActionEvent>(OnSwallowSoul);
        SubscribeLocalEvent<DeaditeComponent, DeaditeSwallowSoulDoAfterEvent>(OnSwallowSoulDoAfter);
        SubscribeLocalEvent<NecroKandarianDaggerComponent, MeleeHitEvent>(OnDaggerHit);
        SubscribeLocalEvent<NecroLindaNecklaceComponent, AfterInteractEvent>(OnNecklaceShown);
    }

    /// <summary>
    /// Linda's necklace: shown to a Deadite, the person inside remembers who they are and the demon loses its grip
    /// for a few seconds. Shown to someone being puppeted, it breaks the puppeting.
    /// </summary>
    private void OnNecklaceShown(Entity<NecroLindaNecklaceComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User)
            return;

        var deadite = HasComp<DeaditeComponent>(target);
        var puppet = HasComp<NecroPuppetHostComponent>(target);
        if (!deadite && !puppet)
            return;

        args.Handled = true;
        var now = _timing.CurTime;
        if (now < ent.Comp.NextUse)
        {
            _popup.PopupCursor(Loc.GetString("necro-necklace-dim"), args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.NextUse = now + ent.Comp.Cooldown;

        if (puppet && TryComp<NecroPuppetHostComponent>(target, out var host) && _necro.TryGetDemon(host.Demon, out var puppeteer))
            _necro.EndPuppet(puppeteer.Value);

        if (deadite)
        {
            _stun.TryUpdateParalyzeDuration(target, ent.Comp.StunTime);
            _jitter.DoJitter(target, ent.Comp.StunTime, true, 10f, 6f);
            _popup.PopupCursor(Loc.GetString("necro-necklace-deadite"), target, PopupType.LargeCaution);
        }

        _popup.PopupEntity(Loc.GetString("necro-necklace-shown",
                ("user", Identity.Entity(args.User, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
            target, PopupType.LargeCaution);
    }

    private bool CanSwallow(Entity<DeaditeComponent> ent, EntityUid target)
    {
        return ent.Comp.Revealed && !ent.Comp.Severed
               && HasComp<HumanoidProfileComponent>(target) && !_necro.IsKandarian(target)
               && _mobState.IsCritical(target);
    }

    private void OnSwallowSoul(Entity<DeaditeComponent> ent, ref DeaditeSwallowSoulActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!CanSwallow(ent, target))
        {
            _popup.PopupCursor(Loc.GetString("deadite-swallow-invalid"), ent, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.SwallowTime, new DeaditeSwallowSoulDoAfterEvent(), ent, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
        _chat.TrySendInGameICMessage(ent, Loc.GetString("accent-deadite-4"), InGameICChatType.Speak, hideChat: false,
            ignoreActionBlocker: true);
        _popup.PopupEntity(Loc.GetString("deadite-swallow-start",
                ("user", Identity.Entity(ent, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
            ent, PopupType.LargeCaution);
        _jitter.DoJitter(target, ent.Comp.SwallowTime, true, 8f, 6f);
    }

    private void OnSwallowSoulDoAfter(Entity<DeaditeComponent> ent, ref DeaditeSwallowSoulDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!CanSwallow(ent, target))
            return;

        _damageable.TryChangeDamage(target, ent.Comp.SwallowDamage, ignoreResistances: true, origin: ent);
        _damageable.ClearAllDamage(ent.Owner);
        _popup.PopupEntity(Loc.GetString("deadite-swallow-done",
                ("user", Identity.Entity(ent, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
            target, PopupType.LargeCaution);

        if (_necro.TryGetDemon(ent.Comp.Demon, out var demon))
        {
            _necro.AddSoulPower(demon.Value, ent.Comp.SwallowReward);
            _necro.Chant(ent, 3);
            _necro.AddFearAround(demon.Value, ent, demon.Value.Comp.WitnessRange, NecroFearSource.Witness, exclude: target);
        }
    }

    private void OnDaggerHit(Entity<NecroKandarianDaggerComponent> ent, ref MeleeHitEvent args)
    {
        if (args.HitEntities.Count == 0)
            return;

        var bonus = false;
        foreach (var hit in args.HitEntities)
        {
            if (TryComp<DeaditeComponent>(hit, out var deadite) && !deadite.Severed && _mobState.IsCritical(hit))
            {
                Sever((hit, deadite));
                continue;
            }

            if (HasComp<NecroManifestationComponent>(hit) || HasComp<DemonspawnComponent>(hit))
                bonus = true;
        }

        if (bonus)
            args.BonusDamage += ent.Comp.DemonBonus;
    }
}
