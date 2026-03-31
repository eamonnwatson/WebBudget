using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using PredictiveBudget.Web.Styling;

namespace PredictiveBudget.Web.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ThemeState ThemeState { get; set; } = default!;

    private string RelativePath
        => NavigationManager.ToBaseRelativePath(NavigationManager.Uri).Trim('/');

    private bool IsPlanRoute
        => RelativePath.StartsWith("plans/", StringComparison.OrdinalIgnoreCase);

    private string CurrentPlanHref
        => IsPlanRoute ? $"/{RelativePath}" : "/";

    private string ThemeToggleIcon
        => ThemeState.IsDarkMode ? Icons.Material.Filled.LightMode : Icons.Material.Filled.DarkMode;

    private string ThemeToggleTooltip
        => ThemeState.IsDarkMode ? "Switch to light mode" : "Switch to dark mode";

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += HandleLocationChanged;
        ThemeState.Changed += HandleThemeChanged;
    }

    private static string GetNavPillClass(bool isActive)
        => isActive ? "nav-pill nav-pill-active" : "nav-pill";

    private void ToggleTheme()
        => ThemeState.Toggle();

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
        => _ = InvokeAsync(StateHasChanged);

    private void HandleThemeChanged()
        => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        NavigationManager.LocationChanged -= HandleLocationChanged;
        ThemeState.Changed -= HandleThemeChanged;
    }
}
