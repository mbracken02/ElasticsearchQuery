﻿// Licensed under the Apache 2.0 License. See LICENSE.txt in the project root for more information.

using ElasticLinq.Utility;
using ElasticLinq.Logging;
using ElasticLinq.Request;
using ElasticLinq.Response.Model;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ElasticLinq
{
    /// <summary>
    /// Specifies connection parameters for Elasticsearch.
    /// </summary>
    [DebuggerDisplay("{Endpoint.ToString(),nq}{Index,nq}")]
    public class ElasticConnection : BaseElasticConnection, IDisposable
    {
        private readonly string[] parameterSeparator = { "&" };

        /// <summary>
        /// Create a new ElasticConnection with the given parameters defining its properties.
        /// </summary>
        /// <param name="endpoint">The URL endpoint of the Elasticsearch server.</param>
        /// <param name="userName">UserName to use to connect to the server (optional).</param>
        /// <param name="password">Password to use to connect to the server (optional).</param>
        /// <param name="timeout">TimeSpan to wait for network responses before failing (optional, defaults to 10 seconds).</param>
        /// <param name="index">Name of the index to use on the server (optional).</param>
        /// <param name="options">Additional options that specify how this connection should behave.</param>
        public ElasticConnection(Uri endpoint, string userName = null, string password = null, TimeSpan? timeout = null, string index = null, ElasticConnectionOptions options = null)
            : this(new HttpClientHandler(), endpoint, userName, password, index, timeout, options) { }


        /// <summary>
        /// Create a new ElasticConnection with the given parameters for internal testing.
        /// </summary>
        /// <param name="innerMessageHandler">The HttpMessageHandler used to intercept network requests for testing.</param>
        /// <param name="endpoint">The URL endpoint of the Elasticsearch server.</param>
        /// <param name="userName">UserName to use to connect to the server (optional).</param>
        /// <param name="password">Password to use to connect to the server (optional).</param>
        /// <param name="timeout">TimeSpan to wait for network responses before failing (optional, defaults to 10 seconds).</param>
        /// <param name="index">Name of the index to use on the server (optional).</param>
        /// <param name="options">Additional options that specify how this connection should behave.</param>
        internal ElasticConnection(HttpMessageHandler innerMessageHandler, Uri endpoint, string userName = null, string password = null, string index = null, TimeSpan? timeout = null, ElasticConnectionOptions options = null)
            : base(index, timeout, options)
        {
            Argument.EnsureNotNull(nameof(endpoint), endpoint);

            this.Endpoint = endpoint;

            var httpClientHandler = innerMessageHandler as HttpClientHandler;
            if (httpClientHandler != null && httpClientHandler.SupportsAutomaticDecompression)
                httpClientHandler.AutomaticDecompression = DecompressionMethods.GZip;

            HttpClient = new HttpClient(new ForcedAuthHandler(userName, password, innerMessageHandler), true);
        }

        /// <summary>
        /// The HttpClient used for issuing HTTP network requests.
        /// </summary>
        internal HttpClient HttpClient { get; private set; }

        /// <summary>
        /// The Uri that specifies the public endpoint for the server.
        /// </summary>
        /// <example>http://myserver.example.com:9200</example>
        public Uri Endpoint { get; }

        /// <summary>
        /// Dispose of this ElasticConnection and any associated resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (HttpClient != null)
                {
                    HttpClient.Dispose();
                    HttpClient = null;
                }
            }
        }

        /// <inheritdoc/>
        public override async Task<ElasticResponse> SearchAsync(
            string body,
            SearchRequest searchRequest,
            CancellationToken token,
            ILog log)
        {
            var uri = GetSearchUri(searchRequest);

            log.Debug(null, null, "Request: POST {0}", uri);
            log.Debug(null, null, "Body:\n{0}", body);

            using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, uri) {Content = new StringContent(body)})
            {
                requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                try
                {
                    using (var response = await SendRequestAsync(requestMessage, token, log).ConfigureAwait(false))
                    {
                        using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            return ParseResponse(responseStream, log);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is HttpRequestException)
                    {
                    }

                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public override Uri GetSearchUri(SearchRequest searchRequest)
        {
            var builder = new UriBuilder(Endpoint);
            builder.Path += (Index ?? "") + "";

            //if (!String.IsNullOrEmpty(searchRequest.))
            //    builder.Path += searchRequest.DocumentType + "/";

            builder.Path += "_search";

            var parameters = builder.Uri.GetComponents(UriComponents.Query, UriFormat.Unescaped)
                .Split(parameterSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('='))
                .ToDictionary(k => k[0], v => v.Length > 1 ? v[1] : null);

            if (Options.Pretty)
                parameters["pretty"] = "true";

            builder.Query = String.Join("&", parameters.Select(p => p.Value == null ? p.Key : p.Key + "=" + p.Value));

            return builder.Uri;
        }

        private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage requestMessage, CancellationToken token, ILog log)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await HttpClient.SendAsync(requestMessage, token).ConfigureAwait(false);
            stopwatch.Stop();

            log.Debug(null, null, "Response: {0} {1} (in {2}ms)", (int)response.StatusCode, response.StatusCode, stopwatch.ElapsedMilliseconds);

            response.EnsureSuccessStatusCode();

            return response;
        }

        internal static ElasticResponse ParseResponse(Stream responseStream, ILog log)
        {
            var stopwatch = Stopwatch.StartNew();

            using (var textReader = new JsonTextReader(new StreamReader(responseStream)))
            {
                var results = new JsonSerializer().Deserialize<ElasticResponse>(textReader);
                stopwatch.Stop();

                var resultSummary = String.Join(", ", GetResultSummary(results));
                log.Debug(null, null, "Deserialized {0} bytes into {1} in {2}ms", responseStream.Length, resultSummary, stopwatch.ElapsedMilliseconds);

                return results;
            }
        }

        internal static IEnumerable<string> GetResultSummary(ElasticResponse results)
        {
            if (results == null)
            {
                yield return "nothing";
            }
            else
            {
                if (results.hits?.hits != null && results.hits.hits.Length > 0)
                    yield return results.hits.hits.Length + " hits";
            }
        }
    }
}