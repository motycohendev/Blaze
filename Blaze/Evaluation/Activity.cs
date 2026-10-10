using Blaze.Helpers;
using Blaze.MoveGen;
using System.Numerics;

namespace Blaze.Evaluation
{
    public static class Activity
    {
        #region SQUARE_TABLES
        private static readonly int[] EarlyPawnSquareTables =
        {
             0,  0,  0,  0,  0,  0,  0,  0,
            50, 50, 50, 50, 50, 50, 50, 50,
            10, 10, 20, 30, 30, 20, 10, 10,
             5,  5, 10, 25, 25, 10,  5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5, -5,-10,  0,  0,-10, -5,  5,
             5, 10, 10,-20,-20, 10, 10,  5,
             0,  0,  0,  0,  0,  0,  0,  0
        };

        private static readonly int[] LatePawnSquareTables =
        {
             0,  0,  0,  0,  0,  0,  0,  0,
             80,  80,  80,  80,  80,  80,  80,  80,
             50,  50,  50,  50,  50,  50,  50,  50,
             30,  30,  30,  30,  30,  30,  30,  30,
             20,  20,  20,  20,  20,  20,  20,  20,
             10,  10,  10,  10,  10,  10,  10,  10,
             10,  10,  10,  10,  10,  10,  10,  10,
             0,  0,  0,  0,  0,  0,  0,  0,
        };

        private static readonly int[] KnightSquareTables =
        {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -50,-40,-30,-30,-30,-30,-40,-50,
        };

        private static readonly int[] BishopSquareTables =
        {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -20,-10,-10,-10,-10,-10,-10,-20,
        };

        private static readonly int[] RookSquareTables =
        {
              0,  0,  0,  0,  0,  0,  0,  0,
              5, 10, 10, 10, 10, 10, 10,  5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
              0,  0,  0,  5,  5,  0,  0,  0
        };

        private static readonly int[] QueenSquareTables =
        {
            -20,-10,-10, -5, -5,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5,  5,  5,  5,  0,-10,
             -5,  0,  5,  5,  5,  5,  0, -5,
              0,  0,  5,  5,  5,  5,  0, -5,
            -10,  5,  5,  5,  5,  5,  0,-10,
            -10,  0,  5,  0,  0,  0,  0,-10,
            -20,-10,-10, -5, -5,-10,-10,-20
        };

        private static readonly int[] EarlyKingSquareTables =
        {
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -20,-30,-30,-40,-40,-30,-30,-20,
            -10,-20,-20,-20,-20,-20,-20,-10,
             20, 20,  0,  0,  0,  0, 20, 20,
             20, 30, 10,  0,  0, 10, 30, 20
        };

        private static readonly int[] LateKingSquareTables =
        {
            -50,-40,-30,-20,-20,-30,-40,-50,
            -30,-20,-10,  0,  0,-10,-20,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-30,  0,  0,  0,  0,-30,-30,
            -50,-30,-30,-30,-30,-30,-30,-50
        };

        private static readonly int[][] EarlySquareTables =
        {
            EarlyPawnSquareTables,
            KnightSquareTables,
            BishopSquareTables,
            RookSquareTables,
            QueenSquareTables,
            EarlyKingSquareTables
        };

        private static readonly int[][] LateSquareTables =
        {
            LatePawnSquareTables,
            KnightSquareTables,
            BishopSquareTables,
            RookSquareTables,
            QueenSquareTables,
            LateKingSquareTables
        };
        #endregion

        private static readonly int[] gamephaseInc = { 0, 1, 1, 2, 4, 0 };

        public static int EvaluatePieceSquareTables(Board board)
        {
            int mgEval = 0;
            int egEval = 0;

            int gamePhase = 0;

            for (int i = 0; i < 12; i++)
            {
                ulong Bitboard = board.PiecesBitboards[i];
                int pieceType = i % 6;
                int color = i < 6 ? 1 : -1; // White pieces are indexed 0-5, black pieces are indexed 6-11

                while (Bitboard != 0)
                {
                    int square = BitboardHelper.PopLSB(ref Bitboard);
                    int squareIndex = color == 1 ? square ^ 56 : square; // Flip the square index for black pieces

                    mgEval += EarlySquareTables[pieceType][squareIndex] * color;
                    egEval += LateSquareTables[pieceType][squareIndex] * color;

                    gamePhase += gamephaseInc[pieceType];
                }
            }

            if (gamePhase > 24) gamePhase = 24; /* in case of early promotion */
            return (mgEval * gamePhase + egEval * (24 - gamePhase)) / 24;
        }
    }
}