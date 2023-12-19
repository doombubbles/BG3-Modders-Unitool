using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Alphaleonis.Win32.Filesystem;
using bg3_modders_multitool.Services;
using bg3_modders_multitool.ViewModels;
using CommandLine;
using CommandLine.Text;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace BG3_Modders_Unitool;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var window = new MainWindow();
        _ = new Application
        {
            MainWindow = new Window
            {
                DataContext = window
            }
        };
        Parser.Default.ParseArguments<Options>(args).WithParsedAsync(Run).ContinueWith(_ => Console.WriteLine(window.ConsoleOutput));
    }

    public static async Task Run(Options o)
    {
        var source = Path.GetFullPath(o.Source);
        var modName = Path.GetFileName(source);
        var dest = string.IsNullOrEmpty(o.Destination) ? Path.GetFullPath(Path.Combine(source, "..")) : Path.GetFullPath(o.Destination);
        var output = Path.Combine(DragAndDropHelper.TempFolder, modName + ".pak");

        if (!Directory.Exists(source))
        {
            await Console.Error.WriteLineAsync($"Error: path {source} does not refer to a valid directory");
            return;
        }

        Directory.CreateDirectory(DragAndDropHelper.TempFolder);

        var metaList = new Dictionary<string, List<string>>();

        var processMod = typeof(DragAndDropHelper).GetMethod("ProcessMod", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            if (processMod.Invoke(null, new object[] {source, modName}) is List<string> metas)
            {
                metaList.Add(Guid.NewGuid().ToString(), metas);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            throw;
        }

        if (!File.Exists(output))
        {
            await Console.Error.WriteLineAsync("Failed to pak mod file");
            return;
        }


        if (o.Zip && DragAndDropHelper.GenerateInfoJson(metaList))
        {
            DragAndDropHelper.GenerateZip(Path.Combine(dest, "_"), modName);
        }
        else
        {
            File.Move(output, Path.Combine(dest, modName + ".pak"), MoveOptions.ReplaceExisting);
        }

        DragAndDropHelper.CleanTempDirectory();
    }

    public class Options
    {
        [Option('z', "zip", Required = false, HelpText = "Whether to compress to zip file instead of leaving as .pak")]
        public bool Zip { get; set; }

        // [Option('s', "source", Required = true, HelpText = "Set source file path or directory")]
        [Value(0, Required = true, HelpText = "Path to mod folder")]
        public string Source { get; set; } = "";

        [Value(0, HelpText = "Path to destination folder")]
        public string Destination { get; set; } = "";

        // ReSharper disable once UnusedMember.Global
        [Usage(ApplicationAlias = "bg3-modders-unitool")]
        public static IEnumerable<Example> Examples => new List<Example>
        {
            new("Convert your mod and put it in your mods folder", new Options
            {
                Source = "./MyModFolder",
                Destination = "~/AppData/Local/Larian Studios/Baldur's Gate 3/Mods"
            }),
            new("Convert your mod and add it to a zip next to your folder", new Options()
            {
                Zip = true,
                Source = "./MyModFolder",
            })
        };
    }
}