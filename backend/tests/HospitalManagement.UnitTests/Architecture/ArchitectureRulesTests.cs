namespace HospitalManagement.UnitTests.Architecture;

/// <summary>
/// Architecture rules that a code review could miss. They read the source, so they fail the build the moment a
/// rule is broken, before any behavior test notices (ARCHITECTURE.md §1 and §3).
/// </summary>
public class ArchitectureRulesTests
{
    private static readonly DirectoryInfo Backend = FindBackend();

    /// <summary>ARCHITECTURE D-9: which patient's data a caller may reach is decided only by IPatientAccessPolicy.</summary>
    [Theory]
    [InlineData("GetCurrentPatientIdAsync")]
    [InlineData("IsInRole(")]
    [InlineData("IsStaff")]
    [Trait("Risk", "R-001")]
    [Trait("Risk", "R-002")]
    [Trait("Risk", "R-003")]
    public void Controllers_DoNotDecideOwnershipThemselves(string forbidden)
    {
        var offenders = SourceFiles("api/Controllers", "api/Infrastructure")
            .Where(f => File.ReadAllText(f.FullName).Contains(forbidden))
            .Select(f => f.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"'{forbidden}' used in {string.Join(", ", offenders)}: ask IPatientAccessPolicy instead.");
    }

    /// <summary>ARCHITECTURE D-16: services return DTOs, never EF entities.</summary>
    [Fact]
    [Trait("Risk", "R-054")]
    public void ServiceInterfaces_DoNotExposeEntities()
    {
        var entityNames = SourceFiles("Domain/Entities")
            .Select(f => Path.GetFileNameWithoutExtension(f.Name))
            .ToArray();
        var offenders = SourceFiles("Services")
            .Where(f => f.Name.StartsWith("I") && f.Name.EndsWith("Service.cs"))
            .SelectMany(f => File.ReadAllLines(f.FullName)
                .Where(line => line.TrimStart().StartsWith("Task<"))
                .Where(line => entityNames.Any(e => line.Contains($"<{e}>") || line.Contains($"<{e}?>")
                                                     || line.Contains($"<IEnumerable<{e}>>")))
                .Select(line => $"{f.Name}: {line.Trim()}"))
            .Where(line => !line.Contains("INotificationService"))
            .ToList();

        Assert.True(offenders.Count == 0, "Entities in service contracts:\n" + string.Join("\n", offenders));
    }

    private static IEnumerable<FileInfo> SourceFiles(params string[] folders) =>
        folders.SelectMany(folder => new DirectoryInfo(Path.Combine(Backend.FullName, folder))
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")));

    private static DirectoryInfo FindBackend()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "backend.sln"))) return dir;
        throw new InvalidOperationException("backend.sln not found above " + AppContext.BaseDirectory);
    }
}
