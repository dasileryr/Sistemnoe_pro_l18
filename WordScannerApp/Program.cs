using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace WordScannerApp
{
    public partial class App
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Проверка аргументов командной строки
            if (args.Length > 0 && (args[0] == "--no-gui" || args[0] == "-nogui"))
            {
                // Режим без GUI
                RunConsoleMode(args);
            }
            else
            {
                // Обычный режим с GUI
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
        }

        private static void RunConsoleMode(string[] args)
        {
            Console.WriteLine("Режим командной строки");
            Console.WriteLine("Использование: WordScannerApp.exe --no-gui --words <файл> --output <директория> [--extensions <расширения>]");
            Console.WriteLine();

            string? wordsFile = null;
            string? outputDir = null;
            var extensions = new[] { ".txt" };

            // Парсинг аргументов
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--words" && i + 1 < args.Length)
                {
                    wordsFile = args[i + 1];
                    i++;
                }
                else if (args[i] == "--output" && i + 1 < args.Length)
                {
                    outputDir = args[i + 1];
                    i++;
                }
                else if (args[i] == "--extensions" && i + 1 < args.Length)
                {
                    extensions = args[i + 1].Split(',').Select(e => e.Trim().StartsWith(".") ? e.Trim() : "." + e.Trim()).ToArray();
                    i++;
                }
            }

            if (string.IsNullOrEmpty(wordsFile) || string.IsNullOrEmpty(outputDir))
            {
                Console.WriteLine("Ошибка: необходимо указать --words и --output");
                return;
            }

            if (!File.Exists(wordsFile))
            {
                Console.WriteLine($"Ошибка: файл с запрещенными словами не найден: {wordsFile}");
                return;
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            try
            {
                var words = File.ReadAllLines(wordsFile)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToList();

                Console.WriteLine($"Загружено запрещенных слов: {words.Count}");
                Console.WriteLine($"Выходная директория: {outputDir}");
                Console.WriteLine($"Расширения файлов: {string.Join(", ", extensions)}");
                Console.WriteLine("Начинаем сканирование...");
                Console.WriteLine();

                var scanner = new Services.FileScannerService(maxConcurrency: 4);
                int processedFiles = 0;
                int foundFiles = 0;

                scanner.FilesProcessedUpdated += (s, count) => 
                {
                    processedFiles = count;
                    Console.Write($"\rОбработано файлов: {processedFiles}");
                };

                scanner.FileScanned += (s, result) =>
                {
                    foundFiles++;
                    Console.WriteLine($"\nНайден файл: {result.FilePath} (замен: {result.TotalReplacements})");
                };

                var results = scanner.ScanFilesAsync(words, outputDir, extensions.ToList()).Result;

                // Генерация отчета
                var reportService = new Services.ReportService();
                var reportPath = Path.Combine(outputDir, $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                reportService.GenerateReportAsync(results, reportPath).Wait();

                Console.WriteLine();
                Console.WriteLine($"Сканирование завершено!");
                Console.WriteLine($"Найдено файлов: {results.Count}");
                Console.WriteLine($"Отчет сохранен: {reportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}

