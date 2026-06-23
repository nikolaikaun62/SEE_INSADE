# SEE_INSADE PRO UI FEATURES

Этот пакет ставится поверх GPU experimental/fixed версии и добавляет операторские настройки, улучшенный интерфейс и полезные функции без изменения физической логики сканирования.

## Что добавлено

### 1. PRO OPERATOR PANEL
Справа поверх главного окна появляется новая панель:

- Operator presets
- Performance mode
- GPU backend status
- Useful actions
- Live diagnostics
- Hotkeys

### 2. Готовые операторские пресеты

- Airport Standard
- Maximum Penetration
- Organic Threat
- Metal Search
- Edge Inspection
- Low Noise
- Density Inspector
- High Contrast

Пресеты автоматически меняют:

- основной фильтр;
- силу фильтра;
- brightness;
- contrast;
- material enhancement;
- edge detection;
- noise reduction;
- detector sensitivity.

### 3. Производительность

Режимы:

- High FPS — быстрый UI tick;
- Balanced — обычный режим;
- Quality — более спокойный режим для детального просмотра.

### 4. GPU/CPU backend

На панели видно:

- включён ли GPU;
- доступен ли GPU;
- фактически работает GPU или CPU fallback.

### 5. Snapshot и отчёт

Кнопка `PNG snapshot` сохраняет настоящий PNG кадра в папку `Scans`.

Если включён `Save TXT report with snapshots`, рядом сохраняется TXT отчёт:

- backend;
- GPU status;
- выбранный preset;
- objects;
- non-air pixels;
- dense pixels;
- organic pixels;
- metal pixels;
- suspect pixels;
- risk score;
- material distribution.

### 6. Hotkeys

- F5 — scan forward
- F6 — scan backward
- Space — stop
- Ctrl+R — reset
- Ctrl+S — PNG snapshot + report
- Ctrl+G — GPU on/off
- Ctrl+1..8 — быстрый выбор пресетов

### 7. Улучшения старых кнопок

- `Snapshot` заменён на реальное сохранение PNG + отчёта.
- `Diagnostics` и `Detectors` теперь видимы в верхней панели.
- Добавлены подсказки к основным кнопкам.

## Установка

Скопируй содержимое папки `SEE_INSADE_PRO_UI_FEATURES` в корень проекта `SEE_INSADE` с заменой файлов.

Или запусти:

```powershell
.\install_pro_ui_update.ps1 -ProjectRoot "C:\path\to\SEE_INSADE"
```

После установки:

```powershell
dotnet clean .\SEE_INSADE.csproj
Remove-Item -Recurse -Force .\bin, .\obj -ErrorAction SilentlyContinue
dotnet restore .\SEE_INSADE.csproj
dotnet build .\SEE_INSADE.csproj
dotnet run --project .\SEE_INSADE.csproj
```

## Важно

Пакет не трогает `ScanService` и не меняет саму модель сканирования. Он добавляет поверх неё более удобный операторский слой, экспорт, настройки производительности и контроль GPU/CPU backend.
