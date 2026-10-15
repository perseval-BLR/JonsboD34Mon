Jonsbo D34 Mon — portable / портативная версия
==============================================

RU
--
1. Распакуйте папку куда угодно (например, D:\Tools\Jonsbo D34 Mon) и запустите JonsboD34Mon.exe.
   Windows спросит права администратора: они нужны драйверу датчиков.
2. Если вместо температуры CPU видно "--", а в окне написано «Нет драйвера PawnIO», один раз
   запустите PawnIO_setup.exe из этой папки (официальный подписанный установщик драйвера).
3. Настройки хранятся рядом с exe (settings.ini), пока в папке лежит файл JonsboD34Mon.portable.
   Удалите его — и настройки будут в %APPDATA%\JonsboD34Mon, как у установленной версии.
4. «Автозапуск» в окне создаёт задачу в Планировщике на текущее место exe. Перенесли папку —
   просто запустите программу из нового места, задача обновится сама.

Удаление: «Выгрузить из памяти», выключить «Автозапуск», удалить папку.
PawnIO удаляется отдельно (Параметры → Приложения), если он больше не нужен.

Это экспериментальная программа: см. раздел о рисках на странице проекта.

EN
--
1. Unpack anywhere and run JonsboD34Mon.exe (it asks for admin rights: the sensor driver needs them).
2. If the CPU temperature shows "--" and the window says "No PawnIO driver", run PawnIO_setup.exe
   from this folder once (the official signed driver setup).
3. Settings live next to the exe (settings.ini) while JonsboD34Mon.portable is in the folder.
   Delete that file to keep them in %APPDATA%\JonsboD34Mon instead.
4. "Autostart" creates a Task Scheduler task for the exe's current location. If you move the folder,
   start the program from the new place once and the task follows.

Removal: "Quit and unload", turn off "Autostart", delete the folder.
PawnIO is removed separately (Settings → Apps) if nothing else needs it.

This is an experimental program: see the risks section on the project page.
