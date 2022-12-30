using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace BitFieldGenerator.DataModels
{
    public class Field
    {
        public TypeSyntax Type { get; }
        public string Name { get; }
        public SyntaxTriviaList LeadingTrivia { get; }
        public SyntaxTriviaList TrailingTrivia { get; }
        public int Bits => _bits.Value;

        private int? _bits;

        public Field(FieldDeclarationSyntax d)
        {
            Type = d.Declaration.Type;
            Name = d.Declaration.Variables[0].Identifier.Text;
            LeadingTrivia = d.GetLeadingTrivia();
            TrailingTrivia = d.GetTrailingTrivia();
            _bits = FindBitFieldAttribute(d);
        }

        /// <summary>
        /// find N from
        /// <code><![CDATA[
        /// [BitField(N)]
        /// SomeType SomeField;
        /// ]]>
        /// </code>
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        private static int? FindBitFieldAttribute(FieldDeclarationSyntax d)
            => (
            from list in d.AttributeLists
            from a in list.Attributes
            where a.Name.ToString().Contains("BitField")
            from arg in a.ArgumentList.Arguments
            let x = arg.Expression as LiteralExpressionSyntax
            where x != null && x.IsKind(SyntaxKind.NumericLiteralExpression)
            select ParseOrDefault(x.Token.Text)
            )
            .FirstOrDefault();

        public static IEnumerable<Field> New(StructDeclarationSyntax decl)
            => decl.Members.OfType<FieldDeclarationSyntax>().Select(d => new Field(d)).Where(f => f._bits.HasValue);

        private static int? ParseOrDefault(string s)
        {
            int x;
            return int.TryParse(s, out x) ? x : default(int?);
        }
    }
}
