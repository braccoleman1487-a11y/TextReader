using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TextReaderProject
{
    internal class Program
    {
        static async Task Main()
        {
            Console.WriteLine("Welcome to the File Analyzer!!!");
            Console.WriteLine("---------------------------------");
            Console.WriteLine("To analyze a file, type the name of the file in your directory.");
            Console.Write("File Name: ");

            string? fileName = Console.ReadLine();
            if (string.IsNullOrEmpty(fileName))
            {
                Console.WriteLine("File name can't be null or empty.");
                return;
            }

            string currentDirectory = Directory.GetCurrentDirectory();
            string fullPath = Path.Combine(currentDirectory, $"{fileName}.txt");

            FileInputProvider provider = new FileInputProvider(fullPath);
            using CancellationTokenSource cts = new CancellationTokenSource();

            try
            {
                // Start the background listener task
                _ = Task.Run(() =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                        {
                            cts.Cancel();
                            break;
                        }
                        Thread.Sleep(50); // Small sleep to save CPU cycles
                    }
                });

                Console.WriteLine("\n[Press ESC at any time to cancel process]");

                // 1. Load the file (Cancellable)
                await provider.LoadTextAsync(cts.Token);

                // 2. Show the text line-by-line (Cancellable)
                Console.WriteLine("\n--- Document Content ---");
                await VisualEffects.DisplayLinesAsync(provider.RawText, 300, cts.Token);

                // 3. Show the analysis with typewriter effect (Cancellable)
                Console.WriteLine("\nGenerating Analysis...");
                await Task.Delay(1000, cts.Token);
                string report = provider.GetAnalysisReport();
                await VisualEffects.TypewriterEffectAsync(report, 30, cts.Token);

                // 4. Handle Export (Point of no return - Token is NOT passed here)
                Console.Write("\nEnter the name of the file you wish to save to: ");
                string? exportName = Console.ReadLine();

                if (!string.IsNullOrEmpty(exportName))
                {
                    await provider.ExportAsync($"{exportName}.txt");
                    Console.WriteLine("File exported successfully.");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n\n[CANCELLED]: Operation aborted by user.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR]: {ex.Message}");
            }
        }
    }

    public interface IExportable
    {
        Task ExportAsync(string fileName);
    }

    public abstract class TextProvider
    {
        public string RawText { get; protected set; } = string.Empty;
        public abstract Task LoadTextAsync(CancellationToken token);

        public string GetAnalysisReport()
        {
            if (string.IsNullOrWhiteSpace(RawText))
                return "No text available to analyze.";

            string[] words = RawText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            string[] sentences = RawText.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, int> wordCounts = new Dictionary<string, int>();
            foreach (string word in words)
            {
                string cleanWord = word.ToLower().Trim();
                if (wordCounts.ContainsKey(cleanWord)) wordCounts[cleanWord]++;
                else wordCounts.Add(cleanWord, 1);
            }

            string mostUsedWord = wordCounts.Count > 0
                ? wordCounts.OrderByDescending(x => x.Value).First().Key
                : "N/A";

            return $"--- Analysis Results ---\n" +
                   $"Word Count: {words.Length}\n" +
                   $"Sentence Count: {sentences.Length}\n" +
                   $"Most Used Word: '{mostUsedWord}'\n" +
                   $"------------------------";
        }
    }

    public class FileInputProvider : TextProvider, IExportable
    {
        private string _path;
        public FileInputProvider(string path) => _path = path;

        public override async Task LoadTextAsync(CancellationToken token)
        {
            if (!File.Exists(_path))
                throw new FileNotFoundException($"Could not find file at: {_path}");

            Console.WriteLine("\nAccessing Disk...");
            for (int i = 0; i <= 100; i += 20)
            {
                token.ThrowIfCancellationRequested();
                Console.Write($"\rProgress: {i}% ");
                await Task.Delay(300, token);
            }

            RawText = await File.ReadAllTextAsync(_path, token);
            Console.WriteLine("\nFile Loaded successfully.");
        }

        public async Task ExportAsync(string fileName)
        {
            string report = GetAnalysisReport();
            await File.WriteAllTextAsync(fileName, report);
        }
    }

    public static class VisualEffects
    {
        public static async Task DisplayLinesAsync(string text, int delayMs, CancellationToken token)
        {
            if (string.IsNullOrEmpty(text)) return;
            string[] lines = text.Split('\n');
            foreach (var line in lines)
            {
                token.ThrowIfCancellationRequested();
                Console.WriteLine(line);
                await Task.Delay(delayMs, token);
            }
        }

        public static async Task TypewriterEffectAsync(string text, int speedMs, CancellationToken token)
        {
            foreach (char c in text)
            {
                token.ThrowIfCancellationRequested();
                Console.Write(c);
                await Task.Delay(speedMs, token);
            }
            Console.WriteLine();
        }
    }
}