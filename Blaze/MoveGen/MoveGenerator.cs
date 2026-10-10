using Blaze.Helpers;
using System.Numerics;

namespace Blaze.MoveGen
{
    /// <summary>
    /// Generates legal moves for a position using bitboards: precomputed tables for knights, kings,
    /// and pawns, and magic bitboards (see <see cref="Magic"/>) for sliding pieces. Checks, pins, and
    /// en passant edge cases are resolved directly against bitboard masks rather than by generating
    /// pseudo-legal moves and filtering them afterward.
    /// </summary>
    public static class MoveGenerator
    {
        private static int CurrentMoveIndex;
        private static bool IsInCheck;
        private static bool IsInDoubleCheck;

        /// <summary>
        /// Squares that resolve the current check: the checking piece's square and, for a sliding
        /// check, the ray between it and the king. Equal to <see cref="ulong.MaxValue"/> when not in check.
        /// </summary>
        private static ulong CheckRayMask;

        private static ulong OrthogonalPinMask;         // Friendly pieces pinned along a rank or file.
        private static ulong OrthogonalPinMovementMask; // Squares an orthogonally pinned piece may still move to.
        private static ulong DiagonalPinMask;            // Friendly pieces pinned along a diagonal.
        private static ulong DiagonalPinMovementMask;    // Squares a diagonally pinned piece may still move to.

        public static ulong EnemyAttacks;
        private static ulong MovementMask; // Restricts generation to captures only, when requested.
        private static ulong ComputedHash; // Position hash that EnemyAttacks/pin data was last computed for.
        private static int FriendlyKingSquare;

        // Precomputed attacks for knights, kings, and pawns, indexed by square - avoids recalculating
        // these on the fly. Sliding pieces (bishops, rooks, queens) use magic bitboards instead (see Magic.cs).

        /// <summary>Precomputed knight attack bitboard for each square.</summary>
        public static ulong[] KnightAttacks = new ulong[64];

        /// <summary>Precomputed king attack bitboard for each square.</summary>
        public static ulong[] KingAttacks = new ulong[64];

        /// <summary>Precomputed white pawn attack bitboard for each square.</summary>
        public static ulong[] WhitePawnsAttacks = new ulong[64];

        /// <summary>Precomputed black pawn attack bitboard for each square.</summary>
        public static ulong[] BlackPawnsAttacks = new ulong[64];

        /// <summary>
        /// Generates all legal moves for the current position and writes them into <paramref name="legalMoves"/>.
        /// Uses the precomputed attack tables above and magic bitboards for sliding pieces.
        /// </summary>
        /// <param name="board">The position to generate moves for.</param>
        /// <param name="legalMoves">Buffer the generated moves are written into.</param>
        /// <param name="lookAtCapturesOnly">When true, only captures are generated.</param>
        /// <returns>The number of moves written into <paramref name="legalMoves"/>.</returns>
        /// <remarks>Should only be called via Board's move generation entry point, which owns the move buffer.</remarks>
        public static int GenerateLegalMoves(Board board, Span<Move> legalMoves, bool lookAtCapturesOnly)
        {
            Init(board);

            if (lookAtCapturesOnly)
                MovementMask = board.ColoredBitboards[board.ColorToMove ^ 1];

            GetLegalKingMoves(board, ref legalMoves);
            if (!IsInDoubleCheck)
            {
                GetLegalPawnMoves(board, ref legalMoves);
                GetLegalKnightMoves(board, ref legalMoves);
                GetLegalSlidingMoves(board, ref legalMoves);
            }

            return CurrentMoveIndex;
        }

        private static void GetLegalPawnMoves(Board board, ref Span<Move> legalMoves)
        {
            ulong pawns = board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Pawn, board.ColorToMove)];

            int pushDir = board.ColorToMove == Piece.White ? 8 : -8;

            ulong promotionRank = board.ColorToMove == Piece.White
                ? 0xFF00000000000000
                : 0x00000000000000FF;

            ulong notPromotionRank = ~promotionRank;

            // Squares one step ahead must be empty.
            ulong pawnsOneSquarePush =
                BitboardHelper.ShiftBitboard(pawns & ~DiagonalPinMask, pushDir)
                & ~board.ColoredBitboards[2];

            // Squares two steps ahead must also be empty, and pawns can only double-push from their starting rank.
            ulong pawnsTwoSquaresPush =
                BitboardHelper.ShiftBitboard(pawnsOneSquarePush, pushDir)
                & ~board.ColoredBitboards[2]
                & CheckRayMask;

            pawnsTwoSquaresPush &=
                (ulong)(board.ColorToMove == Piece.White
                    ? 0x00000000FF000000
                    : 0x000000FF00000000)
                & MovementMask;

            // Captures to the right (toward the h-file); must land on an opponent piece.
            ulong rightCaptures =
                BitboardHelper.ShiftBitboard(
                    (pawns & ~OrthogonalPinMask & PrecomputeMoveData.NotHFile),
                    pushDir + 1)
                & board.ColoredBitboards[board.ColorToMove ^ 1];

            // Captures to the left (toward the a-file); must land on an opponent piece.
            ulong leftCaptures =
                BitboardHelper.ShiftBitboard(
                    (pawns & ~OrthogonalPinMask & PrecomputeMoveData.NotAFile),
                    pushDir - 1)
                & board.ColoredBitboards[board.ColorToMove ^ 1];

            // Split each set into promotion vs non-promotion targets so promotions can be expanded below.
            ulong pushPromos =
                pawnsOneSquarePush & promotionRank & CheckRayMask & MovementMask;

            ulong pushNormal =
                pawnsOneSquarePush & notPromotionRank & CheckRayMask & MovementMask;

            ulong rightCapturePromos =
                rightCaptures & promotionRank & CheckRayMask;

            ulong rightCaptureNormal =
                rightCaptures & notPromotionRank & CheckRayMask;

            ulong leftCapturePromos =
                leftCaptures & promotionRank & CheckRayMask;

            ulong leftCaptureNormal =
                leftCaptures & notPromotionRank & CheckRayMask;

            #region Normal Pushes

            while (pushNormal != 0)
            {
                int targetSquare = BitboardHelper.PopLSB(ref pushNormal);
                int startingSquare = targetSquare - pushDir;

                // An orthogonally pinned pawn may only push along its pin ray.
                if ((OrthogonalPinMask & 1ul << startingSquare) != 0
                    && (OrthogonalPinMovementMask & 1ul << targetSquare) == 0)
                    continue;

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare);
            }

            #endregion

            #region Double Pushes

            while (pawnsTwoSquaresPush != 0)
            {
                int targetSquare = BitboardHelper.PopLSB(ref pawnsTwoSquaresPush);
                int startingSquare = targetSquare - 2 * pushDir;

                if ((OrthogonalPinMask & 1ul << startingSquare) != 0
                    && (OrthogonalPinMovementMask & 1ul << targetSquare) == 0)
                    continue;

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.DoublePawnPush);
            }

            #endregion

            #region Normal Captures

            while (rightCaptureNormal != 0)
            {
                int targetSquare = BitboardHelper.PopLSB(ref rightCaptureNormal);
                int startingSquare = targetSquare - (pushDir + 1);

                if ((DiagonalPinMask & 1ul << startingSquare) != 0
                    && (DiagonalPinMovementMask & 1ul << targetSquare) == 0)
                    continue;

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare);
            }

            while (leftCaptureNormal != 0)
            {
                int targetSquare = BitboardHelper.PopLSB(ref leftCaptureNormal);
                int startingSquare = targetSquare - (pushDir - 1);

                if ((DiagonalPinMask & 1ul << startingSquare) != 0
                    && (DiagonalPinMovementMask & 1ul << targetSquare) == 0)
                    continue;

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare);
            }

            #endregion

            #region Promotions

            while (pushPromos != 0)
            {
                int targetSquare = BitboardHelper.PopLSB(ref pushPromos);
                int startingSquare = targetSquare - pushDir;

                if ((OrthogonalPinMask & 1ul << startingSquare) != 0
                    && (OrthogonalPinMovementMask & 1ul << targetSquare) == 0)
                    continue;

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.QueenPromotion);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.RookPromotion);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.BishopPromotion);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.KnightPromotion);
            }

            while (rightCapturePromos != 0)
            {
                int targetSquare = BitboardHelper.PopLSB(ref rightCapturePromos);
                int startingSquare = targetSquare - (pushDir + 1);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.QueenPromotion);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.RookPromotion);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.BishopPromotion);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.KnightPromotion);
            }

            while (leftCapturePromos != 0)
            {
                int targetSquare = BitboardHelper.PopLSB(ref leftCapturePromos);
                int startingSquare = targetSquare - (pushDir - 1);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.QueenPromotion);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.RookPromotion);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.BishopPromotion);

                legalMoves[CurrentMoveIndex++] =
                    new Move(startingSquare, targetSquare, MoveFlag.KnightPromotion);
            }

            #endregion

            #region En Passant

            if (board.EnPassantSquare != -1)
            {
                ulong epSquareBit = 1ul << board.EnPassantSquare;
                ulong capturedPawnBit = 1ul << (board.EnPassantSquare - pushDir); // the pawn being captured

                // En passant only resolves a check if it blocks the check ray or captures the checking pawn.
                bool epResolvesCheck =
                    (epSquareBit & CheckRayMask) != 0
                    || (capturedPawnBit & CheckRayMask) != 0;

                // Skip en passant entirely if we're in check and it wouldn't resolve the check.
                if (!epResolvesCheck && CheckRayMask != ulong.MaxValue)
                    return;

                // Candidate pawns that can capture en passant, from the right and left.
                ulong epRightCapture =
                    BitboardHelper.ShiftBitboard(
                        pawns & ~OrthogonalPinMask & PrecomputeMoveData.NotHFile,
                        pushDir + 1)
                    & epSquareBit;

                ulong epLeftCapture =
                    BitboardHelper.ShiftBitboard(
                        pawns & ~OrthogonalPinMask & PrecomputeMoveData.NotAFile,
                        pushDir - 1)
                    & epSquareBit;

                if (epRightCapture != 0)
                {
                    int startingSquare = board.EnPassantSquare - (pushDir + 1);

                    // Diagonal pin: the capturing pawn can only move along its pin ray.
                    if ((DiagonalPinMask & 1ul << startingSquare) != 0
                        && (DiagonalPinMovementMask & epSquareBit) == 0)
                    {
                        epRightCapture = 0;
                    }

                    // Horizontal pin: removing both pawns could expose the king to a rook/queen on the rank.
                    if (epRightCapture != 0
                        && IsHorizontalPinned(board, startingSquare, board.EnPassantSquare - pushDir))
                    {
                        epRightCapture = 0;
                    }

                    if (epRightCapture != 0)
                    {
                        legalMoves[CurrentMoveIndex++] =
                            new Move(startingSquare, board.EnPassantSquare, MoveFlag.EnPassant);
                    }
                }

                if (epLeftCapture != 0)
                {
                    int startingSquare = board.EnPassantSquare - (pushDir - 1);

                    // Diagonal pin: the capturing pawn can only move along its pin ray.
                    if ((DiagonalPinMask & 1ul << startingSquare) != 0
                        && (DiagonalPinMovementMask & epSquareBit) == 0)
                    {
                        epLeftCapture = 0;
                    }

                    // Horizontal pin: removing both pawns could expose the king to a rook/queen on the rank.
                    if (epLeftCapture != 0
                        && IsHorizontalPinned(board, startingSquare, board.EnPassantSquare - pushDir))
                    {
                        epLeftCapture = 0;
                    }

                    if (epLeftCapture != 0)
                    {
                        legalMoves[CurrentMoveIndex++] =
                            new Move(startingSquare, board.EnPassantSquare, MoveFlag.EnPassant);
                    }
                }
            }

            #endregion
        }

        private static void GetLegalKnightMoves(Board board, ref Span<Move> legalMoves)
        {
            ulong friendlyPieces = board.ColoredBitboards[board.ColorToMove];

            ulong moveMask = ~friendlyPieces & CheckRayMask & MovementMask;

            // A pinned knight can never move without exposing the king, so pinned knights are excluded entirely.
            ulong knights = board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Knight, board.ColorToMove)]
                & ~OrthogonalPinMask & ~DiagonalPinMask;

            while (knights != 0)
            {
                int startingSquare = BitboardHelper.PopLSB(ref knights);

                ulong moves = KnightAttacks[startingSquare] & moveMask;

                while (moves != 0)
                {
                    legalMoves[CurrentMoveIndex++] =
                        new Move(startingSquare, BitboardHelper.PopLSB(ref moves));
                }
            }
        }

        private static void GetLegalKingMoves(Board board, ref Span<Move> legalMoves)
        {
            //TODO remove when fixed bug
            if (board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.King, board.ColorToMove)] == 0)
                return;

            ulong friendlyPieces = board.ColoredBitboards[board.ColorToMove];

            ulong kingMoves =
                KingAttacks[FriendlyKingSquare] & ~friendlyPieces & ~EnemyAttacks & MovementMask;

            while (kingMoves != 0)
            {
                legalMoves[CurrentMoveIndex++] = new Move(FriendlyKingSquare, BitboardHelper.PopLSB(ref kingMoves));
            }

            if (IsInCheck || MovementMask != ulong.MaxValue)
                return;

            ulong occupied = board.ColoredBitboards[2];

            if (board.CurrentGameState.HasCastleRight(board.ColorToMove, true))
            {
                // f- and g-file squares must be empty and not attacked.
                ulong castleSquares =
                    (1UL << (FriendlyKingSquare + 1)) |
                    (1UL << (FriendlyKingSquare + 2));

                if (((EnemyAttacks | occupied) & castleSquares) == 0)
                {
                    legalMoves[CurrentMoveIndex++] =
                        new Move(
                            FriendlyKingSquare,
                            FriendlyKingSquare + 2,
                            MoveFlag.CastleShort);
                }
            }

            if (board.CurrentGameState.HasCastleRight(board.ColorToMove, false))
            {
                // d- and c-file squares must be empty and not attacked; the b-file square only needs to be empty.
                ulong kingPath =
                    (1UL << (FriendlyKingSquare - 1)) |
                    (1UL << (FriendlyKingSquare - 2));

                ulong emptySquares =
                    kingPath |
                    (1UL << (FriendlyKingSquare - 3));

                if ((EnemyAttacks & kingPath) == 0 &&
                    (occupied & emptySquares) == 0)
                {
                    legalMoves[CurrentMoveIndex++] =
                        new Move(
                            FriendlyKingSquare,
                            FriendlyKingSquare - 2,
                            MoveFlag.CastleLong);
                }
            }
        }

        private static void GetLegalSlidingMoves(Board board, ref Span<Move> legalMoves)
        {
            ulong occupied = board.ColoredBitboards[2];
            ulong friendlyPieces = board.ColoredBitboards[board.ColorToMove];

            ulong moveMask =
                ~friendlyPieces &
                CheckRayMask &
                MovementMask;

            ulong orthogonalSliders =
                (board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Rook, board.ColorToMove)] |
                 board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Queen, board.ColorToMove)])
                & ~DiagonalPinMask;

            ulong diagonalSliders =
                (board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Bishop, board.ColorToMove)] |
                 board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Queen, board.ColorToMove)])
                & ~OrthogonalPinMask;

            ulong orthogonalPinned = orthogonalSliders & OrthogonalPinMask;
            ulong orthogonalFree = orthogonalSliders & ~OrthogonalPinMask;

            ulong diagonalPinned = diagonalSliders & DiagonalPinMask;
            ulong diagonalFree = diagonalSliders & ~DiagonalPinMask;

            while (orthogonalFree != 0)
            {
                int startingSquare = BitboardHelper.PopLSB(ref orthogonalFree);

                ulong moves =
                    Magic.GetRookAttacks(startingSquare, occupied) &
                    moveMask;

                while (moves != 0)
                {
                    legalMoves[CurrentMoveIndex++] =
                        new Move(startingSquare, BitboardHelper.PopLSB(ref moves));
                }
            }

            while (orthogonalPinned != 0)
            {
                int startingSquare = BitboardHelper.PopLSB(ref orthogonalPinned);

                ulong moves =
                    Magic.GetRookAttacks(startingSquare, occupied) &
                    moveMask &
                    OrthogonalPinMovementMask;

                while (moves != 0)
                {
                    legalMoves[CurrentMoveIndex++] =
                        new Move(startingSquare, BitboardHelper.PopLSB(ref moves));
                }
            }

            while (diagonalFree != 0)
            {
                int startingSquare = BitboardHelper.PopLSB(ref diagonalFree);

                ulong moves =
                    Magic.GetBishopAttacks(startingSquare, occupied) &
                    moveMask;

                while (moves != 0)
                {
                    legalMoves[CurrentMoveIndex++] =
                        new Move(startingSquare, BitboardHelper.PopLSB(ref moves));
                }
            }

            while (diagonalPinned != 0)
            {
                int startingSquare = BitboardHelper.PopLSB(ref diagonalPinned);

                ulong moves =
                    Magic.GetBishopAttacks(startingSquare, occupied) &
                    moveMask &
                    DiagonalPinMovementMask;

                while (moves != 0)
                {
                    legalMoves[CurrentMoveIndex++] =
                        new Move(startingSquare, BitboardHelper.PopLSB(ref moves));
                }
            }
        }

        private static bool IsHorizontalPinned(Board board, int capturingPawnSquare, int capturedPawnSquare)
        {
            int kingRank = FriendlyKingSquare >> 3;

            // Only relevant if the king is on the same rank as both pawns.
            if (kingRank != capturingPawnSquare >> 3)
                return false;

            // Simulate the position after both pawns are removed from the board.
            ulong occupancyAfterEp = board.ColoredBitboards[2]
                ^ (1ul << capturingPawnSquare)
                ^ (1ul << capturedPawnSquare);

            ulong rankAttacksFromKing = Magic.GetRookAttacks(FriendlyKingSquare, occupancyAfterEp)
                & (0xFFul << (kingRank << 3)); // restrict to the king's rank

            ulong enemyRooksAndQueens =
                board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Rook, board.ColorToMove ^ 1)] |
                board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Queen, board.ColorToMove ^ 1)];

            return (rankAttacksFromKing & enemyRooksAndQueens) != 0;
        }

        private static void Init(Board board)
        {
            CurrentMoveIndex = 0;
            IsInCheck = IsInDoubleCheck = false;
            CheckRayMask = 0;
            OrthogonalPinMask = 0;
            OrthogonalPinMovementMask = 0;
            DiagonalPinMask = 0;
            DiagonalPinMovementMask = 0;
            MovementMask = ulong.MaxValue;
            FriendlyKingSquare = Math.Min(63, BitOperations.TrailingZeroCount(board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.King, board.ColorToMove)]));
            EnemyAttacks = GetEnemyAttacks(board);
            ComputedHash = board.CurrentHash;
        }

        /// <summary>
        /// Returns whether the side to move is currently in check. Uses a cached result when the
        /// position hash matches the last computed one, and recomputes enemy attack data otherwise.
        /// </summary>
        public static bool InCheck(Board board)
        {
            if (ComputedHash != board.CurrentHash)
            {
                // Position changed since the last computation - recompute enemy attacks and check state.
                GetEnemyAttacks(board);
                return IsInCheck;
            }
            return IsInCheck;
        }

        /// <summary>
        /// Computes the set of squares attacked by the enemy side. As a side effect, also updates
        /// IsInCheck, IsInDoubleCheck, CheckRayMask, and the orthogonal/diagonal pin masks for the current position.
        /// </summary>
        public static ulong GetEnemyAttacks(Board board)
        {
            ulong attacks = 0;

            int enemyColor = board.ColorToMove ^ 1;

            ulong kingBit = 1UL << FriendlyKingSquare;
            ulong occupied = board.ColoredBitboards[2];

            int checkerCount = 0;

            ulong pawns = board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Pawn, enemyColor)];
            ulong knights = board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Knight, enemyColor)];
            ulong bishops = board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Bishop, enemyColor)]
                          | board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Queen, enemyColor)];
            ulong rooks = board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Rook, enemyColor)]
                        | board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.Queen, enemyColor)];
            ulong king = board.PiecesBitboards[BitboardHelper.GetBitboardIndex(Piece.King, enemyColor)];

            ulong pawnCheckers = board.IsWhiteToMove
                ? WhitePawnsAttacks[FriendlyKingSquare] & pawns
                : BlackPawnsAttacks[FriendlyKingSquare] & pawns;

            checkerCount += BitOperations.PopCount(pawnCheckers);
            CheckRayMask |= pawnCheckers;

            attacks |= board.IsWhiteToMove
                ? ((pawns & PrecomputeMoveData.NotAFile) >> 9)
                | ((pawns & PrecomputeMoveData.NotHFile) >> 7)
                : ((pawns & PrecomputeMoveData.NotAFile) << 7)
                | ((pawns & PrecomputeMoveData.NotHFile) << 9);

            ulong knightCheckers = KnightAttacks[FriendlyKingSquare] & knights;

            checkerCount += BitOperations.PopCount(knightCheckers);
            CheckRayMask |= knightCheckers;

            while (knights != 0)
            {
                attacks |= KnightAttacks[BitboardHelper.PopLSB(ref knights)];
            }

            ulong blockers = occupied ^ kingBit;

            ulong bishopAttacks;
            ulong bishopTunnel;

            while (bishops != 0)
            {
                int square = BitboardHelper.PopLSB(ref bishops);

                bishopAttacks = Magic.GetBishopAttacks(square, blockers);
                bishopTunnel = BoardHelper.GetAttackTunnel(square, FriendlyKingSquare, false);

                if ((bishopAttacks & kingBit) != 0)
                {
                    checkerCount++;

                    CheckRayMask |= 1UL << square;
                    CheckRayMask |= bishopTunnel;
                }

                ulong diagonalPin = bishopTunnel & occupied;

                if (BitOperations.PopCount(diagonalPin) == 1)
                {
                    DiagonalPinMask |= diagonalPin;
                    DiagonalPinMovementMask |= bishopTunnel;
                    DiagonalPinMovementMask |= 1UL << square;
                }

                attacks |= bishopAttacks;
            }

            ulong rookAttacks;
            ulong rookTunnel;

            while (rooks != 0)
            {
                int square = BitboardHelper.PopLSB(ref rooks);

                rookAttacks = Magic.GetRookAttacks(square, blockers);
                rookTunnel = BoardHelper.GetAttackTunnel(square, FriendlyKingSquare, true);

                if ((rookAttacks & kingBit) != 0)
                {
                    checkerCount++;

                    CheckRayMask |= 1UL << square;
                    CheckRayMask |= rookTunnel;
                }

                ulong orthogonalPin = rookTunnel & occupied;

                if (BitOperations.PopCount(orthogonalPin) == 1)
                {
                    OrthogonalPinMask |= orthogonalPin;
                    OrthogonalPinMovementMask |= rookTunnel;
                    OrthogonalPinMovementMask |= 1UL << square;
                }

                attacks |= rookAttacks;
            }

            //TODO fix bug that sometime king don't exist? king bitboard is 0. faced during null move adding
            if (king != 0)
                attacks |= KingAttacks[BitOperations.TrailingZeroCount(king)];

            IsInCheck = checkerCount != 0;
            IsInDoubleCheck = checkerCount > 1;

            if (CheckRayMask == 0)
                CheckRayMask = ulong.MaxValue;

            return attacks;
        }

        /// <summary>
        /// Recursively counts the number of leaf positions reachable from the current position at the
        /// given depth. Used to verify move generator correctness and measure its speed.
        /// </summary>
        public static int Perft(Board board, int depth)
        {
            Span<Move> moves = stackalloc Move[218]; // 218 is the most legal moves possible in any position

            int moveCount = board.GetLegalMoves(moves);

            if (depth == 1)
                return moveCount;

            int nodes = 0;

            for (int i = 0; i < moveCount; i++)
            {
                Move move = moves[i];
                board.MakeMove(move);
                nodes += Perft(board, depth - 1);
                board.UndoMove(move);
            }

            return nodes;
        }
    }
}