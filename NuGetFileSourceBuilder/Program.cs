using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Emit;
using System.Xml.Linq;

namespace NuGetFileSourceBuilder
{
    class Program
    {
        private static string _inputPath;
        private static string _outputPath;
        private static string _nugetPath;
        private static Dictionary<string, string> cmdArgs = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            if (args.Length > 0)
                _inputPath = args[0];

            if (args.Length > 1)
                _outputPath = args[1];

            ParseCmdArgs(args);

            var packageMaker = new PackageMaker(_inputPath, _outputPath, _nugetPath);
            packageMaker.MakePackages();

            Console.ReadKey();
        }

        static void ParseCmdArgs(string[] args)
        {

            
            try
            {
                cmdArgs = args.Select(s => s.Split('=')).ToDictionary(s => s[0].ToUpper(), s => s[1]);
                _inputPath = cmdArgs["INPUT"];
                _outputPath = cmdArgs["OUTPUT"];
                _nugetPath = cmdArgs["NUGET"];
            }
            catch (Exception e)
            {
                Console.WriteLine("Invalid arguments exception.\n<ParamName>=<ParamValue>" + e);
            }
        }
    }




    public class PackageMaker
    {
        private string _inputPath;
        private string _outputPath;
        private string _nugetPath;
        private List<List<string>> ImportsPaths = new List<List<string>>();

        public PackageMaker(string input, string output, string nuget)
        {
            _inputPath = input;
            _outputPath = output;
            _nugetPath = nuget;
        }

        public void MakePackages()
        {
            try
            {
                GetImportsFolders(_inputPath);
                foreach (var repo in ImportsPaths.SelectMany(repos => repos))
                    GenerateNuspecXml(repo);
                Console.WriteLine("Completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }

        

        #region ImportsAndFilesLookup
        /// <summary>
        /// Look for import repositories in the directory tree and fills list of lists for each subimport folder path
        /// </summary>
        /// <param name="path"></param>
        private void GetDirsRecursively(string path)
        {
            bool repoDirFound = false;

            foreach (var directory in Directory.GetDirectories(path))
            {
                if (directory.Contains(".svn"))
                {
                    repoDirFound = true;
                    continue;
                }
                if (directory.Contains("imports"))
                {
                    var dirPaths = Directory.GetDirectories(directory).ToList();
                    ImportsPaths.Add(dirPaths);
                    return;
                }
                if (repoDirFound)
                    continue;

                GetDirsRecursively(directory);
            }
        }

        private List<string> GetFilesFromDirectoriesRecursively(string path)
        {
            List<string> files = new List<string>();

            files.AddRange(Directory.GetFiles(path)); // add files from current dir

            foreach (var directory in Directory.GetDirectories(path).Where(directory => !directory.Contains(".svn")))
                files.AddRange(GetFilesFromDirectoriesRecursively(directory));

            return files;
        }

        private void GetImportsFolders(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Directory \"{path}\" does not exist...");
                return;
            }

            GetDirsRecursively(path);
        }

        private string GetFileTarget(string file, string importPath)
        {
            var internalPath = file.Substring(importPath.Length, file.Length - importPath.Length);
            char separator = Path.DirectorySeparatorChar;
            if (!internalPath.Contains('\\'))
                return string.Empty;

            var targetPath = internalPath.Substring(0, internalPath.LastIndexOf('\\'));
            return $"{targetPath}\\";
        }

        #endregion

        private IEnumerable<XElement> GetMetaDataElements(List<string> files, string importPath, XNamespace xmlns)
        {
            string id = new DirectoryInfo(importPath).Name;
            string description = id + "Nuget Package";
            string title = id;
            // a way to find version of package look for all dlls and exes and find most used version which we consider as package version
            string version = files.Where(f => f.EndsWith(".dll") || f.EndsWith(".exe"))
                .Select(f => FileVersionInfo.GetVersionInfo(f).ProductVersion)
                .GroupBy(f => f)
                .OrderByDescending(grp => grp.Count())
                .Select(grp => grp.Key)
                .First();


            yield return new XElement(xmlns + "id", id);
            yield return new XElement(xmlns + "version", version);
            yield return new XElement(xmlns + "description", description);
            yield return new XElement(xmlns + "title", title);
            yield return new XElement(xmlns + "authors", "CSS Applications");
            yield return new XElement(xmlns + "owners", "NICE Systems");
            yield return new XElement(xmlns + "copyright", "Copyright © 2016 NICE Systems, All rights reserved");
        }

        private IEnumerable<XElement> GetFilesXElements(List<string> files, string importPath, XNamespace xmlns)
        {

            foreach (var file in files)
            {
                var xelem = new XElement(xmlns + "file");
                xelem.Add(new XAttribute("src", file));
                xelem.Add(new XAttribute("target", GetFileTarget(file, importPath)));
                yield return xelem;
            }
        }

        private void GenerateNuspecXml(string importRepoPath)
        {
            XNamespace xmlns = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd";

            var files = GetFilesFromDirectoriesRecursively(importRepoPath);
            var metadataXElements = GetMetaDataElements(files, importRepoPath, xmlns);
            var fileXElements = GetFilesXElements(files, importRepoPath, xmlns);

            XDocument nuspecXml = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));

            nuspecXml.Add(new XElement(xmlns + "package", new XAttribute("xmlns", xmlns)));

            nuspecXml.Root.Add(new XElement(xmlns + "metadata", metadataXElements),
                                new XElement(xmlns + "files", fileXElements));

            string packageName = new DirectoryInfo(importRepoPath).Name;

            Console.WriteLine($"Package {packageName} has been created in {importRepoPath}.");
            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);

            nuspecXml.Save($"{_outputPath}/{packageName}.nuspec");
        }

        //private void NugetPack
    }
}