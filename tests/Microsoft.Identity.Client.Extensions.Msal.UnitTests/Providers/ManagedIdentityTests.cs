﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Identity.Client.Extensions.Msal.Providers
{
    [TestClass]
    public class ManagedIdentityTests
    {
        [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification = "Fake Secret")]
        private const string AccessToken = "abcdefg"; 
        private const string RefreshToken = "hijklmn";
        private const string ExpiresOn = "1506484173";
        private const string AzureManagementVMManagedIdentityJson = @"{
          ""access_token"": """ + AccessToken + @""",
          ""refresh_token"": """ + RefreshToken + @""",
          ""expires_in"": ""3599"",
          ""expires_on"": """ + ExpiresOn + @""",
          ""not_before"": ""1506480273"",
          ""resource"": ""https://management.azure.com/"",
          ""token_type"": ""Bearer""
        }";

        private const string AzureAppServiceManagedIdentityJson = @"{
          ""access_token"": """ + AccessToken + @""",
          ""expires_on"": ""4/10/2019 6:27:14 AM +00:00"",
          ""resource"": ""https://management.azure.com/"",
          ""token_type"": ""Bearer""
        }";

        [TestInitialize]
        public void TestInitialize()
        {
        }

        private IConfiguration FakeConfiguration(IEnumerable<KeyValuePair<string,string>> initialData = null)
        {
            initialData = initialData ?? new List<KeyValuePair<string, string>>();
            return new ConfigurationBuilder().AddInMemoryCollection(initialData).Build();
        }

        [TestMethod]
        [TestCategory("ManagedIdentityTests")]
        public async Task ProbeShouldNotBeAvailableWithoutManagedIdentityServiceAsync()
        {
            var probe = new ManagedIdentityTokenProvider(config: FakeConfiguration());
            var st = DateTime.Now;
            Assert.IsFalse(await probe.IsAvailableAsync().ConfigureAwait(false));
            Assert.IsTrue(DateTime.Now - st < TimeSpan.FromMilliseconds(800), "should take less than 800 milliseconds");
        }

        [TestMethod]
        [TestCategory("ManagedIdentityTests")]
        public async Task ProbeShouldBeAvailableWithAppServiceConfigManagedIdentityServiceAsync()
        {
            var config = FakeConfiguration(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(Constants.ManagedIdentityEndpointEnvName,
                    "http://127.0.0.1/foo"),
                new KeyValuePair<string, string>(Constants.ManagedIdentitySecretEnvName, "secret")

            });
            var probe = new ManagedIdentityTokenProvider(config: config);
            Assert.IsTrue(await probe.IsAvailableAsync().ConfigureAwait(false));
        }

        [TestMethod]
        [TestCategory("ManagedIdentityTests")]
        public async Task ProbeShouldFetchTokenFromAppServiceManagedIdentityServiceAsync()
        {
            var handler = new MockManagedIdentityHttpMessageHandler();
            handler.Responders.Add(new Responder
            {
                Matcher = (req, state) =>
                {
                    var apiVersion = Constants.ManagedIdentityAppServiceApiVersion;
                    return req.RequestUri.ToString() == "http://127.0.0.1/foo?resource=https://management.azure.com/&api-version=" + apiVersion &&
                        req.Headers.GetValues("Secret").FirstOrDefault() == "secret";
                },
                MockResponse = (req, state) =>
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new MockJsonContent(AzureAppServiceManagedIdentityJson)
                    };
                    return resp;
                }
            });
            var client = new HttpClient(handler);
            var config = FakeConfiguration(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(Constants.ManagedIdentityEndpointEnvName, "http://127.0.0.1/foo"),
                new KeyValuePair<string, string>(Constants.ManagedIdentitySecretEnvName, "secret")

            });
            var provider = new ManagedIdentityTokenProvider(httpClient: client, config: config);
            var token = await provider.GetTokenAsync(new List<string> { "https://management.azure.com/.default" }).ConfigureAwait(false);
            Assert.IsNotNull(token);
            Assert.AreEqual(DateTimeOffset.Parse("4/10/19 6:27:14 AM +00:00", CultureInfo.InvariantCulture), token.ExpiresOn);
            Assert.AreEqual(AccessToken, token.AccessToken);
        }

        [TestMethod]
        [TestCategory("ManagedIdentityTests")]
        public async Task ProbeShouldFetchTokenFromAppServiceManagedIdentityWithResourceUriAsync()
        {
            var handler = new MockManagedIdentityHttpMessageHandler();
            handler.Responders.Add(new Responder
            {
                Matcher = (req, state) =>
                {
                    var apiVersion = Constants.ManagedIdentityAppServiceApiVersion;
                    return req.RequestUri.ToString() == "http://127.0.0.1/foo?resource=https://management.azure.com/&api-version=" + apiVersion &&
                           req.Headers.GetValues("Secret").FirstOrDefault() == "secret";
                },
                MockResponse = (req, state) =>
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new MockJsonContent(AzureAppServiceManagedIdentityJson)
                    };
                    return resp;
                }
            });
            var client = new HttpClient(handler);
            var config = FakeConfiguration(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(Constants.ManagedIdentityEndpointEnvName, "http://127.0.0.1/foo"),
                new KeyValuePair<string, string>(Constants.ManagedIdentitySecretEnvName, "secret")

            });
            var provider = new ManagedIdentityTokenProvider(httpClient: client, config: config);
            var token = await provider.GetTokenWithResourceUriAsync("https://management.azure.com/").ConfigureAwait(false);
            Assert.IsNotNull(token);
            Assert.AreEqual(DateTimeOffset.Parse("4/10/19 6:27:14 AM +00:00", CultureInfo.InvariantCulture), token.ExpiresOn);
            Assert.AreEqual(AccessToken, token.AccessToken);
        }

        [TestMethod]
        [TestCategory("ManagedIdentityTests")]
        public async Task ProbeShouldFetchTokenWithClientIdFromManagedIdentityServiceAsync()
        {
            var handler = new MockManagedIdentityHttpMessageHandler();
            handler.Responders.Add(new Responder
            {
                Matcher = (req, state) =>
                {
                    var tokenEndpoint = Constants.ManagedIdentityTokenEndpoint;
                    var apiVersion = Constants.ManagedIdentityVMApiVersion;
                    return req.RequestUri.ToString() == tokenEndpoint + "?resource=https://management.azure.com/&client_id=foo&api-version=" + apiVersion;
                },
                MockResponse = (req, state) =>
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new MockJsonContent(AzureManagementVMManagedIdentityJson)
                    };
                    return resp;
                }
            });
            var client = new HttpClient(handler);
            var config = FakeConfiguration(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(Constants.AzureClientIdEnvName, "foo"),

            });
            var provider = new ManagedIdentityTokenProvider(httpClient: client, config: config);
            var token = await provider.GetTokenAsync(new List<string> { "https://management.azure.com/.default" }).ConfigureAwait(false);
            Assert.IsNotNull(token);
            var seconds = double.Parse(ExpiresOn, CultureInfo.InvariantCulture);
            var startOfUnixTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(token.ExpiresOn, startOfUnixTime.AddSeconds(seconds));
            Assert.AreEqual(AccessToken, token.AccessToken);
        }

        [TestMethod]
        [TestCategory("ManagedIdentityTests")]
        public async Task ProbeBeAvailableWhenTokenReturnedManagedIdentityServiceAsync()
        {
            var handler = new MockManagedIdentityHttpMessageHandler();
            handler.Responders.Add(new Responder
            {
                Matcher = (req, state) =>
                {
                    var tokenEndpoint = Constants.ManagedIdentityTokenEndpoint;
                    var apiVersion = Constants.ManagedIdentityVMApiVersion;
                    return req.RequestUri.ToString() == tokenEndpoint + "?resource=https://management.azure.com/&api-version=" + apiVersion;
                },
                MockResponse = (req, state) =>
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new MockJsonContent(AzureManagementVMManagedIdentityJson)
                    };
                    return resp;
                }
            });
            var client = new HttpClient(handler);
            var provider = new ManagedIdentityTokenProvider(httpClient: client, config: FakeConfiguration());
            Assert.IsTrue(await provider.IsAvailableAsync().ConfigureAwait(false));
        }

        [TestMethod]
        [TestCategory("ManagedIdentityTests")]
        public async Task ProbeNotBeAvailableWhen400ReturnedFromManagedIdentityServiceAsync()
        {
            var handler = new MockManagedIdentityHttpMessageHandler();
            handler.Responders.Add(new Responder
            {
                Matcher = (req, state) =>
                {
                    var tokenEndpoint = Constants.ManagedIdentityTokenEndpoint;
                    var apiVersion = Constants.ManagedIdentityVMApiVersion;
                    return req.RequestUri.ToString() == tokenEndpoint + "?resource=https://management.azure.com/&api-version=" + apiVersion;
                },
                MockResponse = (req, state) => new HttpResponseMessage(HttpStatusCode.BadRequest)
            });
            var client = new HttpClient(handler);
            var provider = new ManagedIdentityTokenProvider(httpClient: client, config: FakeConfiguration());
            Assert.IsFalse(await provider.IsAvailableAsync().ConfigureAwait(false));
        }

        [TestMethod]
        [TestCategory("ManagedIdentityTests")]
        public async Task ProbeBeAvailableWhenInitial404ReturnedFromManagedIdentityServiceAsync()
        {
            var handler = new MockManagedIdentityHttpMessageHandler();
            handler.Responders.Add(new Responder
            {
                Matcher = (req, state) =>
                {
                    var tokenEndpoint = Constants.ManagedIdentityTokenEndpoint;
                    var apiVersion = Constants.ManagedIdentityVMApiVersion;
                    return req.RequestUri.ToString() == tokenEndpoint + "?resource=https://management.azure.com/&api-version=" + apiVersion;
                },
                MockResponse = (req, state) =>
                {
                    if (state.Keys.Contains("error"))
                    {
                        var resp = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new MockJsonContent(AzureManagementVMManagedIdentityJson)
                        };
                        return resp;
                    }

                    state["error"] = true;
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
            });
            var client = new HttpClient(handler);
            var provider = new ManagedIdentityTokenProvider(httpClient: client, config: FakeConfiguration());
            Assert.IsTrue(await provider.IsAvailableAsync().ConfigureAwait(false));
        }

        [TestMethod]
        [TestCategory("ManagedIdentityTests")]
        public async Task ProbeBeAvailableWhenInitial429ReturnedFromManagedIdentityServiceAsync()
        {
            var handler = new MockManagedIdentityHttpMessageHandler();
            handler.Responders.Add(new Responder
            {
                Matcher = (req, state) =>
                {
                    var tokenEndpoint = Constants.ManagedIdentityTokenEndpoint;
                    var apiVersion = Constants.ManagedIdentityVMApiVersion;
                    return req.RequestUri.ToString() == tokenEndpoint + "?resource=https://management.azure.com/&api-version=" + apiVersion;
                },
                MockResponse = (req, state) =>
                {
                    if (state.Keys.Contains("error"))
                    {
                        var resp = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new MockJsonContent(AzureManagementVMManagedIdentityJson)
                        };
                        return resp;
                    }
                    else
                    {
                        state["error"] = true;
                        return new HttpResponseMessage((HttpStatusCode)429);
                    }
                }
            });
            var client = new HttpClient(handler);
            var provider = new ManagedIdentityTokenProvider(httpClient: client, config: FakeConfiguration());
            Assert.IsTrue(await provider.IsAvailableAsync().ConfigureAwait(false));
        }

        [TestMethod]
        [TestCategory("ManagedIdentityTests")]
        public async Task ProbeBeAvailableWhenInitial500sReturnedFromManagedIdentityServiceAsync()
        {
            var handler = new MockManagedIdentityHttpMessageHandler();
            handler.Responders.Add(new Responder
            {
                Matcher = (req, state) =>
                {
                    var tokenEndpoint = Constants.ManagedIdentityTokenEndpoint;
                    var apiVersion = Constants.ManagedIdentityVMApiVersion;
                    return req.RequestUri.ToString() == tokenEndpoint + "?resource=https://management.azure.com/&api-version=" + apiVersion;
                },
                MockResponse = (req, state) =>
                {
                    if (state.Keys.Contains("error1") && state.Keys.Contains("error2"))
                    {
                        var resp = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new MockJsonContent(AzureManagementVMManagedIdentityJson)
                        };
                        return resp;
                    }
                    else if (!state.Keys.Contains("error1"))
                    {
                        state["error1"] = true;
                        return new HttpResponseMessage((HttpStatusCode)500);
                    }
                    else
                    {
                        state["error2"] = true;
                        return new HttpResponseMessage((HttpStatusCode)501);
                    }
                }
            });
            var client = new HttpClient(handler);
            var provider = new ManagedIdentityTokenProvider(httpClient: client, config: FakeConfiguration());
            Assert.IsTrue(await provider.IsAvailableAsync().ConfigureAwait(false));
        }
    }

    public class MockManagedIdentityHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var responder = Responders.FirstOrDefault(i => i.Matcher(request, i.State));
            if (responder == null)
            {
                Assert.Fail($"responder was not found that matched the request: {request}");
            }

            return Task.FromResult(responder.MockResponse(request, responder.State));
        }

        public IList<Responder> Responders { get; } = new List<Responder>();
    }

    public class MockJsonContent : HttpContent
    {
        private readonly MemoryStream _stream = new MemoryStream();

        public MockJsonContent(string json)
        {

            Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var sw = new StreamWriter(_stream, new UnicodeEncoding());
            sw.Write(json);
            sw.Flush();//otherwise you are risking empty stream
            _stream.Seek(0, SeekOrigin.Begin);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return _stream.CopyToAsync(stream);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _stream.Length;
            return true;
        }
    }

    public class Responder
    {
        public Dictionary<string, object> State { get; } = new Dictionary<string, object>();
        public Func<HttpRequestMessage, Dictionary<string, object>, bool> Matcher { get; set; }
        public Func<HttpRequestMessage, Dictionary<string, object>, HttpResponseMessage> MockResponse { get; set; }
    }
}
