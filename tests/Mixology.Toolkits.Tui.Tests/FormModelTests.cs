using Xunit;

namespace Mixology.Toolkits.Tui.Tests;

public sealed class FormModelTests
{
    [Fact]
    public void EditValidateSubmitAndCompleteHaveExplicitOwnershipTransitions()
    {
        FormModel form = CreateForm();
        Assert.Equal(FormMode.Browse, form.Mode);
        Assert.Equal(InputOwnership.Browse, form.InputOwnership);

        form.BeginEdit();
        form.SetValue("name", "");
        Assert.True(form.IsDirty);
        Assert.Equal(InputOwnership.Edit, form.InputOwnership);
        Assert.False(form.TryBeginSubmit());
        Assert.Equal("name is required", form.Errors["name"]);

        form.SetValue("name", "Negroni");
        Assert.True(form.TryBeginSubmit());
        Assert.Equal(FormMode.Submitting, form.Mode);
        form.CompleteSubmit();

        Assert.Equal(FormMode.Browse, form.Mode);
        Assert.False(form.IsDirty);
        Assert.Equal("Negroni", form["name"]);
    }

    [Fact]
    public void CancelRestoresBaselineAndFailedSubmitReturnsToEdit()
    {
        FormModel form = CreateForm();
        form.BeginEdit();
        form.SetValue("name", "Daiquiri");
        form.CancelEdit();
        Assert.Equal("Margarita", form["name"]);
        Assert.False(form.IsDirty);

        form.BeginEdit();
        form.SetValue("name", "Daiquiri");
        Assert.True(form.TryBeginSubmit());
        form.FailSubmit("conflict");

        Assert.Equal(FormMode.Edit, form.Mode);
        Assert.Equal("conflict", form.Errors[string.Empty]);
        Assert.True(form.IsDirty);
    }

    [Fact]
    public void InvalidTransitionUsesTypedLifecycleError()
    {
        FormModel form = CreateForm();

        TuiLifecycleException failure = Assert.Throws<TuiLifecycleException>(() =>
            form.SetValue("name", "invalid"));

        Assert.Contains("browse", failure.Message, StringComparison.Ordinal);
    }

    private static FormModel CreateForm() => new(
    [
        new FormField(
            "name",
            "Margarita",
            static value => string.IsNullOrWhiteSpace(value) ? "name is required" : null),
        new FormField("description"),
    ]);
}
