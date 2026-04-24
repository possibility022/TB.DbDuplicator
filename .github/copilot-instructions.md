# DatabaseCopier - AI Agent Instructions

## Project Overview
A .NET Framework 4.8 WPF desktop application for bulk copying data between SQL Server databases using `SqlBulkCopy`.

## Solution Structure

| Project | Type | Purpose |
|---------|------|---------|
| `DatabaseCopier` | WPF App | Main UI application with MVVM pattern |
| `DatabaseCopier.Proxy` | Class Library | Data access layer for SQL Server operations |
| `DatabaseCopier.Models` | Class Library | Domain models and business logic |

## Key Files

### DatabaseCopier (UI Layer)
- `MainWindow.xaml` / `MainWindow.xaml.cs` - Main application window
- `ViewModels/MainWindowViewModel.cs` - MVVM ViewModel with commands and bindings
- `Engine.cs` - Orchestrates the copy process with progress events
- `CacheFile.cs` - Persists connection strings and ignored tables to `cache.cache`
- `Commands/RelayCommand.cs` - ICommand implementation
- `Converters/SecoundsToTimeConverter.cs` - Time display converter

### DatabaseCopier.Proxy (Data Access)
- `DatabaseIO.cs` - Core SQL operations:
  - `GetSchemas()` - Reads `sys.schemas`
  - `GetTables()` - Reads `sys.tables`
  - `GetForeignKeys()` - Reads `sys.foreign_keys`
  - `GetRows()` - Counts rows in a table
  - `CreateTableIfNotExists()` - Creates schema/table in target DB
  - `CopyTable()` - Bulk copies data using `SqlBulkCopy`

### DatabaseCopier.Models (Domain)
- `TableNode.cs` - Represents a database table with schema, FK relationships
- `TableSchema.cs` - Represents a database schema
- `ForeignKey.cs` - Represents a foreign key relationship
- `Hierarchy.cs` - Topological sort of tables by FK dependencies

## Technical Details

### Dependencies
- .NET Framework 4.8
- Prism.Core (MVVM framework)
- Newtonsoft.Json (serialization)
- System.Data.SqlClient (SQL Server connectivity)

### Key Features
1. **SqlBulkCopy** - Batch size: 30,000 rows, configurable timeout
2. **FK Ordering** - Tables copied in dependency order to avoid constraint violations
3. **Temporal Tables** - Full support for system-versioned temporal tables (see below)
4. **Auto Schema/Table Creation** - Creates missing structures in target DB
5. **Progress Tracking** - Events for row progress and table completion
6. **Connection Caching** - Saves settings to `cache.cache` (JSON)

### Temporal Table Handling
The tool properly handles SQL Server system-versioned temporal tables:

1. **Detection** - `GetTables()` reads `temporal_type` and `history_table_id` from `sys.tables`
2. **Linking** - `TableNode.HistoryTableNode` and `TableNode.MainTemporalTableNode` link main↔history tables
3. **Ordering** - `Hierarchy.BuildReferences()` adds history tables as dependencies so they're copied first
4. **Copy Process**:
   - Disables `SYSTEM_VERSIONING` on target if already enabled
   - Copies data including hidden period columns (`SysStartTime`, `SysEndTime`)
   - Re-enables `SYSTEM_VERSIONING` pointing to the history table
5. **User Exclusion** - If user excludes history table from copy, the main table still copies correctly; versioning will be enabled pointing to the existing history table in target

Key files for temporal logic:
- `DatabaseIO.GetTables()` - Fetches temporal relationships
- `DatabaseIO.GetTemporalInfo()` - Gets period column details
- `DatabaseIO.CopyTable()` - Handles versioning disable/enable
- `Hierarchy.BuildReferences()` - Adds temporal dependencies

### Data Flow
1. User enters source/destination connection strings
2. `Load` → `DatabaseIO.GetTables()` fetches table metadata including temporal relationships
3. User selects tables (move between copy/ignore lists)
4. `Start` → `Hierarchy.GetTablesInOrder()` sorts by FK and temporal dependencies
5. `Engine.Start()` iterates tables:
   - `CreateTableIfNotExists()` ensures target table exists
   - `GetRows()` counts source rows
   - `CopyTable()` bulk copies with progress notifications

### Important Patterns
- **MVVM** - ViewModel bindings in `MainWindowViewModel.cs`
- **Events** - `Engine` exposes `RowsCopiedNotify`, `StartingWith`, `DoneWith`
- **Async** - `Engine.StartAsync()` for non-blocking UI

## Build & Run
```powershell
# Restore packages and build
msbuild DatabaseCopier\DatabaseCopier.sln /t:Restore
msbuild DatabaseCopier\DatabaseCopier.sln /p:Configuration=Release

# Output: DatabaseCopier\bin\Release\DatabaseCopier.exe
```

## Common Tasks

### Adding a new column type
Edit `DatabaseIO.CreateTableIfNotExists()` switch statement for type mapping.

### Modifying bulk copy behavior
Edit `DatabaseIO.CopyTable()` - adjust `BatchSize`, `BulkCopyTimeout`, or `SqlBulkCopyOptions`.

### Adding UI features
Follow MVVM: add property to `MainWindowViewModel.cs`, bind in `MainWindow.xaml`.

### Changing table ordering logic
Modify `Hierarchy.GetTablesInOrder()` algorithm.
