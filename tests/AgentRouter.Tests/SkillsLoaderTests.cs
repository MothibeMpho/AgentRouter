using System;
using System.IO;
using System.Collections.Generic;
using AgentRouter.businessLogic.skills;
using AgentRouter.models;
using Xunit;

namespace AgentRouter.Tests
{
    public class SkillsLoaderTests
    {
        [Fact]
        public void LoadSkillsFromJson_ReturnsSkills_WhenFileExists()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".yml");
            try
            {
                var yaml = "- name: sample\n  trigger_keywords: [\"hello\", \"world\"]\n  model: sample-model\n";
                File.WriteAllText(tempFile, yaml);

                var result = SkillsLoader.LoadSkillsFromJson(tempFile);

                Assert.NotNull(result);
                Assert.Single(result);
                Assert.Equal("sample", result[0].Name);
                Assert.Equal("sample-model", result[0].Model);
                Assert.Contains("hello", result[0].TriggerKeywords);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void LoadAll_ReturnsSkillFromMdFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "skill1.md");

            try
            {
                var content = "name: md-skill\ntrigger_keywords: [\"k1\", \"k2\"]\nmodel: md-model\n---\nThis is description\n";
                File.WriteAllText(file, content);

                var skills = SkillsLoader.LoadAll(dir);

                Assert.NotNull(skills);
                Assert.Single(skills);
                var s = skills[0];
                Assert.Equal("md-skill", s.Name);
                Assert.Equal("md-model", s.Model);
                Assert.Equal("This is description", s.Description);
            }
            finally
            {
                try { File.Delete(file); } catch { }
                try { Directory.Delete(dir); } catch { }
            }
        }

        [Fact]
        public void LoadAll_ThrowsDirectoryNotFoundException_WhenMissing()
        {
            var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Assert.False(Directory.Exists(missing));
            Assert.Throws<DirectoryNotFoundException>(() => SkillsLoader.LoadAll(missing));
        }

        [Fact]
        public void LoadSkillsFromJson_ThrowsFileNotFound_WhenMissing()
        {
            var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".yml");
            Assert.False(File.Exists(missing));
            Assert.Throws<FileNotFoundException>(() => SkillsLoader.LoadSkillsFromJson(missing));
        }

        [Fact]
        public void LoadAll_EmptyMdFile_ThrowsInvalidOperationException()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "empty.md");
            File.WriteAllText(file, string.Empty);

            try
            {
                Assert.Throws<InvalidOperationException>(() => SkillsLoader.LoadAll(dir));
            }
            finally
            {
                try { File.Delete(file); } catch { }
                try { Directory.Delete(dir); } catch { }
            }
        }
    }
}
