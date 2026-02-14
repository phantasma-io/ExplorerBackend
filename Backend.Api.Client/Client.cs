using System;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Backend.Api;

public static class Client
{
    // Shared client to reuse connections and reduce per-request overhead.
    private static readonly HttpClient SharedClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Other");
        return httpClient;
    }

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
        for (var i = 1; i <= max; i++)
            try
            {
                var effectiveTimeout = timeoutInSeconds > 0 ? timeoutInSeconds : 100;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(effectiveTimeout));

                var startTime = DateTime.Now;
                switch (requestType)
                {
                    case RequestType.Get:
                        {
                            using var response = SharedClient.GetAsync(url, cts.Token).GetAwaiter().GetResult();
                            stringResponse = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
                            break;
                        }
                    case RequestType.Post:
                        {
                            using var content = new StringContent(postString, Encoding.UTF8, "application/json");
                            using var response = SharedClient.PostAsync(url, content, cts.Token).GetAwaiter().GetResult();
                            stringResponse = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
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

                if (string.IsNullOrEmpty(stringResponse)) return default;

                // Log.Write("APIRequest: response: " + contents, Log.Level.Networking);

                if (typeof(T) == typeof(JsonDocument))
                {
                    JsonDocument node = null;
                    try
                    {
                        node = JsonDocument.Parse(stringResponse);
                    }
                    catch (Exception e)
                    {
                        Log.Debug(
                            "API request error for {Url}:\nJSON parsing error:\n{Message}\nResponse: {Response}",
                            url, e.Message, stringResponse
                        );
                    }

                    return (T)(object)node;
                }

                if (typeof(T) == typeof(JsonNode))
                {
                    JsonNode node = null;
                    try
                    {
                        node = JsonNode.Parse(stringResponse);
                    }
                    catch (Exception e)
                    {
                        Log.Debug(
                            "API request error for {Url}:\nJSON parsing error:\n{Message}\nResponse: {Response}",
                            url, e.Message, stringResponse);
                    }

                    return (T)(object)node;
                }

                throw new Exception($"Unsupported output type {typeof(T).FullName}");
            }
            catch (Exception e)
            {
                string logMessage;
                if (e.Message.Contains("The operation has timed out.") ||
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
                    if (!e.Message.Contains(
                             "An error occurred while sending the request. The response ended prematurely.") &&
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

                    if (errorHandlingCallback == null) Log.Debug(logMessage);
                }

                errorHandlingCallback?.Invoke(logMessage);

                if (i < max)
                {
                    Thread.Sleep(1000 * i);
                    Log.Debug("API request for {Url}:\nTrying again...", url);
                }
            }

        return default;
    }

    private static JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
    private static int _maxAttempts = 5;

    private static bool IsTransientRpcFailure(Exception exception)
    {
        while (exception != null)
        {
            if (exception is TimeoutException or TaskCanceledException or OperationCanceledException
                or HttpRequestException or SocketException)
                return true;

            var message = exception.Message?.ToLowerInvariant() ?? "";
            if (message.Contains("operation was canceled") ||
                message.Contains("timed out") ||
                message.Contains("connection refused") ||
                message.Contains("response ended prematurely") ||
                message.Contains("unable to connect") ||
                message.Contains("no such host") ||
                message.Contains("name or service not known") ||
                message.Contains("error occurred while sending the request"))
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }

    public static async Task<(T, int)> ApiRequestAsync<T>(string url, int timeoutInSeconds = 0, string postString = null, RequestType requestType = RequestType.Get)
    {
        Log.Debug("[API request]: url: " + url);

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
                        {
                            using var response = await SharedClient.GetAsync(url, cts.Token);
                            if (!response.IsSuccessStatusCode)
                            {
                                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                                Log.Warning("[API request]: {Url} returned {StatusCode}: {Body}", url,
                                    response.StatusCode, errorBody);

                                if (i < _maxAttempts)
                                {
                                    await Task.Delay(1000 * i, cts.Token);
                                    Log.Debug("API request for {Url}:\nRetrying after non-success status...", url);
                                    continue;
                                }

                                throw new Exception($"API returned {(int)response.StatusCode} {response.StatusCode}");
                            }

                            Log.Debug($"[API request]: header code: {response.StatusCode}");
                            stringResponse = await response.Content.ReadAsStringAsync(cts.Token);
                            Log.Debug($"[API request]: response: {stringResponse}");
                            break;
                        }
                    case RequestType.Post:
                        {
                            if (string.IsNullOrEmpty(postString)) throw new Exception("Empty POST body");
                            var content = new StringContent(postString, Encoding.UTF8, "application/json");
                            using var responsePost = await SharedClient.PostAsync(url, content, cts.Token);
                            if (!responsePost.IsSuccessStatusCode)
                            {
                                var errorBody = await responsePost.Content.ReadAsStringAsync(cts.Token);
                                Log.Warning("[API request]: {Url} returned {StatusCode}: {Body}", url,
                                    responsePost.StatusCode, errorBody);

                                if (i < _maxAttempts)
                                {
                                    await Task.Delay(1000 * i, cts.Token);
                                    Log.Debug("API request for {Url}:\nRetrying after non-success status...", url);
                                    continue;
                                }

                                throw new Exception($"API returned {(int)responsePost.StatusCode} {responsePost.StatusCode}");
                            }

                            stringResponse = await responsePost.Content.ReadAsStringAsync(cts.Token);
                            break;
                        }
                    default:
                        throw new Exception("Unknown RequestType");
                }
                var responseTime = DateTime.Now - startTime;

                Log.Debug($"API response\nurl: {url}\nResponse time: {Math.Round(responseTime.TotalSeconds, 3)} sec.{(string.IsNullOrEmpty(stringResponse) ? " Empty response" : "")}");

                if (string.IsNullOrEmpty(stringResponse))
                    return default;

                return (JsonSerializer.Deserialize<T>(stringResponse, jsonSerializerOptions), stringResponse.Length);
            }
            catch (Exception e)
            {
                var logMessage =
                    $"API request attempt {i}/{_maxAttempts} failed for {url}: {e.GetType().Name} {e.Message}";

                var transientFailure = IsTransientRpcFailure(e);
                if (transientFailure && !Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                {
                    // High-volume block fetch retries can fail in bursts when RPC is unstable.
                    // Keep per-request retries in debug only; caller-level handlers emit compact warnings.
                    Log.Debug(logMessage);
                }
                else
                {
                    Log.Error(e, logMessage);
                }

                if (i < _maxAttempts)
                {
                    Thread.Sleep(1000 * i);
                    Log.Debug("API request for {Url}:\nTrying again...", url);
                }
                else
                {
                    throw;
                }
            }

        }

        throw new Exception($"API request failed after {_maxAttempts} attempts for {url}");
    }
}
