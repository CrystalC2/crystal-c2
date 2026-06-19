using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Client;

[RequiresUnreferencedCode("Default implementation of ViewLocator involves reflection which may be trimmed away.", Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
internal sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is not null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = $"Control '{name}' not found" };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}