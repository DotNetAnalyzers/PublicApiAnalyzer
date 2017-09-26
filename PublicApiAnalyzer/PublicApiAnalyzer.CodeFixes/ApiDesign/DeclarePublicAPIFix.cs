// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer.ApiDesign
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeActions;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.Text;

    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = "DeclarePublicAPIFix")]
    [Shared]
    internal sealed class DeclarePublicAPIFix : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(RoslynDiagnosticIds.DeclarePublicApiRuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return new PublicSurfaceAreaFixAllProvider();
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var project = context.Document.Project;
            TextDocument publicSurfaceAreaDocument = GetPublicSurfaceAreaDocument(project);
            if (publicSurfaceAreaDocument == null)
            {
                return;
            }

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            foreach (var diagnostic in context.Diagnostics)
            {
                string minimalSymbolName = diagnostic.Properties[DeclarePublicAPIAnalyzer.MinimalNamePropertyBagKey];
                string publicSurfaceAreaSymbolName = diagnostic.Properties[DeclarePublicAPIAnalyzer.PublicApiNamePropertyBagKey];
                ImmutableHashSet<string> siblingSymbolNamesToRemove = diagnostic.Properties[DeclarePublicAPIAnalyzer.PublicApiNamesOfSiblingsToRemovePropertyBagKey]
                    .Split(DeclarePublicAPIAnalyzer.PublicApiNamesOfSiblingsToRemovePropertyBagValueSeparator.ToCharArray())
                    .ToImmutableHashSet();

                context.RegisterCodeFix(
                    new AdditionalDocumentChangeAction(
                        $"Add {minimalSymbolName} to public API",
                        c => this.GetFixAsync(publicSurfaceAreaDocument, publicSurfaceAreaSymbolName, siblingSymbolNamesToRemove, c)),
                    diagnostic);
            }
        }

        private static TextDocument GetPublicSurfaceAreaDocument(Project project)
        {
            return project.AdditionalDocuments.FirstOrDefault(doc => doc.Name.Equals(DeclarePublicAPIAnalyzer.UnshippedFileName, StringComparison.Ordinal));
        }

        private static SourceText AddSymbolNamesToSourceText(SourceText sourceText, IEnumerable<string> newSymbolNames)
        {
            HashSet<string> lines = GetLinesFromSourceText(sourceText);

            foreach (string name in newSymbolNames)
            {
                lines.Add(name);
            }

            var sortedLines = lines.OrderBy(s => s, StringComparer.Ordinal);

            var newSourceText = sourceText.Replace(new TextSpan(0, sourceText.Length), string.Join(Environment.NewLine, sortedLines) + GetEndOfFileText(sourceText));
            return newSourceText;
        }

        private static SourceText RemoveSymbolNamesFromSourceText(SourceText sourceText, ImmutableHashSet<string> linesToRemove)
        {
            if (linesToRemove.IsEmpty)
            {
                return sourceText;
            }

            var lines = GetLinesFromSourceText(sourceText);
            var newLines = lines.Where(line => !linesToRemove.Contains(line));

            var sortedLines = newLines.OrderBy(s => s, StringComparer.Ordinal);

            string newText = sortedLines.Any() ? string.Join(Environment.NewLine, sortedLines) + GetEndOfFileText(sourceText) : string.Empty;
            var newSourceText = sourceText.Replace(new TextSpan(0, sourceText.Length), newText);
            return newSourceText;
        }

        private static HashSet<string> GetLinesFromSourceText(SourceText sourceText)
        {
            var lines = new HashSet<string>();

            foreach (var textLine in sourceText.Lines)
            {
                string text = textLine.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text);
                }
            }

            return lines;
        }

        /// <summary>
        /// Returns the trailing newline from the end of <paramref name="sourceText"/>, if one exists.
        /// </summary>
        /// <param name="sourceText">The source text.</param>
        /// <returns><see cref="Environment.NewLine"/> if <paramref name="sourceText"/> ends with a trailing newline;
        /// otherwise, <see cref="string.Empty"/>.</returns>
        private static string GetEndOfFileText(SourceText sourceText)
        {
            if (sourceText.Length == 0)
            {
                // An empty file is treated as though it ends with an empty line
                return Environment.NewLine;
            }

            var lastLine = sourceText.Lines[sourceText.Lines.Count - 1];
            return lastLine.Span.IsEmpty ? Environment.NewLine : string.Empty;
        }

        private async Task<Solution> GetFixAsync(TextDocument publicSurfaceAreaDocument, string newSymbolName, ImmutableHashSet<string> siblingSymbolNamesToRemove, CancellationToken cancellationToken)
        {
            var sourceText = await publicSurfaceAreaDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var newSourceText = AddSymbolNamesToSourceText(sourceText, new[] { newSymbolName });
            newSourceText = RemoveSymbolNamesFromSourceText(newSourceText, siblingSymbolNamesToRemove);

            return publicSurfaceAreaDocument.Project.Solution.WithAdditionalDocumentText(publicSurfaceAreaDocument.Id, newSourceText);
        }

        private class AdditionalDocumentChangeAction : CodeAction
        {
            private readonly Func<CancellationToken, Task<Solution>> createChangedAdditionalDocument;

            public AdditionalDocumentChangeAction(string title, Func<CancellationToken, Task<Solution>> createChangedAdditionalDocument)
            {
                this.Title = title;
                this.createChangedAdditionalDocument = createChangedAdditionalDocument;
            }

            public override string Title { get; }

            public override string EquivalenceKey => this.Title;

            protected override Task<Solution> GetChangedSolutionAsync(CancellationToken cancellationToken)
            {
                return this.createChangedAdditionalDocument(cancellationToken);
            }
        }

        private class FixAllAdditionalDocumentChangeAction : CodeAction
        {
            private readonly List<KeyValuePair<Project, ImmutableArray<Diagnostic>>> diagnosticsToFix;
            private readonly Solution solution;

            public FixAllAdditionalDocumentChangeAction(string title, Solution solution, List<KeyValuePair<Project, ImmutableArray<Diagnostic>>> diagnosticsToFix)
            {
                this.Title = title;
                this.solution = solution;
                this.diagnosticsToFix = diagnosticsToFix;
            }

            public override string Title { get; }

            protected override async Task<Solution> GetChangedSolutionAsync(CancellationToken cancellationToken)
            {
                var updatedPublicSurfaceAreaText = new List<KeyValuePair<DocumentId, SourceText>>();

                foreach (var pair in this.diagnosticsToFix)
                {
                    var project = pair.Key;
                    var diagnostics = pair.Value;

                    var publicSurfaceAreaAdditionalDocument = GetPublicSurfaceAreaDocument(project);

                    if (publicSurfaceAreaAdditionalDocument == null)
                    {
                        continue;
                    }

                    var sourceText = await publicSurfaceAreaAdditionalDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);

                    var groupedDiagnostics =
                        diagnostics
                            .Where(d => d.Location.IsInSource)
                            .GroupBy(d => d.Location.SourceTree);

                    var newSymbolNames = new List<string>();
                    var symbolNamesToRemoveBuilder = ImmutableHashSet.CreateBuilder<string>();

                    foreach (var grouping in groupedDiagnostics)
                    {
                        var document = project.GetDocument(grouping.Key);

                        if (document == null)
                        {
                            continue;
                        }

                        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                        foreach (var diagnostic in grouping)
                        {
                            string publicSurfaceAreaSymbolName = diagnostic.Properties[DeclarePublicAPIAnalyzer.PublicApiNamePropertyBagKey];

                            newSymbolNames.Add(publicSurfaceAreaSymbolName);

                            string siblingNamesToRemove = diagnostic.Properties[DeclarePublicAPIAnalyzer.PublicApiNamesOfSiblingsToRemovePropertyBagKey];
                            if (siblingNamesToRemove.Length > 0)
                            {
                                var namesToRemove = siblingNamesToRemove.Split(DeclarePublicAPIAnalyzer.PublicApiNamesOfSiblingsToRemovePropertyBagValueSeparator.ToCharArray());
                                foreach (var nameToRemove in namesToRemove)
                                {
                                    symbolNamesToRemoveBuilder.Add(nameToRemove);
                                }
                            }
                        }
                    }

                    var symbolNamesToRemove = symbolNamesToRemoveBuilder.ToImmutable();

                    // We shouldn't be attempting to remove any symbol name, while also adding it.
                    Debug.Assert(newSymbolNames.All(newSymbolName => !symbolNamesToRemove.Contains(newSymbolName)), "Assertion failed: newSymbolNames.All(newSymbolName => !symbolNamesToRemove.Contains(newSymbolName))");

                    var newSourceText = AddSymbolNamesToSourceText(sourceText, newSymbolNames);
                    newSourceText = RemoveSymbolNamesFromSourceText(newSourceText, symbolNamesToRemove);

                    updatedPublicSurfaceAreaText.Add(new KeyValuePair<DocumentId, SourceText>(publicSurfaceAreaAdditionalDocument.Id, newSourceText));
                }

                var newSolution = this.solution;

                foreach (var pair in updatedPublicSurfaceAreaText)
                {
                    newSolution = newSolution.WithAdditionalDocumentText(pair.Key, pair.Value);
                }

                return newSolution;
            }
        }

        private class PublicSurfaceAreaFixAllProvider : FixAllProvider
        {
            public override async Task<CodeAction> GetFixAsync(FixAllContext fixAllContext)
            {
                var diagnosticsToFix = new List<KeyValuePair<Project, ImmutableArray<Diagnostic>>>();
                string titleFormat = "Add all items in {0} {1} to the public API";
                string title = null;

                switch (fixAllContext.Scope)
                {
                case FixAllScope.Document:
                    {
                        var diagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(fixAllContext.Document).ConfigureAwait(false);
                        diagnosticsToFix.Add(new KeyValuePair<Project, ImmutableArray<Diagnostic>>(fixAllContext.Project, diagnostics));
                        title = string.Format(titleFormat, "document", fixAllContext.Document.Name);
                        break;
                    }

                case FixAllScope.Project:
                    {
                        var project = fixAllContext.Project;
                        var diagnostics = await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
                        diagnosticsToFix.Add(new KeyValuePair<Project, ImmutableArray<Diagnostic>>(fixAllContext.Project, diagnostics));
                        title = string.Format(titleFormat, "project", fixAllContext.Project.Name);
                        break;
                    }

                case FixAllScope.Solution:
                    {
                        foreach (var project in fixAllContext.Solution.Projects)
                        {
                            var diagnostics = await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
                            diagnosticsToFix.Add(new KeyValuePair<Project, ImmutableArray<Diagnostic>>(project, diagnostics));
                        }

                        title = "Add all items in the solution to the public API";
                        break;
                    }

                case FixAllScope.Custom:
                    return null;

                default:
                    break;
                }

                return new FixAllAdditionalDocumentChangeAction(title, fixAllContext.Solution, diagnosticsToFix);
            }
        }
    }
}
