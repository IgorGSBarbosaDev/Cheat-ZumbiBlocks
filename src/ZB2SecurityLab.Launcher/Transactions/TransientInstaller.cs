using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;

namespace ZB2SecurityLab.Launcher.Transactions;

internal interface ITransientInstaller
{
    void Install(LaunchSessionJournal journal, PayloadPackage payload, JournalStore store);
}

internal sealed class TransientInstaller : ITransientInstaller
{
    public void Install(LaunchSessionJournal journal, PayloadPackage payload, JournalStore store)
    {
        var gameRoot = Path.GetFullPath(journal.GamePath);
        EnsureNoCollisions(gameRoot, payload);
        journal.Phase = JournalPhase.Installing;
        store.Save(journal);

        try
        {
            var bepInExRoot = BuildValidator.ResolveGamePath(gameRoot, "BepInEx");
            journal.BepInExDirectoryCreated = true;
            store.Save(journal);
            CreateNewDirectory(bepInExRoot);
            var markerPath = BuildValidator.ResolveGamePath(gameRoot, PayloadConstants.MarkerRelativePath);
            journal.CreatedFiles.Add(PayloadConstants.MarkerRelativePath);
            store.Save(journal);
            WriteNew(markerPath, Encoding.UTF8.GetBytes(journal.SessionId));

            foreach (var file in payload.Files)
            {
                var destination = BuildValidator.ResolveGamePath(gameRoot, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                journal.CreatedFiles.Add(file.RelativePath);
                if (!file.RelativePath.StartsWith("BepInEx\\", StringComparison.OrdinalIgnoreCase))
                {
                    journal.RootFileHashes[file.RelativePath] = file.Sha256;
                }

                store.Save(journal);
                WriteNew(destination, file.Contents);
            }

            journal.Phase = JournalPhase.Installed;
            store.Save(journal);
        }
        catch (Exception exception)
        {
            journal.Errors.Add($"INSTALL_FAILED:{exception.GetType().Name}:{exception.Message}");
            journal.Phase = JournalPhase.RecoveryRequired;
            store.Save(journal);
            throw;
        }
    }

    internal static void EnsureNoCollisions(string gameRoot, PayloadPackage payload)
    {
        if (Directory.Exists(Path.Combine(gameRoot, "BepInEx")))
        {
            throw new IOException("BepInEx já existe; nenhum arquivo será mesclado.");
        }

        foreach (var file in payload.Files.Where(file => !file.RelativePath.StartsWith("BepInEx\\", StringComparison.OrdinalIgnoreCase)))
        {
            var destination = BuildValidator.ResolveGamePath(gameRoot, file.RelativePath);
            if (File.Exists(destination) || Directory.Exists(destination))
            {
                throw new IOException($"Destino já existe: {file.RelativePath}.");
            }
        }
    }

    internal static void ProbeWriteAccess(string gameRoot)
    {
        var probe = BuildValidator.ResolveGamePath(gameRoot, $".zb2securitylab-write-probe-{Guid.NewGuid():N}");
        try
        {
            WriteNew(probe, Array.Empty<byte>());
        }
        finally
        {
            if (File.Exists(probe))
            {
                File.Delete(probe);
            }
        }
    }

    private static void WriteNew(string path, byte[] contents)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(contents);
        stream.Flush(flushToDisk: true);
    }

    private static void CreateNewDirectory(string path)
    {
        if (!CreateDirectory(path, IntPtr.Zero))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), $"Não foi possível criar exclusivamente '{path}'.");
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectory(string path, IntPtr securityAttributes);
}
