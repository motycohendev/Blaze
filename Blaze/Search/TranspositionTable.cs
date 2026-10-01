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
        public int Depth;
        public int Score;
        public TTFlag Flag;
        public Move BestMove;
    }

    public static class TranspositionTable
    {
        private static TTEntry[] table;
        private static int mask;

        public static void Initialize(int megabytes)
        {
            long bytes = (long)megabytes * 1024 * 1024;

            int entrySize = 32;
            int count = 1;

            while ((long)(count << 1) * entrySize <= bytes)
                count <<= 1;

            table = new TTEntry[count];
            mask = count - 1;
        }

        public static void Clear()
        {
            Array.Clear(table);
        }

        public static bool Probe(ulong key, int depth, int alpha, int beta, int ply, out int score, out Move bestMove)
        {
            TTEntry entry = table[(int)(key & (ulong)mask)];

            bestMove = Move.NullMove;
            score = 0;

            if (entry.Key != key)
                return false;

            bestMove = entry.BestMove;

            if (entry.Depth < depth)
                return false;

            int ttScore = ScoreFromTT(entry.Score, ply);

            switch (entry.Flag)
            {
                case TTFlag.Exact:
                    score = ttScore;
                    return true;

                case TTFlag.LowerBound:
                    if (ttScore >= beta)
                    {
                        score = ttScore;
                        return true;
                    }
                    break;

                case TTFlag.UpperBound:
                    if (ttScore <= alpha)
                    {
                        score = ttScore;
                        return true;
                    }
                    break;
            }

            return false;
        }

        public static Move GetBestMove(ulong key)
        {
            TTEntry entry = table[(int)(key & (ulong)mask)];

            if (entry.Key == key)
                return entry.BestMove;

            return Move.NullMove;
        }

        public static void Store(ulong key, int depth, int score, int ply, TTFlag flag, Move bestMove)
        {
            int index = (int)(key & (ulong)mask);

            TTEntry old = table[index];

            // Replace if empty, same position, or deeper/equal depth.
            if (old.Key == 0 ||
                old.Key == key ||
                depth >= old.Depth)
            {
                table[index] = new TTEntry
                {
                    Key = key,
                    Depth = depth,
                    Score = ScoreToTT(score, ply),
                    Flag = flag,
                    BestMove = bestMove
                };
            }
        }

        private const int MateThreshold = 900000;

        private static int ScoreToTT(int score, int ply)
        {
            if (score >= MateThreshold) return score + ply;
            if (score <= -MateThreshold) return score - ply;
            return score;
        }

        private static int ScoreFromTT(int score, int ply)
        {
            if (score >= MateThreshold) return score - ply;
            if (score <= -MateThreshold) return score + ply;
            return score;
        }
    }
}