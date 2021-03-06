﻿/**
 *  @file MazeStructure.cs
 *  @author Dean Thomas
 *  @version 0.1
 *  
 *  @section LICENSE
 *  
 *  @section DESCRIPTION
 *  
 *  The MazeStructure class is a class to hold an array of cells to form a maze.  Also includes
 *  methods to generate the maze using different algorithms.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;

namespace Maze2012
{
    class MazeStructure
    {
        #region REFERENCES

        //  Background worker:  
        //  http://msdn.microsoft.com/en-us/library/system.componentmodel.backgroundworker.aspx     [11-05-2012]
       
        #endregion
        #region PRIVATE_VARIABLES

        //  List of all the cells associated with the current maze
        List<Cell> cells = new List<Cell>();

        //  Random number generator
        #if FIXED_MAZE_LAYOUT
        private static Random random = new Random(1);
        #else
        private static Random random = new Random();
        #endif


        //  Dimensions of the maze (number of cells)
        Size mazeDimensions;

        //  Cell sizes
        Size cellSize;

        //  Start and end of the maze
        Cell origin;
        Cell terminus;

        //  Cell index to highlight
        int selectedCellIndex = -1;

        //  Holder for 2D representation of the maze
        Bitmap twoDimensionalMap;

        //  Background worker for generation algorithms
        BackgroundWorker generationBackgroundWorker = new BackgroundWorker();

        #endregion
        #region PUBLIC_PROPERTIES

        /**
         *  Starting point of the maze
         *  
         *  Return the starting cell of the maze
         */
        public Cell Origin { get { return origin; } }

        /**
         *  Return the maze in 2D
         * 
         *  Return a map of the maze in 2 dimensions
         *  
         *  @return the map as a bitmap
         */
        public Bitmap TwoDimensionalMap { get { createTwoDimensionalMap(true); return twoDimensionalMap; } }

        /**
         *  Return the index of the selected cell
         * 
         *  Return the cell index currently marked as selected
         * 
         *  @return the index of the selected cell
         */
        public int SelectedCellIndex { get { return selectedCellIndex; } set { selectedCellIndex = value; } }

        /**
         *  Return the selected cell as an object
         * 
         *  Return the cell currently marked as selected
         * 
         *  @return the selected cell
         */
        public Cell SelectedCell { get { return cells[selectedCellIndex]; } }

        /**
         *  Return the coordinates of selected cell
         *  
         *  Return the coordinates of the cell current marked as selected
         *  
         *  @return the coordinates of the selected cell
         */
        public Point SelectedCellCoordinates { get { return indexToCoordinate(selectedCellIndex); } }

        //  Bounding rectangle
        public Rectangle getBoundingRectangle(Cell cell)
        {
            return new Rectangle(new Point(cell.Coordinates.X * cellSize.Width,cell.Coordinates.Y * cellSize.Height),cellSize);
        }

        #endregion
        #region DELEGATE_METHODS

        //  Handle progression event in maze generation
        public delegate void generationProgressChangedEventHandler(object sender, ProgressChangedEventArgs e);
        //  Handle completion event in maze generation
        public delegate void generationCompletedEventHandler(object sender, RunWorkerCompletedEventArgs e);

        #endregion
        #region EVENTS

        //  Raise an event to be handled by the parent object for completion of maze generation
        public event generationCompletedEventHandler generationCompleted;

        //  Raise an event to be handled by the parent object for progression of maze generation
        public event generationProgressChangedEventHandler generationProgressChanged;

        #endregion
        #region BACKGROUND_WORKER_METHODS

        /**
         *  Maze generation progressed
         *  
         *  The maze generator has progressed; pass this on to parent objects
         */
        void generationBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            generationProgressChangedEventHandler handler = generationProgressChanged;

            if (handler != null)
            {
                //  Invoke the delegate
                handler(this, e);
            }
        }

        /**
         *  Maze generation completed
         *  
         *  The generation background thread completed; maze has been completed
         */
        void generationBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            generationCompletedEventHandler handler = generationCompleted;

            if (handler != null)
            {
                //  Invoke the delegate
                handler(this, e);
            }
        }

        /**
         *  Generate a new maze
         *  
         *  Generation background work has been started; begin creating a new maze
         */
        void generationBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Debug.WriteLine("Generating new maze");

            this.resetMaze();

            //  Later will add additional algorithms
            this.generateDepthFirst();
            //  this.generatePrims();
        }

        #endregion
        #region CONSTRUCTOR_METHODS

        /**
         *  Default constructor
         *  
         *  Construct a new maze using the default constructor
         */
        public MazeStructure() : this(new Size(8, 8), new Size(32, 32)) { }

        /**
         *  Overloaded constructor
         *  
         *  Construct a new maze using the specified parameters
         *  
         *  @param mazeDimensions number of cells in the x and y dimensions
         *  @param cellSize the size of each cell in pixels in the x and y dimensions
         */
        public MazeStructure(Size mazeDimensions, Size cellSize)
        {
            //  Indent console output
            Debug.Indent();

            //  Set class variables
            this.mazeDimensions = mazeDimensions;
            this.cellSize = cellSize;

            //  Set up the background worker for maze generation
            this.generationBackgroundWorker.WorkerReportsProgress = true;
            this.generationBackgroundWorker.DoWork += new DoWorkEventHandler(generationBackgroundWorker_DoWork);
            this.generationBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(generationBackgroundWorker_ProgressChanged);
            this.generationBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(generationBackgroundWorker_RunWorkerCompleted);


            //  Clear the maze and setup connections
            resetMaze();
        }

        #endregion
        #region PRIVATE_METHODS

        /**
         *  Reset the maze
         *  
         *  Clear any existing maze structure and rebuild all walls between cells
         */
        private void resetMaze()
        {
            //  Clear any previous structures
            if (cells != null)
            {
                cells.Clear();
            }

            //  Create the array of cells (as a 1D list)
            for (int i = 0; i < mazeDimensions.Width * mazeDimensions.Height; i++)
            {
                Cell newCell = new Cell(indexToCoordinate(i), this.cellSize);

                cells.Add(newCell);
            }

            //  Connect the cells
            connectCells();
        }

        /**
         *  Create a two dimensional representation of the maze
         *  
         *  Create a two dimensional representation of the maze and store it
         *  internally within the maze object
         *  
         *  @param show cell distance from origin (false by default)
         */
        private void createTwoDimensionalMap(Boolean showDistanceFromOrigin = false)
        {
            //  If a map currently exists dispose of it
            if (twoDimensionalMap != null)
                twoDimensionalMap.Dispose();

            //  Create a bitmap large enough to hold the map
            twoDimensionalMap = new Bitmap(this.mazeDimensions.Width * cellSize.Width,
                this.mazeDimensions.Height * cellSize.Height);

            //  Create graphics objects to draw the map
            Graphics g = Graphics.FromImage(twoDimensionalMap);
            
            //  Loop through all cells in the array
            foreach (Cell c in cells)
            {
                //  Ask each cell to draw itself and draw this to the
                //  map of the entire maze
                g.DrawImage(c.draw2D(), 
                    c.Coordinates.X * c.CellSize.Width,
                    c.Coordinates.Y * c.CellSize.Height);
            }

            /*
                //  HACK:   tidy this code up, it is difficult to read
                //  Draw the distance from the origin
                if (showDistanceFromOrigin)
                {
                    g.DrawString(cells[i].DistanceFromOrigin.ToString(),
                        new Font("Arial",12),
                        new SolidBrush(Color.Blue),
                        new RectangleF(cells[i].Coordinates.X * cellSize.Width,
                            cells[i].Coordinates.Y * cellSize.Height,
                            cellSize.Width,
                            cellSize.Height));
                }
            
            //}

             */

            g.Dispose();
        }

        /**
         *  Build connections between cells
         *  
         *  Allow the cells in the maze to be aware of their immediate neighbours
         */
        private void connectCells()
        {
            for (int i = 0; i < cells.Count; i++)
            {
                //  Set up connections
                if (indexToCoordinate(i).Y > 0)
                    cells[i].CellToNorth = cells[coordinateToIndex(
                        new Point(indexToCoordinate(i).X, indexToCoordinate(i).Y - 1))];

                if (indexToCoordinate(i).Y < this.mazeDimensions.Height - 1)
                    cells[i].CellToSouth = cells[coordinateToIndex(
                        new Point(indexToCoordinate(i).X, indexToCoordinate(i).Y + 1))];

                if (indexToCoordinate(i).X > 0)
                    cells[i].CellToWest = cells[coordinateToIndex(
                        new Point(indexToCoordinate(i).X - 1, indexToCoordinate(i).Y))];

                if (indexToCoordinate(i).X < mazeDimensions.Width - 1)
                    cells[i].CellToEast = cells[coordinateToIndex(
                        new Point(indexToCoordinate(i).X + 1, indexToCoordinate(i).Y))];

            }
        }

        /**
         *  Convert between cell index and coordinates
         *  
         *  Take a one dimensional index and covert it to a two dimensional
         *  coordinate within the maze
         *  
         *  @param index the index of the cell within the array list
         *  @return Point the coordinates of the corresponding cell
         */
        private Point indexToCoordinate(int index)
        {
            Point result = new Point();

            //  Calculate X and Y coordinates
            result.X = index % this.mazeDimensions.Width;
            result.Y = index / this.mazeDimensions.Width;

            return result;
        }

        /**
         *  Convert between coordinates and cell index
         * 
         *  Take a two dimension cell coordinate and convert it to a 
         *  one dimensional index in the cell array
         *  
         *  @param Point the coordinates of the corresponding cell
         *  @return index the index of the cell within the array list
         */
        private int coordinateToIndex(Point coordinate)
        {
            if ((coordinate.Y >= 0) && (coordinate.Y < mazeDimensions.Height))
            {
                if ((coordinate.X >= 0) && (coordinate.X < mazeDimensions.Height))
                {
                    int result = 0;

                    result = coordinate.Y * mazeDimensions.Width;

                    result += coordinate.X;

                    return result;
                }
                else
                {
                    Debug.WriteLine("coordinate.X {0} out of range (maximum {1})", 
                        coordinate.X, mazeDimensions.Width - 1);

                    return -1;
                }
            }
            else
            {
                Debug.WriteLine("coordinate.Y {0} out of range (maximum {1})", 
                    coordinate.Y, mazeDimensions.Height - 1);

                return -1;
            }
        }

        /**
         *  Generate a depth first maze
         *  
         *  Use the depth first maze creation algorithm to build the
         *  connections between the cells
         */
        private void generateDepthFirst()
        {
            Debug.WriteLine("Using depth first algorithm");

            //  Keep tabs on the number of cells visited and the order in
            //  which they were visited
            Stack<Cell> cellStack = new Stack<Cell>();
            int visitedCells = 0;
            
            //  Distance from origin
            int distanceFromOrigin = 0;
            
            //  Start the maze at a random position
            Cell currentCell = chooseOriginCell();
            origin = currentCell;
            currentCell.DistanceFromOrigin = distanceFromOrigin;

            //  Push the origin onto the stack
            cellStack.Push(currentCell);
            visitedCells++;

            //  Output the current position and count to the console
            Debug.WriteLine("Current cell [{0},{1}]",
                indexToCoordinate(cells.IndexOf(currentCell)).X,
                indexToCoordinate(cells.IndexOf(currentCell)).Y);
            Debug.WriteLine("Visited cells is now {0}", visitedCells);

            //  Repeat until we have visited ever cell in the maze
            while (visitedCells < cells.Count)
            {
                //  Potential cell connections represents neighbouring
                //  cells with four walls intact
                if (currentCell.PotentialCellConnections.Count > 0)
                {
                    //  Move in a random direction
                    currentCell = currentCell.demolishRandomWall();

                    //  Put the new cell on the stack
                    cellStack.Push(currentCell);
                    
                    //  Output that are adding to the stack
                    Debug.WriteLine("Moved into a new cell");

                    //  Mark that we have moved to another cell
                    visitedCells++;

                    //  Output the current position and count to the console
                    Debug.WriteLine("Current cell [{0},{1}]",
                        indexToCoordinate(cells.IndexOf(currentCell)).X,
                        indexToCoordinate(cells.IndexOf(currentCell)).Y);
                    Debug.WriteLine("Visited cells is now {0}", visitedCells);

                    if (currentCell.DistanceFromOrigin == Cell.DISTANCE_UNINITILAISED)
                        currentCell.DistanceFromOrigin = distanceFromOrigin;
                    
                    //  Output distance from origin
                    Debug.WriteLine("Distance from origin is now {0}",
                        distanceFromOrigin);
                }
                else
                {
                    //  Go back down the path we previously followed
                    currentCell = cellStack.Pop();

                    //  Output that we are heading down through the stack
                    Debug.WriteLine("Returning to previously visited cell");

                    //  Output the current position
                    Debug.WriteLine("Current cell [{0},{1}]",
                        indexToCoordinate(cells.IndexOf(currentCell)).X,
                        indexToCoordinate(cells.IndexOf(currentCell)).Y);
                    
                    //  Output distance from origin
                    Debug.WriteLine("Distance from origin is now {0}",
                        distanceFromOrigin);
                }

                //  Report the generation progress to the delegate method
                generationBackgroundWorker.ReportProgress(
                    (int)(100 * ((decimal)visitedCells / (decimal)cells.Count)));
            }

            //  Mark the exit of the maze
            this.terminus = currentCell;

            //  Calculate the distances for the solvers
            calculateDistancesFromOrigin();

            //  Tell the origin and exit cells of their position
            origin.IsOrigin = true;
            terminus.IsExit = true;
        }

        /**
         *  Choose the maze origin
         *  
         *  Choose a random cell in the maze to use as the start or
         *  origin of the maze
         */
        private Cell chooseOriginCell()
        {
            //  TODO:  Add additional code to allow additional
            //  parameters for the start cell - e.g. start on outer
            //  edge
            return cells[random.Next(cells.Count)];
        }

        /**
         *  Generate a Prim's algorithms maze
         *  
         *  Use Prim's algorithm to build the connection between cells
         * 
         */
        private void generatePrims()
        {
            Debug.WriteLine("Using Prim's algorithm");

            Queue<Cell> openCells = new Queue<Cell>();
            List<Cell> closedCells = new List<Cell>();

            //  Choose the maze origin
            Cell currentCell = chooseOriginCell();
            origin = currentCell;

            //  Add the origin to the priority queue
            openCells.Enqueue(currentCell);
            
            //  Need to visit every cell in the maze structure
            while (closedCells.Count < cells.Count)
            {
                //  Keep a reference to the previous cell to
                //  allow us to knock a wall down
                Cell previousCell = currentCell;

                //  Work with the cell at the front of the queue
                currentCell = openCells.Dequeue();

                //  Knock a wall down
                currentCell.demolishWallBetweenCells(previousCell);
                //previousCell.demolishWallBetweenCells(currentCell);

                //  In theory we shouldn't hit this as closed
                //  cells should not appear in the queue
                if (!closedCells.Contains(currentCell))
                {

                    closedCells.Add(currentCell);

                    //  TODO: use an array of neighbour
                    //  cells for more tidy code
                    //
                    //  TODO: could also make the order
                    //  in which the cells are added random
                    try
                    {
                        if (currentCell.CellToNorth != null)
                        {
                            if (!closedCells.Contains(currentCell.CellToNorth))
                                openCells.Enqueue(currentCell.CellToNorth);
                        }

                        if (currentCell.CellToEast != null)
                        {
                            if (!closedCells.Contains(currentCell.CellToEast))
                                openCells.Enqueue(currentCell.CellToEast);
                        }

                        if (currentCell.CellToSouth != null)
                        {
                            if (!closedCells.Contains(currentCell.CellToSouth))
                                openCells.Enqueue(currentCell.CellToSouth);
                        }

                        if (currentCell.CellToWest != null)
                        {
                            if (!closedCells.Contains(currentCell.CellToWest))
                                openCells.Enqueue(currentCell.CellToWest);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                        Debug.WriteLine("Closed cells: {0} Current Cell: {1}",
                            closedCells.Count,
                            currentCell.Coordinates.ToString());
                    }

                }

                //  Report the generation progress to the delegate method
                generationBackgroundWorker.ReportProgress(
                    (int)(100 * ((decimal)closedCells.Count / (decimal)cells.Count)));

                Debug.WriteLine("Cells in queue: {0} Current Cell: {1}",
                    openCells.Count,
                    currentCell.Coordinates.ToString());
            }
        }

        /**
         *  Used to work out the distance in squares relative to the origin
         */
        private void calculateDistancesFromOrigin()
        {
            
        }

        #endregion
        #region PUBLIC_METHODS

        /**
         *  Generate a new maze
         *  
         *  Generate a new maze; handled on a background thread
         */
        public void generateMaze()
        {
            //  Start the generator thread
            generationBackgroundWorker.RunWorkerAsync();
        }

        #endregion
    }
}
