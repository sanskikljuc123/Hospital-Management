using Microsoft.Playwright;
using Reqnroll;
using Reqnroll.BoDi;

namespace HospitalManagement.E2E.Support;

/// <summary>Starts one browser per test run and one isolated context per scenario; keeps a trace of failures.</summary>
[Binding]
public sealed class Hooks
{
    private static IPlaywright? _playwright;
    private static IBrowser? _browser;

    private readonly IObjectContainer _container;
    private readonly ScenarioContext _scenario;
    private IBrowserContext? _context;

    public Hooks(IObjectContainer container, ScenarioContext scenario)
    {
        _container = container;
        _scenario = scenario;
    }

    [BeforeTestRun]
    public static async Task StartBrowserAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = !E2EConfig.Headed });
    }

    [AfterTestRun]
    public static async Task StopBrowserAsync()
    {
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [BeforeScenario]
    public async Task OpenContextAsync()
    {
        // A tag such as @timezone:Europe/Sarajevo (on a scenario or an Examples block) runs the browser in that
        // time zone, whatever the machine's zone is; dates in the UI depend on it (R-060).
        var timezone = _scenario.ScenarioInfo.CombinedTags
            .FirstOrDefault(t => t.StartsWith("timezone:", StringComparison.Ordinal))?["timezone:".Length..];
        _context = await _browser!.NewContextAsync(new() { TimezoneId = timezone });
        _context.SetDefaultTimeout(15_000);
        await _context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        _container.RegisterInstanceAs(new BrowserSession(await _context.NewPageAsync()));
    }

    [AfterScenario]
    public async Task CloseContextAsync()
    {
        if (_context == null) return;
        // A Playwright trace for every failed scenario (QUALITY_GATES G7 evidence); open with "playwright show-trace".
        var failed = _scenario.TestError != null;
        var path = failed
            ? Path.Combine("TestResults", "traces", $"{Sanitize(_scenario.ScenarioInfo.Title)}.zip")
            : null;
        await _context.Tracing.StopAsync(new() { Path = path });
        await _context.DisposeAsync();
    }

    private static string Sanitize(string title) =>
        string.Concat(title.Select(c => char.IsLetterOrDigit(c) ? c : '-'));
}
