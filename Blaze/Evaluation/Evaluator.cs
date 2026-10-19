using System.Numerics;

namespace Blaze.Evaluation
{
    public static class Evaluator
    {
        private static readonly int[] PieceValues =
        {
            100, // Pawn
            320, // Knight
            330, // Bishop
            500, // Rook
            900, // Queen
            0 // King
        };
        private static readonly int[] gamephaseInc = { 0, 1, 1, 2, 4, 0};

        public static int Evaluate(Board board)
        {
            int mgEval = 0;
            int egEval = 0;
                
            int material = CountMaterial(board, out int gamePhase);
            mgEval += material;
            egEval += material;

            var psqt = Activity.EvaluatePieceSquareTables(board);
            mgEval += psqt.mgScore;
            egEval += psqt.egScore;

            int colorBias = board.IsWhiteToMove ? 1 : -1;
            if (gamePhase > 24) gamePhase = 24; /* in case of early promotion */
            return (mgEval * gamePhase + egEval * (24 - gamePhase)) / 24 * colorBias;
        }

        private static int CountMaterial(Board board, out int gamePhase)
        {
            int material = 0;
            gamePhase = 0;

            for (int i = 0; i < 11; i++)
            {
                ulong Bitboard = board.PiecesBitboards[i];
                int pieceCount = BitOperations.PopCount(Bitboard);
                int pieceColor = i < 6 ? 1 : -1; // White pieces are indexed 0-5, black pieces are indexed 6-11
                int pieceValue = PieceValues[i % 6];

                material += pieceCount * pieceValue * pieceColor;
                gamePhase += pieceCount * gamephaseInc[i % 6];
            }

            return material;
        }
    }
}