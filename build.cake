//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");

//////////////////////////////////////////////////////////////////////
// SET PACKAGE VERSION
//////////////////////////////////////////////////////////////////////

var version = "0.1.0";

var isAppveyor = BuildSystem.IsRunningOnAppVeyor;
var dbgSuffix = configuration == "Debug" ? "-dbg" : "";
var packageVersion = version;
var packageVersionString = version + dbgSuffix;

//////////////////////////////////////////////////////////////////////
// DEFINE RUN CONSTANTS
//////////////////////////////////////////////////////////////////////

// Directories
var PROJECT_DIR = Context.Environment.WorkingDirectory.FullPath + "/";
var SRC_DIR = PROJECT_DIR + "src/";
var PACKAGE_DIR = PROJECT_DIR + "package/" + configuration;

var ANALYZERS_TESTS_OUTPUT_DIR = SRC_DIR + "nunit.analyzers.tests/bin/";
var ANALYZERS_OUTPUT_DIR = SRC_DIR + "nunit.analyzers/bin/";

// Solution
var SOLUTION_FILE = PROJECT_DIR + "src/nunit.analyzers.sln";

// Projects
var ANALYZER_PROJECT = SRC_DIR + "nunit.analyzers/nunit.analyzers.csproj";
var TEST_PROJECT = SRC_DIR + "nunit.analyzers.tests/nunit.analyzers.tests.csproj";

// Package sources for nuget restore
var PACKAGE_SOURCE = new string[]
{
    "https://www.nuget.org/api/v2",
};

//////////////////////////////////////////////////////////////////////
// SETUP AND TEARDOWN TASKS
//////////////////////////////////////////////////////////////////////
Setup(context =>
{
    if (BuildSystem.IsRunningOnAppVeyor)
    {
        var tag = AppVeyor.Environment.Repository.Tag;

        if (tag.IsTag)
        {
            packageVersion = tag.Name;
            packageVersionString = tag.Name;
        }
        else
        {
            var buildNumber = AppVeyor.Environment.Build.Number.ToString("00000");
            var branch = AppVeyor.Environment.Repository.Branch;
            var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;

            packageVersion = version + "." + buildNumber;

            if (branch == "master" && !isPullRequest)
            {
                packageVersionString = version + "-dev-" + buildNumber + dbgSuffix;
            }
            else
            {
                var suffix = "-ci-" + buildNumber + dbgSuffix;

                if (isPullRequest)
                    suffix += "-pr-" + AppVeyor.Environment.PullRequest.Number;
                else if (AppVeyor.Environment.Repository.Branch.StartsWith("release", StringComparison.OrdinalIgnoreCase))
                    suffix += "-pre-" + buildNumber;
                else
                    suffix += "-" + branch;

                // Nuget limits "special version part" to 20 chars. Add one for the hyphen.
                if (suffix.Length > 21)
                    suffix = suffix.Substring(0, 21);

                packageVersionString = version + suffix;
            }
        }

        AppVeyor.UpdateBuildVersion(packageVersionString);
    }

    // Executed BEFORE the first task.
    Information("Building {0} version \"{1}\" / {2} of NUnit.Analyzers", configuration, packageVersion, packageVersionString);

    foreach(var assemblyInfo in GetFiles("./src/**/AssemblyInfo.cs"))
    {
        CreateAssemblyInfo(
            assemblyInfo.ChangeExtension(".Generated.cs"),
            new AssemblyInfoSettings
            {
                Version = packageVersion,
                FileVersion = packageVersion
            });
    }
});

//////////////////////////////////////////////////////////////////////
// CLEAN
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        CleanDirectory(ANALYZERS_TESTS_OUTPUT_DIR);
        CleanDirectory(ANALYZERS_OUTPUT_DIR);
    });


//////////////////////////////////////////////////////////////////////
// RESTORE NUGET PACKAGES
//////////////////////////////////////////////////////////////////////

Task("RestorePackages")
    .Does(() =>
    {
        DotNetCoreRestore(SOLUTION_FILE, new DotNetCoreRestoreSettings 
        {
            Sources = PACKAGE_SOURCE,
        });
    });

//////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("RestorePackages")
    .Does(() =>
    {
        DotNetCoreBuild(ANALYZER_PROJECT, new DotNetCoreBuildSettings
        {
            Configuration = configuration,
            Verbosity = DotNetCoreVerbosity.Minimal,
            NoRestore = true
        });
    });

//////////////////////////////////////////////////////////////////////
// TEST
//////////////////////////////////////////////////////////////////////

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
    {
        DotNetCoreTest(TEST_PROJECT, new DotNetCoreTestSettings
        {
            Configuration = configuration,
            Logger = "trx",
            VSTestReportPath = "TestResult.xml",
        });
    })
    .Finally(() =>
    {
        if (AppVeyor.IsRunningOnAppVeyor)
        {
            AppVeyor.UploadTestResults("TestResult.xml", AppVeyorTestResultsType.MSTest);
        }
    });


//////////////////////////////////////////////////////////////////////
// Pack
//////////////////////////////////////////////////////////////////////

Task("Pack")
    .IsDependentOn("Build")
    .Does(() =>
    {
        NuGetPack("./src/nunit.analyzers/nunit.analyzers.nuspec", new NuGetPackSettings()
        {
            Version = packageVersionString,
            OutputDirectory = PACKAGE_DIR,
            Properties = new Dictionary<string, string>()
            {
                {"Configuration", configuration}
            }
        });
    });

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Rebuild")
    .IsDependentOn("Clean")
    .IsDependentOn("Build");

Task("Default")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

Task("Appveyor")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
