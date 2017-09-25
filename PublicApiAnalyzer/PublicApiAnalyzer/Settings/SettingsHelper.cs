// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer
{
    using System.Collections.Immutable;
    using System.IO;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Text;

    /// <summary>
    /// Class that manages the settings files for PublicApiAnalyzer.
    /// </summary>
    internal static class SettingsHelper
    {
        internal const string PublicApiFileName = "PublicAPI.Unshipped.txt";

        /// <summary>
        /// Gets the public API text.
        /// </summary>
        /// <param name="context">The context that will be used to determine the public API.</param>
        /// <param name="cancellationToken">The cancellation token that the operation will observe.</param>
        /// <returns>The contents of the public API document for the compilation, or <see langword="null"/> if no public
        /// API document is part of the compilation.</returns>
        internal static SourceText GetPublicApi(this SyntaxTreeAnalysisContext context, CancellationToken cancellationToken)
        {
            return context.Options.GetPublicApi(cancellationToken);
        }

        /// <summary>
        /// Gets the public API text.
        /// </summary>
        /// <param name="options">The analyzer options that will be used to determine the public API.</param>
        /// <param name="cancellationToken">The cancellation token that the operation will observe.</param>
        /// <returns>The contents of the public API document for the compilation, or <see langword="null"/> if no public
        /// API document is part of the compilation.</returns>
        internal static SourceText GetPublicApi(this AnalyzerOptions options, CancellationToken cancellationToken)
        {
            return GetPublicApi(options != null ? options.AdditionalFiles : ImmutableArray.Create<AdditionalText>(), cancellationToken);
        }

        private static SourceText GetPublicApi(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
        {
            foreach (var additionalFile in additionalFiles)
            {
                if (Path.GetFileName(additionalFile.Path).ToLowerInvariant() == PublicApiFileName)
                {
                    SourceText additionalTextContent = additionalFile.GetText(cancellationToken);
                    return additionalTextContent;
                }
            }

            return null;
        }
    }
}
