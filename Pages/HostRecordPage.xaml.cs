using InLoVe.Utils;

namespace InLoVe.Pages;

public partial class HostRecordPage
{

    public HostRecordPage()
    {
        InitializeComponent();
        HostRecordScrollViewer.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());

    }
}

