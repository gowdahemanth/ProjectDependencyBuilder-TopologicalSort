using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.XPath;
using System.Xml;
using System.IO;

namespace DependencyBuilder
{
    public class HandleBuildFailure
    {
        private const string modifiedProjectsFile = @"previous_modified_projects.txt";
        public HandleBuildFailure() { }


        /// <summary>
        /// Parses report.xml file to check if the previous build failed.
        /// report.xml stores the a summary of all the past builds.
        /// we can also use latest.build file to retrive the status of the previous build.
        /// </summary>
        public static bool IsPreviousBuildSuccessfull(string reportFilePath)
        {
            //string inputFile = @"C:\release\Trunk_Partial_Build\report.xml";
            string inputFile = Path.Combine(reportFilePath, "report.xml");
            XPathDocument doc;
            using (XmlReader xr = XmlReader.Create(inputFile,
                                  new XmlReaderSettings()
                                  {
                                      ConformanceLevel = ConformanceLevel.Fragment
                                  }))
            {
                doc = new XPathDocument(xr);
            }

            XPathNavigator nav = doc.CreateNavigator();
            if (nav != null)
            {
                XPathNodeIterator xPathIterator = nav.Select("/integration");
                int count = 0;

                while (xPathIterator.MoveNext())
                {
                    if (count == xPathIterator.Count - 1)
                        break;

                    count++;
                }

                XPathNavigator xpn = xPathIterator.Current;
                string status = xpn.GetAttribute("status", "");
                if (status.ToUpper().Equals("SUCCESS"))
                {
                    return true;
                }
            }
            return false;
        }


        /// <summary>
        /// The previous_modified_projects.txt file stores a list of all the projects that were modified in the last build or 
        /// files that were modified since the last failed build until we get a successful build.
        /// </summary>
        public static List<string> ParsePreviousModification()
        {
            try
            {
                if (File.Exists(modifiedProjectsFile))
                {
                    StreamReader sr = new StreamReader(modifiedProjectsFile);
                    List<string> previousModifiedProjects = new List<string>();
                    string line = sr.ReadLine();

                    //Continue to read until you reach end of file
                    while (line != null && (line.Trim().ToString() != ""))
                    {
                        //add to a list.
                        previousModifiedProjects.Add(line.Trim().ToString());
                        line = sr.ReadLine();
                    }

                    //close the file
                    sr.Close();
                    return previousModifiedProjects;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
            return null;
        }


        /// <summary>
        /// The modified projects of current build are saved. If the build fails, this list is used to build the failed projects 
        /// during the next build.
        /// </summary>
        public static void SaveModifiedProjects(List<string> modifiedProjects)
        {
            try
            {
                if (!File.Exists(modifiedProjectsFile))
                {
                    File.Create(modifiedProjectsFile).Close();
                }
                TextWriter tw = new StreamWriter(modifiedProjectsFile);
                foreach (string mProj in modifiedProjects)
                {
                    if (mProj.Trim() != "")
                        tw.WriteLine(mProj.ToString());
                }
                tw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }

    }
}
