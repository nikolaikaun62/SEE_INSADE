# Nuctech IMG Viewer standalone

Это отдельная программа для открытия `.img` файлов Nuctech/OIS без запуска основной оболочки SEE INSADE.

## Что умеет

- открывает `.img`;
- использует тот же native-декодер через `img2png.dll`;
- показывает реальное цветное X-ray изображение;
- применяет операторские фильтры;
- экспортирует текущий вид в PNG.

## Сборка

```powershell
dotnet build .\Standalone\NuctechImgViewer\NuctechImgViewer.csproj
```

## Запуск

```powershell
.\Standalone\NuctechImgViewer\bin\Debug\net8.0-windows\NuctechImgViewer.exe
```

## Runtime

Программа ищет native runtime в:

1. своей output-папке `Plugins\NuctechImg\NativeRuntime`;
2. `SEE_INSADE_OIS_SDK`;
3. стандартных папках OIS/XRay.

В проект уже добавлен runtime:

`Plugins\NuctechImg\NativeRuntime`

Если нужно заменить библиотеки под другую модель, заменить содержимое этой папки на SDK от нужного оборудования и проверить через `NUCTECH_IMG_DECODER_GUIDE_RU.md`.
