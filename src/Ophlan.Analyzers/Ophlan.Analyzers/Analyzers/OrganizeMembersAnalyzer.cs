using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace Ophlan.Analyzers.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OrganizeMembersAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OphlanAnalyzersOrganizeMembers";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.OrganizeMembersAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.OrganizeMembersAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.OrganizeMembersAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Ophlan";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            var members = namedTypeSymbol.GetMembers().Where(x => !x.IsImplicitlyDeclared && x.CanBeReferencedByName).ToArray();

            var orderedMembers = members.OrderBy(x => x.DeclaredAccessibility).ThenBy(x => x.Kind).ThenBy(x => x.Name).ToArray();

            var alphabetized = true;

            for (var i = 0; i < members.Length; i++)
            {
                if(orderedMembers[i] != members[i])
                {
                    alphabetized = false;
                }
            }

            if (!alphabetized)
            {
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
