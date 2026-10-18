using Blaze.Helpers;
using Blaze.MoveGen;

namespace Blaze
{
    /// <summary>
    /// Manages the current game and applies UCI position commands.
    /// </summary>
    public static class MatchManager
    {
        public static Board board = new();

        /// <summary>
        /// Loads a position from a UCI "position" command and
        /// plays any moves that follow it.
        /// </summary>
        public static void LoadPositionCommand(string[] commandTokens)
        {
            if (commandTokens.Length < 2)
                return;

            // The actual moves start after the "moves" token, which is at index 3 for "startpos" command
            int movesIndex = 2;

            if (commandTokens[1] == "startpos")
            {
                board = new Board();
            }
            else if (commandTokens[1] == "fen")
            {
                // The FEN command must have at least 6 parts for the FEN string, plus the "fen" token and the "position" token, making a total of 8 tokens.
                if (commandTokens.Length < 8)
                    return;

                string fen = string.Join(' ', commandTokens, 2, 6);
                board = new Board(fen);

                // The actual moves start after the "moves" token, which is at index 9 for "fen"
                movesIndex = 8;
            }
            else
            {
                return;
            }

            // If there are no moves specified, we can return early. we only needed to load to the position.
            if (commandTokens.Length <= movesIndex ||
                commandTokens[movesIndex] != "moves")
            {
                return;
            }

            for (int i = movesIndex + 1; i < commandTokens.Length; i++)
            {
                Move move = Notation.NotationToMove(commandTokens[i]);

                int pieceType = Piece.PieceType(board.Squares[move.StartingSquare]);

                if (pieceType == Piece.Pawn)
                {
                    // Load double pawn push separately to set the en passant square correctly.
                    if (Math.Abs((move.TargetSquare >> 3) - (move.StartingSquare >> 3)) == 2)
                    {
                        board.MakeMove(new Move(
                            move.StartingSquare,
                            move.TargetSquare,
                            MoveFlag.DoublePawnPush));

                        continue;
                    }

                    // En passant capture.
                    if (move.TargetSquare == board.EnPassantSquare)
                    {
                        board.MakeMove(new Move(
                            move.StartingSquare,
                            move.TargetSquare,
                            MoveFlag.EnPassant));

                        continue;
                    }
                }

                // Load castling moves separately to set the rook's position correctly and update castling rights.
                if (pieceType == Piece.King &&
                    Math.Abs(move.StartingSquare - move.TargetSquare) == 2)
                {
                    board.MakeMove(new Move(
                        move.StartingSquare,
                        move.TargetSquare,
                        move.TargetSquare % 8 == 6
                            ? MoveFlag.CastleShort
                            : MoveFlag.CastleLong));

                    continue;
                }

                board.MakeMove(move);
            }
        }
    }
}