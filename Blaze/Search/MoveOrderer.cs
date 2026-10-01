using Blaze.MoveGen;

namespace Blaze.Search
{
    public static class MoveOrderer
    {
        private static readonly int[] PiecesValues =
        {
            100, // Pawn
            320, // Knight
            330, // Bishop
            500, // Rook
            900, // Queen
            0 // King
        };

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
            int score = 0;

            //Capture
            if (board.Squares[move.TargetSquare] != Piece.None)
            {
                int capturedPieceType = Piece.PieceType(board.Squares[move.TargetSquare]);
                int movingPieceType = Piece.PieceType(board.Squares[move.StartingSquare]);

                score = 10_000 + 100 * capturedPieceType - movingPieceType;
            }

            return score;
        }
    }
}