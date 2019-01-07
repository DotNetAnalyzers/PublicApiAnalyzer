# Public API Analyzer

[![Join the chat at https://gitter.im/DotNetAnalyzers/PublicApiAnalyzer](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/DotNetAnalyzers/PublicApiAnalyzer?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

[![Build status](https://ci.appveyor.com/api/projects/status/27963rsy48aseywm/branch/master?svg=true)](https://ci.appveyor.com/project/sharwell/publicapianalyzer/branch/master)

[![codecov.io](http://codecov.io/github/DotNetAnalyzers/PublicApiAnalyzer/coverage.svg?branch=master)](http://codecov.io/github/DotNetAnalyzers/PublicApiAnalyzer?branch=master)

## Using Public API Analyzer

The preferable way to use this package is to add the NuGet package [DotNetAnalyzers.PublicApiAnalyzer](http://www.nuget.org/packages/DotNetAnalyzers.PublicApiAnalyzer/)
to the project where you want to enforce rules.

The severity of individual rules may be configured using [rule set files](https://msdn.microsoft.com/en-us/library/dd264996.aspx)
in Visual Studio 2015.

## Team Considerations

If you use older versions of Visual Studio in addition to Visual Studio 2015, you may still install these analyzers. They will be automatically disabled when you open the project back up in Visual Studio 2013 or earlier.

## Contributing

See [Contributing](CONTRIBUTING.md)
