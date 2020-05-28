//------------------------------------------------------------------------------
// <copyright file="AadWindowsInteractiveAuthenticationDialog.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl
{
    /// <summary>
    /// A Windows Form based AAD authentication dialog
    /// </summary>
    /// <remarks>For internal use only.</remarks>
    [ComVisible(true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // The code below is based on the MSAL implementation for interactive login on .NET Fx
    // Original code at:
    //   https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/master/src/client/Microsoft.Identity.Client/Platforms/net45/WindowsFormsWebAuthenticationDialogBase.cs
    //   https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/master/src/client/Microsoft.Identity.Client/Platforms/net45/WindowsFormsWebAuthenticationDialog.cs
    public sealed class NetCoreInteractiveAuthenticationDialog : Form
    {
        const string NonHttpsRedirectNotSupported = "Non-HTTPS url redirect is not supported in webview";

        const int UIWidth = 566;

        static readonly Dictionary<int, string> NavigateErrorStatusMessages = new Dictionary<int, string>
        {
            { (int)NavigateErrorStatusCode.HTTP_STATUS_BAD_REQUEST, "The request could not be processed by the server due to invalid syntax." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_DENIED, "The requested resource requires user authentication." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_PAYMENT_REQ, "Not currently implemented in the HTTP protocol." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_FORBIDDEN, "The server understood the request, but is refusing to fulfill it." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_NOT_FOUND, "The server has not found anything matching the requested URI (Uniform Resource Identifier)." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_BAD_METHOD, "The HTTP verb used is not allowed." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_NONE_ACCEPTABLE, "No responses acceptable to the client were found." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_PROXY_AUTH_REQ, "Proxy authentication required." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_REQUEST_TIMEOUT, "The server timed out waiting for the request." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_CONFLICT, "The request could not be completed due to a conflict with the current state of the resource. The user should resubmit with more information." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_GONE, "The requested resource is no longer available at the server, and no forwarding address is known." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_LENGTH_REQUIRED, "The server refuses to accept the request without a defined content length." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_PRECOND_FAILED, "The precondition given in one or more of the request header fields evaluated to false when it was tested on the server." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_REQUEST_TOO_LARGE, "The server is refusing to process a request because the request entity is larger than the server is willing or able to process." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_URI_TOO_LONG, "The server is refusing to service the request because the request URI (Uniform ScopeSet Identifier) is longer than the server is willing to interpret." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_UNSUPPORTED_MEDIA, "The server is refusing to service the request because the entity of the request is in a format not supported by the requested resource for the requested method." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_RETRY_WITH, "The request should be retried after doing the appropriate action." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_SERVER_ERROR, "The server encountered an unexpected condition that prevented it from fulfilling the request." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_NOT_SUPPORTED, "The server does not support the functionality required to fulfill the request." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_BAD_GATEWAY, "The server, while acting as a gateway or proxy, received an invalid response from the upstream server it accessed in attempting to fulfill the request." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_SERVICE_UNAVAIL, "The service is temporarily overloaded." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_GATEWAY_TIMEOUT, "The request was timed out waiting for a gateway." },
            { (int)NavigateErrorStatusCode.HTTP_STATUS_VERSION_NOT_SUP, "The server does not support, or refuses to support, the HTTP protocol version that was used in the request message." },
            { (int)NavigateErrorStatusCode.INET_E_AUTHENTICATION_REQUIRED, "Authentication is needed to access the object." },
            { (int)NavigateErrorStatusCode.INET_E_CANNOT_CONNECT, "The attempt to connect to the Internet has failed." },
            { (int)NavigateErrorStatusCode.INET_E_CANNOT_INSTANTIATE_OBJECT, "CoCreateInstance failed." },
            { (int)NavigateErrorStatusCode.INET_E_CANNOT_LOAD_DATA, "The object could not be loaded." },
            { (int)NavigateErrorStatusCode.INET_E_CANNOT_LOCK_REQUEST, "The requested resource could not be locked." },
            { (int)NavigateErrorStatusCode.INET_E_CANNOT_REPLACE_SFP_FILE, "Cannot replace a file that is protected by SFP." },
            { (int)NavigateErrorStatusCode.INET_E_CODE_DOWNLOAD_DECLINED, "The component download was declined by the user." },
            { (int)NavigateErrorStatusCode.INET_E_CODE_INSTALL_SUPPRESSED, "Internet Explorer 6 for Windows XP SP2 and later. The Authenticode prompt for installing a ActiveX control was not shown because the page restricts the installation of the ActiveX controls. The usual cause is that the Information Bar is shown instead of the Authenticode prompt." },
            { (int)NavigateErrorStatusCode.INET_E_CODE_INSTALL_BLOCKED_BY_HASH_POLICY, "Internet Explorer 6 for Windows XP SP2 and later. Installation of ActiveX control (as identified by cryptographic file hash) has been disallowed by registry key policy." },
            { (int)NavigateErrorStatusCode.INET_E_CONNECTION_TIMEOUT, "The Internet connection has timed out." },
            { (int)NavigateErrorStatusCode.INET_E_DATA_NOT_AVAILABLE, "An Internet connection was established, but the data cannot be retrieved." },
            { (int)NavigateErrorStatusCode.INET_E_DOWNLOAD_FAILURE, "The download has failed (the connection was interrupted)." },
            { (int)NavigateErrorStatusCode.INET_E_INVALID_CERTIFICATE, "The SSL certificate is invalid." },
            { (int)NavigateErrorStatusCode.INET_E_INVALID_REQUEST, "The request was invalid." },
            { (int)NavigateErrorStatusCode.INET_E_INVALID_URL, "The URL could not be parsed." },
            { (int)NavigateErrorStatusCode.INET_E_NO_SESSION, "No Internet session was established." },
            { (int)NavigateErrorStatusCode.INET_E_NO_VALID_MEDIA, "The object is not in one of the acceptable MIME types." },
            { (int)NavigateErrorStatusCode.INET_E_OBJECT_NOT_FOUND, "The object was not found." },
            { (int)NavigateErrorStatusCode.INET_E_REDIRECT_FAILED, "WinInet cannot redirect. This error code might also be returned by a custom protocol handler." },
            { (int)NavigateErrorStatusCode.INET_E_REDIRECT_TO_DIR, "The request is being redirected to a directory." },
            { (int)NavigateErrorStatusCode.INET_E_RESOURCE_NOT_FOUND, "The server or proxy was not found." },
            { (int)NavigateErrorStatusCode.INET_E_RESULT_DISPATCHED, "The binding has already been completed and the result has been dispatched, so your abort call has been canceled." },
            { (int)NavigateErrorStatusCode.INET_E_SECURITY_PROBLEM, "A security problem was encountered." },
            { (int)NavigateErrorStatusCode.INET_E_TERMINATED_BIND, "Binding was terminated. (See IBinding::GetBindResult.)" },
            { (int)NavigateErrorStatusCode.INET_E_UNKNOWN_PROTOCOL, "The protocol is not known and no pluggable protocols have been entered that match." },
            { (int)NavigateErrorStatusCode.INET_E_USE_EXTEND_BINDING, "(Microsoft internal.) Reissue request with extended binding." }
        };
        static readonly int ZoomPercent = NetCoreInteractiveAuthenticationDialog.CalcZoomPercent();

        readonly IWin32Window owner;
        readonly AadWebBrowser webBrowser;
        Panel webBrowserPanel;
        Uri desiredCallbackUri;
        Keys key = Keys.None;
        int statusCode;
        bool isZoomed;
        AuthenticationDialogResult result;

        internal NetCoreInteractiveAuthenticationDialog()
            : base()
        {
            EnsureSessionCookiesLifetime();

            this.webBrowser = NetCoreInteractiveAuthenticationDialog.CreateWebBrowser(this);

            InitializeDialog(IntPtr.Zero);
        }
        internal NetCoreInteractiveAuthenticationDialog(Control owner)
            : base()
        {
            Debug.Assert(owner != null);

            EnsureSessionCookiesLifetime();

            this.owner = owner;
            this.webBrowser = NetCoreInteractiveAuthenticationDialog.CreateWebBrowser(this);

            InitializeDialog(owner.Handle);
        }
        internal NetCoreInteractiveAuthenticationDialog(IntPtr ownerHandle)
            : base()
        {
            Debug.Assert(ownerHandle != IntPtr.Zero);

            EnsureSessionCookiesLifetime();

            this.owner = new Win32Window(ownerHandle);
            this.webBrowser = NetCoreInteractiveAuthenticationDialog.CreateWebBrowser(this);

            InitializeDialog(ownerHandle);
        }

        /// <summary>
        /// Disposes of the resources (other than memory) used by the dialog.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        /// <remarks>For internal use only.</remarks>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                StopWebBrowser();

            base.Dispose(disposing);
        }

        internal Uri Authenticate(Uri requestUri, Uri callbackUri)
        {
            this.desiredCallbackUri = callbackUri;
            this.result = new AuthenticationDialogResult();
            this.isZoomed = false;
            this.statusCode = 0;

            this.webBrowser.Navigate(requestUri);
            ShowBrowser();

            return this.result.GetResult();
        }

        internal bool HandleBrowserNavigateError(string url, string targetFrameName, int statusCode, object webBrowserActiveXInstance)
        {
            if (this.DialogResult == DialogResult.OK)
                return true;
            // we cancel all flow in disposed object.
            if (this.webBrowser.IsDisposed)
                return true;
            // this event came from internal frame, ignore this.
            if (this.webBrowser.ActiveXInstance != webBrowserActiveXInstance)
                return false;
            // we could get redirect flows here as well.
            if (statusCode >= 300 && statusCode < 400)
                return false;

            StopWebBrowser();
            this.statusCode = statusCode;
            this.DialogResult = (statusCode == 0 ? DialogResult.Cancel : DialogResult.Abort);

            return true;
        }

        static int CalcZoomPercent()
        {
            const double DefaultDpi = 96.0;
            const int LOGPIXELSX = 88;
            const int LOGPIXELSY = 90;

            double deviceDpiX;
            double deviceDpiY;

            IntPtr dC = NativeMethods.GetDC(IntPtr.Zero);
            if (dC != IntPtr.Zero)
            {
                deviceDpiX = NativeMethods.GetDeviceCaps(dC, LOGPIXELSX);
                deviceDpiY = NativeMethods.GetDeviceCaps(dC, LOGPIXELSY);
                NativeMethods.ReleaseDC(IntPtr.Zero, dC);
            }
            else
            {
                deviceDpiX = DefaultDpi;
                deviceDpiY = DefaultDpi;
            }

            int zoomPercentX = (int)(100 * (deviceDpiX / DefaultDpi));
            int zoomPercentY = (int)(100 * (deviceDpiY / DefaultDpi));

            return Math.Min(zoomPercentX, zoomPercentY);
        }

        static AadWebBrowser CreateWebBrowser(NetCoreInteractiveAuthenticationDialog owner)
        {
            AadWebBrowser webBrowser = new AadWebBrowser(owner);
            webBrowser.ObjectForScripting = owner;
            webBrowser.PreviewKeyDown += owner.WebBrowser_PreviewKeyDown;
            webBrowser.DocumentTitleChanged += owner.WebBrowser_DocumentTitleChanged;
            webBrowser.Navigating += owner.WebBrowser_Navigating;

            return webBrowser;
        }

        void InvokeInOwnerContext(Action action)
        {
            if (this.owner != null && this.owner is Control)
                ((Control)this.owner).Invoke(action);
            else
                action();
        }

        void EnsureSessionCookiesLifetime()
        {
            // From MSDN (http://msdn.microsoft.com/en-us/library/ie/dn720860(v=vs.85).aspx):
            // The net session count tracks the number of instances of the web browser control.
            // When a web browser control is created, the net session count is incremented. When the control
            // is destroyed, the net session count is decremented. When the net session count reaches zero,
            // the session cookies for the process are cleared. SetQueryNetSessionCount can be used to prevent
            // the session cookies from being cleared for applications where web browser controls are being created
            // and destroyed throughout the lifetime of the application. (Because the application lives longer than
            // a given instance, session cookies must be retained for a longer periods of time.
            if (NativeMethods.SetQueryNetSessionCount(SessionOp.SESSION_QUERY) == 0)
                NativeMethods.SetQueryNetSessionCount(SessionOp.SESSION_INCREMENT);
        }
        void InitializeDialog(IntPtr ownerHandle)
        {
            InvokeInOwnerContext(
                delegate ()
                {
                    Screen screen = (ownerHandle != IntPtr.Zero ? Screen.FromHandle(ownerHandle) : Screen.PrimaryScreen);
                    // Window height is set to 70% of the screen height.
                    int uiHeight = (int)(Math.Max(screen.WorkingArea.Height, 160) * 70.0 / NetCoreInteractiveAuthenticationDialog.ZoomPercent);

                    SuspendLayout();
                    this.webBrowserPanel = new Panel();
                    this.webBrowserPanel.SuspendLayout();

                    // webBrowser
                    this.webBrowser.Dock = DockStyle.Fill;
                    this.webBrowser.Location = new Point(0, 25);
                    this.webBrowser.MinimumSize = new Size(20, 20);
                    this.webBrowser.Name = "webBrowser";
                    this.webBrowser.Size = new Size(NetCoreInteractiveAuthenticationDialog.UIWidth, 565);
                    this.webBrowser.TabIndex = 1;
                    this.webBrowser.IsWebBrowserContextMenuEnabled = false;

                    // webBrowserPanel
                    this.webBrowserPanel.Controls.Add(this.webBrowser);
                    this.webBrowserPanel.Dock = DockStyle.Fill;
                    this.webBrowserPanel.BorderStyle = BorderStyle.None;
                    this.webBrowserPanel.Location = new Point(0, 0);
                    this.webBrowserPanel.Name = "webBrowserPanel";
                    this.webBrowserPanel.Size = new Size(NetCoreInteractiveAuthenticationDialog.UIWidth, uiHeight);
                    this.webBrowserPanel.TabIndex = 2;

                    // BrowserAuthenticationWindow
                    this.AutoScaleDimensions = new SizeF(6, 13);
                    this.AutoScaleMode = AutoScaleMode.Font;
                    this.ClientSize = new Size(NetCoreInteractiveAuthenticationDialog.UIWidth, uiHeight);
                    this.Controls.Add(this.webBrowserPanel);
                    this.FormBorderStyle = FormBorderStyle.FixedSingle;
                    this.Name = "BrowserAuthenticationWindow";
                    // Move the window to the center of the parent window only if owner window is set.
                    this.StartPosition = (ownerHandle != IntPtr.Zero ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen);
                    this.Text = string.Empty;
                    this.ShowIcon = false;
                    this.MaximizeBox = false;
                    this.MinimizeBox = false;
                    // If we don't have an owner we need to make sure that the pop up browser window is in the task bar so that it can be selected with the mouse.
                    this.ShowInTaskbar = (ownerHandle == IntPtr.Zero);

                    this.webBrowserPanel.ResumeLayout(false);
                    ResumeLayout(false);
                });

            this.Shown += FormShown;
        }
        void StopWebBrowser()
        {
            InvokeInOwnerContext(
                delegate ()
                {
                    // Guard condition
                    if (!this.webBrowser.IsDisposed && this.webBrowser.IsBusy)
                        this.webBrowser.Stop();
                });
        }
        void SetBrowserZoom()
        {
            if (NativeMethods.IsProcessDPIAware() && NetCoreInteractiveAuthenticationDialog.ZoomPercent != 100 && !this.isZoomed)
            {
                // There is a bug in some versions of the IE browser control that causes it to ignore scaling unless it is changed.
                SetBrowserZoomImpl(NetCoreInteractiveAuthenticationDialog.ZoomPercent - 1);
                SetBrowserZoomImpl(NetCoreInteractiveAuthenticationDialog.ZoomPercent);

                this.isZoomed = true;
            }
        }
        void SetBrowserZoomImpl(int zoomPercent)
        {
            const int OLECMDID_OPTICAL_ZOOM = 63;
            const int OLECMDEXECOPT_DONTPROMPTUSER = 2;

            IWebBrowser2 browser2 = (IWebBrowser2)this.webBrowser.ActiveXInstance;
            IOleCommandTarget cmdTarget = browser2.Document as IOleCommandTarget;
            if (cmdTarget != null)
            {
                object[] commandInput = { zoomPercent };

                int hResult = cmdTarget.Exec(IntPtr.Zero, OLECMDID_OPTICAL_ZOOM, OLECMDEXECOPT_DONTPROMPTUSER, commandInput, IntPtr.Zero);
                Marshal.ThrowExceptionForHR(hResult);
            }
        }
        bool CheckForClosingUrl(Uri url)
        {
            bool readyToClose = false;

            if (url.Authority.Equals(this.desiredCallbackUri.Authority, StringComparison.OrdinalIgnoreCase) &&
                url.AbsolutePath.Equals(this.desiredCallbackUri.AbsolutePath))
            {
                this.result = new AuthenticationDialogResult(url);
                readyToClose = true;
            }

            if (!readyToClose && !url.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && !url.AbsoluteUri.Equals("about:blank", StringComparison.OrdinalIgnoreCase) &&
                !url.Scheme.Equals("javascript", StringComparison.OrdinalIgnoreCase))
            {
                this.result = new AuthenticationDialogResult(MsalError.NonHttpsRedirectNotSupported, NetCoreInteractiveAuthenticationDialog.NonHttpsRedirectNotSupported);
                readyToClose = true;
            }

            if (readyToClose)
            {
                StopWebBrowser();
                // in this handler object could be already disposed, so it should be the last method
                this.DialogResult = DialogResult.OK;
            }

            return readyToClose;
        }
        void ShowBrowser()
        {
            DialogResult uiResult = DialogResult.None;
            InvokeInOwnerContext(
                delegate ()
                {
                    uiResult = (this.owner != null ? ShowDialog(this.owner) : ShowDialog());
                });
            switch (uiResult)
            {
                case DialogResult.OK:
                    break;
                case DialogResult.Cancel:
                    throw new OperationCanceledException();
                default:
                    this.result = new AuthenticationDialogResult(this.statusCode);
                    break;
            }
        }

        void FormShown(object sender, EventArgs e)
        {
            // If we don't have an owner we need to make sure that the pop up browser window is on top of other windows;
            // activating the window will accomplish this.
            if (this.owner == null)
                Activate();
        }
        void WebBrowser_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Back)
                this.key = Keys.Back;
        }
        void WebBrowser_DocumentTitleChanged(object sender, EventArgs e)
        {
            this.Text = this.webBrowser.DocumentTitle;
        }
        void WebBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            SetBrowserZoom();

            if (this.DialogResult == DialogResult.OK)
            {
                e.Cancel = true;
                return;
            }

            if (this.webBrowser.IsDisposed)
            {
                // we cancel all flows in disposed object and just do nothing, let object to close.
                // it just for safety.
                e.Cancel = true;
                return;
            }

            if (this.key == Keys.Back)
            {
                //navigation is being done via back key. This needs to be disabled.
                this.key = Keys.None;
                e.Cancel = true;
            }

            // we cancel further processing, if we reached final URL.
            // Security issue: we prohibit navigation with auth code if redirect URI is URN, then we prohibit navigation, to prevent random browser popup.
            e.Cancel = CheckForClosingUrl(e.Url);

            // check if the url scheme is of type browser-install://
            // this means we need to launch external browser
            if (e.Url.Scheme.Equals("browser", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(e.Url.AbsoluteUri.Replace("browser://", "https://"));
                e.Cancel = true;
            }
        }

        enum NavigateErrorStatusCode
        {
            HTTP_STATUS_BAD_REQUEST = 400,
            HTTP_STATUS_DENIED = 401,
            HTTP_STATUS_PAYMENT_REQ = 402,
            HTTP_STATUS_FORBIDDEN = 403,
            HTTP_STATUS_NOT_FOUND = 404,
            HTTP_STATUS_BAD_METHOD = 405,
            HTTP_STATUS_NONE_ACCEPTABLE = 406,
            HTTP_STATUS_PROXY_AUTH_REQ = 407,
            HTTP_STATUS_REQUEST_TIMEOUT = 408,
            HTTP_STATUS_CONFLICT = 409,
            HTTP_STATUS_GONE = 410,
            HTTP_STATUS_LENGTH_REQUIRED = 411,
            HTTP_STATUS_PRECOND_FAILED = 412,
            HTTP_STATUS_REQUEST_TOO_LARGE = 413,
            HTTP_STATUS_URI_TOO_LONG = 414,
            HTTP_STATUS_UNSUPPORTED_MEDIA = 415,
            HTTP_STATUS_RETRY_WITH = 449,
            HTTP_STATUS_SERVER_ERROR = 500,
            HTTP_STATUS_NOT_SUPPORTED = 501,
            HTTP_STATUS_BAD_GATEWAY = 502,
            HTTP_STATUS_SERVICE_UNAVAIL = 503,
            HTTP_STATUS_GATEWAY_TIMEOUT = 504,
            HTTP_STATUS_VERSION_NOT_SUP = 505,
            INET_E_INVALID_URL = -2146697214,
            INET_E_NO_SESSION = -2146697213,
            INET_E_CANNOT_CONNECT = -2146697212,
            INET_E_RESOURCE_NOT_FOUND = -2146697211,
            INET_E_OBJECT_NOT_FOUND = -2146697210,
            INET_E_DATA_NOT_AVAILABLE = -2146697209,
            INET_E_DOWNLOAD_FAILURE = -2146697208,
            INET_E_AUTHENTICATION_REQUIRED = -2146697207,
            INET_E_NO_VALID_MEDIA = -2146697206,
            INET_E_CONNECTION_TIMEOUT = -2146697205,
            INET_E_INVALID_REQUEST = -2146697204,
            INET_E_UNKNOWN_PROTOCOL = -2146697203,
            INET_E_SECURITY_PROBLEM = -2146697202,
            INET_E_CANNOT_LOAD_DATA = -2146697201,
            INET_E_CANNOT_INSTANTIATE_OBJECT = -2146697200,
            INET_E_REDIRECT_FAILED = -2146697196,
            INET_E_REDIRECT_TO_DIR = -2146697195,
            INET_E_CANNOT_LOCK_REQUEST = -2146697194,
            INET_E_USE_EXTEND_BINDING = -2146697193,
            INET_E_TERMINATED_BIND = -2146697192,
            INET_E_INVALID_CERTIFICATE = -2146697191,
            INET_E_CODE_DOWNLOAD_DECLINED = -2146696960,
            INET_E_RESULT_DISPATCHED = -2146696704,
            INET_E_CANNOT_REPLACE_SFP_FILE = -2146696448,
            INET_E_CODE_INSTALL_BLOCKED_BY_HASH_POLICY = -2146695936,
            INET_E_CODE_INSTALL_SUPPRESSED = -2146696192
        }

        struct AuthenticationDialogResult
        {
            Uri result;
            string error;
            string errorMessage;

            public AuthenticationDialogResult(Uri result)
            {
                this.result = result;
                this.error = null;
                this.errorMessage = null;
            }
            public AuthenticationDialogResult(string error, string errorMessage = null)
            {
                this.result = null;
                this.error = error;
                this.errorMessage = errorMessage;
            }
            public AuthenticationDialogResult(int statusCode)
            {
                this.result = null;
                this.error = MsalError.AuthenticationUiFailedError;

                string message;
                if (NetCoreInteractiveAuthenticationDialog.NavigateErrorStatusMessages.TryGetValue(statusCode, out message))
                    this.errorMessage =
                        string.Format(CultureInfo.InvariantCulture, "The browser based authentication dialog failed to complete. Reason: {0}", message);
                else
                    this.errorMessage =
                        string.Format(CultureInfo.InvariantCulture, "The browser based authentication dialog failed to complete for an unknown reason. StatusCode: {0}", statusCode);
            }

            public Uri GetResult()
            {
                if (!string.IsNullOrEmpty(this.error))
                    if (string.IsNullOrEmpty(this.errorMessage))
                        throw new MsalClientException(this.error);
                    else
                        throw new MsalClientException(this.error, this.errorMessage);

                return this.result;
            }
        }

        sealed class Win32Window : IWin32Window
        {
            public Win32Window(IntPtr handle)
            {
                this.Handle = handle;
            }

            public IntPtr Handle { get; private set; }
        }

    }
     
}
