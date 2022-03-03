using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Xml.Linq;

namespace DependencyBuilder
{
    public interface IUtilityFuncs
    {
        ReferenceMaps LoadReferenceMaps();
        void WriteToTextFile(string projName, int levelDeep);

        void AddToCycleList(List<ProjectList> identifyCyclesList, string projName);
        void PrintCycleList();
        void DisplayBuildOrder(List<string> finalBuildOrder);
        void CompareFullAndPartialBuildProjects();
    }

    public class UtilityFuncs : IUtilityFuncs
    {
        List<List<string>> numberOfCycles = new List<List<string>>();
        public static List<string> requiredPartialBuildProjects = new List<string>() {"DBRNXTRACT", "HLPSRC", "REPORTS", "CLINICALLOGICS", "CHAMELEON", "FONTS", "PUBLICREFS",
                                "ISDATA", "HTTPSSYNC", "FINANCIAL_CORE_TEST", "FINANCIAL_VIEWMODEL_TEST", "TFAEVALUATION_TEST", 
                                "VERSIONBINARIES", "HOMEWORKSINSTALLER" };
        public UtilityFuncs() { }


        /// <summary>
        /// Parses the [component-project reference] mapping in projectReferences.txt file and loads it to 'ReferenceMaps' data structure.
        /// </summary>
        public ReferenceMaps LoadReferenceMaps()
        {
            string inputFile = @"projectReferences.txt";
            XmlSerializer xmlSer = new System.Xml.Serialization.XmlSerializer(typeof(ReferenceMaps));
            XmlTextReader xmlText = new XmlTextReader(string.Format("{0}", inputFile));

            ReferenceMaps rm = (ReferenceMaps)xmlSer.Deserialize(xmlText);
            return rm;
        }


        /// <summary>
        /// Writes the project dependency details to the BuildOrderDetails.txt file.
        /// </summary>
        public void WriteToTextFile(string projName, int levelDeep)
        {
            int indentation = LoadFile.MAXLEVEL - levelDeep;

            using (StreamWriter sw = File.AppendText(LoadFile.outputFile))
            {
                if (!projName.Equals("[c]"))
                    sw.WriteLine();

                for (int i = 0; i < indentation; i++)
                {
                    sw.Write("  ");
                }
                sw.Write(projName);
            }
        }


        /// <summary>
        /// Project that is identified as causing a cyclic dependency is added to this cycles list.
        /// </summary>
        public void AddToCycleList(List<ProjectList> identifyCyclesList, string projName)
        {
            List<string> projList = new List<string>();
            foreach (var proj in identifyCyclesList)
            {
                projList.Add(proj.ProjectName);
            }
            projList.Add(projName);
            numberOfCycles.Add(projList);
        }


        /// <summary>
        /// Identified cyclic dependencies are written to BuildOrderDetails.txt file.
        /// </summary>
        public void PrintCycleList()
        {
            using (StreamWriter sw = File.AppendText(LoadFile.outputFile))
            {
                sw.WriteLine(); sw.WriteLine();
                foreach (List<string> cList in numberOfCycles)
                {
                    sw.WriteLine("\n\n");
                    foreach (string project in cList)
                    {
                        sw.Write(project.ToString() + " -> ");
                    }
                    sw.WriteLine("\n\n");
                }
            }
        }


        /// <summary>
        /// The generated build order of all projects is written to BuildOrderDetails.txt file.
        /// </summary>
        public void DisplayBuildOrder(List<string> finalBuildOrder)
        {
            List<string> oldVersionProjects = new List<string>();
            using (StreamWriter sw = File.AppendText(LoadFile.outputFile))
            {
                //sw.WriteLine(); sw.WriteLine();
                sw.WriteLine("\n\nDependency List to use in full_build.proj....\n");
                //sw.WriteLine();
                foreach (var projName in finalBuildOrder)
                {
                    if (projName[projName.Length - 1].Equals('1'))
                    {
                        oldVersionProjects.Add(projName.Substring(0, projName.Length - 2).ToString());
                        continue;
                    }
                    else if (projName[projName.Length - 2].Equals('-'))
                        sw.WriteLine(projName.Substring(0, projName.Length - 2).ToString() + ';');
                    else
                        sw.WriteLine(projName.ToString() + ';');
                }

                sw.WriteLine("\n\nUse the older version of the below projects....\n");
                foreach (var oldProj in oldVersionProjects)
                {
                    if (oldProj.ToUpper().Equals("BTICAREWATCHSVCUI"))
                    {
                        sw.Write(oldProj.ToString());
                        sw.WriteLine(" [Place the old version of BTICareWatchSvcUI.exe in ..\\release\\bin] ");
                    }
                    else
                        sw.WriteLine(oldProj.ToString());
                }
            }
        }


        /// <summary>
        /// Here we are creating the full_build_min.proj file which is used for the purpose of partial build.
        /// full_build_min.proj file is an exact copy of full_build.proj file, except that the projects build list replaced with 
        /// only those projects that were modified and its dependencies.
        /// </summary>
        public static void UpdateFullBuildProj(List<string> finalBuildOrder)
        {
            List<string> oldVersionProjects = new List<string>();
            if (File.Exists(string.Format("{0}full_build_min.proj", Program.OutputPath)))
            {
                System.IO.File.Delete(string.Format("{0}full_build_min.proj", Program.OutputPath));
                Console.WriteLine("Removed old full_build_min file...");
            }
            Console.WriteLine("Creating new full_build_min file...");
            System.IO.File.Copy(string.Format("{0}full_build.proj", Program.OutputPath), string.Format("{0}full_build_min.proj", Program.OutputPath));
            XDocument doc = XDocument.Load(string.Format("{0}full_build_min.proj", Program.OutputPath));
            XElement targets = doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "FullBuildTargets").First();
            targets.Value = "" + '\n';

            //Remove projects not in original full_build.proj
            finalBuildOrder = CompareMinBuildProjectsWithFullBuild(finalBuildOrder);

            foreach (string projName in finalBuildOrder)
            {
                if (projName[projName.Length - 1].Equals('1'))
                {
                    oldVersionProjects.Add(projName.Substring(0, projName.Length - 2).ToString());
                    continue;
                }
                else if (projName[projName.Length - 2].Equals('-'))
                    targets.Value += projName.Substring(0, projName.Length - 2).ToString() + ';' + '\n';
                else
                    targets.Value += projName.ToString() + ';' + '\n';
            }

            doc.Save(string.Format("{0}full_build_min.proj", Program.OutputPath));
        }


        /// <summary>
        /// Add the project to projlist if its not already added.
        /// </summary>
        public static void InsertProjectInToList(ref List<string> projList, string projName)
        {
            if (projList.FindIndex(x => x.Trim().ToUpper() == projName.Trim().ToUpper()) < 1)
            {
                List<string> fullBuildProjects = RetrieveBuildProjectList();
                if (fullBuildProjects.Contains(projName, StringComparer.OrdinalIgnoreCase))
                    projList.Add(projName);
            }
        }


        /// <summary>
        /// Check if the current build is a forced build.
        /// </summary>
        public static bool IsForceBuild()
        {
            try
            {
                XDocument doc = XDocument.Load(string.Format("{0}modifications.xml", Program.InputPath));
                if (doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "Modification").Count() == 0)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Message ", ex);
            }
            return false;
        }


        /// <summary>
        /// The new project build list (partial or Full build list), is compared against the projects list in full_build.proj file.
        /// This is to make sure we don't have any new projects in the list.
        /// </summary>
        private static List<string> CompareMinBuildProjectsWithFullBuild(List<string> finalBuildOrder)
        {
            List<string> newList = new List<string>();
            List<string> fullBuildProjects = RetrieveBuildProjectList();
            foreach (string projName in finalBuildOrder)
            {
                if (projName[projName.Length - 2].Equals('-') || fullBuildProjects.Contains(projName, StringComparer.OrdinalIgnoreCase))
                {
                    newList.Add(projName);
                }
            }
            return newList;
        }


        /// <summary>
        /// Retrives the list of all the projects from the full_build.proj file
        /// </summary>
        public static List<string> RetrieveBuildProjectList(string buildFile = "full_build.proj")
        {
            XDocument doc = XDocument.Load(string.Format("{0}{1}", Program.OutputPath, buildFile));
            XElement targets = doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "FullBuildTargets").First();
            List<string> projects = targets.Value.Replace("\n", "").Split(";".ToCharArray()).ToList<string>();
            List<string> fullBuildProjects = new List<string>();
            foreach (string proj in projects)
            {
                fullBuildProjects.Add(proj.Trim());
            }
            return fullBuildProjects;
        }


        /// <summary>
        /// This is currently not used. It can be used for debugging. It compares the build list generated from the DependencyBuilder algorithm
        /// with that of the build list in full_build.proj file.
        /// </summary>
        public void CompareFullAndPartialBuildProjects()
        {
            List<string> fullBuildProjects = RetrieveBuildProjectList();
            List<string> partialBuildProjects = RetrieveBuildProjectList("full_build_min.proj");

            List<string> except = fullBuildProjects.Except(partialBuildProjects, StringComparer.OrdinalIgnoreCase).ToList();
            List<string> except2 = partialBuildProjects.Except(fullBuildProjects, StringComparer.OrdinalIgnoreCase).ToList();
        }

    }
}
