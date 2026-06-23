# Nuctech/OIS IMG decoder: как работает и как повторять для других моделей

## Что было найдено

Файлы `.img` от CX6040D_B16N не являются простым BGR/BGRA raw-изображением. Внутри есть служебный контейнер OIS/Nuctech, метаданные и бинарные данные скана. Детекторная высота для модели фиксированная, а длина изображения меняется от файла к файлу.

Для CX6040D_B16N подтверждено:

- модель: `CX6040D_B16N`;
- серийный номер читается как `TFNPA08230008`;
- высота детекторной линейки: `876`;
- ширина индивидуальна для каждого скана: например `1824`, `1674`, `961`, `1047`, `1170`;
- родная библиотека `img2png.dll` распаковывает файл в два PNG-представления;
- основное представление `view0.png` используется в SEE INSADE.

## Текущая схема работы

1. Оператор открывает `.img` в плагине `Nuctech IMG Viewer`.
2. `NuctechImgDecoder.Decode(...)` сначала ищет native runtime:
   - `Plugins/NuctechImg/NativeRuntime`;
   - переменную окружения `SEE_INSADE_OIS_SDK`;
   - стандартные пути `D:\OISV3\Plug-ins\Plugin_WeKnow\sdk`, `C:\OISV3\...`, `D:\XRayV3\...`.
3. Если найден `img2png.dll`, запускается отдельный 32-битный helper:
   - `Tools/OisImgNativeDecodeHelper/OisImgNativeDecodeHelper.exe`.
4. Helper грузит `img2png.dll`, читает `.img` целиком в память и вызывает native API:
   - `IMG2PNG_Init(logDir, imgType, modeType)`;
   - `IMG2PNG_set_img(fileName, imgBytesPtr, imgBytesLength, outCount, outItems)`;
   - `IMG2PNG_get_png(buffer, outX, outY, outWidth, outHeight, outChannels, viewId)`.
5. Helper сохраняет `view0.png` и `view1.png` во временную папку.
6. Основной WPF-процесс загружает `view0.png` как `WriteableBitmap`.
7. По цветам PNG строятся:
   - `MaterialMap`;
   - `DensityMap`.
8. Все существующие фильтры SEE INSADE работают уже поверх этой карты.

## Почему helper отдельный

`img2png.dll` является 32-битной DLL. Основное приложение может быть x64 или AnyCPU, поэтому напрямую загрузить эту DLL в основной процесс нельзя. Отдельный x86 helper решает это:

- основной процесс остается обычным WPF-приложением;
- native DLL грузится в совместимом 32-битном процессе;
- результат передается через PNG-файл;
- при ошибке или зависании native DLL можно убить helper, не роняя SEE INSADE.

## Какие файлы нужны runtime

Минимальный рабочий runtime лежит здесь:

`Plugins/NuctechImg/NativeRuntime`

Туда скопированы корневые DLL и конфиги из:

`D:\OISV3\Plug-ins\Plugin_WeKnow\sdk`

Ключевой файл:

- `img2png.dll`

На практике ему также нужны зависимости рядом:

- `opencv_world412.dll`;
- `libstdc++-6.dll`;
- `libgcc_s_dw2-1.dll`;
- `libwinpthread-1.dll`;
- `log4cplusU.dll`;
- часть Qt DLL и plugin-папок, если native код их подгружает.

Большие таблицы `ThreatObjectDetection` не нужны для простого открытия `.img` в PNG и в проект не копировались.

## Как повторить для другой модели

1. Собрать 3-5 реальных `.img` с этой модели.
2. Узнать фиксированную высоту детекторной линейки:
   - из метаданных `.img`;
   - из конфигов оборудования;
   - из логов native `img2png.dll`, строка вида `vhw <views> <height> <width>`.
3. Найти родной OIS/Nuctech SDK для этой модели.
4. Проверить наличие `img2png.dll`.
5. Скопировать runtime в:
   - `Plugins/NuctechImg/NativeRuntime`;
   - или указать путь через `SEE_INSADE_OIS_SDK`.
6. Запустить helper вручную:

```powershell
.\Tools\OisImgNativeDecodeHelper\bin\Debug\net8.0-windows\win-x86\OisImgNativeDecodeHelper.exe `
  --sdk ".\Plugins\NuctechImg\NativeRuntime" `
  --img "C:\path\scan.img" `
  --out "C:\Temp\img-test"
```

7. Проверить, что появились:
   - `view0.png`;
   - желательно `view1.png`;
   - `log\img2png_0.txt`.
8. Открыть `view0.png` и сравнить с родным ПО:
   - ориентация;
   - зеркальность;
   - цвета материалов;
   - белый фон;
   - высота и ширина.
9. Если картинка повернута или зеркальна, это исправляется в параметрах окна плагина:
   - `Rotate 90`;
   - `Flip X`;
   - `Flip Y`.
10. Если native decode не работает, только тогда возвращаться к fallback raw-декодеру и искать структуру контейнера вручную.

## Быстрая диагностика

Если изображение не открывается:

- проверить, что `Plugins/NuctechImg/NativeRuntime/img2png.dll` существует;
- проверить, что helper собран как x86;
- проверить, что рядом лежит `opencv_world412.dll`;
- открыть лог helper во временной папке `%TEMP%\SEE_INSADE_OIS_IMG_*`;
- проверить строку `IMG2PNG_set_img finished`;
- проверить строки `viewid 0 pngfilelen ... hwc 876,1170,3`.

Если `IMG2PNG_set_img` падает:

- почти всегда не совпадает сигнатура native вызова;
- текущая рабочая сигнатура:

```text
int IMG2PNG_set_img(char* fileName, void* imgBytes, int imgBytesLength, int* outCount, int* outItems)
```

Если `IMG2PNG_get_png` возвращает пустой буфер:

- проверить `viewId`;
- для CX6040D_B16N рабочие view: `0` и `1`;
- `view0` используется как основное изображение.

## Что нельзя забыть

- Высота детектора фиксирована для модели.
- Ширина изображения индивидуальна для каждого файла.
- Не делить ширину на количество энергетических каналов, если native DLL уже отдала готовый PNG.
- Native путь должен идти первым.
- Raw fallback нужен только как аварийный режим.
