using System.Windows;

namespace TinyIcon.Views;

/// <summary>Interaction logic for NewIconDialog.xaml.</summary>
public partial class NewIconDialog : Window
{
    public NewIconDialog() => InitializeComponent();

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
