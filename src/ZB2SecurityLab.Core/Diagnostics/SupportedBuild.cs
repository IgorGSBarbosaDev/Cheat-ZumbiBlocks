namespace ZB2SecurityLab.Core.Diagnostics;

/// <summary>
/// Single C# source of truth for the Steam build authorized by the lab.
/// </summary>
public static class SupportedBuild
{
    public const string SteamAppId = "1941780";
    public const string BuildId = "24525702";
    public const string UnityVersion = "6000.3.21f1";
    public const string Runtime = "Mono x64";

    public const string ExecutableRelativePath = "ZumbiBlocks2.exe";
    public const string AssemblyCSharpRelativePath = "ZumbiBlocks2_Data/Managed/Assembly-CSharp.dll";
    public const string MonoLibraryRelativePath = "ZumbiBlocks2_Data/Managed/mscorlib.dll";

    public const string ExecutableSha256 = "66C3ED6829349AAC8B5CB5FDB2A85EF62D17C1BDED4334352AF7349CA608EA0A";
    public const string AssemblyCSharpSha256 = "C41A298975D35F0DAD0A05531BCE6E0B6E274D0DDF265217D65CE3AC5CBC84E1";
}
