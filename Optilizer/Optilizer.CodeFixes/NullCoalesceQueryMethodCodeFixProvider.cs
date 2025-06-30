using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Optilizer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullCoalesceQueryMethodCodeFixProvider)), Shared]
    public class NullCoalesceQueryMethodCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(NullCoalesceQueryMethodAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken)
                .ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the InvocationExpressionSyntax for list.Contains(x ?? 0)
            var node = root.FindNode(diagnosticSpan);
            var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation == null) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Refactor to HasValue and Value usage",
                    createChangedDocument: c => ApplyFixAsync(context.Document, invocation, c),
                    equivalenceKey: "RefactorNullCoalescing"),
                diagnostic);
        }

        private async Task<Document> ApplyFixAsync(Document document, InvocationExpressionSyntax invocationExpr, CancellationToken cancellationToken)
        {
            // Find the argument that is a coalesce expression (x ?? 0)
            if (invocationExpr.ArgumentList.Arguments.Count != 1)
                return document;
            var arg = invocationExpr.ArgumentList.Arguments[0].Expression as BinaryExpressionSyntax;
            if (arg == null || arg.Kind() != SyntaxKind.CoalesceExpression)
                return document;

            var left = arg.Left;
            var identifier = left.ToString(); // e.g., x

            // Find the list expression (list.Contains)
            var memberAccess = invocationExpr.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
                return document;
            var listExpr = memberAccess.Expression.ToString();

            // Build x.HasValue && list.Contains(x.Value)
            var hasValueCheck = SyntaxFactory.ParseExpression($"{identifier}.HasValue");
            var containsCall = SyntaxFactory.ParseExpression($"{listExpr}.Contains({identifier}.Value)");
            var andExpr = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, hasValueCheck, containsCall)
                .WithTriviaFrom(invocationExpr);

            // Check if the invocation is part of any binary expression (e.g., &&, ||, etc.)
            var parent = invocationExpr.Parent;
            bool needsParens = parent is BinaryExpressionSyntax;
            ExpressionSyntax newExpr = andExpr;
            if (needsParens)
            {
                newExpr = SyntaxFactory.ParenthesizedExpression(andExpr).WithTriviaFrom(invocationExpr);
            }

            // Replace the invocation expression (list.Contains(x ?? 0)) with the new expression
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(invocationExpr, newExpr);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
