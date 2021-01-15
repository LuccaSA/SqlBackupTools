using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;
using SqlBackupTools.Helpers;
using SqlBackupTools.Restore;

namespace SqlBackupTools.Notification
{
    public static class NotificationExtensions
    {
        public static async Task SendMailAsync(this ReportState state,string email, string smtp, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(smtp))
            {
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("SQL backup", email));
            message.To.Add(new MailboxAddress("SQL backup", email));

            message.Subject =
                $"Restore Backup {Environment.MachineName} : {state.Restored.Count}/{state.TotalProcessed}";

            var sb = new StringBuilder();
            sb.AppendLine($"Restore finished in {state.TotalTime.HumanizedTimeSpan()}");
            sb.AppendLine();

            if (state.Errors.Count != 0)
            {
                sb.AppendLine($"Errors : {state.Errors.Count}");
                foreach (var e in state.Errors)
                {
                    sb.AppendLine($"{e.Item.Name} : {e.Error}");
                }

                sb.AppendLine();
            }

            if (state.BackupNotFoundDbExists.Count != 0)
            {
                sb.AppendLine($"Warnings : {state.BackupNotFoundDbExists.Count}");
                foreach (var w in state.BackupNotFoundDbExists)
                {
                    sb.AppendLine($"Db {w.Name} in state {w.State}, no .bak found");
                }
                sb.AppendLine();
            }

            if (state.MissingFull.Count != 0)
            {
                sb.AppendLine($"Missing .bak : {state.MissingFull.Count}");
                foreach (var w in state.MissingFull)
                {
                    sb.AppendLine($"Missing .bak in folder " + w.Path);
                }
                sb.AppendLine();
            }

            if (state.Restored.Count != 0)
            {
                sb.AppendLine($"OK : {state.Restored.Count}");
                foreach (var o in state.Restored)
                {
                    sb.AppendLine($"{o.Name}");
                }
                sb.AppendLine();
            }

            message.Body = new TextPart("plain")
            {
                Text = sb.ToString()
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp, 25, false, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
