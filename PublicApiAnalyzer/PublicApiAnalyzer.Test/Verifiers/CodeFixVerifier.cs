﻿// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace TestHelper
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeActions;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Formatting;
    using PublicApiAnalyzer.Test.Helpers;
    using Xunit;

    /// <summary>
    /// Superclass of all unit tests made for diagnostics with code fixes.
    /// Contains methods used to verify correctness of code fixes.
    /// </summary>
    public abstract partial class CodeFixVerifier : DiagnosticVerifier
    {
        private const int DefaultNumberOfIncrementalIterations = -1000;
        private const int DefaultIndentationSize = 4;
        private const bool DefaultUseTabs = false;

        public CodeFixVerifier()
        {
            this.IndentationSize = DefaultIndentationSize;
            this.UseTabs = DefaultUseTabs;
        }

        protected delegate Task VerifyCodeFixAsync(Project project, bool fixAll, CancellationToken cancellationToken);

        /// <summary>
        /// Gets or sets the value of the <see cref="FormattingOptions.IndentationSize"/> to apply to the test
        /// workspace.
        /// </summary>
        /// <value>
        /// The value of the <see cref="FormattingOptions.IndentationSize"/> to apply to the test workspace.
        /// </value>
        public int IndentationSize
        {
            get;
            protected set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="FormattingOptions.UseTabs"/> option is applied to the
        /// test workspace.
        /// </summary>
        /// <value>
        /// The value of the <see cref="FormattingOptions.UseTabs"/> to apply to the test workspace.
        /// </value>
        public bool UseTabs
        {
            get;
            protected set;
        }

        /// <summary>
        /// Returns the code fix being tested (C#) - to be implemented in non-abstract class.
        /// </summary>
        /// <returns>The <see cref="CodeFixProvider"/> to be used for C# code.</returns>
        protected abstract CodeFixProvider GetCSharpCodeFixProvider();

        /// <summary>
        /// Called to test a C# code fix when applied on the input source as a string.
        /// </summary>
        /// <param name="oldSource">A class in the form of a string before the code fix was applied to it.</param>
        /// <param name="newSource">A class in the form of a string after the code fix was applied to it.</param>
        /// <param name="batchNewSource">A class in the form of a string after the batch fixer was applied to it.</param>
        /// <param name="oldFileName">The name of the file in the project before the code fix was applied.</param>
        /// <param name="newFileName">The name of the file in the project after the code fix was applied.</param>
        /// <param name="codeFixIndex">Index determining which code fix to apply if there are multiple.</param>
        /// <param name="allowNewCompilerDiagnostics">A value indicating whether or not the test will fail if the code fix introduces other warnings after being applied.</param>
        /// <param name="numberOfIncrementalIterations">The number of iterations the incremental fixer will be called.
        /// If this value is less than 0, the negated value is treated as an upper limit as opposed to an exact
        /// value.</param>
        /// <param name="numberOfFixAllIterations">The number of iterations the Fix All fixer will be called. If this
        /// value is less than 0, the negated value is treated as an upper limit as opposed to an exact value.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected Task VerifyCSharpFixAsync(string oldSource, string newSource, string batchNewSource = null, string oldFileName = null, string newFileName = null, int? codeFixIndex = null, bool allowNewCompilerDiagnostics = false, int numberOfIncrementalIterations = DefaultNumberOfIncrementalIterations, int numberOfFixAllIterations = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var batchNewSources = batchNewSource == null ? null : new[] { batchNewSource };
            var oldFileNames = oldFileName == null ? null : new[] { oldFileName };
            var newFileNames = newFileName == null ? null : new[] { newFileName };
            return this.VerifyCSharpFixAsync(new[] { oldSource }, new[] { newSource }, batchNewSources, oldFileNames, newFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfIncrementalIterations, numberOfFixAllIterations, cancellationToken);
        }

        /// <summary>
        /// Called to test a C# code fix when applied on the input source as a string.
        /// </summary>
        /// <param name="oldSource">A class in the form of a string before the code fix was applied to it.</param>
        /// <param name="verifyFixedProjectAsync">A validation function to verify the results of the code fix.</param>
        /// <param name="oldFileName">The name of the file in the project before the code fix was applied.</param>
        /// <param name="codeFixIndex">Index determining which code fix to apply if there are multiple.</param>
        /// <param name="allowNewCompilerDiagnostics">A value indicating whether or not the test will fail if the code fix introduces other warnings after being applied.</param>
        /// <param name="numberOfIncrementalIterations">The number of iterations the incremental fixer will be called.
        /// If this value is less than 0, the negated value is treated as an upper limit as opposed to an exact
        /// value.</param>
        /// <param name="numberOfFixAllIterations">The number of iterations the Fix All fixer will be called. If this
        /// value is less than 0, the negated value is treated as an upper limit as opposed to an exact value.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected Task VerifyCSharpFixAsync(string oldSource, VerifyCodeFixAsync verifyFixedProjectAsync, string oldFileName = null, int? codeFixIndex = null, bool allowNewCompilerDiagnostics = false, int numberOfIncrementalIterations = DefaultNumberOfIncrementalIterations, int numberOfFixAllIterations = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var oldFileNames = oldFileName == null ? null : new[] { oldFileName };
            return this.VerifyCSharpFixAsync(new[] { oldSource }, verifyFixedProjectAsync, oldFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfIncrementalIterations, numberOfFixAllIterations, cancellationToken);
        }

        /// <summary>
        /// Called to test a C# code fix when applied on the input source as a string.
        /// </summary>
        /// <param name="oldSources">An array of sources in the form of strings before the code fix was applied to them.</param>
        /// <param name="newSources">An array of sources in the form of strings after the code fix was applied to them.</param>
        /// <param name="batchNewSources">An array of sources in the form of a strings after the batch fixer was applied to them.</param>
        /// <param name="oldFileNames">An array of file names in the project before the code fix was applied.</param>
        /// <param name="newFileNames">An array of file names in the project after the code fix was applied.</param>
        /// <param name="codeFixIndex">Index determining which code fix to apply if there are multiple.</param>
        /// <param name="allowNewCompilerDiagnostics">A value indicating whether or not the test will fail if the code fix introduces other warnings after being applied.</param>
        /// <param name="numberOfIncrementalIterations">The number of iterations the incremental fixer will be called.
        /// If this value is less than 0, the negated value is treated as an upper limit as opposed to an exact
        /// value.</param>
        /// <param name="numberOfFixAllIterations">The number of iterations the Fix All fixer will be called. If this
        /// value is less than 0, the negated value is treated as an upper limit as opposed to an exact value.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected async Task VerifyCSharpFixAsync(string[] oldSources, string[] newSources, string[] batchNewSources = null, string[] oldFileNames = null, string[] newFileNames = null, int? codeFixIndex = null, bool allowNewCompilerDiagnostics = false, int numberOfIncrementalIterations = DefaultNumberOfIncrementalIterations, int numberOfFixAllIterations = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var t1 = this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), oldSources, newSources, oldFileNames, newFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfIncrementalIterations, FixEachAnalyzerDiagnosticAsync, cancellationToken).ConfigureAwait(false);

            var fixAllProvider = this.GetCSharpCodeFixProvider().GetFixAllProvider();
            Assert.NotEqual(WellKnownFixAllProviders.BatchFixer, fixAllProvider);

            if (fixAllProvider == null)
            {
                await t1;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    await t1;
                }

                var t2 = this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), oldSources, batchNewSources ?? newSources, oldFileNames, newFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfFixAllIterations, FixAllAnalyzerDiagnosticsInDocumentAsync, cancellationToken).ConfigureAwait(false);
                if (Debugger.IsAttached)
                {
                    await t2;
                }

                var t3 = this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), oldSources, batchNewSources ?? newSources, oldFileNames, newFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfFixAllIterations, FixAllAnalyzerDiagnosticsInProjectAsync, cancellationToken).ConfigureAwait(false);
                if (Debugger.IsAttached)
                {
                    await t3;
                }

                var t4 = this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), oldSources, batchNewSources ?? newSources, oldFileNames, newFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfFixAllIterations, FixAllAnalyzerDiagnosticsInSolutionAsync, cancellationToken).ConfigureAwait(false);
                if (Debugger.IsAttached)
                {
                    await t4;
                }

                if (!Debugger.IsAttached)
                {
                    // Allow the operations to run in parallel
                    await t1;
                    await t2;
                    await t3;
                    await t4;
                }
            }
        }

        /// <summary>
        /// Called to test a C# code fix when applied on the input source as a string.
        /// </summary>
        /// <param name="oldSources">An array of sources in the form of strings before the code fix was applied to them.</param>
        /// <param name="verifyFixedProjectAsync">A validation function to verify the results of the code fix.</param>
        /// <param name="oldFileNames">An array of file names in the project before the code fix was applied.</param>
        /// <param name="codeFixIndex">Index determining which code fix to apply if there are multiple.</param>
        /// <param name="allowNewCompilerDiagnostics">A value indicating whether or not the test will fail if the code fix introduces other warnings after being applied.</param>
        /// <param name="numberOfIncrementalIterations">The number of iterations the incremental fixer will be called.
        /// If this value is less than 0, the negated value is treated as an upper limit as opposed to an exact
        /// value.</param>
        /// <param name="numberOfFixAllIterations">The number of iterations the Fix All fixer will be called. If this
        /// value is less than 0, the negated value is treated as an upper limit as opposed to an exact value.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected async Task VerifyCSharpFixAsync(string[] oldSources, VerifyCodeFixAsync verifyFixedProjectAsync, string[] oldFileNames = null, int? codeFixIndex = null, bool allowNewCompilerDiagnostics = false, int numberOfIncrementalIterations = DefaultNumberOfIncrementalIterations, int numberOfFixAllIterations = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
#pragma warning disable SA1101 // Prefix local calls with this
            Func<Project, CancellationToken, Task> verifyAsync = (project, ct) => verifyFixedProjectAsync(project, false, ct);
#pragma warning restore SA1101 // Prefix local calls with this

            var t1 = this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), oldSources, oldFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfIncrementalIterations, FixEachAnalyzerDiagnosticAsync, verifyAsync, cancellationToken).ConfigureAwait(false);

            var fixAllProvider = this.GetCSharpCodeFixProvider().GetFixAllProvider();
            Assert.NotEqual(WellKnownFixAllProviders.BatchFixer, fixAllProvider);

            if (fixAllProvider == null)
            {
                await t1;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    await t1;
                }

#pragma warning disable SA1101 // Prefix local calls with this
                Func<Project, CancellationToken, Task> verifyFixAllAsync = (project, ct) => verifyFixedProjectAsync(project, true, ct);
#pragma warning restore SA1101 // Prefix local calls with this

                var t2 = this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), oldSources, oldFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfFixAllIterations, FixAllAnalyzerDiagnosticsInDocumentAsync, verifyFixAllAsync, cancellationToken).ConfigureAwait(false);
                if (Debugger.IsAttached)
                {
                    await t2;
                }

                var t3 = this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), oldSources, oldFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfFixAllIterations, FixAllAnalyzerDiagnosticsInProjectAsync, verifyFixAllAsync, cancellationToken).ConfigureAwait(false);
                if (Debugger.IsAttached)
                {
                    await t3;
                }

                var t4 = this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), oldSources, oldFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfFixAllIterations, FixAllAnalyzerDiagnosticsInSolutionAsync, verifyFixAllAsync, cancellationToken).ConfigureAwait(false);
                if (Debugger.IsAttached)
                {
                    await t4;
                }

                if (!Debugger.IsAttached)
                {
                    // Allow the operations to run in parallel
                    await t1;
                    await t2;
                    await t3;
                    await t4;
                }
            }
        }

        /// <summary>
        /// Called to test a C# fix all provider when applied on the input source as a string.
        /// </summary>
        /// <param name="oldSource">A class in the form of a string before the code fix was applied to it.</param>
        /// <param name="newSource">A class in the form of a string after the code fix was applied to it.</param>
        /// <param name="codeFixIndex">Index determining which code fix to apply if there are multiple.</param>
        /// <param name="allowNewCompilerDiagnostics">A value indicating whether or not the test will fail if the code fix introduces other warnings after being applied.</param>
        /// <param name="numberOfIterations">The number of iterations the fixer will be called. If this value is less
        /// than 0, the negated value is treated as an upper limit as opposed to an exact value.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected async Task VerifyCSharpFixAllFixAsync(string oldSource, string newSource, int? codeFixIndex = null, bool allowNewCompilerDiagnostics = false, int numberOfIterations = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), new[] { oldSource }, new[] { newSource }, null, null, codeFixIndex, allowNewCompilerDiagnostics, numberOfIterations, FixAllAnalyzerDiagnosticsInDocumentAsync, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all offered code fixes for the specified diagnostic within the given source.
        /// </summary>
        /// <param name="source">A valid C# source file in the form of a string.</param>
        /// <param name="diagnosticIndex">Index determining which diagnostic to use for determining the offered code fixes. Uses the first diagnostic if null.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
        /// <returns>The collection of offered code actions. This collection may be empty.</returns>
        protected async Task<Tuple<Solution, ImmutableArray<CodeAction>>> GetOfferedCSharpFixesAsync(string source, int? diagnosticIndex = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.GetOfferedFixesInternalAsync(LanguageNames.CSharp, source, diagnosticIndex, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override Solution CreateSolution(ProjectId projectId, string language)
        {
            Solution solution = base.CreateSolution(projectId, language);
            solution.Workspace.Options =
                solution.Workspace.Options
                .WithChangedOption(FormattingOptions.IndentationSize, language, this.IndentationSize)
                .WithChangedOption(FormattingOptions.UseTabs, language, this.UseTabs);
            return solution;
        }

        private static async Task<Project> FixEachAnalyzerDiagnosticAsync(ImmutableArray<DiagnosticAnalyzer> analyzers, CodeFixProvider codeFixProvider, int? codeFixIndex, Project project, int numberOfIterations, CancellationToken cancellationToken)
        {
            int expectedNumberOfIterations = numberOfIterations;
            if (numberOfIterations < 0)
            {
                numberOfIterations = -numberOfIterations;
            }

            var previousDiagnostics = ImmutableArray.Create<Diagnostic>();

            bool done;
            do
            {
                var analyzerDiagnostics = await GetSortedDiagnosticsFromDocumentsAsync(analyzers, project.Documents.ToArray(), cancellationToken).ConfigureAwait(false);
                if (analyzerDiagnostics.Length == 0)
                {
                    break;
                }

                if (!AreDiagnosticsDifferent(analyzerDiagnostics, previousDiagnostics))
                {
                    break;
                }

                if (--numberOfIterations < 0)
                {
                    Assert.True(false, "The upper limit for the number of code fix iterations was exceeded");
                }

                previousDiagnostics = analyzerDiagnostics;

                done = true;
                foreach (var diagnostic in analyzerDiagnostics)
                {
                    if (!codeFixProvider.FixableDiagnosticIds.Contains(diagnostic.Id))
                    {
                        // do not pass unsupported diagnostics to a code fix provider
                        continue;
                    }

                    var actions = new List<CodeAction>();
                    var context = new CodeFixContext(project.GetDocument(diagnostic.Location.SourceTree), diagnostic, (a, d) => actions.Add(a), cancellationToken);
                    await codeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

                    if (actions.Count > 0)
                    {
                        var fixedProject = await ApplyFixAsync(project, actions.ElementAt(codeFixIndex.GetValueOrDefault(0)), cancellationToken).ConfigureAwait(false);
                        if (fixedProject != project)
                        {
                            done = false;

                            project = await RecreateProjectDocumentsAsync(fixedProject, cancellationToken).ConfigureAwait(false);
                            break;
                        }
                    }
                }
            }
            while (!done);

            if (expectedNumberOfIterations >= 0)
            {
                Assert.Equal($"{expectedNumberOfIterations} iterations", $"{expectedNumberOfIterations - numberOfIterations} iterations");
            }

            return project;
        }

        private static Task<Project> FixAllAnalyzerDiagnosticsInDocumentAsync(ImmutableArray<DiagnosticAnalyzer> analyzers, CodeFixProvider codeFixProvider, int? codeFixIndex, Project project, int numberOfIterations, CancellationToken cancellationToken)
        {
            return FixAllAnalyerDiagnosticsInScopeAsync(FixAllScope.Document, analyzers, codeFixProvider, codeFixIndex, project, numberOfIterations, cancellationToken);
        }

        private static Task<Project> FixAllAnalyzerDiagnosticsInProjectAsync(ImmutableArray<DiagnosticAnalyzer> analyzers, CodeFixProvider codeFixProvider, int? codeFixIndex, Project project, int numberOfIterations, CancellationToken cancellationToken)
        {
            return FixAllAnalyerDiagnosticsInScopeAsync(FixAllScope.Project, analyzers, codeFixProvider, codeFixIndex, project, numberOfIterations, cancellationToken);
        }

        private static Task<Project> FixAllAnalyzerDiagnosticsInSolutionAsync(ImmutableArray<DiagnosticAnalyzer> analyzers, CodeFixProvider codeFixProvider, int? codeFixIndex, Project project, int numberOfIterations, CancellationToken cancellationToken)
        {
            return FixAllAnalyerDiagnosticsInScopeAsync(FixAllScope.Solution, analyzers, codeFixProvider, codeFixIndex, project, numberOfIterations, cancellationToken);
        }

        private static async Task<Project> FixAllAnalyerDiagnosticsInScopeAsync(FixAllScope scope, ImmutableArray<DiagnosticAnalyzer> analyzers, CodeFixProvider codeFixProvider, int? codeFixIndex, Project project, int numberOfIterations, CancellationToken cancellationToken)
        {
            int expectedNumberOfIterations = numberOfIterations;
            if (numberOfIterations < 0)
            {
                numberOfIterations = -numberOfIterations;
            }

            var previousDiagnostics = ImmutableArray.Create<Diagnostic>();

            var fixAllProvider = codeFixProvider.GetFixAllProvider();

            if (fixAllProvider == null)
            {
                return null;
            }

            bool done;
            do
            {
                var analyzerDiagnostics = await GetSortedDiagnosticsFromDocumentsAsync(analyzers, project.Documents.ToArray(), cancellationToken).ConfigureAwait(false);
                if (analyzerDiagnostics.Length == 0)
                {
                    break;
                }

                if (!AreDiagnosticsDifferent(analyzerDiagnostics, previousDiagnostics))
                {
                    break;
                }

                if (--numberOfIterations < 0)
                {
                    Assert.True(false, "The upper limit for the number of fix all iterations was exceeded");
                }

                Diagnostic firstDiagnostic = null;
                string equivalenceKey = null;
                foreach (var diagnostic in analyzerDiagnostics)
                {
                    if (!codeFixProvider.FixableDiagnosticIds.Contains(diagnostic.Id))
                    {
                        // do not pass unsupported diagnostics to a code fix provider
                        continue;
                    }

                    var actions = new List<CodeAction>();
                    var context = new CodeFixContext(project.GetDocument(diagnostic.Location.SourceTree), diagnostic, (a, d) => actions.Add(a), cancellationToken);
                    await codeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
                    if (actions.Count > (codeFixIndex ?? 0))
                    {
                        firstDiagnostic = diagnostic;
                        equivalenceKey = actions[codeFixIndex ?? 0].EquivalenceKey;
                        break;
                    }
                }

                if (firstDiagnostic == null)
                {
                    return project;
                }

                previousDiagnostics = analyzerDiagnostics;

                done = true;

                FixAllContext.DiagnosticProvider fixAllDiagnosticProvider = TestDiagnosticProvider.Create(analyzerDiagnostics);

                IEnumerable<string> analyzerDiagnosticIds = analyzers.SelectMany(x => x.SupportedDiagnostics).Select(x => x.Id);
                IEnumerable<string> compilerDiagnosticIds = codeFixProvider.FixableDiagnosticIds.Where(x => x.StartsWith("CS", StringComparison.Ordinal));
                IEnumerable<string> disabledDiagnosticIds = project.CompilationOptions.SpecificDiagnosticOptions.Where(x => x.Value == ReportDiagnostic.Suppress).Select(x => x.Key);
                IEnumerable<string> relevantIds = analyzerDiagnosticIds.Concat(compilerDiagnosticIds).Except(disabledDiagnosticIds).Distinct();
                FixAllContext fixAllContext = new FixAllContext(project.GetDocument(firstDiagnostic.Location.SourceTree), codeFixProvider, scope, equivalenceKey, relevantIds, fixAllDiagnosticProvider, cancellationToken);

                CodeAction action = await fixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);
                if (action == null)
                {
                    return project;
                }

                var fixedProject = await ApplyFixAsync(project, action, cancellationToken).ConfigureAwait(false);
                if (fixedProject != project)
                {
                    done = false;

                    project = await RecreateProjectDocumentsAsync(fixedProject, cancellationToken).ConfigureAwait(false);
                }
            }
            while (!done);

            if (expectedNumberOfIterations >= 0)
            {
                Assert.Equal($"{expectedNumberOfIterations} iterations", $"{expectedNumberOfIterations - numberOfIterations} iterations");
            }

            return project;
        }

        private static bool AreDiagnosticsDifferent(ImmutableArray<Diagnostic> analyzerDiagnostics, ImmutableArray<Diagnostic> previousDiagnostics)
        {
            if (analyzerDiagnostics.Length != previousDiagnostics.Length)
            {
                return true;
            }

            for (var i = 0; i < analyzerDiagnostics.Length; i++)
            {
                if ((analyzerDiagnostics[i].Id != previousDiagnostics[i].Id)
                    || (analyzerDiagnostics[i].Location.SourceSpan != previousDiagnostics[i].Location.SourceSpan))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<Project> ApplyFixInternalAsync(
            string language,
            ImmutableArray<DiagnosticAnalyzer> analyzers,
            CodeFixProvider codeFixProvider,
            string[] oldSources,
            string[] oldFileNames,
            int? codeFixIndex,
            bool allowNewCompilerDiagnostics,
            int numberOfIterations,
            Func<ImmutableArray<DiagnosticAnalyzer>, CodeFixProvider, int?, Project, int, CancellationToken, Task<Project>> getFixedProject,
            CancellationToken cancellationToken)
        {
            if (oldFileNames != null)
            {
                // Make sure the test case is consistent regarding the number of sources and file names before the code fix
                Assert.Equal($"{oldSources.Length} old file names", $"{oldFileNames.Length} old file names");
            }

            var project = this.CreateProject(oldSources, language, oldFileNames);
            var compilerDiagnostics = await GetCompilerDiagnosticsAsync(project, cancellationToken).ConfigureAwait(false);

            project = await getFixedProject(analyzers, codeFixProvider, codeFixIndex, project, numberOfIterations, cancellationToken).ConfigureAwait(false);

            var newCompilerDiagnostics = GetNewDiagnostics(compilerDiagnostics, await GetCompilerDiagnosticsAsync(project, cancellationToken).ConfigureAwait(false));

            // Check if applying the code fix introduced any new compiler diagnostics
            if (!allowNewCompilerDiagnostics && newCompilerDiagnostics.Any())
            {
                // Format and get the compiler diagnostics again so that the locations make sense in the output
                project = await ReformatProjectDocumentsAsync(project, cancellationToken).ConfigureAwait(false);
                newCompilerDiagnostics = GetNewDiagnostics(compilerDiagnostics, await GetCompilerDiagnosticsAsync(project, cancellationToken).ConfigureAwait(false));

                var message = new StringBuilder();
                message.Append("Fix introduced new compiler diagnostics:\r\n");
                newCompilerDiagnostics.Aggregate(message, (sb, d) => sb.Append(d.ToString()).Append("\r\n"));
                foreach (var document in project.Documents)
                {
                    message.Append("\r\n").Append(document.Name).Append(":\r\n");
                    message.Append((await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)).ToFullString());
                    message.Append("\r\n");
                }

                Assert.True(false, message.ToString());
            }

            return project;
        }

        private async Task VerifyFixInternalAsync(
            string language,
            ImmutableArray<DiagnosticAnalyzer> analyzers,
            CodeFixProvider codeFixProvider,
            string[] oldSources,
            string[] newSources,
            string[] oldFileNames,
            string[] newFileNames,
            int? codeFixIndex,
            bool allowNewCompilerDiagnostics,
            int numberOfIterations,
            Func<ImmutableArray<DiagnosticAnalyzer>, CodeFixProvider, int?, Project, int, CancellationToken, Task<Project>> getFixedProject,
            CancellationToken cancellationToken)
        {
            if (oldFileNames != null)
            {
                // Make sure the test case is consistent regarding the number of sources and file names before the code fix
                Assert.Equal($"{oldSources.Length} old file names", $"{oldFileNames.Length} old file names");
            }

            if (newFileNames != null)
            {
                // Make sure the test case is consistent regarding the number of sources and file names after the code fix
                Assert.Equal($"{newSources.Length} new file names", $"{newFileNames.Length} new file names");
            }

            // After applying all of the code fixes, compare the resulting string to the inputed one
            Func<Project, CancellationToken, Task> verifyFixedProjectAsync =
                async (project, ct) =>
                {
                    var updatedDocuments = project.Documents.ToArray();

                    Assert.Equal($"{newSources.Length} documents", $"{updatedDocuments.Length} documents");

                    for (int i = 0; i < updatedDocuments.Length; i++)
                    {
                        var actual = await GetStringFromDocumentAsync(updatedDocuments[i], cancellationToken).ConfigureAwait(false);
                        Assert.Equal(newSources[i], actual);

                        if (newFileNames != null)
                        {
                            Assert.Equal(newFileNames[i], updatedDocuments[i].Name);
                        }
                    }
                };

            await this.VerifyFixInternalAsync(language, analyzers, codeFixProvider, oldSources, oldFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfIterations, getFixedProject, verifyFixedProjectAsync, cancellationToken).ConfigureAwait(false);
        }

        private async Task VerifyFixInternalAsync(
            string language,
            ImmutableArray<DiagnosticAnalyzer> analyzers,
            CodeFixProvider codeFixProvider,
            string[] oldSources,
            string[] oldFileNames,
            int? codeFixIndex,
            bool allowNewCompilerDiagnostics,
            int numberOfIterations,
            Func<ImmutableArray<DiagnosticAnalyzer>, CodeFixProvider, int?, Project, int, CancellationToken, Task<Project>> getFixedProject,
            Func<Project, CancellationToken, Task> verifyFixedProjectAsync,
            CancellationToken cancellationToken)
        {
            var project = await this.ApplyFixInternalAsync(language, analyzers, codeFixProvider, oldSources, oldFileNames, codeFixIndex, allowNewCompilerDiagnostics, numberOfIterations, getFixedProject, cancellationToken).ConfigureAwait(false);

            await verifyFixedProjectAsync(project, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Tuple<Solution, ImmutableArray<CodeAction>>> GetOfferedFixesInternalAsync(string language, string source, int? diagnosticIndex, ImmutableArray<DiagnosticAnalyzer> analyzers, CodeFixProvider codeFixProvider, CancellationToken cancellationToken)
        {
            var document = this.CreateDocument(source, language);
            var analyzerDiagnostics = await GetSortedDiagnosticsFromDocumentsAsync(analyzers, new[] { document }, cancellationToken).ConfigureAwait(false);

            var index = diagnosticIndex.HasValue ? diagnosticIndex.Value : 0;

            Assert.True(index < analyzerDiagnostics.Count());

            var actions = new List<CodeAction>();

            // do not pass unsupported diagnostics to a code fix provider
            if (codeFixProvider.FixableDiagnosticIds.Contains(analyzerDiagnostics[index].Id))
            {
                var context = new CodeFixContext(document, analyzerDiagnostics[index], (a, d) => actions.Add(a), cancellationToken);
                await codeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
            }

            return Tuple.Create(document.Project.Solution, actions.ToImmutableArray());
        }
    }
}
