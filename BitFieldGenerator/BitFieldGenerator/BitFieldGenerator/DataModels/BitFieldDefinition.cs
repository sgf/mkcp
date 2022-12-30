using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace BitFieldGenerator.DataModels
{
    public class BitFieldDefinition
    {
        public IReadOnlyList<Field> Properties { get; }

        public BitFieldDefinition(StructDeclarationSyntax decl)
        {
            Properties = Field.New(decl).ToArray();
        }
    }
}
