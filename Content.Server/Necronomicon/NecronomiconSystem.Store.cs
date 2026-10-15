using Content.Server.Necronomicon.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Content.Shared.Store.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Random;

namespace Content.Server.Necronomicon;

/// <summary>
/// Skill Store purchases, additional powers and the physical form.
/// The store itself (balance display, costs, prerequisites, purchase feedback) is the stock StoreSystem;
/// listings are in Resources/Prototypes/Catalog/necronomicon_catalog.yml.
/// </summary>
public sealed partial class NecronomiconSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;

    private void InitializeStore()
    {
        SubscribeLocalEvent<NecroDemonComponent, NecroSelectFormEvent>(OnSelectForm);
        SubscribeLocalEvent<NecroDemonComponent, NecroBuyPossessionChargeEvent>(OnBuyCharge);
        SubscribeLocalEvent<NecroDemonComponent, NecroSummonDemonspawnEvent>(OnSummonDemonspawn);
        SubscribeLocalEvent<NecroDemonComponent, NecroUpgradeEvent>(OnUpgrade);

        SubscribeLocalEvent<NecroDemonComponent, NecroWhisperActionEvent>(OnWhisper);
        SubscribeLocalEvent<NecroDemonComponent, NecroDarkPresenceActionEvent>(OnDarkPresence);
        SubscribeLocalEvent<NecroDemonComponent, NecroManifestFormActionEvent>(OnManifestForm);

        SubscribeLocalEvent<NecroManifestationComponent, MapInitEvent>(OnManifestationMapInit);
        SubscribeLocalEvent<NecroManifestationComponent, NecroReturnToEtherActionEvent>(OnReturnToEther);
        SubscribeLocalEvent<NecroManifestationComponent, MobStateChangedEvent>(OnManifestationMobState);
        SubscribeLocalEvent<NecroManifestationComponent, EntityTerminatingEvent>(OnManifestationTerminating);
    }

    /// <summary>Upgrades are tracked through the store's purchase counts so they can't desync from the UI.</summary>
    public bool HasUpgrade(Entity<NecroDemonComponent> demon, NecroUpgrade upgrade)
    {
        return upgrade switch
        {
            NecroUpgrade.SwiftPossession => HasPurchased(demon, "NecroSwiftPossession"),
            NecroUpgrade.FarWhisper => HasPurchased(demon, "NecroFarWhisper"),
            NecroUpgrade.DreadAura => HasPurchased(demon, "NecroDreadAura"),
            NecroUpgrade.InfiltratorI => HasPurchased(demon, "NecroDeaditeInfiltrator1"),
            NecroUpgrade.InfiltratorII => HasPurchased(demon, "NecroDeaditeInfiltrator2"),
            NecroUpgrade.BruteI => HasPurchased(demon, "NecroDeaditeBrute1"),
            NecroUpgrade.BruteII => HasPurchased(demon, "NecroDeaditeBrute2"),
            NecroUpgrade.StalkerI => HasPurchased(demon, "NecroDeaditeStalker1"),
            NecroUpgrade.StalkerII => HasPurchased(demon, "NecroDeaditeStalker2"),
            NecroUpgrade.ScreamerI => HasPurchased(demon, "NecroDeaditeScreamer1"),
            NecroUpgrade.ScreamerII => HasPurchased(demon, "NecroDeaditeScreamer2"),
            _ => false,
        };
    }

    private bool HasPurchased(EntityUid store, string listingId)
    {
        if (!TryComp<StoreComponent>(store, out var storeComp))
            return false;

        foreach (var listing in storeComp.FullListingsCatalog)
        {
            if (listing.ID == listingId)
                return listing.PurchaseAmount > 0;
        }

        return false;
    }

    /// <summary>
    /// Store purchases are already validated server-side by the StoreSystem (balance, stock, prerequisites).
    /// This only blocks effects after banishment, when the store has been emptied anyway.
    /// </summary>
    private bool ValidatePurchase(Entity<NecroDemonComponent> demon)
    {
        return !demon.Comp.Banished;
    }

    private void OnSelectForm(Entity<NecroDemonComponent> ent, ref NecroSelectFormEvent args)
    {
        if (!ValidatePurchase(ent))
            return;

        ent.Comp.SelectedForm = args.Form;
        _appearance.SetData(ent, NecroDemonVisuals.Form, $"{args.Rsi}|{args.State}");
        _popup.PopupCursor(Loc.GetString("necro-form-selected"), ent, PopupType.Medium);
    }

    private void OnBuyCharge(Entity<NecroDemonComponent> ent, ref NecroBuyPossessionChargeEvent args)
    {
        if (!ValidatePurchase(ent))
            return;

        ent.Comp.PossessionCharges++;
        _popup.PopupCursor(Loc.GetString("necro-charge-bought", ("charges", ent.Comp.PossessionCharges)),
            ent, PopupType.Medium);
    }

    private void OnSummonDemonspawn(Entity<NecroDemonComponent> ent, ref NecroSummonDemonspawnEvent args)
    {
        if (!ValidatePurchase(ent))
            return;

        // Spawn at the book if it's out, otherwise wherever the demon is lurking.
        var coords = IsBookManifested(ent, out var book)
            ? Transform(book.Value).Coordinates
            : Transform(ent.Comp.Manifestation ?? ent.Owner).Coordinates;

        var spawn = Spawn(ent.Comp.DemonspawnPrototype, coords);
        _transform.AttachToGridOrMap(spawn);
        var kandarian = EnsureComp<KandarianEntityComponent>(spawn);
        kandarian.Demon = ent;
        ent.Comp.DemonspawnSummoned++;

        _popup.PopupEntity(Loc.GetString("necro-demonspawn-summoned"), spawn, PopupType.LargeCaution);
        SupernaturalBurst(coords, ent.Comp.SupernaturalRange, 3);
        _audio.PlayPvs(ent.Comp.DemonspawnSummonSound, spawn);
        AddFearAround(ent, spawn, ent.Comp.WitnessRange, NecroFearSource.Supernatural);
    }

    private void OnUpgrade(Entity<NecroDemonComponent> ent, ref NecroUpgradeEvent args)
    {
        if (!ValidatePurchase(ent))
            return;

        switch (args.Upgrade)
        {
            case NecroUpgrade.FearMultiplier:
                ent.Comp.FearMultiplier += ent.Comp.FearMultiplierStep;
                break;
            case NecroUpgrade.DreadAura:
                ent.Comp.ProximityMultiplier = 2f;
                break;
            case NecroUpgrade.HardenedFlesh:
                ent.Comp.HardenedFleshStacks++;
                break;
            case NecroUpgrade.RendingClaws:
                ent.Comp.RendingClawsStacks++;
                break;
            case NecroUpgrade.InfiltratorI:
            case NecroUpgrade.InfiltratorII:
            case NecroUpgrade.BruteI:
            case NecroUpgrade.BruteII:
            case NecroUpgrade.StalkerI:
            case NecroUpgrade.StalkerII:
            case NecroUpgrade.ScreamerI:
            case NecroUpgrade.ScreamerII:
                // The effects are read from the purchase count; existing Deadites just need new actions and a nudge.
                _deadite.OnTreeUpgrade(ent, args.Upgrade);
                break;
            // SwiftPossession / FarWhisper are read from the purchase count.
        }

        _popup.PopupCursor(Loc.GetString("necro-upgrade-bought"), ent, PopupType.Medium);
    }

    #region Powers

    private void OnWhisper(Entity<NecroDemonComponent> ent, ref NecroWhisperActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        var target = args.Target;
        if (!HasComp<HumanoidProfileComponent>(target) || IsKandarian(target))
            return;

        if (!_transform.InRange(Transform(args.Performer).Coordinates, Transform(target).Coordinates, ent.Comp.WhisperRange))
        {
            _popup.PopupCursor(Loc.GetString("necro-too-far"), args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!TrySpendSoulPower(ent, ent.Comp.WhisperCost, args.Performer))
            return;

        args.Handled = true;

        // Mindshields dull the whisper: shorter effect, but they still hear something.
        var shielded = IsMindshielded(target);
        var line = Loc.GetString($"necro-whisper-line-{_random.Next(1, 7)}");
        _audio.PlayGlobal(ent.Comp.WhisperSound, target);
        _popup.PopupCursor(line, target, shielded ? PopupType.Medium : PopupType.LargeCaution);
        _jitter.DoJitter(target, TimeSpan.FromSeconds(shielded ? 1 : 4), true, 6f, 5f);
        _popup.PopupCursor(Loc.GetString("necro-whisper-sent", ("target", Identity.Entity(target, EntityManager))),
            args.Performer);

        AddFear(ent, target, NecroFearSource.Supernatural);
    }

    private void OnDarkPresence(Entity<NecroDemonComponent> ent, ref NecroDarkPresenceActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        if (!TrySpendSoulPower(ent, ent.Comp.DarkPresenceCost, args.Performer))
            return;

        args.Handled = true;

        var coords = Transform(args.Performer).Coordinates;
        foreach (var uid in _lookup.GetEntitiesInRange(coords, ent.Comp.DarkPresenceRange))
        {
            // Lights flicker, lockers bang, machines glitch - an area-wide boo.
            _ghost.DoGhostBooEvent(uid, Content.Shared.Ghost.Components.GhostBooIntensity.Normal);
        }

        AddFearAround(ent, args.Performer, ent.Comp.DarkPresenceRange, NecroFearSource.Supernatural);
    }

    #endregion

    #region Physical manifestation

    private void OnManifestForm(Entity<NecroDemonComponent> ent, ref NecroManifestFormActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        if (ent.Comp.Manifestation != null)
            return;

        if (_timing.CurTime < ent.Comp.NextManifestForm)
        {
            _popup.PopupCursor(Loc.GetString("necro-form-recovering",
                ("seconds", (int) (ent.Comp.NextManifestForm - _timing.CurTime).TotalSeconds)),
                args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!_mind.TryGetMind(ent, out var mindId, out _))
            return;

        if (!TrySpendSoulPower(ent, ent.Comp.ManifestFormCost, args.Performer))
            return;

        args.Handled = true;

        var form = SpawnPhysicalForm(ent);

        var manifestation = EnsureComp<NecroManifestationComponent>(form);
        manifestation.Demon = ent;
        EnsureComp<KandarianEntityComponent>(form).Demon = ent;
        ent.Comp.Manifestation = form;

        // Stats are configured on the form prototype; store upgrades stack on top.
        if (ent.Comp.HardenedFleshStacks > 0 && _thresholds.TryGetDeadThreshold(form, out var dead))
        {
            var bonus = ent.Comp.HardenedFleshBonus * ent.Comp.HardenedFleshStacks;
            if (_thresholds.TryGetThresholdForState(form, MobState.Critical, out var crit))
                _thresholds.SetMobStateThreshold(form, crit.Value + bonus, MobState.Critical);
            _thresholds.SetMobStateThreshold(form, dead.Value + bonus, MobState.Dead);
        }

        if (ent.Comp.RendingClawsStacks > 0 && TryComp<MeleeWeaponComponent>(form, out var melee))
        {
            for (var i = 0; i < ent.Comp.RendingClawsStacks; i++)
                melee.Damage += ent.Comp.RendingClawsBonus;
            Dirty(form, melee);
        }

        _mind.TransferTo(mindId, form);

        // Powers of the ether (Whisper, Dark Presence, Demonic Possession...) live on the mind and would follow it
        // into the body. In the physical world the body gets its own: Unholy Roar, Poltergeist and Return to Ether.
        _actions.RemoveProvidedActions(form, mindId);

        _popup.PopupEntity(Loc.GetString("necro-form-manifest", ("demon", form)), form, PopupType.LargeCaution);
        Chant(form, _random.Next(ChantLines.Length));
        SupernaturalBurst(Transform(form).Coordinates, ent.Comp.SupernaturalRange, 6);
        AddFearAround(ent, form, ent.Comp.WitnessRange, NecroFearSource.Reveal);
    }

    /// <summary>
    /// Spawns the demon's body in the chosen form. Stats come from the form's prototype; the body carries the demon's
    /// name and glows in its aura colour.
    /// </summary>
    private EntityUid SpawnPhysicalForm(Entity<NecroDemonComponent> ent)
    {
        var creature = Spawn(ent.Comp.SelectedForm, Transform(ent).Coordinates);
        _transform.AttachToGridOrMap(creature);
        _metaData.SetEntityName(creature, Name(ent));
        _lights.SetColor(creature, ent.Comp.AuraColor);
        return creature;
    }

    private void OnManifestationMapInit(Entity<NecroManifestationComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ReturnActionEntity, ent.Comp.ReturnAction);
        _actions.AddAction(ent, ref ent.Comp.RoarActionEntity, ent.Comp.RoarAction);
        _actions.AddAction(ent, ref ent.Comp.PoltergeistActionEntity, ent.Comp.PoltergeistAction);
    }

    private void OnReturnToEther(Entity<NecroManifestationComponent> ent, ref NecroReturnToEtherActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ReturnToEther(ent, false);
    }

    private void OnManifestationMobState(EntityUid uid, NecroManifestationComponent comp, MobStateChangedEvent args)
    {
        // Critical or dead: the body is spent. (Character bodies go critical long before they die.)
        if (args.NewMobState != MobState.Dead && args.NewMobState != MobState.Critical)
            return;

        ReturnToEther(uid, true);
    }

    private void OnManifestationTerminating(Entity<NecroManifestationComponent> ent, ref EntityTerminatingEvent args)
    {
        // Gibbed, deleted, spaced: the demon survives, the book is untouched.
        ReturnToEther(ent, true, deleting: true);
    }

    /// <summary>Moves the mind back into the incorporeal demon and removes the physical form.</summary>
    public void ReturnToEther(EntityUid form, bool destroyed, bool deleting = false)
    {
        if (!TryComp<NecroManifestationComponent>(form, out var manifestation)
            || !TryGetDemon(manifestation.Demon, out var demon))
            return;

        if (demon.Value.Comp.Manifestation != form)
            return;

        demon.Value.Comp.Manifestation = null;
        manifestation.Demon = null;

        if (!TerminatingOrDeleted(demon.Value))
        {
            _transform.SetCoordinates(demon.Value, Transform(form).Coordinates);
            _transform.AttachToGridOrMap(demon.Value);

            if (_mind.TryGetMind(form, out var mindId, out _))
                _mind.TransferTo(mindId, demon.Value);

            if (destroyed)
            {
                demon.Value.Comp.NextManifestForm = _timing.CurTime + demon.Value.Comp.ManifestFormCooldownOnDeath;
                _popup.PopupCursor(Loc.GetString("necro-form-destroyed"), demon.Value, PopupType.LargeCaution);
            }
        }

        if (!deleting)
        {
            _popup.PopupEntity(Loc.GetString("necro-form-dissolves"), form, PopupType.MediumCaution);
            QueueDel(form);
        }
    }

    /// <summary>The incorporeal demon follows its body around so it reappears where the body was.</summary>
    private void UpdateManifestationFollow(Entity<NecroDemonComponent> demon)
    {
        if (demon.Comp.Manifestation is not { } form)
            return;

        if (TerminatingOrDeleted(form))
        {
            demon.Comp.Manifestation = null;
            return;
        }

        _transform.SetCoordinates(demon, Transform(form).Coordinates);
    }

    #endregion
}
