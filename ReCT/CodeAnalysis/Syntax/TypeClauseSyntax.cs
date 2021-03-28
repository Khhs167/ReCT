namespace ReCT.CodeAnalysis.Syntax
{
    public sealed class TypeClauseSyntax : SyntaxNode
    {
        public TypeClauseSyntax(SyntaxTree syntaxTree, SyntaxToken identifier)
            : base(syntaxTree)
        {
            Identifier = identifier;
        }

        public override SyntaxKind Kind => SyntaxKind.TypeClause;
        public SyntaxToken Identifier { get; }
    }
}