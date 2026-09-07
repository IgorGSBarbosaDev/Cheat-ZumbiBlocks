using System.ComponentModel;
using System.Windows;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.ViewModels;

namespace ZB2SecurityLab.Launcher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    internal MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.State is LauncherState.Preparing or LauncherState.LaunchRequested or LauncherState.Running or LauncherState.Cleaning)
        {
            var result = MessageBox.Show(
                "O worker concluirá o cleanup mesmo se a janela fechar. Deseja fechar o launcher?",
                "ZB2 Security Lab",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
            }
        }

        base.OnClosing(e);
    }
}
