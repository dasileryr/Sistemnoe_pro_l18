# Инструкции по сборке проектов

## Требования
- .NET 8.0 SDK или выше
- Windows (для работы Windows API hooks в UserMonitorApp)
- Visual Studio 2022 или выше (опционально, можно использовать только dotnet CLI)

## Сборка через командную строку

### WordScannerApp
```bash
cd WordScannerApp
dotnet build -c Release
```

Исполняемый файл будет в: `WordScannerApp/bin/Release/net8.0-windows/WordScannerApp.exe`

### UserMonitorApp
```bash
cd UserMonitorApp
dotnet build -c Release
```

Исполняемый файл будет в: `UserMonitorApp/bin/Release/net8.0-windows/UserMonitorApp.exe`

## Сборка через Visual Studio
1. Откройте решение (если есть) или отдельные проекты
2. Выберите конфигурацию Release
3. Нажмите Build > Build Solution

## Запуск

### WordScannerApp
- **С GUI**: Просто запустите `WordScannerApp.exe`
- **Без GUI**: `WordScannerApp.exe --no-gui --words "words.txt" --output "C:\Output"`

### UserMonitorApp
- Запустите `UserMonitorApp.exe` (желательно от имени администратора для полной функциональности)

## Примечания
- UserMonitorApp требует прав администратора для перехвата клавиатуры
- Оба приложения используют Mutex для предотвращения запуска нескольких копий
- WordScannerApp может работать без прав администратора, но может не иметь доступа к некоторым системным папкам

