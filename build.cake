#addin "nuget:?package=Cake.Git&version=0.14.0"
#addin "nuget:?package=Octokit&version=0.24.0"
#tool "nuget:?package=coveralls.io&version=1.3.4"
#tool "nuget:?package=xunit.runner.console&version=2.2.0"

using LibGit2Sharp;
using System.Text.RegularExpressions;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var nugetApiKey = Argument("nugetApiKey", "");
var githubApiKey = Argument("githubApiKey", "");

var solutionFileName = "NuGetToolsPackager.sln";
var githubOwner = "ejball";
var githubRepo = "NuGetToolsPackager";
var nugetSource = "https://api.nuget.org/v3/index.json";
var nugetPackageProjects = new[] { @"src\NuGetToolsPackager\NuGetToolsPackager.csproj" };

var rootPath = MakeAbsolute(Directory(".")).FullPath;
var gitRepository = LibGit2Sharp.Repository.IsValid(rootPath) ? new LibGit2Sharp.Repository(rootPath) : null;

var githubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("build.cake"));
if (!string.IsNullOrEmpty(githubApiKey))
	githubClient.Credentials = new Octokit.Credentials(githubApiKey);

Task("Clean")
	.Does(() =>
	{
		CleanDirectories("src/**/bin");
		CleanDirectories("src/**/obj");
		CleanDirectories("tests/**/bin");
		CleanDirectories("tests/**/obj");
		CleanDirectories("release");
	});

Task("Build")
	.IsDependentOn("Clean")
	.Does(() =>
	{
		DotNetCoreRestore(solutionFileName);
		DotNetCoreBuild(solutionFileName, new DotNetCoreBuildSettings { Configuration = configuration });
	});

Task("NuGetPackage")
	.IsDependentOn("Build")
	.Does(() =>
	{
		ExecuteProcess($@"src\NuGetToolsPackager\bin\{configuration}\net46\NuGetToolsPackager.exe", @"src\NuGetToolsPackager\NuGetToolsPackager.csproj --platform net46");
		NuGetPack(@"src\NuGetToolsPackager\NuGetToolsPackager.nuspec", new NuGetPackSettings { OutputDirectory = "release" });
	});

Task("NuGetPublish")
	.IsDependentOn("NuGetPackage")
	.WithCriteria(() => !string.IsNullOrEmpty(nugetApiKey) && !string.IsNullOrEmpty(githubApiKey))
	.Does(() =>
	{
		var dirtyEntry = gitRepository.RetrieveStatus().FirstOrDefault(x => x.State != FileStatus.Unaltered && x.State != FileStatus.Ignored);
		if (dirtyEntry != null)
			throw new InvalidOperationException($"The git working directory must be clean, but '{dirtyEntry.FilePath}' is dirty.");

		string headSha = gitRepository.Head.Tip.Sha;
		try
		{
			githubClient.Repository.Commit.GetSha1(githubOwner, githubRepo, headSha).GetAwaiter().GetResult();
		}
		catch (Octokit.NotFoundException exception)
		{
			throw new InvalidOperationException($"The current commit '{headSha}' must be pushed to GitHub.", exception);
		}

		string version = null;
		var pushSettings = new NuGetPushSettings { ApiKey = nugetApiKey, Source = nugetSource };
		foreach (var nupkgPath in GetFiles("release/*.nupkg").Select(x => x.FullPath))
		{
			string nupkgVersion = Regex.Match(nupkgPath, @"\.([^\.]+\.[^\.]+\.[^\.]+)\.nupkg$").Groups[1].ToString();
			if (version == null)
				version = nupkgVersion;
			else if (version != nupkgVersion)
				throw new InvalidOperationException($"Mismatched package versions '{version}' and '{nupkgVersion}'.");

			NuGetPush(nupkgPath, pushSettings);
		}

		var tagName = $"nuget-{version}";
		Information($"Creating git tag '{tagName}'...");
		githubClient.Git.Reference.Create(githubOwner, githubRepo,
			new Octokit.NewReference($"refs/tags/{tagName}", headSha)).GetAwaiter().GetResult();
	});

Task("Default")
	.IsDependentOn("Build");

void ExecuteProcess(string exePath, string arguments)
{
	int exitCode = StartProcess(exePath, arguments);
	if (exitCode != 0)
		throw new InvalidOperationException($"{exePath} failed with exit code {exitCode}.");
}

RunTarget(target);
