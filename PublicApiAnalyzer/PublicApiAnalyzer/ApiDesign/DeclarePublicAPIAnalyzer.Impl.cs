// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer.ApiDesign
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Text;

    /// <content>
    /// This file contains the main implementation for <see cref="DeclarePublicAPIAnalyzer"/>.
    /// </content>
    internal partial class DeclarePublicAPIAnalyzer : DiagnosticAnalyzer
    {
        private struct RemovedApiLine
        {
            public readonly string Text;
            public readonly ApiLine ApiLine;

            internal RemovedApiLine(string text, ApiLine apiLine)
            {
                this.Text = text;
                this.ApiLine = apiLine;
            }
        }

        private struct ApiData
        {
            public readonly ImmutableArray<ApiLine> ApiList;
            public readonly ImmutableArray<RemovedApiLine> RemovedApiList;

            internal ApiData(ImmutableArray<ApiLine> apiList, ImmutableArray<RemovedApiLine> removedApiList)
            {
                this.ApiList = apiList;
                this.RemovedApiList = removedApiList;
            }
        }

        private sealed class ApiLine
        {
            internal ApiLine(string text, TextSpan span, SourceText sourceText, string path)
            {
                this.Text = text;
                this.Span = span;
                this.SourceText = sourceText;
                this.Path = path;
            }

            public string Text { get; }

            public TextSpan Span { get; }

            public SourceText SourceText { get; }

            public string Path { get; }
        }

        private sealed class Impl
        {
            private static readonly HashSet<MethodKind> IgnorableMethodKinds = new HashSet<MethodKind>
            {
                MethodKind.EventAdd,
                MethodKind.EventRemove
            };

            private readonly ApiData unshippedData;
            private readonly Dictionary<ITypeSymbol, bool> typeCanBeExtendedCache = new Dictionary<ITypeSymbol, bool>();
            private readonly HashSet<string> visitedApiList = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, ApiLine> publicApiMap = new Dictionary<string, ApiLine>(StringComparer.Ordinal);

            internal Impl(ApiData shippedData, ApiData unshippedData)
            {
                this.unshippedData = unshippedData;

                foreach (var cur in shippedData.ApiList)
                {
                    this.publicApiMap.Add(cur.Text, cur);
                }

                foreach (var cur in unshippedData.ApiList)
                {
                    this.publicApiMap.Add(cur.Text, cur);
                }
            }

            internal void OnSymbolAction(SymbolAnalysisContext symbolContext)
            {
                var symbol = symbolContext.Symbol;

                var methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol != null &&
                    IgnorableMethodKinds.Contains(methodSymbol.MethodKind))
                {
                    return;
                }

                if (!this.IsPublicApi(symbol))
                {
                    return;
                }

                string publicApiName = GetPublicApiName(symbol);
                this.visitedApiList.Add(publicApiName);

                if (!this.publicApiMap.ContainsKey(publicApiName))
                {
                    var errorMessageName = symbol.ToDisplayString(ShortSymbolNameFormat);
                    var propertyBag = ImmutableDictionary<string, string>.Empty
                        .Add(PublicApiNamePropertyBagKey, publicApiName)
                        .Add(MinimalNamePropertyBagKey, errorMessageName);

                    foreach (var sourceLocation in symbol.Locations.Where(loc => loc.IsInSource))
                    {
                        symbolContext.ReportDiagnostic(Diagnostic.Create(DeclareNewApiRule, sourceLocation, propertyBag, errorMessageName));
                    }
                }

                // Check if a public API is a constructor that makes this class instantiable, even though the base class
                // is not instantiable. That API pattern is not allowed, because it causes protected members of
                // the base class, which are not considered public APIs, to be exposed to subclasses of this class.
                if ((symbol as IMethodSymbol)?.MethodKind == MethodKind.Constructor &&
                    symbol.ContainingType.TypeKind == TypeKind.Class &&
                    !symbol.ContainingType.IsSealed &&
                    symbol.ContainingType.BaseType != null &&
                    this.IsPublicApi(symbol.ContainingType.BaseType) &&
                    !this.CanTypeBeExtendedPublicly(symbol.ContainingType.BaseType))
                {
                    var errorMessageName = symbol.ToDisplayString(ShortSymbolNameFormat);
                    var propertyBag = ImmutableDictionary<string, string>.Empty;
                    symbolContext.ReportDiagnostic(Diagnostic.Create(ExposedNoninstantiableType, symbol.Locations[0], propertyBag, errorMessageName));
                }
            }

            internal void OnCompilationEnd(CompilationAnalysisContext context)
            {
                List<ApiLine> deletedApiList = this.GetDeletedApiList();
                foreach (var cur in deletedApiList)
                {
                    var linePositionSpan = cur.SourceText.Lines.GetLinePositionSpan(cur.Span);
                    var location = Location.Create(cur.Path, cur.Span, linePositionSpan);
                    var propertyBag = ImmutableDictionary<string, string>.Empty.Add(PublicApiNamePropertyBagKey, cur.Text);
                    context.ReportDiagnostic(Diagnostic.Create(RemoveDeletedApiRule, location, propertyBag, cur.Text));
                }
            }

            /// <summary>
            /// Calculated the set of APIs which have been deleted but not yet documented.
            /// </summary>
            /// <returns>The set of APIs which have been deleted but not yet documented.</returns>
            internal List<ApiLine> GetDeletedApiList()
            {
                var list = new List<ApiLine>();
                foreach (var pair in this.publicApiMap)
                {
                    if (this.visitedApiList.Contains(pair.Key))
                    {
                        continue;
                    }

                    if (this.unshippedData.RemovedApiList.Any(x => x.Text == pair.Key))
                    {
                        continue;
                    }

                    list.Add(pair.Value);
                }

                return list;
            }

            private bool IsPublicApi(ISymbol symbol)
            {
                switch (symbol.DeclaredAccessibility)
                {
                case Accessibility.Public:
                    return symbol.ContainingType == null || this.IsPublicApi(symbol.ContainingType);

                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    // Protected symbols must have parent types (that is, top-level protected
                    // symbols are not allowed.
                    return
                        symbol.ContainingType != null &&
                        this.IsPublicApi(symbol.ContainingType) &&
                        this.CanTypeBeExtendedPublicly(symbol.ContainingType);

                default:
                    return false;
                }
            }

            private bool CanTypeBeExtendedPublicly(ITypeSymbol type)
            {
                bool result;
                if (this.typeCanBeExtendedCache.TryGetValue(type, out result))
                {
                    return result;
                }

                // a type can be extended publicly if (1) it isn't sealed, and (2) it has some constructor that is
                // not internal, private or protected & internal
                result = !type.IsSealed &&
                    type.GetMembers(WellKnownMemberNames.InstanceConstructorName).Any(
                        m => m.DeclaredAccessibility != Accessibility.Internal && m.DeclaredAccessibility != Accessibility.Private && m.DeclaredAccessibility != Accessibility.ProtectedAndInternal);

                this.typeCanBeExtendedCache.Add(type, result);
                return result;
            }
        }
    }
}
