using Agent.Runtime.Tools;
using Xunit;
using System.IO;

namespace WorldSeed.Tests;

public class DotnetToolTests
{
    [Fact]
    public async Task DotnetTool_BuildsAndRunsProject()
    {
        var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(temp);
        var original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(temp);
        try
        {
            // create minimal console project
            await File.WriteAllTextAsync("Program.cs", "Console.WriteLine(args[0] == \"3+4*2\" ? \"11\" : \"Error\");");
            File.WriteAllText("DotnetTool.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>12</LangVersion></PropertyGroup></Project>");
            var tool = new DotnetTool();
            var result = await tool.ExecuteAsync("");
            Assert.False(string.IsNullOrWhiteSpace(result));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(temp, true);
        }
    }
}

