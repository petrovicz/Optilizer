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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeometryUnionInLoopCodeFixProvider)), Shared]
    public class GeometryUnionInLoopCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(GeometryUnionInLoopAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken)
                .ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the Union invocation
            var unionInvocation = root.FindNode(diagnosticSpan) as InvocationExpressionSyntax;
            if (unionInvocation == null) return;

            // Find the containing loop statement
            var loopStatement = unionInvocation.FirstAncestorOrSelf<StatementSyntax>(node =>
                node is ForStatementSyntax || 
                node is ForEachStatementSyntax || 
                node is WhileStatementSyntax);

            if (loopStatement == null) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Replace with CascadedPolygonUnion for better performance",
                    createChangedDocument: c => ReplaceWithCascadedUnion(context.Document, loopStatement, unionInvocation, c),
                    equivalenceKey: "ReplaceBatchUnion"),
                diagnostic);
        }

        private async Task<Document> ReplaceWithCascadedUnion(
            Document document, 
            StatementSyntax loopStatement, 
            InvocationExpressionSyntax unionInvocation,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            // Analyze the loop pattern to understand how Union is being used
            var (collectionVariable, accumulatorVariable) = AnalyzeLoopPattern(loopStatement, unionInvocation);

            if (collectionVariable == null)
                return document; // Can't determine pattern, no fix available

            // Create the replacement code - simple assignment to the accumulator variable
            var replacementCode = accumulatorVariable != null
                ? $"{accumulatorVariable} = CascadedPolygonUnion.Union({collectionVariable});"
                : $"var result = CascadedPolygonUnion.Union({collectionVariable});";

            var replacementStatement = SyntaxFactory.ParseStatement(replacementCode)
                .WithLeadingTrivia(loopStatement.GetLeadingTrivia())
                .WithTrailingTrivia(loopStatement.GetTrailingTrivia());

            // Replace the loop with the new statement
            var newRoot = root.ReplaceNode(loopStatement, replacementStatement);
            
            // Add using directive if needed
            newRoot = AddUsingDirectiveIfNeeded(newRoot, "NetTopologySuite.Operation.Union");

            return document.WithSyntaxRoot(newRoot);
        }

        private (string collectionVariable, string accumulatorVariable) AnalyzeLoopPattern(
            StatementSyntax loopStatement, 
            InvocationExpressionSyntax unionInvocation)
        {
            string collectionVariable = null;
            string accumulatorVariable = null;

            switch (loopStatement)
            {
                case ForEachStatementSyntax forEachLoop:
                    collectionVariable = forEachLoop.Expression.ToString();
                    
                    // Look for assignment pattern like: result = result.Union(item)
                    var assignment = unionInvocation.FirstAncestorOrSelf<AssignmentExpressionSyntax>();
                    if (assignment != null && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
                    {
                        accumulatorVariable = assignment.Left.ToString();
                    }
                    break;

                case ForStatementSyntax forLoop:
                    // For loops are more complex, try to identify the collection being iterated
                    var loopBody = forLoop.Statement;
                    var elementAccess = loopBody.DescendantNodes()
                        .OfType<ElementAccessExpressionSyntax>()
                        .FirstOrDefault();
                    
                    if (elementAccess != null)
                    {
                        collectionVariable = elementAccess.Expression.ToString();
                    }

                    // Look for assignment pattern
                    var forAssignment = unionInvocation.FirstAncestorOrSelf<AssignmentExpressionSyntax>();
                    if (forAssignment != null && forAssignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
                    {
                        accumulatorVariable = forAssignment.Left.ToString();
                    }
                    break;

                case WhileStatementSyntax whileLoop:
                    // While loops are complex, try basic pattern detection
                    var whileAssignment = unionInvocation.FirstAncestorOrSelf<AssignmentExpressionSyntax>();
                    if (whileAssignment != null && whileAssignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
                    {
                        accumulatorVariable = whileAssignment.Left.ToString();
                    }
                    break;
            }

            return (collectionVariable, accumulatorVariable);
        }



        private SyntaxNode AddUsingDirectiveIfNeeded(SyntaxNode root, string namespaceName)
        {
            if (!(root is CompilationUnitSyntax compilationUnit))
                return root;

            // Check if the using directive already exists
            var existingUsing = compilationUnit.Usings
                .FirstOrDefault(u => u.Name.ToString() == namespaceName);

            if (existingUsing != null)
                return root; // Already exists

            // Add the using directive
            var newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName));
            var newUsings = compilationUnit.Usings.Add(newUsing);

            return compilationUnit.WithUsings(newUsings);
        }
    }
}