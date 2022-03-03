using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;

namespace DependencyBuilder
{
    public class Log
    {
        /// <summary>
        /// Create a new log file everyday.
        /// </summary>
        public static void CreateLogFile()
        {
            try
            {
                string logFilePath = RetrieveLogFilePath();
                if (!File.Exists(logFilePath))
                {
                    File.Create(logFilePath).Close();
                    using (StreamWriter sw = File.AppendText(logFilePath))
                    {
                        sw.WriteLine("Dependency Builder log file dated " + DateTime.Today.Date);
                        sw.WriteLine("--------------------------------------------------------\n\n");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }


        /// <summary>
        /// Delete old log files. [If its 10 days old, delete.]
        /// </summary>
        public static void DeleteOldLogFile()
        {
            try
            {
                var files = new DirectoryInfo(@"log").GetFiles("*.txt");
                foreach (var file in files)
                {
                    if (DateTime.UtcNow - file.CreationTimeUtc > TimeSpan.FromDays(10))
                    {
                        File.Delete(file.FullName);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }


        /// <summary>
        /// This method logs the list of checkins made by the user.
        /// </summary>
        public static void LogModificationFileChanges()
        {
            try
            {
                string logFilePath = RetrieveLogFilePath();
                XDocument doc = XDocument.Load(string.Format("{0}modifications.xml", Program.InputPath));
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("Start of LogModificationFileChanges");
                if (doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "Modification").Count() > 0)
                {
                    var targets = doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "Modification");

                    sb.AppendLine("Below are the actual changes that are checked in by the user... \n");
                    sb.AppendLine("Modification_Type | Folder_Name | File_Name | Modified_Time | User_Name | Change_Number");
                    foreach (XElement target in targets)
                    {
                        string modificationType = target.Element("Type").Value.ToString();
                        string folderName = target.Element("FolderName").Value.ToString();
                        string fileName = target.Element("FileName").Value.ToString();
                        string modifiedTime = target.Element("ModifiedTime").Value.ToString();
                        string userName = target.Element("UserName").Value.ToString();
                        string changeNumber = target.Element("ChangeNumber").Value.ToString();
                        string modificationDetails = modificationType + " | " + folderName + " | " + fileName + " | " + modifiedTime + " | " +
                                        userName + " | " + changeNumber;

                        sb.AppendLine(modificationDetails);
                    }
                }
                sb.AppendLine("End of LogModificationFileChanges");

                using (StreamWriter sw = File.AppendText(logFilePath))
                {
                    sw.WriteLine();
                    sw.WriteLine(sb);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }


        /// <summary>
        /// This method logs the list of projects that were identified as modified by the dependency builder. 
        /// [Ideally this should match the projects checked in ]
        /// </summary>
        public static void LogModifiedProjectsList(List<string> modifiedProjects)
        {
            try
            {
                string logFilePath = RetrieveLogFilePath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Start of LogModifiedProjectsList");
                if (modifiedProjects.Count > 0)
                {
                    sb.AppendLine("Modified projects list...\n");
                    foreach (string project in modifiedProjects)
                    {
                        sb.AppendLine(project);
                    }
                }
                sb.AppendLine("End of LogModifiedProjectsList");

                using (StreamWriter sw = File.AppendText(logFilePath))
                {
                    sw.WriteLine();
                    sw.WriteLine(sb);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }


        /// <summary>
        /// Writes to log file.
        /// </summary>
        public static void Write(string loggingInfo)
        {
            string logFilePath = RetrieveLogFilePath();
            using (StreamWriter sw = File.AppendText(logFilePath))
            {
                sw.WriteLine(loggingInfo);
            }
        }


        private static string RetrieveLogFilePath()
        {
            string logFile = "DBLog_" + DateTime.Today.Month + "_" + DateTime.Today.Day + "_" + DateTime.Today.Year + ".txt";
            string logFolder = "log";
            
            Directory.CreateDirectory("log");
            return Path.Combine(logFolder, logFile);
        }
    }
}
