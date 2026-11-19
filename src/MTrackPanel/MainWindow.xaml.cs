using System.ComponentModel;
using System.Windows;
using MTrackPanel.Services;

namespace MTrackPanel;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) ViewModel.Activate();
            else ViewModel.Deactivate();
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Closing the window only hides it; the app keeps running in the tray.
        if (!((App)Application.Current).IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    private void SoundSettings_Click(object sender, RoutedEventArgs e) => Shell.OpenSoundSettings();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
