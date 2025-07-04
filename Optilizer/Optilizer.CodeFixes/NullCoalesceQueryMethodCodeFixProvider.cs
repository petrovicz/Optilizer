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

            // Use semantic model to determine if left is value type or reference type
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var leftSymbol = semanticModel.GetTypeInfo(left, cancellationToken).Type;
            bool isNullable = leftSymbol != null && leftSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
            bool isReference = leftSymbol != null && leftSymbol.IsReferenceType;

            ExpressionSyntax checkExpr;
            ExpressionSyntax valueExpr;
            if (isNullable)
            {
                checkExpr = SyntaxFactory.ParseExpression($"{identifier}.HasValue");
                valueExpr = SyntaxFactory.ParseExpression($"{identifier}.Value");
            }
            else if (isReference)
            {
                checkExpr = SyntaxFactory.ParseExpression($"{identifier} != null");
                valueExpr = SyntaxFactory.ParseExpression($"{identifier}");
            }
            else
            {
                // fallback, keep original
                return document;
            }

            var containsCall = SyntaxFactory.ParseExpression($"{listExpr}.Contains({valueExpr})");
            var andExpr = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, checkExpr, containsCall)
                .WithTriviaFrom(invocationExpr);

			// Check if the invocation is negated (e.g., !list.Contains(x ?? 0))
			// or part of a binary expression (e.g., list.Contains(x ?? 0) || otherCondition)
			// If so, we need to wrap the expression in parentheses to preserve the original logic
			var parent = invocationExpr.Parent;
            bool isNegated = parent is PrefixUnaryExpressionSyntax prefix && prefix.IsKind(SyntaxKind.LogicalNotExpression);
			bool isPartOfBinaryExpression = parent is BinaryExpressionSyntax binary;
			bool needsParens = isNegated || isPartOfBinaryExpression;

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
