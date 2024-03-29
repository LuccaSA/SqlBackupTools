using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Serilog;

namespace SqlBackupTools.Notification
{
    public class SlackClient
    {
        private readonly ILogger _logger;
        private static string _slackUri = "https://slack.com";
        private static JsonSerializerOptions _opt = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        public SlackClient(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<SlackResponse> SendSlackMessageAsync(SlackMessage message, string slackSecret)
        {
            try
            {
                var client = new HttpClient { BaseAddress = new Uri(_slackUri) };
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", slackSecret);

                var json = JsonSerializer.Serialize(message, _opt);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var ok = await client.PostAsync("api/chat.postMessage", content);
                if (!ok.IsSuccessStatusCode)
                {
                    throw new NotificationException(ok.StatusCode.ToString());
                }

                var resp = await ok.Content.ReadAsStringAsync();
                SlackResponse response;
                try
                {
                    response = JsonSerializer.Deserialize<SlackResponse>(resp, _opt);
                    if (!response.Ok)
                    {
                        throw new NotificationException(response.Error);
                    }
                }
                catch (Exception ee)
                {
                    _logger.Error(ee, json);
                    _logger.Error(ee, resp);
                    throw;
                }

                return response;
            }
            catch (Exception e)
            {
                _logger.Error(e, e.Message);
            }
            return null;
        }
    }


    public class SlackMessage
    {
        public string Channel { get; set; }
        public string Text { get; set; }
        [JsonPropertyName("thread_ts")]
        public string ThreadTs { get; set; }
        public List<Attachment> Attachments { get; set; }

    }

    public class Attachment
    {
        public string Fallback { get; set; }
        public string Color { get; set; }
        public string Pretext { get; set; }

        public string Title { get; set; }
        public string TitleLink { get; set; }
        public string Text { get; set; }
        public Field[] Fields { get; set; }
    }

    public class Field
    {
        public string Title { get; set; }
        public string Value { get; set; }
        public bool Short { get; set; }
    }


    public enum AlertLevel
    {
        Info,
        Warning,
        Error
    }

    public static class AlertHelper
    {
        public static string ToSlackColor(this AlertLevel alertLevel)
        {
            switch (alertLevel)
            {
                case AlertLevel.Info:
                    return "good";
                case AlertLevel.Warning:
                    return "warning";
                case AlertLevel.Error:
                    return "danger";
                default:
                    throw new ArgumentOutOfRangeException(nameof(alertLevel), alertLevel, null);
            }
        }
    }



    public class SlackResponse
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public string Channel { get; set; }
        public string Ts { get; set; }
        public Message Message { get; set; }
    }

    public class Message
    {
        public string Text { get; set; }
        public string Username { get; set; }
        public string BotId { get; set; }
        public Attachment[] Attachments { get; set; }
        public string Type { get; set; }
        public string Subtype { get; set; }
        public string Ts { get; set; }
    }

    // ------------------------
     
}
