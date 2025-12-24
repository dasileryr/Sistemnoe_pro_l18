using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WordScannerApp.Models;

namespace WordScannerApp.Services
{
    public class ReportService
    {
        public async System.Threading.Tasks.Task GenerateReportAsync(
            List<FileScanResult> results,
            string outputPath)
        {
            var report = new StringBuilder();
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine("ОТЧЕТ О ПОИСКЕ ЗАПРЕЩЕННЫХ СЛОВ");
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine($"Дата создания: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Всего найдено файлов: {results.Count}");
            report.AppendLine();

            // Общая статистика
            var totalSize = results.Sum(r => r.FileSize);
            var totalReplacements = results.Sum(r => r.TotalReplacements);
            report.AppendLine("ОБЩАЯ СТАТИСТИКА:");
            report.AppendLine($"  Общий размер файлов: {FormatFileSize(totalSize)}");
            report.AppendLine($"  Всего замен: {totalReplacements}");
            report.AppendLine();

            // Топ-10 самых популярных запрещенных слов
            var wordStats = new Dictionary<string, int>();
            foreach (var result in results)
            {
                foreach (var kvp in result.WordCounts)
                {
                    if (!wordStats.ContainsKey(kvp.Key))
                        wordStats[kvp.Key] = 0;
                    wordStats[kvp.Key] += kvp.Value;
                }
            }

            var topWords = wordStats.OrderByDescending(kvp => kvp.Value).Take(10).ToList();
            report.AppendLine("ТОП-10 САМЫХ ПОПУЛЯРНЫХ ЗАПРЕЩЕННЫХ СЛОВ:");
            for (int i = 0; i < topWords.Count; i++)
            {
                report.AppendLine($"  {i + 1}. {topWords[i].Key}: {topWords[i].Value} раз(а)");
            }
            report.AppendLine();

            // Детальная информация по файлам
            report.AppendLine("ДЕТАЛЬНАЯ ИНФОРМАЦИЯ ПО ФАЙЛАМ:");
            report.AppendLine("-".PadRight(80, '-'));
            
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                report.AppendLine($"Файл #{i + 1}:");
                report.AppendLine($"  Путь: {result.FilePath}");
                report.AppendLine($"  Размер: {FormatFileSize(result.FileSize)}");
                report.AppendLine($"  Время сканирования: {result.ScanTime:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"  Всего замен: {result.TotalReplacements}");
                report.AppendLine("  Найденные слова:");
                foreach (var kvp in result.WordCounts.OrderByDescending(x => x.Value))
                {
                    report.AppendLine($"    - {kvp.Key}: {kvp.Value} раз(а)");
                }
                report.AppendLine();
            }

            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine("КОНЕЦ ОТЧЕТА");

            await File.WriteAllTextAsync(outputPath, report.ToString(), Encoding.UTF8);
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

