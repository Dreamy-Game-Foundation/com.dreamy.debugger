using System;
using System.ComponentModel;
using System.Linq;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
using NUnit.Framework;

namespace Dreamy.Debugger.Tests
{
    public sealed class AttributedCardTests
    {
        private sealed class ResourceFixture
        {
            [System.ComponentModel.Category("Economy"), ResourceCheat("game.coins", "Coins", CheatCardRole.Balance)]
            public long Balance => 100;

            [System.ComponentModel.Category("Economy"), ResourceCheat("game.coins", "Coins", CheatCardRole.Amount), NumberRange(1, 1000)]
            public long Amount { get; set; } = 25;

            [System.ComponentModel.Category("Economy"), ResourceCheat("game.coins", "Coins", CheatCardRole.Action)]
            public void Add() { }

            [CheatId("game.stable-status"), DisplayName("Status")]
            public string RenamedStatus => "Ready";
        }

        private sealed class InvalidFixture
        {
            [ResourceCheat("game.invalid", "Invalid", CheatCardRole.Balance)]
            public int WritableBalance { get; set; }
        }

        [Test] public void CardAttributesGenerateStableIdsAndCompleteMetadata()
        {
            var options = AttributedCheatOptions.From(new ResourceFixture(), "unused").ToArray();
            Assert.That(options.Select(o => o.Id), Is.EquivalentTo(new[]
            {
                "game.coins.balance", "game.coins.amount", "game.coins.grant", "game.stable-status"
            }));
            var card = options.Where(o => o.Card != null).ToArray();
            Assert.That(card.Length, Is.EqualTo(3));
            Assert.That(card.All(o => o.Card.Id == "game.coins" && o.Card.Title == "Coins"), Is.True);
            Assert.That(card.Single(o => o.Card.Role == CheatCardRole.Action).RequiresConfirmation, Is.True);
            Assert.That(options.Single(o => o.Id == "game.stable-status").DisplayName, Is.EqualTo("Status"));
        }

        [Test] public void InvalidCardAndDuplicateContainersNeverPartiallyRegister()
        {
            var registry = new CheatRegistry(() => true);
            Assert.Throws<ArgumentException>(() => registry.Register(AttributedCheatOptions.From(new InvalidFixture(), "test")));
            Assert.That(registry.Snapshot(), Is.Empty);
            using (registry.Register(AttributedCheatOptions.From(new ResourceFixture(), "test")))
            {
                Assert.Throws<ArgumentException>(() => registry.Register(AttributedCheatOptions.From(new ResourceFixture(), "other")));
                Assert.That(registry.Snapshot().Count, Is.EqualTo(4));
            }
            Assert.That(registry.Snapshot(), Is.Empty);
        }

        [Test] public void OwnershipIdsAreGrantOnlyAndRejectResourceRoles()
        {
            var owned = new OwnershipCheatAttribute("game.ads", "No Ads", CheatCardRole.Owned);
            var grant = new OwnershipCheatAttribute("game.ads", "No Ads", CheatCardRole.Action);
            Assert.That(owned.OptionId, Is.EqualTo("game.ads.owned"));
            Assert.That(grant.OptionId, Is.EqualTo("game.ads.unlock"));
            Assert.Throws<ArgumentException>(() => new OwnershipCheatAttribute("x", "Invalid", CheatCardRole.Amount));
            Assert.Throws<ArgumentException>(() => new ResourceCheatAttribute("x", "Invalid", CheatCardRole.Owned));
        }
    }
}
