namespace PredictiveBudget.Web.Styling;

/// <summary>
/// Tracks the current light or dark theme mode for the active UI session.
/// </summary>
public sealed class ThemeState
{
    private bool isDarkMode = true;

    public bool IsDarkMode => isDarkMode;

    public event Action? Changed;

    public void Toggle()
        => SetDarkMode(!isDarkMode);

    public void SetDarkMode(bool value)
    {
        if (isDarkMode == value)
        {
            return;
        }

        isDarkMode = value;
        Changed?.Invoke();
    }
}
