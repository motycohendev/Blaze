using Blaze.MoveGen;

namespace Blaze.Search
{
    public static class MoveOrderer
    {
        private static readonly int[] PieceValues =
        {
            0,  // None
            100, // Pawn
            320, // Knight
            330, // Bishop
            500, // Rook
            900, // Queen
            0 // King
        };

        public static void OrderMoves(Span<Move> moves, Board board)
        {
            moves.Sort((a, b) => Score(b, board).CompareTo(Score(a, board)));
        }

        private static int Score(Move move, Board board)
        {
            //Capture
            if (board.Squares[move.TargetSquare] != Piece.None)
            {
                int capturedPieceValue = PieceValues[Piece.PieceType(board.Squares[move.TargetSquare])];
                int movingPieceValue = PieceValues[Piece.PieceType(board.Squares[move.StartingSquare])];
                return (capturedPieceValue * 10) - movingPieceValue;
            }

            return 0;
        }
    }
}