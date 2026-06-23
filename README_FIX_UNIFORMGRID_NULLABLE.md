# Build fix: UniformGrid + nullable warning

This patch fixes the reported compile errors:

1. `UniformGrid` namespace was missing in `UI/MainWindows/MainWindow.GpuAcceleration.cs`.
   Added:

```csharp
using System.Windows.Controls.Primitives;
```

2. Generic `GetMapValue<T>` helpers were constrained with `where T : notnull` and now return `map[x, y]` without nullable warnings.

After installing, run:

```powershell
dotnet clean .\SEE_INSADE.csproj
Remove-Item -Recurse -Force .\bin, .\obj -ErrorAction SilentlyContinue
dotnet restore .\SEE_INSADE.csproj
dotnet build .\SEE_INSADE.csproj
```
