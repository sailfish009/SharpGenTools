﻿using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SharpGen.Model;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using SharpGen.Transform;

namespace SharpGen.Generator
{
    class FieldCodeGenerator : MemberCodeGeneratorBase<CsField>
    {
        private readonly bool explicitLayout;

        public FieldCodeGenerator(IDocumentationAggregator documentation, bool explicitLayout)
            :base(documentation)
        {
            this.explicitLayout = explicitLayout;
        }

        public override IEnumerable<MemberDeclarationSyntax> GenerateCode(CsField csElement)
        {
            var docComments = GenerateDocumentationTrivia(csElement);
            if (csElement.IsBoolToInt && !csElement.IsArray)
            {
                yield return PropertyDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), csElement.Name)
                    .WithAccessorList(
                    AccessorList(
                        List(
                            new[]
                            {
                                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                    .WithExpressionBody(ArrowExpressionClause(
                                        BinaryExpression(SyntaxKind.NotEqualsExpression,
                                                ParseName($"_{csElement.Name}"),
                                                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)))))
                                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                                AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                    .WithExpressionBody(ArrowExpressionClause(CastExpression(ParseTypeName(csElement.PublicType.QualifiedName), ParseName("value"))))
                                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                            })))
                    .WithModifiers(TokenList(ParseTokens(csElement.VisibilityName)))
                    .WithLeadingTrivia(Trivia(docComments));
                yield return GenerateBackingField(csElement, explicitLayout, null);
            }
            else if (csElement.IsArray && csElement.QualifiedName != "System.String")
            {
                yield return PropertyDeclaration(ArrayType(ParseTypeName(csElement.PublicType.QualifiedName), SingletonList(ArrayRankSpecifier())), csElement.Name)
                    .WithAccessorList(
                        AccessorList(
                            List(
                                new[]
                                {
                                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                        .WithExpressionBody(ArrowExpressionClause(
                                            BinaryExpression(SyntaxKind.CoalesceExpression,
                                            ParseName($"_{csElement.Name}"),
                                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                                ParseName($"_{csElement.Name}"),
                                                ObjectCreationExpression(
                                                    ArrayType(ParseTypeName(csElement.PublicType.QualifiedName),
                                                    SingletonList(
                                                        ArrayRankSpecifier(
                                                            SingletonSeparatedList<ExpressionSyntax>(
                                                                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(csElement.ArrayDimensionValue))))
                                                    )))))))
                                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                                })))
                    .WithModifiers(TokenList(ParseTokens(csElement.VisibilityName)))
                    .WithLeadingTrivia(Trivia(docComments));
                yield return GenerateBackingField(csElement, explicitLayout, null, true);
            }
            else if (csElement.IsBitField)
            {
                if (csElement.BitMask == 1)
                {
                    yield return PropertyDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), csElement.Name)
                        .WithAccessorList(
                            AccessorList(
                                SingletonList(
                                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                        .WithExpressionBody(ArrowExpressionClause(
                                            BinaryExpression(SyntaxKind.NotEqualsExpression,
                                                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)),
                                                BinaryExpression(SyntaxKind.BitwiseAndExpression,
                                                    BinaryExpression(SyntaxKind.RightShiftExpression,
                                                        ParseName(csElement.Name),
                                                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(csElement.BitOffset))),
                                                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(csElement.BitMask))))))
                                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                                    )))
                    .WithModifiers(TokenList(ParseTokens(csElement.VisibilityName)))
                    .WithLeadingTrivia(Trivia(docComments));
                }
                else
                {

                    yield return PropertyDeclaration(ParseTypeName(csElement.PublicType.QualifiedName), csElement.Name)
                        .WithAccessorList(
                            AccessorList(
                                SingletonList(
                                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                        .WithExpressionBody(ArrowExpressionClause(
                                            CastExpression(ParseTypeName(csElement.PublicType.QualifiedName),
                                                BinaryExpression(SyntaxKind.BitwiseAndExpression,
                                                    BinaryExpression(SyntaxKind.RightShiftExpression,
                                                        ParseName(csElement.Name),
                                                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(csElement.BitOffset))),
                                                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(csElement.BitMask))))))
                                    )))
                    .WithModifiers(TokenList(ParseTokens(csElement.VisibilityName)))
                    .WithLeadingTrivia(Trivia(docComments));
                }
                yield return GenerateBackingField(csElement, explicitLayout, null);
            }
            else
            {
                yield return GenerateBackingField(csElement, explicitLayout, docComments);
            }
        }

        private static MemberDeclarationSyntax GenerateBackingField(CsField field, bool explicitLayout, DocumentationCommentTriviaSyntax docTrivia, bool isArray = false)
        {
            var fieldDecl = FieldDeclaration(
                VariableDeclaration(isArray ?
                    ArrayType(ParseTypeName(field.PublicType.QualifiedName), SingletonList(ArrayRankSpecifier()))
                    : ParseTypeName(field.PublicType.QualifiedName),
                    SingletonSeparatedList(
                        VariableDeclarator(field.Name)
                    )))
                    .WithModifiers(TokenList(ParseTokens(field.VisibilityName)));

            if (explicitLayout)
            {
                fieldDecl = fieldDecl.WithAttributeLists(SingletonList(
                    AttributeList(
                        SingletonSeparatedList(Attribute(
                            ParseName("System.Runtime.InteropServices.FieldOffset"),
                            AttributeArgumentList(
                                SingletonSeparatedList(AttributeArgument(
                                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(field.Offset))))))))
                ));
            }
            return docTrivia != null ? fieldDecl.WithLeadingTrivia(Trivia(docTrivia)) : fieldDecl;
        }

    }
}