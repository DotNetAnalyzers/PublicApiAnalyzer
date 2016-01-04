// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer
{
    internal static class RoslynDiagnosticIds
    {
        public const string DeclarePublicApiRuleId = "RS0016";
        public const string RemoveDeletedApiRuleId = "RS0017";
        public const string ExposedNoninstantiableTypeRuleId = "RS0022";
        public const string PublicApiFilesInvalid = "RS0024";
        public const string DuplicatedSymbolInPublicApiFiles = "RS0025";
    }
}
