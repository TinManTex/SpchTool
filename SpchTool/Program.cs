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

            List<string> dictionaries = new List<string>();

            foreach (var dictionaryPath in dictionaryNames)
                if (File.Exists(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/" + dictionaryPath))
                    dictionaries.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/" + dictionaryPath);

			hashManager.StrCode32LookupTable = MakeHashLookupTableFromFiles(dictionaries, FoxHash.Type.StrCode32);

            List<string> UserStrings = new List<string>();

            //deal with args
            bool outputHashes = false;
            string gameId = "TPP";
            string outputPath = @"D:\Github\mgsv-lookup-strings";
            List<string> files = new List<string>();
            int idx = 0;
            if (args[idx].ToLower() == "-outputhashes" || args[idx].ToLower() == "-o")
            {
                outputHashes = true;
                outputPath = args[idx += 1];
                gameId = args[idx += 1].ToUpper();
                Console.WriteLine("Adding to file list");
                for (int i = idx += 1; i < args.Length; i++)
                {
                    AddToFiles(files, args[i], fileType);
                }
            } else
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
                    if (!outputHashes)
                    {

                        WriteToXml(spch, Path.GetFileNameWithoutExtension(spchPath) + ".spch.xml");
                    }
                    else
                    {
                        OutputHashes(gameId, outputPath, spchPath, spch);
                    }//if outputhashes
                }
                else
                {
                    Console.WriteLine($"Unrecognized input type: {fileExtension}");
                }
            }//foreach files

            // Write hash matches output
            if (!outputHashes)
            {
                WriteHashMatchesToFile(DefaultHashDumpFileName, hashManager);
                WriteUserStringsToFile(UserStrings);
            }
        }

        private static void OutputHashes(string gameId, string outputPath, string spchPath, SpchFile spch)
        {
                        var hashSets = new Dictionary<string, HashSet<string>>();
                        hashSets.Add("LabelName", new HashSet<string>());
                        hashSets.Add("SbpListId", new HashSet<string>());
                        hashSets.Add("VoiceType", new HashSet<string>());
                        hashSets.Add("SbpVoiceClip", new HashSet<string>());
                        hashSets.Add("AnimationAct", new HashSet<string>());

                        var hashTypeNames = new Dictionary<string, string>();
                        hashTypeNames.Add("LabelName", "StrCode32");
                        hashTypeNames.Add("SbpListId", "Unknown32");
                        hashTypeNames.Add("VoiceType", "StrCode32");
                        hashTypeNames.Add("SbpVoiceClip", "Unknown32");
                        hashTypeNames.Add("AnimationAct", "StrCode32");

                        foreach (var label in spch.Labels)
                        {
                            hashSets["LabelName"].Add(label.LabelName.HashValue.ToString());
                            hashSets["SbpListId"].Add(label.SbpListId.ToString());
                            foreach (var voiceClip in label.VoiceClips)
                            {
                                hashSets["VoiceType"].Add(voiceClip.VoiceType.HashValue.ToString());
                                hashSets["SbpVoiceClip"].Add(voiceClip.SbpVoiceClip.ToString());
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
        private static Dictionary<uint, string> MakeHashLookupTableFromFiles(List<string> paths, FoxHash.Type hashType)
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
                if (hashType == FoxHash.Type.StrCode32)
                {
                    uint hash = HashManager.StrCode32(entry);
                    table.TryAdd(hash, entry);
                }
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
                if (IsUserString(label.LabelName.StringLiteral, UserStrings, hashManager))
                    UserStrings.Add(label.LabelName.StringLiteral);
                foreach (var voiceClip in label.VoiceClips)
                {
                    if (IsUserString(voiceClip.VoiceType.StringLiteral, UserStrings, hashManager))
                        UserStrings.Add(voiceClip.VoiceType.StringLiteral);
                    if (IsUserString(voiceClip.AnimationAct.StringLiteral, UserStrings, hashManager))
                        UserStrings.Add(voiceClip.AnimationAct.StringLiteral);
                }
            }
        }
        public static bool IsUserString(string userString, List<string> list, HashManager hashManager)
        {
            if (!hashManager.StrCode32LookupTable.ContainsValue(userString) && !list.Contains(userString))
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
