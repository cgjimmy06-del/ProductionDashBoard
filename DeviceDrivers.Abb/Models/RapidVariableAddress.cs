namespace DeviceDrivers.Abb;

/// <summary>
/// RAPID 變數位址：以 Task / Module / Variable 三層定位控制器內的一個變數。
/// </summary>
public sealed record RapidVariableAddress
{
    /// <summary>建立位址，三個欄位皆不可為空白。</summary>
    /// <exception cref="ArgumentException">任一欄位為 null 或空白。</exception>
    public RapidVariableAddress(string task, string module, string variable)
    {
        Task = NotBlank(task, nameof(task));
        Module = NotBlank(module, nameof(module));
        Variable = NotBlank(variable, nameof(variable));
    }

    /// <summary>RAPID Task 名稱。</summary>
    public string Task { get; }

    /// <summary>RAPID Module 名稱。</summary>
    public string Module { get; }

    /// <summary>RAPID 變數名稱。</summary>
    public string Variable { get; }

    private static string NotBlank(string value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("RAPID 變數位址欄位不可為空白。", paramName)
            : value;

    /// <inheritdoc/>
    public override string ToString() => $"{Task}/{Module}/{Variable}";
}
