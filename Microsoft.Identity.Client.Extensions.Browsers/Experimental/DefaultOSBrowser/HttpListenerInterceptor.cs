// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Identity.Client.Extensions.Browsers.Experimental.DefaultOSBrowser
{
    internal class HttpListenerInterceptor : IUriInterceptor
    {
        #region Test Hooks 
        public Action TestBeforeTopLevelCall { get; set; }
        public Action TestBeforeStart { get; set; }
        public Action TestBeforeGetContext { get; set; }
        #endregion


        public async Task<Uri> ListenToSingleRequestAndRespondAsync(
            int port,
            Func<Uri, MessageAndHttpCode> responseProducer,
            CancellationToken cancellationToken)
        {
            TestBeforeTopLevelCall?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();

            HttpListener httpListener = null;
            try
            {
                string urlToListenTo = "http://localhost:" + port + "/";

                httpListener = new HttpListener();
                httpListener.Prefixes.Add(urlToListenTo);

                TestBeforeStart?.Invoke();

                httpListener.Start();
                
                
                Debug.WriteLine("Listening for authorization code on " + urlToListenTo);

                using (cancellationToken.Register(() =>
                {
                    Debug.WriteLine("HttpListener stopped because cancellation was requested.");
                    TryStopListening(httpListener);
                }))
                {
                    TestBeforeGetContext?.Invoke();
                    HttpListenerContext context = await httpListener.GetContextAsync()
                        .ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();

                    Respond(responseProducer, context);
                    Debug.WriteLine("HttpListner received a message on " + urlToListenTo);

                    // the request URL should now contain the auth code and pkce
                    return context.Request.Url;
                }
            }
            // If cancellation is requested before GetContextAsync is called, then either 
            // an ObjectDisposedException or an HttpListenerException is thrown.
            // But this is just cancellation...
            catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException)
            {
                Debug.WriteLine("HttpListenerException - cancellation requested? " + cancellationToken.IsCancellationRequested);
                cancellationToken.ThrowIfCancellationRequested();

                // if cancellation was not requested, propagate original ex
                throw;
            }
            finally
            {
                TryStopListening(httpListener);
            }
        }

        private static void TryStopListening(HttpListener httpListener)
        {
            try
            {
                httpListener?.Abort();
            }
            catch
            {
                // 
            }
        }

        private void Respond(Func<Uri, MessageAndHttpCode> responseProducer, HttpListenerContext context)
        {
            MessageAndHttpCode messageAndCode = responseProducer(context.Request.Url);
            Debug.WriteLine("Processing a response message to the browser. HttpStatus:" + messageAndCode.HttpCode);

            switch (messageAndCode.HttpCode)
            {
            case HttpStatusCode.Found:
                context.Response.StatusCode = (int)HttpStatusCode.Found;
                context.Response.RedirectLocation = messageAndCode.Message;
                break;
            case HttpStatusCode.OK:
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(messageAndCode.Message);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                break;
            default:
                throw new NotImplementedException("HttpCode not supported" + messageAndCode.HttpCode);
            }

            context.Response.OutputStream.Close();
        }
    }
}
