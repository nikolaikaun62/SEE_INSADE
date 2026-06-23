# SEE_INSADE GPU acceleration experimental package

Этот пакет добавляет экспериментальный GPU backend для операторских фильтров SEE_INSADE.

## Что добавлено

- `ComputeSharp` 3.2.0 в `SEE_INSADE.csproj`.
- `Core/Imaging/Gpu/GpuImageProcessor.cs` — подготовка буферов, запуск DX12 compute shader, CPU fallback.
- `Core/Imaging/Gpu/GpuFilterKernels.cs` — GPU kernel для операторских фильтров.
- `Core/Imaging/ImageProcessor.cs` — CPU-циклы переведены на `Parallel.For`, операторский view умеет использовать GPU.
- `Core/Config/ConfigManager.cs` — настройка `DisplaySettings.UseGpuAcceleration`.
- `UI/MainWindows/MainWindow.GpuAcceleration.cs` — отдельный partial class, который добавляет галочку `Use GPU acceleration (experimental)` без правки XAML.

## Что переносится на GPU

- Enhanced Color
- High Penetration
- Organic / Inorganic / Metal Focus
- Density Map
- Negative
- Threshold
- Edge Emphasis
- Suspect Highlight
- Brightness / Contrast
- Material Enhancement
- Noise Reduction

## Что специально не переносится на GPU

- логика сканирования и dual-energy модель;
- UI-поток WPF;
- создание `WriteableBitmap`;
- чтение/запись файлов;
- база данных.

## Установка

1. Закрой Visual Studio и приложение SEE_INSADE.
2. Распакуй содержимое этого архива прямо в корень проекта `SEE_INSADE` с заменой файлов.
3. Выполни:

```powershell
dotnet restore .\SEE_INSADE.csproj
dotnet build .\SEE_INSADE.csproj
```

4. Запусти приложение.
5. В панели фильтров появится галочка `Use GPU acceleration (experimental)` и строка статуса backend.

## Откат

Если сборка или запуск не понравятся:

```powershell
git checkout -- SEE_INSADE.csproj Core/Config/ConfigManager.cs Core/Imaging/ImageProcessor.cs
git clean -f Core/Imaging/Gpu UI/MainWindows/MainWindow.GpuAcceleration.cs
```

Если Git пока не установлен, просто верни старые файлы из резервной копии проекта.

## Важное замечание

Это именно экспериментальный GPU backend. Если ComputeSharp/DX12 не стартует, приложение должно продолжить работу через CPU fallback. Для максимальной скорости следующий шаг — держать GPU-буферы между кадрами и обновлять только изменённые участки, а не пересылать всю карту материала/плотности каждый кадр.
