# SEE_INSADE GPU package - build error fix

Fixed compiler errors reported after the first experimental package:

1. Added `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` to `SEE_INSADE.csproj`.
   ComputeSharp source generators require unsafe blocks to emit valid shader descriptors for `[GeneratedComputeShaderDescriptor]`.

2. Fixed nullable return warnings in generic `GetMapValue<T>` helpers by returning `map[x, y]!`.

3. Kept the shader declaration as:

```csharp
[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct GpuOperatorFilterKernel(...) : IComputeShader
```

This is the form required by ComputeSharp for `GraphicsDevice.For(...)` to accept the shader type after source generation.

## Rebuild

```powershell
dotnet clean .\SEE_INSADE.csproj
dotnet restore .\SEE_INSADE.csproj
dotnet build .\SEE_INSADE.csproj
```

If Visual Studio still shows the old `IComputeShaderDescriptor` error after applying the fix, close Visual Studio, delete `bin` and `obj`, then reopen the solution. That error can remain cached when source generation previously failed.
