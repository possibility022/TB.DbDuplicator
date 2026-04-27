# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade all 3 projects from .NET Framework 4.8 to .NET 10.0 (LTS)
**Scope**: Small solution — 3 projects across 3 dependency tiers

### Selected Strategy
**Bottom-Up (Dependency-First)** — Upgrade from leaf nodes to root application, tier by tier.
**Rationale**: 3-tier dependency graph with significant breaking changes expected (WPF APIs, System.Data.SqlClient migration, legacy configuration system). Each tier is validated before proceeding.

```
Tier 3: [DatabaseCopier] (WPF App)
              ↓
Tier 2: [DatabaseCopier.Proxy] (Data Access)
              ↓
Tier 1: [DatabaseCopier.Models] (Domain Models)
```

## Tasks

### 01-sdk-prerequisites
Validate the .NET 10 SDK is installed and global.json (if present) is compatible. This is a gate check before any project changes.

**Done when**: .NET 10 SDK confirmed installed; any global.json constraints updated to allow net10.0 builds.

---

### 02-domain-models
Convert `DatabaseCopier.Models` to SDK-style and upgrade it to net10.0. This is the foundation library with no project dependencies — a clean starting point with minimal breaking changes expected.

Affects: `DatabaseCopier.Models\DatabaseCopier.Models.csproj`

**Done when**: Project is SDK-style, targets net10.0, builds successfully with no errors.

---

### 03-data-access
Convert `DatabaseCopier.Proxy` to SDK-style and upgrade it to net10.0. This project contains SQL Server data access code using `System.Data.SqlClient`, which must be migrated to `Microsoft.Data.SqlClient` — the primary source of breaking changes in this tier.

Affects: `DatabaseCopier.Proxy\DatabaseCopier.Proxy.csproj` and all `.cs` files referencing `System.Data.SqlClient`.

**Done when**: Project is SDK-style, targets net10.0, `Microsoft.Data.SqlClient` replaces `System.Data.SqlClient` throughout, project builds successfully.

---

### 04-wpf-application
Convert `DatabaseCopier` (WPF app) to SDK-style and upgrade it to net10.0. This is the most complex tier: WPF on modern .NET requires `<UseWPF>true</UseWPF>`, there are API-level binary/source incompatibilities, and the legacy configuration system (`System.Configuration.ConfigurationManager`) must be addressed.

Affects: `DatabaseCopier\DatabaseCopier.csproj` and all dependent `.cs`/`.xaml` files.

**Done when**: Project is SDK-style, targets net10.0 with `UseWPF` enabled, all API incompatibilities resolved, application builds and runs successfully.

---

### 05-final-validation
Full solution validation after all tiers are upgraded. Run a clean solution build, verify no remaining compatibility issues, and confirm the application launches.

**Done when**: Full solution builds with zero errors, application starts successfully.
