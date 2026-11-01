using Blaze.Evaluation;
using Blaze.Helpers;
using Blaze.MoveGen;
using System.Diagnostics;

namespace Blaze.Search
{
    public static class Searcher
    {
        private static Board board;

        private static Move bestMove;
        private static Move bestMoveThisIteration = Move.NullMove;
        private static Move bestMoveLastIteration = Move.NullMove;

        private static int bestEval;
        private static int bestEvalThisIteration;

        private static int Depth;
        private const int MaxDepth = 255;
        private const int NegativeInfinity = -1000000;
        private const int PositiveInfinity = 1000000;
        private const int MateThreshold = 900000;

        private static bool searchCancelled;

        private static long nps;
        private static long elapsedMs;

        private static string pvLine;

        private static Move[][] pvArray;
        private static int[] pvLength;

        private static Move[] prevPv = new Move[MaxDepth];
        private static int prevPvLength;

        private static int nodes;

        private static Stopwatch stopwatch;

        private static readonly TranspositionTable tt = new TranspositionTable(64);

        public static event Action<Move> OnSearchComplete;

        public static void EndSearch()
        {
            searchCancelled = true;
        }

        public static void ClearTT()
        {
            tt.Clear();
        }

        // Call on "setoption name Hash value N"
        public static void ResizeTT(int sizeMb)
        {
            tt.Resize(sizeMb);
        }

        public static void StartNewSearch()
        {

            board = MatchManager.board;

            searchCancelled = false;

            tt.NewSearch();

            pvArray = new Move[MaxDepth][];
            pvLength = new int[MaxDepth];

            for (int i = 0; i < MaxDepth; i++)
                pvArray[i] = new Move[MaxDepth];

            bestMove = Move.NullMove;
            bestMoveLastIteration = Move.NullMove;

            bestEval = 0;
            bestEvalThisIteration = 0;

            stopwatch = Stopwatch.StartNew();

            StartIterativeDeepeningSearch();

            OnSearchComplete?.Invoke(bestMove);
        }

        private static void StartIterativeDeepeningSearch()
        {
            nodes = 0;

            for (Depth = 1; Depth <= MaxDepth; Depth++)
            {
                bestMoveThisIteration = Move.NullMove;

                Array.Clear(pvLength, 0, pvLength.Length);

                Search(Depth, NegativeInfinity, PositiveInfinity, 0, true);

                if (bestMoveThisIteration != Move.NullMove)
                {
                    bestMove = bestMoveThisIteration;
                    bestEval = bestEvalThisIteration;
                }

                if (searchCancelled && Depth > 1)
                    break;

                prevPvLength = pvLength[0];
                Array.Copy(pvArray[0], prevPv, prevPvLength);

                pvLine = "";

                for (int i = 0; i < pvLength[0]; i++)
                {
                    pvLine += Notation.MoveToNotation(pvArray[0][i]) + " ";
                }

                elapsedMs = stopwatch.ElapsedMilliseconds;

                nps = elapsedMs > 0 ? nodes * 1000L / elapsedMs : 0;

                PrintInfoLine();

                bestMoveLastIteration = bestMoveThisIteration;
            }
        }

        private static int Search(int depth, int alpha, int beta, int plyFromRoot, bool onPv)
        {
            nodes++;

            if (searchCancelled && Depth > 1)
                return 0;

            pvLength[plyFromRoot] = 0;

            if (board.IsDraw())
                return 0;

            ulong key = board.CurrentHash;
            Move ttMove = Move.NullMove;

            if (depth > 0 && tt.Probe(key, out TTEntry entry))
            {
                ttMove = entry.Move;

                // No cutoffs at the root or on the previous PV line, so we always
                // get a real best move and a full-length PV out of the search.
                if (plyFromRoot > 0 && !onPv && entry.Depth >= depth)
                {
                    int ttScore = ScoreFromTT(entry.Score, plyFromRoot);

                    switch (entry.Flag)
                    {
                        case TTFlag.Exact:
                            return ttScore;
                        case TTFlag.LowerBound when ttScore >= beta:
                            return ttScore;
                        case TTFlag.UpperBound when ttScore <= alpha:
                            return ttScore;
                    }
                }
            }

            Span<Move> moves = stackalloc Move[218];
            int moveCount = board.GetLegalMoves(moves);

            if (moveCount == 0)
            {
                if (board.IsInCheck())
                    return NegativeInfinity + plyFromRoot;

                return 0;
            }

            if (depth == 0)
                return Quiesce(alpha, beta);

            Move pvMove = onPv && plyFromRoot < prevPvLength ? prevPv[plyFromRoot] : Move.NullMove;

            Move firstMove = ttMove;
            if (pvMove != Move.NullMove)
                firstMove = pvMove;
            if (plyFromRoot == 0 && bestMoveLastIteration != Move.NullMove)
                firstMove = bestMoveLastIteration;

            MoveOrderer.OrderMoves(moves[..moveCount], board, firstMove);

            int alphaOrig = alpha;
            int bestScore = NegativeInfinity;
            Move bestMoveHere = Move.NullMove;

            for (int i = 0; i < moveCount; i++)
            {
                Move move = moves[i];

                bool childOnPv = pvMove != Move.NullMove && move == pvMove;

                board.MakeMove(move);
                int score = -Search(depth - 1, -beta, -alpha, plyFromRoot + 1, childOnPv);
                board.UndoMove(move);

                if (searchCancelled && Depth > 1)
                    return 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMoveHere = move;

                    pvArray[plyFromRoot][0] = move;

                    for (int j = 0; j < pvLength[plyFromRoot + 1]; j++)
                    {
                        pvArray[plyFromRoot][j + 1] =
                            pvArray[plyFromRoot + 1][j];
                    }

                    pvLength[plyFromRoot] = pvLength[plyFromRoot + 1] + 1;

                    if (plyFromRoot == 0)
                    {
                        bestMoveThisIteration = move;
                        bestEvalThisIteration = score;
                    }
                }
                alpha = Math.Max(score, alpha);
                if (alpha >= beta)
                    break;
            }

            TTFlag flag;
            Move storeMove = bestMoveHere;

            if (bestScore <= alphaOrig)
            {
                flag = TTFlag.UpperBound;     // failed low: no reliable best move
                storeMove = Move.NullMove;
            }
            else if (bestScore >= beta)
            {
                flag = TTFlag.LowerBound;     // failed high: bestMoveHere caused the cutoff
            }
            else
            {
                flag = TTFlag.Exact;
            }

            tt.Store(key, depth, ScoreToTT(bestScore, plyFromRoot), flag, storeMove);

            return bestScore;
        }

        private static int ScoreToTT(int score, int plyFromRoot)
        {
            if (score > MateThreshold)
                return score + plyFromRoot;
            if (score < -MateThreshold)
                return score - plyFromRoot;

            return score;
        }

        private static int ScoreFromTT(int score, int plyFromRoot)
        {
            if (score > MateThreshold)
                return score - plyFromRoot;
            if (score < -MateThreshold)
                return score + plyFromRoot;

            return score;
        }

        private static int Quiesce(int alpha, int beta)
        {
            nodes++;

            int static_eval = Evaluator.Evaluate(board);

            // Stand Pat
            int best_value = static_eval;
            if (best_value >= beta)
                return best_value;
            if (best_value > alpha)
                alpha = best_value;

            Span<Move> moves = stackalloc Move[218];
            int moveCount = board.GetLegalMoves(moves, true);

            MoveOrderer.OrderMoves(moves[..moveCount], board, Move.NullMove);

            for (int i = 0; i < moveCount; i++)
            {
                Move move = moves[i];

                board.MakeMove(move);
                int score = -Quiesce(-beta, -alpha);
                board.UndoMove(move);

                if (score >= beta)
                    return score;
                if (score > best_value)
                    best_value = score;
                if (score > alpha)
                    alpha = score;
            }

            return best_value;
        }

        private static bool IsMateScore(int score)
        {
            const int mateThreshold = 900000;

            return Math.Abs(score) > mateThreshold;
        }

        private static int MateInMoves(int score)
        {
            if (score > 0)
            {
                return (PositiveInfinity - score + 1) / 2;
            }

            return -(PositiveInfinity + score + 1) / 2;
        }

        private static void PrintInfoLine()
        {
            string scoreStr = IsMateScore(bestEval)
                    ? $"mate {MateInMoves(bestEval)}"
                    : $"cp {bestEval}";

            Console.WriteLine(
                $"info depth {Depth} " +
                $"score {scoreStr} " +
                $"nodes {nodes} " +
                $"nps {nps} " +
                $"hashfull {tt.HashFull()} " +
                $"time {elapsedMs} " +
                $"pv {pvLine.Trim()}"
            );
        }
    }
}