# 04-wpf-application: wpf-application

Convert `DatabaseCopier` (WPF app) to SDK-style and upgrade it to net10.0. This is the most complex tier: WPF on modern .NET requires `<UseWPF>true</UseWPF>`, there are API-level binary/source incompatibilities, and the legacy configuration system (`System.Configuration.ConfigurationManager`) must be addressed.

Affects: `DatabaseCopier\DatabaseCopier.csproj` and all dependent `.cs`/`.xaml` files.

**Done when**: Project is SDK-style, targets net10.0 with `UseWPF` enabled, all API incompatibilities resolved, application builds and runs successfully.
