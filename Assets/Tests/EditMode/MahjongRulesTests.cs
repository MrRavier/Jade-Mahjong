using System.Linq;
using JadeMahjong.Core;
using JadeMahjong.Networking;
using NUnit.Framework;

namespace JadeMahjong.Tests
{
    public sealed class MahjongRulesTests
    {
        [Test]
        public void JadePalaceContainsTraditional144Tiles()
        {
            var deal = ShanghaiBoardFactory.Create(20260831);
            Assert.That(deal.Slots.Count, Is.EqualTo(144));
            Assert.That(deal.GuaranteedSolution.Count, Is.EqualTo(72));

            for (var kind = 0; kind < 34; kind++)
                Assert.That(deal.Slots.Count(tile => tile.Kind == kind), Is.EqualTo(4),
                    $"Kind {kind} should appear four times.");
            for (var kind = 34; kind < 42; kind++)
                Assert.That(deal.Slots.Count(tile => tile.Kind == kind), Is.EqualTo(1),
                    $"Bonus kind {kind} should appear once.");
        }

        [TestCase(1)]
        [TestCase(77)]
        [TestCase(20260831)]
        [TestCase(-912345)]
        public void GeneratedDealCanBeClearedByItsGuaranteedSolution(int seed)
        {
            var deal = ShanghaiBoardFactory.Create(seed);
            var board = new MahjongBoardModel(deal);
            foreach (var pair in deal.GuaranteedSolution)
            {
                Assert.That(board.IsFree(pair.firstId), Is.True,
                    $"First tile {pair.firstId} was blocked with {board.Remaining} remaining.");
                Assert.That(board.IsFree(pair.secondId), Is.True,
                    $"Second tile {pair.secondId} was blocked with {board.Remaining} remaining.");
                Assert.That(board.TryRemove(pair.firstId, pair.secondId), Is.EqualTo(RemoveResult.Removed));
            }
            Assert.That(board.IsCleared, Is.True);
        }

        [Test]
        public void SameSeedCreatesSameBoard()
        {
            var first = ShanghaiBoardFactory.Create(4567);
            var second = ShanghaiBoardFactory.Create(4567);
            Assert.That(first.Slots.Select(tile => tile.Kind),
                Is.EqualTo(second.Slots.Select(tile => tile.Kind)));
        }

        [Test]
        public void DifferentSeedsChangeTheDeal()
        {
            var first = ShanghaiBoardFactory.Create(11);
            var second = ShanghaiBoardFactory.Create(12);
            Assert.That(first.Slots.Select(tile => tile.Kind),
                Is.Not.EqualTo(second.Slots.Select(tile => tile.Kind)));
        }

        [Test]
        public void FlowersAndSeasonsUseFamilyMatching()
        {
            Assert.That(MahjongBoardModel.KindsMatch(34, 37), Is.True);
            Assert.That(MahjongBoardModel.KindsMatch(38, 41), Is.True);
            Assert.That(MahjongBoardModel.KindsMatch(34, 38), Is.False);
            Assert.That(MahjongBoardModel.KindsMatch(7, 8), Is.False);
            Assert.That(MahjongBoardModel.KindsMatch(7, 7), Is.True);
        }

        [Test]
        public void RoomCodeRoundTripsPrivateIpv4()
        {
            var code = LanRoomCode.Encode("192.168.1.37");
            Assert.That(code.Length, Is.EqualTo(9));
            Assert.That(LanRoomCode.TryDecode(code, out var address), Is.True);
            Assert.That(address, Is.EqualTo("192.168.1.37"));
        }

        [Test]
        public void RoomCodeRejectsChangedChecksum()
        {
            var code = LanRoomCode.Encode("10.0.0.42");
            var changed = code.Substring(0, 8) + (code[8] == '0' ? '1' : '0');
            Assert.That(LanRoomCode.TryDecode(changed, out _), Is.False);
        }

        [Test]
        public void RedealPreservesRemainingKindsAndProvidesAMove()
        {
            var board = new MahjongBoardModel(ShanghaiBoardFactory.Create(9001));
            var before = board.Slots.Where(tile => tile.Active).Select(tile => tile.Kind).OrderBy(x => x).ToArray();
            board.RedealActive(501);
            var after = board.Slots.Where(tile => tile.Active).Select(tile => tile.Kind).OrderBy(x => x).ToArray();
            Assert.That(after, Is.EqualTo(before));
            Assert.That(board.HasAvailablePair(), Is.True);
        }
    }
}
