namespace Mixology.Toolkits.Tui;

public enum FormMode
{
    Browse,
    Edit,
    Submitting,
}

public sealed record FormField
{
    public FormField(string name, string value = "", Func<string, string?>? validate = null)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("form field name is required", nameof(name))
            : name.Trim();
        Value = value ?? string.Empty;
        Validate = validate;
    }

    public string Name { get; }
    public string Value { get; }
    public Func<string, string?>? Validate { get; }
}

public sealed class FormModel
{
    private readonly Dictionary<string, FormField> fields;
    private readonly Dictionary<string, string> baseline;
    private readonly Dictionary<string, string> values;
    private readonly Dictionary<string, string> errors = new(StringComparer.Ordinal);

    public FormModel(IEnumerable<FormField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        FormField[] fieldArray = fields.ToArray();
        this.fields = fieldArray.ToDictionary(static field => field.Name, StringComparer.Ordinal);
        if (this.fields.Count != fieldArray.Length)
        {
            throw new ArgumentException("form field names must be unique", nameof(fields));
        }

        baseline = this.fields.ToDictionary(static pair => pair.Key, static pair => pair.Value.Value, StringComparer.Ordinal);
        values = new Dictionary<string, string>(baseline, StringComparer.Ordinal);
    }

    public FormMode Mode { get; private set; }
    public bool IsDirty => values.Any(pair => !string.Equals(pair.Value, baseline[pair.Key], StringComparison.Ordinal));
    public IReadOnlyDictionary<string, string> Errors => errors;
    public InputOwnership InputOwnership => Mode == FormMode.Browse
        ? InputOwnership.Browse
        : InputOwnership.Edit;

    public string this[string name] => values.TryGetValue(name, out string? value)
        ? value
        : throw new KeyNotFoundException($"form field \"{name}\" was not found");

    public void BeginEdit()
    {
        RequireMode(FormMode.Browse, "begin editing");
        errors.Clear();
        Mode = FormMode.Edit;
    }

    public void SetValue(string name, string value)
    {
        RequireMode(FormMode.Edit, "change a field");
        if (!values.ContainsKey(name))
        {
            throw new KeyNotFoundException($"form field \"{name}\" was not found");
        }

        values[name] = value ?? string.Empty;
        _ = errors.Remove(name);
    }

    public bool TryBeginSubmit()
    {
        RequireMode(FormMode.Edit, "submit");
        errors.Clear();
        foreach ((string name, FormField field) in fields)
        {
            string? error = field.Validate?.Invoke(values[name]);
            if (!string.IsNullOrEmpty(error))
            {
                errors[name] = error;
            }
        }

        if (errors.Count != 0)
        {
            return false;
        }

        Mode = FormMode.Submitting;
        return true;
    }

    public void CompleteSubmit()
    {
        RequireMode(FormMode.Submitting, "complete submission");
        foreach ((string name, string value) in values)
        {
            baseline[name] = value;
        }

        errors.Clear();
        Mode = FormMode.Browse;
    }

    public void FailSubmit(string message)
    {
        RequireMode(FormMode.Submitting, "fail submission");
        errors[string.Empty] = string.IsNullOrWhiteSpace(message) ? "submission failed" : message;
        Mode = FormMode.Edit;
    }

    public void CancelEdit()
    {
        RequireMode(FormMode.Edit, "cancel editing");
        values.Clear();
        foreach ((string name, string value) in baseline)
        {
            values[name] = value;
        }

        errors.Clear();
        Mode = FormMode.Browse;
    }

    private void RequireMode(FormMode required, string operation)
    {
        if (Mode != required)
        {
            throw new TuiLifecycleException(
                $"cannot {operation} while form is {Mode.ToString().ToLowerInvariant()}");
        }
    }
}
