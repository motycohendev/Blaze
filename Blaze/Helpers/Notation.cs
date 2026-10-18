using Blaze.MoveGen;
using System.Diagnostics;

namespace Blaze.Helpers
{
    /// <summary>
    /// Helper methods for converting between internal board representations
    /// and standard chess notation.
    /// </summary>
    public static class Notation
    {
        /// <summary>
        /// Converts a square index (0-63) to algebraic notation
        /// (e.g. 0 -> "a1", 63 -> "h8").
        /// </summary>
        public static string IndexToSquare(int squareIndex)
        {
            int file = squareIndex & 7;
            int rank = squareIndex >> 3;

            return $"{(char)('a' + file)}{(char)('1' + rank)}";
        }

        /// <summary>
        /// Converts a move to UCI notation
        /// (e.g. "e2e4", "e7e8q").
        /// </summary>
        public static string MoveToNotation(Move move)
        {
            string notation =
                IndexToSquare(move.StartingSquare) +
                IndexToSquare(move.TargetSquare);

            return move.Flag switch
            {
                MoveFlag.QueenPromotion => notation + "q",
                MoveFlag.RookPromotion => notation + "r",
                MoveFlag.BishopPromotion => notation + "b",
                MoveFlag.KnightPromotion => notation + "n",
                _ => notation
            };
        }

        /// <summary>
        /// Converts a square in algebraic notation
        /// (e.g. "e4") to a square index (0-63).
        /// </summary>
        public static int SquareToIndex(string square)
        {
            int file = square[0] - 'a';
            int rank = square[1] - '1';

            return (rank << 3) + file;
        }

        /// <summary>
        /// Converts a move in UCI notation
        /// (e.g. "e2e4", "e7e8q") to a <see cref="Move"/>.
        /// </summary>
        public static Move NotationToMove(string notation)
        {
            int startingSquare = SquareToIndex(notation[..2]);
            int targetSquare = SquareToIndex(notation[2..4]);

            int moveFlag = notation.Length > 4
                ? notation[4] switch
                {
                    'q' => MoveFlag.QueenPromotion,
                    'r' => MoveFlag.RookPromotion,
                    'b' => MoveFlag.BishopPromotion,
                    'n' => MoveFlag.KnightPromotion,
                    _ => 0
                }
                : 0;

            return new Move(startingSquare, targetSquare, moveFlag);
        }

        /// <summary>
        /// Runs a divide-style perft test and prints the node count
        /// for each legal root move, along with the total nodes,
        /// elapsed time, and nodes per second.
        /// </summary>
        public static void PrintPerftTest(Board board, int depth)
        {
            ulong totalNodes = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            Span<Move> moves = stackalloc Move[218];
            int moveCount = board.GetLegalMoves(moves);

            for (int i = 0; i < moveCount; i++)
            {
                Move move = moves[i];

                board.MakeMove(move);

                ulong nodes = depth == 1
                    ? 1
                    : (ulong)MoveGenerator.Perft(board, depth - 1);

                Console.WriteLine($"{MoveToNotation(move)}: {nodes}");

                totalNodes += nodes;
                board.UndoMove(move);
            }

            stopwatch.Stop();

            long milliseconds = Math.Max(1, stopwatch.ElapsedMilliseconds);

            Console.WriteLine($"Looked at a total of {totalNodes} nodes in {milliseconds} ms.");
            Console.WriteLine($"NPS: {(long)(totalNodes / (milliseconds / 1000.0))}");
        }
    }
}