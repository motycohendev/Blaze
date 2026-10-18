namespace Blaze
{
    /// <summary>
    /// Stores the information required to restore the board when unmaking a move.
    /// A copy of this struct is pushed onto the history stack before each move.
    /// </summary>
    public struct GameState
    {
        public int MovingPiece;
        public int CapturedPiece;
        public int EnPassantSquare;
        public CastlingRights CastlingRights;
        public int PlyCount;
        public int PlySinceCaptureOrPawnMove;

        /// <summary>
        /// Creates a new game state.
        /// </summary>
        public GameState(
            int movingPiece,
            int capturedPiece,
            int enPassantSquare,
            CastlingRights castlingRights,
            int plyCount,
            int plySinceCaptureOrPawnMove)
        {
            MovingPiece = movingPiece;
            CapturedPiece = capturedPiece;
            EnPassantSquare = enPassantSquare;
            CastlingRights = castlingRights;
            PlyCount = plyCount;
            PlySinceCaptureOrPawnMove = plySinceCaptureOrPawnMove;
        }

        /// <summary>
        /// Returns whether the specified color can still castle on the given side.
        /// </summary>
        public readonly bool HasCastleRight(int color, bool kingside)
        {
            CastlingRights right = color == Piece.White
                ? (kingside
                    ? CastlingRights.WhiteKingside
                    : CastlingRights.WhiteQueenside)
                : (kingside
                    ? CastlingRights.BlackKingside
                    : CastlingRights.BlackQueenside);

            return (CastlingRights & right) != 0;
        }
    }

    /// <summary>
    /// Bit flags representing the castling rights currently available.
    /// Multiple values can be combined using bitwise OR.
    /// </summary>
    [Flags]
    public enum CastlingRights : byte
    {
        None = 0,
        WhiteKingside = 1 << 0,
        WhiteQueenside = 1 << 1,
        BlackKingside = 1 << 2,
        BlackQueenside = 1 << 3,

        All = WhiteKingside |
              WhiteQueenside |
              BlackKingside |
              BlackQueenside
    }
}