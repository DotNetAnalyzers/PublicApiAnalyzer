// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer.Helpers
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.CodeAnalysis;

    internal static class IMethodSymbolExtensions
    {
        public static bool HasOptionalParameters(this IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Any(p => p.IsOptional);
        }

        public static IEnumerable<IMethodSymbol> GetOverloads(this IMethodSymbol method)
        {
            foreach (var member in method?.ContainingType?.GetMembers(method.Name).OfType<IMethodSymbol>())
            {
                if (!member.Equals(method))
                {
                    yield return member;
                }
            }
        }
    }
}
