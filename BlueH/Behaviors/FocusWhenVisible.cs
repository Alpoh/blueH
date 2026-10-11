using System.Windows;
using System.Windows.Controls;

namespace BlueH.Behaviors;

public static class FocusWhenVisible
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(FocusWhenVisible),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not FrameworkElement target)
        {
            return;
        }

        target.IsVisibleChanged -= TakeFocus;

        if (args.NewValue is true)
        {
            target.IsVisibleChanged += TakeFocus;
        }
    }

    private static void TakeFocus(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement target || !target.IsVisible)
        {
            return;
        }

        target.Dispatcher.InvokeAsync(() => FocusAndSelectAll(target));
    }

    private static void FocusAndSelectAll(FrameworkElement target)
    {
        target.Focus();

        if (target is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }
}
