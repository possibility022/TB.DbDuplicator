# 03-data-access: data-access

Convert `DatabaseCopier.Proxy` to SDK-style and upgrade it to net10.0. This project contains SQL Server data access code using `System.Data.SqlClient`, which must be migrated to `Microsoft.Data.SqlClient` — the primary source of breaking changes in this tier.

Affects: `DatabaseCopier.Proxy\DatabaseCopier.Proxy.csproj` and all `.cs` files referencing `System.Data.SqlClient`.

**Done when**: Project is SDK-style, targets net10.0, `Microsoft.Data.SqlClient` replaces `System.Data.SqlClient` throughout, project builds successfully.
