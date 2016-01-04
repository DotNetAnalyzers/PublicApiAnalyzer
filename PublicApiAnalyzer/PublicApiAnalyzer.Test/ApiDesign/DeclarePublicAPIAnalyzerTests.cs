// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer.Test.ApiDesign
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis.CodeActions;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Text;
    using PublicApiAnalyzer.ApiDesign;
    using TestHelper;
    using Xunit;

    public class DeclarePublicAPIAnalyzerTests : CodeFixVerifier
    {
        private string shippedText;
        private string unshippedText;

        [Fact]
        public async Task SimpleMissingTypeAsync()
        {
            var source = @"
public class C
{
}
";

            this.shippedText = string.Empty;
            this.unshippedText = string.Empty;

            var expected = this.CSharpDiagnostic(DeclarePublicAPIAnalyzer.DeclareNewApiRule).WithArguments("C").WithLocation(2, 14);
            await this.VerifyCSharpDiagnosticAsync(source, expected, CancellationToken.None).ConfigureAwait(false);

            string fixedApi = "C";
            var updatedApi = await this.GetUpdatedApiAsync(source, 0, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(fixedApi, updatedApi.ToString());
        }

        [Fact]
        public async Task SimpleMissingMemberAsync()
        {
            var source = @"
public class C
{
    public int Field;
    public int Property { get; set; }
    public void Method() { }
}
";

            this.shippedText = string.Empty;
            this.unshippedText = string.Empty;

            DiagnosticResult[] expected =
            {
                // Test0.cs(2,14): error RS0016: Symbol 'C' is not part of the declared API.
                this.CSharpDiagnostic(DeclarePublicAPIAnalyzer.DeclareNewApiRule).WithArguments("C").WithLocation(2, 14),

                // Test0.cs(4,16): error RS0016: Symbol 'Field' is not part of the declared API.
                this.CSharpDiagnostic(DeclarePublicAPIAnalyzer.DeclareNewApiRule).WithArguments("Field").WithLocation(4, 16),

                // Test0.cs(5,27): error RS0016: Symbol 'Property.get' is not part of the declared API.
                this.CSharpDiagnostic(DeclarePublicAPIAnalyzer.DeclareNewApiRule).WithArguments("Property.get").WithLocation(5, 27),

                // Test0.cs(5,32): error RS0016: Symbol 'Property.set' is not part of the declared API.
                this.CSharpDiagnostic(DeclarePublicAPIAnalyzer.DeclareNewApiRule).WithArguments("Property.set").WithLocation(5, 32),

                // Test0.cs(6,17): error RS0016: Symbol 'Method' is not part of the declared API.
                this.CSharpDiagnostic(DeclarePublicAPIAnalyzer.DeclareNewApiRule).WithArguments("Method").WithLocation(6, 17)
            };

            await this.VerifyCSharpDiagnosticAsync(source, expected, CancellationToken.None).ConfigureAwait(false);

            string fixedApi = "C";
            var updatedApi = await this.GetUpdatedApiAsync(source, 0, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(fixedApi, updatedApi.ToString());

            fixedApi = "C.Field -> int";
            updatedApi = await this.GetUpdatedApiAsync(source, 1, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(fixedApi, updatedApi.ToString());

            fixedApi = "C.Property.get -> int";
            updatedApi = await this.GetUpdatedApiAsync(source, 2, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(fixedApi, updatedApi.ToString());

            fixedApi = "C.Property.set -> void";
            updatedApi = await this.GetUpdatedApiAsync(source, 3, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(fixedApi, updatedApi.ToString());

            fixedApi = "C.Method() -> void";
            updatedApi = await this.GetUpdatedApiAsync(source, 4, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(fixedApi, updatedApi.ToString());
        }

        [Fact]
        public async Task SimpleMemberAsync()
        {
            var source = @"
public class C
{
    public int Field;
    public int Property { get; set; }
    public void Method() { }
}
";

            this.shippedText = @"
C
C.Field -> int
C.Property.get -> int
C.Property.set -> void
C.Method() -> void
";
            this.unshippedText = string.Empty;

            await this.VerifyCSharpDiagnosticAsync(source, EmptyDiagnosticResults, CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public async Task SplitBetweenShippedUnshippedAsync()
        {
            var source = @"
public class C
{
    public int Field;
    public int Property { get; set; }
    public void Method() { }
}
";

            this.shippedText = @"
C
C.Field -> int
C.Property.get -> int
C.Property.set -> void
";
            this.unshippedText = @"
C.Method() -> void
";

            await this.VerifyCSharpDiagnosticAsync(source, EmptyDiagnosticResults, CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public async Task EnumSplitBetweenFilesAsync()
        {
            var source = @"
public enum E
{
    V1 = 1,
    V2 = 2,
    V3 = 3,
}
";

            this.shippedText = @"
E
E.V1 = 1 -> E
E.V2 = 2 -> E
";

            this.unshippedText = @"
E.V3 = 3 -> E
";

            await this.VerifyCSharpDiagnosticAsync(source, EmptyDiagnosticResults, CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public async Task SimpleRemovedMemberAsync()
        {
            var source = @"
public class C
{
    public int Field;
    public int Property { get; set; }
}
";

            this.shippedText = @"
C
C.Field -> int
C.Property.get -> int
C.Property.set -> void
C.Method() -> void
";

            this.unshippedText = $@"
{DeclarePublicAPIAnalyzer.RemovedApiPrefix}C.Method() -> void
";

            await this.VerifyCSharpDiagnosticAsync(source, EmptyDiagnosticResults, CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public async Task ApiFileShippedWithRemovedAsync()
        {
            var source = @"
public class C
{
    public int Field;
    public int Property { get; set; }
}
";

            this.shippedText = $@"
C
C.Field -> int
C.Property.get -> int
C.Property.set -> void
{DeclarePublicAPIAnalyzer.RemovedApiPrefix}C.Method() -> void
";

            this.unshippedText = string.Empty;

                // error RS0024: The contents of the public API files are invalid: The shipped API file can't have removed members
            var expected = this.CSharpDiagnostic(DeclarePublicAPIAnalyzer.PublicApiFilesInvalid).WithArguments(DeclarePublicAPIAnalyzer.InvalidReasonShippedCantHaveRemoved);

            await this.VerifyCSharpDiagnosticAsync(source, expected, CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public async Task DuplicateSymbolInSameAPIFileAsync()
        {
            var source = @"
public class C
{
    public int Field;
    public int Property { get; set; }
}
";

            this.shippedText = @"
C
C.Field -> int
C.Property.get -> int
C.Property.set -> void
C.Property.get -> int
";

            this.unshippedText = string.Empty;

            // Warning RS0025: The symbol 'C.Property.get -> int' appears more than once in the public API files.
            var expected = this.CSharpDiagnostic(DeclarePublicAPIAnalyzer.DuplicateSymbolInApiFiles)
                .WithArguments("C.Property.get -> int")
                .WithLocation(DeclarePublicAPIAnalyzer.ShippedFileName, 6, 1)
                .WithLocation(DeclarePublicAPIAnalyzer.ShippedFileName, 4, 1);

            await this.VerifyCSharpDiagnosticAsync(source, expected, CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public async Task DuplicateSymbolInDifferentAPIFilesAsync()
        {
            var source = @"
public class C
{
    public int Field;
    public int Property { get; set; }
}
";

            this.shippedText = @"
C
C.Field -> int
C.Property.get -> int
C.Property.set -> void
";

            this.unshippedText = @"
C.Property.get -> int";

            // Warning RS0025: The symbol 'C.Property.get -> int' appears more than once in the public API files.
            var expected = this.CSharpDiagnostic(DeclarePublicAPIAnalyzer.DuplicateSymbolInApiFiles)
                .WithArguments("C.Property.get -> int")
                .WithLocation(DeclarePublicAPIAnalyzer.UnshippedFileName, 2, 1)
                .WithLocation(DeclarePublicAPIAnalyzer.ShippedFileName, 4, 1);

            await this.VerifyCSharpDiagnosticAsync(source, expected, CancellationToken.None).ConfigureAwait(false);
        }

        protected override IEnumerable<DiagnosticAnalyzer> GetCSharpDiagnosticAnalyzers()
        {
            yield return new DeclarePublicAPIAnalyzer();
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new DeclarePublicAPIFix();
        }

        protected override string GetShippedPublicApi()
        {
            return this.shippedText;
        }

        protected override string GetUnshippedPublicApi()
        {
            return this.unshippedText;
        }

        private async Task<SourceText> GetUpdatedApiAsync(string source, int diagnosticIndex, CancellationToken cancellationToken)
        {
            var fixes = await this.GetOfferedCSharpFixesAsync(source, diagnosticIndex, cancellationToken).ConfigureAwait(false);
            Assert.Equal(1, fixes.Item2.Length);

            var operations = await fixes.Item2[0].GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(1, operations.Length);
            ApplyChangesOperation operation = operations[0] as ApplyChangesOperation;
            Assert.NotNull(operation);

            var oldSolution = fixes.Item1;
            var newSolution = operation.ChangedSolution;
            var solutionChanges = newSolution.GetChanges(oldSolution);
            var projectChanges = solutionChanges.GetProjectChanges().Single();
            var changedDocumentId = projectChanges.GetChangedAdditionalDocuments().Single();
            var newDocument = projectChanges.NewProject.GetAdditionalDocument(changedDocumentId);
            var newText = await newDocument.GetTextAsync(CancellationToken.None).ConfigureAwait(false);

            return newText;
        }
    }
}
