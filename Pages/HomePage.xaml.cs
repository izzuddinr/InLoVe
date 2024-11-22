using System;
using InLoVe.Utils;
using Microsoft.UI.Xaml.Controls;
using Windows.Media.Core;

namespace InLoVe.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        HomePageScrollViewer.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());
    }
}