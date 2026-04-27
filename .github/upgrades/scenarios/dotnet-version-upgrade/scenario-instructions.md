# Scenario Instructions

## Parameters
- **Solution**: C:\workspace\DatabaseCopier\DatabaseCopier\DatabaseCopier.sln
- **Target Framework**: net10.0 (.NET 10.0 LTS)
- **Source Branch**: master
- **Working Branch**: upgrade-to-NET10

## Strategy
**Bottom-Up (Dependency-First)** — Upgrade leaf nodes first (Models → Proxy → WPF App), validate each tier before proceeding.

### Execution Constraints
- Strict tier ordering: Tier N must build successfully before starting Tier N+1
- Between-tier validation: after each tier, confirm the full solution still builds
- System.Data.SqlClient → Microsoft.Data.SqlClient migration required in DatabaseCopier.Proxy
- WPF project requires `<UseWPF>true</UseWPF>` and Windows target in SDK-style format
- Commit after each completed task (After Each Task strategy)

## Preferences
- **Flow Mode**: Automatic
- **Commit Strategy**: After Each Task
- **Pace**: Standard

## Key Decisions Log
- Target framework net10.0 confirmed by user. Working branch upgrade-to-NET10 created from master.
