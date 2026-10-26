using System.Windows;

namespace TinyIcon.Views;

/// <summary>Interaction logic for SubImagePickerDialog.xaml.</summary>
public partial class SubImagePickerDialog : Window
{
    public SubImagePickerDialog() => InitializeComponent();

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    // The legacy columns sit left of 24/32-bit, so keep the right edge in place while they are shown or
    // hidden; otherwise the modern columns would jump under the mouse.
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded && e.WidthChanged)
            Left = Math.Max(SystemParameters.VirtualScreenLeft, Left - (e.NewSize.Width - e.PreviousSize.Width));
    }
}
