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

        private static bool searchCancelled;

        private static long nps;
        private static long elapsedMs;

        private static string pvLine;

        private static Move[][] pvArray;
        private static int[] pvLength;

        private static int nodes;

        private static Stopwatch stopwatch;

        public static event Action<Move> OnSearchComplete;

        static Searcher()
        {
            TranspositionTable.Initialize(16);
        }

        public static void EndSearch()
        {
            searchCancelled = true;
        }

        public static void StartNewSearch()
        {
            TranspositionTable.NewSearch();
            MoveOrderer.ClearKillers();
            MoveOrderer.ClearHistory();

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
            nodes = 0;

            for (Depth = 1; Depth <= MaxDepth; Depth++)
            {
                bestMoveThisIteration = Move.NullMove;                                                                                                                                  

                Array.Clear(pvLength, 0, pvLength.Length);

                Search(Depth, NegativeInfinity, PositiveInfinity, 0);

                if (bestMoveThisIteration != Move.NullMove)
                {
                    bestMove = bestMoveThisIteration;
                    bestEval = bestEvalThisIteration;
                }

                if (searchCancelled && Depth > 1)
                    break;

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

        private static int Search(int depth, int alpha, int beta, int plyFromRoot)
        {
            nodes++;

            if (searchCancelled && Depth > 1)
                return 0;

            pvLength[plyFromRoot] = 0;

            if (board.IsDraw())
                return 0;

            int alphaOriginal = alpha;
            int betaOriginal = beta;

            ulong key = board.CurrentHash;

            Move ttMove = Move.NullMove;

            if (TranspositionTable.Probe(key, out TTEntry ttEntry))
            {
                ttMove = ttEntry.BestMove;

                if (plyFromRoot > 0 && ttEntry.Depth >= depth)
                {
                    int ttScore = ScoreFromTT(ttEntry.Score, plyFromRoot);

                    switch (ttEntry.Flag)
                    {
                        case TTFlag.Exact:
                            return ttScore;

                        case TTFlag.LowerBound:
                            if (ttScore >= beta)
                                return ttScore;
                            break;

                        case TTFlag.UpperBound:
                            if (ttScore <= alpha)
                                return ttScore;
                            break;
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

            MoveOrderer.OrderMoves(moves[..moveCount], board, plyFromRoot == 0 ? bestMoveLastIteration : Move.NullMove, ttMove, plyFromRoot);

            int bestScore = NegativeInfinity;
            Move bestMoveThisNode = Move.NullMove;

            for (int i = 0; i < moveCount; i++)
            {
                Move move = moves[i];

                board.MakeMove(move);

                int score;

                // According to the move ordering, the first move is the most promising.
                // do a full window search for it. For subsequent moves, we do a null window search first (scout search),
                // and only do a full window search if the null window search indicates that the move is better than alpha.
                if (i == 0)
                {
                    score = -Search(depth - 1, -beta, -alpha, plyFromRoot + 1);
                }
                else
                {
                    score = -Search(depth - 1, -alpha - 1, -alpha, plyFromRoot + 1);

                    if (score > alpha && score < beta)
                        score = -Search(depth - 1, -beta, -alpha, plyFromRoot + 1);
                }

                board.UndoMove(move);

                if (searchCancelled && Depth > 1)
                    return 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMoveThisNode = move;

                    if (score > alpha)
                    {
                        pvArray[plyFromRoot][0] = move;

                        for (int j = 0; j < pvLength[plyFromRoot + 1]; j++)
                        {
                            pvArray[plyFromRoot][j + 1] =
                                pvArray[plyFromRoot + 1][j];
                        }

                        pvLength[plyFromRoot] = pvLength[plyFromRoot + 1] + 1;
                    }

                    if (plyFromRoot == 0)
                    {
                        bestMoveThisIteration = move;
                        bestEvalThisIteration = score;
                    }
                }
                alpha = Math.Max(alpha, bestScore);
                if (alpha >= beta)
                {
                    if (board.Squares[move.TargetSquare] == Piece.None)
                    {
                        MoveOrderer.AddKiller(move, plyFromRoot);
                        MoveOrderer.UpdateHistory(board.IsWhiteToMove, move, depth, true);

                        // Penalize quiet moves that were tried first and failed to cut off
                        for (int j = 0; j < i; j++)
                        {
                            Move earlier = moves[j];
                            if (board.Squares[earlier.TargetSquare] == Piece.None)
                                MoveOrderer.UpdateHistory(board.IsWhiteToMove, earlier, depth, false);
                        }
                    }
                    break; // Beta cutoff
                }
            }

            TTFlag flag;

            if (bestScore <= alphaOriginal)
                flag = TTFlag.UpperBound;
            else if (bestScore >= betaOriginal)
                flag = TTFlag.LowerBound;
            else
                flag = TTFlag.Exact;

            TranspositionTable.Store(key, depth, ScoreToTT(bestScore, plyFromRoot), flag, bestMoveThisNode);

            return bestScore;
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
            MoveOrderer.OrderMoves(moves[..moveCount], board, Move.NullMove, Move.NullMove, -1);

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
                $"time {elapsedMs} " +
                $"pv {pvLine.Trim()}"
            );
        }

        private const int MateThreshold = 900000;

        private static int ScoreToTT(int score, int ply)
        {
            if (score > MateThreshold)
                return score + ply;

            if (score < -MateThreshold)
                return score - ply;

            return score;
        }

        private static int ScoreFromTT(int score, int ply)
        {
            if (score > MateThreshold)
                return score - ply;

            if (score < -MateThreshold)
                return score + ply;

            return score;
        }
    }
}