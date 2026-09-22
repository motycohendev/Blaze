using System.Numerics;
using System.Runtime.CompilerServices;

namespace Blaze.Helpers
{
    /// <summary>
    /// Helper methods for bitboard manipulation.
    /// </summary>
    public static class BitboardHelper
    {
        /// <summary>
        /// Bit mask for file A.
        /// </summary>
        public const ulong FileAMask = 0x0101010101010101;

        /// <summary>
        /// Returns the bitboard index for the specified piece type and color.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBitboardIndex(int pieceType, int color)
        {
            return pieceType + (color * 6) - 1;
        }

        /// <summary>
        /// Returns the bitboard index for the specified piece type and color.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBitboardIndex(int pieceType, bool isWhite)
        {
            return pieceType + (isWhite ? 0 : 6) - 1;
        }

        /// <summary>
        /// Toggles the bit at the specified index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToggleBit(ref ulong bitboard, int bitIndex)
        {
            bitboard ^= 1UL << bitIndex;
        }

        /// <summary>
        /// Moves a piece from one square to another.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MovePiece(ref ulong bitboard, int from, int to)
        {
            bitboard ^= (1UL << from) | (1UL << to);
        }

        /// <summary>
        /// Returns whether the specified bit is set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(ulong bitboard, int bitIndex)
        {
            return (bitboard & (1UL << bitIndex)) != 0;
        }

        /// <summary>
        /// Removes and returns the index of the least significant set bit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopLSB(ref ulong bitboard)
        {
            int square = BitOperations.TrailingZeroCount(bitboard);
            bitboard &= bitboard - 1;
            return square;
        }

        /// <summary>
        /// Shifts a bitboard left or right.
        /// </summary>
        public static ulong ShiftBitboard(ulong bitboard, int shift)
        {
            if (shift > 0)
                return bitboard << shift;

            if (shift < 0)
                return bitboard >> -shift;

            return bitboard;
        }

        /// <summary>
        /// Prints a bitboard to the console.
        /// </summary>
        public static void PrintBitboard(ulong bitboard)
        {
            for (int rank = 7; rank >= 0; rank--)
            {
                for (int file = 0; file < 8; file++)
                {
                    int square = rank * 8 + file;
                    char symbol = IsBitSet(bitboard, square) ? '1' : '.';
                    Console.Write($" {symbol}");
                }

                Console.WriteLine();
            }
        }
    }
}