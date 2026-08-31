using System;
using System.Collections.Generic;
using System.Linq;

namespace JadeMahjong.Core
{
    public enum MatchGroup
    {
        Exact,
        Flowers,
        Seasons
    }

    public enum RemoveResult
    {
        Removed,
        SameTile,
        Blocked,
        NotMatching,
        Missing
    }

    [Serializable]
    public sealed class TileSlot
    {
        public int Id { get; }
        public int Kind { get; internal set; }
        public int X2 { get; }
        public int Y2 { get; }
        public int Layer { get; }
        public bool Active { get; internal set; }

        public TileSlot(int id, int kind, int x2, int y2, int layer)
        {
            Id = id;
            Kind = kind;
            X2 = x2;
            Y2 = y2;
            Layer = layer;
            Active = true;
        }

        public TileSlot Clone()
        {
            return new TileSlot(Id, Kind, X2, Y2, Layer) { Active = Active };
        }
    }

    public sealed class MahjongDeal
    {
        public int Seed { get; }
        public IReadOnlyList<TileSlot> Slots { get; }
        public IReadOnlyList<(int firstId, int secondId)> GuaranteedSolution { get; }

        public MahjongDeal(int seed, IReadOnlyList<TileSlot> slots,
            IReadOnlyList<(int firstId, int secondId)> guaranteedSolution)
        {
            Seed = seed;
            Slots = slots;
            GuaranteedSolution = guaranteedSolution;
        }
    }

    public static class ShanghaiBoardFactory
    {
        public const int TileCount = 144;
        public const int KindCount = 42;

        public static MahjongDeal Create(int seed)
        {
            var slots = BuildJadePalaceSlots();
            var solution = BuildRemovalSchedule(slots);
            var pairs = BuildTraditionalPairs();
            var random = new JadeRandom(seed);
            random.Shuffle(pairs);

            for (var index = 0; index < solution.Count; index++)
            {
                var pair = pairs[index];
                var ids = solution[index];
                if (random.Next(2) == 0)
                {
                    slots[ids.firstId].Kind = pair.firstKind;
                    slots[ids.secondId].Kind = pair.secondKind;
                }
                else
                {
                    slots[ids.firstId].Kind = pair.secondKind;
                    slots[ids.secondId].Kind = pair.firstKind;
                }
            }

            return new MahjongDeal(seed, slots, solution);
        }

        private static List<TileSlot> BuildJadePalaceSlots()
        {
            var slots = new List<TileSlot>(TileCount);
            var id = 0;
            int[] baseCounts = { 8, 10, 12, 12, 12, 12, 10, 8 };
            for (var row = 0; row < baseCounts.Length; row++)
            {
                var count = baseCounts[row];
                var y2 = -7 + row * 2;
                AddRow(slots, ref id, 0, y2, count, -(count - 1));
            }

            for (var row = 0; row < 6; row++)
                AddRow(slots, ref id, 1, -6 + row * 2, 6, -6);

            for (var row = 0; row < 4; row++)
                AddRow(slots, ref id, 2, -3 + row * 2, 4, -3);

            for (var row = 0; row < 2; row++)
                AddRow(slots, ref id, 3, -2 + row * 2, 2, -2);

            AddRow(slots, ref id, 4, -1, 4, -3);

            if (slots.Count != TileCount)
                throw new InvalidOperationException($"Expected {TileCount} slots, built {slots.Count}.");

            return slots;
        }

        private static void AddRow(List<TileSlot> slots, ref int id, int layer,
            int y2, int count, int xStart)
        {
            for (var column = 0; column < count; column++)
                slots.Add(new TileSlot(id++, 0, xStart + column * 2, y2, layer));
        }

        private static List<(int firstId, int secondId)> BuildRemovalSchedule(
            IReadOnlyList<TileSlot> slots)
        {
            var result = new List<(int, int)>(TileCount / 2);
            for (var layer = 4; layer >= 0; layer--)
            {
                var rows = slots
                    .Where(slot => slot.Layer == layer)
                    .GroupBy(slot => slot.Y2)
                    .OrderBy(group => group.Key);

                foreach (var row in rows)
                {
                    var ordered = row.OrderBy(slot => slot.X2).ToList();
                    var left = 0;
                    var right = ordered.Count - 1;
                    while (left < right)
                    {
                        result.Add((ordered[left].Id, ordered[right].Id));
                        left++;
                        right--;
                    }
                }
            }

            if (result.Count != TileCount / 2)
                throw new InvalidOperationException("The layout does not contain complete pairs.");

            return result;
        }

        private static List<(int firstKind, int secondKind)> BuildTraditionalPairs()
        {
            var pairs = new List<(int, int)>(TileCount / 2);
            for (var kind = 0; kind < 34; kind++)
            {
                pairs.Add((kind, kind));
                pairs.Add((kind, kind));
            }

            pairs.Add((34, 35));
            pairs.Add((36, 37));
            pairs.Add((38, 39));
            pairs.Add((40, 41));
            return pairs;
        }
    }

    public sealed class MahjongBoardModel
    {
        private readonly List<TileSlot> _slots;
        private readonly Dictionary<int, TileSlot> _byId;

        public IReadOnlyList<TileSlot> Slots => _slots;
        public int Remaining => _slots.Count(slot => slot.Active);
        public bool IsCleared => Remaining == 0;

        public MahjongBoardModel(MahjongDeal deal)
        {
            _slots = deal.Slots.Select(slot => slot.Clone()).ToList();
            _byId = _slots.ToDictionary(slot => slot.Id);
        }

        public TileSlot Get(int id)
        {
            return _byId.TryGetValue(id, out var tile) ? tile : null;
        }

        public bool IsFree(int id)
        {
            if (!_byId.TryGetValue(id, out var tile) || !tile.Active)
                return false;

            var covered = _slots.Any(other =>
                other.Active &&
                other.Layer > tile.Layer &&
                Math.Abs(other.X2 - tile.X2) <= 1 &&
                Math.Abs(other.Y2 - tile.Y2) <= 1);
            if (covered)
                return false;

            var blockedLeft = _slots.Any(other =>
                other.Active &&
                other.Layer == tile.Layer &&
                other.X2 == tile.X2 - 2 &&
                Math.Abs(other.Y2 - tile.Y2) < 2);
            var blockedRight = _slots.Any(other =>
                other.Active &&
                other.Layer == tile.Layer &&
                other.X2 == tile.X2 + 2 &&
                Math.Abs(other.Y2 - tile.Y2) < 2);
            return !blockedLeft || !blockedRight;
        }

        public RemoveResult TryRemove(int firstId, int secondId)
        {
            if (firstId == secondId)
                return RemoveResult.SameTile;
            if (!_byId.TryGetValue(firstId, out var first) ||
                !_byId.TryGetValue(secondId, out var second) ||
                !first.Active || !second.Active)
                return RemoveResult.Missing;
            if (!IsFree(firstId) || !IsFree(secondId))
                return RemoveResult.Blocked;
            if (!KindsMatch(first.Kind, second.Kind))
                return RemoveResult.NotMatching;

            first.Active = false;
            second.Active = false;
            return RemoveResult.Removed;
        }

        public bool TryGetAvailablePair(out int firstId, out int secondId)
        {
            var free = _slots.Where(slot => slot.Active && IsFree(slot.Id)).ToList();
            for (var first = 0; first < free.Count; first++)
            {
                for (var second = first + 1; second < free.Count; second++)
                {
                    if (!KindsMatch(free[first].Kind, free[second].Kind))
                        continue;
                    firstId = free[first].Id;
                    secondId = free[second].Id;
                    return true;
                }
            }

            firstId = -1;
            secondId = -1;
            return false;
        }

        public bool HasAvailablePair()
        {
            return TryGetAvailablePair(out _, out _);
        }

        public void RedealActive(int seed)
        {
            var active = _slots.Where(slot => slot.Active).ToList();
            var kinds = active.Select(slot => slot.Kind).ToList();
            var random = new JadeRandom(seed);
            random.Shuffle(kinds);
            for (var index = 0; index < active.Count; index++)
                active[index].Kind = kinds[index];

            if (active.Count < 2 || HasAvailablePair())
                return;

            var free = active.Where(slot => IsFree(slot.Id)).Take(2).ToList();
            if (free.Count < 2)
                return;

            TileSlot pairA = null;
            TileSlot pairB = null;
            for (var first = 0; first < active.Count && pairA == null; first++)
            {
                for (var second = first + 1; second < active.Count; second++)
                {
                    if (!KindsMatch(active[first].Kind, active[second].Kind))
                        continue;
                    pairA = active[first];
                    pairB = active[second];
                    break;
                }
            }

            if (pairA == null)
                return;

            SwapKinds(pairA, free[0]);
            if (ReferenceEquals(pairB, free[0]))
                pairB = pairA;
            SwapKinds(pairB, free[1]);
        }

        private static void SwapKinds(TileSlot first, TileSlot second)
        {
            var kind = first.Kind;
            first.Kind = second.Kind;
            second.Kind = kind;
        }

        public static bool KindsMatch(int first, int second)
        {
            if (first < 0 || second < 0)
                return false;
            if (first < 34 || second < 34)
                return first == second;
            if (first <= 37 && second <= 37)
                return true;
            return first >= 38 && first <= 41 && second >= 38 && second <= 41;
        }
    }

    public sealed class JadeRandom
    {
        private uint _state;

        public JadeRandom(int seed)
        {
            _state = unchecked((uint)seed);
            if (_state == 0)
                _state = 0x9E3779B9u;
        }

        public uint NextUInt()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }

        public int Next(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            return (int)(NextUInt() % (uint)exclusiveMax);
        }

        public void Shuffle<T>(IList<T> values)
        {
            for (var index = values.Count - 1; index > 0; index--)
            {
                var other = Next(index + 1);
                (values[index], values[other]) = (values[other], values[index]);
            }
        }
    }
}
