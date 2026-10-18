namespace Blaze.MoveGen
{
    public static class PrecomputeMoveData
    {
        public const ulong NotAFile = 0xfefefefefefefefe;
        public const ulong NotHFile = 0x7f7f7f7f7f7f7f7f;
        public const ulong NotABFile = 0xfcfcfcfcfcfcfcfc;
        public const ulong NotGHFile = 0x3f3f3f3f3f3f3f3f;

        /// <summary>
        /// Precomputes pawn, knight, and king attack tables used during move generation.
        /// </summary>
        public static void Init()
        {
            PrecomputePawnAttacks();
            PrecomputeKnightAttacks();
            PrecomputeKingAttacks();
        }

        private static void PrecomputePawnAttacks()
        {
            for (int square = 0; square < 64; square++)
            {
                ulong bitboard = 1UL << square;

                MoveGenerator.WhitePawnsAttacks[square] =
                    CalculatePawnAttacks(bitboard, true);

                MoveGenerator.BlackPawnsAttacks[square] =
                    CalculatePawnAttacks(bitboard, false);
            }
        }

        private static ulong CalculatePawnAttacks(ulong pawn, bool isWhite)
        {
            if (isWhite)
            {
                return ((pawn & NotHFile) << 9) |
                       ((pawn & NotAFile) << 7);
            }

            return ((pawn & NotHFile) >> 7) |
                   ((pawn & NotAFile) >> 9);
        }

        private static void PrecomputeKnightAttacks()
        {
            for (int square = 0; square < 64; square++)
            {
                MoveGenerator.KnightAttacks[square] =
                    CalculateKnightAttacks(1UL << square);
            }
        }

        private static ulong CalculateKnightAttacks(ulong knight)
        {
            ulong attacks = 0;

            attacks |= (knight & NotHFile) << 17;
            attacks |= (knight & NotGHFile) << 10;
            attacks |= (knight & NotGHFile) >> 6;
            attacks |= (knight & NotHFile) >> 15;
            attacks |= (knight & NotAFile) << 15;
            attacks |= (knight & NotABFile) << 6;
            attacks |= (knight & NotABFile) >> 10;
            attacks |= (knight & NotAFile) >> 17;

            return attacks;
        }

        private static void PrecomputeKingAttacks()
        {
            for (int square = 0; square < 64; square++)
            {
                MoveGenerator.KingAttacks[square] =
                    CalculateKingAttacks(1UL << square);
            }
        }

        private static ulong CalculateKingAttacks(ulong king)
        {
            ulong attacks = 0;

            attacks |= (king & NotAFile) >> 1;
            attacks |= (king & NotAFile) >> 9;
            attacks |= (king & NotAFile) << 7;
            attacks |= (king & NotHFile) << 1;
            attacks |= (king & NotHFile) << 9;
            attacks |= (king & NotHFile) >> 7;
            attacks |= king >> 8;
            attacks |= king << 8;

            return attacks;
        }
    }
}