# Jonsbo D34 Mon

**Экспериментальная** лёгкая альтернатива программе *PC Monitor All* от JONSBO для экрана корпуса **JONSBO D34**:
время, температура CPU (со шкалой), температура GPU, загрузка и частота CPU.

*An **experimental** lightweight alternative to JONSBO's PC Monitor All for the JONSBO D34 case screen.
[English below](#english).*

<img src="docs/window.png" width="300" alt="Окно программы: копия экрана корпуса">

> ⚠️ Это эксперимент одного энтузиаста, а не продукт. Проверено на одном компьютере.
> Перед установкой прочитайте раздел [«Риски и ограничения»](#риски-и-ограничения).
> Проект не связан с JONSBO.

## Было → стало

Оригинальная программа *PC Monitor All 1.0* и Jonsbo D34 Mon 1.1 на одном и том же ПК, в фоне (свёрнуто в трей),
замер за 60 секунд. Ryzen 7 9800X3D, RTX 5070 Ti, Windows 11.

| | PC Monitor All 1.0 | Jonsbo D34 Mon 1.1 |
|---|---|---|
| Нагрузка на CPU | **15–19 %** одного ядра, постоянно | **0,55 %** одного ядра |
| Память (собственная) | ~75 МБ | **~16 МБ** |
| Дескрипторы (handles) | **утечка**: 44 000 через 2,5 ч, +5 в секунду | стабильно ~410 |
| Потоки | 23 | 12 |
| Опрос датчиков | все 22 устройства HWiNFO каждую секунду (включая планки памяти по 30 мс) | только 4 нужных значения |
| Отправка на экран | 5 раз/с, обычный приоритет | 4 раза/с из потока с высоким приоритетом: экран не гаснет даже при 100 % загрузке CPU (макс. пауза 265 мс) |
| Зависимости | закрытая библиотека HWiNFO SDK, лицензированная JONSBO | открытые: LibreHardwareMonitor + драйвер PawnIO |
| Окно | перегруженное, 11 выпадающих списков | копия экрана корпуса; ⚙ у значения — выбор датчика |
| Тема / язык | — / китайский, английский | светлая и тёмная / русский, английский |

Что было не так в оригинале (найдено при разборе его кода):

- 20 раз в секунду перечитывает список всех процессов Windows, только чтобы проверить, не запущена ли вторая копия. Отсюда нагрузка на CPU и утечка дескрипторов;
- каждую секунду перебирает 512 индексов × 8 типов для каждого датчика HWiNFO;
- напряжение CPU передаётся на экран с ошибкой (всегда «1.00 V»);
- автозапуск нельзя выключить: галочка убирает запись из реестра, но ярлык в «Автозагрузке» остаётся;
- пункт «Показ на экране» ничего не делает, язык не сохраняется, при закрытии процесс убивает сам себя.

## Установка

Скачайте файлы со страницы [Releases](../../releases):

| Файл | Что это |
|---|---|
| `Jonsbo D34 Mon 1.1.0 (ru).msi` / `(en).msi` | установщик: Program Files, ярлыки, удаление через «Приложения» |
| `Jonsbo D34 Mon 1.1.0 portable.zip` | портативная версия: распаковать и запустить, настройки рядом с exe |
| `SHA256SUMS.txt` | контрольные суммы файлов |

- .NET уже встроен, ставить ничего не нужно.
- Установщик сам поставит драйвер датчиков [PawnIO](https://pawnio.eu), если его ещё нет. В портативной версии он лежит в архиве (`PawnIO_setup.exe`): запустите его один раз, если температура CPU показывает `--`.
- После первого запуска включите в окне **«Автозапуск»**.
- **Отключите автозапуск старого PC Monitor All** (Диспетчер задач → Автозагрузка): две программы одновременно заставляют экран мигать.
- Установщик не подписан сертификатом, поэтому Windows покажет «Windows защитил ваш компьютер»: «Подробнее» → «Выполнить в любом случае». Сверить файл можно по `SHA256SUMS.txt`.

**Удаление:** через «Приложения» (MSI) или «Выгрузить из памяти» → выключить «Автозапуск» → удалить папку (portable).
PawnIO при этом остаётся, им могут пользоваться другие программы; удаляется отдельно.

## Риски и ограничения

Всё как есть, без приукрашивания:

- **Драйвер ядра.** Температуру и частоты CPU без драйвера не прочитать. Используется PawnIO — подписанный открытый драйвер, который сейчас применяют LibreHardwareMonitor, FanControl, OpenRGB и другие. Но любой драйвер ядра — это код с максимальными правами: если в нём найдут уязвимость, она будет и у вас.
- **Античиты.** Некоторые античиты в онлайн-играх могут косо смотреть на драйверы мониторинга. На тестовом ПК проблем не было, но это ничего не гарантирует.
- **Права администратора.** Программа всегда запускается от администратора (так же, как и оригинал): иначе драйвер недоступен.
- **Проверено на одной конфигурации:** AMD Ryzen 7 9800X3D, NVIDIA RTX 5070 Ti, Windows 11, корпус D34. Intel, видеокарты AMD/Intel и другие экраны JONSBO не проверялись. Если что-то показывает не то — пришлите вывод `JonsboD34Mon.exe --probe probe.txt` в Issues.
- **Протокол экрана восстановлен по оригинальной программе**, документации от JONSBO нет. Другая прошивка экрана может вести себя иначе. Поля, которые экран D34 не показывает (мощность, напряжение, вентиляторы), отправляются нулями. Команда «выключить экран» (`0x0F`) взята из оригинала и проверена только визуально.
- **Цифры ресурсов** выше сняты на одном ПК за одну минуту. На другом железе и другой версии Windows они будут другими. Опрос CPU через библиотеку занимает 2–60 мс реального времени в секунду, в основном это ожидание ответа от процессора, а не работа CPU.
- **Размер на диске** ~108 МБ: в exe встроен .NET, чтобы ничего не нужно было ставить. В памяти он почти не занимает места.
- **Значения могут отличаться от HWiNFO** на 1–2 °C: другой источник и момент чтения. Частота по умолчанию — максимальная по ядрам; оригинал показывал частоту только нулевого ядра.
- **Новые процессоры** заработают, когда их поддержку добавят в LibreHardwareMonitor и программа будет пересобрана.
- **Нет** автообновлений, телеметрии и доступа в сеть.

## Как это устроено

- **CPU:** [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), только группа CPU, через PawnIO.
- **GPU:** температура от самой Windows (`D3DKMTQueryAdapterInfo`, тот же источник, что у Диспетчера задач).
- **Экран:** обычное HID-устройство `VID_5131&PID_2007`, 64-байтные пакеты через Windows HID API. Протокол описан в [`CaseDisplay.cs`](JonsboD34Mon/CaseDisplay.cs).
- Два фоновых потока: датчики раз в секунду и отправка на экран каждые 250 мс. Окно рисуется вручную и существует, только пока открыто.

**Сборка:** нужен .NET 10 SDK, затем `powershell -ExecutionPolicy Bypass -File build.ps1`. Результат в `dist\`.

## Лицензия

Код — [MIT](LICENSE). Сторонние компоненты и их лицензии — в [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

## English

An **experimental**, lightweight, standalone replacement for JONSBO's *PC Monitor All* for the **JONSBO D34**
case screen: time, CPU temperature (with the bar), GPU temperature, CPU load and CPU clock.
One person's experiment, tested on one PC. Not affiliated with JONSBO.

### Before → after

Same PC, minimized to the tray, 60-second measurement (Ryzen 7 9800X3D, RTX 5070 Ti, Windows 11):

| | PC Monitor All 1.0 | Jonsbo D34 Mon 1.1 |
|---|---|---|
| CPU | **15–19 %** of one core | **0.55 %** of one core |
| Private memory | ~75 MB | **~16 MB** |
| Handles | **leak**: 44,000 after 2.5 h, +5/s | stable, ~410 |
| Sensor polling | all 22 HWiNFO devices every second | only the 4 values shown |
| Screen updates | 5/s, normal priority | 4/s from a high-priority thread; no blanking even at 100 % CPU |
| Dependencies | closed HWiNFO SDK licensed to JONSBO | open: LibreHardwareMonitor + PawnIO driver |
| UI | 11 dropdowns | replica of the case screen, ⚙ per value, light/dark, RU/EN |

### Install

From [Releases](../../releases): `Jonsbo D34 Mon 1.1.0 (en).msi` (or `(ru)`), or the `portable.zip`.
.NET is included; setup installs the [PawnIO](https://pawnio.eu) driver if missing (in the portable zip: run `PawnIO_setup.exe` once).
Turn on **Autostart** in the window and **turn off PC Monitor All's autostart** — two programs make the screen flicker.
The installer is unsigned: SmartScreen → *More info → Run anyway*; verify with `SHA256SUMS.txt`.

### Risks and limitations

- **Kernel driver:** CPU temperatures need one. PawnIO is signed and open-source and used by LibreHardwareMonitor, FanControl, OpenRGB etc., but any kernel driver carries risk.
- **Anti-cheat** software may dislike monitoring drivers. No problems on the test PC, which guarantees nothing.
- **Always runs as administrator** (the original does too).
- **Tested on one configuration** (Ryzen 7 9800X3D, RTX 5070 Ti, Windows 11). Intel CPUs, AMD/Intel GPUs and other JONSBO screens are untested — please send `JonsboD34Mon.exe --probe probe.txt` output via Issues.
- **The screen protocol was reconstructed** from the original app; no documentation from JONSBO. Fields the D34 doesn't show are sent as zero; the "screen off" command (`0x0F`) is taken from the original.
- **Resource numbers** are from one PC, one minute. Yours will differ.
- **~108 MB on disk** (.NET included), little of it in RAM.
- Values may differ from HWiNFO by 1–2 °C. New CPUs need a LibreHardwareMonitor update and a rebuild.
- No auto-update, no telemetry, no network access.

### Build

.NET 10 SDK, then `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\`.

### License

[MIT](LICENSE); third-party components in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
