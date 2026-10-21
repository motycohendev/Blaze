using Blaze.MoveGen;

namespace Blaze.Search
{
    public static class MoveOrderer
    {
        public static void OrderMoves(Span<Move> moves, Board board)
        {
            moves.Sort((a, b) =>
            {
                int scoreA = Score(a, board);
                int scoreB = Score(b, board);
                return scoreB.CompareTo(scoreA);
            });
        }

        private static int Score(Move move, Board board)
        {
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