using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;

namespace ZB2SecurityLab.Launcher.Transactions;

internal interface ICleanupCoordinator
{
    bool Cleanup(LaunchSessionJournal journal, JournalStore store, LocalDataPaths dataPaths);
}

internal sealed class CleanupCoordinator(IBuildValidator buildValidator) : ICleanupCoordinator
{
    public bool Cleanup(LaunchSessionJournal journal, JournalStore store, LocalDataPaths dataPaths)
    {
        var errors = new List<string>();
        journal.Phase = JournalPhase.Cleaning;
        store.Save(journal);

        ArchiveLoaderLog(journal, dataPaths, errors);
        RemoveRootFiles(journal, errors);
        RemoveOwnedBepInEx(journal, errors);

        var validation = buildValidator.ValidateFiles(journal.GamePath);
        if (!validation.Executable.IsMatch || !validation.AssemblyCSharp.IsMatch)
        {
            errors.Add("Os fingerprints originais não puderam ser confirmados após o cleanup.");
        }

        journal.Errors.AddRange(errors);
        journal.Phase = errors.Count == 0 ? JournalPhase.Completed : JournalPhase.RecoveryRequired;
        store.Save(journal);
        return errors.Count == 0;
    }

    private static void ArchiveLoaderLog(LaunchSessionJournal journal, LocalDataPaths dataPaths, ICollection<string> errors)
    {
        var source = BuildValidator.ResolveGamePath(journal.GamePath, @"BepInEx\LogOutput.log");
        if (!File.Exists(source))
        {
            return;
        }

        try
        {
            dataPaths.EnsureCreated();
            File.Copy(source, Path.Combine(dataPaths.LoaderLogs, $"{journal.SessionId}.log"), overwrite: true);
        }
        catch (Exception exception)
        {
            errors.Add($"LOG_ARCHIVE_FAILED:{exception.GetType().Name}:{exception.Message}");
        }
    }

    private static void RemoveRootFiles(LaunchSessionJournal journal, ICollection<string> errors)
    {
        var allowedRootFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".doorstop_version",
            "doorstop_config.ini",
            "winhttp.dll"
        };
        foreach (var file in journal.RootFileHashes)
        {
            if (!allowedRootFiles.Contains(file.Key) || !journal.CreatedFiles.Contains(file.Key, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"UNSAFE_ROOT_JOURNAL_ENTRY:{file.Key}");
                continue;
            }

            var path = BuildValidator.ResolveGamePath(journal.GamePath, file.Key);
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
                if (!string.Equals(actual, file.Value, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"ROOT_FILE_CHANGED:{file.Key}");
                    continue;
                }

                File.Delete(path);
            }
            catch (Exception exception)
            {
                errors.Add($"ROOT_DELETE_FAILED:{file.Key}:{exception.GetType().Name}:{exception.Message}");
            }
        }
    }

    private static void RemoveOwnedBepInEx(LaunchSessionJournal journal, ICollection<string> errors)
    {
        var root = BuildValidator.ResolveGamePath(journal.GamePath, "BepInEx");
        if (!Directory.Exists(root))
        {
            return;
        }

        var marker = BuildValidator.ResolveGamePath(journal.GamePath, PayloadConstants.MarkerRelativePath);
        try
        {
            if (!journal.BepInExDirectoryCreated)
            {
                errors.Add("BEPINEX_NOT_OWNED_BY_JOURNAL");
                return;
            }

            if (!File.Exists(marker))
            {
                if (journal.BepInExDirectoryCreated && !Directory.EnumerateFileSystemEntries(root).Any())
                {
                    Directory.Delete(root);
                    return;
                }

                errors.Add("BEPINEX_MARKER_MISSING");
                return;
            }

            if (!string.Equals(File.ReadAllText(marker), journal.SessionId, StringComparison.Ordinal))
            {
                errors.Add("BEPINEX_MARKER_MISMATCH");
                return;
            }

            Directory.Delete(root, recursive: true);
        }
        catch (Exception exception)
        {
            errors.Add($"BEPINEX_DELETE_FAILED:{exception.GetType().Name}:{exception.Message}");
        }
    }
}
