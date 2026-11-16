#nullable enable

using System.IO;
using UnityEngine;

namespace OperatorConsole
{
    public static class OCPaths
    {
        /// <summary>The Resources name the Config is loaded from: <c>Resources/OperatorConsoleConfig.asset</c>.</summary>
        public const string ConfigResourceName = "OperatorConsoleConfig";

        /// <summary>Where <b>Draft › Operator Console › Create Config Asset</b> puts the Config.</summary>
        public const string ConfigAssetPath = "Assets/Resources/" + ConfigResourceName + ".asset";

        internal const string LegacyConfigResourceName = "DSMConfig";
        internal const string DefaultDirectoryName = "OperatorConsole";
        internal const string LegacyDirectoryName = "DSM";
        private const string DefaultFileName = "save.json";

        private static bool s_warnedLegacyConfig;

        /// <summary>
        /// Loads the Config from Resources. Falls back to the pre-7.0 name <c>DSMConfig</c>, logging a
        /// warning once, and returns null when neither exists.
        /// </summary>
        public static OCConfig? LoadConfig()
        {
            var config = Resources.Load<OCConfig>(ConfigResourceName);
            if (config != null) return config;

            config = Resources.Load<OCConfig>(LegacyConfigResourceName);
            if (config != null && !s_warnedLegacyConfig)
            {
                s_warnedLegacyConfig = true;
                Debug.LogWarning($"OC: loaded the Config from Resources/{LegacyConfigResourceName}; rename it to {ConfigResourceName}. The old name stops loading in 8.0.");
            }
            return config;
        }

        public static string ResolveSaveFilePath(OCConfig config) =>
            Resolve(config.SaveDirectory, config.FileName, Path.GetDirectoryName(Application.dataPath) ?? string.Empty, Application.persistentDataPath);

        internal static string Resolve(string saveDirectory, string fileName, string appRoot, string persistentDataPath)
        {
            string dir;
            if (string.IsNullOrEmpty(saveDirectory))
                dir = Path.Combine(persistentDataPath, DefaultDirectoryName);
            else if (Path.IsPathRooted(saveDirectory))
                dir = saveDirectory;
            else
                dir = Path.GetFullPath(Path.Combine(appRoot, saveDirectory));

            return Path.Combine(dir, FileNameOrDefault(fileName));
        }

        /// <summary>
        /// Moves a Save File left at the pre-7.0 default location (<c>persistentDataPath/DSM/</c>) to the
        /// current default location. Does nothing when the Config sets a Save Directory, when the current
        /// Save File already exists, or when there is no old file.
        /// </summary>
        public static void MoveLegacySaveFile(OCConfig config) =>
            MoveLegacySaveFile(config.SaveDirectory, config.FileName, Application.persistentDataPath);

        internal static void MoveLegacySaveFile(string saveDirectory, string fileName, string persistentDataPath)
        {
            if (!string.IsNullOrEmpty(saveDirectory)) return;

            var file = FileNameOrDefault(fileName);
            var legacyPath = Path.Combine(persistentDataPath, LegacyDirectoryName, file);
            var path = Path.Combine(persistentDataPath, DefaultDirectoryName, file);
            if (File.Exists(path) || !File.Exists(legacyPath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Move(legacyPath, path);
            Debug.Log($"OC: moved the Save File from {legacyPath} to {path}.");
        }

        private static string FileNameOrDefault(string fileName) =>
            string.IsNullOrEmpty(fileName) ? DefaultFileName : fileName;
    }
}
