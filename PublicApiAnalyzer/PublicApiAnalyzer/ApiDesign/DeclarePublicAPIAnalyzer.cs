﻿// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer.ApiDesign
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Text;

    /// <summary>
    /// This file contains the descriptors and driver for the Public API analyzer diagnostics.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal sealed partial class DeclarePublicAPIAnalyzer : DiagnosticAnalyzer
    {
        internal const string ShippedFileName = "PublicAPI.Shipped.txt";
        internal const string UnshippedFileName = "PublicAPI.Unshipped.txt";
        internal const string PublicApiNamePropertyBagKey = "PublicAPIName";
        internal const string MinimalNamePropertyBagKey = "MinimalName";
        internal const string RemovedApiPrefix = "*REMOVED*";
        internal const string InvalidReasonShippedCantHaveRemoved = "The shipped API file can't have removed members";

        internal static readonly DiagnosticDescriptor DeclareNewApiRule = new DiagnosticDescriptor(
            id: RoslynDiagnosticIds.DeclarePublicApiRuleId,
            title: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.DeclarePublicApiTitle), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            messageFormat: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.DeclarePublicApiMessage), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            category: AnalyzerCategory.ApiDesign,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.DeclarePublicApiDescription), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            customTags: WellKnownDiagnosticTags.Telemetry);

        internal static readonly DiagnosticDescriptor RemoveDeletedApiRule = new DiagnosticDescriptor(
            id: RoslynDiagnosticIds.RemoveDeletedApiRuleId,
            title: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.RemoveDeletedApiTitle), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            messageFormat: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.RemoveDeletedApiMessage), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            category: AnalyzerCategory.ApiDesign,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.RemoveDeletedApiDescription), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            customTags: WellKnownDiagnosticTags.Telemetry);

        internal static readonly DiagnosticDescriptor ExposedNoninstantiableType = new DiagnosticDescriptor(
            id: RoslynDiagnosticIds.ExposedNoninstantiableTypeRuleId,
            title: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.ExposedNoninstantiableTypeTitle), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            messageFormat: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.ExposedNoninstantiableTypeMessage), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            category: AnalyzerCategory.ApiDesign,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.ExposedNoninstantiableTypeDescription), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            customTags: WellKnownDiagnosticTags.Telemetry);

        internal static readonly DiagnosticDescriptor PublicApiFilesInvalid = new DiagnosticDescriptor(
            id: RoslynDiagnosticIds.PublicApiFilesInvalid,
            title: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.PublicApiFilesInvalidTitle), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            messageFormat: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.PublicApiFilesInvalidMessage), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            category: AnalyzerCategory.ApiDesign,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.Telemetry);

        internal static readonly DiagnosticDescriptor DuplicateSymbolInApiFiles = new DiagnosticDescriptor(
            id: RoslynDiagnosticIds.DuplicatedSymbolInPublicApiFiles,
            title: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.DuplicateSymbolsInPublicApiFilesTitle), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            messageFormat: new LocalizableResourceString(nameof(RoslynDiagnosticsResources.DuplicateSymbolsInPublicApiFilesMessage), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources)),
            category: AnalyzerCategory.ApiDesign,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.Telemetry);

        internal static readonly SymbolDisplayFormat ShortSymbolNameFormat =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                memberOptions: SymbolDisplayMemberOptions.None,
                parameterOptions: SymbolDisplayParameterOptions.None,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.None);

        private static readonly SymbolDisplayFormat PublicApiFormat =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeModifiers | SymbolDisplayMemberOptions.IncludeConstantValue,
                parameterOptions: SymbolDisplayParameterOptions.IncludeExtensionThis | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DeclareNewApiRule, RemoveDeletedApiRule, ExposedNoninstantiableType, PublicApiFilesInvalid, DuplicateSymbolInApiFiles);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(this.OnCompilationStart);
        }

        internal static string GetPublicApiName(ISymbol symbol)
        {
            var publicApiName = symbol.ToDisplayString(PublicApiFormat);

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

            return publicApiName;
        }

        private static ApiData ReadApiData(string path, SourceText sourceText)
        {
            var apiBuilder = ImmutableArray.CreateBuilder<ApiLine>();
            var removedBuilder = ImmutableArray.CreateBuilder<RemovedApiLine>();

            foreach (var line in sourceText.Lines)
            {
                var text = line.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var apiLine = new ApiLine(text, line.Span, sourceText, path);
                if (text.StartsWith(RemovedApiPrefix, StringComparison.Ordinal))
                {
                    var removedtext = text.Substring(RemovedApiPrefix.Length);
                    removedBuilder.Add(new RemovedApiLine(removedtext, apiLine));
                }
                else
                {
                    apiBuilder.Add(apiLine);
                }
            }

            return new ApiData(apiBuilder.ToImmutable(), removedBuilder.ToImmutable());
        }

        private static bool TryGetApiData(ImmutableArray<AdditionalText> additionalTexts, CancellationToken cancellationToken, out ApiData shippedData, out ApiData unshippedData)
        {
            AdditionalText shippedText;
            AdditionalText unshippedText;
            if (!TryGetApiText(additionalTexts, cancellationToken, out shippedText, out unshippedText))
            {
                shippedData = default(ApiData);
                unshippedData = default(ApiData);
                return false;
            }

            shippedData = ReadApiData(ShippedFileName, shippedText.GetText(cancellationToken));
            unshippedData = ReadApiData(UnshippedFileName, unshippedText.GetText(cancellationToken));
            return true;
        }

        private static bool TryGetApiText(ImmutableArray<AdditionalText> additionalTexts, CancellationToken cancellationToken, out AdditionalText shippedText, out AdditionalText unshippedText)
        {
            shippedText = null;
            unshippedText = null;

            var comparer = StringComparer.Ordinal;
            foreach (var text in additionalTexts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(text.Path);
                if (comparer.Equals(fileName, ShippedFileName))
                {
                    shippedText = text;
                    continue;
                }

                if (comparer.Equals(fileName, UnshippedFileName))
                {
                    unshippedText = text;
                    continue;
                }
            }

            return shippedText != null && unshippedText != null;
        }

        private static void ValidateApiList(Dictionary<string, ApiLine> publicApiMap, ImmutableArray<ApiLine> apiList, List<Diagnostic> errors)
        {
            foreach (var cur in apiList)
            {
                ApiLine existingLine;
                if (publicApiMap.TryGetValue(cur.Text, out existingLine))
                {
                    var existingLinePositionSpan = existingLine.SourceText.Lines.GetLinePositionSpan(existingLine.Span);
                    var existingLocation = Location.Create(existingLine.Path, existingLine.Span, existingLinePositionSpan);

                    var duplicateLinePositionSpan = cur.SourceText.Lines.GetLinePositionSpan(cur.Span);
                    var duplicateLocation = Location.Create(cur.Path, cur.Span, duplicateLinePositionSpan);
                    errors.Add(Diagnostic.Create(DuplicateSymbolInApiFiles, duplicateLocation, new[] { existingLocation }, cur.Text));
                }
                else
                {
                    publicApiMap.Add(cur.Text, cur);
                }
            }
        }

        private void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            var additionalFiles = compilationContext.Options.AdditionalFiles;

            ApiData shippedData;
            ApiData unshippedData;
            if (!TryGetApiData(additionalFiles, compilationContext.CancellationToken, out shippedData, out unshippedData))
            {
                return;
            }

            List<Diagnostic> errors;
            if (!this.ValidateApiFiles(shippedData, unshippedData, out errors))
            {
                compilationContext.RegisterCompilationEndAction(context =>
                {
                    foreach (var cur in errors)
                    {
                        context.ReportDiagnostic(cur);
                    }
                });

                return;
            }

            var impl = new Impl(shippedData, unshippedData);
            compilationContext.RegisterSymbolAction(
                impl.OnSymbolAction,
                SymbolKind.NamedType,
                SymbolKind.Event,
                SymbolKind.Field,
                SymbolKind.Method);
            compilationContext.RegisterCompilationEndAction(impl.OnCompilationEnd);
        }

        private bool ValidateApiFiles(ApiData shippedData, ApiData unshippedData, out List<Diagnostic> errors)
        {
            errors = new List<Diagnostic>();
            if (shippedData.RemovedApiList.Length > 0)
            {
                errors.Add(Diagnostic.Create(PublicApiFilesInvalid, Location.None, InvalidReasonShippedCantHaveRemoved));
            }

            var publicApiMap = new Dictionary<string, ApiLine>(StringComparer.Ordinal);
            ValidateApiList(publicApiMap, shippedData.ApiList, errors);
            ValidateApiList(publicApiMap, unshippedData.ApiList, errors);

            return errors.Count == 0;
        }
    }
}
