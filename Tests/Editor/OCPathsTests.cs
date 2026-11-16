#nullable enable

using System.IO;
using NUnit.Framework;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCPathsTests
    {
        private const string AppRoot = "/app/root";
        private const string PersistentDataPath = "/persistent/data";

        [Test]
        public void EmptyDirectory_ResolvesUnderPersistentDataPath()
        {
            var result = OCPaths.Resolve(string.Empty, "save.json", AppRoot, PersistentDataPath);
            Assert.AreEqual(Path.Combine(PersistentDataPath, "OperatorConsole", "save.json"), result);
        }

        [Test]
        public void RootedDirectory_IsKeptAsIs()
        {
            var rooted = Path.Combine(Path.GetPathRoot(Path.GetTempPath()) ?? "/", "custom", "dir");
            var result = OCPaths.Resolve(rooted, "save.json", AppRoot, PersistentDataPath);
            Assert.AreEqual(Path.Combine(rooted, "save.json"), result);
        }

        [Test]
        public void RelativeDirectory_ResolvesUnderAppRoot()
        {
            var result = OCPaths.Resolve("saves", "save.json", AppRoot, PersistentDataPath);
            Assert.AreEqual(Path.Combine(Path.GetFullPath(Path.Combine(AppRoot, "saves")), "save.json"), result);
        }

        [Test]
        public void EmptyFileName_DefaultsToSaveJson()
        {
            var result = OCPaths.Resolve(string.Empty, string.Empty, AppRoot, PersistentDataPath);
            Assert.AreEqual(Path.Combine(PersistentDataPath, "OperatorConsole", "save.json"), result);
        }

        [Test]
        public void LegacySaveFile_MovesToTheCurrentDefaultDirectory()
        {
            var root = NewTempDir();
            var legacy = WriteFile(Path.Combine(root, "DSM", "save.json"), "{\"a\":1}");

            OCPaths.MoveLegacySaveFile(string.Empty, "save.json", root);

            Assert.IsFalse(File.Exists(legacy));
            Assert.AreEqual("{\"a\":1}", File.ReadAllText(Path.Combine(root, "OperatorConsole", "save.json")));
        }

        [Test]
        public void LegacySaveFile_IsLeftAloneWhenTheCurrentOneExists()
        {
            var root = NewTempDir();
            var legacy = WriteFile(Path.Combine(root, "DSM", "save.json"), "old");
            var current = WriteFile(Path.Combine(root, "OperatorConsole", "save.json"), "new");

            OCPaths.MoveLegacySaveFile(string.Empty, "save.json", root);

            Assert.AreEqual("old", File.ReadAllText(legacy));
            Assert.AreEqual("new", File.ReadAllText(current));
        }

        [Test]
        public void LegacySaveFile_IsLeftAloneWhenASaveDirectoryIsSet()
        {
            var root = NewTempDir();
            var legacy = WriteFile(Path.Combine(root, "DSM", "save.json"), "old");

            OCPaths.MoveLegacySaveFile("saves", "save.json", root);

            Assert.IsTrue(File.Exists(legacy));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "OperatorConsole")));
        }

        private string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "OCPathsTests", Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            _tempDirs.Add(dir);
            return dir;
        }

        private static string WriteFile(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
            return path;
        }

        private readonly System.Collections.Generic.List<string> _tempDirs = new();

        [TearDown]
        public void DeleteTempDirs()
        {
            foreach (var dir in _tempDirs)
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            _tempDirs.Clear();
        }
    }
}
