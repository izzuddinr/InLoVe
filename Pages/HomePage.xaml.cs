using System;
using Windows.UI.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Qatalyst.Services;
using Qatalyst.Utils;

namespace Qatalyst.Pages;

public sealed partial class HomePage : Page
{
    private ConfigService _configService;
    private DispatcherQueueTimer _quoteTimer;

    public HomePage()
    {
        InitializeComponent();
        HomePageGrid.Background = ColorManager.GetBrush(ApplicationColor.AppBackgroundColor.ToString());
        TextQ.Foreground = ColorManager.GetBrush(ApplicationColor.VerboseColor.ToString());
        AuthorQ.Foreground = ColorManager.GetBrush(ApplicationColor.InfoColor.ToString());
        _configService = App.Services.GetService<ConfigService>();

        // Initialize the DispatcherQueueTimer
        InitializeQuoteTimer();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        GetRandomQuote();
        StartQuoteTimer();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopQuoteTimer();
    }

    private void GetRandomQuote()
    {
        var QDictionary = _configService.RandomQs;
        if (QDictionary == null || QDictionary.Count == 0) return;

        var random = new Random();
        var index = random.Next(0, QDictionary.Count);
        var randomQ = QDictionary[index];

        TextQ.Text = $"\"{randomQ.Text}\"";
        TextQ.FontStyle = FontStyle.Italic;
        AuthorQ.Text = $"— {randomQ.From} —";
    }

    private void InitializeQuoteTimer()
    {
        _quoteTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _quoteTimer.Interval = TimeSpan.FromSeconds(15); // Set the interval to 10 seconds
        _quoteTimer.Tick += (sender, args) =>
        {
            // Update the quote on the UI thread
            GetRandomQuote();
        };
    }

    private void StartQuoteTimer()
    {
        if (!_quoteTimer.IsRunning)
        {
            _quoteTimer.Start();
        }
    }

    private void StopQuoteTimer()
    {
        if (_quoteTimer.IsRunning)
        {
            _quoteTimer.Stop();
        }
    }
}
