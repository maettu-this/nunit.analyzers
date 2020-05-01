using Microsoft.CodeAnalysis;

namespace NUnit.Analyzers.TestCaseSourceUsage
{
    internal sealed class SourceAttributeInformation
    {
        public INamedTypeSymbol SourceType { get; }
        public string? SourceName { get; }
        public SyntaxNode? SyntaxNode { get; }
        public bool IsStringLiteral { get; }
        public int? NumberOfMethodParameters { get; }

        public SourceAttributeInformation(
            INamedTypeSymbol sourceType,
            string? sourceName,
            SyntaxNode? syntaxNode,
            bool isStringLiteral,
            int? numberOfMethodParameters)
        {
            this.SourceType = sourceType;
            this.SourceName = sourceName;
            this.SyntaxNode = syntaxNode;
            this.IsStringLiteral = isStringLiteral;
            this.NumberOfMethodParameters = numberOfMethodParameters;
        }
    }
}
