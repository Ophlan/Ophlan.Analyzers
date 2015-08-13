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
using Ophlan.Analyzers.Analyzer;

namespace Ophlan.Analyzers.CodeFix
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OrganizeMembersCodeFixProvider)), Shared]
    public class OrganizeMembersCodeFixProvider : CodeFixProvider
    {
        private const string title = "Alphabetize Members";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(OrganizeMembersAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

            if (declaration != null)
            {
                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        c => AlphabetizeMembersAsync(context.Document, declaration, c),
                        equivalenceKey: title),
                    diagnostic);
            }
        }

        private async Task<Document> AlphabetizeMembersAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetSymbolInfo(typeDecl).Symbol as INamedTypeSymbol;
            
            var members = typeDecl.Members.ToArray();
            members = members.OrderBy(x => GetDeclaredAccessibility(x)).ThenBy(x => x.Kind()).ThenBy(x => GetName(x)).ToArray();

            var first = members.First();

            var rest = members.Where(x => x != first);

            var newTypeDecl = typeDecl.RemoveNodes(rest, SyntaxRemoveOptions.KeepNoTrivia);
            
            newTypeDecl = newTypeDecl.InsertNodesAfter(newTypeDecl.Members.First(), rest);
            
            var root = await document.GetSyntaxRootAsync();
            var newRoot = root.ReplaceNode(typeDecl, newTypeDecl);

            var newDocument = document.WithSyntaxRoot(newRoot);

            return newDocument;
        }

        SyntaxKind[] accessibilityTypes = new SyntaxKind[] { SyntaxKind.PublicKeyword, SyntaxKind.PrivateKeyword, SyntaxKind.InternalKeyword, SyntaxKind.ProtectedKeyword };

        private Accessibility GetDeclaredAccessibility(MemberDeclarationSyntax declaration)
        {
            var result = Accessibility.Private;

            SyntaxTokenList? syntaxTokenList;
            syntaxTokenList = (declaration as PropertyDeclarationSyntax)?.Modifiers ??
                              (declaration as FieldDeclarationSyntax)?.Modifiers ??
                              (declaration as ConstructorDeclarationSyntax)?.Modifiers ??
                              (declaration as MethodDeclarationSyntax)?.Modifiers;
            var accessibilityToken = syntaxTokenList?.Where(x => accessibilityTypes.Contains(x.Kind())).FirstOrDefault();
            if (accessibilityToken.HasValue)
            {
                switch (accessibilityToken.Value.Kind())
                {
                    case SyntaxKind.PublicKeyword:
                        result = Accessibility.Public;
                        break;
                    case SyntaxKind.PrivateKeyword:
                        result = Accessibility.Private;
                        break;
                    case SyntaxKind.InternalKeyword:
                        result = Accessibility.Internal;
                        break;
                    case SyntaxKind.ProtectedKeyword:
                        result = Accessibility.Protected;
                        break;
                    default:
                        result = Accessibility.Private;
                        break;
                }
            }

            return result;
        }

        private string GetName(MemberDeclarationSyntax declaration)
        {
            var result = string.Empty;

            result = (declaration as PropertyDeclarationSyntax)?.Identifier.ValueText ??
                              (declaration as FieldDeclarationSyntax)?.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText ??
                              (declaration as ConstructorDeclarationSyntax)?.Identifier.ValueText ??
                              (declaration as MethodDeclarationSyntax)?.Identifier.ValueText;
            
            return result;
        }
    }
}