#nullable enable

using NUnit.Framework;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCTagFilterTests
    {
        private static OCEntryDefinition Entry(bool exposed, params string[] tagIds)
        {
            var entry = new OCEntryDefinition { Key = "k", Exposed = exposed };
            entry.TagIds.AddRange(tagIds);
            return entry;
        }

        [Test]
        public void Shows_FalseWhenNotExposed()
        {
            Assert.IsFalse(OCTagFilter.Shows(Entry(false), new string[0]));
            Assert.IsFalse(OCTagFilter.Shows(Entry(false, "a"), new[] { "a" }));
        }

        [Test]
        public void Shows_TrueWhenPanelServesNoTag()
        {
            Assert.IsTrue(OCTagFilter.Shows(Entry(true, "a"), new string[0]));
        }

        [Test]
        public void Shows_TrueWhenEntryNamesNoTag()
        {
            Assert.IsTrue(OCTagFilter.Shows(Entry(true), new[] { "a" }));
        }

        [Test]
        public void Shows_TrueWhenAnyTagIsShared()
        {
            Assert.IsTrue(OCTagFilter.Shows(Entry(true, "a", "b"), new[] { "c", "b" }));
        }

        [Test]
        public void Shows_FalseWhenNoTagIsShared()
        {
            Assert.IsFalse(OCTagFilter.Shows(Entry(true, "a"), new[] { "b" }));
        }

        [Test]
        public void Shows_Command_MatchesTagNamesThroughTheConfig()
        {
            var config = TestConfigs.Create();
            config.AddTagForTests(new OCTag("tech-id", "Technician"));
            config.AddTagForTests(new OCTag("admin-id", "Admin"));
            var registry = new OCCommandRegistry(() => null);
            try
            {
                var untagged = registry.Register(new object(), "Untagged", _ => Cysharp.Threading.Tasks.UniTask.CompletedTask);
                var tech = registry.Register(new object(), "Tech", _ => Cysharp.Threading.Tasks.UniTask.CompletedTask, new OCCommandOptions { Tags = new[] { "Technician" } });
                var ghost = registry.Register(new object(), "Ghost", _ => Cysharp.Threading.Tasks.UniTask.CompletedTask, new OCCommandOptions { Tags = new[] { "Ghost" } });

                Assert.IsTrue(OCTagFilter.Shows(untagged, new[] { "admin-id" }, config));
                Assert.IsTrue(OCTagFilter.Shows(tech, new[] { "tech-id" }, config));
                Assert.IsFalse(OCTagFilter.Shows(tech, new[] { "admin-id" }, config));
                Assert.IsTrue(OCTagFilter.Shows(ghost, new string[0], config));
                Assert.IsFalse(OCTagFilter.Shows(ghost, new[] { "admin-id" }, config));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Shows_FalseWhenEntryNamesOnlyUnknownTags()
        {
            Assert.IsFalse(OCTagFilter.Shows(Entry(true, "deleted"), new[] { "a" }));
        }

        private static void WithTags(System.Action<OCConfig> test)
        {
            var config = TestConfigs.Create();
            config.AddTagForTests(new OCTag("display-id", "Display"));
            config.AddTagForTests(new OCTag("p4-id", "Profile4"));
            try { test(config); }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void TryResolveNames_ResolvesNamesToIdsWithoutDuplicates()
        {
            WithTags(config =>
            {
                var ids = new System.Collections.Generic.List<string>();
                Assert.IsTrue(OCTagFilter.TryResolveNames(config, new[] { "Display", "Profile4", "Display" }, ids, out _));
                CollectionAssert.AreEqual(new[] { "display-id", "p4-id" }, ids);
            });
        }

        [Test]
        public void TryResolveNames_RefusesAnEmptySetAndKeepsIds()
        {
            WithTags(config =>
            {
                var ids = new System.Collections.Generic.List<string> { "display-id" };
                Assert.IsFalse(OCTagFilter.TryResolveNames(config, new string[0], ids, out var error));
                StringAssert.Contains("ShowAllTags", error);
                CollectionAssert.AreEqual(new[] { "display-id" }, ids);
            });
        }

        [Test]
        public void TryResolveNames_RefusesAnUndeclaredTagAndKeepsIds()
        {
            WithTags(config =>
            {
                var ids = new System.Collections.Generic.List<string> { "display-id" };
                Assert.IsFalse(OCTagFilter.TryResolveNames(config, new[] { "Display", "Profil4" }, ids, out var error));
                StringAssert.Contains("Profil4", error);
                CollectionAssert.AreEqual(new[] { "display-id" }, ids);
            });
        }

        [Test]
        public void SameSet_IgnoresOrder()
        {
            Assert.IsTrue(OCTagFilter.SameSet(new[] { "a", "b" }, new[] { "b", "a" }));
            Assert.IsFalse(OCTagFilter.SameSet(new[] { "a" }, new[] { "a", "b" }));
            Assert.IsFalse(OCTagFilter.SameSet(new[] { "a", "b" }, new[] { "a", "c" }));
        }
    }
}
