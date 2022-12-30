using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using BitFieldGenerator.DataModels;
using Microsoft.CodeAnalysis.Formatting;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace BitFieldGenerator
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BitFieldGeneratorCodeFixProvider)), Shared]
    public class BitFieldGeneratorCodeFixProvider : CodeFixProvider
    {
        private const string title = "Generate Bit Fields";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(BitFieldGeneratorAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start) // cursor position
                .Parent.AncestorsAndSelf().OfType<StructDeclarationSyntax>().First() // struct BitFields
                .Parent as StructDeclarationSyntax; // a struct which contains the BitFields

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedSolution: c => GenerateValueChanged(context.Document, declaration, c),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Solution> GenerateValueChanged(Document document, StructDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            document = await AddPartialModifier(document, classDecl, cancellationToken);
            document = await AddNewDocument(document, classDecl, cancellationToken);
            return document.Project.Solution;
        }

        private static async Task<Document> AddPartialModifier(Document document, StructDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var newTypeDecl = typeDecl.AddPartialModifier();

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) as CompilationUnitSyntax;
            var newRoolt = root.ReplaceNode(typeDecl, newTypeDecl)
                .WithAdditionalAnnotations(Formatter.Annotation);

            document = document.WithSyntaxRoot(newRoolt);
            return document;
        }

        private static async Task<Document> AddNewDocument(Document document, StructDeclarationSyntax typeDecl, CancellationToken cancellationToken)

        {
            var newRoot = await GeneratePartialDeclaration(document, typeDecl, cancellationToken);

            var name = typeDecl.Identifier.Text;
            var generatedName = name + ".BitFields.cs";

            var project = document.Project;

            var existed = project.Documents.FirstOrDefault(d => d.Name == generatedName);
            if (existed != null) return existed.WithSyntaxRoot(newRoot);
            else return project.AddDocument(generatedName, newRoot, document.Folders);
        }

        private static async Task<CompilationUnitSyntax> GeneratePartialDeclaration(Document document, StructDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var bitFieldDecl = (StructDeclarationSyntax)classDecl.ChildNodes().First(x => x is StructDeclarationSyntax);

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var ti = semanticModel.GetTypeInfo(bitFieldDecl);

            var def = new BitFieldDefinition(bitFieldDecl);
            var generatedNodes = GetGeneratedNodes(def).ToArray();

            var newClassDecl = classDecl.GetPartialTypeDelaration()
                .AddMembers(generatedNodes)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var ns = classDecl.FirstAncestorOrSelf<NamespaceDeclarationSyntax>()?.Name.WithoutTrivia().GetText().ToString();

            MemberDeclarationSyntax topDecl;
            if (ns != null)
            {
                topDecl = NamespaceDeclaration(IdentifierName(ns))
                    .AddMembers(newClassDecl)
                    .WithAdditionalAnnotations(Formatter.Annotation);
            }
            else
            {
                topDecl = newClassDecl;
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) as CompilationUnitSyntax;

            return CompilationUnit().AddUsings(root.Usings.ToArray())
                .AddMembers(topDecl)
                .WithTrailingTrivia(CarriageReturnLineFeed)
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        private static IEnumerable<MemberDeclarationSyntax> GetGeneratedNodes(BitFieldDefinition def)
        {
            int position = 0;
            foreach (var p in def.Properties)
                foreach (var s in WithTrivia(GetGeneratedMember(p, ref position), p.LeadingTrivia, p.TrailingTrivia))
                    yield return s;

            //todo: constructor
            //↓ sample
            /*
        public short X => (short)((_value >> 0) & 0x3FF);

        public byte Y => (byte)((_value >> 10) & 0x3);

        public short Z => (short)((_value >> 12) & 0xFFF);

        public int W => (int)((_value >> 24) & 0xFFFFFF);

        public MyCode(short x, byte y, short z, int w)
        {
            _value = 0;
            _value |= (long)(x & 0x3FF) << 0;
            _value |= (long)(y & 0x3) << 10;
            _value |= (long)(z & 0xFFF) << 12;
            _value |= (long)(w & 0xFFFFFF) << 24;
        }
             */
        }

        private static IEnumerable<MemberDeclarationSyntax> WithTrivia(IEnumerable<MemberDeclarationSyntax> members, SyntaxTriviaList leadingTrivia, SyntaxTriviaList trailingTrivia)
        {
            var array = members.ToArray();

            if (array.Length == 0) yield break;

            if (array.Length == 1)
            {
                yield return array[0]
                    .WithLeadingTrivia(leadingTrivia)
                    .WithTrailingTrivia(trailingTrivia);

                yield break;
            }

            yield return array[0].WithLeadingTrivia(leadingTrivia);

            for (int i = 1; i < array.Length - 1; i++)
                yield return array[i];

            yield return array[array.Length - 1].WithTrailingTrivia(trailingTrivia);
        }

        private static IEnumerable<MemberDeclarationSyntax> GetGeneratedMember(Field p, ref int position)
        {
            var name = p.Name;
            var typeName = p.Type.WithoutTrivia().GetText().ToString();
            var bits = p.Bits;
            var mask = (1 << bits) - 1;

            var source = $@"        public {typeName} {name} => ({typeName})((_value >> {position}) & {"0x" + mask.ToString("X")});
";

            var generatedNodes = CSharpSyntaxTree.ParseText(source)
                .GetRoot().ChildNodes()
                .OfType<MemberDeclarationSyntax>()
                .ToArray();

            position += p.Bits;
            return generatedNodes;
        }
    }
}