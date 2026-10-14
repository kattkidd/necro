using Content.Server.Necronomicon.Components;
using Content.Shared.Chat;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Necronomicon;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Necronomicon;

/// <summary>
/// Fear -> Soul Power.
/// Anti-farming: every reward needs an eligible (player-controlled, living, non-Kandarian) victim,
/// each victim has a per-source cooldown, and each victim accumulates "fatigue" that divides further rewards
/// until it decays. Mindshielded victims only yield a fraction.
/// </summary>
public sealed partial class NecronomiconSystem
{
    private void InitializeFear()
    {
        SubscribeLocalEvent<KandarianEntityComponent, EntitySpokeEvent>(OnKandarianSpoke);
        SubscribeLocalEvent<KandarianEntityComponent, MeleeHitEvent>(OnKandarianMeleeHit);
    }

    /// <summary>
    /// Attempts to grant Fear from <paramref name="victim"/> to the demon. Returns the SP actually granted.
    /// </summary>
    public FixedPoint2 AddFear(Entity<NecroDemonComponent> demon, EntityUid victim, NecroFearSource source, float scale = 1f)
    {
        if (!demon.Comp.Awakened || demon.Comp.Banished)
            return FixedPoint2.Zero;

        if (!IsEligibleSoul(victim))
            return FixedPoint2.Zero;

        if (!demon.Comp.FearSources.TryGetValue(source, out var config))
            return FixedPoint2.Zero;

        var now = _timing.CurTime;
        if (!demon.Comp.Victims.TryGetValue(victim, out var record))
        {
            record = new NecroVictimFear { LastUpdate = now };
            demon.Comp.Victims[victim] = record;
        }

        if (record.NextAllowed.TryGetValue(source, out var next) && now < next)
            return FixedPoint2.Zero;

        // Decay fatigue since the last reward.
        var elapsed = (float) (now - record.LastUpdate).TotalSeconds;
        record.Fatigue = MathF.Max(0f, record.Fatigue - elapsed * demon.Comp.FatigueDecayPerSecond);
        record.LastUpdate = now;
        record.NextAllowed[source] = now + config.Cooldown;

        var reward = config.Reward * scale * demon.Comp.FearMultiplier / (1f + record.Fatigue);
        if (IsMindshielded(victim))
            reward *= demon.Comp.MindshieldFearScale;

        record.Fatigue += demon.Comp.FatiguePerReward;

        var amount = FixedPoint2.New(reward);
        if (amount <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        demon.Comp.TotalFearEarned += amount;
        AddSoulPower(demon, amount);
        return amount;
    }

    /// <summary>Grants fear to every eligible soul who can see <paramref name="origin"/>.</summary>
    public void AddFearAround(Entity<NecroDemonComponent> demon, EntityUid origin, float range, NecroFearSource source,
        float scale = 1f, EntityUid? exclude = null)
    {
        var coords = Transform(origin).Coordinates;
        foreach (var victim in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, range))
        {
            if (victim.Owner == exclude || victim.Owner == origin)
                continue;

            if (!_interaction.InRangeUnobstructed(victim.Owner, origin, range))
                continue;

            AddFear(demon, victim.Owner, source, scale);
        }
    }

    /// <summary>Is this Kandarian entity currently visibly monstrous (and therefore frightening)?</summary>
    public bool IsVisiblyMonstrous(EntityUid uid)
    {
        if (TryComp<DeaditeComponent>(uid, out var deadite))
            return deadite.Revealed;

        return HasComp<DemonspawnComponent>(uid) || HasComp<NecroManifestationComponent>(uid);
    }

    private void UpdateProximityFear(Entity<NecroDemonComponent> demon, TimeSpan now)
    {
        if (!demon.Comp.Awakened || demon.Comp.Banished || now < demon.Comp.NextProximityCheck)
            return;

        demon.Comp.NextProximityCheck = now + demon.Comp.ProximityInterval;

        var query = EntityQueryEnumerator<KandarianEntityComponent>();
        while (query.MoveNext(out var uid, out var kandarian))
        {
            if (kandarian.Demon != demon.Owner || !IsVisiblyMonstrous(uid))
                continue;

            if (_mobState.IsIncapacitated(uid))
                continue;

            AddFearAround(demon, uid, demon.Comp.ProximityRange, NecroFearSource.Proximity, demon.Comp.ProximityMultiplier);
        }
    }

    private void OnKandarianSpoke(Entity<KandarianEntityComponent> ent, ref EntitySpokeEvent args)
    {
        // Radio (Ancient Sumerian) is private and frightens nobody.
        if (args.Channel != null || !IsVisiblyMonstrous(ent))
            return;

        if (!TryGetDemon(ent.Comp.Demon, out var demon))
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextSpeechFear)
            return;

        ent.Comp.NextSpeechFear = now + TimeSpan.FromSeconds(10);
        AddFearAround(demon.Value, ent, demon.Value.Comp.WitnessRange, NecroFearSource.Speech);
    }

    private void OnKandarianMeleeHit(Entity<KandarianEntityComponent> ent, ref MeleeHitEvent args)
    {
        // Only unarmed / natural attacks are raised on the attacker itself.
        if (args.Weapon != args.User || args.HitEntities.Count == 0)
            return;

        if (!TryGetDemon(ent.Comp.Demon, out var demon))
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!IsEligibleSoul(hit))
                continue;

            AddFear(demon.Value, hit, NecroFearSource.Attacked);
            AddFearAround(demon.Value, hit, demon.Value.Comp.WitnessRange, NecroFearSource.Witness, exclude: hit);
        }
    }
}
