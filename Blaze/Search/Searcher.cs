using Blaze.Evaluation;
using Blaze.Helpers;
using Blaze.MoveGen;
using System.Diagnostics;
using static System.Formats.Asn1.AsnWriter;

namespace Blaze.Search
{
    public static class Searcher
    {
        private static Board board;

        private static Move bestMove;
        private static Move bestMoveThisIteration = Move.NullMove;

        private static int bestEval;
        private static int bestEvalThisIteration;

        private static int Depth;
        private const int MaxDepth = 255;
        private const int NegativeInfinity = -1000000;
        private const int PositiveInfinity = 1000000;

        private static bool searchCancelled;

        private static long nps;
        private static long elapsedMs;

        private static string pvLine;

        private static Move[][] pvArray;
        private static int[] pvLength;
        
        private static int nodes;

        private static Stopwatch stopwatch;

        public static event Action<Move> OnSearchComplete;

        public static void EndSearch()
        {
            searchCancelled = true;
        }

        public static void StartNewSearch()
        {
            board = MatchManager.board;

            searchCancelled = false;

            pvArray = new Move[MaxDepth][];
            pvLength = new int[MaxDepth];

            for (int i = 0; i < MaxDepth; i++)
                pvArray[i] = new Move[MaxDepth];

            bestMove = Move.NullMove;
            bestMoveThisIteration = Move.NullMove;

            bestEval = 0;
            bestEvalThisIteration = 0;

            stopwatch = Stopwatch.StartNew();

            StartIterativeDeepeningSearch();

            OnSearchComplete?.Invoke(bestMove);
        }

        private static void StartIterativeDeepeningSearch()
        {
            for (Depth = 1; Depth <= MaxDepth; Depth++)
            {
                nodes = 0;

                Array.Clear(pvLength, 0, pvLength.Length);

                Search(Depth, NegativeInfinity, PositiveInfinity, 0);

                pvLine = "";

                for (int i = 0; i < pvLength[0]; i++)
                {
                    pvLine += Notation.MoveToNotation(pvArray[0][i]) + " ";
                }

                if (searchCancelled && Depth > 1)
                    break;

                elapsedMs = stopwatch.ElapsedMilliseconds;

                nps = elapsedMs > 0 ? nodes * 1000L / elapsedMs : 0;

                bestMove = bestMoveThisIteration;
                bestEval = bestEvalThisIteration;

                PrintInfoLine();
            }
        }

        private static int Search(int depth, int alpha, int beta, int plyFromRoot)
        {
            if (searchCancelled && Depth > 1)
                return 0;

            if (board.IsDraw())
                return 0;

            if (depth == 0)
                return Quiesce(alpha, beta);

            Span<Move> moves = stackalloc Move[218];

            int moveCount = board.GetLegalMoves(moves);

            if (moveCount == 0)
            {
                if (board.IsInCheck())
                    return NegativeInfinity + plyFromRoot;

                return 0;
            }

            MoveOrderer.OrderMoves(moves[..moveCount], board);

            int bestScore = NegativeInfinity;

            for (int i = 0; i < moveCount; i++)
            {
                nodes++;

                Move move = moves[i];

                board.MakeMove(move);
                int score = -Search(depth - 1, -beta, -alpha, plyFromRoot + 1);
                board.UndoMove(move);

                if (searchCancelled && Depth > 1)
                    break;

                if (score > bestScore)
                {
                    bestScore = score;

                    pvArray[plyFromRoot][0] = move;

                    for (int j = 0; j < pvLength[plyFromRoot + 1]; j++)
                    {
                        pvArray[plyFromRoot][j + 1] =
                            pvArray[plyFromRoot + 1][j];
                    }

                    pvLength[plyFromRoot] =
                        pvLength[plyFromRoot + 1] + 1;

                    if (plyFromRoot == 0)
                    {
                        bestMoveThisIteration = move;
                        bestEvalThisIteration = score;
                    }
                }
                alpha = Math.Max(alpha, bestScore);
                if (alpha >= beta)
                {
                    break;
                }
            }

            return bestScore;
        }

        private static int Quiesce(int alpha, int beta)
        {
            int static_eval = Evaluator.Evaluate(board);

            // Stand Pat
            int best_value = static_eval;
            if (best_value >= beta)
                return best_value;
            if (best_value > alpha)
                alpha = best_value;

            Span<Move> moves = stackalloc Move[218];

            int moveCount = board.GetLegalMoves(moves, true);

            MoveOrderer.OrderMoves(moves[..moveCount], board);

            for (int i = 0; i < moveCount; i++)
            {
                nodes++;

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
                $"time {elapsedMs} " +
                $"pv {pvLine.Trim()}"
            );
        }
    }
}