using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;

namespace DependencyBuilder
{
    public interface IPartialBuild
    {
        void GeneratePartialBuildList(ICollection<string> finalBuildOrder, ICollection<ProjectList> mainProjectList);
        List<string> GetPartialBuildProjectList(List<string> newlyModifiedProjects, ICollection<ProjectList> mainProjectList);
        List<string> LoadDependantProjects(ICollection<ProjectList> mainProjectList, List<string> modifiedProjects);
        List<string> LoadPreviousAndNewProjects(List<string> previousModifiedProjects, List<string> newlyModifiedProjects);
        List<string> CheckForCommonVBandVCProjects(ICollection<ProjectList> mainProjectList, List<string> modifiedProjects);
    }

    public class PartialBuild : IPartialBuild
    {
        private const string COMMON_VB = "COMMON-VB";
        private const string COMMON_VC = "COMMON-VC";


        /// <summary>
        /// Initiates the generation of partial build-order list.
        /// </summary>
        public void GeneratePartialBuildList(ICollection<string> finalBuildOrder, ICollection<ProjectList> mainProjectList)
        {
            List<string> newlyModifiedProjects = IdentifyNewlyModifiedProjects(finalBuildOrder);
            List<string> currentPartialBuildProjectList = GetPartialBuildProjectList(newlyModifiedProjects, mainProjectList);
            UtilityFuncs.UpdateFullBuildProj(currentPartialBuildProjectList);
        }

        public List<string> GetPartialBuildProjectList(List<string> newlyModifiedProjects, ICollection<ProjectList> mainProjectList)
        {
            //If previous build has failed, include the previously modified projects to the build list. OR
            //If this is a force build, just build the projects that were built in the previous build.
            List<string> previousModifiedProjects = new List<string>();
            if ((HandleBuildFailure.IsPreviousBuildSuccessfull(Program.ReportFilePath) == false) || UtilityFuncs.IsForceBuild())
            {
                previousModifiedProjects = HandleBuildFailure.ParsePreviousModification();
            }
            List<string> modifiedProjects = LoadPreviousAndNewProjects(previousModifiedProjects, newlyModifiedProjects);
            modifiedProjects = SortModifiedProjects(mainProjectList, modifiedProjects);

            //write modified projects to text file.
            if (modifiedProjects != null && modifiedProjects.Count > 0)
            {
                HandleBuildFailure.SaveModifiedProjects(modifiedProjects);
            }

            modifiedProjects = LoadDependantProjects(mainProjectList, modifiedProjects);
            modifiedProjects = CheckForCommonVBandVCProjects(mainProjectList, modifiedProjects);

            //remove requiredPartialBuildProjects from modifiedProjects list.
            List<string> newProjList = new List<string>();
            foreach (string mProj in modifiedProjects)
            {
                if (UtilityFuncs.requiredPartialBuildProjects.Exists(x => x.Trim().ToUpper() == mProj.Trim().ToUpper()) == false)
                {
                    newProjList.Add(mProj);  //Add only those modified projects that are not in required project list.
                }
            }

            //Finally just append all the required projects.
            newProjList.AddRange(UtilityFuncs.requiredPartialBuildProjects);
            return newProjList;

            //UpdateFullBuildProj(newProjList);
        }

        //
        public List<string> SortModifiedProjects(ICollection<ProjectList> mainProjectList, List<string> modifiedProjects)
        {
            List<string> sortedProjList = new List<string>();

            foreach (string modifiedProj in modifiedProjects)
            {
                ProjectList parentProj = mainProjectList.First(x => x.ProjectName.ToUpper().ToString() == modifiedProj.ToUpper());
                IList<ProjectReferenceList> prl = parentProj.projectReferenceList;

                foreach (string proj in modifiedProjects)
                {
                    if(proj != modifiedProj && prl.Any(x => x.RefProjectName.ToUpper().ToString() == proj.ToUpper()))
                    {
                        int index1 = -1, index2 = -1;
                        if (sortedProjList.Contains(modifiedProj))
                        {
                            index1 = sortedProjList.IndexOf(modifiedProj);

                            if (sortedProjList.Contains(proj))
                            {
                                index2 = sortedProjList.IndexOf(proj);
                            }
                            if (index1 < index2)
                            {
                                sortedProjList.Remove(proj);
                                sortedProjList.Insert(index1, proj);
                            }
                        }
                        else
                        {
                            sortedProjList.Add(proj);
                        }
                    }
                }
                if (!sortedProjList.Contains(modifiedProj))
                {
                    sortedProjList.Add(modifiedProj);
                }
            }

            return sortedProjList;
        }

        /// <summary>
        /// This method retrieves all the projects that depends on the modified projects. These dependent projects are added to partial build list.
        /// </summary>
        public List<string> LoadDependantProjects(ICollection<ProjectList> mainProjectList, List<string> modifiedProjects)
        {
            //identify the dependent projects.
            //-> for each project in mainProjectList, just go one-level deep to check if it has any of the "modified projects".
            //  -> If there is one add the parent to the modifiedProjects list
            List<string> dependantProjList = new List<string>();
            foreach (ProjectList parentProj in mainProjectList)
            {
                foreach (ProjectReferenceList childProj in parentProj.projectReferenceList)
                {
                    foreach (string modifiedProj in modifiedProjects)
                    {
                        if (modifiedProj.ToUpper().Equals(childProj.RefProjectName.ToUpper()))
                        {
                            UtilityFuncs.InsertProjectInToList(ref dependantProjList, parentProj.ProjectName);
                            break;
                        }
                    }
                }
            }
            //add dependent projects to modified projects list.
            modifiedProjects.AddRange(dependantProjList);
            return modifiedProjects;
        }

        private static List<string> IdentifyNewlyModifiedProjects(ICollection<string> finalBuildOrder)
        {
            List<string> allProjects = new List<string>();
            List<string> newlyModifiedProjects = new List<string>();
            XDocument doc = XDocument.Load(string.Format("{0}modifications.xml", Program.InputPath));

            //identify the modified/checked-in projects 
            if (doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "Modification").Count() > 0)
            {
                XElement targets = doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "Modification").First();

                //list of all projects.
                foreach (string projName in finalBuildOrder)
                {
                    if (projName[projName.Length - 2].Equals('-'))
                        UtilityFuncs.InsertProjectInToList(ref allProjects, projName.Substring(0, projName.Length - 2).ToString());
                    else
                        UtilityFuncs.InsertProjectInToList(ref allProjects, projName.ToString());
                }

                //identifying projects that are modified.
                do
                {
                    string[] project = targets.Element("FolderName").Value.Split('/');

                    for (int i = 3; i < project.Length; i++)
                    {
                        string pName = project[i].Trim().ToString();
                        if (pName.Equals("CA"))
                            pName = "ClientActivity";
                        pName = pName.Replace('.', '_');

                        if (((allProjects.FindIndex(x => x.Trim().ToUpper() == pName.Trim().ToUpper()) >= 0) ||
                            pName.ToUpper().Equals(COMMON_VB) || pName.ToUpper().Equals(COMMON_VC)))
                        {
                            if (newlyModifiedProjects.Contains(pName.Trim()) == false)
                            {
                                newlyModifiedProjects.Add(pName);
                            }
                            break;
                        }
                    }

                    targets = (System.Xml.Linq.XElement)targets.NextNode;

                } while (targets != null);
            }
            Log.LogModifiedProjectsList(newlyModifiedProjects);

            return newlyModifiedProjects;
        }


        /// <summary>
        /// Here modification.xml file is parsed to retrieve all the projects that were modified since previous build.
        /// If previous build failed, the projects from the previous build are included to build again.
        /// All the dependant projects of the modified projects are included.
        /// </summary>
        //private List<string> IdentifyModifiedProjects(ICollection<string> finalBuildOrder, ICollection<ProjectList> mainProjectList)
        //{
        //    List<string> allProjects = new List<string>();
        //    List<string> newlyModifiedProjects = new List<string>();
        //    XDocument doc = XDocument.Load(string.Format("{0}modifications.xml", Program.InputPath));

        //    //identify the modified/checked-in projects 
        //    if (doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "Modification").Count() > 0)
        //    {
        //        XElement targets = doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "Modification").First();

        //        //list of all projects.
        //        foreach (string projName in finalBuildOrder)
        //        {
        //            if (projName[projName.Length - 2].Equals('-'))
        //                UtilityFuncs.InsertProjectInToList(ref allProjects, projName.Substring(0, projName.Length - 2).ToString());
        //            else
        //                UtilityFuncs.InsertProjectInToList(ref allProjects, projName.ToString());
        //        }

        //        //identifying projects that are modified.
        //        do
        //        {
        //            string[] project = targets.Element("FolderName").Value.Split('/');

        //            for (int i = 3; i < project.Length; i++)
        //            {
        //                string pName = project[i].Trim().ToString();
        //                if (pName.Equals("CA"))
        //                    pName = "ClientActivity";
        //                pName = pName.Replace('.', '_');

        //                if (((allProjects.FindIndex(x => x.Trim().ToUpper() == pName.Trim().ToUpper()) >= 0) ||
        //                    pName.ToUpper().Equals(COMMON_VB) || pName.ToUpper().Equals(COMMON_VC)))
        //                {
        //                    if (newlyModifiedProjects.Contains(pName.Trim()) == false)
        //                    {
        //                        newlyModifiedProjects.Add(pName);
        //                    }
        //                    break;
        //                }
        //            }

        //            targets = (System.Xml.Linq.XElement)targets.NextNode;

        //        } while (targets != null);
        //    }
        //    Log.LogModifiedProjectsList(newlyModifiedProjects);

        //    //If previous build has failed, include the previously modified projects to the build list. OR
        //    //If this is a force build, just build the projects that were built in the previous build.
        //    List<string> previousModifiedProjects = new List<string>();
        //    if ((HandleBuildFailure.IsPreviousBuildSuccessfull(Program.ReportFilePath) == false) || UtilityFuncs.IsForceBuild())
        //    {
        //        previousModifiedProjects = HandleBuildFailure.ParsePreviousModification();
        //    }
        //    List<string> modifiedProjects = LoadPreviousAndNewProjects(previousModifiedProjects, newlyModifiedProjects);

        //    //write modified projects to text file.
        //    if (modifiedProjects != null && modifiedProjects.Count > 0)
        //    {
        //        HandleBuildFailure.SaveModifiedProjects(modifiedProjects);
        //    }

        //    modifiedProjects = LoadDependantProjects(mainProjectList, modifiedProjects);
        //    modifiedProjects = CheckForCommonVBandVCProjects(mainProjectList, modifiedProjects);

        //    //remove requiredPartialBuildProjects from modifiedProjects list.
        //    List<string> newProjList = new List<string>();
        //    foreach (string mProj in modifiedProjects)
        //    {
        //        if (UtilityFuncs.requiredPartialBuildProjects.Exists(x => x.Trim().ToUpper() == mProj.Trim().ToUpper()) == false)
        //        {
        //            newProjList.Add(mProj);
        //        }
        //    }

        //    //add all the requiredPartialBuildProjects
        //    newProjList.AddRange(UtilityFuncs.requiredPartialBuildProjects);
        //    return newProjList;

        //    //UpdateFullBuildProj(newProjList);
        //}


        /// <summary>
        /// If modifications include 'Common-VB' and 'Common-VC' folder changes, then all the projects that depend on them are built.
        /// </summary>
        public List<string> CheckForCommonVBandVCProjects(ICollection<ProjectList> mainProjectList, List<string> modifiedProjects)
        {
            if (modifiedProjects.Contains(COMMON_VB, StringComparer.OrdinalIgnoreCase))
            {
                modifiedProjects.AddRange(RetriveCommonVBandVCDependantProject(mainProjectList, COMMON_VB));
            }
            if (modifiedProjects.Contains(COMMON_VC, StringComparer.OrdinalIgnoreCase))
            {
                modifiedProjects.AddRange(RetriveCommonVBandVCDependantProject(mainProjectList, COMMON_VC));
            }
            modifiedProjects.Remove(COMMON_VB);
            modifiedProjects.Remove(COMMON_VC);

            return modifiedProjects;
        }


        /// <summary>
        /// Retrieves the Common-VB or Common-VC dependant projects based on the projectType argument passed.
        /// </summary>
        private static ICollection<string> RetriveCommonVBandVCDependantProject(ICollection<ProjectList> mainProjectList, string projectType)
        {
            ICollection<string> tempList = new List<string>();
            foreach (ProjectList parentProj in mainProjectList)
            {
                if ((parentProj.ProjectName.ToUpper().Equals(projectType)))
                {
                    foreach (ProjectReferenceList childProj in parentProj.projectReferenceList)
                    {
                        tempList.Add(childProj.RefProjectName);
                    }
                }
            }
            return tempList;
        }

        /// <summary>
        /// If previous build failed, then it will load the projects modified during previous build. Otherwise, only 
        /// projects modified in later checkins are included.
        /// </summary>
        public List<string> LoadPreviousAndNewProjects(List<string> previousModifiedProjects, List<string> newlyModifiedProjects)
        {
            List<string> modifiedProjects = new List<string>();
            if (previousModifiedProjects != null && previousModifiedProjects.Count > 0)
            {
                modifiedProjects.AddRange(previousModifiedProjects);
            }
            //'newlyModifiedProjects' list is empty, if its a force build.
            foreach (string mProj in newlyModifiedProjects)
            {
                if (modifiedProjects.Exists(x => x.Trim().ToUpper() == mProj.Trim().ToUpper()) == false)
                {
                    modifiedProjects.Add(mProj);
                }
            }
            return modifiedProjects;
        }
        
    }
}
