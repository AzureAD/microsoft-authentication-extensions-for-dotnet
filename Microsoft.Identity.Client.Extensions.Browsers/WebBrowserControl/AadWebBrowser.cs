//------------------------------------------------------------------------------
// <copyright file="AadWebBrowser.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;


namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl
{
    // The code below is based on the MSAL implementation for interactive login on .NET Fx
    // Original code at:
    //   https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/master/src/client/Microsoft.Identity.Client/Platforms/net45/CustomWebBrowser.cs
    //   https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/master/src/client/Microsoft.Identity.Client/Platforms/net45/CustomWebBrowser.CustomWebBrowserEvent.cs
    internal sealed class AadWebBrowser : WebBrowser
    {
        const int S_OK = 0;
        const int S_FALSE = 1;
        const int WM_CHAR = 0x102;

        static readonly HashSet<Shortcut> shortcutBlacklist = new HashSet<Shortcut>();

        readonly NetCoreInteractiveAuthenticationDialog owner;
        AadWebBrowserEvent webBrowserEvent;
        AxHost.ConnectionPointCookie webBrowserEventCookie;

        static AadWebBrowser()
        {
            shortcutBlacklist.Add(Shortcut.AltBksp);
            shortcutBlacklist.Add(Shortcut.AltDownArrow);
            shortcutBlacklist.Add(Shortcut.AltLeftArrow);
            shortcutBlacklist.Add(Shortcut.AltRightArrow);
            shortcutBlacklist.Add(Shortcut.AltUpArrow);
        }

        public AadWebBrowser(NetCoreInteractiveAuthenticationDialog owner)
            : base()
        {
            this.owner = owner;
        }

        protected override WebBrowserSiteBase CreateWebBrowserSiteBase()
        {
            return new AadSite(this);
        }
        protected override void CreateSink()
        {
            base.CreateSink();

            object activeXInstance = ActiveXInstance;
            if (activeXInstance != null)
            {
                this.webBrowserEvent = new AadWebBrowserEvent(this);
                this.webBrowserEventCookie = new AxHost.ConnectionPointCookie(activeXInstance, this.webBrowserEvent, typeof(DWebBrowserEvents2));
            }
        }
        protected override void DetachSink()
        {
            if (this.webBrowserEventCookie != null)
            {
                this.webBrowserEventCookie.Disconnect();
                this.webBrowserEventCookie = null;
            }

            base.DetachSink();
        }

        [ClassInterface(ClassInterfaceType.None)]
        sealed class AadWebBrowserEvent : StandardOleMarshalObject, DWebBrowserEvents2
        {
            readonly AadWebBrowser parent;

            public AadWebBrowserEvent(AadWebBrowser parent)
            {
                this.parent = parent;
            }

            public void NavigateError(object pDisp, ref object url, ref object frame, ref object statusCode, ref bool cancel)
            {
                cancel = this.parent.owner.HandleBrowserNavigateError(url != null ? (string)url : string.Empty, frame != null ? (string)frame : string.Empty,
                    statusCode != null ? (int)statusCode : 0, pDisp);
            }

            public void BeforeNavigate2(object pDisp, ref object urlObject, ref object flags, ref object targetFrameName, ref object postData, ref object headers, ref bool cancel)
            {
            }
            public void ClientToHostWindow(ref long cX, ref long cY)
            {
            }
            public void CommandStateChange(long command, bool enable)
            {
            }
            public void DocumentComplete(object pDisp, ref object urlObject)
            {
            }
            public void DownloadBegin()
            {
            }
            public void DownloadComplete()
            {
            }
            public void FileDownload(ref bool cancel)
            {
            }
            public void NavigateComplete2(object pDisp, ref object urlObject)
            {
            }
            public void NewWindow2(ref object ppDisp, ref bool cancel)
            {
            }
            public void OnFullScreen(bool fullScreen)
            {
            }
            public void OnMenuBar(bool menuBar)
            {
            }
            public void OnQuit()
            {
            }
            public void OnStatusBar(bool statusBar)
            {
            }
            public void OnTheaterMode(bool theaterMode)
            {
            }
            public void OnToolBar(bool toolBar)
            {
            }
            public void OnVisible(bool visible)
            {
            }
            public void PrintTemplateInstantiation(object pDisp)
            {
            }
            public void PrintTemplateTeardown(object pDisp)
            {
            }
            public void PrivacyImpactedStateChange(bool bImpacted)
            {
            }
            public void ProgressChange(int progress, int progressMax)
            {
            }
            public void PropertyChange(string szProperty)
            {
            }
            public void SetSecureLockIcon(int secureLockIcon)
            {
            }
            public void StatusTextChange(string text)
            {
            }
            public void TitleChange(string text)
            {
            }
            public void UpdatePageStatus(object pDisp, ref object nPage, ref object fDone)
            {
            }
            public void WindowClosing(bool isChildWindow, ref bool cancel)
            {
            }
            public void WindowSetHeight(int height)
            {
            }
            public void WindowSetLeft(int left)
            {
            }
            public void WindowSetResizable(bool resizable)
            {
            }
            public void WindowSetTop(int top)
            {
            }
            public void WindowSetWidth(int width)
            {
            }
        }

        [ComVisible(true)]
        [ComDefaultInterface(typeof(IDocHostUIHandler))]
        sealed class AadSite : WebBrowserSite, IDocHostUIHandler, ICustomQueryInterface
        {
            const int NotImplemented = -2147467263;

            readonly WebBrowser host;

            public AadSite(WebBrowser host)
                : base(host)
            {
                this.host = host;
            }

            public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
            {
                if (iid == typeof(IDocHostUIHandler).GUID)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IDocHostUIHandler), CustomQueryInterfaceMode.Ignore);
                    return CustomQueryInterfaceResult.Handled;
                }

                ppv = IntPtr.Zero;
                return CustomQueryInterfaceResult.NotHandled;
            }

            public int EnableModeless(bool fEnable)
            {
                return AadSite.NotImplemented;
            }
            public int FilterDataObject(System.Runtime.InteropServices.ComTypes.IDataObject pDO, out System.Runtime.InteropServices.ComTypes.IDataObject ppDORet)
            {
                ppDORet = null;
                return AadWebBrowser.S_FALSE;
            }
            public int GetDropTarget(IOleDropTarget pDropTarget, out IOleDropTarget ppDropTarget)
            {
                ppDropTarget = null;
                return AadWebBrowser.S_OK;
            }
            public int GetExternal(out object ppDispatch)
            {
                ppDispatch = host.ObjectForScripting;
                return AadWebBrowser.S_OK;
            }
            public int GetHostInfo(DOCHOSTUIINFO info)
            {
                const int DOCHOSTUIFLAG_ENABLE_REDIRECT_NOTIFICATION = 0x4000000;
                const int DOCHOSTUIFLAG_NO3DOUTERBORDER = 0x0020000;
                const int DOCHOSTUIFLAG_DISABLE_SCRIPT_INACTIVE = 0x00000010;
                const int DOCHOSTUIFLAG_NOTHEME = 0x00080000;
                const int DOCHOSTUIFLAG_SCROLL_NO = 0x00000008;
                const int DOCHOSTUIFLAG_FLAT_SCROLLBAR = 0x00000080;
                const int DOCHOSTUIFLAG_THEME = 0x00040000;
                const int DOCHOSTUIFLAG_DPI_AWARE = 0x40000000;

                info.dwDoubleClick = 0;
                info.dwFlags = DOCHOSTUIFLAG_NO3DOUTERBORDER | DOCHOSTUIFLAG_DISABLE_SCRIPT_INACTIVE;
                if (NativeMethods.IsProcessDPIAware())
                    info.dwFlags |= DOCHOSTUIFLAG_DPI_AWARE;
                if (host.ScrollBarsEnabled)
                    info.dwFlags |= DOCHOSTUIFLAG_FLAT_SCROLLBAR;
                else
                    info.dwFlags |= DOCHOSTUIFLAG_SCROLL_NO;
                if (Application.RenderWithVisualStyles)
                    info.dwFlags |= DOCHOSTUIFLAG_THEME;
                else
                    info.dwFlags |= DOCHOSTUIFLAG_NOTHEME;
                info.dwFlags |= DOCHOSTUIFLAG_ENABLE_REDIRECT_NOTIFICATION;

                return AadWebBrowser.S_OK;
            }
            public int GetOptionKeyPath(string[] pbstrKey, int dw)
            {
                return AadSite.NotImplemented;
            }
            public int HideUI()
            {
                return AadSite.NotImplemented;
            }
            public int OnDocWindowActivate(bool fActivate)
            {
                return AadSite.NotImplemented;
            }
            public int OnFrameWindowActivate(bool fActivate)
            {
                return AadSite.NotImplemented;
            }
            public int ResizeBorder(COMRECT rect, IOleInPlaceUIWindow doc, bool fFrameWindow)
            {
                return AadSite.NotImplemented;
            }
            public int ShowContextMenu(int dwID, POINT pt, object pcmdtReserved, object pdispReserved)
            {
                switch (dwID)
                {
                    // http://msdn.microsoft.com/en-us/library/aa753264(v=vs.85).aspx
                    case 0x2: // this is edit CONTEXT_MENU_CONTROL
                    case 0x4: // selected text CONTEXT_MENU_TEXTSELECT
                    case 0x9: // CONTEXT_MENU_VSCROLL
                    case 0x10: //CONTEXT_MENU_HSCROLL
                        return AadWebBrowser.S_FALSE; // allow to show menu; Host did not display its UI. MSHTML will display its UI.
                    default:
                        return AadWebBrowser.S_OK;
                }
            }
            public int ShowUI(int dwID, IOleInPlaceActiveObject activeObject, IOleCommandTarget commandTarget, IOleInPlaceFrame frame, IOleInPlaceUIWindow doc)
            {
                return AadWebBrowser.S_FALSE;
            }
            public int TranslateAccelerator(ref MSG msg, ref Guid group, int nCmdID)
            {
                if (msg.message != AadWebBrowser.WM_CHAR)
                {
                    if (ModifierKeys == Keys.Shift || ModifierKeys == Keys.Alt || ModifierKeys == Keys.Control)
                    {
                        int num = ((int)msg.wParam) | (int)ModifierKeys;
                        Shortcut s = (Shortcut)num;
                        if (shortcutBlacklist.Contains(s))
                            return AadWebBrowser.S_OK;
                    }
                }

                return AadWebBrowser.S_FALSE;
            }
            public int TranslateUrl(int dwTranslate, string strUrlIn, out string pstrUrlOut)
            {
                pstrUrlOut = null;
                return AadWebBrowser.S_FALSE;
            }
            public int UpdateUI()
            {
                return AadSite.NotImplemented;
            }
        }
    }
}
