using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Necronomicon;
using Content.Server.Necronomicon.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Necronomicon;

/// <summary>
/// Core loops of the Necronomicon antagonist: the pin rule, possession charges, disguise/reveal,
/// fear anti-farming and burning the book.
/// </summary>
[TestOf(typeof(NecronomiconSystem))]
public sealed class NecronomiconTests : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private Entity<NecroDemonComponent> SpawnAwakeDemon()
    {
        var coords = SEntMan.GetCoordinates(PlayerCoords);
        var uid = SEntMan.SpawnEntity("MobKandarianDemon", coords);
        var demon = SEntMan.GetComponent<NecroDemonComponent>(uid);
        SEntMan.EnsureComponent<KandarianEntityComponent>(uid).Demon = uid;
        var necro = SEntMan.System<NecronomiconSystem>();
        necro.Awaken((uid, demon));
        Assert.That(necro.IsBookManifested((uid, demon), out _), Is.False, "The book starts in the ether");
        necro.ManifestBook((uid, demon), coords);
        return (uid, demon);
    }

    [Test]
    public async Task DemonIsIncorporealAndStartsWithTwoCharges()
    {
        await Server.WaitAssertion(() =>
        {
            var demon = SpawnAwakeDemon();
            Assert.That(demon.Comp.PossessionCharges, Is.EqualTo(2));
            Assert.That(SEntMan.HasComponent<Content.Shared.Damage.Components.DamageableComponent>(demon), Is.False,
                "Incorporeal demon must not be damageable");
            Assert.That(SEntMan.HasComponent<Content.Shared.Hands.Components.HandsComponent>(demon), Is.False,
                "Incorporeal demon must not be able to hold or interact physically");
            Assert.That(SEntMan.HasComponent<Content.Shared.Weapons.Melee.MeleeWeaponComponent>(demon), Is.False,
                "Incorporeal demon must not be able to attack");
        });
    }

    [Test]
    public async Task CrewHoldingBookPreventsDemanifest()
    {
        await Server.WaitAssertion(() =>
        {
            var necro = SEntMan.System<NecronomiconSystem>();
            var hands = SEntMan.System<SharedHandsSystem>();
            var demon = SpawnAwakeDemon();

            Assert.That(necro.IsBookManifested(demon, out var book), Is.True, "Manifesting puts the book in the world");
            Assert.That(hands.TryPickupAnyHand(SPlayer, book!.Value), Is.True);

            Assert.That(necro.IsPinnedByCrew(book.Value, out var carrier), Is.True);
            Assert.That(carrier, Is.EqualTo(SPlayer));
            Assert.That(necro.DemanifestBook(demon), Is.False, "A non-Deadite holding the book must pin it");
            Assert.That(necro.IsBookManifested(demon, out _), Is.True);

            Assert.That(hands.TryDrop(SPlayer, book.Value, checkActionBlocker: false), Is.True);
            Assert.That(necro.DemanifestBook(demon), Is.True, "Dropped book can return to the ether");
            Assert.That(necro.IsBookManifested(demon, out _), Is.False);
        });
    }

    [Test]
    public async Task PossessionConsumesChargeAndDeaditesStartDisguised()
    {
        await Server.WaitAssertion(() =>
        {
            var necro = SEntMan.System<NecronomiconSystem>();
            var deadite = SEntMan.System<DeaditeSystem>();
            var demon = SpawnAwakeDemon();
            var originalName = SEntMan.GetComponent<MetaDataComponent>(SPlayer).EntityName;

            Assert.That(necro.CanBecomeDeadite(SPlayer, null), Is.True);
            necro.Prepare(demon, SPlayer);
            Assert.That(necro.ConvertToDeadite(demon, SPlayer), Is.True);
            Assert.That(demon.Comp.PossessionCharges, Is.EqualTo(1));

            var comp = SEntMan.GetComponent<DeaditeComponent>(SPlayer);
            Assert.That(comp.Revealed, Is.False);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(SPlayer).EntityName, Is.EqualTo(originalName),
                "Disguised Deadites keep their name");

            // No duplicate conversions.
            Assert.That(necro.CanBecomeDeadite(SPlayer, null), Is.False);
            Assert.That(necro.ConvertToDeadite(demon, SPlayer), Is.False);
            Assert.That(demon.Comp.PossessionCharges, Is.EqualTo(1));

            deadite.Reveal((SPlayer, comp));
            Assert.That(comp.Revealed, Is.True);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(SPlayer).EntityName, Does.Contain("Deadite"));
        });
    }

    [Test]
    public async Task FearHasCooldownsAndDiminishingReturns()
    {
        await Server.WaitAssertion(() =>
        {
            var necro = SEntMan.System<NecronomiconSystem>();
            var demon = SpawnAwakeDemon();

            var first = necro.AddFear(demon, SPlayer, NecroFearSource.Attacked);
            var second = necro.AddFear(demon, SPlayer, NecroFearSource.Attacked);
            var other = necro.AddFear(demon, SPlayer, NecroFearSource.Witness);

            Assert.That(first, Is.GreaterThan(Content.Shared.FixedPoint.FixedPoint2.Zero));
            Assert.That(second, Is.EqualTo(Content.Shared.FixedPoint.FixedPoint2.Zero), "Same source is on cooldown");
            Assert.That(other, Is.LessThan(first), "Fatigue reduces further rewards from the same victim");

            // Kandarian entities and non-souls never generate fear.
            Assert.That(necro.AddFear(demon, demon.Owner, NecroFearSource.Attacked),
                Is.EqualTo(Content.Shared.FixedPoint.FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task BurningBookBanishesDemonAndReleasesDeadites()
    {
        await Server.WaitAssertion(() =>
        {
            var necro = SEntMan.System<NecronomiconSystem>();
            var mobState = SEntMan.System<MobStateSystem>();
            var demon = SpawnAwakeDemon();

            necro.Prepare(demon, SPlayer);
            necro.ConvertToDeadite(demon, SPlayer);
            Assert.That(necro.IsBookManifested(demon, out var book), Is.True);

            necro.DestroyBook((book!.Value, SEntMan.GetComponent<NecronomiconComponent>(book.Value)));

            Assert.That(demon.Comp.Banished, Is.True);
            Assert.That(demon.Comp.PossessionCharges, Is.EqualTo(0));
            Assert.That(SEntMan.HasComponent<DeaditeComponent>(SPlayer), Is.False, "Deadites are released");
            Assert.That(mobState.IsDead(SPlayer), Is.True, "Released Deadites collapse");
        });
    }

    [Test]
    public async Task DemonicPossessionIsHarmlessAndTemporary()
    {
        await Server.WaitAssertion(() =>
        {
            var necro = SEntMan.System<NecronomiconSystem>();
            var standing = SEntMan.System<Content.Shared.Standing.StandingStateSystem>();
            var blocker = SEntMan.System<Content.Shared.ActionBlocker.ActionBlockerSystem>();
            var damageable = SEntMan.System<Content.Shared.Damage.Systems.DamageableSystem>();
            var demon = SpawnAwakeDemon();

            necro.StartPuppet(demon, SPlayer);
            Assert.That(demon.Comp.PuppetHost, Is.EqualTo(SPlayer));
            Assert.That(SEntMan.HasComponent<NecroPuppetHostComponent>(SPlayer), Is.True);
            Assert.That(standing.IsDown(SPlayer), Is.True, "Host collapses");
            Assert.That(blocker.CanMove(SPlayer), Is.False, "Host cannot move");
            Assert.That(blocker.CanSpeak(SPlayer), Is.False, "Host cannot speak for themselves");
            Assert.That(damageable.GetTotalDamage(SPlayer), Is.EqualTo(Content.Shared.FixedPoint.FixedPoint2.Zero), "Harmless");
            Assert.That(SEntMan.HasComponent<DeaditeComponent>(SPlayer), Is.False, "Not a Deadite conversion");
            Assert.That(demon.Comp.PossessionCharges, Is.EqualTo(2), "Uses no possession charge");

            necro.EndPuppet(demon);
            Assert.That(demon.Comp.PuppetHost, Is.Null);
            Assert.That(SEntMan.HasComponent<NecroPuppetHostComponent>(SPlayer), Is.False);
            Assert.That(standing.IsDown(SPlayer), Is.False, "Host gets back up");
            Assert.That(blocker.CanMove(SPlayer), Is.True);
        });
    }

    [Test]
    public async Task ExorcismFreesDeaditeAndBlessesThem()
    {
        await Server.WaitAssertion(() =>
        {
            var necro = SEntMan.System<NecronomiconSystem>();
            var deadite = SEntMan.System<DeaditeSystem>();
            var mobState = SEntMan.System<MobStateSystem>();
            var demon = SpawnAwakeDemon();
            var originalName = SEntMan.GetComponent<MetaDataComponent>(SPlayer).EntityName;

            necro.Prepare(demon, SPlayer);
            Assert.That(necro.ConvertToDeadite(demon, SPlayer), Is.True);
            var comp = SEntMan.GetComponent<DeaditeComponent>(SPlayer);
            deadite.Reveal((SPlayer, comp));

            deadite.Exorcise((SPlayer, comp), null, TimeSpan.FromMinutes(10));

            Assert.That(SEntMan.HasComponent<DeaditeComponent>(SPlayer), Is.False, "The curse is cast out");
            Assert.That(necro.IsKandarian(SPlayer), Is.False);
            Assert.That(mobState.IsAlive(SPlayer), Is.True, "Exorcism restores the victim, it doesn't kill them");
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(SPlayer).EntityName, Is.EqualTo(originalName));
            Assert.That(SEntMan.HasComponent<NecroBlessedComponent>(SPlayer), Is.True);
            Assert.That(necro.CanBecomeDeadite(SPlayer, null), Is.False, "Blessed crew can't be claimed again right away");

            Assert.That(demon.Comp.Exorcisms, Is.EqualTo(1));
            Assert.That(demon.Comp.ExorcisedNames, Has.Count.EqualTo(1));
            Assert.That(demon.Comp.Conversions, Is.EqualTo(1), "The conversion still counts in the statistics");
        });
    }

    [Test]
    public async Task DemonspawnMaturesExactlyOnce()
    {
        await Server.WaitAssertion(() =>
        {
            var spawnSys = SEntMan.System<DemonspawnSystem>();
            var demon = SpawnAwakeDemon();

            var juvenile = SEntMan.SpawnEntity("MobDemonspawn", SEntMan.GetCoordinates(PlayerCoords));
            SEntMan.EnsureComponent<KandarianEntityComponent>(juvenile).Demon = demon;
            var comp = SEntMan.GetComponent<DemonspawnComponent>(juvenile);
            Assert.That(comp.Adult, Is.False);
            Assert.That(comp.MatureAt, Is.Not.Null, "Juveniles start their growth timer when summoned");

            var adult = spawnSys.Mature((juvenile, comp));
            Assert.That(adult, Is.Not.Null);
            Assert.That(spawnSys.Mature((juvenile, comp)), Is.Null, "Maturation only ever happens once");

            var adultComp = SEntMan.GetComponent<DemonspawnComponent>(adult!.Value);
            Assert.That(adultComp.Adult, Is.True);
            Assert.That(SEntMan.GetComponent<KandarianEntityComponent>(adult.Value).Demon, Is.EqualTo(demon.Owner),
                "The adult keeps its allegiance");
            Assert.That(demon.Comp.DemonspawnMatured, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task HolyWaterMeltsDemonspawnButNotDeadites()
    {
        await Server.WaitAssertion(() =>
        {
            var necro = SEntMan.System<NecronomiconSystem>();
            var reactive = SEntMan.System<Content.Shared.Chemistry.ReactiveSystem>();
            var demon = SpawnAwakeDemon();

            var spawn = SEntMan.SpawnEntity("MobDemonspawn", SEntMan.GetCoordinates(PlayerCoords));
            var holyWater = new Content.Shared.Chemistry.Reagent.ReagentQuantity("Holywater", 5);
            reactive.ReactionEntity(spawn, Content.Shared.Chemistry.ReactionMethod.Touch, holyWater);
            Assert.That(SEntMan.GetComponent<DemonspawnComponent>(spawn).MeltAt, Is.Not.Null, "Holy water melts Demonspawn");

            necro.Prepare(demon, SPlayer);
            necro.ConvertToDeadite(demon, SPlayer);
            reactive.ReactionEntity(SPlayer, Content.Shared.Chemistry.ReactionMethod.Touch, holyWater);
            Assert.That(SEntMan.HasComponent<DeaditeComponent>(SPlayer), Is.True, "Deadites are not affected");
            Assert.That(SEntMan.Deleted(SPlayer), Is.False);
        });
    }
}
