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

        public static int Evaluate(Board board)
        {
            int eval = 0;

            eval += CountMaterial(board);
            eval += Activity.EvaluatePieceSquareTables(board);

            int colorBias = board.IsWhiteToMove ? 1 : -1;
            return eval * colorBias;
        }

        private static int CountMaterial(Board board)
        {
            int material = 0;

            for (int i = 0; i < 11; i++)
            {
                ulong Bitboard = board.PiecesBitboards[i];
                int pieceCount = BitOperations.PopCount(Bitboard);
                int pieceColor = i < 6 ? 1 : -1; // White pieces are indexed 0-5, black pieces are indexed 6-11
                int pieceValue = PieceValues[i % 6];

                material += pieceCount * pieceValue * pieceColor;
            }

            return material;
        }
    }
}