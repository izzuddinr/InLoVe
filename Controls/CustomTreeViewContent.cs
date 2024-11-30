using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace Qatalyst.Controls;

public partial class CustomTreeViewContent : INotifyPropertyChanged
{
    private string _value;
    private string _tag;
    private object? _icon;
    private Brush? _textColor;

    public CustomTreeViewContent(string value = "", object? icon = null, Brush? textColor = null)
    {
        _value = value;
        _icon = icon;
        _textColor = textColor ?? new SolidColorBrush(Colors.Black);
    }

    public object? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            OnPropertyChanged(nameof(Icon));
        }
    }

    public string Tag
    {
        get => _tag;
        set
        {
            _tag = value;
            OnPropertyChanged(nameof(Tag));
        }
    }

    public Brush? TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            OnPropertyChanged(nameof(TextColor));
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            _value = value;
            OnPropertyChanged(nameof(Value));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public static class HostRecordTagExtensions
{
    private static string K_RECORD = "K Records (CA Public Key)";
    private static string A_RECORD = "A Records (Contact Configs)";
    private static string X_RECORD = "X Records (Contactless Config)";
    private static string C_RECORD = "C Records (Card Prefix Configs)";
    private static string CARD_CONFIGS = "Card Configs";

    private static List<string> kRecordTags =
    [
        "rid",
        "ridIndex",
        "exponent",
        "modulus",
        "sha1",
        "expiryDate",
    ];

    private static List<string> emvRecordTags =
    [
        "aid",
        "appVersion",
        "terminalCapabilities",
        "thresholdValue",
        "targetPercentage",
        "maxTargetPercentage",
        "tacDenial",
        "defaultAccount",
        "tacOnline",
        "tacDefault",
        "recommendedAppName",
        "defaultDdol",
        "defaultTdol",
        "enabled",
        "currencyLabel",
        "emvNonEmvRefundTransaction"
    ];

    private static List<string> cRecordTags =
    [
        "panMinLength",
        "panMaxLength",
        "cvvBypassCheck",
        "cardCashEnable",
        "cardCashbackEnable",
        "cardVoidCashEnable",
        "magstripePinRequired",
        "manualPinRequired",
        "cardPinBypassEnable",
        "checkSvc",
        "luhnCheckMode",
        "expDateCheckMode",
        "binRangeLow",
        "binRangeHigh",
        "velocityCard",
        "panTruncationStart",
        "panTruncationEnd",
        "onlinePurchaseWithoutCashTxnLimit",
        "onlinePurchaseWithCashTxnLimit",
        "onlineCashTxnLimit",
        "onlineRefundTxnLimit",
        "offlinePurchaseWithoutCashTxnLimit",
        "txnAuthorityRequirement",
        "cardSaleEnable",
        "accountGroupingCodeOnline",
        "accountGroupingCodeOffline",
        "cvcPrompt",
        "enabled",
        "emvEnabled",
        "clessEnabled",
        "manualEnabled",
        "swipeEnabled",
        "cardScheme",
        "addressVerification",
        "addressVerificationSwipe",
        "cardRefundEnable",
    ];

    public static string GetHostRecordContent(this HostRecordTag tag) =>
        tag switch
        {
            HostRecordTag.A_RECORD => A_RECORD,
            HostRecordTag.C_RECORD => C_RECORD,
            HostRecordTag.K_RECORD => K_RECORD,
            HostRecordTag.X_RECORD => X_RECORD,
            HostRecordTag.CARD_CONFIGS => CARD_CONFIGS,
            _ => string.Empty
        };

    public static string ToString(this HostRecordTag tag) =>
        tag switch
        {
            HostRecordTag.A_RECORD => "A_RECORD",
            HostRecordTag.C_RECORD => "C_RECORD",
            HostRecordTag.K_RECORD => "K_RECORD",
            HostRecordTag.X_RECORD => "X_RECORD",
            HostRecordTag.CARD_CONFIGS => "CARD_CONFIGS",
            _ => string.Empty
        };

    public static HostRecordTag ToHostRecordTag(this string tag) =>
        tag switch
        {
            "A_RECORD" => HostRecordTag.A_RECORD,
            "C_RECORD" => HostRecordTag.C_RECORD,
            "K_RECORD" => HostRecordTag.K_RECORD,
            "X_RECORD" => HostRecordTag.X_RECORD,
            "CARD_CONFIGS" => HostRecordTag.CARD_CONFIGS,
            _ => HostRecordTag.UNKNOWN
        };

    public static List<string> GetRecordTags(this HostRecordTag tag) {
        var recordTags = tag switch
        {
            HostRecordTag.A_RECORD or HostRecordTag.X_RECORD => emvRecordTags,
            HostRecordTag.C_RECORD => cRecordTags,
            HostRecordTag.K_RECORD => kRecordTags,
            _ => []
        };

        recordTags.Sort();

        return recordTags;
    }
}

public enum HostRecordTag
{
    A_RECORD,
    C_RECORD,
    K_RECORD,
    X_RECORD,
    CARD_CONFIGS,
    UNKNOWN,
}