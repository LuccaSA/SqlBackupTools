using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SqlBackupTools.Helpers;

namespace SqlBackupTools.Restore
{
    public static class BackupDiscoveryExtension
    {
        public static Task PrepareRestoreJobAsync(this RestoreState state)
        {
            return Task.Run(async () =>
            {
                await Task.Yield();
                state.Loggger.Information("Crawling folders");

                var sourceFolders = GetSourceFolder(state);

                var po = new ParallelOptions
                {
                    MaxDegreeOfParallelism = state.RestoreCommand.Threads * 4
                };

                var explicitDatabases = state.RestoreCommand.Databases?.ToHashSet(StringComparer.InvariantCultureIgnoreCase);

                bool IsExcluded(DirectoryInfo directoryInfo)
                {
                    return state.RestoreCommand.IsDatabaseIgnored(directoryInfo.Name);
                }

                var restoreItems = new ConcurrentBag<(int sourceId, RestoreItem item)>();

                Stopwatch sw = Stopwatch.StartNew();
                Parallel.ForEach(sourceFolders.MixSourceFolders(), po,
                    i =>
                    {
                        if (IsExcluded(i.directory))
                        {
                            return;
                        }

                        // ignore folder without FULL backup
                        if (RestoreItem.TryCreateRestoreItem(i.directory, state, out RestoreItem ri))
                        {
                            if (explicitDatabases != null && explicitDatabases.Count != 0)
                            {
                                if (explicitDatabases.Contains(ri.Name))
                                {
                                    restoreItems.Add((i.sourceId, ri));
                                }
                            }
                            else
                            {
                                restoreItems.Add((i.sourceId, ri));
                            }
                        }
                    });

                var excluded = new HashSet<(int, string)>();
                foreach (var dup in restoreItems.GroupBy(r => r.item.Name).Where(g => g.Count() > 1))
                {
                    var mostRecent = dup
                        .Select(i => new { item = i, files = i.item.BaseDirectoryInfo.GetFiles("*", SearchOption.AllDirectories) })
                        .Where(k => k.files.Length != 0)
                        .OrderByDescending(i => i.files.Select(i => i.BackupDate()).Max())
                        .Select(i => i.item)
                        .FirstOrDefault();

                    if (mostRecent.item == null)
                    {
                        continue;
                    }

                    var t = dup.Where(i => i != mostRecent).Select(i => i.item.Full).ToList();

                    if (!state.RestoreCommand.IsDatabaseIgnored(dup.Key))
                    {
                        state.DuplicatesExcluded.Add((
                            name: dup.Key,
                            count: dup.Count(),
                            excluded: t));
                    }
                    foreach (var d in dup.Where(i => i != mostRecent))
                    {
                        excluded.Add((d.sourceId, d.item.Name));
                    }
                }

                foreach (var dup in state.DuplicatesExcluded)
                {
                    state.Loggger.Warning($"Duplicate found ({dup.count}) for {dup.name}");
                    state.Loggger.Debug("Total list : " + Environment.NewLine + string.Join(Environment.NewLine, dup.excluded.Select(i => i.FullName)));
                    foreach (var d in dup.excluded)
                    {
                        state.Loggger.Warning("Excluding " + d.FullName);
                    }
                }

                state.Directories = restoreItems
                    .Where(i => !excluded.Contains((i.sourceId, i.item.Name)))
                            .FlattenSources(state.RestoreCommand.ReverseOrder).ToList();

                sw.Stop();
                state.Loggger.Information($"Crawled in " + sw.Elapsed.HumanizedTimeSpan());
                state.Loggger.Information($"Total FULL backup size : {state.Directories.Sum(i => i.FullSize).HumanizeSize()}");
            });
        }

        private static IEnumerable<DirectoryInfo> GetSourceFolder(RestoreState state)
        {
            IEnumerable<DirectoryInfo> sourceFolders;
            if (state.RestoreCommand.IsUncheckedModeEnable)
            {
                sourceFolders = state.RestoreCommand.Unchecked;
            }
            else
            {
                if (state.RestoreCommand.BackupFolders == null)
                {
                    throw new SourceFolderException();
                }

                foreach (var folder in state.RestoreCommand.BackupFolders)
                {
                    if (folder == null)
                    {
                        throw new SourceFolderException();
                    }

                    if (!folder.Exists)
                    {
                        throw new SourceFolderException();
                    }
                }

                sourceFolders = state.RestoreCommand.BackupFolders;
            }

            return sourceFolders;
        }

        private static IEnumerable<(int sourceId, DirectoryInfo directory)> MixSourceFolders(this IEnumerable<DirectoryInfo> sourceFolders)
        {
            var src = sourceFolders.ToList();
            if (src.Count == 1)
            {
                return src.First().EnumerateDirectories("*", SearchOption.TopDirectoryOnly).Select(i => (0, i));
            }
            else
            {
                return YieldMixed(src);
            }
        }

        private static IEnumerable<RestoreItem> FlattenSources(this IEnumerable<(int sourceId, RestoreItem item)> source, bool reverse)
        {
            var mixed = new List<Queue<RestoreItem>>();
            foreach (var g in source.GroupBy(i => i.sourceId))
            {
                if (reverse)
                {
                    mixed.Add(new Queue<RestoreItem>(g.Select(i => i.item).OrderBy(t => t.FullSize)));
                }
                else
                {
                    mixed.Add(new Queue<RestoreItem>(g.Select(i => i.item).OrderByDescending(t => t.FullSize)));
                }
            }
            if (mixed.Count == 1)
            {
                var q = mixed.First();
                while (q.TryDequeue(out var item))
                {
                    yield return item;
                }
            }
            else
            {
                while (mixed.Count != 0)
                {
                    for (int i = 0; i < mixed.Count; i++)
                    {
                        var innerQueue = mixed[i];

                        if (innerQueue.TryDequeue(out var item))
                        {
                            yield return item;
                        }
                        else
                        {
                            mixed.Remove(innerQueue);
                            break;
                        }
                    }
                }
            }
        }

        private static IEnumerable<(int sourceId, DirectoryInfo directory)> YieldMixed(IEnumerable<DirectoryInfo> src)
        {
            var groups = src
                .Select(i => i
                    .EnumerateDirectories("*", SearchOption.TopDirectoryOnly).GetEnumerator()).ToList();

            while (groups.Count != 0)
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    IEnumerator<DirectoryInfo> innerEnumerator = groups[i];

                    if (innerEnumerator.MoveNext())
                    {
                        yield return (i, innerEnumerator.Current);
                    }
                    else
                    {
                        groups.Remove(groups[i]);
                        break;
                    }
                }
            }
        }
    }
}
