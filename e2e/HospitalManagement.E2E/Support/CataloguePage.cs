using Microsoft.Playwright;

namespace HospitalManagement.E2E.Support;

/// <summary>
/// One of the catalogue pages of the web application (Doctors, Patients, Services): a table with View, Edit and
/// Delete per row and a drawer with the form. Locators use roles, accessible names and form labels only — no CSS
/// classes or positions (UI_TEST_DESIGN §8).
/// </summary>
public sealed class CataloguePage
{
    private readonly IPage _page;
    private readonly string _path;
    private readonly string _apiResource;

    /// <param name="path">The frontend route, e.g. "/doctor".</param>
    /// <param name="apiResource">The API path the page reads its list from, e.g. "/api/doctor" (any casing).</param>
    public CataloguePage(IPage page, string path, string apiResource)
    {
        _page = page;
        _path = path;
        _apiResource = apiResource;
    }

    public static CataloguePage Doctors(IPage page) => new(page, "/doctor", "/api/doctor");
    public static CataloguePage Patients(IPage page) => new(page, "/patient", "/api/patient");
    public static CataloguePage Services(IPage page) => new(page, "/service", "/api/service");

    /// <summary>Opens the page and waits until its list has arrived from the API.</summary>
    public async Task OpenAsync() =>
        await _page.RunAndWaitForResponseAsync(
            () => _page.GotoAsync(E2EConfig.FrontendUrl + _path),
            r => IsResource(r.Url) && r.Request.Method == "GET" && r.Ok);

    /// <summary>The table row that shows <paramref name="text"/>.</summary>
    public ILocator Row(string text) => _page.GetByRole(AriaRole.Row).Filter(new() { HasText = text });

    public Task EditAsync(string rowText) =>
        Row(rowText).GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();

    public Task ClickAsync(string buttonName) =>
        _page.GetByRole(AriaRole.Button, new() { Name = buttonName, Exact = true }).ClickAsync();

    /// <summary>A field of the open form, found by its label.</summary>
    public ILocator Field(string label) => _page.GetByLabel(label, new() { Exact = true });

    /// <summary>Types a date into an antd date picker and confirms it.</summary>
    public async Task PickDateAsync(string label, string isoDate)
    {
        await Field(label).FillAsync(isoDate);
        await Field(label).PressAsync("Enter");
    }

    /// <summary>
    /// Selects an option of an antd select by clicking it by its title (antd puts the option text in
    /// <c>title</c>). It does not type to filter: the selects filter on the option value (the id), so typing a
    /// name finds nothing (UI_TEST_DESIGN D-17). The list is virtualized, so this works while the option is among
    /// the first rendered ones; tests create few services. Escape then closes the list, not the drawer, because
    /// focus is in the select.
    /// </summary>
    public async Task SelectAsync(string label, string optionText)
    {
        await Field(label).ClickAsync();
        await _page.GetByTitle(optionText, new() { Exact = true }).ClickAsync();
        await _page.Keyboard.PressAsync("Escape");
    }

    /// <summary>
    /// Clicks the form's submit button and returns the API's answer to the write it sends. The page closes the
    /// drawer before the request finishes, so the response — not the drawer — tells whether the save worked.
    /// </summary>
    public async Task<IResponse> SubmitAsync(string buttonName) =>
        await _page.RunAndWaitForResponseAsync(
            () => ClickAsync(buttonName),
            r => IsResource(r.Url) && r.Request.Method != "GET");

    private bool IsResource(string url) =>
        new Uri(url).AbsolutePath.StartsWith(_apiResource, StringComparison.OrdinalIgnoreCase);
}
