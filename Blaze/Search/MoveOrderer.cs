using Blaze.MoveGen;

namespace Blaze.Search
{
    public class MoveOrderer
    {
        public const int MaxPly = 256;
        private const int KillerSlots = 2;

        // killers[ply, slot]: quiet moves that caused a beta cutoff at this ply
        private static readonly Move[,] killers = new Move[MaxPly, KillerSlots];

        public static void ClearKillers()
        {
            Array.Clear(killers, 0, killers.Length);
        }

        public static void AddKiller(Move move, int ply)
        {
            if (ply >= MaxPly)
                return;

            if (move == killers[ply, 0])
                return;

            // Shift the old primary killer down and store the new one
            killers[ply, 1] = killers[ply, 0];
            killers[ply, 0] = move;
        }

        public static void OrderMoves(Span<Move> moves, Board board, Move PvMove, Move ttMove, int ply)
        {
            moves.Sort((a, b) =>
            {
                int scoreA = Score(a, board, PvMove, ttMove, ply);
                int scoreB = Score(b, board, PvMove, ttMove, ply);
                return scoreB.CompareTo(scoreA); // Descending order
            });
        }

        private static int Score(Move move, Board board, Move PvMove, Move ttMove, int ply)
        {
            if (move == PvMove)
                return 1000000;

            if (move == ttMove)
                return 100000;

            // Captures
            if (board.Squares[move.TargetSquare] != Piece.None)
            {
                int capturedPieceType = Piece.PieceType(board.Squares[move.TargetSquare]);
                int movingPieceType = Piece.PieceType(board.Squares[move.StartingSquare]);
                return 1000 + 100 * capturedPieceType - movingPieceType;
            }

            if (ply >= 0 && ply < MaxPly)
            {
                if (move == killers[ply, 0])
                    return 900;
                if (move == killers[ply, 1])
                    return 800;
            }

            return 0;
        }
    }
}