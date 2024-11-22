using System;
using Windows.Media.Core;
using InLoVe.Utils;
using Microsoft.UI.Xaml.Controls;

namespace InLoVe.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        HomePageScrollViewer.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());
    }
}