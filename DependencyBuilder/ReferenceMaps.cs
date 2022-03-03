using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DependencyBuilder
{
    public class ReferenceMap
    {
        public string Component { get; set; }
        public string Reference { get; set; }
    }

    public class ReferenceMaps
    {
        public List<ReferenceMap> ReferenceMap;

        public ReferenceMaps()
        {
            ReferenceMap = new List<ReferenceMap>();
        }
    }
}
