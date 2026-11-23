using System.Net;
using System.Text.Json;
using HospitalManagement.E2E.Support;
using Microsoft.Playwright;
using Reqnroll;

namespace HospitalManagement.E2E.Steps;

/// <summary>
/// Edits in the web application compared with what the API stored (R-059, R-060). The browser does the edit; the
/// administrator's real Keycloak token, taken from that browser, prepares and checks the data. Everything the
/// scenario creates is deleted afterwards.
/// </summary>
[Binding]
public sealed class CatalogueEditingSteps : IDisposable
{
    private readonly BrowserSession _browser;
    private readonly List<string> _createdPaths = new(); // deleted in reverse order after the scenario
    private readonly string _tag = Guid.NewGuid().ToString("N")[..8];
    private HospitalApi? _admin;

    private int _departmentId, _serviceId, _doctorId, _patientId;
    private JsonElement _before;
    private string _newValue = "";
    private IResponse? _lastWrite;

    public CatalogueEditingSteps(BrowserSession browser) => _browser = browser;

    private HospitalApi Admin => _admin ?? throw new InvalidOperationException("The administrator is not signed in.");
    private string LastName => $"E2E-{_tag}";
    private string ServiceName => $"E2E service {_tag}";

    [Given("the administrator works in the web application")]
    public async Task AdministratorSignedIn()
    {
        await _browser.SignInFromStartAsync("admin.user", E2EConfig.UsersPassword);
        _admin = new HospitalApi(await _browser.CaptureAccessTokenAsync());
    }

    // ---------- Data prepared through the API ----------

    [Given("a service with a department")]
    public async Task ServiceWithDepartment()
    {
        _departmentId = await CreateAsync("/api/Department", new { name = $"E2E department {_tag}", description = "E2E" });
        _serviceId = await CreateAsync("/api/Service", new { name = ServiceName, description = "E2E", departmentId = _departmentId });
        _before = await GetAsync($"/api/Service/{_serviceId}");
    }

    [Given("a doctor born on {string} with a department and a service")]
    public async Task DoctorWithDepartmentAndService(string birthday)
    {
        await ServiceWithDepartment();
        _doctorId = await CreateAsync("/api/Doctor", new
        {
            firstName = "Before", lastName = LastName, address = "1 Test Street", birthday,
            departmentId = _departmentId, serviceIds = new[] { _serviceId },
        });
        _before = await GetAsync($"/api/Doctor/{_doctorId}");
    }

    [Given("a patient with an email address")]
    public async Task PatientWithEmail()
    {
        await ServiceWithDepartment();
        _patientId = await CreateAsync("/api/Patient", new
        {
            userName = $"E2E Patient {_tag}", phone = 10000000, email = $"e2e.{_tag}@hospital.test", serviceId = _serviceId,
        });
        _before = await GetAsync($"/api/Patient/{_patientId}");
    }

    // ---------- Actions in the web application ----------

    [When("the administrator renames the doctor in the web application")]
    public async Task RenameDoctor()
    {
        var page = CataloguePage.Doctors(_browser.Page);
        await page.OpenAsync();
        await page.EditAsync(LastName);
        _newValue = $"After{_tag}";
        await page.Field("Doctor First Name").FillAsync(_newValue);
        _lastWrite = await page.SubmitAsync("Update Doctor");
    }

    [When("the administrator renames the patient in the web application")]
    public async Task RenamePatient()
    {
        var page = CataloguePage.Patients(_browser.Page);
        await page.OpenAsync();
        await page.EditAsync($"E2E Patient {_tag}");
        _newValue = $"E2E Patient {_tag} renamed";
        await page.Field("Patient Name").FillAsync(_newValue);
        _lastWrite = await page.SubmitAsync("Update Patient");
    }

    [When("the administrator changes the service's description in the web application")]
    public async Task ChangeServiceDescription()
    {
        var page = CataloguePage.Services(_browser.Page);
        await page.OpenAsync();
        await page.EditAsync(ServiceName);
        _newValue = $"Changed {_tag}";
        await page.Field("Description").FillAsync(_newValue);
        _lastWrite = await page.SubmitAsync("Update Service");
    }

    [When("the administrator adds a doctor born on {string} in the web application")]
    public async Task AddDoctor(string birthday)
    {
        var page = CataloguePage.Doctors(_browser.Page);
        await page.OpenAsync();
        await page.ClickAsync("Add a new Doctor");
        await page.Field("Doctor First Name").FillAsync("New");
        await page.Field("Doctor Last Name").FillAsync(LastName);
        await page.Field("Doctor Address").FillAsync("2 Test Street");
        await page.PickDateAsync("Doctor Birthday", birthday);
        await page.SelectAsync("Select Service", ServiceName);
        _lastWrite = await page.SubmitAsync("Create Doctor");
        if (_lastWrite.Status == (int)HttpStatusCode.Created)
        {
            _doctorId = (await _lastWrite.JsonAsync())!.Value.GetProperty("id").GetInt32();
            _createdPaths.Add($"/api/Doctor/{_doctorId}");
        }
    }

    // ---------- Outcomes ----------

    [Then("the web application saves the change with PUT")]
    public void SavedWithPut()
    {
        Assert.NotNull(_lastWrite);
        Assert.Equal("PUT", _lastWrite!.Request.Method);
        Assert.Equal((int)HttpStatusCode.OK, _lastWrite.Status);
    }

    [Then("the web application saves the new record")]
    public void SavedNewRecord()
    {
        Assert.NotNull(_lastWrite);
        Assert.Equal((int)HttpStatusCode.Created, _lastWrite!.Status);
    }

    [Then("the list shows the doctor's new name")]
    public Task DoctorListUpdated() =>
        Assertions.Expect(CataloguePage.Doctors(_browser.Page).Row(LastName)).ToContainTextAsync(_newValue);

    [Then("the list shows the patient's new name")]
    public Task PatientListUpdated() =>
        Assertions.Expect(CataloguePage.Patients(_browser.Page).Row(_newValue)).ToBeVisibleAsync();

    [Then("the doctor's birthday, department and services are unchanged")]
    public async Task DoctorUnchanged()
    {
        var after = await GetAsync($"/api/Doctor/{_doctorId}");
        Assert.Equal(_newValue, after.GetProperty("firstName").GetString());
        Assert.Equal(_before.GetProperty("birthday").GetString(), after.GetProperty("birthday").GetString());
        Assert.Equal(_departmentId.ToString(), after.GetProperty("departmentId").GetRawText());
        Assert.Equal(new[] { _serviceId }, after.GetProperty("serviceIds").EnumerateArray().Select(s => s.GetInt32()));
        Assert.Equal(_before.GetProperty("address").GetString(), after.GetProperty("address").GetString());
    }

    [Then("the patient's email address, phone and service are unchanged")]
    public async Task PatientUnchanged()
    {
        var after = await GetAsync($"/api/Patient/{_patientId}");
        Assert.Equal(_newValue, after.GetProperty("userName").GetString());
        Assert.Equal(_before.GetProperty("email").GetString(), after.GetProperty("email").GetString());
        Assert.Equal(_before.GetProperty("phone").GetInt32(), after.GetProperty("phone").GetInt32());
        Assert.Equal(_serviceId, after.GetProperty("serviceId").GetInt32());
    }

    [Then("the service's new description is stored and its department is unchanged")]
    public async Task ServiceUnchanged()
    {
        var after = await GetAsync($"/api/Service/{_serviceId}");
        Assert.Equal(_newValue, after.GetProperty("description").GetString());
        Assert.Equal(_before.GetProperty("name").GetString(), after.GetProperty("name").GetString());
        Assert.Equal(_departmentId.ToString(), after.GetProperty("departmentId").GetRawText());
    }

    [Then("the new doctor's stored birthday is {string}")]
    public async Task StoredBirthday(string birthday)
    {
        var doctor = await GetAsync($"/api/Doctor/{_doctorId}");
        Assert.Equal($"{birthday}T00:00:00Z", doctor.GetProperty("birthday").GetString());
    }

    [Then("editing the new doctor shows the birthday {string}")]
    public async Task EditShowsBirthday(string birthday)
    {
        var page = CataloguePage.Doctors(_browser.Page);
        await page.OpenAsync();
        await page.EditAsync(LastName);
        await Assertions.Expect(page.Field("Doctor Birthday")).ToHaveValueAsync(birthday);
    }

    // ---------- Helpers ----------

    private async Task<int> CreateAsync(string path, object body)
    {
        var (status, created) = await Admin.PostAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, status);
        var id = created.GetProperty("id").GetInt32();
        _createdPaths.Add($"{path}/{id}");
        return id;
    }

    private async Task<JsonElement> GetAsync(string path)
    {
        var (status, body) = await Admin.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, status);
        return body;
    }

    [AfterScenario]
    public async Task DeleteCreatedDataAsync()
    {
        if (_admin == null) return;
        // Doctors and patients first, then services, then departments: the API refuses (409) to delete a
        // service or department that is still used.
        var order = new[] { "/api/Doctor/", "/api/Patient/", "/api/Service/", "/api/Department/" };
        foreach (var path in _createdPaths.OrderBy(p => Array.FindIndex(order, o => p.StartsWith(o, StringComparison.Ordinal))))
            await _admin.DeleteAsync(path);
    }

    public void Dispose() => _admin?.Dispose();
}
