using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Serilog;

namespace GhostDevs.Api
{
    public static class Client
    {
        public enum RequestType
        {
            GET,
            POST
        }
        public static T APIRequest<T>(string url, out string stringResponse, Action<string> errorHandlingCallback = null, int timeoutInSeconds = 0, string postString = "", RequestType requestType = RequestType.GET)
        {
            stringResponse = null;

            Log.Debug("API request: url: " + url);

            int max = 5;
            for (int i = 1; i <= max; i++)
            {
                try
                {
                    using (var wc = new HttpClient())
                    {
                        if (timeoutInSeconds > 0)
                            wc.Timeout = TimeSpan.FromSeconds(timeoutInSeconds);

                        DateTime startTime = DateTime.Now;
                        switch (requestType)
                        {
                            case RequestType.GET:
                                var response = wc.GetAsync(url).Result;
                                using (var content = response.Content)
                                {
                                    stringResponse = content.ReadAsStringAsync().Result;
                                }
                                break;
                            case RequestType.POST:
                                {
                                    var content = new StringContent(postString, System.Text.Encoding.UTF8, "application/json");
                                    var responseContent = wc.PostAsync(url, content).Result.Content;
                                    stringResponse = responseContent.ReadAsStringAsync().Result;
                                    break;
                                }
                            default:
                                throw new Exception("Unknown RequestType");
                        }
                        TimeSpan responseTime = DateTime.Now - startTime;

                        Log.Debug($"API response\nurl: {url}\nResponse time: {Math.Round(responseTime.TotalSeconds, 3)} sec.{(String.IsNullOrEmpty(stringResponse) ? " Empty response" : "")}");
                    }

                    if (String.IsNullOrEmpty(stringResponse))
                        return default;

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
                            Log.Debug($"API request error for {url}:\nJSON parsing error:\n{e.Message}\nResponse: {stringResponse}");
                        }
                        return (T)(object)node;
                    }
                    else if (typeof(T) == typeof(JsonNode))
                    {
                        JsonNode node = null;
                        try
                        {
                            node = JsonNode.Parse(stringResponse);
                        }
                        catch (Exception e)
                        {
                            Log.Debug($"API request error for {url}:\nJSON parsing error:\n{e.Message}\nResponse: {stringResponse}");
                        }
                        return (T)(object)node;
                    }
                    else
                    {
                        throw new Exception($"Unsupported output type {typeof(T).FullName}");
                    }
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

                        if (errorHandlingCallback == null)
                            Log.Debug(logMessage);
                    }

                    errorHandlingCallback?.Invoke(logMessage);

                    if (i < max)
                    {
                        Thread.Sleep(1000 * i);
                        Log.Debug($"API request for {url}:\nTrying again...");
                    }
                }
            }

            return default;
        }
        private class RpcRequest
        {
            public string jsonrpc { get; set; }
            public string method { get; set; }
            public string id { get; set; }
            public object[] @params { get; set; }
        }
        public static JsonDocument RPCRequest(string url, string method, out string stringResponse, Action<string> errorHandlingCallback = null, int timeoutInSeconds = 0, params object[] parameters)
        {
            var rpcRequest = new RpcRequest
            {
                jsonrpc = "2.0",
                method = method,
                id = "1",
                @params = parameters
            };

            var json = JsonSerializer.Serialize(rpcRequest);

            Log.Debug($"RPC request\nurl: {url}\njson: {json}");

            return APIRequest<JsonDocument>(url, out stringResponse, errorHandlingCallback, timeoutInSeconds, json, RequestType.POST);
        }
    }
}
