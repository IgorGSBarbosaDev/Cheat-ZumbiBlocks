using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Launcher.Services;

namespace ZB2SecurityLab.Launcher.Tests;

[TestClass]
public sealed class ValveKeyValuesTests
{
    [TestMethod]
    public void Parse_ReadsNestedValuesEscapesAndComments()
    {
        var root = ValveKeyValues.Parse("""
            // Steam libraries
            "libraryfolders"
            {
                "0" { "path" "C:\\Program Files (x86)\\Steam" }
            }
            """);

        Assert.AreEqual(@"C:\Program Files (x86)\Steam", root.Child("libraryfolders")!.Child("0")!.Value("path"));
    }

    [TestMethod]
    public void Parse_SupportsLegacyDirectLibraryValues()
    {
        var root = ValveKeyValues.Parse("\"libraryfolders\" { \"1\" \"D:\\\\Games\" }");

        Assert.AreEqual(@"D:\Games", root.Child("libraryfolders")!.Value("1"));
    }

    [TestMethod]
    public void Parse_RejectsUnclosedObject()
        => Assert.ThrowsException<FormatException>(() => ValveKeyValues.Parse("\"root\" { \"key\" \"value\""));

    [TestMethod]
    public void Parse_RejectsUnexpectedClosingBrace()
        => Assert.ThrowsException<FormatException>(() => ValveKeyValues.Parse("}"));

    [TestMethod]
    public void NormalizeArchivePath_RejectsTraversal()
        => Assert.ThrowsException<InvalidDataException>(() => EmbeddedPayloadProvider.NormalizeArchivePath("BepInEx/../winhttp.dll"));

    [TestMethod]
    public void NormalizeArchivePath_ConvertsDirectorySeparators()
        => Assert.AreEqual(@"BepInEx\core\BepInEx.dll", EmbeddedPayloadProvider.NormalizeArchivePath("BepInEx/core/BepInEx.dll"));
}
