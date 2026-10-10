using Blaze.MoveGen;

namespace Blaze.Helpers
{
    /// <summary>
    /// Helper methods for board-related calculations.
    /// </summary>
    public static class BoardHelper
    {
        /// <summary>
        /// Returns the squares strictly between two aligned squares on a rook
        /// or bishop attack line. Returns 0 if the squares are not connected
        /// by the specified sliding piece.
        /// </summary>
        public static ulong GetAttackTunnel(int square1, int square2, bool isRook)
        {
            ulong square1Bitboard = 1UL << square1;
            ulong square2Bitboard = 1UL << square2;

            if (isRook)
            {
                if ((Magic.GetRookAttacks(square1, 0) & square2Bitboard) == 0)
                    return 0;

                return Magic.GetRookAttackMask(square1, square2Bitboard) &
                       Magic.GetRookAttackMask(square2, square1Bitboard);
            }

            if ((Magic.GetBishopAttacks(square1, 0) & square2Bitboard) == 0)
                return 0;

            return Magic.GetBishopAttackMask(square1, square2Bitboard) &
                   Magic.GetBishopAttackMask(square2, square1Bitboard);
        }
    }
}