#addin "nuget:?package=SharpZipLib&Version=1.3.3"
#addin "nuget:?package=Cake.Compression&Version=0.3.0"
#addin "nuget:?package=Cake.FileHelpers&Version=5.0.0"

var workflow = BuildSystem.GitHubActions.Environment.Workflow;
var buildId = workflow.RunNumber;
var tag = workflow.RefType == GitHubActionsRefType.Tag ? workflow.RefName : null;

var buildVersion = EnvironmentVariable("IMPOSTOR_VERSION") ?? "1.0.0";
var buildBranch = EnvironmentVariable("APPVEYOR_REPO_BRANCH") ?? "dev";
var buildDir = MakeAbsolute(Directory("./build"));
CreateDirectory(buildDir);

var prNumber = EnvironmentVariable("APPVEYOR_PULL_REQUEST_NUMBER");
var target = Argument("target", "Deploy");
var configuration = Argument("configuration", "Release");

var msbuildSettings = new DotNetMSBuildSettings();

if (tag != null) 
{
    if (tag[1..] != buildVersion) throw new Exception("Tag version has to be the same as VersionPrefix in Directory.Build.props");
    msbuildSettings.Version = buildVersion;
}
else if (buildId != 0) 
{
    buildId += 500; 
    msbuildSettings.VersionSuffix = "ci." + buildId;
    buildVersion += "-ci." + buildId;
} 
else 
{
    buildVersion += "-dev";
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
        Information("Finished zipping folder: " + projBuildDir);
    } else {
        GZipCompress(projBuildDir, buildDir.CombineWithFilePath(projBuildName + ".tar.gz"));
        Information("Finished gzipping folder: " + projBuildDir);
    }

    if (BuildSystem.GitHubActions.IsRunningOnGitHubActions) {
        BuildSystem.GitHubActions.Commands.UploadArtifact(projBuildDir, projBuildName);
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

        // Server.
        ImpostorPublish("Impostor-Server", "./src/Impostor.Server/Impostor.Server.csproj", "win-x64", true);
        ImpostorPublish("Impostor-Server", "./src/Impostor.Server/Impostor.Server.csproj", "osx-x64", true);
        ImpostorPublish("Impostor-Server", "./src/Impostor.Server/Impostor.Server.csproj", "linux-x64", true);
        ImpostorPublish("Impostor-Server", "./src/Impostor.Server/Impostor.Server.csproj", "linux-arm", true);
        ImpostorPublish("Impostor-Server", "./src/Impostor.Server/Impostor.Server.csproj", "linux-arm64", true);

        // API.
        DotNetPack("./src/Impostor.Api/Impostor.Api.csproj", new DotNetPackSettings {
            Configuration = configuration,
            OutputDirectory = buildDir,
            IncludeSource = true,
            IncludeSymbols = true,
            MSBuildSettings = msbuildSettings
        });

        if (BuildSystem.GitHubActions.IsRunningOnGitHubActions) {
            foreach (var file in GetFiles(buildDir + "/*.{nupkg,snupkg}"))
            {
                BuildSystem.GitHubActions.Commands.UploadArtifact(file, "Impostor.Api");
            }
        }
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
