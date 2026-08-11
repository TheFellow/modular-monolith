using Microsoft.Maui.Controls;

namespace Mixology.Gui;

public sealed class LabeledCheckBox : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(LabeledCheckBox),
        string.Empty);

    public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create(
        nameof(IsChecked),
        typeof(bool),
        typeof(LabeledCheckBox),
        false,
        BindingMode.TwoWay);

    public LabeledCheckBox()
    {
        CheckBox checkBox = new();
        checkBox.SetBinding(CheckBox.IsCheckedProperty, new Binding(
            nameof(IsChecked),
            source: this,
            mode: BindingMode.TwoWay));
        Label label = new() { VerticalOptions = LayoutOptions.Center };
        label.SetBinding(Label.TextProperty, new Binding(nameof(Text), source: this));
        Content = new HorizontalStackLayout
        {
            Spacing = 6,
            Children = { checkBox, label },
        };
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }
}
