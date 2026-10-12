using Blaze.MoveGen;

namespace Blaze.Search
{
    public class MoveOrderer
    {
        public const int MaxPly = 256;
        private const int KillerSlots = 2;
        // 128 * 128
        private const int MaxHistory = 16384;


        private const int PvScore = 2_000_000;
        private const int TTScore = 1_000_000;
        private const int CaptureBase = 100_000;
        private const int Killer1Score = 90_000;
        private const int Killer2Score = 80_000;

        // killers[ply, slot]: quiet moves that caused a beta cutoff at this ply
        private static readonly Move[,] killers = new Move[MaxPly, KillerSlots];
        // history[side, from, to]
        private static readonly int[,,] history = new int[2, 64, 64];

        public static void ClearKillers()
        {
            Array.Clear(killers, 0, killers.Length);
        }

        public static void ClearHistory()
        {
            Array.Clear(history, 0, history.Length);
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

        // good = true for the quiet move that caused a cutoff,
        // false for quiet moves searched before it that didn't.
        public static void UpdateHistory(bool whiteToMove, Move move, int depth, bool good)
        {
            int bonus = Math.Min(depth * depth, 1200);
            if (!good)
                bonus = -bonus;

            ref int h = ref history[whiteToMove ? 0 : 1, move.StartingSquare, move.TargetSquare];

            // Gravity: pulls h toward the bonus sign but keeps |h| < MaxHistory
            h += bonus - h * Math.Abs(bonus) / MaxHistory;
        }

        public static void OrderMoves(Span<Move> moves, Board board, Move pvMove, Move ttMove, int ply)
        {
            int colorToMove = board.ColorToMove;
            Span<int> scores = stackalloc int[moves.Length];
            for (int i = 0; i < moves.Length; i++)
                scores[i] = -Score(moves[i], board, colorToMove, pvMove, ttMove, ply); // negated: ascending sort = best first

            scores.Sort(moves);
        }

        private static int Score(Move move, Board board, int colorToMove, Move pvMove, Move ttMove, int ply)
        {
            if (move == pvMove)
                return PvScore;

            if (move == ttMove)
                return TTScore;

            int captured = board.Squares[move.TargetSquare];
            if (captured != Piece.None)
            {
                int capturedPieceType = Piece.PieceType(captured);
                int movingPieceType = Piece.PieceType(board.Squares[move.StartingSquare]);
                return CaptureBase + 100 * capturedPieceType - movingPieceType;
            }

            if (ply >= 0 && ply < MaxPly)
            {
                if (move == killers[ply, 0])
                    return Killer1Score;
                if (move == killers[ply, 1])
                    return Killer2Score;
            }

            return history[colorToMove, move.StartingSquare, move.TargetSquare];
        }
    }
}