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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullCoalesceQueryAlwaysContainsCodeFixProvider)), Shared]
    public class NullCoalesceQueryAlwaysContainsCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(
                NullCoalesceQueryMethodAnalyzer.DiagnosticId, // NC001
                NullCoalesceQueryExpressionAnalyzer.DiagnosticId // NC002
            );

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
                    title: "Refactor by keeping conditions",
                    createChangedDocument: c => ApplyFixAsync(context.Document, invocation, c),
                    equivalenceKey: "RefactorNullCoalescingAlwaysContains"),
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
            var right = arg.Right;
            var identifier = left.ToString(); // e.g., x
            var fallback = right.ToString(); // e.g., 0 or "default"

            // Find the list expression (list.Contains)
            var memberAccess = invocationExpr.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
                return document;
            var listExpr = memberAccess.Expression.ToString();

            // Use semantic model to determine if left is value type or reference type
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var leftSymbol = semanticModel.GetTypeInfo(left, cancellationToken).Type;
            bool isNullable = leftSymbol != null && leftSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
            bool isReference = leftSymbol != null && leftSymbol.IsReferenceType;

            ExpressionSyntax newExpr;
            if (isNullable)
            {
                // ((x.HasValue && list.Contains(x.Value)) || (!x.HasValue && list.Contains(fallback)))
                var hasValue = SyntaxFactory.ParseExpression($"{identifier}.HasValue");
                var notHasValue = SyntaxFactory.ParseExpression($"!{identifier}.HasValue");
                var containsValue = SyntaxFactory.ParseExpression($"{listExpr}.Contains({identifier}.Value)");
                var containsFallback = SyntaxFactory.ParseExpression($"{listExpr}.Contains({fallback})");
                var and1 = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, hasValue, containsValue);
                var and2 = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, notHasValue, containsFallback);
                // Parenthesize both sides of the OR
                var parenAnd1 = SyntaxFactory.ParenthesizedExpression(and1);
                var parenAnd2 = SyntaxFactory.ParenthesizedExpression(and2);
                newExpr = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalOrExpression, parenAnd1, parenAnd2);
            }
            else if (isReference)
            {
                // ((x != null && list.Contains(x)) || (x == null && list.Contains(fallback)))
                var notNull = SyntaxFactory.ParseExpression($"{identifier} != null");
                var isNull = SyntaxFactory.ParseExpression($"{identifier} == null");
                var containsValue = SyntaxFactory.ParseExpression($"{listExpr}.Contains({identifier})");
                var containsFallback = SyntaxFactory.ParseExpression($"{listExpr}.Contains({fallback})");
                var and1 = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, notNull, containsValue);
                var and2 = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, isNull, containsFallback);
                // Parenthesize both sides of the OR
                var parenAnd1 = SyntaxFactory.ParenthesizedExpression(and1);
                var parenAnd2 = SyntaxFactory.ParenthesizedExpression(and2);
                newExpr = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalOrExpression, parenAnd1, parenAnd2);
            }
            else
            {
                // fallback, keep original
                return document;
            }

            // Parenthesize the whole expression for clarity and precedence
            newExpr = SyntaxFactory.ParenthesizedExpression(newExpr).WithTriviaFrom(invocationExpr);

            // Replace the invocation expression (list.Contains(x ?? 0)) with the new expression
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(invocationExpr, newExpr);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
