﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static ChessBot.ChessPiece;

namespace ChessBot
{
    /// <summary>
    /// Immutable class representing the state of the chess board.
    /// </summary>
    public class ChessState
    {
        private static ChessState _initial;
        public static ChessState Initial
        {
            get
            {
                if (_initial == null)
                {
                    var pieceMap = new Dictionary<string, ChessPiece>
                    {
                        ["a1"] = WhiteRook,
                        ["b1"] = WhiteKnight,
                        ["c1"] = WhiteBishop,
                        ["d1"] = WhiteQueen,
                        ["e1"] = WhiteKing,
                        ["f1"] = WhiteBishop,
                        ["g1"] = WhiteKnight,
                        ["h1"] = WhiteRook,

                        ["a2"] = WhitePawn,
                        ["b2"] = WhitePawn,
                        ["c2"] = WhitePawn,
                        ["d2"] = WhitePawn,
                        ["e2"] = WhitePawn,
                        ["f2"] = WhitePawn,
                        ["g2"] = WhitePawn,
                        ["h2"] = WhitePawn,

                        ["a7"] = BlackPawn,
                        ["b7"] = BlackPawn,
                        ["c7"] = BlackPawn,
                        ["d7"] = BlackPawn,
                        ["e7"] = BlackPawn,
                        ["f7"] = BlackPawn,
                        ["g7"] = BlackPawn,
                        ["h7"] = BlackPawn,

                        ["a8"] = BlackRook,
                        ["b8"] = BlackKnight,
                        ["c8"] = BlackBishop,
                        ["d8"] = BlackQueen,
                        ["e8"] = BlackKing,
                        ["f8"] = BlackBishop,
                        ["g8"] = BlackKnight,
                        ["h8"] = BlackRook,
                    };
                    _initial = new ChessState(pieceMap);
                }
                return _initial;
            }
        }

        private static ChessTile[,] CreateBoard(IDictionary<string, ChessPiece> pieceMap)
        {
            var board = new ChessTile[8, 8];

            foreach (var (locationString, piece) in pieceMap)
            {
                var (c, r) = BoardLocation.Parse(locationString);
                board[c, r] = new ChessTile((c, r), piece);
            }

            for (int c = 0; c < 8; c++)
            {
                for (int r = 0; r < 8; r++)
                {
                    if (board[c, r] == null)
                    {
                        board[c, r] = new ChessTile((c, r));
                    }
                }
            }
            return board;
        }

        private readonly ChessTile[,] _board; // todo: this should use an immutable array?

        private ChessState(
            ChessTile[,] board,
            PlayerColor nextPlayer,
            bool hasWhiteCastled,
            bool hasBlackCastled)
        {
            _board = board;
            NextPlayer = nextPlayer;
            HasWhiteCastled = hasWhiteCastled;
            HasBlackCastled = hasBlackCastled;
        }

        public ChessState(
            IDictionary<string, ChessPiece> pieceMap = null,
            PlayerColor nextPlayer = PlayerColor.White,
            bool hasWhiteCastled = false,
            bool hasBlackCastled = false)
            : this(CreateBoard(pieceMap), nextPlayer, hasWhiteCastled, hasBlackCastled)
        {
        }

        public PlayerColor NextPlayer { get; }
        public bool HasWhiteCastled { get; }
        public bool HasBlackCastled { get; }

        public bool IsCheck => false; // todo
        public bool IsCheckmate => false; // todo
        public bool IsStalemate => false; // todo

        public ChessTile this[int column, int row] => _board[column, row];
        public ChessTile this[BoardLocation location] => this[location.Column, location.Row];

        public ChessState ApplyMove(ChessMove move)
        {
            if (move == null)
            {
                throw new ArgumentNullException(nameof(move));
            }

            var (source, destination) = (move.Source, move.Destination);
            if (!this[source].HasPiece)
            {
                throw new InvalidChessMoveException("Source tile is empty");
            }

            var piece = this[source].Piece;
            if (this[destination].HasPiece && this[destination].Piece.Color == piece.Color)
            {
                throw new InvalidChessMoveException("Destination tile is already occupied by a piece of the same color");
            }
            // todo: support en passant captures
            if (move.IsCapture != this[destination].HasPiece)
            {
                throw new InvalidChessMoveException($"{nameof(move.IsCapture)} property is not set properly");
            }

            // todo: handle castling

            // todo:
            // - first check with IsMovePossible() fn.
            // - then, create the new state, but with the same current player, and see if our king is currently in check.
            // - if not, we're successful; otherwise, we fail.

            var newBoard = (ChessTile[,])_board.Clone();
            var (sx, sy, dx, dy) = (source.Column, source.Row, destination.Column, destination.Row);
            newBoard[sx, sy] = this[source].WithPiece(null);
            newBoard[dx, dy] = this[destination].WithPiece(piece);

            var newNextPlayer = (NextPlayer == PlayerColor.White) ? PlayerColor.Black : PlayerColor.White;

            bool castled = (piece.Kind == PieceKind.King && (destination == source.Right(2) || destination == source.Left(2)));
            bool newHasWhiteCastled = HasWhiteCastled || (NextPlayer == PlayerColor.White && castled);
            bool newHasBlackCastled = HasBlackCastled || (NextPlayer == PlayerColor.Black && castled);

            return new ChessState(
                board: newBoard,
                nextPlayer: newNextPlayer,
                hasWhiteCastled: newHasWhiteCastled,
                hasBlackCastled: newHasBlackCastled);
        }

        public IEnumerable<ChessTile> EnumerateTiles()
        {
            for (int c = 0; c < 8; c++)
            {
                for (int r = 0; r < 8; r++)
                {
                    yield return this[c, r];
                }
            }
        }

        public IEnumerable<ChessMove> GetMoves() => GetMovesAndSuccessors().Select(t => t.Item1);

        public IEnumerable<ChessState> GetSucessors() => GetMovesAndSuccessors().Select(t => t.Item2);

        public IEnumerable<(ChessMove, ChessState)> GetMovesAndSuccessors()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Checks whether it's possible to move the piece on <paramref name="source"/> to <paramref name="destination"/>.
        /// Ignores whether we would create an illegal position by putting our king in check.
        /// </summary>
        internal bool IsMovePossible(BoardLocation source, BoardLocation destination)
        {
            if (source == destination)
            {
                return false;
            }

            var sourceTile = this[source];
            var destinationTile = this[destination];
            var piece = sourceTile.Piece;

            if (destinationTile.HasPiece && destinationTile.Piece.Color == piece.Color)
            {
                return false;
            }

            bool canMoveIfUnblocked;
            bool canPieceBeBlocked = false;
            var delta = (x: destination.Column - source.Column, y: destination.Row - source.Row);

            switch (piece.Kind)
            {
                case PieceKind.Bishop:
                    canMoveIfUnblocked = (Math.Abs(delta.x) == Math.Abs(delta.y));
                    canPieceBeBlocked = true;
                    break;
                case PieceKind.King:
                    // note: We ignore the possibility of castling since we already have logic to handle that
                    canMoveIfUnblocked = (Math.Abs(delta.x) <= 1 && Math.Abs(delta.y) <= 1);
                    break;
                case PieceKind.Knight:
                    canMoveIfUnblocked = (Math.Abs(delta.x) == 1 && Math.Abs(delta.y) == 2) || (Math.Abs(delta.x) == 2 && Math.Abs(delta.y) == 1);
                    break;
                case PieceKind.Pawn:
                    int forward = (piece.Color == PlayerColor.White ? 1 : -1);
                    bool isAdvance = (!destinationTile.HasPiece && delta.x == 0 && (delta.y == forward || delta.y == forward * 2));
                    bool isCapture = (destinationTile.HasPiece && Math.Abs(delta.x) == 1 && delta.y == forward); // todo: support en passant captures

                    canMoveIfUnblocked = (isAdvance || isCapture);
                    canPieceBeBlocked = isAdvance;
                    break;
                case PieceKind.Queen:
                    canMoveIfUnblocked = (delta.x == 0 || delta.y == 0 || Math.Abs(delta.x) == Math.Abs(delta.y));
                    canPieceBeBlocked = true;
                    break;
                case PieceKind.Rook:
                    canMoveIfUnblocked = (delta.x == 0 || delta.y == 0);
                    canPieceBeBlocked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return canMoveIfUnblocked && (!canPieceBeBlocked || GetLocationsBetween(source, destination).All(loc => !this[loc].HasPiece));
        }

        /// <summary>
        /// Returns the tiles along a vertical, horizontal, or diagonal line between <paramref name="source"/> and <paramref name="destination"/>, exclusive.
        /// </summary>
        private static IEnumerable<BoardLocation> GetLocationsBetween(BoardLocation source, BoardLocation destination)
        {
            Debug.Assert(source != destination);
            var delta = (x: destination.Column - source.Column, y: destination.Row - source.Row);

            if (delta.x == 0)
            {
                // Vertical
                var start = (delta.y > 0) ? source : destination;
                int shift = Math.Abs(delta.y);
                for (int dy = 1; dy < shift; dy++)
                {
                    yield return start.Up(dy);
                }
            }
            else if (delta.y == 0)
            {
                // Horizontal
                var start = (delta.x > 0) ? source : destination;
                int shift = Math.Abs(delta.x);
                for (int dx = 1; dx < shift; dx++)
                {
                    yield return start.Right(dx);
                }
            }
            else
            {
                // Diagonal
                Debug.Assert(Math.Abs(delta.x) == Math.Abs(delta.y));

                var start = (delta.x > 0) ? source : destination;
                int shift = Math.Abs(delta.x);
                int slope = (delta.x == delta.y) ? 1 : -1;
                for (int dx = 1; dx < shift; dx++)
                {
                    int dy = dx * slope;
                    yield return start.Right(dx).Up(dy);
                }
            }
        }
    }
}
