using System.Windows;
using ZB2SecurityLab.Launcher.ViewModels;
using ZB2SecurityLab.Launcher.Worker;

namespace ZB2SecurityLab.Launcher;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (WorkerEntry.IsWorker(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var exitCode = await WorkerEntry.RunAsync(e.Args).ConfigureAwait(true);
            Shutdown(exitCode);
            return;
        }

        var viewModel = new MainViewModel();
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
        await viewModel.InitializeAsync().ConfigureAwait(true);
    }
}
