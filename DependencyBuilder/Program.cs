using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DependencyBuilder
{
    public class Program
    {
        public static string BuildMode { get; set; }
        public static string InputPath { get; set; }
        public static string OutputPath { get; set; }
        public static string ReportFilePath { get; set; }

        static void Main(string[] args)
        {
            Program p = new Program();
            Log.DeleteOldLogFile();
            Log.CreateLogFile();

            switch (args.Length)
            {
                case 4:
                    //param1:-F, param2:..\..\modifications.xml, param3:..\..\full_build1.xml, param4:C:\release\Trunk_Partial_Build\report.xml
                    BuildMode = args[0].ToString();
                    InputPath = args[1].ToString();
                    OutputPath = args[2].ToString();
                    ReportFilePath = args[3].ToString();

                    Console.WriteLine("DependencyBuilder in progress...");
                    LoadFile loadFile = new LoadFile(new UtilityFuncs());
                    loadFile.LoadDependencies();

                    break;
                default:
                    Console.WriteLine("Invalid arguments...");
                    Console.WriteLine("Proper format: DependencyBuilder '-F(for full build)/-M(for min build)' 'path to modifications.xml' 'output path of full_build_min.xml'  C:\\release\\Trunk_Partial_Build\\report.xml ");
                    break;
            };

            Log.Write("End of Dependency Builder");
        }

    }
}
