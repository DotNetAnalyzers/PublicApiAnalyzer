// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.ExceptionServices;
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

        private static readonly bool AvoidAdditionalTextGetText;

        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, FieldInfo>> FieldInfos =
            new ConcurrentDictionary<Type, ConcurrentDictionary<string, FieldInfo>>();

        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, PropertyInfo>> PropertyInfos =
            new ConcurrentDictionary<Type, ConcurrentDictionary<string, PropertyInfo>>();

        static SettingsHelper()
        {
            // dotnet/roslyn#6596 was fixed for Roslyn 1.2
            AvoidAdditionalTextGetText = typeof(AdditionalText).GetTypeInfo().Assembly.GetName().Version < new Version(1, 2, 0, 0);
        }

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
                    SourceText additionalTextContent = GetText(additionalFile, cancellationToken);
                    return additionalTextContent;
                }
            }

            return null;
        }

        /// <summary>
        /// This code works around dotnet/roslyn#6596 by using reflection APIs to bypass the problematic method while
        /// reading the content of an <see cref="AdditionalText"/> file. If the reflection approach fails, the code
        /// falls back to the previous behavior.
        /// </summary>
        /// <param name="additionalText">The additional text to read.</param>
        /// <param name="cancellationToken">The cancellation token that the operation will observe.</param>
        /// <returns>The content of the additional text file.</returns>
        private static SourceText GetText(AdditionalText additionalText, CancellationToken cancellationToken)
        {
            if (AvoidAdditionalTextGetText)
            {
                object document = GetField(additionalText, "_document");
                if (document != null)
                {
                    object textSource = GetField(document, "textSource");
                    if (textSource != null)
                    {
                        object textAndVersion = CallMethod(textSource, "GetValue", new[] { typeof(CancellationToken) }, cancellationToken);
                        if (textAndVersion != null)
                        {
                            SourceText text = GetProperty(textAndVersion, "Text") as SourceText;
                            if (text != null)
                            {
                                return text;
                            }
                        }
                    }
                }
            }

            return additionalText.GetText(cancellationToken);
        }

        private static object GetField(object obj, string name)
        {
            if (obj == null)
            {
                return null;
            }

            ConcurrentDictionary<string, FieldInfo> fieldsForType = FieldInfos.GetOrAdd(obj.GetType(), _ => new ConcurrentDictionary<string, FieldInfo>());
            FieldInfo fieldInfo;
            if (!fieldsForType.TryGetValue(name, out fieldInfo))
            {
                fieldInfo = fieldsForType.GetOrAdd(name, _ => obj.GetType().GetRuntimeFields().FirstOrDefault(i => i.Name == name));
            }

            return fieldInfo?.GetValue(obj);
        }

        private static object CallMethod(object obj, string name, Type[] parameters, params object[] arguments)
        {
            try
            {
                MethodInfo methodInfo = obj?.GetType().GetRuntimeMethod(name, parameters);
                return methodInfo?.Invoke(obj, arguments);
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private static object GetProperty(object obj, string name)
        {
            if (obj == null)
            {
                return null;
            }

            ConcurrentDictionary<string, PropertyInfo> propertiesForType = PropertyInfos.GetOrAdd(obj.GetType(), _ => new ConcurrentDictionary<string, PropertyInfo>());
            PropertyInfo propertyInfo;
            if (!propertiesForType.TryGetValue(name, out propertyInfo))
            {
                propertyInfo = propertiesForType.GetOrAdd(name, _ => obj.GetType().GetRuntimeProperty(name));
            }

            try
            {
                return propertyInfo?.GetValue(obj);
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
