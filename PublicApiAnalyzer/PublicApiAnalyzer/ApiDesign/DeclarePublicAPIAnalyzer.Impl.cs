// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer.ApiDesign
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Text;
    using PublicApiAnalyzer.Helpers;

    /// <content>
    /// This file contains the main implementation for <see cref="DeclarePublicAPIAnalyzer"/>.
    /// </content>
    internal partial class DeclarePublicAPIAnalyzer : DiagnosticAnalyzer
    {
        private struct RemovedApiLine
        {
            internal RemovedApiLine(string text, ApiLine apiLine)
            {
                this.Text = text;
                this.ApiLine = apiLine;
            }

            public string Text { get; }

            public ApiLine ApiLine { get; }
        }

        private struct ApiData
        {
            internal ApiData(ImmutableArray<ApiLine> apiList, ImmutableArray<RemovedApiLine> removedApiList)
            {
                this.ApiList = apiList;
                this.RemovedApiList = removedApiList;
            }

            public ImmutableArray<ApiLine> ApiList { get; }

            public ImmutableArray<RemovedApiLine> RemovedApiList { get; }
        }

        private sealed class ApiLine
        {
            internal ApiLine(string text, TextSpan span, SourceText sourceText, string path, bool isShippedApi)
            {
                this.Text = text;
                this.Span = span;
                this.SourceText = sourceText;
                this.Path = path;
                this.IsShippedApi = isShippedApi;
            }

            public string Text { get; }

            public TextSpan Span { get; }

            public SourceText SourceText { get; }

            public string Path { get; }

            public bool IsShippedApi { get; }
        }

        private sealed class Impl
        {
            private static readonly HashSet<MethodKind> IgnorableMethodKinds = new HashSet<MethodKind>
            {
                MethodKind.EventAdd,
                MethodKind.EventRemove,
            };

            private readonly Compilation compilation;
            private readonly ApiData unshippedData;
            private readonly Dictionary<ITypeSymbol, bool> typeCanBeExtendedCache = new Dictionary<ITypeSymbol, bool>();
            private readonly HashSet<string> visitedApiList = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, ApiLine> publicApiMap = new Dictionary<string, ApiLine>(StringComparer.Ordinal);

            internal Impl(Compilation compilation, ApiData shippedData, ApiData unshippedData)
            {
                this.compilation = compilation;
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
                this.OnSymbolActionCore(symbolContext.Symbol, symbolContext.ReportDiagnostic);
            }

            internal void OnCompilationEnd(CompilationAnalysisContext context)
            {
                this.ProcessTypeForwardedAttributes(context.Compilation, context.ReportDiagnostic, context.CancellationToken);
                var deletedApiList = this.GetDeletedApiList();
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

            private static string GetErrorMessageName(ISymbol symbol, bool isImplicitlyDeclaredConstructor)
            {
                return isImplicitlyDeclaredConstructor ?
                    string.Format(RoslynDiagnosticsResources.PublicImplicitConstructorErrorMessageName, symbol.ContainingSymbol.ToDisplayString(ShortSymbolNameFormat)) :
                    symbol.ToDisplayString(ShortSymbolNameFormat);
            }

            private static bool ContainsPublicApiName(string apiLineText, string publicApiNameToSearch)
            {
                // Ensure we don't search in parameter list/return type.
                var indexOfParamsList = apiLineText.IndexOf('(');
                if (indexOfParamsList > 0)
                {
                    apiLineText = apiLineText.Substring(0, indexOfParamsList);
                }
                else
                {
                    var indexOfReturnType = apiLineText.IndexOf("->", StringComparison.Ordinal);
                    if (indexOfReturnType > 0)
                    {
                        apiLineText = apiLineText.Substring(0, indexOfReturnType);
                    }
                }

                // Ensure that we don't have any leading characters in matched substring, apart from whitespace.
                var index = apiLineText.IndexOf(publicApiNameToSearch, StringComparison.Ordinal);
                return index == 0 || (index > 0 && apiLineText[index - 1] == ' ');
            }

            /// <summary>Analyzes a symbol.</summary>
            /// <param name="symbol">The symbol to analyze. Will also analyze implicit constructors too.</param>
            /// <param name="reportDiagnostic">Action called to actually report a diagnostic.</param>
            /// <param name="explicitLocation">A location to report the diagnostics for a symbol at. If null, then
            /// the location of the symbol will be used.</param>
            private void OnSymbolActionCore(ISymbol symbol, Action<Diagnostic> reportDiagnostic, Location explicitLocation = null)
            {
                if (!this.IsPublicAPI(symbol))
                {
                    return;
                }

                Debug.Assert(!symbol.IsImplicitlyDeclared, "Assertion failed: !symbol.IsImplicitlyDeclared");
                this.OnSymbolActionCore(symbol, reportDiagnostic, isImplicitlyDeclaredConstructor: false, explicitLocation: explicitLocation);

                // Handle implicitly declared public constructors.
                if (symbol.Kind == SymbolKind.NamedType)
                {
                    var namedType = (INamedTypeSymbol)symbol;
                    if (namedType.InstanceConstructors.Length == 1 &&
                        (namedType.TypeKind == TypeKind.Class || namedType.TypeKind == TypeKind.Struct))
                    {
                        var instanceConstructor = namedType.InstanceConstructors[0];
                        if (instanceConstructor.IsImplicitlyDeclared)
                        {
                            this.OnSymbolActionCore(instanceConstructor, reportDiagnostic, isImplicitlyDeclaredConstructor: true, explicitLocation: explicitLocation);
                        }
                    }
                }
            }

            /// <summary>Analyzes a Public API symbol.</summary>
            /// <param name="symbol">The symbol to analyze.</param>
            /// <param name="reportDiagnostic">Action called to actually report a diagnostic.</param>
            /// <param name="isImplicitlyDeclaredConstructor">If the symbol is an implicitly declared constructor.</param>
            /// <param name="explicitLocation">A location to report the diagnostics for a symbol at. If null, then
            /// the location of the symbol will be used.</param>
            private void OnSymbolActionCore(ISymbol symbol, Action<Diagnostic> reportDiagnostic, bool isImplicitlyDeclaredConstructor, Location explicitLocation = null)
            {
                Debug.Assert(this.IsPublicAPI(symbol), "Assertion failed: this.IsPublicAPI(symbol)");

                string publicApiName = this.GetPublicApiName(symbol);
                this.visitedApiList.Add(publicApiName);

                var locationsToReport = new List<Location>();

                if (explicitLocation != null)
                {
                    locationsToReport.Add(explicitLocation);
                }
                else
                {
                    var locations = isImplicitlyDeclaredConstructor ? symbol.ContainingType.Locations : symbol.Locations;
                    locationsToReport.AddRange(locations.Where(l => l.IsInSource));
                }

                void ReportDiagnosticAtLocations(DiagnosticDescriptor descriptor, ImmutableDictionary<string, string> propertyBag, params object[] args)
                {
                    foreach (var location in locationsToReport)
                    {
                        reportDiagnostic(Diagnostic.Create(descriptor, location, propertyBag, args));
                    }
                }

                var hasPublicApiEntry = this.publicApiMap.TryGetValue(publicApiName, out ApiLine apiLine);
                if (!hasPublicApiEntry)
                {
                    // Unshipped public API with no entry in public API file - report diagnostic.
                    string errorMessageName = GetErrorMessageName(symbol, isImplicitlyDeclaredConstructor);

                    // Compute public API names for any stale siblings to remove from unshipped text (e.g. during signature change of unshipped public API).
                    var siblingPublicApiNamesToRemove = this.GetSiblingNamesToRemoveFromUnshippedText(symbol);
                    var propertyBag = ImmutableDictionary<string, string>.Empty
                        .Add(PublicApiNamePropertyBagKey, publicApiName)
                        .Add(MinimalNamePropertyBagKey, errorMessageName)
                        .Add(PublicApiNamesOfSiblingsToRemovePropertyBagKey, siblingPublicApiNamesToRemove);

                    ReportDiagnosticAtLocations(DeclareNewApiRule, propertyBag, errorMessageName);
                }

                if (symbol.Kind == SymbolKind.Method)
                {
                    var method = (IMethodSymbol)symbol;
                    var isMethodShippedApi = hasPublicApiEntry && apiLine.IsShippedApi;

                    // Check if a public API is a constructor that makes this class instantiable, even though the base class
                    // is not instantiable. That API pattern is not allowed, because it causes protected members of
                    // the base class, which are not considered public APIs, to be exposed to subclasses of this class.
                    if (!isMethodShippedApi &&
                        method.MethodKind == MethodKind.Constructor &&
                        method.ContainingType.TypeKind == TypeKind.Class &&
                        !method.ContainingType.IsSealed &&
                        method.ContainingType.BaseType != null &&
                        this.IsPublicApiCore(method.ContainingType.BaseType) &&
                        !this.CanTypeBeExtendedPublicly(method.ContainingType.BaseType))
                    {
                        string errorMessageName = GetErrorMessageName(method, isImplicitlyDeclaredConstructor);
                        var propertyBag = ImmutableDictionary<string, string>.Empty;
                        var locations = isImplicitlyDeclaredConstructor ? method.ContainingType.Locations : method.Locations;
                        reportDiagnostic(Diagnostic.Create(ExposedNoninstantiableType, locations[0], propertyBag, errorMessageName));
                    }

                    // Flag public API with optional parameters that violate backcompat requirements: https://github.com/dotnet/roslyn/blob/master/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.
                    if (method.HasOptionalParameters())
                    {
                        foreach (var overload in method.GetOverloads())
                        {
                            if (!this.IsPublicAPI(overload))
                            {
                                continue;
                            }

                            // Don't flag overloads which have identical params (e.g. overloading a generic and non-generic method with same parameter types).
                            if (overload.Parameters.Length == method.Parameters.Length &&
                                overload.Parameters.Select(p => p.Type).SequenceEqual(method.Parameters.Select(p => p.Type)))
                            {
                                continue;
                            }

                            // RS0026: Symbol '{0}' violates the backcompat requirement: 'Do not add multiple overloads with optional parameters'. See '{1}' for details.
                            var overloadHasOptionalParams = overload.HasOptionalParameters();
                            if (overloadHasOptionalParams)
                            {
                                // Flag only if 'method' is a new unshipped API with optional parameters.
                                if (!isMethodShippedApi)
                                {
                                    string errorMessageName = GetErrorMessageName(method, isImplicitlyDeclaredConstructor);
                                    ReportDiagnosticAtLocations(AvoidMultipleOverloadsWithOptionalParameters, ImmutableDictionary<string, string>.Empty, errorMessageName, AvoidMultipleOverloadsWithOptionalParameters.HelpLinkUri);
                                    break;
                                }
                            }

                            // RS0027: Symbol '{0}' violates the backcompat requirement: 'Public API with optional parameter(s) should have the most parameters amongst its public overloads'. See '{1}' for details.
                            if (method.Parameters.Length <= overload.Parameters.Length)
                            {
                                // 'method' is unshipped: Flag regardless of whether the overload is shipped/unshipped.
                                // 'method' is shipped:   Flag only if overload is unshipped and has no optional parameters (overload will already be flagged with RS0026)
                                if (!isMethodShippedApi)
                                {
                                    string errorMessageName = GetErrorMessageName(method, isImplicitlyDeclaredConstructor);
                                    ReportDiagnosticAtLocations(OverloadWithOptionalParametersShouldHaveMostParameters, ImmutableDictionary<string, string>.Empty, errorMessageName, OverloadWithOptionalParametersShouldHaveMostParameters.HelpLinkUri);
                                    break;
                                }
                                else if (!overloadHasOptionalParams)
                                {
                                    var overloadPublicApiName = this.GetPublicApiName(overload);
                                    var isOverloadUnshipped = !this.publicApiMap.TryGetValue(overloadPublicApiName, out ApiLine overloadPublicApiLine) ||
                                        !overloadPublicApiLine.IsShippedApi;
                                    if (isOverloadUnshipped)
                                    {
                                        string errorMessageName = GetErrorMessageName(method, isImplicitlyDeclaredConstructor);
                                        ReportDiagnosticAtLocations(OverloadWithOptionalParametersShouldHaveMostParameters, ImmutableDictionary<string, string>.Empty, errorMessageName, OverloadWithOptionalParametersShouldHaveMostParameters.HelpLinkUri);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            private string GetSiblingNamesToRemoveFromUnshippedText(ISymbol symbol)
            {
                // Don't crash the analyzer if we are unable to determine stale entries to remove in public API text.
                try
                {
                    return this.GetSiblingNamesToRemoveFromUnshippedTextCore(symbol);
                }
                catch (Exception ex)
                {
                    Debug.Assert(false, ex.Message);
                    return string.Empty;
                }
            }

            private string GetSiblingNamesToRemoveFromUnshippedTextCore(ISymbol symbol)
            {
                // Compute all sibling names that must be removed from unshipped text, as they are no longer public or have been changed.
                if (symbol.ContainingSymbol is INamespaceOrTypeSymbol containingSymbol)
                {
                    // First get the lines in the unshipped text for siblings of the symbol:
                    //  (a) Contains Public API name of containing symbol.
                    //  (b) Doesn't contain Public API name of nested types/namespaces of containing symbol.
                    var containingSymbolPublicApiName = this.GetPublicApiName(containingSymbol);

                    var nestedNamespaceOrTypeMembers = containingSymbol.GetMembers().OfType<INamespaceOrTypeSymbol>().ToImmutableArray();
                    var nestedNamespaceOrTypesPublicApiNames = new List<string>(nestedNamespaceOrTypeMembers.Length);
                    foreach (var nestedNamespaceOrType in nestedNamespaceOrTypeMembers)
                    {
                        var nestedNamespaceOrTypePublicApiName = this.GetPublicApiName(nestedNamespaceOrType);
                        nestedNamespaceOrTypesPublicApiNames.Add(nestedNamespaceOrTypePublicApiName);
                    }

                    var publicApiLinesForSiblingsOfSymbol = new HashSet<string>();
                    foreach (var apiLine in this.unshippedData.ApiList)
                    {
                        var apiLineText = apiLine.Text;
                        if (apiLineText == containingSymbolPublicApiName)
                        {
                            // Not a sibling of symbol.
                            continue;
                        }

                        if (!ContainsPublicApiName(apiLineText, containingSymbolPublicApiName + "."))
                        {
                            // Doesn't contain containingSymbol public API name - not a sibling of symbol.
                            continue;
                        }

                        var containedInNestedMember = false;
                        foreach (var nestedNamespaceOrTypePublicApiName in nestedNamespaceOrTypesPublicApiNames)
                        {
                            if (ContainsPublicApiName(apiLineText, nestedNamespaceOrTypePublicApiName + "."))
                            {
                                // Belongs to a nested type/namespace in containingSymbol - not a sibling of symbol.
                                containedInNestedMember = true;
                                break;
                            }
                        }

                        if (containedInNestedMember)
                        {
                            continue;
                        }

                        publicApiLinesForSiblingsOfSymbol.Add(apiLineText);
                    }

                    // Now remove the lines for siblings which are still public APIs - we don't want to remove those.
                    if (publicApiLinesForSiblingsOfSymbol.Count > 0)
                    {
                        var siblings = containingSymbol.GetMembers();
                        foreach (var sibling in siblings)
                        {
                            if (sibling.IsImplicitlyDeclared)
                            {
                                if (!sibling.IsConstructor())
                                {
                                    continue;
                                }
                            }
                            else if (!this.IsPublicAPI(sibling))
                            {
                                continue;
                            }

                            var siblingPublicApiName = this.GetPublicApiName(sibling);
                            publicApiLinesForSiblingsOfSymbol.Remove(siblingPublicApiName);
                        }

                        // Join all the symbols names with a special separator.
                        return string.Join(PublicApiNamesOfSiblingsToRemovePropertyBagValueSeparator, publicApiLinesForSiblingsOfSymbol);
                    }
                }

                return string.Empty;
            }

            private string GetPublicApiName(ISymbol symbol)
            {
                string publicApiName = symbol.ToDisplayString(PublicApiFormat);

                ITypeSymbol memberType = null;
                if (symbol is IMethodSymbol)
                {
                    memberType = ((IMethodSymbol)symbol).ReturnType;
                }
                else if (symbol is IPropertySymbol)
                {
                    memberType = ((IPropertySymbol)symbol).Type;
                }
                else if (symbol is IEventSymbol)
                {
                    memberType = ((IEventSymbol)symbol).Type;
                }
                else if (symbol is IFieldSymbol)
                {
                    memberType = ((IFieldSymbol)symbol).Type;
                }

                if (memberType != null)
                {
                    publicApiName = publicApiName + " -> " + memberType.ToDisplayString(PublicApiFormat);
                }

                if (((symbol as INamespaceSymbol)?.IsGlobalNamespace).GetValueOrDefault())
                {
                    return string.Empty;
                }

                if (symbol.ContainingAssembly != null && !symbol.ContainingAssembly.Equals(this.compilation.Assembly))
                {
                    publicApiName += $" (forwarded, contained in {symbol.ContainingAssembly.Name})";
                }

                return publicApiName;
            }

            private void ProcessTypeForwardedAttributes(Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
            {
                var typeForwardedToAttribute = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.TypeForwardedToAttribute");
                if (typeForwardedToAttribute != null)
                {
                    foreach (var attribute in compilation.Assembly.GetAttributes())
                    {
                        if (attribute.AttributeClass.Equals(typeForwardedToAttribute))
                        {
                            if (attribute.AttributeConstructor.Parameters.Length == 1 &&
                                attribute.ConstructorArguments.Length == 1)
                            {
                                var forwardedType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
                                if (forwardedType != null)
                                {
                                    this.VisitForwardedTypeRecursively(forwardedType, reportDiagnostic, attribute.ApplicationSyntaxReference.GetSyntax(cancellationToken).GetLocation(), cancellationToken);
                                }
                            }
                        }
                    }
                }
            }

            private void VisitForwardedTypeRecursively(ISymbol symbol, Action<Diagnostic> reportDiagnostic, Location typeForwardedAttributeLocation, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.OnSymbolActionCore(symbol, reportDiagnostic, typeForwardedAttributeLocation);

                if (symbol is INamedTypeSymbol namedTypeSymbol)
                {
                    foreach (var nestedType in namedTypeSymbol.GetTypeMembers())
                    {
                        this.VisitForwardedTypeRecursively(nestedType, reportDiagnostic, typeForwardedAttributeLocation, cancellationToken);
                    }

                    foreach (var member in namedTypeSymbol.GetMembers())
                    {
                        if (!(member.IsImplicitlyDeclared && member.IsDefaultConstructor()))
                        {
                            this.VisitForwardedTypeRecursively(member, reportDiagnostic, typeForwardedAttributeLocation, cancellationToken);
                        }
                    }
                }
            }

            private bool IsPublicAPI(ISymbol symbol)
            {
                if (symbol is IMethodSymbol methodSymbol && IgnorableMethodKinds.Contains(methodSymbol.MethodKind))
                {
                    return false;
                }

                return this.IsPublicApiCore(symbol);
            }

            private bool IsPublicApiCore(ISymbol symbol)
            {
                switch (symbol.DeclaredAccessibility)
                {
                    case Accessibility.Public:
                        return symbol.ContainingType == null || this.IsPublicApiCore(symbol.ContainingType);
                    case Accessibility.Protected:
                    case Accessibility.ProtectedOrInternal:
                        // Protected symbols must have parent types (that is, top-level protected
                        // symbols are not allowed.
                        return
                            symbol.ContainingType != null &&
                            this.IsPublicApiCore(symbol.ContainingType) &&
                            this.CanTypeBeExtendedPublicly(symbol.ContainingType);
                    default:
                        return false;
                }
            }

            private bool CanTypeBeExtendedPublicly(ITypeSymbol type)
            {
                if (this.typeCanBeExtendedCache.TryGetValue(type, out bool result))
                {
                    return result;
                }

                // a type can be extended publicly if (1) it isn't sealed, and (2) it has some constructor that is
                // not internal, private or protected&internal
                result = !type.IsSealed &&
                    type.GetMembers(WellKnownMemberNames.InstanceConstructorName).Any(
                        m => m.DeclaredAccessibility != Accessibility.Internal && m.DeclaredAccessibility != Accessibility.Private && m.DeclaredAccessibility != Accessibility.ProtectedAndInternal);

                this.typeCanBeExtendedCache.Add(type, result);
                return result;
            }
        }
    }
}
