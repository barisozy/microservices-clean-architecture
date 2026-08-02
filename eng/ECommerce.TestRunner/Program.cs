using System.Diagnostics;

var options = TestOptions.Parse(args);
var repositoryRoot = FindRepositoryRoot();
var testResultsRoot = Path.Combine(repositoryRoot, "artifacts", "test-results");

foreach (var suite in options.Suites)
    await RunSuiteAsync(suite, options);

if (options.Coverage)
    await CreateCoverageReportAsync(options);

return;

async Task RunSuiteAsync(TestSuite suite, TestOptions testOptions)
{
    var resultDirectory = Path.Combine(testResultsRoot, suite.Name.ToLowerInvariant());
    CleanDirectory(resultDirectory);

    var arguments = new List<string>
    {
        "test",
        Path.Combine(repositoryRoot, suite.SolutionFilter),
        "--configuration", testOptions.Configuration,
        "--nologo",
        "--disable-build-servers",
        "--logger", $"trx;LogFilePrefix={suite.Name.ToLowerInvariant()}",
        "--results-directory", resultDirectory
    };

    if (testOptions.NoRestore)
        arguments.Add("--no-restore");

    if (suite.CollectCoverage)
    {
        arguments.Add("--settings");
        arguments.Add(Path.Combine(repositoryRoot, "coverage.runsettings"));
        arguments.Add("--collect");
        arguments.Add("XPlat Code Coverage");
    }

    Console.WriteLine($"Running {suite.Name} tests...");
    await RunProcessAsync("dotnet", arguments, repositoryRoot);
}

async Task CreateCoverageReportAsync(TestOptions testOptions)
{
    var coverageResults = Path.Combine(testResultsRoot, "coverage");
    var reportDirectory = Path.Combine(repositoryRoot, "artifacts", "coverage", "local");
    CleanDirectory(reportDirectory);

    await RunProcessAsync("dotnet", ["tool", "restore"], repositoryRoot);
    await RunProcessAsync("dotnet",
    [
        "tool", "run", "reportgenerator",
        $"-reports:{Path.Combine(coverageResults, "**", "coverage.cobertura.xml")}",
        $"-targetdir:{reportDirectory}",
        "-reporttypes:Html;TextSummary",
        "-assemblyfilters:+*.Domain;+*.Application;+ECommerce.Audit"
    ], repositoryRoot);

    var summaryPath = Path.Combine(reportDirectory, "Summary.txt");
    if (File.Exists(summaryPath))
        Console.WriteLine(await File.ReadAllTextAsync(summaryPath));

    var reportPath = Path.Combine(reportDirectory, "index.html");
    Console.WriteLine($"Coverage report: {reportPath}");

    if (testOptions.OpenReport && File.Exists(reportPath))
        Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
}

static async Task RunProcessAsync(string fileName, IEnumerable<string> arguments, string workingDirectory)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false
    };

    foreach (var argument in arguments)
        startInfo.ArgumentList.Add(argument);

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"Could not start '{fileName}'.");
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
        throw new InvalidOperationException($"'{fileName}' exited with code {process.ExitCode}.");
}

static void CleanDirectory(string directory)
{
    var repositoryRoot = FindRepositoryRoot();
    var artifactsRoot = Path.GetFullPath(Path.Combine(repositoryRoot, "artifacts")) + Path.DirectorySeparatorChar;
    var resolvedDirectory = Path.GetFullPath(directory);
    if (!resolvedDirectory.StartsWith(artifactsRoot, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Refusing to delete a directory outside artifacts: {resolvedDirectory}");

    if (Directory.Exists(resolvedDirectory))
        Directory.Delete(resolvedDirectory, recursive: true);
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "ECommerce.sln")))
            return directory.FullName;
    }

    throw new InvalidOperationException("Could not locate the repository root containing ECommerce.sln.");
}

enum SuiteSelection
{
    Unit,
    Integration,
    Contract,
    All,
    Coverage
}

sealed record TestSuite(string Name, string SolutionFilter, bool CollectCoverage = false);

sealed record TestOptions(SuiteSelection Suite, string Configuration, bool NoRestore, bool OpenReport)
{
    public bool Coverage => Suite is SuiteSelection.Coverage;

    public IReadOnlyList<TestSuite> Suites => Suite switch
    {
        SuiteSelection.Unit => [new("Unit", "ECommerce.UnitTests.slnf")],
        SuiteSelection.Integration => [new("Integration", "ECommerce.IntegrationTests.slnf")],
        SuiteSelection.Contract => [new("Contract", "ECommerce.ContractTests.slnf")],
        SuiteSelection.All =>
        [
            new("Unit", "ECommerce.UnitTests.slnf"),
            new("Integration", "ECommerce.IntegrationTests.slnf"),
            new("Contract", "ECommerce.ContractTests.slnf")
        ],
        SuiteSelection.Coverage => [new("Coverage", "ECommerce.UnitTests.slnf", CollectCoverage: true)],
        _ => throw new ArgumentOutOfRangeException()
    };

    public static TestOptions Parse(string[] args)
    {
        var suite = SuiteSelection.Unit;
        var configuration = "Release";
        var noRestore = false;
        var openReport = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--suite" when index + 1 < args.Length:
                    if (!Enum.TryParse<SuiteSelection>(args[++index], ignoreCase: true, out suite))
                        throw new ArgumentException("--suite must be Unit, Integration, Contract, All, or Coverage.");
                    break;
                case "--configuration" when index + 1 < args.Length:
                    configuration = args[++index];
                    if (configuration is not ("Debug" or "Release"))
                        throw new ArgumentException("--configuration must be Debug or Release.");
                    break;
                case "--no-restore": noRestore = true; break;
                case "--open-report": openReport = true; break;
                case "--help" or "-h":
                    Console.WriteLine("Usage: dotnet run --project eng/ECommerce.TestRunner -- [--suite Unit|Integration|Contract|All|Coverage] [--configuration Debug|Release] [--no-restore] [--open-report]");
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[index]}");
            }
        }

        return new TestOptions(suite, configuration, noRestore, openReport);
    }
}
