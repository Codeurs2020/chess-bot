﻿using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using ChessBot.AlgebraicNotation;
using System.Linq;
using ChessBot.Exceptions;
using static ChessBot.AlgebraicNotation.AlgebraicNotationParser;

namespace ChessBot
{
    public class Move
    {
        private static readonly Dictionary<string, PieceKind> s_pieceKindMap = new Dictionary<string, PieceKind>
        {
            ["N"] = PieceKind.Knight,
            ["B"] = PieceKind.Bishop,
            ["R"] = PieceKind.Rook,
            ["Q"] = PieceKind.Queen,
            ["K"] = PieceKind.King,
        };

        internal static Move Castle(PlayerColor color, bool kingside)
        {
            var source = Location.Parse(color == PlayerColor.White ? "e1" : "e8");
            var destination = kingside ? source.Right(2) : source.Left(2);
            return new Move(source, destination, isKingsideCastle: kingside, isQueensideCastle: !kingside);
        }

        private static MoveContext ParseInternal(string an)
        {
            var inputStream = new AntlrInputStream(an);
            var lexer = new AlgebraicNotationLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new AlgebraicNotationParser(tokenStream);
            return parser.move();
        }

        private static Location InferSource(
            SourceContext sourceNode,
            State state,
            PieceKind pieceKind,
            Location destination)
        {
            var possibleSources = state.ActivePlayer.GetOccupiedTiles().AsEnumerable();
            var sourceSquareNode = sourceNode?.square();
            var sourceFileNode = sourceNode?.FILE();
            var sourceRankNode = sourceNode?.RANK();

            if (sourceSquareNode != null)
            {
                var sourceLocation = Location.Parse(sourceSquareNode.GetText());
                possibleSources = new[] { state[sourceLocation] };
            }
            else if (sourceFileNode != null)
            {
                int sourceColumn = sourceFileNode.GetText()[0] - 'a';
                possibleSources = possibleSources.Where(t => t.Location.Column == sourceColumn);
            }
            else if (sourceRankNode != null)
            {
                int sourceRow = sourceRankNode.GetText()[0] - '1';
                possibleSources = possibleSources.Where(t => t.Location.Row == sourceRow);
            }

            try
            {
                var sourceTile = possibleSources.Single(t => t.Piece.Kind == pieceKind && state.IsMovePossible(t.Location, destination));
                return sourceTile.Location;
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidMoveException("Could not infer source location", e);
            }
        }

        // note: This method only checks that the specified piece occupies the source square.
        // It doesn't actually check whether the move is valid; that's done in ChessState.ApplyMove.
        // todo: an empty input leads to nullrefs here
        public static Move Parse(string an, State state)
        {
            var moveNode = ParseInternal(an);
            if (moveNode.exception != null)
            {
                throw new AnParseException("Could not parse input", moveNode.exception);
            }

            // todo: enforce check/checkmate if they are specified
            var moveDescNode = moveNode.moveDesc();
            var kingsideCastleNode = moveDescNode.KINGSIDE_CASTLE();
            var queensideCastleNode = moveDescNode.QUEENSIDE_CASTLE();
            if (kingsideCastleNode != null || queensideCastleNode != null)
            {
                // todo: add a test for when we try to castle but there's no king / multiple kings
                var source = state.GetKingsLocation(state.ActiveColor) ?? throw new InvalidMoveException("Attempt to castle without exactly 1 king");
                var destination = (kingsideCastleNode != null) ? source.Right(2) : source.Left(2);
                return new Move(
                    source,
                    destination,
                    isCapture: false,
                    isKingsideCastle: (kingsideCastleNode != null),
                    isQueensideCastle: (queensideCastleNode != null));
            }
            else
            {
                var ordinaryMoveDescNode = moveDescNode.ordinaryMoveDesc();
                var pieceKindNode = ordinaryMoveDescNode.pieceKind();
                var sourceNode = ordinaryMoveDescNode.source();
                var captureNode = ordinaryMoveDescNode.CAPTURE();
                var destinationNode = ordinaryMoveDescNode.destination();
                var promotionKindNode = ordinaryMoveDescNode.promotionKind();

                var pieceKind = (pieceKindNode != null) ? s_pieceKindMap[pieceKindNode.GetText()] : PieceKind.Pawn;
                bool isCapture = (captureNode != null); // todo: enforce this if true. take en passant captures into account.
                var destination = Location.Parse(destinationNode.GetText());
                var promotionKind = (promotionKindNode != null) ? s_pieceKindMap[promotionKindNode.GetText()] : (PieceKind?)null;
                var source = InferSource(sourceNode, state, pieceKind, destination);

                return new Move(
                    source,
                    destination,
                    isCapture: isCapture,
                    promotionKind: promotionKind);
            }
        }

        public Move(
            Location source,
            Location destination,
            bool? isCapture = null,
            bool isKingsideCastle = false,
            bool isQueensideCastle = false,
            PieceKind? promotionKind = null)
        {
            if (source == destination)
            {
                // todo: throw error
            }

            if ((isKingsideCastle && destination != source.Right(2)) ||
                (isQueensideCastle && destination != source.Left(2)))
            {
                // todo: throw error
            }

            switch (promotionKind)
            {
                case null:
                case PieceKind.Bishop:
                case PieceKind.Knight:
                case PieceKind.Queen:
                case PieceKind.Rook:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(promotionKind));
            }

            Source = source;
            Destination = destination;
            IsCapture = isCapture;
            IsKingsideCastle = isKingsideCastle;
            IsQueensideCastle = isQueensideCastle;
            PromotionKind = promotionKind;
        }

        public Location Source { get; }
        public Location Destination { get; }
        public bool? IsCapture { get; }
        public bool IsKingsideCastle { get; }
        public bool IsQueensideCastle { get; }
        public PieceKind? PromotionKind { get; }

        public override string ToString()
        {
            // todo: add info about more fields
            return $"{Source} > {Destination}";
        }
    }
}
