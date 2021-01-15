using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SqlBackupTools.Tests
{
    public class CommandLineTest
    {
        [Fact]
        public async Task CleanEmpty()
        {
            var temp = new DirectoryInfo(Path.GetTempPath());
            var source = temp.CreateSubdirectory(Guid.NewGuid().ToString("N"));
            var target = temp.CreateSubdirectory(Guid.NewGuid().ToString("N"));

            var result = await Program
                .Main(new[] { "clean", "-h", TestContext.SqlInstance, "-f", source.FullName, "-c", target.FullName });

            Assert.Equal(0, result);

            source.Delete(true);
            target.Delete(true);
        }

        [Fact]
        public async Task CleanFilled100Percent()
        {
            var temp = new DirectoryInfo(Path.GetTempPath());
            var source = temp.CreateSubdirectory(Guid.NewGuid().ToString("N"));
            var target = temp.CreateSubdirectory(Guid.NewGuid().ToString("N"));
            for (int i = 0; i < 10; i++)
            {
                source.CreateSubdirectory(Guid.NewGuid().ToString("N"));
            }

            var result = await Program
                .Main(new[] { "clean", "-h", TestContext.SqlInstance, "-f", source.FullName, "-c", target.FullName });

            Assert.Equal(1, result);

            source.Delete(true);
            target.Delete(true);
        }

        [Fact]
        public void IncompleteCommands()
        {
            Assert.False(new[] { "clean", "-h", TestContext.SqlInstance }.TryParseCommand(out _));
            Assert.False(new[] { "clean", "-h", TestContext.SqlInstance, "-f", "test" }.TryParseCommand(out _));
            Assert.False(new[] { "clean", "-h", TestContext.SqlInstance, "-c", "" }.TryParseCommand(out _));
            Assert.False(new[] { "clean", "-h", TestContext.SqlInstance, "-f", "", "-c", "" }.TryParseCommand(out _));
            Assert.False(new[] { "clean", "-h", TestContext.SqlInstance, "-f", "test", "-c", "" }.TryParseCommand(out _));
            Assert.False(new[] { "clean", "-h", TestContext.SqlInstance, "-f", "", "-c", "test" }.TryParseCommand(out _));
            
            Assert.True(new[] { "clean", "-h", TestContext.SqlInstance, "-f", "test", "-c", "test" }.TryParseCommand(out _));
        }
    }
}
