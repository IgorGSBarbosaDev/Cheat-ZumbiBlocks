using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using ZB2SecurityLab.Launcher.Domain;

namespace ZB2SecurityLab.Launcher.Worker;

internal sealed record WorkerResult(bool Succeeded, bool RecoveryRequired, string? Error);

internal sealed class WorkerClient
{
    internal async Task<WorkerResult> RunSessionAsync(
        string journalPath,
        string nonce,
        Func<Task<DateTimeOffset>> launch,
        Action<string, string?> status,
        CancellationToken cancellationToken)
    {
        var first = await RunAttemptAsync(
            LauncherProtocol.WorkerArgument,
            journalPath,
            nonce,
            elevated: false,
            launch,
            status,
            cancellationToken).ConfigureAwait(true);

        if (!string.Equals(first.Error, LauncherProtocol.ElevationRequired, StringComparison.Ordinal))
        {
            return first;
        }

        status(LauncherProtocol.ElevationRequired, "Permissão administrativa necessária para a instalação temporária.");
        return await RunAttemptAsync(
            LauncherProtocol.WorkerArgument,
            journalPath,
            nonce,
            elevated: true,
            launch,
            status,
            cancellationToken).ConfigureAwait(true);
    }

    internal async Task<WorkerResult> RecoverAsync(
        string journalPath,
        string nonce,
        Action<string, string?> status,
        CancellationToken cancellationToken)
    {
        var first = await RunAttemptAsync(
            LauncherProtocol.RecoverArgument,
            journalPath,
            nonce,
            elevated: false,
            launch: null,
            status,
            cancellationToken).ConfigureAwait(true);

        if (!string.Equals(first.Error, LauncherProtocol.ElevationRequired, StringComparison.Ordinal))
        {
            return first;
        }

        status(LauncherProtocol.ElevationRequired, "Permissão administrativa necessária para concluir o cleanup.");
        return await RunAttemptAsync(
            LauncherProtocol.RecoverArgument,
            journalPath,
            nonce,
            elevated: true,
            launch: null,
            status,
            cancellationToken).ConfigureAwait(true);
    }

    private static async Task<WorkerResult> RunAttemptAsync(
        string mode,
        string journalPath,
        string nonce,
        bool elevated,
        Func<Task<DateTimeOffset>>? launch,
        Action<string, string?> status,
        CancellationToken cancellationToken)
    {
        var pipeName = $"zb2securitylab-{Guid.NewGuid():N}";
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        using var process = StartWorker(mode, journalPath, pipeName, nonce, elevated);
        using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectionTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        await pipe.WaitForConnectionAsync(connectionTimeout.Token).ConfigureAwait(true);

        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        var hello = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(true);
        if (!string.Equals(hello, $"HELLO|{nonce}", StringComparison.Ordinal))
        {
            return new WorkerResult(false, true, "Falha de autenticação do worker.");
        }

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(true);
            if (line is null)
            {
                return new WorkerResult(false, true, "Worker encerrou sem resultado final.");
            }

            var separator = line.IndexOf('|');
            var code = separator < 0 ? line : line[..separator];
            var detail = separator < 0 ? null : line[(separator + 1)..];
            status(code, detail);

            switch (code)
            {
                case LauncherProtocol.ElevationRequired:
                    return new WorkerResult(false, false, LauncherProtocol.ElevationRequired);
                case LauncherProtocol.Installed:
                    if (launch is null)
                    {
                        await writer.WriteLineAsync("ABORT").ConfigureAwait(true);
                        break;
                    }

                    try
                    {
                        var requestedUtc = await launch().ConfigureAwait(true);
                        await writer.WriteLineAsync($"LAUNCH|{requestedUtc:O}").ConfigureAwait(true);
                    }
                    catch (Exception exception)
                    {
                        await writer.WriteLineAsync($"ABORT|{exception.GetType().Name}:{exception.Message}").ConfigureAwait(true);
                    }

                    break;
                case LauncherProtocol.Completed:
                    return new WorkerResult(true, false, null);
                case LauncherProtocol.Blocked:
                    return new WorkerResult(false, false, detail);
                case LauncherProtocol.RecoveryRequired:
                    return new WorkerResult(false, true, detail);
            }
        }
    }

    private static Process StartWorker(string mode, string journalPath, string pipeName, string nonce, bool elevated)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path indisponível.");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = $"{mode} {Quote(journalPath)} {Quote(pipeName)} {Quote(nonce)}",
            UseShellExecute = elevated,
            CreateNoWindow = !elevated,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        if (elevated)
        {
            startInfo.Verb = "runas";
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Não foi possível iniciar o worker.");
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
