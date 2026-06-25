using Avalonia.Controls;

namespace Zaide;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Zaide";
        Width = 1280;
        Height = 800;
        MinWidth = 800;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}
