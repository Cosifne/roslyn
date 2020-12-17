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
            if (!TryGetSupportedNode(root, currentCaret.Value, out var supportedNode))
            {
                nextCommandHandler();
                return;
            }

            using var transaction = args.TextView.CreateEditTransaction(nameof(BraceCompletionCommandHandler), _textUndoHistoryRegistry, _editorOperationsFactoryService);

            // Insert '{}' to the document
            var insertPosition = supportedNode!.Span.End;
            var newDocument = document.InsertText(insertPosition, s_bracePair, cancellationToken);

            // Place caret between '{$$}'
            args.TextView.TryMoveCaretToAndEnsureVisible(
                new SnapshotPoint(args.SubjectBuffer.CurrentSnapshot, insertPosition + 1));

            // Format
            Format(newDocument, insertPosition, cancellationToken);

            // Hit enter
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

        private static bool TryGetSupportedNode(
            SyntaxNode root,
            int caretPosition,
            out SyntaxNode? supportedNode)
        {
            supportedNode = null;
            var token = root.FindTokenOnLeftOfPosition(caretPosition);
            if (token.IsKind(SyntaxKind.None))
            {
                return false;
            }

            // Find the innermost matching node
            // For example:
            // void Bar() { void Bar1() { void Bar2$$() }}
            // Here only 'Bar2' is needed.
            var node = token.GetAncestors(IsSupportedSyntaxNode).FirstOrDefault();
            if (node != null)
            {
                supportedNode = node;
                return true;
            }

            return false;
        }

        private static bool IsSupportedSyntaxNode(SyntaxNode node)
        {
            // 1. For syntax node like ClassDeclarationSyntax, check if the brace pair is missing
            if (IsSyntaxNodeOnlySupportsBracePair(node))
            {
                var (openBrace, closeBrace) = node.GetBraces();
                return openBrace.IsMissing && closeBrace.IsMissing;
            }

            // 2. For embedded statement owner, like if statement
            // check two things, I. Does it have correct parenthesis pair. II. does it have no statement.
            if (node.IsEmbeddedStatementOwner() && HasNoMissingParenthesis(node))
            {
                var statement = node.GetEmbeddedStatement();
                if (statement == null)
                {
                    return false;
                }

                return statement.IsMissing;
            }

            // 3. For method and local function
            // check two things, I. Does it have correct parenthesis pair. II. does it have no method body.
            if (node is MethodDeclarationSyntax methodNode && HasNoMissingParenthesis(methodNode))
            {
                return methodNode.Body == null && methodNode.ExpressionBody == null;
            }

            if (node is LocalFunctionStatementSyntax localFunctionNode && HasNoMissingParenthesis(localFunctionNode))
            {
                return localFunctionNode.Body == null && localFunctionNode.ExpressionBody == null;
            }

            return false;
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
               || node is UsingStatementSyntax;

        private static bool IsSyntaxNodeOnlySupportsBracePair(SyntaxNode n)
            => n is NamespaceDeclarationSyntax
               || n is ClassDeclarationSyntax
               || n is StructDeclarationSyntax
               || n is RecordDeclarationSyntax;
    }
}
