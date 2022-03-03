using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DependencyBuilder
{
    public class CycleList
    {
        public List<string>[] cycleList = new List<string>[10];
        public CycleList()
        {
            for (int index = 0; index < 10; index++)
            {
                cycleList[index] = new List<string>();
            }
        }
    }
}
