using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ArgsReading;

namespace NuGetToolsPackager
{
	public sealed class NuGetToolsPackagerApp
	{
		public static int Main(string[] args)
		{
			return new NuGetToolsPackagerApp().Run(args);
		}

		public int Run(IReadOnlyList<string> args)
		{
			try
			{
				var argsReader = new ArgsReader(args);
				if (argsReader.ReadFlag("help|h|?"))
				{
					WriteUsage(Console.Out);
					return 0;
				}

				string configuration = argsReader.ReadOption("configuration") ?? "Release";
				string platform = argsReader.ReadOption("platform");
				string filespecs = argsReader.ReadOption("files") ?? "*.exe;*.dll;*.config";
				bool isQuiet = argsReader.ReadFlag("quiet");

				string csprojPath = argsReader.ReadArgument();
				if (csprojPath == null)
					throw new ArgsReaderException("Missing .csproj file.");

				argsReader.VerifyComplete();

				var projectElement = XDocument.Load(csprojPath).Root;
				if (projectElement == null)
					throw new ApplicationException("Failed to load project file.");

				string version = projectElement.XPathSelectElement("PropertyGroup/Version")?.Value;
				if (version == null)
					throw new ApplicationException("Project file is missing <Version>.");

				var packageDocument = new XDocument();
				var packageElement = new XElement(XName.Get("package", c_ns));
				packageDocument.Add(packageElement);
				var metadataElement = new XElement(XName.Get("metadata", c_ns));
				packageElement.Add(metadataElement);

				string packageId = projectElement.XPathSelectElement("PropertyGroup/PackageId")?.Value ?? Path.GetFileNameWithoutExtension(csprojPath);
				metadataElement.Add(new XElement(XName.Get("id", c_ns), packageId));
				metadataElement.Add(new XElement(XName.Get("version", c_ns), version));

				AddMetadataElement(metadataElement, "description", projectElement, "Description", "");
				AddMetadataElement(metadataElement, "authors", projectElement, "Authors", "");
				AddMetadataElement(metadataElement, "owners", projectElement, "Authors", "");
				AddMetadataElement(metadataElement, "projectUrl", projectElement, "PackageProjectUrl");
				AddMetadataElement(metadataElement, "licenseUrl", projectElement, "PackageLicenseUrl");
				AddMetadataElement(metadataElement, "iconUrl", projectElement, "PackageIconUrl");
				AddMetadataElement(metadataElement, "requireLicenseAcceptance", projectElement, "PackageRequireLicenseAcceptance");
				AddMetadataElement(metadataElement, "releaseNotes", projectElement, "PackageReleaseNotes");
				AddMetadataElement(metadataElement, "copyright", projectElement, "Copyright");
				AddMetadataElement(metadataElement, "tags", projectElement, "PackageTags");

#if false
				// pending https://github.com/NuGet/Home/issues/5099
				string repositoryUrl = projectElement.XPathSelectElement("PropertyGroup/RepositoryUrl")?.Value;
				string repositoryType = projectElement.XPathSelectElement("PropertyGroup/RepositoryType")?.Value;
				if (!string.IsNullOrWhiteSpace(repositoryUrl))
				{
					var repositoryElement = new XElement(XName.Get("repository", c_ns));
					if (!string.IsNullOrWhiteSpace(repositoryType))
						repositoryElement.SetAttributeValue("type", repositoryType);
					repositoryElement.SetAttributeValue("url", repositoryUrl);
					metadataElement.Add(repositoryElement);
				}
#endif

				var filesElement = new XElement(XName.Get("files", c_ns));
				packageElement.Add(filesElement);

				foreach (string filespec in filespecs.Split(',', ';').Select(x => x.Trim()).Where(x => x.Length != 0))
				{
					filesElement.Add(new XElement(XName.Get("file", c_ns),
						new XAttribute("src", string.IsNullOrWhiteSpace(platform) ? Path.Combine("bin", configuration, filespec) : Path.Combine("bin", configuration, platform, filespec)),
						new XAttribute("target", "tools")));
				}

				string outputPath = Path.Combine(Path.GetDirectoryName(csprojPath) ?? "", $"{packageId}.nuspec");
				packageDocument.Save(outputPath);
				if (!isQuiet)
					Console.WriteLine($"Created: {outputPath}");

				return 0;
			}
			catch (Exception exception)
			{
				if (exception is ArgsReaderException)
				{
					Console.Error.WriteLine(exception.Message);
					Console.Error.WriteLine();
					WriteUsage(Console.Error);
					return 2;
				}
				else if (exception is ApplicationException || exception is FileNotFoundException || exception is XmlException)
				{
					Console.Error.WriteLine(exception.Message);
					Console.Error.WriteLine();
					return 3;
				}
				else
				{
					Console.Error.WriteLine(exception.ToString());
					return 3;
				}
			}
		}

		private void AddMetadataElement(XElement metadataElement, string targetName, XElement projectElement, string propertyName, string defaultValue = null)
		{
			string propertyValue = projectElement.XPathSelectElement($"PropertyGroup/{propertyName}")?.Value ?? defaultValue;
			if (propertyValue != null)
				metadataElement.Add(new XElement(XName.Get(targetName, c_ns), propertyValue));
		}

		private void WriteUsage(TextWriter textWriter)
		{
			textWriter.WriteLine("Generates a .nuspec file for a tools package from a VS2017 console app project.");
			textWriter.WriteLine();
			textWriter.WriteLine("Usage: NuGetToolsPackager input [options]");
			textWriter.WriteLine();
			textWriter.WriteLine("   input");
			textWriter.WriteLine("      The path to the .csproj file.");
			textWriter.WriteLine();
			textWriter.WriteLine("   --configuration <name>");
			textWriter.WriteLine("      The project configuration to use, e.g. Release.");
			textWriter.WriteLine("   --platform <name>");
			textWriter.WriteLine("      The project platform to use, e.g. net46.");
			textWriter.WriteLine("   --files <filespecs>");
			textWriter.WriteLine("      The files to be included in the NuGet package, e.g. *.exe;*.dll");
			textWriter.WriteLine("   --quiet");
			textWriter.WriteLine("      Suppresses normal console output.");
		}

		const string c_ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
	}
}
