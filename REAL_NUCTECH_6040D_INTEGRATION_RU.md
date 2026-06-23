# Режимы источника сканирования и интеграция Nuctech 6040D

В главном окне есть два режима источника данных:

- `Архивные IMG` - тестирование на реальных `.img` сканах.
- `Nuctech 6040D` - точка подключения к реальной детекторной линейке.

## Архивные IMG

Папка со сканами теперь настраивается прямо в интерфейсе. В нижней панели рядом с выбором источника есть поле пути и кнопка `...`.

Логика работы:

1. `ScanService` берет все `.img` из указанной папки.
2. Каждый файл открывается через `NuctechImgDecoder`.
3. `NuctechImgDecoder` использует native runtime `img2png.dll`.
4. Полученное изображение проигрывается колонка за колонкой, как реальное линейное сканирование.
5. Когда один скан закончился, автоматически загружается следующий `.img`.

Путь хранится в `config.json`:

```json
"ArchiveScanFolder": "C:\\Users\\nikol\\OneDrive\\Desktop\\03"
```

## Управление изображением

В основной зоне проекции:

- колесо мыши - приближение и отдаление;
- зажатая левая кнопка мыши - перемещение изображения;
- правая кнопка мыши - вернуть изображение по размеру окна.

Это сделано для просмотра длинных реальных сканов без открытия отдельного просмотрщика.

## Что найдено в родном ПО

Проверены папки:

- `D:\XRayV3`
- `D:\OISV3`

Главные найденные компоненты:

- `D:\XRayV3\Config\MCBCtrl.ini`
- `D:\XRayV3\Config\MCBCtrl_AX5000H_A01.ini`
- `D:\OISV3\DataCollector.dll`
- `D:\OISV3\CollectControler.dll`
- `D:\OISV3\MCBCtrl.dll`
- `D:\OISV3\MCBCtrl2.dll`
- `D:\OISV3\KeyBoardControl.dll`
- `D:\OISV3\SoftKeyboard.dll`
- `D:\OISV3\HardwareLib.dll`
- `D:\OISV3\Plug-ins\Plugin_TRSLinker_PLC\modbus.dll`
- `D:\OISV3\Plug-ins\Plugin_TRSLinker_PLC\ois_plugin.cfg`
- `D:\XRayV3\Log\modbus_plc_logger.txt`

## Детекторная линейка

В `MCBCtrl.ini` найден контроллер детекторных модулей:

```ini
[CONNECTION]
IPADDR = 192.168.127.240
```

Для одной из конфигураций:

```ini
[MCB]
MCBNUM = 1
MCB0ADNUM = 38

[ADM]
ROW = 64
COLUMN = 2
ADBIT = 16

[MODE0]
POTMT = 2500
DATARDYT = 480
POTMW = 100
INTT = 1250
```

Для другой конфигурации:

```ini
[MCB]
MCBNUM = 4
MCB0ADNUM = 13
MCB1ADNUM = 12
MCB2ADNUM = 13
MCB3ADNUM = 12
```

Вывод: детекторная часть, скорее всего, не COM-порт, а сетевой контроллер MCB. Родное ПО собирает линии через `DataCollector.dll` и `MCBCtrl.dll`.

В строках `DataCollector.dll` найдены признаки live-сбора:

- `Nuctech Collector: started`
- `Nuctech Collector: stopped`
- `GetNextLine`
- `Last Line Index`
- `Lost Net Packet`
- `Net Packet Number Per Line`
- `No interfaces found! Make sure WinPcap is installed.`
- `C:\WINDOWS\NUCTECH_CAP.ini`
- `AlogicalPreprocessMultiEnergy.dll`
- `SaveImageFile`
- `*.img`

Это означает, что реальный сбор линий завязан на сетевой захват пакетов, вероятно через WinPcap/Npcap, и на внутренние классы родного ПО.

## Конвейер и датчики прохода багажа

В `Plugin_TRSLinker_PLC\ois_plugin.cfg` найден PLC/Modbus:

```ini
[modbus]
modbus_address=192.168.250.91
modbus_port=502
```

Там же есть параметры логики прохода багажа:

```ini
[BHS]
work_mode=0
min_infrared_time=500
max_scan_lines=1200
min_scan_lines=200
transport_time=6000
time_delay_stop_at_exit=500
```

В `D:\XRayV3\Log\modbus_plc_logger.txt` есть запуск Modbus:

```text
Initialize Modbus
CModbusCtrl StartWork
```

Вывод: конвейер, датчики входа/выхода и часть BHS-логики вынесены в PLC по Modbus TCP. Для полной интеграции нужно реализовать отдельный транспорт PLC, а не смешивать его с детекторным MCB.

## Клавиатура управления

Найдены:

- `KeyBoardControl.dll`
- `SoftKeyboard.dll`
- секции `KeyboardConfig` в конфигурациях оборудования.

Это отдельный слой управления операторскими кнопками. Его нужно подключать через экспортированные функции DLL или через перехват команд родного интерфейса, если DLL не имеет публичного SDK.

## Текущая точка интеграции в SEE INSADE

Контракт реальной детекторной линейки:

```csharp
bool TryConnect();
bool TryReadLine(out NuctechDetectorLine line);
```

Файлы:

- `Services/Scanning/INuctech6040DDetectorConnection.cs`
- `Services/Scanning/Nuctech6040DDetectorConnection.cs`
- `Services/Scanning/ScanService.cs`

`NuctechDetectorLine` содержит:

```csharp
double[] LowEnergy;
double[] HighEnergy;
```

Для CX6040D_B16N ожидаемая высота детекторной линейки из `.img` - `876`.

Когда настоящий транспорт будет реализован, `ScanService` уже умеет:

1. сдвигать изображение;
2. вставлять новую детекторную колонку;
3. считать dual-energy отношение;
4. классифицировать материал;
5. обновлять фильтры и интерфейс.

## Как двигаться к настоящему подключению

Следующий технический шаг - не угадывать формат, а зафиксировать протоколы:

1. Проверить наличие `C:\WINDOWS\NUCTECH_CAP.ini`.
2. Поставить Npcap/WinPcap совместимый режим, если родное ПО требует pcap.
3. Снять сетевой дамп обмена с `192.168.127.240` при запуске родного ПО и при проходе тестового объекта.
4. Снять Modbus TCP обмен с `192.168.250.91:502` при командах старт/стоп конвейера и срабатывании датчиков.
5. Проверить экспорт функций DLL:
   - `DataCollector.dll`
   - `CollectControler.dll`
   - `MCBCtrl.dll`
   - `MCBCtrl2.dll`
   - `KeyBoardControl.dll`
6. Если экспорты пригодны, сделать P/Invoke-обертку.
7. Если экспорты закрытые C++ decorated API, безопаснее реализовать сетевой транспорт по захваченному протоколу.

## Важное ограничение

Прямое управление рентгеном, конвейером и датчиками нельзя включать “наугад”. Сейчас в программе сделан безопасный режим: реальный источник `Nuctech 6040D` не имитирует успешное подключение, пока не реализован проверенный транспорт.

Архивные `.img` уже можно использовать для удобной проверки интерфейса, фильтров и логики линейного сканирования.
