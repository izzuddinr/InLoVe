using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml.Controls;

namespace Qatalyst.Pages;

public sealed partial class PackageSelectionDialog
{
    private List<string> _allPackages = [];
    public List<string> SelectedPackages { get; private set; } = [];

    public PackageSelectionDialog()
    {
        InitializeComponent();
        SetScrollViewerMaxHeight();
    }

    private void SetScrollViewerMaxHeight()
    {
        var mainWindow = App.MainAppWindow;
        LogScrollViewer.MaxHeight = mainWindow.Bounds.Height * 0.75;
    }

    public void PopulatePackages(IEnumerable<string> packages, IEnumerable<string>? initiallySelectedPackages = null)
    {
        _allPackages = packages.ToList();
        if (initiallySelectedPackages != null)
        {
            SelectedPackages = initiallySelectedPackages.ToList();
        }
        InitializePackageCheckboxes();
    }

    private void InitializePackageCheckboxes()
    {
        var row = 0;
        PackageCheckboxGrid.RowDefinitions.Add(new RowDefinition());

        foreach (var packageName in _allPackages)
        {
            var checkBox = new CheckBox
            {
                Content = packageName,
                IsChecked = SelectedPackages.Contains(packageName)
            };

            checkBox.Checked += (_, _) => SelectedPackages.Add(packageName);
            checkBox.Unchecked += (_, _) => SelectedPackages.Remove(packageName);

            PackageCheckboxGrid.Children.Add(checkBox);
            Grid.SetRow(checkBox, row);

            row++;
            PackageCheckboxGrid.RowDefinitions.Add(new RowDefinition());
        }
        SortPackagesBySearchTerm(string.Empty);
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchTerm = SearchTextBox.Text.ToLower();
        SortPackagesBySearchTerm(searchTerm);
    }

    public void SortPackagesBySearchTerm(string searchTerm)
    {
        var allCheckboxes = PackageCheckboxGrid.Children.OfType<CheckBox>().ToList();

        var sortedCheckboxes = allCheckboxes
            .OrderByDescending(cb => cb.IsChecked == true) // Checked items come first
            .ThenByDescending(cb => cb.Content.ToString().Contains(searchTerm, System.StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        PackageCheckboxGrid.Children.Clear();

        var row = 0;

        foreach (var checkBox in sortedCheckboxes)
        {
            Grid.SetRow(checkBox, row);

            PackageCheckboxGrid.Children.Add(checkBox);

            if (++row >= PackageCheckboxGrid.RowDefinitions.Count)
            {
                PackageCheckboxGrid.RowDefinitions.Add(new RowDefinition());
            }
        }
    }

    public void ClearAllCheckboxes(ObservableCollection<string> selectedPackages)
    {
        foreach (var checkBox in PackageCheckboxGrid.Children.OfType<CheckBox>())
        {
            checkBox.IsChecked = false;
        }
        PackageCheckboxGrid.Children.Clear();
        SelectedPackages.Clear();
        selectedPackages.Clear();
        InitializePackageCheckboxes();
    }
}