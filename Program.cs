using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

class Program
{
    static readonly Regex SectionRegex = new(@"^\+\s*##\s+(.*)");
    static readonly Regex ItemRegex = new(@"^[+-]\s*-\s*(.*)");

    static void Main(string[] args)
    {
        // Default: last commit
        string commitRange = args.Length > 0 ? args[0] : "HEAD~1..HEAD";

        var diffLines = RunGitDiff(commitRange);
        var notes = ParseDiff(diffLines);

        PrintReleaseNotes(notes);
    }

    static List<string> RunGitDiff(string range)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff {range}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var lines = new List<string>();
        while (!process.StandardOutput.EndOfStream)
        {
            lines.Add(process.StandardOutput.ReadLine()!);
        }

        process.WaitForExit();
        return lines;
    }

    static Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>> ParseDiff(
        List<string> lines)
    {
        var notes = new Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>>();

        string? currentFile = null;
        string? currentSection = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("diff --git"))
            {
                currentFile = null;
                currentSection = null;
                continue;
            }

            if (line.StartsWith("+++ b/") && line.EndsWith(".md"))
            {
                currentFile = line.Replace("+++ b/", "");
                notes.TryAdd(currentFile, new());
                continue;
            }

            if (currentFile != null && SectionRegex.IsMatch(line))
            {
                currentSection = SectionRegex.Match(line).Groups[1].Value;
                notes[currentFile].TryAdd(currentSection, new());
                continue;
            }

            if (currentFile != null && currentSection != null && ItemRegex.IsMatch(line))
            {
                var item = ItemRegex.Match(line).Groups[1].Value;
                var changeType = line.StartsWith("+") ? "Added" : "Removed";

                if (!notes[currentFile][currentSection].ContainsKey(changeType))
                {
                    notes[currentFile][currentSection][changeType] = new();
                }

                notes[currentFile][currentSection][changeType].Add(item);
            }
        }

        return notes;
    }

    static void PrintReleaseNotes(
        Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>> notes)
    {
        foreach (var file in notes)
        {
            var title = System.IO.Path.GetFileNameWithoutExtension(file.Key)
                .Replace("-", " ");
            title = char.ToUpper(title[0]) + title[1..];

            Console.WriteLine($"\n## {title}\n");

            foreach (var section in file.Value)
            {
                Console.WriteLine($"### {section.Key}");

                foreach (var change in section.Value)
                {
                    Console.WriteLine($"**{change.Key}**");
                    foreach (var item in change.Value)
                    {
                        Console.WriteLine($"- {item}");
                    }
                }
                Console.WriteLine();
            }
        }
    }
}
