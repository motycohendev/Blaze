using System.Runtime.CompilerServices;

namespace Blaze
{
    /// <summary>
    /// Encodes a chess piece as a single integer.
    /// Bits 0-1 store the color and the remaining bits store the piece type.
    /// </summary>
    public static class Piece
    {
        // Piece types
        public const int None = 0;
        public const int Pawn = 1;
        public const int Knight = 2;
        public const int Bishop = 3;
        public const int Rook = 4;
        public const int Queen = 5;
        public const int King = 6;

        // Colors
        public const int White = 0;
        public const int Black = 1;

        private const int ColorMask = 0b11;
        private const int PieceTypeShift = 2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Create(int pieceType, int color)
        {
            return (pieceType << PieceTypeShift) | color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Create(int pieceType, bool isWhite)
        {
            return (pieceType << PieceTypeShift) | (isWhite ? White : Black);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PieceType(int piece)
        {
            return piece >> PieceTypeShift;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Color(int piece)
        {
            return piece & ColorMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWhite(int piece)
        {
            return Color(piece) == White;
        }

        /// <summary>
        /// Returns whether the piece is a sliding piece (bishop, rook, or queen).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSliding(int piece)
        {
            return PieceType(piece) is Bishop or Rook or Queen;
        }

        /// <summary>
        /// Converts a FEN piece symbol to its encoded piece representation.
        /// </summary>
        public static int FenSymbolToPiece(char symbol)
        {
            bool isWhite = char.IsUpper(symbol);

            return char.ToLowerInvariant(symbol) switch
            {
                'p' => Create(Pawn, isWhite),
                'n' => Create(Knight, isWhite),
                'b' => Create(Bishop, isWhite),
                'r' => Create(Rook, isWhite),
                'q' => Create(Queen, isWhite),
                'k' => Create(King, isWhite),
                _ => None
            };
        }

        /// <summary>
        /// Converts an encoded piece to its FEN symbol.
        /// </summary>
        public static char PieceToFenSymbol(int piece)
        {
            char symbol = PieceType(piece) switch
            {
                Pawn => 'p',
                Knight => 'n',
                Bishop => 'b',
                Rook => 'r',
                Queen => 'q',
                King => 'k',
                _ => ' '
            };

            return Color(piece) == White
                ? char.ToUpperInvariant(symbol)
                : symbol;
        }
    }
}