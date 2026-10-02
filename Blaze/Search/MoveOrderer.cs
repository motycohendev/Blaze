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

        private const int MaxPly = 256;

        private const int TTMoveScore = 1_000_000;
        private const int CaptureBase = 10_000;
        private const int Killer1Score = 9_000;
        private const int Killer2Score = 8_000;

        // Two killer slots per ply
        private static readonly Move[,] killers = new Move[MaxPly, 2];

        public static void ClearKillers()
        {
            for (int i = 0; i < MaxPly; i++)
            {
                killers[i, 0] = Move.NullMove;
                killers[i, 1] = Move.NullMove;
            }
        }

        public static void StoreKiller(Move move, int ply)
        {
            if (ply < 0 || ply >= MaxPly)
                return;

            // Don't duplicate; shift slot 0 into slot 1
            if (!move.Equals(killers[ply, 0]))
            {
                killers[ply, 1] = killers[ply, 0];
                killers[ply, 0] = move;
            }
        }


        public static void OrderMoves(Span<Move> moves, Board board, Move ttMove, int ply)
        {
            moves.Sort((a, b) =>
            {
                int scoreA = Score(a, board, ttMove, ply);
                int scoreB = Score(b, board, ttMove, ply);
                return scoreB.CompareTo(scoreA);
            });
        }

        private static int Score(Move move, Board board, Move ttMove, int ply)
        {
            if (move.Equals(ttMove))
                return TTMoveScore;

            // Capture 
            int captured = board.Squares[move.TargetSquare];
            if (captured != Piece.None)
            {
                int capturedPieceType = Piece.PieceType(captured);
                int movingPieceType = Piece.PieceType(board.Squares[move.StartingSquare]);

                return CaptureBase + 100 * capturedPieceType - movingPieceType;
            }

            // Killer moves
            if (ply >= 0 && ply < MaxPly)
            {
                if (move.Equals(killers[ply, 0])) return Killer1Score;
                if (move.Equals(killers[ply, 1])) return Killer2Score;
            }

            return 0;
        }
    }
}