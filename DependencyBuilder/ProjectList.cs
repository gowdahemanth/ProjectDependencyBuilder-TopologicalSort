using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DependencyBuilder
{
    public class ProjectList
    {
        public string ProjectName { get; set; }
        public bool InBuildList { get; set; }
        public int VersionNuber { get; set; }
        public List<ProjectReferenceList> projectReferenceList = new List<ProjectReferenceList>();
    }

    public class ProjectReferenceList
    {
        public string RefProjectName { get; set; }
        public bool IsChecked { get; set; }
    }
}
