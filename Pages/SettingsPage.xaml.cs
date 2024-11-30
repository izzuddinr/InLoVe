using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Qatalyst.Utils;

namespace Qatalyst.Pages;

public sealed partial class SettingsPage : Page
{
    private List<Image> _images = [];

    public SettingsPage()
    {
        InitializeComponent();
        MainGrid.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());
        _images =
        [
            ImageViewer0,
            ImageViewer1,
            ImageViewer2,
            ImageViewer3,
            ImageViewer4,
            ImageViewer5,
            ImageViewer6,
            ImageViewer7,
            ImageViewer8,
            ImageViewer9,
            ImageViewer10,
            ImageViewer11
        ];
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadRandomImage();
    }

    private void LoadRandomImage()
    {
        try
        {
            var randomNumber = new Random().Next(0, 11); // Generate a random number between 0 and 11

            foreach (var image in _images)
            {
                image.Visibility = Visibility.Collapsed;
            }

            _images[randomNumber].Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            // Handle any errors (e.g., missing image files)
            Console.WriteLine("Error loading image: " + ex.StackTrace);
        }
    }
}