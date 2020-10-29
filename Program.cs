using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace ServerTemplateCreator
{
    internal static class Program
    {
        private static string _directory = string.Empty;

        private static readonly List<string> AllowedTypes = new List<string> {"yml", "txt"};

        public static void Main()
        {
            Console.WriteLine(@"Вас приветствует помощник по созданию серверов ServerMaker,
разработанный создателем группы vk.com/servbuy
Пиши info чтобы ознакомиться со списком команд" + "\n");
        _directory = @"C:\Users\dimon\Desktop\MinePlay";
        while (!_exitFlag)
        {
            var line = Console.ReadLine();
            int position;
            position = line.IndexOf(' ');
            if (position < 0)
                position = line.Length;
            var commandName = line.Substring(0, position);
            var commandArgs = line.Substring(position).Trim();
            if (Actions.ContainsKey(commandName))
            {
                Actions[commandName](_directory, commandArgs);
            }
            else
            {
                Console.WriteLine("Команда не найдена");
            }
        }
        }

    private static IReadOnlyList<Command> Commands = new List<Command>
        {
            new Command("info", "Получить список команд", (directory, argumentsLine) =>
            {
                foreach (var cmd in Commands)
                {
                    Console.WriteLine($"Command: {cmd.Name} - {cmd.Description}\n");
                }
                Console.WriteLine();
            }),
            new Command("help", "Help documentation", (directory, commandArgs) =>
            {
                Console.WriteLine(Descriptions.ContainsKey(commandArgs)
                    ? Descriptions[commandArgs]
                    : $"Описание для команды {commandArgs} не найдено");
            }),
            new Command("exit", "Закрывает программу",
                (directory, argumentsLine) => 
                { 
                    _exitFlag = true;
                }),
            new Command("open", "Открывает директорию с сервером игры", 
            (directory, commandArgs) => 
            { 
                if (commandArgs == "" || !Directory.Exists(commandArgs))
                    Console.WriteLine("[ERROR] Данной директории не существует или введена пустая строка\n");
                else
                {
                    _directory = commandArgs;
                    Console.WriteLine("[LOG] Директория успешно открыта\n");
                    _isDirrectoryOpened = true;
                }
            }),
            new Command("replace", "Заменяет старое значение на новое\nUsage: replace <[oldTitle] [newTitle]>", 
                (directory, commandArgs) =>
                {
                    var splitValues = commandArgs.Split();
                    if (!_isDirrectoryOpened)
                        Console.WriteLine("[ERROR] Сначала откройте исходную папку open [Path]\n");
                    else
                    {
                        switch(splitValues[0].ToLower())
                        {
                            case "?":
                                Console.WriteLine(Descriptions["replace"]);
                                break;
                            case "fromconfig":
                            {
                                var replacements = ReadConfig();
                                var raw = replacements["serverName"];
                                replacements["coloredName"] = String.Format(raw, replacements["colors"].Split());
                                replacements["serverName"] = String.Format(raw, new String[raw.Split('}').Length]);
                                var dict = ReplacementParams.Keys
                                    .ToDictionary(key => ReplacementParams[key], key => replacements[key]);
                                ReplaceTitle(directory, dict);
                                break;
                            }
                            case "todefault":
                            {
                                if (splitValues.Length < 2)
                                {
                                    Console.WriteLine("[ERROR] Неверное количество аргументов");
                                    break;
                                }
                                var replacements = new Dictionary<string, string>();
                                for (var i = 1; i < splitValues.Length; i++)
                                {
                                    var raw = splitValues[i]
                                        .Split(new[] {':'}, StringSplitOptions.RemoveEmptyEntries);
                                    if (raw.Length % 2 != 0)
                                    {
                                        Console.WriteLine("[ERROR] Неверный аргумент для ключа " + raw[0]);
                                    }
                                    var (key, value) = (raw[0], raw[1]);
                                    if (!ReplacementParams.ContainsKey(key))
                                    {
                                        Console.WriteLine("[ERROR] Параметра " + key + " не существует!" );
                                        continue;
                                    }
                                    replacements[key] = value;
                                }
                                var dict = replacements.Keys
                                    .ToDictionary(k => replacements[k], k => ReplacementParams[k]);
                                ReplaceTitle(directory, dict, false);
                                break;
                            }
                            default:
                                if (splitValues.Length < 2 || splitValues.Length % 2 == 1)
                                    Console.WriteLine("[ERROR] replace <[старое значение] [новое значение]>\n");
                                else
                                {
                                    //ReplaceTitle(directory, splitValues);
                                }
                                break;

                        }
                    }
                }),
        };
        
        private static void ReplaceTitle(string sourcePath, Dictionary<string, string> dictionary, bool flag = true)
        {
            Console.Write("[LOG] Изменяем заголовки... ");
            using (var progress = new ProgressBar())
            {
                var filePaths = Directory.GetFiles(sourcePath + @"\plugins", "*.*",
                    SearchOption.AllDirectories);
                var current = 0;
                foreach (var filePath in filePaths)
                {
                    current++;
                    progress.Report((double) current / filePaths.Length);
                    if (!AllowedTypes.Contains(filePath.Split('.').Last())) continue;
                    var fileText = File.ReadAllText(filePath);
                    if (flag)
                        foreach (Match match in Regex.Matches(fileText, @"\%[0-9a-zA-Z_\-]*\%"))
                        {
                            if (!dictionary.ContainsKey(match.Value)) continue;
                            fileText = fileText.Replace(match.Value, dictionary[match.Value]);
                            WriteLog(
                                $"[CHANGED] {filePath.Substring(sourcePath.Length + 9)}\t{match.Value} --> {dictionary[match.Value]}\n");
                        }
                    else
                    {
                        foreach (var key in dictionary.Keys.Where(key => fileText.Contains(key)))
                        {
                            fileText = fileText.Replace(key, dictionary[key]);
                            WriteLog($"[CHANGED] {filePath.Substring(sourcePath.Length + 9)}\t{key} --> {dictionary[key]}\n");
                        }
                    }
                    File.WriteAllText(filePath, fileText);
                }
            }
            Console.WriteLine("Готово.\n");
        }

        private static void WriteLog(string logText) => 
            File.AppendAllText(_directory + @"\changelog.txt", logText);
        
        private static Dictionary<string, string> ReadConfig()
        {
            var lines = File.ReadAllLines(_directory + @"\SBConfig.yml");
            var result = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                var deserializer = new Deserializer();
                result = result
                    .Concat(deserializer.Deserialize<Dictionary<string, string>>(line))
                    .ToDictionary(p => p.Key, p => p.Value);
            }
            return result;
        }

        /*private static readonly Func<string, string[]> SplitByCapitalLetter =
            value => Regex.Replace(value, "((?<=[a-zа-яё])[A-ZА-ЯЁ]|[A-ZА-ЯЁ](?=[a-zа-яё]))", " $1").TrimStart().Split();*/

        private static readonly Dictionary<string, string> ReplacementParams = new Dictionary<string, string>()
        {
            {"site", "%SB_SITE%"},
            {"vk", "%SB_VK%"},
            {"serverName", "%SB_SERVERNAME%"},
            {"coloredName", "%SB_COLOREDNAME%"}
        };
        
        private static readonly ReadOnlyDictionary<string, Action<string,string>> Actions =
            new ReadOnlyDictionary<string, Action<string,string>>(Commands.ToDictionary(f => f.Name, 
                f => f.CommandMethod));
        
        private static readonly ReadOnlyDictionary<string, string> Descriptions =
            new ReadOnlyDictionary<string, string>(Commands.ToDictionary(f => f.Name, f => f.Description));

        private static bool _exitFlag;

        private static bool _isDirrectoryOpened;
    }
}