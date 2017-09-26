// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeFixes;
    using PublicApiAnalyzer.ApiDesign;
    using Xunit;

    public class ExportCodeFixProviderAttributeNameTest
    {
        public static IEnumerable<object[]> CodeFixProviderTypeData
        {
            get
            {
                var codeFixProviders = typeof(DeclarePublicAPIFix)
                    .Assembly
                    .GetTypes()
                    .Where(t => typeof(CodeFixProvider).IsAssignableFrom(t));

                return codeFixProviders.Select(x => new[] { x });
            }
        }

        [Theory]
        [MemberData(nameof(CodeFixProviderTypeData))]
        public void TestExportCodeFixProviderAttribute(Type codeFixProvider)
        {
            var exportCodeFixProviderAttribute = codeFixProvider.GetCustomAttributes<ExportCodeFixProviderAttribute>(false).FirstOrDefault();
            var noCodeFixAttribute = codeFixProvider.GetCustomAttributes<NoCodeFixAttribute>(false).FirstOrDefault();

            if (noCodeFixAttribute != null)
            {
                Assert.Null(exportCodeFixProviderAttribute);

                return;
            }

            Assert.NotNull(exportCodeFixProviderAttribute);
            Assert.Equal(codeFixProvider.Name, exportCodeFixProviderAttribute.Name);
            Assert.Contains(LanguageNames.CSharp, exportCodeFixProviderAttribute.Languages);
        }
    }
}
