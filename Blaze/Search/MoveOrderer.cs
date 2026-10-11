using Blaze.MoveGen;

namespace Blaze.Search
{
    public class MoveOrderer
    {
        public static void OrderMoves(Span<Move> moves, Board board)
        {
            // Order moves based on a simple heuristic: captures first, then promotions, then others
            moves.Sort((a, b) =>
            {
                int scoreA = Score(a, board);
                int scoreB = Score(b, board);
                return scoreB.CompareTo(scoreA); // Descending order
            });
        }

        private static int Score(Move move, Board board)
        {
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