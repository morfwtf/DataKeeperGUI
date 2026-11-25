# DataKeeperGUI

Windows-приложение для личных зашифрованных заметок. Графическая версия DataKeeper; заметки в формате `.dk` совместимы с консольной программой.

## Возможности

- Создание и редактирование заметок с шифрованием AES-256-GCM.
- Открытие, удаление и смена пароля с проверкой пароля заметки.
- Выбор или создание папки хранения.
- Светлая и тёмная темы.
- Автономный одиночный EXE для Windows x64 в разделе Releases.

Пароль не сохраняется в файле. Для каждой записи используется новый случайный salt и ключ PBKDF2-SHA256 (210 000 итераций). Если пароль утрачен, восстановить содержимое заметки нельзя.

## Сборка

Требуется .NET SDK 10 или новее:

```powershell
dotnet build .\DataKeeperGUI.slnx -c Release
```

Для автономного одиночного исполняемого файла Windows x64:

```powershell
dotnet publish .\DataKeeperGUI\DataKeeperGUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

При первом запуске выберите папку для заметок. Путь к папке и выбранная тема сохраняются локально для текущего пользователя. Храните резервные копии зашифрованных файлов.
