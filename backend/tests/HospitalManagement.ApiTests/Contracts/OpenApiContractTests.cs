using System.Text.Json;
using System.Text.Json.Nodes;
using HospitalManagement.ApiTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Swashbuckle.AspNetCore.Swagger;

namespace HospitalManagement.ApiTests.Contracts;

/// <summary>
/// R-061: the web frontend and the API must agree on routes, methods and fields. The API's OpenAPI description is
/// committed as <c>contracts/openapi.json</c>; the frontend component tests check every request they send and
/// every response they mock against that file. This test keeps the file equal to what the API really offers, so
/// a change to the API shows up in the pull request as a change to the contract, and the frontend tests then hold
/// the frontend to it.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Contracts")]
public class OpenApiContractTests
{
    private const string UpdateVariable = "UPDATE_OPENAPI_CONTRACT";
    private readonly ApiFactory _factory;

    public OpenApiContractTests(ApiFixture fixture) => _factory = fixture.Factory;

    [Fact]
    [Trait("Risk", "R-061")]
    public void OpenApiContract_MatchesTheCommittedFile()
    {
        var generated = Normalize(_factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1")
            .SerializeAsJson(OpenApiSpecVersion.OpenApi3_0));
        var path = Path.Combine(RepositoryRoot(), "contracts", "openapi.json");

        // Update mode never passes on a file it has just rewritten: a run with the variable set by mistake (in CI, say)
        // fails instead of silently accepting any contract (assertion rule 15). An unchanged file is compared below.
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1"
            && (!File.Exists(path) || File.ReadAllText(path).Replace("\r\n", "\n") != generated))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, generated);
            Assert.Fail($"{path} was updated. Review and commit it, then run the test again without {UpdateVariable}.");
        }

        Assert.True(File.Exists(path), $"{path} is missing. Create it: {UpdateVariable}=1 dotnet test --filter OpenApiContract");
        var committed = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.True(committed == generated,
            "The API's OpenAPI contract differs from contracts/openapi.json. If the API change is intended, update the " +
            $"file with `{UpdateVariable}=1 dotnet test --filter OpenApiContract` (in backend/tests/HospitalManagement.ApiTests), " +
            "commit it, and run the frontend component tests (npm test in frontend-web): they check the frontend against it.");
    }

    /// <summary>
    /// The contract without what depends on where the API runs: the Keycloak sign-in scheme carries the identity
    /// provider's URL, which differs between environments and is not part of what the frontend calls.
    /// </summary>
    private static string Normalize(string json)
    {
        var document = JsonNode.Parse(json)!.AsObject();
        document.Remove("security");
        document.Remove("servers");
        if (document["components"] is JsonObject components)
            components.Remove("securitySchemes");
        foreach (var (_, pathItem) in document["paths"]!.AsObject())
            foreach (var (_, operation) in pathItem!.AsObject())
                (operation as JsonObject)?.Remove("security");
        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
                return directory.FullName;
        throw new InvalidOperationException("Repository root (global.json) not found above " + AppContext.BaseDirectory);
    }
}
