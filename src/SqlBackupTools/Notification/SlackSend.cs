using SqlBackupTools.Helpers;
using SqlBackupTools.Restore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SqlBackupTools.Notification
{
    public class SlackSender
    {
        private readonly SlackClient _slackClient;

        public SlackSender(SlackClient slackClient)
        {
            _slackClient = slackClient;
        }

        public async Task ReportAsync(ReportState state, string slackChannel, string slackSecret,
            bool restoreCommandSlackOnlyOnError)
        {
            if (string.IsNullOrWhiteSpace(slackChannel) ||
                string.IsNullOrWhiteSpace(slackSecret))
            {
                return;
            }

            if (restoreCommandSlackOnlyOnError && !state.Status.HasFlag(ReportStatus.Warning) && !state.Status.HasFlag(ReportStatus.Error))
            {
                return;
            }

            string shell = SlackShell(state);

            var msgRoot = new SlackMessage
            {
                Channel = slackChannel,
                Text = $"{shell} *{state.Info.ServerName}* : {state.Restored.Count}/{state.TotalProcessed} db in {state.TotalTime.HumanizedTimeSpan()}",
            };

            var response = await _slackClient.SendSlackMessageAsync(msgRoot, slackSecret);

            var subMsg = new SlackMessage
            {
                Channel = slackChannel,
                Text = $"Details for {state.Info.ServerName ?? Environment.MachineName}",
                ThreadTs = response.Ts,
                Attachments = new List<Attachment>()
            };

            int fullRestored = 0;
            int diffRestored = 0;
            int logRestored = 0;
            int dropped = 0;
            foreach (var ri in state.Restored)
            {
                fullRestored += ri.StatsFullRestored;
                diffRestored += ri.StatsDiffRestored;
                logRestored += ri.StatsLogRestored;
                dropped += ri.StatsDropped;
            }
            var restored = new List<string>();
            if (fullRestored != 0)
            {
                restored.Add(fullRestored + " FULL");
            }
            if (diffRestored != 0)
            {
                restored.Add(diffRestored + " DIFF");
            }
            if (logRestored != 0)
            {
                restored.Add(logRestored + " LOG");
            }
            if (dropped != 0)
            {
                restored.Add(dropped + " DROPs");
            }

            subMsg.Attachments.Add(new Attachment
            {
                Color = AlertLevel.Info.ToSlackColor(),
                Title = $"Details :",
                Fields = new[]
                {
                    new Field
                    {
                        Value = $"AVG : {(state.AvgRpo != null ? state.AvgRpo.Value.HumanizedTimeSpan() : "none")}",
                        Short = true
                    },
                    new Field
                    {
                        Value = $"Processed in : {state.TotalTime.HumanizedTimeSpan()}",
                        Short = true
                    },
                    new Field
                    {
                        Value =  $" MAX : {(state.MaxRpo != null ? state.MaxRpo.Value.HumanizedTimeSpan() : "none")}",
                        Short = true
                    },
                    new Field
                    {
                        Value = $"Mode : {state.Mode}",
                        Short = true
                    },
                    new Field
                    {
                        Value = string.Join(", ",restored),
                        Short = true
                    },
                    new Field
                    {
                        Value = $"{state.Restored.Count}/{state.TotalProcessed} restored successfully",
                        Short = true
                    }
                }
            });

            if (state.RpoOutliers.Count != 0)
            {
                subMsg.Attachments.Add(new Attachment
                {
                    Color = AlertLevel.Warning.ToSlackColor(),
                    Title = $"RPO Outliers :",
                    Fields = state.RpoOutliers.Select(b => new Field
                    {
                        Title = b.Name,
                        Value = b.Rpo.HumanizedTimeSpan(),
                        Short = true
                    }).ToArray()
                });
            }

            if (state.DuplicatesExcluded.Count != 0)
            {
                subMsg.Attachments.Add(new Attachment
                {
                    Color = AlertLevel.Warning.ToSlackColor(),
                    Title = $"Duplicated databases :",
                    Fields = state.DuplicatesExcluded.Select(b => new Field
                    {
                        Title = b.count + " x " + b.name,
                        Value = "Excluding : " + Environment.NewLine + string.Join(Environment.NewLine, b.excluded.Select(i => i.FullName)),
                        Short = false
                    }).ToArray()
                });
            }

            if (state.BackupNotFoundDbExists.Count != 0)
            {
                subMsg.Attachments.Add(new Attachment
                {
                    Color = AlertLevel.Warning.ToSlackColor(),
                    Title = $"Database exists but no backup found :",
                    Fields = state.BackupNotFoundDbExists.Select(b => new Field
                    {
                        Value = b.Name,
                        Short = true
                    }).ToArray()
                });
            }

            if (state.MissingFull.Count != 0)
            {
                subMsg.Attachments.Add(new Attachment
                {
                    Color = AlertLevel.Info.ToSlackColor(),
                    Title = $"Source folder exists but no backup found :",
                    Fields = state.MissingFull
                        .GroupBy(i => i.Path?.Root.Name ?? "")
                        .Select(i => new Field
                        {
                            Title = i.Key + " :",
                            Value = string.Join(Environment.NewLine, i.Select(k => k.Path.Name)),
                            Short = false
                        }).ToArray()
                });
            }

            if (state.MissingFullMoreThan24Hours.Count != 0)
            {
                subMsg.Attachments.Add(new Attachment
                {
                    Color = AlertLevel.Warning.ToSlackColor(),
                    Title = $"Source folder exists for more than 24h but no backup found :",
                    Fields = state.MissingFullMoreThan24Hours
                        .GroupBy(i => i.Path?.Root.Name ?? "")
                        .Select(i => new Field
                        {
                            Title = i.Key + " :",
                            Value = string.Join(Environment.NewLine, i.Select(k => k.Path.Name)),
                            Short = false
                        }).ToArray()
                });
            }

            if (state.Errors.Count != 0)
            {
                subMsg.Attachments.Add(new Attachment
                {
                    Color = AlertLevel.Error.ToSlackColor(),
                    Title = $"Exception while restoring :",
                    Fields = state.Errors
                        .GroupBy(i => i.Item?.BaseDirectoryInfo?.Root.Name ?? "")
                        .Select(i => new Field
                        {
                            Title = i.Key + " :",
                            Value = string.Join(Environment.NewLine, i.Select(k => k.Item.Name + ": " + k.Error)),
                            Short = false
                        }).ToArray()
                });
            }

            await _slackClient.SendSlackMessageAsync(subMsg, slackSecret);
        }

        private static string SlackShell(ReportState state)
        {
            if (state.Status.HasFlag(ReportStatus.Error))
            {
                return ":redshell:";
            }
            if (state.Status.HasFlag(ReportStatus.Warning))
            {
                return ":warning:";
            }
            return ":greenshell:";
        }
    }

    public class SlackReport
    {
        public string BlockName { get; set; }
        public string Message { get; set; }
        public AlertLevel Alert { get; set; }
    }
}
