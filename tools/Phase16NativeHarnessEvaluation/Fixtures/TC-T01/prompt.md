# TC-T01 — Rename Interface Method Across Consumers

Rename the `FetchData` method on `IDataSource` to `RetrieveData` across the entire `Tuning.T01` solution.

## Scope

Update every production and test reference to the method, including:

- `DataSource.Contracts/IDataSource.cs` — interface declaration
- `DataSource.Lib/DataRepository.cs` — implementation
- `Consumer.A`, `Consumer.B`, and `Consumer.C` — services that call the method
- `Di.Tests` — tests that resolve `IDataSource` through DI and invoke the method

## Constraints

- Do **not** change DI registration structure (service lifetimes, registration order, or which types are registered).
- Do **not** change test logic beyond updating the method name and related identifiers.
- Do **not** modify `.csproj` or solution files unless required for the rename to compile.

## Success criteria

- `dotnet build Tuning.T01.slnx --no-incremental` exits 0
- `dotnet test Tuning.T01.slnx --no-build` exits 0 with all tests passing
- No identifier `FetchData` remains in any `.cs` file under the workspace
- `IDataSource` exposes `RetrieveData` and all three consumers compile against it

## Verification

```bash
./verify.sh
```
