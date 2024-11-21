using InLoVe.Utils;
using Microsoft.UI.Xaml.Controls;

namespace InLoVe.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());
    }
}