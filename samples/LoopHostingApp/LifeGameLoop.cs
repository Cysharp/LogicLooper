using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.LogicLooper;
using Microsoft.Extensions.Logging;

namespace LoopHostingApp
{
    public class LifeGameLoop
    {
        private static int _gameLoopSeq = 0;
        public static ConcurrentBag<LifeGameLoop> All { get; } = new ConcurrentBag<LifeGameLoop>();

        private readonly ILogger _logger;

        public World World { get; }
        public int Id { get; }

        /// <summary>
        /// Create a new life-game loop and register into the LooperPool.
        /// </summary>
        /// <param name="looperPool"></param>
        /// <param name="logger"></param>
        public static void CreateNew(ILogicLooperPool looperPool, ILogger logger)
        {
            var gameLoop = new LifeGameLoop(logger);
            looperPool.RegisterActionAsync(gameLoop.UpdateFrame);
        }

        private LifeGameLoop(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            Id = Interlocked.Increment(ref _gameLoopSeq);
            World = new World(64, 64);
            World.SetPattern(Patterns.GliderGun, 10, 10);

            _logger.LogInformation($"{nameof(LifeGameLoop)}[{Id}]: Register");

            All.Add(this);
        }

        public bool UpdateFrame(in LogicLooperActionContext ctx)
        {
            if (ctx.CancellationToken.IsCancellationRequested)
            {
                // If LooperPool begins shutting down, IsCancellationRequested will be `true`.
                _logger.LogInformation($"{nameof(LifeGameLoop)}[{Id}]: Shutdown");
                return false;
            }

            // Update the world every update cycle.
            World.Update();

            return World.AliveCount != 0;
        }
    }

    public static class Patterns
    {
        // glider pattern
        public static readonly int[,] Glider = new[,]
        {
            { 1, 1, 1 },
            { 1, 0, 0 },
            { 0, 1, 0 }
        };

        public static readonly int[,] GliderGun = new[,]
        {
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0 },
            { 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        };
    }

    public class World
    {
        private readonly Cell[] _cells;
        private readonly int _width;
        private readonly int _height;

        public bool[,] Snapshot { get; private set; }

        public int AliveCount { get; private set; }

        public World(int width, int height)
        {
            _width = width;
            _height = height;
            _cells = new Cell[width * height];

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    _cells[x + (y * width)] = new Cell();
                }
            }
        }

        public void Set(int x, int y, bool isAlive)
        {
            if (!TryGetCell(x, y, out var cell))
            {
                throw new ArgumentOutOfRangeException();
            }

            cell.IsAlive = isAlive;
        }

        public void SetPattern(int[,] pattern, int offsetX, int offsetY)
        {
            for (var x = 0; x <= pattern.GetUpperBound(0); x++)
            {
                for (var y = 0; y <= pattern.GetUpperBound(1); y++)
                {
                    Set(offsetX + x, offsetY + y, pattern[x, y] == 1);
                }
            }
        }

        public void Update()
        {
            var count = 0;
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    if (TryGetCell(x, y, out var cell))
                    {
                        cell.NestState = GetCellNextState(x, y) switch
                        {
                            CellState.Alive => true,
                            CellState.Dead => false,
                            CellState.Remain => cell.IsAlive,
                            _ => throw new ArgumentOutOfRangeException()
                        };
                    }
                }
            }

            // Apply new state and take a snapshot.
            var snapshot = new bool[_width, _height];
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    if (TryGetCell(x, y, out var cell))
                    {
                        cell.IsAlive = cell.NestState;
                        if (cell.IsAlive)
                        {
                            count++;
                        }

                        snapshot[x, y] = cell.IsAlive;
                    }
                }
            }

            AliveCount = count;
            Snapshot = snapshot;
        }

        private enum CellState
        {
            Alive,
            Dead,
            Remain,
        }

        private CellState GetCellNextState(int x, int y)
        {
            var livingCellsCount = 0;
            for (var x2 = x - 1; x2 <= x + 1; x2++)
            {
                for (var y2 = y - 1; y2 <= y + 1; y2++)
                {
                    if (x2 == x && y2 == y) continue;

                    if (TryGetCell(x2, y2, out var cell) && cell.IsAlive)
                    {
                        livingCellsCount++;
                    }
                }
            }

            return livingCellsCount == 2
                ? CellState.Remain
                : livingCellsCount == 3
                    ? CellState.Alive
                    : CellState.Dead;
        }

        private bool TryGetCell(int x, int y, out Cell cell)
        {
            if (x >= _width || y >= _height || x < 0 || y < 0)
            {
                cell = null;
                return false;
            }

            cell = _cells[x + (y * _width)];

            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    if (TryGetCell(x, y, out var cell))
                    {
                        sb.Append(cell.IsAlive ? "x" : ".");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public void Dump()
        {
            Console.WriteLine(Snapshot);
            Console.WriteLine();
        }

        public class Cell
        {
            public bool IsAlive { get; set; }
            public bool NestState { get; set; }
        }
    }

}