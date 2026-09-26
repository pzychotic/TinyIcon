using System.Windows;

namespace TinyIcon.Views;

/// <summary>Interaction logic for SubImagePickerDialog.xaml.</summary>
public partial class SubImagePickerDialog : Window
{
    public SubImagePickerDialog() => InitializeComponent();

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
