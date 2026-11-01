using Blaze.MoveGen;
using System.Runtime.CompilerServices;

namespace Blaze.Search
{
    public enum TTFlag : byte
    {
        None = 0,
        Exact,
        LowerBound, // score >= true value was proven (beta cutoff)
        UpperBound  // score <= true value (failed low)
    }

    public struct TTEntry
    {
        public ulong Key;
        public int Score;
        public Move Move;
        public byte Depth;
        public TTFlag Flag;
        public byte Generation;
    }

    public sealed class TranspositionTable
    {
        private TTEntry[] entries;
        private ulong mask;
        private byte generation;

        public TranspositionTable(int sizeMb)
        {
            Resize(sizeMb);
        }

        public void Resize(int sizeMb)
        {
            long bytes = Math.Max(1, sizeMb) * 1024L * 1024L;
            long count = bytes / Unsafe.SizeOf<TTEntry>();

            // Round down to a power of two so indexing is a cheap mask
            long size = 1;
            while (size * 2 <= count)
                size *= 2;

            entries = new TTEntry[size];
            mask = (ulong)(size - 1);
            generation = 0;
        }

        public void Clear()
        {
            Array.Clear(entries, 0, entries.Length);
            generation = 0;
        }

        // Call once per search so stale entries from earlier searches are easy to replace
        public void NewSearch()
        {
            generation++;
        }

        public bool Probe(ulong key, out TTEntry entry)
        {
            entry = entries[key & mask];
            return entry.Flag != TTFlag.None && entry.Key == key;
        }

        public void Store(ulong key, int depth, int score, TTFlag flag, Move move)
        {
            ref TTEntry e = ref entries[key & mask];

            bool samePosition = e.Flag != TTFlag.None && e.Key == key;

            // Don't let a much shallower, non-exact result clobber a deeper entry
            // for the same position from the current search.
            if (samePosition && e.Generation == generation &&
                flag != TTFlag.Exact && depth < e.Depth - 3)
                return;

            // Keep the old best move if this search didn't produce one
            if (samePosition && move == Move.NullMove)
                move = e.Move;

            e.Key = key;
            e.Score = score;
            e.Move = move;
            e.Depth = (byte)depth;
            e.Flag = flag;
            e.Generation = generation;
        }

        public int HashFull()
        {
            int sample = Math.Min(1000, entries.Length);
            int used = 0;

            for (int i = 0; i < sample; i++)
            {
                if (entries[i].Flag != TTFlag.None && entries[i].Generation == generation)
                    used++;
            }

            return used * 1000 / sample;
        }
    }
}