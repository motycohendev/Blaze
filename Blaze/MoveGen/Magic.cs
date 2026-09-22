using Blaze.Helpers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
namespace Blaze.MoveGen
{
    /// <summary>
    /// Precomputed attack tables for sliding pieces (rooks, bishops, and by extension queens).
    /// For any square and blocker configuration, the attack bitboard can be looked up directly
    /// instead of generated on the fly, which is a major speedup since sliding piece attacks
    /// make up most of move generation's cost.
    /// See https://www.chessprogramming.org/Magic_Bitboards for background.
    /// </summary>
    public static class Magic
    {
        /// <summary>
        /// Precomputes all lookup tables required for magic bitboard move generation.
        /// Must be called once before any attack queries are made.
        /// </summary>
        public static void Init()
        {
            PrecomputeEdgeMasks();
            PrecomputeRookAttacks();
            PrecomputeBishopAttacks();
        }

        #region Magic Numbers

        private static readonly ulong[] MagicRookNumbers = new ulong[]
        {
            0x8a80104000800020, 0x140002000100040, 0x2801880a0017001, 0x100081001000420,
            0x200020010080420, 0x3001c0002010008, 0x8480008002000100, 0x2080088004402900,
            0x800098204000, 0x2024401000200040, 0x100802000801000, 0x120800800801000,
            0x208808088000400, 0x2802200800400, 0x2200800100020080, 0x801000060821100,
            0x80044006422000, 0x100808020004000, 0x12108a0010204200, 0x140848010000802,
            0x481828014002800, 0x8094004002004100, 0x4010040010010802, 0x20008806104,
            0x100400080208000, 0x2040002120081000, 0x21200680100081, 0x20100080080080,
            0x2000a00200410, 0x20080800400, 0x80088400100102, 0x80004600042881,
            0x4040008040800020, 0x440003000200801, 0x4200011004500, 0x188020010100100,
            0x14800401802800, 0x2080040080800200, 0x124080204001001, 0x200046502000484,
            0x480400080088020, 0x1000422010034000, 0x30200100110040, 0x100021010009,
            0x2002080100110004, 0x202008004008002, 0x20020004010100, 0x2048440040820001,
            0x101002200408200, 0x40802000401080, 0x4008142004410100, 0x2060820c0120200,
            0x1001004080100, 0x20c020080040080, 0x2935610830022400, 0x44440041009200,
            0x280001040802101, 0x2100190040002085, 0x80c0084100102001, 0x4024081001000421,
            0x20030a0244872, 0x12001008414402, 0x2006104900a0804, 0x1004081002402
        };

        private static readonly int[] MagicRookShifts = new int[]
        {
            52, 53, 53, 53, 53, 53, 53, 52,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 54, 54, 54, 54, 53,
            52, 53, 53, 53, 53, 53, 53, 52
        };

        private static readonly ulong[] MagicBishopNumbers = new ulong[]
        {
            0x40040844404084, 0x2004208a004208, 0x10190041080202, 0x108060845042010,
            0x581104180800210, 0x2112080446200010, 0x1080820820060210, 0x3c0808410220200,
            0x4050404440404, 0x21001420088, 0x24d0080801082102, 0x1020a0a020400,
            0x40308200402, 0x4011002100800, 0x401484104104005, 0x801010402020200,
            0x400210c3880100, 0x404022024108200, 0x810018200204102, 0x4002801a02003,
            0x85040820080400, 0x810102c808880400, 0xe900410884800, 0x8002020480840102,
            0x220200865090201, 0x2010100a02021202, 0x152048408022401, 0x20080002081110,
            0x4001001021004000, 0x800040400a011002, 0xe4004081011002, 0x1c004001012080,
            0x8004200962a00220, 0x8422100208500202, 0x2000402200300c08, 0x8646020080080080,
            0x80020a0200100808, 0x2010004880111000, 0x623000a080011400, 0x42008c0340209202,
            0x209188240001000, 0x400408a884001800, 0x110400a6080400, 0x1840060a44020800,
            0x90080104000041, 0x201011000808101, 0x1a2208080504f080, 0x8012020600211212,
            0x500861011240000, 0x180806108200800, 0x4000020e01040044, 0x300000261044000a,
            0x802241102020002, 0x20906061210001, 0x5a84841004010310, 0x4010801011c04,
            0xa010109502200, 0x4a02012000, 0x500201010098b028, 0x8040002811040900,
            0x28000010020204, 0x6000020202d0240, 0x8918844842082200, 0x4010011029020020
        };

        private static readonly int[] MagicBishopShifts = new int[]
        {
            58, 59, 59, 59, 59, 59, 59, 58,
            59, 59, 59, 59, 59, 59, 59, 59,
            59, 59, 57, 57, 57, 57, 59, 59,
            59, 59, 57, 55, 55, 57, 59, 59,
            59, 59, 57, 55, 55, 57, 59, 59,
            59, 59, 57, 57, 57, 57, 59, 59,
            59, 59, 59, 59, 59, 59, 59, 59,
            58, 59, 59, 59, 59, 59, 59, 58
        };

        #endregion

        #region Edge Masks

        // Indexed by direction: South, West, East, North.
        private static readonly ulong[] RookEdgeMasks = new ulong[4];

        // Indexed by direction: Southwest, Southeast, Northwest, Northeast.
        private static readonly ulong[] BishopEdgeMasks = new ulong[4];

        private static void PrecomputeEdgeMasks()
        {
            for (int square = 0; square < 64; square++)
            {
                int rank = square >> 3;
                int file = square & 7;
                ulong bit = 1ul << square;

                // Rook
                if (rank == 0) RookEdgeMasks[0] |= bit;
                if (file == 0) RookEdgeMasks[1] |= bit;
                if (file == 7) RookEdgeMasks[2] |= bit;
                if (rank == 7) RookEdgeMasks[3] |= bit;

                // Bishop
                if (rank == 0 || file == 0) BishopEdgeMasks[0] |= bit;
                if (rank == 0 || file == 7) BishopEdgeMasks[1] |= bit;
                if (rank == 7 || file == 0) BishopEdgeMasks[2] |= bit;
                if (rank == 7 || file == 7) BishopEdgeMasks[3] |= bit;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsEdge(ulong[] edgeMasks, int direction, int square)
            => (edgeMasks[direction] & (1ul << square)) != 0;

        #endregion

        #region Shared Helpers

        // Only guards against stepping past square 63; the caller's edge checks are what
        // actually prevent wrapping across ranks/files or going below square 0.
        private static bool IsSquareOnBoard(int square) => square < 64;

        // Enumerates every possible blocker combination (subset) of the given mask.
        private static ulong[] GetBlockerCombinations(ulong mask)
        {
            int bitCount = BitOperations.PopCount(mask);
            int combinationCount = 1 << bitCount;
            ulong[] combinations = new ulong[combinationCount];

            // Extract the set bit positions via PopLSB - no List allocation needed.
            Span<int> squares = stackalloc int[bitCount];
            ulong remaining = mask;
            for (int i = 0; i < bitCount; i++)
                squares[i] = BitboardHelper.PopLSB(ref remaining);

            for (int i = 0; i < combinationCount; i++)
            {
                ulong combination = 0;
                for (int j = 0; j < bitCount; j++)
                    if ((i & (1 << j)) != 0)
                        combination |= 1ul << squares[j];
                combinations[i] = combination;
            }
            return combinations;
        }

        #endregion

        #region Rook

        // South, West, East, North.
        private static readonly int[] RookOffsets = { -8, -1, 1, 8 };
        private static readonly ulong[][] PrecomputedRookAttacks = new ulong[64][];

        /// <summary>Relevant occupancy mask used to compute the magic index for each square.</summary>
        public static readonly ulong[] PrecomputedRookAttackMask = new ulong[64];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetRookIndex(int square, ulong blockers)
        {
            ulong mask = PrecomputedRookAttackMask[square];

            // PEXT is faster and works with any mask, so prefer it when available.
            if (Bmi2.X64.IsSupported)
                return (int)Bmi2.X64.ParallelBitExtract(blockers, mask);

            blockers &= mask;
            return (int)((blockers * MagicRookNumbers[square]) >> MagicRookShifts[square]);
        }

        /// <summary>Returns the rook attack bitboard for a square given the current blockers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetRookAttacks(int square, ulong blockers)
            => PrecomputedRookAttacks[square][GetRookIndex(square, blockers)];

        /// <summary>
        /// Generates the rook attack bitboard for a square by tracing each of the four rays
        /// until a blocker is hit or the ray leaves the board.
        /// </summary>
        /// <param name="blockers">Occupied squares that stop a ray early.</param>
        /// <param name="forMoveGeneration">
        /// True to generate actual attacks during move generation. False to build the
        /// occupancy mask used to compute the magic index.
        /// </param>
        public static ulong GetRookAttackMask(int square, ulong blockers = 0, bool forMoveGeneration = false)
        {
            ulong attackMask = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                int offset = RookOffsets[direction];
                int current = square;
                while (true)
                {
                    // Stop before stepping off the board or wrapping to another rank/file.
                    if (IsEdge(RookEdgeMasks, direction, current)) break;
                    current += offset;
                    if (!IsSquareOnBoard(current)) break; // defensive; the edge check above should prevent this

                    attackMask |= 1ul << current;
                    if ((blockers & (1ul << current)) != 0) break;

                    // When building the occupancy mask, stop once this ray reaches the board edge.
                    if (!forMoveGeneration && IsEdge(RookEdgeMasks, direction, current)) break;
                }
            }
            return attackMask;
        }

        private static void PrecomputeRookAttacks()
        {
            for (int square = 0; square < 64; square++)
            {
                ulong mask = GetRookAttackMask(square);
                PrecomputedRookAttackMask[square] = mask;

                int tableSize = 1 << BitOperations.PopCount(mask);
                PrecomputedRookAttacks[square] = new ulong[tableSize];

                foreach (ulong blockers in GetBlockerCombinations(mask))
                {
                    int index = GetRookIndex(square, blockers);
                    PrecomputedRookAttacks[square][index] = GetRookAttackMask(square, blockers, true);
                }
            }
        }

        #endregion

        #region Bishop

        // Southwest, Southeast, Northwest, Northeast.
        private static readonly int[] BishopOffsets = { -9, -7, 7, 9 };
        private static readonly ulong[][] PrecomputedBishopAttacks = new ulong[64][];

        /// <summary>Relevant occupancy mask used to compute the magic index for each square.</summary>
        public static readonly ulong[] PrecomputedBishopAttackMask = new ulong[64];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBishopIndex(int square, ulong blockers)
        {
            ulong mask = PrecomputedBishopAttackMask[square];

            if (Bmi2.X64.IsSupported)
                return (int)Bmi2.X64.ParallelBitExtract(blockers, mask);

            blockers &= mask;
            return (int)((blockers * MagicBishopNumbers[square]) >> MagicBishopShifts[square]);
        }

        /// <summary>Returns the bishop attack bitboard for a square given the current blockers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetBishopAttacks(int square, ulong blockers)
            => PrecomputedBishopAttacks[square][GetBishopIndex(square, blockers)];

        /// <summary>
        /// Generates the bishop attack bitboard for a square by tracing each of the four diagonal
        /// rays until a blocker is hit or the ray leaves the board.
        /// </summary>
        /// <param name="blockers">Occupied squares that stop a ray early.</param>
        /// <param name="forMoveGeneration">
        /// True to generate actual attacks during move generation. False to build the
        /// occupancy mask used to compute the magic index.
        /// </param>
        public static ulong GetBishopAttackMask(int square, ulong blockers = 0, bool forMoveGeneration = false)
        {
            ulong attackMask = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                int offset = BishopOffsets[direction];
                int current = square;
                while (true)
                {
                    if (IsEdge(BishopEdgeMasks, direction, current)) break;
                    current += offset;
                    if (!IsSquareOnBoard(current)) break;

                    attackMask |= 1ul << current;
                    if ((blockers & (1ul << current)) != 0) break;

                    if (!forMoveGeneration && IsEdge(BishopEdgeMasks, direction, current)) break;
                }
            }
            return attackMask;
        }

        private static void PrecomputeBishopAttacks()
        {
            for (int square = 0; square < 64; square++)
            {
                ulong mask = GetBishopAttackMask(square);
                PrecomputedBishopAttackMask[square] = mask;

                int tableSize = 1 << BitOperations.PopCount(mask);
                PrecomputedBishopAttacks[square] = new ulong[tableSize];

                foreach (ulong blockers in GetBlockerCombinations(mask))
                {
                    int index = GetBishopIndex(square, blockers);
                    PrecomputedBishopAttacks[square][index] = GetBishopAttackMask(square, blockers, true);
                }
            }
        }

        #endregion
    }
}