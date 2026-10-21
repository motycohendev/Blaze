using Blaze.MoveGen;

namespace Blaze.Search
{
    public static class MoveOrderer
    {
        public static void OrderMoves(Span<Move> moves, Board board, Move pvMove)
        {
            moves.Sort((a, b) =>
            {
                int scoreA = Score(a, board, pvMove);
                int scoreB = Score(b, board, pvMove);
                return scoreB.CompareTo(scoreA);
            });
        }

        private static int Score(Move move, Board board, Move pvMove)
        {
            if (move.Equals(pvMove))
            {
                return 1_000_000_000;
            }
            //Captures
            if (board.Squares[move.TargetSquare] != Piece.None)
            {
                int capturedPieceValue = Piece.PieceType(board.Squares[move.TargetSquare]);
                int movingPieceValue = Piece.PieceType(board.Squares[move.StartingSquare]);
                return 1_000_000 + 100 * capturedPieceValue - movingPieceValue;
            }
            return 0;
        }
    }
}