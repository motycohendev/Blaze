using Blaze.Helpers;
using Blaze.MoveGen;
using System.Numerics;

namespace Blaze
{
    public class Board
    {
        // Piece bitboards indexed by piece type and color.
        public ulong[] PiecesBitboards = new ulong[12];

        // Occupancy bitboards:
        // [0] White, [1] Black, [2] All pieces.
        public ulong[] ColoredBitboards = new ulong[3];

        // Mailbox board representation.
        public int[] Squares = new int[64];

        // 0 for White, 1 for Black.
        public int ColorToMove;
        public bool IsWhiteToMove => ColorToMove == 0;

        public ulong CurrentHash;
        public GameState CurrentGameState;

        // Used for undoing moves and repetition detection.
        private ulong[] hashHistory = new ulong[1024];
        private GameState[] gameStateHistory = new GameState[1024];
        public Move[] MoveHistory = new Move[1024];

        // En passant target square, or -1 if none exists.
        public int EnPassantSquare = -1;

        public int PlyCount = 0;
        public int PlyCountSinceCaptureOrPawnMove = 0;

        public Board(string fen = FEN.StartingPositionFEN)
        {
            FEN.LoadPositionFromFEN(fen, this);
            CurrentHash = Zobrist.ComputeHash(this);
        }

        public bool IsInCheck()
        {
            return MoveGenerator.InCheck(this);
        }

        private bool IsDrawByThreefoldRepetition()
        {
            int count = 0;

            // Only positions since the last irreversible move.
            int earliest = Math.Max(0, PlyCount - PlyCountSinceCaptureOrPawnMove);

            for (int i = PlyCount - 2; i >= earliest; i -= 2)
            {
                if (hashHistory[i] == CurrentHash && ++count >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsDrawByInsufficientMaterial()
        {
            // A draw by insufficient material occurs when only the two kings remain,
            // possibly with a single minor piece, since checkmate cannot be forced.
            int numPieces = BitOperations.PopCount(ColoredBitboards[2]);

            ulong bishopsBitboard = PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Bishop, true)] | PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Bishop, false)];
            ulong knightsBitboard = PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Knight, true)] | PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Knight, false)];

            int numBishops = BitOperations.PopCount(bishopsBitboard);
            int numKnights = BitOperations.PopCount(knightsBitboard);

            // Two bishops or two knights can still force checkmate in some positions.
            if (numBishops >= 2 || numKnights >= 2)
            {
                return false;
            }

            // With those cases excluded, exactly two remaining pieces must be the two kings.
            return (numPieces - numBishops - numKnights) == 2;
        }

        public bool IsDraw()
        {
            // 100 plies without a capture or pawn move triggers the fifty-move rule.
            return PlyCountSinceCaptureOrPawnMove >= 100 || IsDrawByThreefoldRepetition() || IsDrawByInsufficientMaterial();
        }

        public int GetLegalMoves(Span<Move> moves, bool capturesOnly = false)
        {
            return MoveGenerator.GenerateLegalMoves(this, moves, capturesOnly);
        }

        public void MakeMove(Move move)
        {
                int movingPiece = Squares[move.StartingSquare];
                int movingPieceType = Piece.PieceType(movingPiece);
                int movingBitboardIndex = BitboardHelper.GetBitboardIndex(movingPieceType, ColorToMove);
                ref ulong movingBitboard = ref PiecesBitboards[movingBitboardIndex];
                ref ulong coloredBitboard = ref ColoredBitboards[ColorToMove];
                int capturedPiece = Squares[move.TargetSquare];

                hashHistory[PlyCount] = CurrentHash;
                MoveHistory[PlyCount] = move;

                int prevEnPassantSquare = EnPassantSquare;
                int prevPlyCountSinceCaptureOrPawnMove = PlyCountSinceCaptureOrPawnMove;
                int prevPlyCount = PlyCount;

                Squares[move.StartingSquare] = Piece.None;
                Squares[move.TargetSquare] = movingPiece;
                EnPassantSquare = -1;

                BitboardHelper.MovePiece(ref movingBitboard, move.StartingSquare, move.TargetSquare);
                BitboardHelper.MovePiece(ref coloredBitboard, move.StartingSquare, move.TargetSquare);

                CurrentHash ^= Zobrist.PieceKey(movingBitboardIndex, move.StartingSquare);
                CurrentHash ^= Zobrist.PieceKey(movingBitboardIndex, move.TargetSquare);

                CastlingRights newCastlingRights = CurrentGameState.CastlingRights;

                PlyCountSinceCaptureOrPawnMove++;
                if (movingPieceType == Piece.Pawn)
                {
                    PlyCountSinceCaptureOrPawnMove = 0;
                }
                // A king move permanently removes both castling rights.
                else if (movingPieceType == Piece.King)
                {
                    newCastlingRights &= (CastlingRights)~(3 << (ColorToMove * 2));
                }

                // Moving from or capturing on a rook home square removes
                // the associated castling right.
                newCastlingRights = UpdateCastlingRightsForSquare(newCastlingRights, move.StartingSquare);
                newCastlingRights = UpdateCastlingRightsForSquare(newCastlingRights, move.TargetSquare);

                CurrentHash ^= Zobrist.SideToMoveKey;

                // Remove the previous en passant and castling rights contributions; the
                // updated values are folded back in once they're known, further down.
                if (prevEnPassantSquare >= 0)
                {
                    CurrentHash ^= Zobrist.EnPassantKey(prevEnPassantSquare & 7);
                }

                CurrentHash ^= Zobrist.CastlingKey((int)CurrentGameState.CastlingRights);

                if (capturedPiece != Piece.None)
                {
                    int capturedPieceType = Piece.PieceType(capturedPiece);
                    int capturedBitboardIndex = BitboardHelper.GetBitboardIndex(capturedPieceType, ColorToMove ^ 1);
                    ref ulong capturedBitboard = ref PiecesBitboards[capturedBitboardIndex];

                    PlyCountSinceCaptureOrPawnMove = 0;

                    BitboardHelper.ToggleBit(ref capturedBitboard, move.TargetSquare);
                    BitboardHelper.ToggleBit(ref ColoredBitboards[ColorToMove ^ 1], move.TargetSquare);

                    CurrentHash ^= Zobrist.PieceKey(capturedBitboardIndex, move.TargetSquare);
                }

                if (move.IsPromotion)
                {
                    int promotionPieceType = move.Flag;
                    int promotionBitboardIndex = BitboardHelper.GetBitboardIndex(promotionPieceType, ColorToMove);
                    ref ulong promotionBitboard = ref PiecesBitboards[promotionBitboardIndex];

                    // Replace the pawn with the promoted piece.
                    BitboardHelper.ToggleBit(ref movingBitboard, move.TargetSquare);
                    BitboardHelper.ToggleBit(ref promotionBitboard, move.TargetSquare);
                    Squares[move.TargetSquare] = Piece.Create(promotionPieceType, ColorToMove);

                    CurrentHash ^= Zobrist.PieceKey(movingBitboardIndex, move.TargetSquare);
                    CurrentHash ^= Zobrist.PieceKey(promotionBitboardIndex, move.TargetSquare);
                }
                // A double pawn push creates an en passant target square.
                else if (move.Flag == MoveFlag.DoublePawnPush)
                {
                    EnPassantSquare = move.TargetSquare - 8 + (16 * ColorToMove);
                }
                else if (move.IsEnPassant)
                {
                    int capturedPawnSquare = move.TargetSquare - 8 + (16 * ColorToMove);
                    int capturedPawnIndex = BitboardHelper.GetBitboardIndex(Piece.Pawn, ColorToMove ^ 1);
                    ref ulong capturedPawnBitboard = ref PiecesBitboards[capturedPawnIndex];

                    Squares[capturedPawnSquare] = Piece.None;
                    BitboardHelper.ToggleBit(ref capturedPawnBitboard, capturedPawnSquare);
                    BitboardHelper.ToggleBit(ref ColoredBitboards[ColorToMove ^ 1], capturedPawnSquare);

                    CurrentHash ^= Zobrist.PieceKey(capturedPawnIndex, capturedPawnSquare);
                }
                else if (move.Flag == MoveFlag.CastleShort)
                {
                    int startingRookSquare = move.TargetSquare + 1;
                    int targetRookSquare = move.TargetSquare - 1;
                    int rookBitboardIndex = BitboardHelper.GetBitboardIndex(Piece.Rook, ColorToMove);
                    ref ulong rookBitboard = ref PiecesBitboards[rookBitboardIndex];

                    BitboardHelper.MovePiece(ref rookBitboard, startingRookSquare, targetRookSquare);
                    BitboardHelper.MovePiece(ref coloredBitboard, startingRookSquare, targetRookSquare);
                    Squares[targetRookSquare] = Squares[startingRookSquare];
                    Squares[startingRookSquare] = Piece.None;

                    CurrentHash ^= Zobrist.PieceKey(rookBitboardIndex, startingRookSquare);
                    CurrentHash ^= Zobrist.PieceKey(rookBitboardIndex, targetRookSquare);
                }
                else if (move.Flag == MoveFlag.CastleLong)
                {
                    int startingRookSquare = move.TargetSquare - 2;
                    int targetRookSquare = move.TargetSquare + 1;
                    int rookBitboardIndex = BitboardHelper.GetBitboardIndex(Piece.Rook, ColorToMove);
                    ref ulong rookBitboard = ref PiecesBitboards[rookBitboardIndex];

                    BitboardHelper.MovePiece(ref rookBitboard, startingRookSquare, targetRookSquare);
                    BitboardHelper.MovePiece(ref coloredBitboard, startingRookSquare, targetRookSquare);
                    Squares[targetRookSquare] = Squares[startingRookSquare];
                    Squares[startingRookSquare] = Piece.None;

                    CurrentHash ^= Zobrist.PieceKey(rookBitboardIndex, startingRookSquare);
                    CurrentHash ^= Zobrist.PieceKey(rookBitboardIndex, targetRookSquare);
                }

                ColorToMove ^= 1;

                // Save the current state so the move can be undone.
                gameStateHistory[PlyCount] = CurrentGameState;
                CurrentGameState = new GameState(movingPiece, capturedPiece, prevEnPassantSquare, newCastlingRights, prevPlyCount, prevPlyCountSinceCaptureOrPawnMove);

                CurrentHash ^= Zobrist.CastlingKey((int)newCastlingRights);

                if (EnPassantSquare >= 0)
                {
                    CurrentHash ^= Zobrist.EnPassantKey(EnPassantSquare & 7);
                }

                PlyCount++;

                // Recompute combined occupancy after the move.
                ColoredBitboards[2] = ColoredBitboards[0] | ColoredBitboards[1];
            }

        public void UndoMove(Move move)
        {
            int otherColor = ColorToMove ^ 1;

            int movingPiece = CurrentGameState.MovingPiece;
            int movingPieceType = Piece.PieceType(movingPiece);
            int movingBitboardIndex = BitboardHelper.GetBitboardIndex(movingPieceType, otherColor);
            ref ulong movingBitboard = ref PiecesBitboards[movingBitboardIndex];
            int capturedPiece = CurrentGameState.CapturedPiece;

            Squares[move.StartingSquare] = movingPiece;
            Squares[move.TargetSquare] = capturedPiece;

            BitboardHelper.MovePiece(ref movingBitboard, move.StartingSquare, move.TargetSquare);
            BitboardHelper.MovePiece(ref ColoredBitboards[otherColor], move.StartingSquare, move.TargetSquare);

            if (capturedPiece != Piece.None)
            {
                int capturedPieceType = Piece.PieceType(capturedPiece);
                int capturedBitboardIndex = BitboardHelper.GetBitboardIndex(capturedPieceType, ColorToMove);
                ref ulong capturedBitboard = ref PiecesBitboards[capturedBitboardIndex];

                BitboardHelper.ToggleBit(ref capturedBitboard, move.TargetSquare);
                BitboardHelper.ToggleBit(ref ColoredBitboards[ColorToMove], move.TargetSquare);
            }
            if (move.IsPromotion)
            {
                int promotionPieceType = move.Flag;
                int promotionBitboardIndex = BitboardHelper.GetBitboardIndex(promotionPieceType, otherColor);
                ref ulong promotionBitboard = ref PiecesBitboards[promotionBitboardIndex];

                BitboardHelper.ToggleBit(ref promotionBitboard, move.TargetSquare);
                BitboardHelper.ToggleBit(ref movingBitboard, move.TargetSquare);
            }
            else if (move.IsEnPassant)
            {
                int capturedPawnSquare = move.TargetSquare - 8 + (16 * otherColor);
                int capturedPawnIndex = BitboardHelper.GetBitboardIndex(Piece.Pawn, ColorToMove);
                ref ulong capturedPawnBitboard = ref PiecesBitboards[capturedPawnIndex];

                Squares[capturedPawnSquare] = Piece.Create(Piece.Pawn, ColorToMove);
                Squares[move.TargetSquare] = Piece.None;

                BitboardHelper.ToggleBit(ref capturedPawnBitboard, capturedPawnSquare);
                BitboardHelper.ToggleBit(ref ColoredBitboards[ColorToMove], capturedPawnSquare);
            }
            else if (move.Flag == MoveFlag.CastleShort)
            {
                int startingRookSquare = move.TargetSquare - 1;
                int targetRookSquare = move.TargetSquare + 1;

                int bitboardIndex = BitboardHelper.GetBitboardIndex(Piece.Rook, otherColor);
                ref ulong rookBitboard = ref PiecesBitboards[bitboardIndex];

                BitboardHelper.MovePiece(ref rookBitboard, startingRookSquare, targetRookSquare);
                BitboardHelper.MovePiece(ref ColoredBitboards[otherColor], startingRookSquare, targetRookSquare);

                Squares[targetRookSquare] = Squares[startingRookSquare];
                Squares[startingRookSquare] = Piece.None;
            }
            else if (move.Flag == MoveFlag.CastleLong)
            {
                int startingRookSquare = move.TargetSquare + 1;
                int targetRookSquare = move.TargetSquare - 2;

                int bitboardIndex = BitboardHelper.GetBitboardIndex(Piece.Rook, otherColor);
                ref ulong rookBitboard = ref PiecesBitboards[bitboardIndex];

                BitboardHelper.MovePiece(ref rookBitboard, startingRookSquare, targetRookSquare);
                BitboardHelper.MovePiece(ref ColoredBitboards[otherColor], startingRookSquare, targetRookSquare);

                Squares[targetRookSquare] = Squares[startingRookSquare];
                Squares[startingRookSquare] = Piece.None;
            }

            ColorToMove ^= 1;
            EnPassantSquare = CurrentGameState.EnPassantSquare;
            PlyCount = CurrentGameState.PlyCount;
            PlyCountSinceCaptureOrPawnMove = CurrentGameState.PlySinceCaptureOrPawnMove;
            CurrentGameState = gameStateHistory[PlyCount];
            CurrentHash = hashHistory[PlyCount];
            ColoredBitboards[2] = ColoredBitboards[0] | ColoredBitboards[1];
        }

        public void MakeNullMove()
        {
            hashHistory[PlyCount] = CurrentHash;
            MoveHistory[PlyCount] = Move.NullMove;

            int prevEnPassantSquare = EnPassantSquare;
            int prevPlyCountSinceCaptureOrPawnMove = PlyCountSinceCaptureOrPawnMove;
            int prevPlyCount = PlyCount;

            // Remove the current en passant square from the hash — it's not
            // carried over, since a null move can't be captured en passant.
            if (prevEnPassantSquare >= 0)
            {
                CurrentHash ^= Zobrist.EnPassantKey(prevEnPassantSquare & 7);
            }

            EnPassantSquare = -1;
            CurrentHash ^= Zobrist.SideToMoveKey;

            ColorToMove ^= 1;
            PlyCountSinceCaptureOrPawnMove++;

            gameStateHistory[PlyCount] = CurrentGameState;
            CurrentGameState = new GameState(
                Piece.None,
                Piece.None,
                prevEnPassantSquare,
                CurrentGameState.CastlingRights,
                prevPlyCount,
                prevPlyCountSinceCaptureOrPawnMove);

            PlyCount++;
        }

        public void UndoNullMove()
        {
            ColorToMove ^= 1;
            EnPassantSquare = CurrentGameState.EnPassantSquare;
            PlyCount = CurrentGameState.PlyCount;
            PlyCountSinceCaptureOrPawnMove = CurrentGameState.PlySinceCaptureOrPawnMove;
            CurrentGameState = gameStateHistory[PlyCount];
            CurrentHash = hashHistory[PlyCount];
        }

        // Returns wether the side to move has more than 1 (kings) pieces other then pawns.
        // Used mainly in null move pruning to avoid misevaluating king and pawn endgames zugzwang positions.
        public bool HasNonPawnMaterial()
        {
            return BitOperations.PopCount(ColoredBitboards[ColorToMove] & ~PiecesBitboards[ColorToMove * 6]) > 1;
        }

        // Removes the castling right associated with a rook home square.
        private static CastlingRights UpdateCastlingRightsForSquare(CastlingRights rights, int square)
        {
            return square switch
            {
                7 => rights & ~CastlingRights.WhiteKingside,
                0 => rights & ~CastlingRights.WhiteQueenside,
                63 => rights & ~CastlingRights.BlackKingside,
                56 => rights & ~CastlingRights.BlackQueenside,
                _ => rights
            };
        }

        /// <summary>
        /// Prints the board to the console.
        /// </summary>
        public static void PrintBoard(Board board)
        {
            for (int rank = 7; rank >= 0; rank--)
            {
                Console.WriteLine(" +---+---+---+---+---+---+---+---+");
                Console.Write(" |");
                for (int file = 0; file < 8; file++)
                {
                    int square = rank * 8 + file;
                    int piece = board.Squares[square];
                    char symbol = Piece.PieceToFenSymbol(piece);
                    Console.Write($" {symbol} |");
                }
                Console.WriteLine($" {rank + 1}");
            }
            Console.WriteLine(" +---+---+---+---+---+---+---+---+");
            Console.WriteLine("   a   b   c   d   e   f   g   h");
        }
    }
}