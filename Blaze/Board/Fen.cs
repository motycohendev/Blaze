using Blaze.Helpers;

namespace Blaze
{
    /// <summary>
    /// Utility class for working with FEN (Forsyth-Edwards Notation).
    /// FEN is the standard notation for describing a chess position.
    /// More information: https://www.chessprogramming.org/Forsyth-Edwards_Notation
    /// </summary>
    public static class FEN
    {
        public const string StartingPositionFEN =
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        private static void ClearBoard(Board board)
        {
            Array.Clear(board.Squares);
            Array.Clear(board.PiecesBitboards);
            Array.Clear(board.ColoredBitboards);
        }

        /// <summary>
        /// Loads a chess position from a FEN string into the specified board.
        /// </summary>
        public static void LoadPositionFromFEN(string fen, Board board)
        {
            ClearBoard(board);

            string[] fenParts = fen.Split(' ');

            // Piece placement starts on rank 8.
            int rank = 7;
            int file = 0;

            foreach (char symbol in fenParts[0])
            {
                if (symbol == '/')
                {
                    file = 0;
                    rank--;
                }
                else if (char.IsDigit(symbol))
                {
                    file += symbol - '0';
                }
                else
                {
                    int piece = Piece.FenSymbolToPiece(symbol);
                    int square = rank * 8 + file;

                    int color = char.IsUpper(symbol)
                        ? Piece.White
                        : Piece.Black;

                    int bitboardIndex = BitboardHelper.GetBitboardIndex(
                        Piece.PieceType(piece),
                        color);

                    ref ulong pieceBitboard = ref board.PiecesBitboards[bitboardIndex];

                    // Update all board representations.
                    BitboardHelper.ToggleBit(ref pieceBitboard, square);
                    BitboardHelper.ToggleBit(ref board.ColoredBitboards[color], square);
                    BitboardHelper.ToggleBit(ref board.ColoredBitboards[2], square);

                    board.Squares[square] = piece;
                    file++;
                }
            }

            // Update Side to move.
            board.ColorToMove = fenParts[1] == "w"
                ? Piece.White
                : Piece.Black;

            // Parse castling rights.
            CastlingRights rights = CastlingRights.None;

            foreach (char symbol in fenParts[2])
            {
                rights |= symbol switch
                {
                    'K' => CastlingRights.WhiteKingside,
                    'Q' => CastlingRights.WhiteQueenside,
                    'k' => CastlingRights.BlackKingside,
                    'q' => CastlingRights.BlackQueenside,
                    _ => 0
                };
            }

            // Parse the en passant target square.
            board.EnPassantSquare = fenParts[3] == "-"
                ? -1
                : Notation.SquareToIndex(fenParts[3]);

            int halfmoveClock = int.Parse(fenParts[4]);
            int fullmoveNumber = int.Parse(fenParts[5]);

            board.PlyCountSinceCaptureOrPawnMove = halfmoveClock;

            // Convert the FEN fullmove number to the engine's ply count.
            board.PlyCount = board.IsWhiteToMove
                ? (fullmoveNumber - 1) * 2
                : (fullmoveNumber - 1) * 2 + 1;

            // Update the game state.
            board.CurrentGameState = new GameState(
                Piece.None,
                Piece.None,
                board.EnPassantSquare,
                rights,
                board.PlyCount,
                board.PlyCountSinceCaptureOrPawnMove);
        }
    }
}