using System;
using System.Collections.Generic;

namespace MinikDuello.Domain.Rounds
{
    /// <summary>Çocuk dostu labirent: tek çözümlü (mükemmel) labirent; hedef başlangıçtan en uzak hücredir.</summary>
    public static class MazeGenerator
    {
        public static MazeRound Generate(int width, int height, Random random)
        {
            if (width < 2 || height < 2) throw new ArgumentOutOfRangeException(nameof(width), "En az 2x2 olmalı.");
            var walls = new Wall[width * height];
            for (int i = 0; i < walls.Length; i++) walls[i] = Wall.North | Wall.East | Wall.South | Wall.West;

            var visited = new bool[width * height];
            var stack = new Stack<int>();
            int start = 0;
            visited[start] = true;
            stack.Push(start);

            while (stack.Count > 0)
            {
                int current = stack.Peek();
                int cx = current % width;
                int cy = current / width;
                var options = new List<int[]>();
                if (cy > 0 && !visited[(cy - 1) * width + cx]) options.Add(new[] { cx, cy - 1, (int)Wall.North, (int)Wall.South });
                if (cx < width - 1 && !visited[cy * width + cx + 1]) options.Add(new[] { cx + 1, cy, (int)Wall.East, (int)Wall.West });
                if (cy < height - 1 && !visited[(cy + 1) * width + cx]) options.Add(new[] { cx, cy + 1, (int)Wall.South, (int)Wall.North });
                if (cx > 0 && !visited[cy * width + cx - 1]) options.Add(new[] { cx - 1, cy, (int)Wall.West, (int)Wall.East });

                if (options.Count == 0)
                {
                    stack.Pop();
                    continue;
                }
                int[] pick = options[random.Next(options.Count)];
                int next = pick[1] * width + pick[0];
                walls[current] &= ~(Wall)pick[2];
                walls[next] &= ~(Wall)pick[3];
                visited[next] = true;
                stack.Push(next);
            }

            int[] distance = Distances(walls, width, height, 0, 0);
            int goal = 0;
            for (int i = 0; i < distance.Length; i++)
            {
                if (distance[i] > distance[goal]) goal = i;
            }

            return new MazeRound
            {
                PromptKey = "mazeGoal",
                Width = width, Height = height, Walls = walls,
                StartX = 0, StartY = 0, GoalX = goal % width, GoalY = goal / width
            };
        }

        public static bool CanMove(MazeRound maze, int x, int y, Wall direction)
        {
            return (maze.Walls[y * maze.Width + x] & direction) == Wall.None;
        }

        /// <summary>Kaynaktan her hücreye adım sayısı (ulaşılamayan = -1).</summary>
        public static int[] Distances(Wall[] walls, int width, int height, int fromX, int fromY)
        {
            var dist = new int[width * height];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var queue = new Queue<int>();
            dist[fromY * width + fromX] = 0;
            queue.Enqueue(fromY * width + fromX);

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int x = cur % width;
                int y = cur / width;
                TryStep(walls, width, dist, queue, cur, x, y - 1, walls[cur], Wall.North, height);
                TryStep(walls, width, dist, queue, cur, x + 1, y, walls[cur], Wall.East, height);
                TryStep(walls, width, dist, queue, cur, x, y + 1, walls[cur], Wall.South, height);
                TryStep(walls, width, dist, queue, cur, x - 1, y, walls[cur], Wall.West, height);
            }
            return dist;
        }

        private static void TryStep(Wall[] walls, int width, int[] dist, Queue<int> queue, int cur, int nx, int ny, Wall currentWalls, Wall dir, int height)
        {
            if ((currentWalls & dir) != Wall.None) return;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height) return;
            int n = ny * width + nx;
            if (dist[n] >= 0) return;
            dist[n] = dist[cur] + 1;
            queue.Enqueue(n);
        }
    }
}
