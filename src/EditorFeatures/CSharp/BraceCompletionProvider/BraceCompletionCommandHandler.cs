// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editor.Implementation.AutomaticCompletion;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.ExternalAccess.VSTypeScript.Api;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CSharp.BraceCompletion
{
    /// <summary>
    /// Command Handler that responsible for adding empty block when user enter 'Edit.SmartBreakLine'.
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [ContentType(ContentTypeNames.CSharpContentType)]
    [Name(PredefinedCommandHandlerNames.AutomaticLineEnder)]
    [Order(After = PredefinedCompletionNames.CompletionCommandHandler)]
    internal class BraceCompletionCommandHandler : IChainedCommandHandler<AutomaticLineEnderCommandArgs>
    {
        private readonly IEditorOperationsFactoryService _editorOperationsFactoryService;
        private readonly ITextUndoHistoryRegistry _textUndoHistoryRegistry;

        private static readonly string s_bracePair = string.Concat(
            SyntaxFacts.GetText(SyntaxKind.OpenBraceToken),
            Environment.NewLine,
            SyntaxFacts.GetText(SyntaxKind.CloseBraceToken));

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public BraceCompletionCommandHandler(
            ITextUndoHistoryRegistry textUndoHistoryRegistry,
            IEditorOperationsFactoryService editorOperationsFactoryService)
        {
            _textUndoHistoryRegistry = textUndoHistoryRegistry;
            _editorOperationsFactoryService = editorOperationsFactoryService;
        }

        public string DisplayName { get; } = nameof(BraceCompletionCommandHandler);

        public CommandState GetCommandState(AutomaticLineEnderCommandArgs args, Func<CommandState> _)
            => CommandState.Available;

        public void ExecuteCommand(AutomaticLineEnderCommandArgs args, Action nextCommandHandler,
            CommandExecutionContext executionContext)
        {
            var editorOperation = _editorOperationsFactoryService.GetEditorOperations(args.TextView);
            if (editorOperation == null)
            {
                nextCommandHandler();
                return;
            }

            var document = args.SubjectBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
            if (document == null)
            {
                nextCommandHandler();
                return;
            }

            var currentCaret = ITextViewExtensions.GetCaretPoint(args.TextView, args.SubjectBuffer);
            if (!currentCaret.HasValue)
            {
                nextCommandHandler();
                return;
            }

            var cancellationToken = executionContext.OperationContext.UserCancellationToken;
            var root = document.GetRequiredSyntaxRootSynchronously(cancellationToken);
            if (!TryGetInsertPosition(root, currentCaret.Value, out var insertPosition)
                || !insertPosition.HasValue)
            {
                nextCommandHandler();
                return;
            }

            using var transaction = args.TextView.CreateEditTransaction(nameof(BraceCompletionCommandHandler), _textUndoHistoryRegistry, _editorOperationsFactoryService);

            // 1. Insert '{\r\n}' to the document
            // After this operation the text would become like
            // Bar(int i, int c) {
            // }
            var newDocument = document.InsertText(insertPosition.Value, s_bracePair, cancellationToken);

            // 2. Place caret between '{$$\r\n}'
            args.TextView.TryMoveCaretToAndEnsureVisible(
                new SnapshotPoint(args.SubjectBuffer.CurrentSnapshot, insertPosition.Value + 1));

            // 3. Format the inserted brace
            // After this operation the text would become like
            // Bar(int i, int c)
            // {$$
            // }
            Format(newDocument, insertPosition.Value, cancellationToken);

            // 4. Press enter
            // After this operation the text would become like
            // Bar(int i, int c)
            // {
            //     $$
            // }
            InsertNewLine(editorOperation);

            transaction.Complete();
        }

        private static void InsertNewLine(IEditorOperations editorOperations)
            => editorOperations.InsertNewLine();

        private static void Format(Document document, int insertionPosition, CancellationToken cancellationToken)
        {
            var formatService = document.GetRequiredLanguageService<IEditorFormattingService>();
            var changes = formatService.GetFormattingChangesAsync(
                document,
                new TextSpan(start: insertionPosition, length: s_bracePair.Length),
                cancellationToken).WaitAndGetResult(cancellationToken);

            document.ApplyTextChanges(changes, cancellationToken);
        }

        private static bool TryGetInsertPosition(
            SyntaxNode root,
            int caretPosition,
            out int? insertPosition)
        {
            insertPosition = null;
            var token = root.FindTokenOnLeftOfPosition(caretPosition);
            if (token.IsKind(SyntaxKind.None))
            {
                return false;
            }

            // Find the innermost matching node
            // For example:
            // void Bar() { void Bar1() { void Bar2$$() }}
            // Here only 'Bar2' should be considered.
            var nodeCandidate = token.GetAncestors(SupportedSyntaxNode).FirstOrDefault();
            if (nodeCandidate == null)
            {
                return false;
            }

            // Try to find the insert position for braces for the syntax node
            return TryGetInsertPositionForEmbeddedStatementOwner(nodeCandidate, out insertPosition)
               || TryGetInsertPositionForNamespaceAndTypeDeclarationNode(nodeCandidate, out insertPosition)
               || TryGetInsertPositionForTypeMemberNode(nodeCandidate, out insertPosition)
               || TryGetInsertPositionForLocalFunction(nodeCandidate, out insertPosition)
               || TryGetInsertPositionForObjectCreationExpression(nodeCandidate, out insertPosition);
        }

        private static bool TryGetInsertPositionForNamespaceAndTypeDeclarationNode(SyntaxNode node, out int? insertPoint)
        {
            insertPoint = null;
            // For namespace, class, struct and etc.,
            // make sure its name identifier is not missing

            if (node is NamespaceDeclarationSyntax {Name: IdentifierNameSyntax name}
                && !name.Identifier.IsMissing
                && HasNoBrace(node))
            {
                insertPoint = name.Identifier.Span.End;
                return true;
            }

            if (node is BaseTypeDeclarationSyntax typeDeclaration
                && !typeDeclaration.Identifier.IsMissing
                && HasNoBrace(node))
            {
                insertPoint = typeDeclaration.Identifier.Span.End;
                return true;
            }

            return false;
        }

        private static bool TryGetInsertPositionForTypeMemberNode(SyntaxNode node, out int? insertPosition)
        {
            insertPosition = null;
            // For method, make sure it has open & close parenthesis for the parameter list,
            // the insertion position is the end of close parenthesis
            if (node is BaseMethodDeclarationSyntax methodNode
                && HasNoMissingParenthesis(methodNode)
                && HasNoBrace(methodNode))
            {
                var (_, closeParenthesis) = methodNode.GetParameterList().GetParentheses();
                insertPosition = closeParenthesis.Span.End;
                return true;
            }

            if (node is AccessorDeclarationSyntax accessorNode)
            {
                // For accessors in Property, event & indexer
                // Only consider inserting {} when it has no ending semicolon & no expression body
                // e.g. Before: class Bar
                // {
                //      int Foo
                //      {
                //          get$$
                //      }
                // }
                // After: class Bar
                // {
                //      int Foo
                //      {
                //          get{}
                //      }
                // }
                //
                var semicolonMissing = accessorNode.SemicolonToken.IsKind(SyntaxKind.None) || accessorNode.SemicolonToken.IsMissing;
                if (semicolonMissing
                    && accessorNode.ExpressionBody == null
                    && accessorNode.Body == null)
                {
                    insertPosition = accessorNode.Keyword.Span.End;
                    return true;
                }
            }

            if (node is EventFieldDeclarationSyntax eventDeclaration
                && eventDeclaration.SemicolonToken.IsMissing)
            {
                // For event field declaration, insert {} if it is doesn't have semicolon
                // e.g. before: event EventHandler Bar$$
                // after: event EventHandler Bar{}
                // Note: EventFieldDeclaration becomes EventDeclarationSyntax
                insertPosition = eventDeclaration.Span.End;
                return true;
            }

            if (node is IndexerDeclarationSyntax indexerDeclaration
                && (indexerDeclaration.AccessorList == null || indexerDeclaration.AccessorList.HasDiagnostics()))
            {
                // For indexer declaration, insert {} if it doesn't have AccessorList.
                // Also check the diagnostics, before for this case:
                // class Bar
                // {
                //      int this[int i]$$
                // }
                // parser will think the last '}' is a part of the AccessorList, and the '{' is missing.
                insertPosition = indexerDeclaration.Span.End;
                return true;
            }

            // Don't consider adding {} for Property's AccessorList because, for example,
            // class Bar {
            //      public int Foo$$
            // }
            // we can't tell if is it a field or property.

            return false;
        }

        private static bool TryGetInsertPositionForEmbeddedStatementOwner(SyntaxNode node, out int? insertPosition)
        {
            insertPosition = null;
            if (node.IsEmbeddedStatementOwner())
            {
                // If the statement is not missing,
                // Don't insert
                var statement = node.GetEmbeddedStatement();
                // CS1023 error.. Is there better way to call binder instead?
                var isValidStatement = !statement.IsMissing
                   && !statement.IsKind(SyntaxKind.LocalDeclarationStatement)
                   && !statement.IsKind(SyntaxKind.LocalFunctionStatement);
                if (isValidStatement || !HasNoBrace(statement))
                {
                    return false;
                }

                // For the node has parenthesis, like If statement and for statement,
                // Make sure its open & close parenthesis are not missing,
                // then use the end of close parenthesis as insert position
                if (ShouldCheckParenthesisForEmbeddedOwner(node)
                    && HasNoMissingParenthesis(node))
                {
                    var (_, closeParenthesis) = node.GetParentheses();
                    insertPosition = closeParenthesis.Span.End;
                    return closeParenthesis != default;
                }

                // For do statement use the end of do keyword as the insert position
                if (node is DoStatementSyntax doStatement && HasNoBrace(doStatement))
                {
                    insertPosition = doStatement.DoKeyword.Span.End;
                    return true;
                }

                // For else clause,
                // 1. If it is an else clause without if, use the end of else keyword as insert position
                // 2. If it is an else clause with if, find the insert position for that if statement
                if (node is ElseClauseSyntax elseClauseSyntax)
                {
                    if (elseClauseSyntax.Statement is IfStatementSyntax ifStatementSyntax)
                    {
                        return TryGetInsertPositionForEmbeddedStatementOwner(ifStatementSyntax, out insertPosition);
                    }

                    var (_, closeParenthesis) = node.GetParentheses();
                    insertPosition = closeParenthesis.Span.End;
                    return closeParenthesis != default;
                }
            }

            return false;
        }

        private static bool TryGetInsertPositionForLocalFunction(SyntaxNode node, out int? insertPosition)
        {
            insertPosition = null;
            if (node is LocalFunctionStatementSyntax localFunction
                && HasNoBrace(localFunction)
                && HasNoMissingParenthesis(localFunction))
            {
                insertPosition = localFunction.Span.End;
                return true;
            }

            return false;
        }

        private static bool TryGetInsertPositionForObjectCreationExpression(SyntaxNode node, out int? insertPosition)
        {
            insertPosition = null;
            // For ObjectCreationExpression, insert {} if it doesn't have initializer
            if (node is ObjectCreationExpressionSyntax {Initializer: null} objectExpression)
            {
                if (objectExpression.ArgumentList == null)
                {
                    // If it doesn't have argument list, then the insert point is the end of the type
                    // e.g. var c = new Bar$$; => var c = new Bar { $$ };
                    insertPosition = objectExpression.Type.Span.End;
                }
                else
                {
                    // If it has argument list, then the insert point is after the close parenthesis.
                    // e.g. var c = new Bar()$$; => var c = new Bar() { $$ };
                    insertPosition = objectExpression.ArgumentList.CloseParenToken.Span.End;
                }

                return true;
            }

            return false;
        }

        private static bool HasNoBrace(SyntaxNode node)
        {
            var (openBrace, closeBrace) = node.GetBraces();
            return (openBrace.IsKind(SyntaxKind.None) && closeBrace.IsKind(SyntaxKind.None))
                || (openBrace.IsMissing && closeBrace.IsMissing);
        }

        private static bool HasNoMissingParenthesis(SyntaxNode node)
        {
            // make sure when we start to add brace, syntax node has
            // correct parenthesis pair.
            // E.g. Insert brace for 'if (bar$$)',
            // don't insert for node like 'if (bar))'
            if (ShouldCheckParenthesisForEmbeddedOwner(node))
            {
                var (openParenthesis, closeParenthesis) = node.GetParentheses();
                return !openParenthesis.IsMissing && !closeParenthesis.IsMissing;
            }

            // if the node has parameter list, make sure the parenthesis not missing
            // e.g.
            // For 'Bar(int i, int c$$)', insert braces
            // For 'Bar(int i, int c,' don't insert braces
            var parameterList = node.GetParameterList();
            if (parameterList != null)
            {
                var (openParenthesis, closeParenthesis) = parameterList.GetParentheses();
                return !openParenthesis.IsMissing && !closeParenthesis.IsMissing;
            }

            return false;
        }

        /// <summary>
        /// A subset of the <see cref="Microsoft.CodeAnalysis.CSharp.Extensions.SyntaxNodeExtensions.IsEmbeddedStatementOwner"/> that
        /// should check if there is missing parenthesis before inserting braces.
        /// Example:
        /// Braces should be inserted after 'if (bar$$)', not 'if (bar$$'
        /// </summary>
        private static bool ShouldCheckParenthesisForEmbeddedOwner(SyntaxNode node)
            => node is FixedStatementSyntax
               || node is CommonForEachStatementSyntax
               || node is ForStatementSyntax
               || node is IfStatementSyntax
               || node is LockStatementSyntax
               || node is UsingStatementSyntax
               || node is WhileStatementSyntax;

        /// <summary>
        /// All the syntax nodes that should be checked to see if braces could be inserted
        /// </summary>
        private static bool SupportedSyntaxNode(SyntaxNode node)
            => node is NamespaceDeclarationSyntax
                   or BaseTypeDeclarationSyntax
                   or BaseMethodDeclarationSyntax
                   or AccessorDeclarationSyntax
                   or EventFieldDeclarationSyntax
                   or IndexerDeclarationSyntax
                   or LocalFunctionStatementSyntax
                   or ObjectCreationExpressionSyntax
               || node.IsEmbeddedStatementOwner();
    }
}
