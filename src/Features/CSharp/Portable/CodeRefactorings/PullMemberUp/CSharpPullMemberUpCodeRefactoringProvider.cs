// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp;
using Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp.Dialog;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.CodeRefactorings.PullMemberUp
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(PredefinedCodeRefactoringProviderNames.PullMemberUp)), Shared]
    internal class CSharpPullMemberUpCodeRefactoringProvider : AbstractPullMemberUpRefactoringProvider
    {
        /// <summary>
        /// Test purpose only.
        /// </summary>
        internal CSharpPullMemberUpCodeRefactoringProvider(IPullMemberUpOptionsService service) : base(service)
        {
        }

        internal CSharpPullMemberUpCodeRefactoringProvider() : base(null)
        {
        }

        protected override bool IsSelectionValid(TextSpan span, SyntaxNode selectedNode)
        {
            var identifier = GetIdentifier(selectedNode);
            if (identifier == default)
            {
                return false;
            }
            else if (identifier.FullSpan.Contains(span) && span.Contains(identifier.Span))
            {
                // Selection lies within the identifier's span
                return true;
            }
            else if (identifier.Span.Contains(span) && span.Length == 0)
            {
                // Cursor stands on the identifier
                return true;
            }
            else
            {
                return false;
            }
        }

        private SyntaxToken GetIdentifier(SyntaxNode selectedNode)
        {
            switch (selectedNode)
            {
                case MemberDeclarationSyntax memberDeclarationSyntax:
                    // Nested type is checked in before this method is called.
                    return memberDeclarationSyntax.GetNameToken();
                case VariableDeclaratorSyntax variableDeclaratorSyntax:
                    // It handles multiple fields or events declared in one line
                    return variableDeclaratorSyntax.Identifier;
                default:
                    return default;
            }
        }

        protected async override Task<SyntaxNode> GetMemberSyntaxNode(
            Document document,
            TextSpan span,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var token = root.FindToken(span.Start);
            // Return the method declaration node when the cursor sits on the open parentheses.
            // e.g. TestMethod[||]() is valid but TestMethod  [||]() is invalid
            if (span.Length == 0 && token.IsKind(SyntaxKind.OpenParenToken))
            {
                var previousToken = token.GetPreviousToken();
                if (previousToken.IsKind(SyntaxKind.IdentifierToken))
            }
            else
            {
                return root;
            }
        }
    }
}
