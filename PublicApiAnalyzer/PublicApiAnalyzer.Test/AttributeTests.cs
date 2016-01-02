// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer.Test
{
    using Xunit;

    public class AttributeTests
    {
        [Fact]
        public void TestNoCodeFixAttributeReason()
        {
            string reason = "Reason";
            var attribute = new NoCodeFixAttribute(reason);
            Assert.Same(reason, attribute.Reason);
        }
    }
}
