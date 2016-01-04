// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("PublicApiAnalyzer")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Tunnel Vision Laboratories, LLC")]
[assembly: AssemblyProduct("Public API Analyzer")]
[assembly: AssemblyCopyright("Copyright © Sam Harwell 2016")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: CLSCompliant(false)]
[assembly: NeutralResourcesLanguage("en-US")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0.0-dev")]

#if DEVELOPMENT_KEY
[assembly: InternalsVisibleTo("PublicApiAnalyzer.CodeFixes, PublicKey=00240000048000009400000006020000002400005253413100040000010001006b4f7413340c619a8d40711b6d977d6dbfb1ab7d7c41aed30d385f4e4c40fd52319b9150eca37207c6e9dfe5bed9876aec9d6b592c17529e56b0ca9119efe4a14797fed920955a01186229599f1374f493c6bdee1d9a8f37f09f564f44a35bb953d36e5846b88c2b8a2818cf0fa9f465be73a191293d56c4f4c05732d4d0f6a5")]
#else
[assembly: InternalsVisibleTo("PublicApiAnalyzer.CodeFixes, PublicKey=0024000004800000940000000602000000240000525341310004000001000100ab5523b35a0e5a54a36c50292857295b245020ce1e7b338804b0c52be7843656fba45dd678b227e250cc126b4c7644151b18cd8e7e3db9876eaa20cc1e108b7679bcd0b655e5cb0c5e2c30ae1d17d82e88cd1c8f68b11f9e91663466d37b606675fa66bf2f00df32d3116aa275e3ce7a909af473b9b48b69b14d3f883e99eccf")]
#endif
[assembly: InternalsVisibleTo("PublicApiAnalyzer.Test, PublicKey=00240000048000009400000006020000002400005253413100040000010001008d7949d002a66db66875775e2b20a3bbf6589ea56624495d375c3d1f15d2517d2baa654575f5384b91edf0e3951c0c85a7a0228391d6a92134b14d8720e3926338e4f5b349f8066f2f98a8a83263bb54ba74a41a91ca51e02f4a3feb666a578bb38bd275397051ef4532b03256a159a9fa54102ce3d5718e5afbd794ee15df92")]
