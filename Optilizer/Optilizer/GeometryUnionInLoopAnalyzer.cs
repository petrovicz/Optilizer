using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Optilizer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class GeometryUnionInLoopAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "GIS001";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Avoid calling Geometry.Union inside loops",
            "Calling Geometry.Union repeatedly in a loop has poor performance. Consider using CascadedPolygonUnion.Union for batch operations.",
            "Performance",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeForStatement, SyntaxKind.ForStatement);
            context.RegisterSyntaxNodeAction(AnalyzeForEachStatement, SyntaxKind.ForEachStatement);
            context.RegisterSyntaxNodeAction(AnalyzeWhileStatement, SyntaxKind.WhileStatement);
        }

        private void AnalyzeForStatement(SyntaxNodeAnalysisContext context)
        {
            var forStatement = (ForStatementSyntax)context.Node;
            AnalyzeLoopBody(context, forStatement.Statement);
        }

        private void AnalyzeForEachStatement(SyntaxNodeAnalysisContext context)
        {
            var forEachStatement = (ForEachStatementSyntax)context.Node;
            AnalyzeLoopBody(context, forEachStatement.Statement);
        }

        private void AnalyzeWhileStatement(SyntaxNodeAnalysisContext context)
        {
            var whileStatement = (WhileStatementSyntax)context.Node;
            AnalyzeLoopBody(context, whileStatement.Statement);
        }

        private void AnalyzeLoopBody(SyntaxNodeAnalysisContext context, StatementSyntax loopBody)
        {
            if (loopBody == null) return;

            // Find all invocation expressions in the loop body
            var invocations = loopBody.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => IsGeometryUnionCall(invocation, context.SemanticModel));

            foreach (var invocation in invocations)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            }
        }

        private bool IsGeometryUnionCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (!(symbolInfo.Symbol is IMethodSymbol methodSymbol))
                return false;

            // Check if the method name is "Union"
            if (methodSymbol.Name != "Union")
                return false;

            // Check if the containing type is a Geometry type from NetTopologySuite
            var containingType = methodSymbol.ContainingType;
            if (containingType == null)
                return false;

            // Check if it's from NetTopologySuite.Geometries namespace
            return IsNetTopologySuiteGeometryType(containingType);
        }

        private bool IsNetTopologySuiteGeometryType(INamedTypeSymbol typeSymbol)
        {
            // Check if the type is in NetTopologySuite.Geometries namespace
            if (typeSymbol.ContainingNamespace?.ToDisplayString() == "NetTopologySuite.Geometries")
                return true;

            // Check base types recursively
            var baseType = typeSymbol.BaseType;
            while (baseType != null)
            {
                if (baseType.ContainingNamespace?.ToDisplayString() == "NetTopologySuite.Geometries" &&
                    baseType.Name == "Geometry")
                    return true;
                baseType = baseType.BaseType;
            }

            return false;
        }
    }
}