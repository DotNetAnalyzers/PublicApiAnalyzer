// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace PublicApiAnalyzer.Helpers
{
    using System;

    internal static class ObjectExtensions
    {
        public static TResult TypeSwitch<TBaseType, TDerivedType1, TDerivedType2, TResult>(this TBaseType obj, Func<TDerivedType1, TResult> matchFunc1, Func<TDerivedType2, TResult> matchFunc2, Func<TBaseType, TResult> defaultFunc = null)
            where TDerivedType1 : TBaseType
            where TDerivedType2 : TBaseType
        {
            if (obj is TDerivedType1 derived1)
            {
                return matchFunc1(derived1);
            }
            else if (obj is TDerivedType2 derived2)
            {
                return matchFunc2(derived2);
            }
            else if (defaultFunc != null)
            {
                return defaultFunc(obj);
            }
            else
            {
                return default;
            }
        }
    }
}
