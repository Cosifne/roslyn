// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.ExtractMethod;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editor.Implementation.AutomaticCompletion;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.ExternalAccess.VSTypeScript.Api;
using Microsoft.CodeAnalysis.ExtractMethod;
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
    [Name(nameof(BraceCompletionCommandHandler))]
    [Order(After = PredefinedCompletionNames.CompletionCommandHandler)]
    internal class BraceCompletionCommandHandler : IChainedCommandHandler<AutomaticLineEnderCommandArgs>
    {
        private readonly IEditorOperationsFactoryService _editorOperationsFactoryService;
        private readonly ITextUndoHistoryRegistry _textUndoHistoryRegistry;

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

            var (currentCaretPosition, hasValue) = ITextViewExtensions.GetCaretPoint(args.TextView, args.SubjectBuffer);
            if (!hasValue)
            {
                nextCommandHandler();
                return;
            }

            var cancellationToken = executionContext.OperationContext.UserCancellationToken;
            var syntaxTree = document.GetRequiredSyntaxTreeSynchronously(cancellationToken);
            var root = syntaxTree.GetRoot(cancellationToken);
            var textChanges = GetTextChanges(
                root,
                currentCaretPosition,
                (CSharpParseOptions)syntaxTree.Options);

            if (textChanges.IsEmpty)
            {
                nextCommandHandler();
                return;
            }

            using var transaction = args.TextView.CreateEditTransaction(nameof(BraceCompletionCommandHandler), _textUndoHistoryRegistry, _editorOperationsFactoryService);

            // 1. Insert '{\r\n}' to the document
            // After this operation the text would become like
            // Bar(int i, int c) {
            // }
            var newDocument = document.ApplyTextChanges(textChanges, cancellationToken);
            var newRoot = newDocument.GetRequiredSyntaxRootSynchronously(cancellationToken);
            if (!TryGetSelectedSyntaxNode(newRoot, currentCaretPosition, out var selectedNode))
            {
                transaction.Cancel();
            }

            if (!TryGetNextCaretPosition(selectedNode!, out var nextCaretPosition))
            {
                transaction.Cancel();
            }

            // 2. Place caret between '{$$\r\n}'
            args.TextView.TryMoveCaretToAndEnsureVisible(
                new SnapshotPoint(args.SubjectBuffer.CurrentSnapshot, nextCaretPosition!.Value));

            // 3. Format the inserted brace
            // After this operation the text would become like
            // Bar(int i, int c)
            // {$$
            // }
            Format(newDocument, selectedNode!.Span, cancellationToken);

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

        private static void Format(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var formatService = document.GetRequiredLanguageService<IEditorFormattingService>();
            var changes = formatService.GetFormattingChangesAsync(
                document,
                span,
                cancellationToken).WaitAndGetResult(cancellationToken);

            document.ApplyTextChanges(changes, cancellationToken);
        }

        private static ImmutableArray<TextChange> GetTextChanges(SyntaxNode root, int caretPosition, CSharpParseOptions options)
        {
            if (!TryGetSelectedSyntaxNode(root, caretPosition, out var selectedNode))
            {
                return TextChange.NoChanges.ToImmutableArray();
            }

            var textChanges = selectedNode switch
            {
                NamespaceDeclarationSyntax or BaseTypeDeclarationSyntax => GetTextChangesForNamespaceAndBaseTypeDeclaration(selectedNode),
                BaseMethodDeclarationSyntax
                    or IndexerDeclarationSyntax
                    or EventFieldDeclarationSyntax
                    or FieldDeclarationSyntax => GetTextChangesForTypeMembers(selectedNode),
                _ => TextChange.NoChanges.ToImmutableArray()
            };

            if (!textChanges.IsEmpty)
            {
                return textChanges;
            }

            return TextChange.NoChanges.ToImmutableArray();

            // return TryGetInsertPositionForEmbeddedStatementOwner(nodeCandidate, out insertPosition)
            //    || TryGetInsertPositionForNamespaceAndTypeDeclarationNode(nodeCandidate, out insertPosition)
            //    || TryGetInsertPositionForTypeMemberNode(nodeCandidate, out insertPosition)
            //    || TryGetInsertPositionForLocalFunction(nodeCandidate, out insertPosition)
            //    || TryGetInsertPositionForObjectCreationExpression(nodeCandidate, out insertPosition);
        }

        private static bool TryGetSelectedSyntaxNode(SyntaxNode root, int caretPosition, out SyntaxNode? selectedNode)
        {
            selectedNode = null;
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
            if (nodeCandidate != null)
            {
                selectedNode = nodeCandidate;
                return true;
            }

            return false;
        }

        // For namespace or BaseTypeDeclaration (e.g. class, struct, enum, interface and record)
        // Check if its open & close braces are both missing.
        // If they are missing, insert an brace pair after the name
        private static ImmutableArray<TextChange> GetTextChangesForNamespaceAndBaseTypeDeclaration(SyntaxNode selectedNode) =>
            selectedNode switch
            {
                NamespaceDeclarationSyntax namespaceNode when HasNoBrace(selectedNode) => CreateInsertBracePairChange(namespaceNode.Name.FullSpan.End),
                BaseTypeDeclarationSyntax baseTypeNode when HasNoBrace(selectedNode) => CreateInsertBracePairChange(baseTypeNode.Span.End),
                _ => TextChange.NoChanges.ToImmutableArray()
            };

        private static ImmutableArray<TextChange> CreateInsertBracePairChange(int insertPosition)
            => ImmutableArray.Create(new TextChange(
                new TextSpan(insertPosition, 0),
                string.Concat(
                    SyntaxFacts.GetText(SyntaxKind.OpenBraceToken),
                    Environment.NewLine,
                    SyntaxFacts.GetText(SyntaxKind.CloseBraceToken))));

        private static ImmutableArray<TextChange> GetTextChangesForTypeMembers(SyntaxNode selectedNode)
        {
            // For method, make sure it doesn't have expression body, body and semicolon.
            // e.g. void Main()$$
            if (selectedNode is BaseMethodDeclarationSyntax { ExpressionBody: null, Body: null, SemicolonToken: { IsMissing: true } })
            {
                return CreateInsertBracePairChange(selectedNode.Span.End);
            }

            // For event field declaration, insert {} if it is doesn't have semicolon
            // e.g. before: event EventHandler Bar$$
            // after: event EventHandler Bar{}
            // Note: EventFieldDeclaration becomes EventDeclarationSyntax
            if (selectedNode is EventFieldDeclarationSyntax { SemicolonToken: { IsMissing: true } })
            {
                return CreateInsertBracePairChange(selectedNode.Span.End);
            }

            if (selectedNode is IndexerDeclarationSyntax indexerSyntax)
            {
                return GetTextChangesForIndexer(indexerSyntax);
            }

            // For field declaration without semicolon, add parenthesis to let it becomes a property
            // for example,
            // class Bar {
            //      public int Foo$$
            // }
            // would become
            // class Bar {
            //      public int Foo { $$ }
            // }
            if (selectedNode is FieldDeclarationSyntax { SemicolonToken: { IsMissing: true } } fieldNode &&
                fieldNode.Declaration.Variables.IsSingle())
            {
                return CreateInsertBracePairChange(fieldNode.Span.End);
            }

            // Don't adding {} for Property's AccessorList because,
            // we can't tell if is it a field or property.

            return TextChange.NoChanges.ToImmutableArray();
        }

        private static ImmutableArray<TextChange> GetTextChangesForIndexer(IndexerDeclarationSyntax indexerNode)
        {
            // 1. If there is no AccessorList.
            if (indexerNode.AccessorList == null || indexerNode.AccessorList.IsMissing)
            {
                return CreateInsertBracePairChange(indexerNode.Span.End);
            }

            // 2. In such case, where the indexer is the last member of its parent
            // class Bar
            // {
            //      int this[this i]$$
            // }
            // parser would think the last close brace is a part of AccessorList in indexer declaration, not the close brace of
            // class Bar.
            // We still insert the brace pair for this case.
            var parent = indexerNode.Parent;
            var accessorList = indexerNode.AccessorList;
            if (accessorList.OpenBraceToken.IsMissing
                && !accessorList.CloseBraceToken.IsMissing
                && parent is TypeDeclarationSyntax { OpenBraceToken: { IsMissing: false }, CloseBraceToken: { IsMissing: true } } typeDeclarationNode)
            {
                var members = typeDeclarationNode.Members;
                return members.Last().Equals(indexerNode)
                    ? CreateInsertBracePairChange(indexerNode.ParameterList.Span.End)
                    : TextChange.NoChanges.ToImmutableArray();
            }

            return TextChange.NoChanges.ToImmutableArray();
        }

        private static bool TryGetInsertPositionForEmbeddedStatementOwner(SyntaxNode node, out int? insertPosition)
        {
            insertPosition = null;
            if (node.IsEmbeddedStatementOwner())
            {
                var statement = node.GetEmbeddedStatement();

                // TODO: This would cause some expected behavior,
                // for example,
                // 'if (tr$$ue) Bar();'
                // would become 'if (tr$$ue) {} Bar();
                // Try to consider a better solution
                var insertBrace = statement == null
                    || statement.IsMissing
                    || HasNoBrace(statement);

                if (insertBrace)
                {
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

                        insertPosition = elseClauseSyntax.ElseKeyword.Span.End;
                        return true;
                    }
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
            if (node is ObjectCreationExpressionSyntax { Initializer: null } objectExpression)
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
        /// All the syntax nodes that should be checked
        /// </summary>
        private static bool SupportedSyntaxNode(SyntaxNode node)
            => node is NamespaceDeclarationSyntax
                   or BaseTypeDeclarationSyntax
                   or BaseMethodDeclarationSyntax
                   or FieldDeclarationSyntax
                   or PropertyDeclarationSyntax
                   or EventFieldDeclarationSyntax
                   or EventDeclarationSyntax
                   or IndexerDeclarationSyntax
                   or LocalFunctionStatementSyntax
                   or ObjectCreationExpressionSyntax
               || node.IsEmbeddedStatementOwner();

        private static bool TryGetNextCaretPosition(SyntaxNode node, out int? nextCaretPosition)
        {
            nextCaretPosition = null;
            SyntaxNode? nodeWithBraces = null;
            if (node is NamespaceDeclarationSyntax or BaseTypeDeclarationSyntax)
            {
                nodeWithBraces = node;
            }

            if (node is BaseMethodDeclarationSyntax methodDeclarationNode)
            {
                nodeWithBraces = methodDeclarationNode.Body;
            }

            if (node is EventDeclarationSyntax eventDeclarationNode)
            {
                nodeWithBraces = eventDeclarationNode.AccessorList;
            }

            if (node is IndexerDeclarationSyntax indexerDeclarationNode)
            {
                nodeWithBraces = indexerDeclarationNode.AccessorList;
            }

            if (node is PropertyDeclarationSyntax propertyDeclarationNode)
            {
                nodeWithBraces = propertyDeclarationNode.AccessorList;
            }

            var (openBrace, _) = nodeWithBraces.GetBraces();
            if (!openBrace.IsMissing)
            {
                nextCaretPosition = openBrace.Span.End + 1;
                return true;
            }

            return false;
        }

        private static bool ValidateTextChange(SyntaxNode originalNode, ImmutableArray<TextChange> textChanges, CSharpParseOptions parseOptions)
        {
            var nodeText = originalNode.GetText().WithChanges(textChanges).ToString();
            SyntaxNode? newNode = originalNode switch
            {
                NamespaceDeclarationSyntax or BaseTypeDeclarationSyntax => SyntaxFactory.ParseCompilationUnit(nodeText, options: parseOptions),
                BaseMethodDeclarationSyntax or EventFieldDeclarationSyntax or IndexerDeclarationSyntax or LocalFunctionStatementSyntax
                    => SyntaxFactory.ParseCompilationUnit(WrapInContext(originalNode, nodeText), options: parseOptions),
                ObjectCreationExpressionSyntax => SyntaxFactory.ParseExpression(nodeText, options: parseOptions),
                _ when originalNode.IsEmbeddedStatementOwner() => SyntaxFactory.ParseCompilationUnit(WrapInContext(originalNode, nodeText), options: parseOptions),
                _ => null
            };

            if (newNode != null && !newNode.HasDiagnostics())
            {
                return true;
            }

            return false;

            static string WrapInContext(SyntaxNode node, string nodeText)
                => node switch
                {
                    BaseMethodDeclarationSyntax or EventFieldDeclarationSyntax or IndexerDeclarationSyntax
                        => $"class Bar {{ {nodeText} }}",
                    LocalFunctionStatementSyntax or _ when node.IsEmbeddedStatementOwner()
                        => $"class Bar {{ void Foo() {{ {nodeText} }}}}",
                    _ => nodeText
                };
        }

    }
}
