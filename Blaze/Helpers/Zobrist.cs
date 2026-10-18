using System.Numerics;
using System.Runtime.CompilerServices;

namespace Blaze.Helpers
{
    /// <summary>
    /// Generates and stores Zobrist keys used to uniquely identify
    /// chess positions.
    ///
    /// A position hash is formed by XORing random values associated with:
    /// - Piece-square combinations
    /// - Castling rights
    /// - En passant files
    /// - Side to move
    /// </summary>
    public static class Zobrist
    {
        /// <summary>
        /// Random values for every piece-square combination.
        /// Indexed as (piece * 64 + square).
        /// </summary>
        private static readonly ulong[] PieceSquareKeys = new ulong[12 * 64];

        /// <summary>
        /// Random values for all castling rights states.
        /// </summary>
        private static readonly ulong[] CastlingKeys = new ulong[16];

        /// <summary>
        /// Random values for en passant files.
        /// </summary>
        private static readonly ulong[] EnPassantKeys = new ulong[8];

        /// <summary>
        /// Random value XORed when black is to move.
        /// </summary>
        public static readonly ulong SideToMoveKey;

        /// <summary>
        /// Deterministic seed used to initialize the Zobrist tables.
        /// </summary>
        private static ulong seed = 1337UL;

        private const ulong XorShift64StarMultiplier = 2685821657736338717UL;

        static Zobrist()
        {
            for (int i = 0; i < PieceSquareKeys.Length; i++)
                PieceSquareKeys[i] = RandomU64();

            for (int i = 0; i < CastlingKeys.Length; i++)
                CastlingKeys[i] = RandomU64();

            for (int i = 0; i < EnPassantKeys.Length; i++)
                EnPassantKeys[i] = RandomU64();

            SideToMoveKey = RandomU64();
        }

        /// <summary>
        /// xorshift64* pseudo-random number generator.
        /// Used only during table initialization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RandomU64()
        {
            seed ^= seed >> 12;
            seed ^= seed << 25;
            seed ^= seed >> 27;

            return seed * XorShift64StarMultiplier;
        }

        /// <summary>
        /// Returns the Zobrist key for a piece on a square.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PieceKey(int pieceIndex, int square)
        {
            return PieceSquareKeys[(pieceIndex << 6) | square];
        }

        /// <summary>
        /// Returns the Zobrist key for a castling rights state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CastlingKey(int castlingRights)
        {
            return CastlingKeys[castlingRights & 0b1111];
        }

        /// <summary>
        /// Returns the Zobrist key for an en passant file.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong EnPassantKey(int file)
        {
            return EnPassantKeys[file];
        }

        /// <summary>
        /// Computes the Zobrist hash of a position from scratch.
        /// Primarily used for initialization and debugging.
        /// Incremental updates should be used during search.
        /// </summary>
        public static ulong ComputeHash(Board board)
        {
            ulong hash = 0;

            for (int piece = 0; piece < PieceSquareKeys.Length / 64; piece++)
            {
                ulong bitboard = board.PiecesBitboards[piece];

                while (bitboard != 0)
                {
                    int square = BitboardHelper.PopLSB(ref bitboard);
                    hash ^= PieceSquareKeys[(piece << 6) | square];
                }
            }

            hash ^= CastlingKeys[(int)board.CurrentGameState.CastlingRights & 0b1111];

            if (board.EnPassantSquare >= 0)
                hash ^= EnPassantKeys[board.EnPassantSquare & 7];

            if (board.ColorToMove == Piece.Black)
                hash ^= SideToMoveKey;

            return hash;
        }
    }
}