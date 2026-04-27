# .NET Version Upgrade Progress

## Overview

Upgrading 3 projects from .NET Framework 4.8 to .NET 10.0 (LTS) using a Bottom-Up strategy. Projects are upgraded tier by tier: domain models first, then data access, then the WPF application.

**Progress**: 3/5 tasks complete (60%) ![60%](https://progress-bar.xyz/60)

## Tasks

- ✅ 01-sdk-prerequisites: Validate .NET 10 SDK and global.json
- ✅ 02-domain-models: Upgrade DatabaseCopier.Models to net10.0
- ✅ 03-data-access: Upgrade DatabaseCopier.Proxy to net10.0 (SqlClient migration)
- 🔄 04-wpf-application: Upgrade DatabaseCopier WPF app to net10.0
- 🔲 05-final-validation: Full solution validation
