﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Maze2012
{
    class DataModel
    {
        MazeStructure mazeStructure;

        public MazeStructure MazeStructure { get { return mazeStructure; } }

        public DataModel()
        {
            mazeStructure = new MazeStructure();
        }
    }
}
