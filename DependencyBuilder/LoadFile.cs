using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Configuration;
using System.IO;

namespace DependencyBuilder
{
    public class LoadFile
    {
        List<ProjectList> mainProjectList = new List<ProjectList>();
        List<ProjectList> oldVersionList = new List<ProjectList>();
        List<string> finalBuildOrder = new List<string>();
        List<ProjectList> identifyCyclesList;
        public static int MAXLEVEL = 12;
        public static string outputFile = @"BuildOrderDetails.txt";
        IUtilityFuncs _utilityFuncs;

        public LoadFile(IUtilityFuncs utilityFuncs)
        {
            _utilityFuncs = utilityFuncs;
            Log.LogModificationFileChanges();
        }


        /// <summary>
        /// Parses projectReferences.txt file and loads all the project and its dependencies information in to the mainProjectList.
        /// It also carries out the rest of the build order generation process.
        /// </summary>
        public void LoadDependencies()
        {
            int projCount = 0;

            StreamWriter sw = File.CreateText(outputFile);
            sw.Dispose();

            //Load the XML to data structure.
            ReferenceMaps rMaps = _utilityFuncs.LoadReferenceMaps();
            for (int refCount = 0; refCount < rMaps.ReferenceMap.Count; )
            {
                ProjectList projListExisting = IdentifyProjectNode(rMaps.ReferenceMap[refCount].Component);
                if (projListExisting != null)
                {
                    while (refCount < rMaps.ReferenceMap.Count && rMaps.ReferenceMap[refCount].Component == projListExisting.ProjectName)
                    {
                        ProjectReferenceList refList = new ProjectReferenceList();
                        refList.RefProjectName = rMaps.ReferenceMap[refCount].Reference;
                        refList.IsChecked = false;
                        if (refList.RefProjectName != "")
                        {
                            if (!refList.RefProjectName.Equals(projListExisting.ProjectName))
                                projListExisting.projectReferenceList.Add(refList);
                        }
                        refCount++;
                    }
                    continue;
                }

                ProjectList projList = new ProjectList();

                string compName = rMaps.ReferenceMap[refCount].Component;
                if (compName.Equals(String.Empty))
                {
                    refCount++;
                    continue;
                }

                projList.ProjectName = compName;
                projList.VersionNuber = 0;

                while (refCount < rMaps.ReferenceMap.Count && rMaps.ReferenceMap[refCount].Component == compName)
                {
                    ProjectReferenceList refList = new ProjectReferenceList();
                    refList.RefProjectName = rMaps.ReferenceMap[refCount].Reference;
                    refList.IsChecked = false;
                    if (refList.RefProjectName != "")
                    {
                        if (!refList.RefProjectName.Equals(projList.ProjectName))
                            projList.projectReferenceList.Add(refList);
                    }
                    refCount++;
                }

                mainProjectList.Add(projList);
                projCount++;
            }

            //Sort based on number of reference projects.
            mainProjectList.Sort((x, y) => x.projectReferenceList.Count.CompareTo(y.projectReferenceList.Count));

            //Initiate identifying project build order list.
            GenerateProjectBuildOrderList(mainProjectList);

            //Added extra validation as a bug was detected.
            //Ensure for each of the old version projects detected, we are rebuilding it a second time.
            List<string> projectsBuiltMultipleTimes = new List<string>();
            List<string> oldVersionList = new List<string>();
            foreach (string proj in finalBuildOrder)
            {
                if (proj[proj.Length - 2].Equals('-'))
                {
                    if(proj[proj.Length - 1].Equals('1'))
                        oldVersionList.Add(proj.Substring(0, proj.Length - 2).ToString());
                    else
                        projectsBuiltMultipleTimes.Add(proj.Substring(0, proj.Length - 2).ToString());
                }
            }
            List<string> leftOverProjects = oldVersionList.Except(projectsBuiltMultipleTimes, StringComparer.OrdinalIgnoreCase).ToList();
            if (leftOverProjects.Count > 0)
            {
                foreach (string missingProject in leftOverProjects)
                    finalBuildOrder.Add(missingProject);
            }


            //Add projects in the 'UtilityFuncs.requiredPartialBuildProjects' list towards the end of finalBuildOrders list. 
            //Care must be taken such that "VERSIONBINARIES" and "HOMEWORKSINSTALLER" are always the last two projects in the list.
            foreach (var pro in UtilityFuncs.requiredPartialBuildProjects)
            {
                finalBuildOrder.Add(pro.ToString());
            }

            //Write to text file.
            _utilityFuncs.PrintCycleList();
            _utilityFuncs.DisplayBuildOrder(finalBuildOrder);

            //Do a partial build or full build
            if (Program.BuildMode.ToUpper() == "-M")
            {
                PartialBuild pb = new PartialBuild();
                pb.GeneratePartialBuildList(finalBuildOrder, mainProjectList);
            }
            else if (Program.BuildMode.ToUpper() == "-F")
            {
                UtilityFuncs.UpdateFullBuildProj(finalBuildOrder);
            }

            //Just for debugging purpose.
            //_utilityFuncs.CompareFullAndPartialBuildProjects();

            return;
        }


        /// <summary>
        /// This method initiates the generation of build order list.
        /// The 'mainProjectList' holds the list of all the projects and there references that is read from projectReferences.txt file.
        /// </summary>
        private void GenerateProjectBuildOrderList(List<ProjectList> mainProjectList)
        {
            foreach (ProjectList pl in mainProjectList)
            {
                if (pl.InBuildList == false)
                {
                    if (pl.projectReferenceList.Count == 0)
                    {
                        foreach (var pro in UtilityFuncs.requiredPartialBuildProjects)
                        {
                            if (pro.Equals(pl.ProjectName))
                                continue;
                        }

                        pl.InBuildList = true;

                        CheckAndAddToFinalBuildOrder(pl.ProjectName);
                        _utilityFuncs.WriteToTextFile(pl.ProjectName, MAXLEVEL);
                        continue;
                    }

                    identifyCyclesList = new List<ProjectList>();
                    if (RecursiveCheck(pl, MAXLEVEL))
                    {
                        pl.InBuildList = true;
                        CheckAndAddToFinalBuildOrder(pl.ProjectName);
                    }
                }
            }
        }


        /// <summary>
        /// This is a recusrsive algorithm, that starts with checking the dependencies for a project, then check the dependencies of all
        /// those dependent project and continue until we reach traverse all the projects in that list.
        /// </summary>
        private bool RecursiveCheck(ProjectList pl, int levelDeep)
        {
            int refListCounter = 0;
            _utilityFuncs.WriteToTextFile(pl.ProjectName, levelDeep);

            if (pl.InBuildList)
            {
                return true;
            }

            foreach (ProjectReferenceList prl in pl.projectReferenceList)
            {
                if (prl.IsChecked == true)
                {
                    refListCounter++;
                    continue;
                }

                //self reference.
                if (prl.RefProjectName.Equals(pl.ProjectName))
                {
                    pl.InBuildList = true;
                    CheckAndAddToFinalBuildOrder(pl.ProjectName);
                    return true;
                }

                if (levelDeep == 0)
                    return false;

                //Start: brake the cycle
                bool cycleFound = false;
                foreach (ProjectList proList in identifyCyclesList)
                {
                    if (pl.ProjectName.Equals(proList.ProjectName))
                    {
                        cycleFound = true;

                        oldVersionList.Add(pl);
                        pl.VersionNuber = pl.VersionNuber + 1;
                        int verVal = pl.VersionNuber;
                        CheckAndAddToFinalBuildOrder(pl.ProjectName + "-" + verVal);
                        _utilityFuncs.AddToCycleList(identifyCyclesList, pl.ProjectName);
                        pl.InBuildList = true;

                        return true;
                    }
                }
                //Add project to cyclelist
                if (!cycleFound)
                    identifyCyclesList.Add(pl);
                //End: brake the cycle

                if (prl.IsChecked == false && levelDeep > 0)
                {
                    levelDeep--;
                    ProjectList pnode = IdentifyProjectNode(prl.RefProjectName);
                    if (pnode != null)
                    {
                        bool val = RecursiveCheck(pnode, levelDeep);
                        if (val)
                        {
                            prl.IsChecked = val;
                            refListCounter++;
                        }
                    }
                    else if (pnode == null)
                    {
                        refListCounter++;
                    }

                    levelDeep++;
                }

                //Have to remove the project from identifyCyclesList
                identifyCyclesList.Remove(pl);

                if (refListCounter.Equals(pl.projectReferenceList.Count))
                {
                    pl.InBuildList = true;

                    CheckReferencesVersionNumber(pl);

                    if (pl.VersionNuber > 0)
                        CheckAndAddToFinalBuildOrder(pl.ProjectName + "-" + pl.VersionNuber);
                    else
                        CheckAndAddToFinalBuildOrder(pl.ProjectName);

                    RevisitOldVersionList();
                    return true;
                }
            }

            if (refListCounter.Equals(pl.projectReferenceList.Count))
            {
                CheckReferencesVersionNumber(pl);
                if (pl.VersionNuber > 0)
                    CheckAndAddToFinalBuildOrder(pl.ProjectName + "-" + pl.VersionNuber);
                else
                    CheckAndAddToFinalBuildOrder(pl.ProjectName);

                RevisitOldVersionList();
                return true;
            }

            return false;
        }


        /// <summary>
        /// 
        /// </summary>
        private void RevisitOldVersionList()
        {
            foreach (ProjectList projList in oldVersionList)
            {
                bool notBuilt = false;
                int oneCount = 0, twoCount = 0;
                if (projList.InBuildList && projList.VersionNuber == 3)
                    continue;

                foreach (var projRef in projList.projectReferenceList)
                {
                    ProjectList pList = IdentifyProjectNode(projRef.RefProjectName);

                    if (pList == null)
                        continue;

                    if (!pList.InBuildList)
                        notBuilt = true;

                    switch (pList.VersionNuber)
                    {
                        case 1:
                            oneCount = 1;
                            break;
                        case 2:
                            twoCount = 2;
                            break;
                        case 3:
                            break;
                        default:
                            break;
                    }
                }

                if (!notBuilt && projList.VersionNuber != 1 && oneCount == 0 && twoCount == 0)
                {
                    projList.VersionNuber = 3;
                    CheckAndAddToFinalBuildOrder(projList.ProjectName + "-" + projList.VersionNuber);
                }
                else if (!notBuilt && oneCount == 0 && twoCount == 2)
                {
                    projList.VersionNuber = 3;
                    CheckAndAddToFinalBuildOrder(projList.ProjectName + "-" + projList.VersionNuber);
                }
                else if (!notBuilt && oneCount == 1)
                {
                    projList.VersionNuber = 2;
                    CheckAndAddToFinalBuildOrder(projList.ProjectName + "-" + projList.VersionNuber);
                }

            }
        }


        /// <summary>
        /// Some projects which happen to appear multiple times because of cyclic dependency is given a version number, for tracking purpose.
        /// </summary>
        private void CheckReferencesVersionNumber(ProjectList pl)
        {
            int minVal = 0, maxVal = 0;
            foreach (var projRef in pl.projectReferenceList)
            {
                ProjectList pList = IdentifyProjectNode(projRef.RefProjectName);

                if (pList == null)
                    continue;

                switch (pList.VersionNuber)
                {
                    case 0:
                        break;
                    case 1:
                        if (minVal == 0)
                        {
                            minVal = 1;
                        }
                        break;
                    case 2:
                        if (maxVal == 0)
                        {
                            maxVal = 2;
                        }
                        break;
                    case 3:
                        break;
                    default:
                        break;
                }
            }

            if (minVal == 0 && maxVal == 2)
            {
                pl.VersionNuber = 3;
                oldVersionList.Add(pl);
            }
            if (minVal == 1)
            {
                pl.VersionNuber = 2;
                oldVersionList.Add(pl);
            }
        }


        /// <summary>
        /// Add the project to build list, if the project is not already added to the build order.
        /// </summary>
        private void CheckAndAddToFinalBuildOrder(string projName)
        {
            projName = projName.Replace('.', '_');

            foreach (string pName in finalBuildOrder)
            {
                string tempPName = "";
                if (pName[pName.Length - 2] == '-')
                    tempPName = pName.Substring(0, pName.Length - 2);
                else
                    tempPName = pName;

                if (projName[projName.Length - 2] == '-')
                    if (projName.Equals(pName))
                        return;
                if (projName.Equals(tempPName))
                    return;
            }

            //Check if the project exists in full_build.proj. If it doesn't exist, its not added to 'fullBuildProjects' list.
            List<string> fullBuildProjects = UtilityFuncs.RetrieveBuildProjectList();
            string tempProjName = projName;
            if (tempProjName[tempProjName.Length - 2].Equals('-'))
            {
                tempProjName = projName.Substring(0, projName.Length - 2);
            }
            if (!fullBuildProjects.Contains(tempProjName, StringComparer.OrdinalIgnoreCase))
                return;

            //some projects are copy files only. they can be moved to the end in the build order.
            foreach (var pro in UtilityFuncs.requiredPartialBuildProjects)
            {
                if (pro.ToUpper().Equals(projName.ToUpper()))
                {
                    return;
                }
            }
            finalBuildOrder.Add(projName);
        }


        /// <summary>
        /// Identifies the project node in mainProjectList data structure for a given project
        /// </summary>
        private ProjectList IdentifyProjectNode(string refProjName)
        {
            foreach (ProjectList pl in mainProjectList)
            {
                if (pl.ProjectName.Equals(refProjName))
                {
                    return pl;
                }
            }
            return null;
        }

    }
}
