using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUDUS.Services {
    // Service to merge heroes by drag-drop with cascade logic
    public class HeroMergeService {
        private readonly AdbService _adb;
        public HeroMergeService(AdbService adb) => _adb = adb;

        public bool MergeHeroes(string deviceId, List<CellResult> cells, int cols, Action<string> log) {
            // Group cells by hero name, map to queues sorted by level
            var heroGroups = cells
                .Where(c => !string.IsNullOrEmpty(c.HeroName) && int.TryParse(c.Level, out var lv) && lv < 4)
                .GroupBy(c => c.HeroName)
                .ToDictionary(
                    g => g.Key,
                    g => new SortedDictionary<int, Queue<CellResult>>(
                        g.GroupBy(c => int.Parse(c.Level))
                         .ToDictionary(gr => gr.Key, gr => new Queue<CellResult>(gr))
                    )
                );
            
            bool anyMergeHappened = false;

            // For each hero type, perform cascade merges
            foreach (var kv in heroGroups) {
                var name = kv.Key;
                var levelMap = kv.Value;
                bool didMerge;
                do {
                    didMerge = false;
                    foreach (var level in levelMap.Keys.OrderBy(l => l).ToList()) {
                        var queue = levelMap[level];
                        if (queue.Count >= 2) {
                            var first = queue.Dequeue();
                            var second = queue.Dequeue();
                            // Perform swipe from second to first
                            var p1 = new Point(
                                first.CellRect.X + first.CellRect.Width / 2,
                                first.CellRect.Y + first.CellRect.Height / 2);
                            var p2 = new Point(
                                second.CellRect.X + second.CellRect.Width / 2,
                                second.CellRect.Y + second.CellRect.Height / 2);
                            _adb.RunShellPersistent($"input swipe {p2.X} {p2.Y} {p1.X} {p1.Y} 200");
                            Thread.Sleep(300);
                            var pos1 = $"{first.Index / cols}_{first.Index % cols}";
                            var pos2 = $"{second.Index / cols}_{second.Index % cols}";
                            log?.Invoke($"Merged {name} lvl{level}: cell {pos2}->{pos1}");

                            // Promote to next level
                            var nextLevel = level + 1;
                            if (!levelMap.ContainsKey(nextLevel))
                                levelMap[nextLevel] = new Queue<CellResult>();
                            levelMap[nextLevel].Enqueue(first);
                            didMerge = true;
                            anyMergeHappened = true;
                            break;
                        }
                    }
                } while (didMerge);
            }
            return anyMergeHappened;
        }
    }
}
