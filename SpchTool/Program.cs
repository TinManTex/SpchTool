using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Reflection;
using System.Linq;//ToList extension for hashset

namespace SpchTool
{
    internal static class Program
    {
        private const string DefaultHashDumpFileName = "spch_hash_dump_dictionary.txt";
        private const string fileType = "spch";

        class RunSettings
        {
            public bool outputHashes = false;
            public string gameId = "TPP";
            public string outputPath = @"D:\Github\mgsv-lookup-strings";
        }//RunSettings
        private static void Main(string[] args)
        {
            var hashManager = new HashManager();

            // Multi-Dictionary Reading!!
            List<string> dictionaryNames = new List<string>
            {
                "spch_dictionary.txt",
                "spch_label_dictionary.txt",
                "spch_voicetype_dictionary.txt",
                "spch_anim_dictionary.txt",
                "spch_user_dictionary.txt",
            };
            List<string> fnvDictionaryNames = new List<string>
            {
                "spch_fnv_voiceevent_dictionary.txt",
                "spch_fnv_voiceid_dictionary.txt",
                "spch_user_dictionary.txt",
            };

            List<string> strCodeDictionaries = new List<string>();
            List<string> fnvDictionaries = new List<string>();

            foreach (var dictionaryPath in dictionaryNames)
                if (File.Exists(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/" + dictionaryPath))
                    strCodeDictionaries.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/" + dictionaryPath);

            foreach (var dictionaryPath in fnvDictionaryNames)
                if (File.Exists(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/" + dictionaryPath))
                    fnvDictionaries.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/" + dictionaryPath);

            hashManager.StrCode32LookupTable = MakeStrCode32HashLookupTableFromFiles(strCodeDictionaries);
            hashManager.Fnv1LookupTable = MakeFnv1HashLookupTableFromFiles(fnvDictionaries);

            List<string> UserStrings = new List<string>();

            //deal with args
            RunSettings runSettings = new RunSettings();

            List<string> files = new List<string>();
            int idx = 0;
            if (args[idx].ToLower() == "-outputhashes" || args[idx].ToLower() == "-o")
            {
                runSettings.outputHashes = true;
                runSettings.outputPath = args[idx += 1];
                runSettings.gameId = args[idx += 1].ToUpper();
                Console.WriteLine("Adding to file list");
                for (int i = idx += 1; i < args.Length; i++)
                {
                    AddToFiles(files, args[i], fileType);
                }
            }
            else
            {
                Console.WriteLine("Adding to file list");
                foreach (var arg in args)
                {
                    AddToFiles(files, arg, "*");
                }//foreach args
            }

            foreach (var spchPath in files)
            {
                Console.WriteLine(spchPath);
                if (File.Exists(spchPath))
                {
                    // Read input file
                    string fileExtension = Path.GetExtension(spchPath);
                    if (fileExtension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        SpchFile spch = ReadFromXml(spchPath);
                        WriteToBinary(spch, Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(spchPath)) + ".spch");
                        CollectUserStrings(spch, hashManager, UserStrings);
                    }
                    else if (fileExtension.Equals(".spch", StringComparison.OrdinalIgnoreCase))
                    {
                        SpchFile spch = ReadFromBinary(spchPath, hashManager);
                        if (!runSettings.outputHashes)
                        {
                            WriteToXml(spch, Path.GetFileNameWithoutExtension(spchPath) + ".spch.xml");
                        }
                        else
                        {
                            OutputHashes(runSettings.gameId, runSettings.outputPath, spchPath, spch);
                        }//if outputhashes
                    }
                    else
                    {
                        Console.WriteLine($"Unrecognized input type: {fileExtension}");
                    }
                }
            }

            // Write hash matches output
            WriteHashMatchesToFile(DefaultHashDumpFileName, hashManager);
            WriteUserStringsToFile(UserStrings);
        }//Main

        private static void OutputHashes(string gameId, string outputPath, string spchPath, SpchFile spch)
        {
            var hashSets = new Dictionary<string, HashSet<string>>();
            hashSets.Add("LabelName", new HashSet<string>());
            hashSets.Add("VoiceEvent", new HashSet<string>());
            hashSets.Add("VoiceType", new HashSet<string>());
            hashSets.Add("VoiceId", new HashSet<string>());
            hashSets.Add("AnimationAct", new HashSet<string>());
            //SYNC mgsv-lookup-strings/spch/spch_hash_types.json
            var hashTypeNames = new Dictionary<string, string>();
            hashTypeNames.Add("LabelName", "StrCode32");
            hashTypeNames.Add("VoiceEvent", "FNV1Hash32");
            hashTypeNames.Add("VoiceType", "StrCode32");
            hashTypeNames.Add("VoiceId", "FNV1Hash32");
            hashTypeNames.Add("AnimationAct", "StrCode32");

            foreach (var label in spch.Labels)
            {
                hashSets["LabelName"].Add(label.LabelName.HashValue.ToString());
                hashSets["VoiceEvent"].Add(label.VoiceEvent.HashValue.ToString());
                foreach (var voiceClip in label.VoiceClips)
                {
                    hashSets["VoiceType"].Add(voiceClip.VoiceType.HashValue.ToString());
                    hashSets["VoiceId"].Add(voiceClip.VoiceId.HashValue.ToString());
                    hashSets["AnimationAct"].Add(voiceClip.AnimationAct.HashValue.ToString());
                }
            }//foreach labels

            foreach (KeyValuePair<string, HashSet<string>> kvp in hashSets)
            {
                string hashName = kvp.Key;
                WriteHashes(kvp.Value, spchPath, hashName, hashTypeNames[hashName], gameId, outputPath);
            }
        }//OutputHashes

        private static void AddToFiles(List<string> files, string path, string fileType)
        {
            if (File.Exists(path))
            {
                files.Add(path);
            }
            else
            {
                if (Directory.Exists(path))
                {
                    var dirFiles = Directory.GetFiles(path, $"*.{fileType}", SearchOption.AllDirectories);
                    foreach (var file in dirFiles)
                    {
                        files.Add(file);
                    }
                }
            }
        }//AddToFiles

        private static string GetAssetsPath(string inputPath)
        {
            int index = inputPath.LastIndexOf("Assets");
            if (index != -1)
            {
                return inputPath.Substring(index);
            }
            return Path.GetFileName(inputPath);
        }//GetAssetsPath
        //tex outputs to mgsv-lookup-strings repo layout
        private static void WriteHashes(HashSet<string> hashSet, string inputFilePath, string hashName, string hashTypeName, string gameId, string outputPath)
        {
            if (hashSet.Count > 0)
            {
                string assetsPath = GetAssetsPath(inputFilePath);
                //OFF string destPath = {inputFilePath}_{hashName}_{hashTypeName}.txt" //Alt: just output to input file path_whatev
                string destPath = Path.Combine(outputPath, $"{fileType}\\Hashes\\{gameId}\\{hashName}\\{assetsPath}_{hashName}_{hashTypeName}.txt");

                List<string> hashes = hashSet.ToList<string>();
                hashes.Sort();

                string destDir = Path.GetDirectoryName(destPath);
                DirectoryInfo di = Directory.CreateDirectory(destDir);
                File.WriteAllLines(destPath, hashes.ToArray());
            }
        }//WriteHashes

        public static void WriteToBinary(SpchFile spch, string path)
        {
            using (BinaryWriter writer = new BinaryWriter(new FileStream(path, FileMode.Create)))
            {
                spch.Write(writer);
            }
        }

        public static SpchFile ReadFromBinary(string path, HashManager hashManager)
        {
            SpchFile spch = new SpchFile();
            using (BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open)))
            {
                spch.Read(reader, hashManager);
            }
            return spch;
        }

        public static void WriteToXml(SpchFile spch, string path)
        {
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings()
            {
                Encoding = Encoding.UTF8,
                Indent = true
            };
            using (var writer = XmlWriter.Create(path, xmlWriterSettings))
            {
                spch.WriteXml(writer);
            }
        }

        public static SpchFile ReadFromXml(string path)
        {
            XmlReaderSettings xmlReaderSettings = new XmlReaderSettings
            {
                IgnoreWhitespace = true
            };

            SpchFile spch = new SpchFile();
            using (var reader = XmlReader.Create(path, xmlReaderSettings))
            {
                spch.ReadXml(reader);
            }
            return spch;
        }

        /// <summary>
        /// Opens a file containing one string per line from the input table of files, hashes each string, and adds each pair to a lookup table.
        /// </summary>
        private static Dictionary<uint, string> MakeStrCode32HashLookupTableFromFiles(List<string> paths)
        {
            ConcurrentDictionary<uint, string> table = new ConcurrentDictionary<uint, string>();

            // Read file
            List<string> stringLiterals = new List<string>();
            foreach (var dictionary in paths)
            {
                using (StreamReader file = new StreamReader(dictionary))
                {
                    // TODO multi-thread
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        stringLiterals.Add(line);
                    }
                }
            }

            // Hash entries
            Parallel.ForEach(stringLiterals, (string entry) =>
            {
                uint hash = HashManager.StrCode32(entry);
                table.TryAdd(hash, entry);
            });

            return new Dictionary<uint, string>(table);
        }
        private static Dictionary<uint, string> MakeFnv1HashLookupTableFromFiles(List<string> paths)
        {
            ConcurrentDictionary<uint, string> table = new ConcurrentDictionary<uint, string>();

            // Read file
            List<string> stringLiterals = new List<string>();
            foreach (var dictionary in paths)
            {
                using (StreamReader file = new StreamReader(dictionary))
                {
                    // TODO multi-thread
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        stringLiterals.Add(line);
                    }
                }
            }

            // Hash entries
            Parallel.ForEach(stringLiterals, (string entry) =>
            {
                uint hash = HashManager.FNV1Hash32Str(entry);
                table.TryAdd(hash, entry);
            });

            return new Dictionary<uint, string>(table);
        }

        /// <summary>
        /// Outputs all hash matched strings to a file.
        /// </summary>
        private static void WriteHashMatchesToFile(string path, HashManager hashManager)
        {
            using (StreamWriter file = new StreamWriter(path))
            {
                foreach (var entry in hashManager.UsedHashes)
                {
                    file.WriteLine(entry.Value);
                }
            }
        }
        public static void CollectUserStrings(SpchFile spch, HashManager hashManager, List<string> UserStrings)
        {
            foreach (var label in spch.Labels) // Analyze hashes
            {
                if (IsUserString(label.LabelName.StringLiteral, UserStrings, hashManager.StrCode32LookupTable))
                    UserStrings.Add(label.LabelName.StringLiteral);
                if (IsUserString(label.VoiceEvent.StringLiteral, UserStrings, hashManager.Fnv1LookupTable))
                    UserStrings.Add(label.VoiceEvent.StringLiteral);
                foreach (var voiceClip in label.VoiceClips)
                {
                    if (IsUserString(voiceClip.VoiceType.StringLiteral, UserStrings, hashManager.StrCode32LookupTable))
                        UserStrings.Add(voiceClip.VoiceType.StringLiteral);
                    if (IsUserString(voiceClip.VoiceId.StringLiteral, UserStrings, hashManager.Fnv1LookupTable))
                        UserStrings.Add(voiceClip.VoiceId.StringLiteral);
                    if (IsUserString(voiceClip.AnimationAct.StringLiteral, UserStrings, hashManager.StrCode32LookupTable))
                        UserStrings.Add(voiceClip.AnimationAct.StringLiteral);
                }
            }
        }
        public static bool IsUserString(string userString, List<string> list, Dictionary<uint,string> dictionaryTable)
        {
            if (!dictionaryTable.ContainsValue(userString) && !list.Contains(userString))
                return true;
            else
                return false;
        }
        public static void WriteUserStringsToFile(List<string> UserStrings)
        {
            UserStrings.Sort(); //Sort alphabetically for neatness
            var UserDictionary = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/" + "spch_user_dictionary.txt";
            foreach (var userString in UserStrings)
                using (StreamWriter file = new StreamWriter(UserDictionary, append: true))
                    file.WriteLine(userString); //Write them into the user dictionary
        }
    }
}
