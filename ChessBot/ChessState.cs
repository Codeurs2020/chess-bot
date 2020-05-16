﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static ChessBot.ChessPiece;

namespace ChessBot
{
    // todo: enforce, for Parse() and ApplyMove(), that if a pawn reaches the back rank it *must* be promoted

    /// <summary>
    /// Immutable class representing the state of the chess board.
    /// </summary>
    public class ChessState : IEquatable<ChessState>
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
            var pieces = pieceMap.Values;
            // todo: add tests for this
            if (pieces.Count(t => t == BlackKing) > 1 || pieces.Count(t => t == WhiteKing) > 1)
            {
                throw new ArgumentException("Cannot have more than 1 king of a given color", nameof(pieceMap));
            }

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
            PlayerColor activeColor,
            PlayerInfo white,
            PlayerInfo black)
        {
            _board = board;
            ActiveColor = activeColor;
            White = white?.SetState(this) ?? new PlayerInfo(this, PlayerColor.White);
            Black = black?.SetState(this) ?? new PlayerInfo(this, PlayerColor.Black);
        }

        public ChessState(
            IDictionary<string, ChessPiece> pieceMap = null,
            PlayerColor activeColor = PlayerColor.White,
            PlayerInfo white = null,
            PlayerInfo black = null)
            : this(CreateBoard(pieceMap), activeColor, white, black)
        {
        }

        public PlayerColor ActiveColor { get; }
        public PlayerInfo White { get; }
        public PlayerInfo Black { get; }

        public PlayerInfo ActivePlayer => GetPlayer(ActiveColor);
        public PlayerColor OpposingColor => (ActiveColor == PlayerColor.White) ? PlayerColor.Black : PlayerColor.White;
        public PlayerInfo OpposingPlayer => GetPlayer(OpposingColor);

        public bool IsCheck => GetKingsLocation() is BoardLocation loc && IsAttackedBy(OpposingColor, loc);
        public bool IsCheckmate => IsCheck && HasNoMoves;
        public bool IsStalemate => !IsCheck && HasNoMoves;
        public bool HasNoMoves => !GetMoves().Any();

        public ChessTile this[int column, int row] => _board[column, row];
        public ChessTile this[BoardLocation location] => this[location.Column, location.Row];
        public ChessTile this[string location] => this[BoardLocation.Parse(location)];

        public ChessState ApplyMove(string move, bool togglePlayer = true) => ApplyMove(ChessMove.Parse(move, this), togglePlayer);

        public ChessState ApplyMove(ChessMove move, bool togglePlayer = true)
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

            // Step 1: Update the board
            var newBoard = _board;

            bool castled = (move.IsKingsideCastle || move.IsQueensideCastle);
            if (castled)
            {
                // todo: we don't enforce the requirement that both pieces must be on the first rank.
                // we assume that you're starting from the initial chess position, for which this is true as long as they have not moved.
                bool hasMovedRook = (move.IsKingsideCastle ? ActivePlayer.HasMovedKingsideRook : ActivePlayer.HasMovedQueensideRook);
                bool kingPassesThroughAttackedLocation = GetLocationsBetween(source, destination).Any(loc => IsAttackedBy(OpposingColor, loc));
                if (ActivePlayer.HasCastled || ActivePlayer.HasMovedKing || hasMovedRook || IsCheck || kingPassesThroughAttackedLocation)
                {
                    throw new InvalidChessMoveException("Requirements for castling not met");
                }

                var rookSource = /* location of kingside or queenside rook of the active player */;
                var rookDestination = move.IsKingsideCastle ? rookSource.Left(2) : rookSource.Right(3);
                newBoard = ApplyMoveInternal(newBoard, rookSource, rookDestination);
            }

            // todo:
            // - first check with castled || IsMovePossible() fn.
            // - then, create the new state, but with the same current player, and see if our king becomes checked.
            //   - as an optimization, we could probably narrow our search if our king is currently checked: only bother for moves
            //     that follow one of the three moves that could possibly get us out of check.
            // - if not, we're successful; otherwise, we fail.

            // Step 2: Ensure our king isn't in check after making the changes
            // todo
            // (may want to consider adding this verification to the ctor as well)

            // Step 3: Update other fields
            var newActiveColor = (togglePlayer ? OpposingColor : ActiveColor);
            var newPlayer = ActivePlayer;

            switch (piece.Kind)
            {
                case PieceKind.King:
                    newPlayer = newPlayer.SetHasMovedKing(true);
                    break;
                case PieceKind.Rook:
                    if (!newPlayer.HasMovedKingsideRook && /* has square of kingside rook */) newPlayer.SetHasMovedKingsideRook(true);
                    if (!newPlayer.HasMovedQueensideRook && /* has square of queenside rook */) newPlayer.SetHasMovedKingsideRook(true);
                    break;
            }

            if (castled)
            {
                newPlayer = newPlayer.SetHasCastled(true);
                newPlayer = move.IsKingsideCastle ? newPlayer.SetHasMovedKingsideRook(true) : newPlayer.SetHasMovedQueensideRook(true);
            }

            return new ChessState(
                board: newBoard,
                activeColor: newActiveColor,
                white: (ActiveColor == PlayerColor.White) ? newPlayer : White,
                black: (ActiveColor == PlayerColor.Black) ? newPlayer : Black); // todo: check if our king is (still?) in check)
        }

        private static ChessTile[,] ApplyMoveInternal(ChessTile[,] board, BoardLocation source, BoardLocation destination)
        {
            var (sx, sy, dx, dy) = (source.Column, source.Row, destination.Column, destination.Row);
            var newBoard = (ChessTile[,])board.Clone();

            newBoard[sx, sy] = newBoard[sx, sy].SetPiece(null);
            newBoard[dx, dy] = newBoard[dx, dy].SetPiece(board[sx, sy].Piece);
            return newBoard;
        }

        public override bool Equals(object obj) => Equals(obj as ChessState);

        public bool Equals([AllowNull] ChessState other)
        {
            if (other == null) return false;

            if (ActiveColor != other.ActiveColor ||
                !White.EqualsIgnoreState(other.White) ||
                !Black.EqualsIgnoreState(other.Black))
            {
                return false;
            }

            for (int c = 0; c < 8; c++)
            {
                for (int r = 0; r < 8; r++)
                {
                    if (!this[c, r].Equals(other[c, r])) return false;
                }
            }

            return true;
        }

        public override int GetHashCode() => throw new NotImplementedException();

        public IEnumerable<ChessMove> GetMoves() => GetMovesAndSuccessors().Select(t => t.Item1);

        public IEnumerable<(ChessMove, ChessState)> GetMovesAndSuccessors()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ChessTile> GetOccupiedTiles() => GetTiles().Where(t => t.HasPiece);

        public PlayerInfo GetPlayer(PlayerColor color) => (color == PlayerColor.White) ? White : Black;

        public IEnumerable<ChessState> GetSucessors() => GetMovesAndSuccessors().Select(t => t.Item2);

        public IEnumerable<ChessTile> GetTiles()
        {
            for (int c = 0; c < 8; c++)
            {
                for (int r = 0; r < 8; r++)
                {
                    yield return this[c, r];
                }
            }
        }

        public override string ToString() => string.Join(Environment.NewLine, GetOccupiedTiles());

        internal BoardLocation? GetKingsLocation(PlayerColor? color = null)
        {
            // todo: fix impl so it doesn't throw if there are 2+ matches
            return GetOccupiedTiles()
                .SingleOrDefault(t => t.Piece.Kind == PieceKind.King && t.Piece.Color == (color ?? ActiveColor))?
                .Location;
        }

        /// <summary>
        /// Checks whether it's possible to move the piece on <paramref name="source"/> to <paramref name="destination"/>.
        /// Ignores whether we would create an illegal position by putting our king in check.
        /// <br/>
        /// This is basically equivalent to checking whether GetPossibleDestinations(<paramref name="source"/>) contains <paramref name="destination"/>.
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
                    // note: We ignore the possibility of castling since we already have logic in place to handle that
                    canMoveIfUnblocked = (Math.Abs(delta.x) <= 1 && Math.Abs(delta.y) <= 1);
                    break;
                case PieceKind.Knight:
                    canMoveIfUnblocked = (Math.Abs(delta.x) == 1 && Math.Abs(delta.y) == 2) || (Math.Abs(delta.x) == 2 && Math.Abs(delta.y) == 1);
                    break;
                case PieceKind.Pawn:
                    int forward = (piece.Color == PlayerColor.White ? 1 : -1);
                    int homeRow = (piece.Color == PlayerColor.White ? 1 : 6);
                    bool isValidAdvance = (!destinationTile.HasPiece && delta.x == 0 && (delta.y == forward || (delta.y == forward * 2 && source.Row == homeRow)));
                    bool isValidCapture = (destinationTile.HasPiece && Math.Abs(delta.x) == 1 && delta.y == forward); // todo: support en passant captures

                    canMoveIfUnblocked = (isValidAdvance || isValidCapture);
                    canPieceBeBlocked = isValidAdvance;
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

        private IEnumerable<BoardLocation> GetPossibleDestinations(BoardLocation source)
        {
            var sourceTile = this[source];
            var piece = sourceTile.Piece;
            var destinations = new List<BoardLocation>();

            switch (piece.Kind)
            {
                case PieceKind.Bishop:
                    destinations.AddRange(GetDiagonalExtension(source));
                    break;
                case PieceKind.King:
                    if (source.Row > 0)
                    {
                        if (source.Column > 0) destinations.Add(source.Down(1).Left(1));
                        destinations.Add(source.Down(1));
                        if (source.Column < 7) destinations.Add(source.Down(1).Right(1));
                    }
                    if (source.Column > 0) destinations.Add(source.Left(1));
                    if (source.Column < 7) destinations.Add(source.Right(1));
                    if (source.Row < 7)
                    {
                        if (source.Column > 0) destinations.Add(source.Up(1).Left(1));
                        destinations.Add(source.Up(1));
                        if (source.Column < 7) destinations.Add(source.Up(1).Right(1));
                    }
                    break;
                case PieceKind.Knight:
                    if (source.Row > 0 && source.Column > 1) destinations.Add(source.Down(1).Left(2));
                    if (source.Row < 7 && source.Column > 1) destinations.Add(source.Up(1).Left(2));
                    if (source.Row > 0 && source.Column < 6) destinations.Add(source.Down(1).Right(2));
                    if (source.Row < 7 && source.Column < 6) destinations.Add(source.Up(1).Right(2));
                    if (source.Row > 1 && source.Column > 0) destinations.Add(source.Down(2).Left(1));
                    if (source.Row < 6 && source.Column > 0) destinations.Add(source.Up(2).Left(1));
                    if (source.Row > 1 && source.Column < 7) destinations.Add(source.Down(2).Right(1));
                    if (source.Row < 6 && source.Column < 7) destinations.Add(source.Up(2).Right(1));
                    break;
                case PieceKind.Pawn:
                    int forward = (piece.Color == PlayerColor.White ? 1 : -1);
                    int homeRow = (piece.Color == PlayerColor.White ? 1 : 6);

                    var (n1, n2) = (source.Up(forward), source.Up(forward * 2));
                    if (!this[n1].HasPiece) destinations.Add(n1);
                    if (!this[n1].HasPiece && !this[n2].HasPiece && source.Row == homeRow) destinations.Add(n2);

                    if (source.Column > 0)
                    {
                        var nw = n1.Left(1);
                        if (this[nw].HasPiece && this[nw].Piece.Color != piece.Color)
                        {
                            destinations.Add(nw);
                        }
                    }

                    if (source.Column < 7)
                    {
                        var ne = n1.Right(1);
                        if (this[ne].HasPiece && this[ne].Piece.Color != piece.Color)
                        {
                            destinations.Add(ne);
                        }
                    }
                    break;
                case PieceKind.Queen:
                    destinations.AddRange(GetDiagonalExtension(source));
                    destinations.AddRange(GetOrthogonalExtension(source));
                    break;
                case PieceKind.Rook:
                    destinations.AddRange(GetOrthogonalExtension(source));
                    break;
            }

            return destinations.Where(d => !this[d].HasPiece || this[d].Piece.Color != piece.Color);
        }

        // note: May include squares occupied by friendly pieces
        private IEnumerable<BoardLocation> GetDiagonalExtension(BoardLocation source)
        {
            var prev = source;

            // Northeast
            while (prev.Row < 7 && prev.Column < 7)
            {
                var next = prev.Up(1).Right(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // Southeast
            while (prev.Row > 0 && prev.Column < 7)
            {
                var next = prev.Down(1).Right(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // Southwest
            while (prev.Row > 0 && prev.Column > 0)
            {
                var next = prev.Down(1).Left(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // Northwest
            while (prev.Row < 7 && prev.Column > 0)
            {
                var next = prev.Up(1).Left(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }
        }

        // note: May include squares occupied by friendly pieces
        private IEnumerable<BoardLocation> GetOrthogonalExtension(BoardLocation source)
        {
            var prev = source;

            // East
            while (prev.Column < 7)
            {
                var next = prev.Right(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // West
            while (prev.Column > 0)
            {
                var next = prev.Left(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // North
            while (prev.Row < 7)
            {
                var next = prev.Up(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // South
            while (prev.Row > 0)
            {
                var next = prev.Down(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }
        }

        /// <summary>
        /// Determines whether <paramref name="location"/> is attacked by an enemy piece.
        /// Ignores whether it's possible for the enemy piece to move (ie. because it is pinned to the enemy king).
        /// </summary>
        private bool IsAttackedBy(PlayerColor color, BoardLocation location)
            // It's ok that IsMovePossible() ignores castling, since the rook/king cannot perform captures while castling.
            => GetPlayer(color).GetOccupiedTiles().Any(t => IsMovePossible(t.Location, location));

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
