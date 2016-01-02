// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer.Test
{
    using System;
    using System.Text;
    using Xunit;

    /// <summary>
    /// Unit tests related to the public API surface of PublicApiAnalyzer.dll.
    /// </summary>
    public class PublicApiTests
    {
        /// <summary>
        /// This test ensures all types in PublicApiAnalyzer.dll are marked internal.
        /// </summary>
        /// <remarks>
        /// <para>This test will be removed after the first alpha release of the public API analyzer is installed in
        /// this project.</para>
        /// </remarks>
        [Fact]
        public void TestAllTypesAreInternal()
        {
            StringBuilder publicTypes = new StringBuilder();
            foreach (Type type in typeof(AnalyzerCategory).Assembly.ExportedTypes)
            {
                if (publicTypes.Length > 0)
                {
                    publicTypes.Append(", ");
                }

                publicTypes.Append(type.Name);
            }

            Assert.Equal(string.Empty, publicTypes.ToString());
        }
    }
}
