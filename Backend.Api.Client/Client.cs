using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Backend.Api;

public static class Client
{
    public enum RequestType
    {
        Get,
        Post
    }


    public static T ApiRequest<T>(string url, out string stringResponse, Action<string> errorHandlingCallback = null,
        int timeoutInSeconds = 0, string postString = "", RequestType requestType = RequestType.Get)
    {
        stringResponse = null;

        Log.Debug("API request: url: {Url}", url);

        const int max = 5;
        for ( var i = 1; i <= max; i++ )
            try
            {
                using ( HttpClient wc = new() )
                {
                    if ( timeoutInSeconds > 0 ) wc.Timeout = TimeSpan.FromSeconds(timeoutInSeconds);

                    var startTime = DateTime.Now;
                    switch ( requestType )
                    {
                        case RequestType.Get:
                            var response = wc.GetAsync(url).Result;
                            using ( var content = response.Content )
                            {
                                stringResponse = content.ReadAsStringAsync().Result;
                            }

                            break;
                        case RequestType.Post:
                        {
                            var content = new StringContent(postString, Encoding.UTF8, "application/json");
                            var responseContent = wc.PostAsync(url, content).Result.Content;
                            stringResponse = responseContent.ReadAsStringAsync().Result;
                            break;
                        }
                        default:
                            throw new Exception("Unknown RequestType");
                    }

                    var responseTime = DateTime.Now - startTime;

                    Log.Debug(
                        "API response\nurl: {Url}\nResponse time: {ResponseTime} sec.{Response}",
                        url, Math.Round(responseTime.TotalSeconds, 3),
                        string.IsNullOrEmpty(stringResponse) ? " Empty response" : "");
                }

                if ( string.IsNullOrEmpty(stringResponse) ) return default;

                // Log.Write("APIRequest: response: " + contents, Log.Level.Networking);

                if ( typeof(T) == typeof(JsonDocument) )
                {
                    JsonDocument node = null;
                    try
                    {
                        node = JsonDocument.Parse(stringResponse);
                    }
                    catch ( Exception e )
                    {
                        Log.Debug(
                            "API request error for {Url}:\nJSON parsing error:\n{Message}\nResponse: {Response}",
                            url, e.Message, stringResponse
                        );
                    }

                    return ( T ) ( object ) node;
                }

                if ( typeof(T) == typeof(JsonNode) )
                {
                    JsonNode node = null;
                    try
                    {
                        node = JsonNode.Parse(stringResponse);
                    }
                    catch ( Exception e )
                    {
                        Log.Debug(
                            "API request error for {Url}:\nJSON parsing error:\n{Message}\nResponse: {Response}",
                            url, e.Message, stringResponse);
                    }

                    return ( T ) ( object ) node;
                }

                throw new Exception($"Unsupported output type {typeof(T).FullName}");
            }
            catch ( Exception e )
            {
                string logMessage;
                if ( e.Message.Contains("The operation has timed out.") ||
                     e.Message.Contains(
                         "A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.") ||
                     e.Message.Contains(
                         "Authentication failed because the remote party has closed the transport stream.")
                   )
                {
                    logMessage = $"API request timeout for {url}";
                    Log.Debug(logMessage);
                }
                else
                {
                    logMessage = $"API request error for {url}:\n{e.Message}";

                    // We don't need stacktrace for known errors like "response ended prematurely".
                    if ( !e.Message.Contains(
                             "An error occurred while sending the request. The response ended prematurely.") &&
                         !e.Message.ToUpper().Contains("TOO MANY REQUESTS") )
                    {
                        var inner = e.InnerException;
                        while ( inner != null )
                        {
                            logMessage += "\n---> " + inner.Message + "\n\n" + inner.StackTrace;

                            inner = inner.InnerException;
                        }

                        logMessage += "\n\n" + e.StackTrace;
                    }

                    if ( errorHandlingCallback == null ) Log.Debug(logMessage);
                }

                errorHandlingCallback?.Invoke(logMessage);

                if ( i < max )
                {
                    Thread.Sleep(1000 * i);
                    Log.Debug("API request for {Url}:\nTrying again...", url);
                }
            }

        return default;
    }

    private static int _maxAttempts = 5;
    public static async Task<JsonDocument> ApiRequestAsync(string url, int timeoutInSeconds = 0, string postString = null, RequestType requestType = RequestType.Get)
    {
        Log.Debug("[API request]: url: " + url);

        using var httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        for (var i = 1; i <= _maxAttempts; i++)
        {
            try
            {
                string stringResponse;

                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutInSeconds > 0 ? timeoutInSeconds : 100));

                DateTime startTime = DateTime.Now;
                switch (requestType)
                {
                    case RequestType.Get:
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "Other");
                        var response = await httpClient.GetAsync(url, cts.Token);
                        Log.Debug($"[API request]: header code: {response.StatusCode}");
                        using (var content = response.Content)
                        {
                            stringResponse = await content.ReadAsStringAsync(cts.Token);
                            Log.Debug($"[API request]: response: {stringResponse}");
                        }
                        break;
                    case RequestType.Post:
                        {
                            if(string.IsNullOrEmpty(postString)) throw new Exception("Empty POST body");
                            var content = new StringContent(postString, Encoding.UTF8, "application/json");
                            var responsePost = await httpClient.PostAsync(url, content, cts.Token);
                            var responseContent = responsePost.Content;
                            stringResponse = await responseContent.ReadAsStringAsync(cts.Token);
                            break;
                        }
                    default:
                        throw new Exception("Unknown RequestType");
                }
                var responseTime = DateTime.Now - startTime;

                Log.Debug($"API response\nurl: {url}\nResponse time: {Math.Round(responseTime.TotalSeconds, 3)} sec.{(string.IsNullOrEmpty(stringResponse) ? " Empty response" : "")}");

                if (string.IsNullOrEmpty(stringResponse))
                    return default;

                return JsonDocument.Parse(stringResponse);
            }
            catch (Exception e)
            {
                string logMessage;
                if (e.Message.Contains("The operation has timed out.") ||
                    e.Message.Contains("A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.") ||
                    e.Message.Contains("Authentication failed because the remote party has closed the transport stream.")
                    )
                {
                    logMessage = $"API request timeout for {url}";
                    Log.Debug(logMessage);
                }
                else
                {
                    logMessage = $"API request error for {url}:\n{e.Message}";

                    // We don't need stacktrace for known errors like "response ended prematurely".
                    if (!e.Message.Contains("An error occurred while sending the request. The response ended prematurely.") &&
                        !e.Message.ToUpper().Contains("TOO MANY REQUESTS"))
                    {
                        var inner = e.InnerException;
                        while (inner != null)
                        {
                            logMessage += "\n---> " + inner.Message + "\n\n" + inner.StackTrace;

                            inner = inner.InnerException;
                        }

                        logMessage += "\n\n" + e.StackTrace;
                    }

                    Log.Debug(logMessage);
                }

                if (i < _maxAttempts)
                {
                    Thread.Sleep(1000 * i);
                    Log.Debug($"API request for {url}:\nTrying again...");
                }
            }
        }

        return default;
    }
}
