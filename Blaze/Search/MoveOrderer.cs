using Blaze.MoveGen;

namespace Blaze.Search
{
    public class MoveOrderer
    {
        public static void OrderMoves(Span<Move> moves, Board board, Move PvMove, Move ttMove)
        {
            moves.Sort((a, b) =>
            {
                int scoreA = Score(a, board, PvMove, ttMove);
                int scoreB = Score(b, board, PvMove, ttMove);
                return scoreB.CompareTo(scoreA); // Descending order
            });
        }

        private static int Score(Move move, Board board, Move PvMove, Move ttMove)
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
            return 0;
        }
    }
}