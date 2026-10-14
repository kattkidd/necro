using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Necronomicon;
using Content.Server.Necronomicon.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;

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
        SEntMan.System<NecronomiconSystem>().Awaken((uid, demon), coords);
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

            Assert.That(necro.IsBookManifested(demon, out var book), Is.True, "Awakening manifests the book");
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
}
