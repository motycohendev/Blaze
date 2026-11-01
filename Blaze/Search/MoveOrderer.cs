using Blaze.MoveGen;

namespace Blaze.Search
{
    public static class MoveOrderer
    {
        public const int MaxPly = 256;
        public static readonly Move[] Killer1 = new Move[MaxPly];
        public static readonly Move[] Killer2 = new Move[MaxPly];
        public static readonly int[,,] History = new int[2, 64, 64]; // [side, from, to]

        public static void ClearKillers()
        {
            Array.Clear(Killer1);
            Array.Clear(Killer2);
        }

        public static void ClearHistory()
        {
            Array.Clear(History);
        }

        public static void AgeHistory()
        {
            for (int s = 0; s < 2; s++)
                for (int f = 0; f < 64; f++)
                    for (int t = 0; t < 64; t++)
                        History[s, f, t] /= 2;
        }

        public static void OrderMoves(Span<Move> moves, Board board, Move pvMove, int plyFromRoot)
        {
            moves.Sort((a, b) =>
            {
                int scoreA = Score(a, board, pvMove, plyFromRoot);
                int scoreB = Score(b, board, pvMove, plyFromRoot);
                return scoreB.CompareTo(scoreA);
            });
        }

        private static int Score(Move move, Board board, Move pvMove, int plyFromRoot)
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

            if (move == Killer1[plyFromRoot]) return 900_000;
            if (move == Killer2[plyFromRoot]) return 800_000;

            return History[board.ColorToMove, move.StartingSquare, move.TargetSquare];
        }
    }
}