#addin "nuget:?package=SharpZipLib&Version=1.3.0"
#addin "nuget:?package=Cake.Compression&Version=0.2.4"
#addin "nuget:?package=Cake.FileHelpers&Version=3.3.0"

var buildId = EnvironmentVariable("APPVEYOR_BUILD_VERSION") ?? "0";
var buildVersion = EnvironmentVariable("IMPOSTOR_VERSION") ?? "1.0.0";
var buildBranch = EnvironmentVariable("APPVEYOR_REPO_BRANCH") ?? "dev";
var buildDir = MakeAbsolute(Directory("./build"));
CreateDirectory(buildDir);

var prNumber = EnvironmentVariable("APPVEYOR_PULL_REQUEST_NUMBER");
var target = Argument("target", "Deploy");
var configuration = Argument("configuration", "Release");

if (buildBranch != "master") {
    buildVersion = buildVersion + "-ci." + buildId;
}

private void ImpostorPublish(string name, string project, string runtime, bool isServer = false) {
    var projBuildDir = buildDir.Combine(name + "_" + runtime);
    var projBuildName = name + "_" + buildVersion + "_" + runtime;

    DotNetPublish(project, new DotNetPublishSettings {
        Configuration = configuration,
        NoRestore = true,
        Framework = "net8.0",
        Runtime = runtime,
        SelfContained = false,
        PublishSingleFile = true,
        PublishTrimmed = false,
        OutputDirectory = projBuildDir
    });

    if (isServer) {
        CreateDirectory(projBuildDir.Combine("plugins"));
        CreateDirectory(projBuildDir.Combine("libraries"));

        if (runtime == "win-x64") {
            FileWriteText(projBuildDir.CombineWithFilePath("run.bat"), "@echo off\r\nImpostor.Server.exe\r\npause");
        }
    }

    if (runtime == "win-x64") {
        Zip(projBuildDir, buildDir.CombineWithFilePath(projBuildName + ".zip"));
    } else {
        GZipCompress(projBuildDir, buildDir.CombineWithFilePath(projBuildName + ".tar.gz"));
    }
}

Task("Clean")
    .Does(() => {
        if (DirectoryExists(buildDir)) {
            DeleteDirectory(buildDir, new DeleteDirectorySettings {
                Recursive = true
            });
        }
    });

Task("Restore")
    .Does(() => {
        DotNetRestore("./src/Impostor.sln");
    });

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() => {
        DotNetBuild("./src/Impostor.sln", new DotNetBuildSettings {
            Configuration = configuration,
        });
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        DotNetTest("./src/Impostor.Tests/Impostor.Tests.csproj", new DotNetTestSettings {
            Configuration = configuration,
            NoBuild = true
        });
    });

Task("Deploy")
    .IsDependentOn("Test")
    .Does(() => {
        Information("Finished.");
    });

RunTarget(target);
