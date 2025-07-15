using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Optilizer
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class NullCoalesceQueryMethodAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "NC001";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			DiagnosticId,
			"Avoid ?? inside Contains within IQueryable.Where",
			"Using ?? here causes EF to generate a COALESCE SQL command, which prevents optimal index usage and can severely impact query performance.",
			"Usage",
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.InvocationExpression);
		}

		private void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
		{
			var invocationExpr = (InvocationExpressionSyntax)context.Node;

			// Check if it's a 'Where' call
			if (!IsQueryableWhereMethod(invocationExpr, context.SemanticModel)) return;

			// Find all 'Contains' calls inside the Where
			var containsCalls = invocationExpr.DescendantNodes()
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

		private bool IsQueryableWhereMethod(InvocationExpressionSyntax invocationExpr, SemanticModel semanticModel)
		{
			var symbol = semanticModel.GetSymbolInfo(invocationExpr.Expression).Symbol as IMethodSymbol;

			if (symbol == null)
				return false;

			return symbol.Name == "Where" && symbol.ContainingType.ToString().StartsWith("System.Linq.Queryable");
		}

		private bool IsContainsMethod(InvocationExpressionSyntax invocationExpr, SemanticModel semanticModel)
		{
			var symbol = semanticModel.GetSymbolInfo(invocationExpr.Expression).Symbol as IMethodSymbol;
			return symbol?.Name == "Contains";
		}
	}
}
