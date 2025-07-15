using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Optilizer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullCoalesceQueryExpressionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NC002";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Avoid ?? inside Contains in LINQ query where clause",
			"Using ?? here causes EF to generate a COALESCE SQL command, which prevents optimal index usage and can severely impact query performance.",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.QueryExpression);
        }

        private void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
        {
            var queryExpr = (QueryExpressionSyntax)context.Node;
            // Find all where clauses
            var whereClauses = queryExpr.Body.Clauses.OfType<WhereClauseSyntax>();
            foreach (var whereClause in whereClauses)
            {
                // Find all Contains calls in the where clause
                var containsCalls = whereClause.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(node => IsContainsMethod(node, context.SemanticModel));
                foreach (var containsCall in containsCalls)
                {
                    // Find all '??' usages inside this Contains call
                    var nullCoalescings = containsCall.DescendantNodes()
                        .OfType<BinaryExpressionSyntax>()
                        .Where(binExp => binExp.IsKind(SyntaxKind.CoalesceExpression));
                    foreach (var nullCoalescing in nullCoalescings)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Rule, nullCoalescing.GetLocation()));
                    }
                }
            }
        }

        private bool IsContainsMethod(InvocationExpressionSyntax invocationExpr, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetSymbolInfo(invocationExpr.Expression).Symbol as IMethodSymbol;
            return symbol?.Name == "Contains";
        }
    }
}
