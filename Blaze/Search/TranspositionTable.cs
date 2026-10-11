
using Blaze.MoveGen;

namespace Blaze.Search
{
    public enum TTFlag : byte
    {
        Exact,
        LowerBound,
        UpperBound
    }

    public struct TTEntry
    {
        public ulong Key;
        public Move BestMove;
        public int Score;
        public int Depth;
        public TTFlag Flag;
        public bool IsValid;
        public byte Generation;
    }

    public static class TranspositionTable
    {
        private static TTEntry[] table = Array.Empty<TTEntry>();
        public static byte Generation;

        private static int indexMask;

        public static int SizeMB { get; private set; }

        public static void NewSearch()
        {
            Generation++;
        }

        public static void Initialize(int sizeMB = 16)
        {
            if (sizeMB < 1)
                sizeMB = 1;

            int entrySize = System.Runtime.InteropServices.Marshal.SizeOf<TTEntry>();

            long targetEntries = (long)sizeMB * 1024 * 1024 / entrySize;

            int count = 1;

            while ((long)count * 2 <= targetEntries &&
                   count < (1 << 29))
            {
                count *= 2;
            }

            table = new TTEntry[count];
            indexMask = count - 1;
            SizeMB = sizeMB;
        }

        public static void Clear()
        {
            Array.Clear(table);
        }

        public static bool Probe(ulong key, out TTEntry entry)
        {
            if (table.Length == 0)
            {
                entry = default;
                return false;
            }

            entry = table[(int)(key & (uint)indexMask)];

            return entry.IsValid && entry.Key == key;
        }

        public static void Store(ulong key, int depth, int score, TTFlag flag, Move bestMove)
        {
            if (table.Length == 0)
                Initialize();

            int index = (int)(key & (uint)indexMask);
            TTEntry oldEntry = table[index];

            bool samePosition = oldEntry.IsValid && oldEntry.Key == key;
            bool oldGeneration = oldEntry.Generation != Generation;

            if (oldEntry.IsValid && !samePosition && !oldGeneration && oldEntry.Depth > depth)
            {
                return;
            }

            // Preserve the old move if the new entry has no move.
            if (bestMove == Move.NullMove && oldEntry.IsValid && oldEntry.Key == key)
            {
                bestMove = oldEntry.BestMove;
            }

            table[index] = new TTEntry
            {
                Key = key,
                Depth = depth,
                Score = score,
                Flag = flag,
                BestMove = bestMove,
                IsValid = true,
                Generation = Generation
            };
        }
    }
}
