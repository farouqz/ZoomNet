// Install tools.
#tool dotnet:?package=GitVersion.Tool&version=5.6.6
#tool nuget:?package=GitReleaseManager&version=0.11.0
#tool nuget:?package=OpenCover&version=4.7.1204
#tool nuget:?package=ReportGenerator&version=4.8.9
#tool nuget:?package=coveralls.io&version=1.4.2
#tool nuget:?package=xunit.runner.console&version=2.4.1

// Install addins.
#addin nuget:?package=Cake.Coveralls&version=1.0.1
#addin nuget:?package=Cake.Git&version=1.0.1


///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

if (IsRunningOnUnix()) target = "Run-Unit-Tests";


///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var libraryName = "ZoomNet";
var gitHubRepo = "ZoomNet";

var testCoverageFilter = "+[ZoomNet]* -[ZoomNet]ZoomNet.Properties.* -[ZoomNet]ZoomNet.Models.*";
var testCoverageExcludeByAttribute = "*.ExcludeFromCodeCoverage*";
var testCoverageExcludeByFile = "*/*Designer.cs;*/*AssemblyInfo.cs";

var nuGetApiUrl = Argument<string>("NUGET_API_URL", EnvironmentVariable("NUGET_API_URL"));
var nuGetApiKey = Argument<string>("NUGET_API_KEY", EnvironmentVariable("NUGET_API_KEY"));

var myGetApiUrl = Argument<string>("MYGET_API_URL", EnvironmentVariable("MYGET_API_URL"));
var myGetApiKey = Argument<string>("MYGET_API_KEY", EnvironmentVariable("MYGET_API_KEY"));

var gitHubToken = Argument<string>("GITHUB_TOKEN", EnvironmentVariable("GITHUB_TOKEN"));
var gitHubUserName = Argument<string>("GITHUB_USERNAME", EnvironmentVariable("GITHUB_USERNAME"));
var gitHubPassword = Argument<string>("GITHUB_PASSWORD", EnvironmentVariable("GITHUB_PASSWORD"));
var gitHubRepoOwner = Argument<string>("GITHUB_REPOOWNER", EnvironmentVariable("GITHUB_REPOOWNER") ?? gitHubUserName);

var sourceFolder = "./Source/";
var outputDir = "./artifacts/";
var codeCoverageDir = $"{outputDir}CodeCoverage/";
var benchmarkDir = $"{outputDir}Benchmark/";

var solutionFile = $"{sourceFolder}{libraryName}.sln";
var sourceProject = $"{sourceFolder}{libraryName}/{libraryName}.csproj";
var integrationTestsProject = $"{sourceFolder}{libraryName}.IntegrationTests/{libraryName}.IntegrationTests.csproj";
var unitTestsProject = $"{sourceFolder}{libraryName}.UnitTests/{libraryName}.UnitTests.csproj";
var benchmarkProject = $"{sourceFolder}{libraryName}.Benchmark/{libraryName}.Benchmark.csproj";

var versionInfo = GitVersion(new GitVersionSettings() { OutputType = GitVersionOutput.Json });
var milestone = versionInfo.MajorMinorPatch;
var cakeVersion = typeof(ICakeContext).Assembly.GetName().Version.ToString();
var isLocalBuild = BuildSystem.IsLocalBuild;
var isMainBranch = StringComparer.OrdinalIgnoreCase.Equals("main", BuildSystem.AppVeyor.Environment.Repository.Branch);
var isMainRepo = StringComparer.OrdinalIgnoreCase.Equals($"{gitHubRepoOwner}/{gitHubRepo}", BuildSystem.AppVeyor.Environment.Repository.Name);
var isPullRequest = BuildSystem.AppVeyor.Environment.PullRequest.IsPullRequest;
var isTagged = (
	BuildSystem.AppVeyor.Environment.Repository.Tag.IsTag &&
	!string.IsNullOrWhiteSpace(BuildSystem.AppVeyor.Environment.Repository.Tag.Name)
);
var isBenchmarkPresent = FileExists(benchmarkProject);


///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
	if (isMainBranch && (context.Log.Verbosity != Verbosity.Diagnostic))
	{
		Information("Increasing verbosity to diagnostic.");
		context.Log.Verbosity = Verbosity.Diagnostic;
	}

	Information("Building version {0} of {1} ({2}, {3}) using version {4} of Cake",
		versionInfo.LegacySemVerPadded,
		libraryName,
		configuration,
		target,
		cakeVersion
	);

	Information("Variables:\r\n\tLocalBuild: {0}\r\n\tIsMainBranch: {1}\r\n\tIsMainRepo: {2}\r\n\tIsPullRequest: {3}\r\n\tIsTagged: {4}",
		isLocalBuild,
		isMainBranch,
		isMainRepo,
		isPullRequest,
		isTagged
	);

	Information("Myget Info:\r\n\tApi Url: {0}\r\n\tApi Key: {1}",
		myGetApiUrl,
		string.IsNullOrEmpty(myGetApiKey) ? "[NULL]" : new string('*', myGetApiKey.Length)
	);

	Information("Nuget Info:\r\n\tApi Url: {0}\r\n\tApi Key: {1}",
		nuGetApiUrl,
		string.IsNullOrEmpty(nuGetApiKey) ? "[NULL]" : new string('*', nuGetApiKey.Length)
	);

	if (!string.IsNullOrEmpty(gitHubToken))
	{
		Information("GitHub Info:\r\n\tRepo: {0}\r\n\tUserName: {1}\r\n\tToken: {2}",
			$"{gitHubRepoOwner}/{gitHubRepo}",
			gitHubUserName,
			new string('*', gitHubToken.Length)
		);
	}
	else
	{
		Information("GitHub Info:\r\n\tRepo: {0}\r\n\tUserName: {1}\r\n\tPassword: {2}",
			$"{gitHubRepoOwner}/{gitHubRepo}",
			gitHubUserName,
			string.IsNullOrEmpty(gitHubPassword) ? "[NULL]" : new string('*', gitHubPassword.Length)
		);
	}

	// Integration tests are intended to be used for debugging purposes and not intended to be executed in CI environment.
	// Also, the runner for these tests contains windows-specific code (such as resizing window, moving window to center of screen, etc.)
	// which can cause problems when attempting to run unit tests on an Ubuntu image on AppVeyor.
	if (!isLocalBuild)
	{
		Information("");
		Information("Removing integration tests");
		DotNetCoreTool(solutionFile, "sln", $"remove {integrationTestsProject.TrimStart(sourceFolder, StringComparison.OrdinalIgnoreCase)}");
	}
});

Teardown(context =>
{
	if (!isLocalBuild)
	{
		Information("Restoring integration tests");
		GitCheckout(".", new FilePath[] { solutionFile });
		Information("");
	}

	// Executed AFTER the last task.
	Information("Finished running tasks.");
});


///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("AppVeyor-Build_Number")
	.WithCriteria(() => AppVeyor.IsRunningOnAppVeyor)
	.Does(() =>
{
	GitVersion(new GitVersionSettings()
	{
		UpdateAssemblyInfo = false,
		OutputType = GitVersionOutput.BuildServer
	});
});

Task("Clean")
	.IsDependentOn("AppVeyor-Build_Number")
	.Does(() =>
{
	// Clean solution directories.
	Information("Cleaning {0}", sourceFolder);
	CleanDirectories($"{sourceFolder}*/bin/{configuration}");
	CleanDirectories($"{sourceFolder}*/obj/{configuration}");

	// Clean previous artifacts
	Information("Cleaning {0}", outputDir);
	if (DirectoryExists(outputDir)) CleanDirectories(MakeAbsolute(Directory(outputDir)).FullPath);
	else CreateDirectory(outputDir);

	// Create folder for code coverage report
	CreateDirectory(codeCoverageDir);
});

Task("Restore-NuGet-Packages")
	.IsDependentOn("Clean")
	.Does(() =>
{
	DotNetCoreRestore("./Source/", new DotNetCoreRestoreSettings
	{
		Sources = new [] {
			"https://api.nuget.org/v3/index.json",
		}
	});
});

Task("Build")
	.IsDependentOn("Restore-NuGet-Packages")
	.Does(() =>
{
	DotNetCoreBuild(solutionFile, new DotNetCoreBuildSettings
	{
		Configuration = configuration,
		NoRestore = true,
		ArgumentCustomization = args => args.Append("/p:SemVer=" + versionInfo.LegacySemVerPadded),
		Framework =  IsRunningOnWindows() ? null : "net5.0"
	});
});

Task("Run-Unit-Tests")
	.IsDependentOn("Build")
	.Does(() =>
{
	DotNetCoreTest(unitTestsProject, new DotNetCoreTestSettings
	{
		NoBuild = true,
		NoRestore = true,
		Configuration = configuration,
		Framework = IsRunningOnWindows() ? null : "net5.0"
	});
});

Task("Run-Code-Coverage")
	.IsDependentOn("Build")
	.Does(() =>
{
	// For the purpose of calculating code coverage, a single target will suffice.
	// FYI, this will cause an error if the unit test project is not configured
	// to target this desired framework:
	var desiredFramework = "net5.0";

	Action<ICakeContext> testAction = ctx => ctx.DotNetCoreTest(unitTestsProject, new DotNetCoreTestSettings
	{
		NoBuild = true,
		NoRestore = true,
		Configuration = configuration,
		Framework = desiredFramework
	});

	OpenCover(testAction,
		$"{codeCoverageDir}coverage.xml",
		new OpenCoverSettings
		{
			OldStyle = true,
			MergeOutput = true,
			ArgumentCustomization = args => args.Append("-returntargetcode")
		}
		.WithFilter(testCoverageFilter)
		.ExcludeByAttribute(testCoverageExcludeByAttribute)
		.ExcludeByFile(testCoverageExcludeByFile)
	);
});

Task("Upload-Coverage-Result")
	.IsDependentOn("Run-Code-Coverage")
	.Does(() =>
{
	CoverallsIo($"{codeCoverageDir}coverage.xml");
});

Task("Generate-Code-Coverage-Report")
	.IsDependentOn("Run-Code-Coverage")
	.Does(() =>
{
	ReportGenerator(
		new FilePath($"{codeCoverageDir}coverage.xml"),
		codeCoverageDir,
		new ReportGeneratorSettings() {
			ClassFilters = new[] { "*.UnitTests*" }
		}
	);
});

Task("Create-NuGet-Package")
	.IsDependentOn("Build")
	.Does(() =>
{
	var releaseNotesUrl = @$"https://github.com/{gitHubRepoOwner}/{gitHubRepo}/releases/tag/{milestone}";

	var settings = new DotNetCorePackSettings
	{
		Configuration = configuration,
		IncludeSource = false,
		IncludeSymbols = true,
		NoBuild = true,
		NoRestore = true,
		NoDependencies = true,
		OutputDirectory = outputDir,
		ArgumentCustomization = (args) =>
		{
			return args
				.Append("/p:SymbolPackageFormat=snupkg")
				.Append("/p:PackageReleaseNotes=\"{0}\"", releaseNotesUrl)
				.Append("/p:Version={0}", versionInfo.LegacySemVerPadded)
				.Append("/p:AssemblyVersion={0}", versionInfo.MajorMinorPatch)
				.Append("/p:FileVersion={0}", versionInfo.MajorMinorPatch)
				.Append("/p:AssemblyInformationalVersion={0}", versionInfo.InformationalVersion);
		}
	};

	DotNetCorePack(sourceProject, settings);
});

Task("Upload-AppVeyor-Artifacts")
	.WithCriteria(() => AppVeyor.IsRunningOnAppVeyor)
	.Does(() =>
{
	foreach (var file in GetFiles($"{outputDir}*.*"))
	{
		AppVeyor.UploadArtifact(file.FullPath);
	}
	foreach (var file in GetFiles($"{benchmarkDir}results/*.*"))
	{
		AppVeyor.UploadArtifact(file.FullPath);
	}
});

Task("Publish-NuGet")
	.IsDependentOn("Create-NuGet-Package")
	.WithCriteria(() => !isLocalBuild)
	.WithCriteria(() => !isPullRequest)
	.WithCriteria(() => isMainRepo)
	.WithCriteria(() => isMainBranch)
	.WithCriteria(() => isTagged)
	.Does(() =>
{
	if(string.IsNullOrEmpty(nuGetApiKey)) throw new InvalidOperationException("Could not resolve NuGet API key.");
	if(string.IsNullOrEmpty(nuGetApiUrl)) throw new InvalidOperationException("Could not resolve NuGet API url.");

	foreach(var package in GetFiles(outputDir + "*.nupkg"))
	{
		// Push the package.
		NuGetPush(package, new NuGetPushSettings {
			ApiKey = nuGetApiKey,
			Source = nuGetApiUrl
		});
	}
});

Task("Publish-MyGet")
	.IsDependentOn("Create-NuGet-Package")
	.WithCriteria(() => !isLocalBuild)
	.WithCriteria(() => !isPullRequest)
	.WithCriteria(() => isMainRepo)
	.Does(() =>
{
	if(string.IsNullOrEmpty(myGetApiKey)) throw new InvalidOperationException("Could not resolve MyGet API key.");
	if(string.IsNullOrEmpty(myGetApiUrl)) throw new InvalidOperationException("Could not resolve MyGet API url.");

	foreach(var package in GetFiles(outputDir + "*.nupkg"))
	{
		// Push the package.
		NuGetPush(package, new NuGetPushSettings {
			ApiKey = myGetApiKey,
			Source = myGetApiUrl
		});
	}
});

Task("Create-Release-Notes")
	.Does(() =>
{
	var settings = new GitReleaseManagerCreateSettings
	{
		Name              = milestone,
		Milestone         = milestone,
		Prerelease        = false,
		TargetCommitish   = "main"
	};

	if (string.IsNullOrEmpty(gitHubToken))
	{
		throw new InvalidOperationException("GitHub token was not provided.");
	}

	GitReleaseManagerCreate(gitHubToken, gitHubRepoOwner, gitHubRepo, settings);
});

Task("Publish-GitHub-Release")
	.WithCriteria(() => !isLocalBuild)
	.WithCriteria(() => !isPullRequest)
	.WithCriteria(() => isMainRepo)
	.WithCriteria(() => isMainBranch)
	.WithCriteria(() => isTagged)
	.Does(() =>
{
	if (string.IsNullOrEmpty(gitHubToken))
	{
		throw new InvalidOperationException("GitHub token was not provided.");
	}

	GitReleaseManagerClose(gitHubToken, gitHubRepoOwner, gitHubRepo, milestone);
});

Task("Generate-Benchmark-Report")
	.IsDependentOn("Build")
	.WithCriteria(isBenchmarkPresent)
	.Does(() =>
{
    var publishDirectory = $"{benchmarkDir}Publish/";
    var publishedAppLocation = MakeAbsolute(File($"{publishDirectory}{libraryName}.Benchmark.exe")).FullPath;
    var artifactsLocation = MakeAbsolute(File(benchmarkDir)).FullPath;

    DotNetCorePublish(benchmarkProject, new DotNetCorePublishSettings
    {
        Configuration = configuration,
		NoRestore = true,
        NoBuild = true,
        OutputDirectory = publishDirectory
    });

	using (DiagnosticVerbosity())
    {
        var processResult = StartProcess(
            publishedAppLocation,
            new ProcessSettings()
            {
                Arguments = $"-f * --artifacts={artifactsLocation}"
            });
        if (processResult != 0)
        {
            throw new Exception($"dotnet-benchmark.exe did not complete successfully. Result code: {processResult}");
        }
    }
});


///////////////////////////////////////////////////////////////////////////////
// TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Coverage")
	.IsDependentOn("Generate-Code-Coverage-Report")
	.Does(() =>
{
	StartProcess("cmd", $"/c start {codeCoverageDir}index.htm");
});

Task("Benchmark")
	.IsDependentOn("Generate-Benchmark-Report")
	.WithCriteria(isBenchmarkPresent)
	.Does(() =>
{
    var htmlReport = GetFiles($"{benchmarkDir}results/*-report.html", new GlobberSettings { IsCaseSensitive = false }).FirstOrDefault();
	StartProcess("cmd", $"/c start {htmlReport}");
});

Task("ReleaseNotes")
	.IsDependentOn("Create-Release-Notes");

Task("AppVeyor")
	.IsDependentOn("Run-Code-Coverage")
	.IsDependentOn("Upload-Coverage-Result")
    .IsDependentOn("Generate-Benchmark-Report")
	.IsDependentOn("Create-NuGet-Package")
	.IsDependentOn("Upload-AppVeyor-Artifacts")
	.IsDependentOn("Publish-MyGet")
	.IsDependentOn("Publish-NuGet")
	.IsDependentOn("Publish-GitHub-Release");

Task("AppVeyor-Ubuntu")
	.IsDependentOn("Run-Unit-Tests");

Task("Default")
	.IsDependentOn("Run-Unit-Tests")
	.IsDependentOn("Create-NuGet-Package");


///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);



///////////////////////////////////////////////////////////////////////////////
// PRIVATE METHODS
///////////////////////////////////////////////////////////////////////////////
private static string TrimStart(this string source, string value, StringComparison comparisonType)
{
	if (source == null)
	{
		throw new ArgumentNullException(nameof(source));
	}

	int valueLength = value.Length;
	int startIndex = 0;
	while (source.IndexOf(value, startIndex, comparisonType) == startIndex)
	{
		startIndex += valueLength;
	}

	return source.Substring(startIndex);
}
