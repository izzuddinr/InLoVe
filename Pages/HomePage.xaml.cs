using System;
using Windows.Media.Core;
using Microsoft.UI.Xaml.Controls;
using Qatalyst.Utils;

namespace Qatalyst.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        ImageViewer.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());
    }
}