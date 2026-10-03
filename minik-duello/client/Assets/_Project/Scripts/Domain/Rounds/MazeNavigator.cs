namespace MinikDuello.Domain.Rounds
{
    public enum MoveResult
    {
        Moved,
        Blocked,
        Goal
    }

    /// <summary>Labirentte karakter hareketi. Duvara çarpmak yalnızca "çarpma" sayar; üç çarpma bir hata sayılır.</summary>
    public sealed class MazeNavigator
    {
        public const int BumpsPerMistake = 3;

        private readonly MazeRound maze;

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Bumps { get; private set; }
        public int Moves { get; private set; }

        public MazeNavigator(MazeRound maze)
        {
            this.maze = maze;
            X = maze.StartX;
            Y = maze.StartY;
        }

        public bool AtGoal => X == maze.GoalX && Y == maze.GoalY;
        public int Mistakes => Bumps / BumpsPerMistake;

        public MoveResult Move(Wall direction)
        {
            if (!MazeGenerator.CanMove(maze, X, Y, direction))
            {
                Bumps++;
                return MoveResult.Blocked;
            }
            switch (direction)
            {
                case Wall.North: Y--; break;
                case Wall.East: X++; break;
                case Wall.South: Y++; break;
                case Wall.West: X--; break;
                default: return MoveResult.Blocked;
            }
            Moves++;
            return AtGoal ? MoveResult.Goal : MoveResult.Moved;
        }
    }
}
