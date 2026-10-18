namespace Blaze.MoveGen
{
    /// <summary>
    /// Compact 16-bit move representation.
    /// Layout:
    /// [from: 6 bits][to: 6 bits][flag: 4 bits]
    /// </summary>
    public readonly struct Move : IEquatable<Move>
    {
        public const ushort StartingSquareMask = 0b1111110000000000;
        public const ushort TargetSquareMask = 0b0000001111110000;
        public const ushort FlagMask = 0b0000000000001111;

        public const int StartingSquareShift = 10;
        public const int TargetSquareShift = 4;

        private readonly ushort rawMove;

        public int StartingSquare => (rawMove & StartingSquareMask) >> StartingSquareShift;
        public int TargetSquare => (rawMove & TargetSquareMask) >> TargetSquareShift;
        public int Flag => rawMove & FlagMask;

        /// <summary>
        /// Creates a quiet move.
        /// </summary>
        public Move(int startingSquare, int targetSquare)
        {
            rawMove = (ushort)(
                (startingSquare << StartingSquareShift) |
                (targetSquare << TargetSquareShift));
        }

        /// <summary>
        /// Creates a move with the specified move flag.
        /// </summary>
        public Move(int startingSquare, int targetSquare, int moveFlag)
        {
            rawMove = (ushort)(
                (startingSquare << StartingSquareShift) |
                (targetSquare << TargetSquareShift) |
                moveFlag);
        }

        public static Move NullMove => default;

        public bool IsNull => rawMove == 0;
        public bool IsPromotion => Flag is >= MoveFlag.KnightPromotion and <= MoveFlag.QueenPromotion;
        public bool IsEnPassant => Flag == MoveFlag.EnPassant;

        public override bool Equals(object? obj)
        {
            return obj is Move other && Equals(other);
        }

        public bool Equals(Move other)
        {
            return rawMove == other.rawMove;
        }

        public override int GetHashCode()
        {
            return rawMove;
        }

        public static bool operator ==(Move left, Move right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Move left, Move right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Move flags stored in the upper four bits of a move.
    /// </summary>
    public static class MoveFlag
    {
        public const int DoublePawnPush = 1;

        public const int KnightPromotion = 2;
        public const int BishopPromotion = 3;
        public const int RookPromotion = 4;
        public const int QueenPromotion = 5;

        public const int EnPassant = 6;
        public const int CastleShort = 7;
        public const int CastleLong = 8;
    }
}