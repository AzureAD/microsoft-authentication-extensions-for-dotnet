//------------------------------------------------------------------------------
// <copyright file="CustomWebUi.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Identity.Client.Extensibility;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl
{

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// The code below is based on the MSAL implementation for interactive login on .NET Fx
    /// Original code at:
    ///   https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/master/src/client/Microsoft.Identity.Client/Platforms/net45/WebUI.cs
    ///   https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/master/src/client/Microsoft.Identity.Client/Platforms/net45/StaTaskScheduler.cs
    /// </remarks>
    public sealed class WebBrowserControlWebUi : ICustomWebUi
    {
        SynchronizationContext _context;
        Form _activeForm;
        IntPtr _mainHandle;

        /// <summary>
        /// Constructor
        /// </summary>
        public WebBrowserControlWebUi()
        {
            this._context = SynchronizationContext.Current;
            this._activeForm = Form.ActiveForm;
            this._mainHandle = Process.GetCurrentProcess().MainWindowHandle;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="authorizationUri"></param>
        /// <param name="redirectUri"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Uri> AcquireAuthorizationCodeAsync(Uri authorizationUri, Uri redirectUri, CancellationToken cancellationToken)
        {
            Uri result = null;

            Action authenticateAction =
                new Action(delegate ()
                {
                    using (NetCoreInteractiveAuthenticationDialog dialog = CreateDialog())
                    {
                        result = dialog.Authenticate(authorizationUri, redirectUri);
                    }
                });
            Action<object> authenticateActionWithTcs =
                new Action<object>(delegate (object tcs)
                {
                    try
                    {
                        using (NetCoreInteractiveAuthenticationDialog dialog = CreateDialog())
                        {
                            result = dialog.Authenticate(authorizationUri, redirectUri);
                        }
                        ((TaskCompletionSource<object>)tcs).TrySetResult(null);
                    }
                    catch (Exception e)
                    {
                        // Need to catch the exception here and put on the TCS which is the task we are waiting on so that
                        // the exception comming out of Authenticate is correctly thrown.
                        ((TaskCompletionSource<object>)tcs).TrySetException(e);
                    }
                });

            // If the thread is MTA, it cannot create or communicate with WebBrowser which is a COM control.
            // In this case, we have to create the browser in an STA thread via StaTaskScheduler object.
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
            {
                if (this._context != null)
                {
                    TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
                    this._context.Post(new SendOrPostCallback(authenticateActionWithTcs), tcs);
                    await tcs.Task.ConfigureAwait(false);
                }
                else
                {
                    using (StaTaskScheduler taskScheduler = new StaTaskScheduler())
                    {
                        try
                        {
                            Task.Factory.StartNew(authenticateAction, cancellationToken, TaskCreationOptions.None, taskScheduler).Wait();
                        }
                        catch (AggregateException ae)
                        {
                            Exception innerException = ae.InnerExceptions[0];

                            // In MTA case, AggregateException is two layer deep, so checking the InnerException for that.
                            if (innerException is AggregateException)
                            {
                                innerException = ((AggregateException)innerException).InnerExceptions[0];
                            }

                            throw innerException;
                        }
                    }
                }
            }
            else
            {
                authenticateAction();
            }

            return await Task.Factory.StartNew(() => result).ConfigureAwait(false);
        }

        NetCoreInteractiveAuthenticationDialog CreateDialog()
        {
            if (this._activeForm != null)
                return new NetCoreInteractiveAuthenticationDialog(this._activeForm);

            if (this._mainHandle != IntPtr.Zero)
                return new NetCoreInteractiveAuthenticationDialog(this._mainHandle);

            return new NetCoreInteractiveAuthenticationDialog();
        }

        sealed class StaTaskScheduler : TaskScheduler, IDisposable
        {
            Thread _thread;
            Queue<Task> _tasks;
            bool _isDisposing;

            public StaTaskScheduler()
            {
                this._tasks = new Queue<Task>();
            }

            public override int MaximumConcurrencyLevel
            {
                get
                {
                    return 1;
                }
            }

            public void Dispose()
            {
                if (this._isDisposing)
                {
                    return;
                }

                this._isDisposing = true;
                if (this._thread != null)
                {
                    this._thread.Join();
                }
            }

            protected override void QueueTask(Task task)
            {
                lock (this._tasks)
                {
                    this._tasks.Enqueue(task);
                    if (this._thread == null)
                    {
                        this._thread = StaTaskScheduler.CreateStaThread(this);
                    }
                }
            }
            protected override IEnumerable<Task> GetScheduledTasks()
            {
                lock (this._tasks)
                {
                    return this._tasks.ToArray();
                }
            }
            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                return (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA && base.TryExecuteTask(task));
            }

            static Thread CreateStaThread(StaTaskScheduler scheduler)
            {
                Debug.Assert(scheduler._tasks.Count > 0);

                var localThread = new Thread(
                    new ThreadStart(delegate ()
                    {
                        Task task;
                        do
                        {
                            lock (scheduler._tasks)
                            {
                                if (!scheduler._isDisposing && scheduler._tasks.Count > 0)
                                {
                                    task = scheduler._tasks.Dequeue();
                                }
                                else
                                {
                                    task = null;
                                    if (!scheduler._isDisposing)
                                        scheduler._thread = null;
                                }
                            }

                            if (!scheduler._isDisposing && task != null)
                                scheduler.TryExecuteTask(task);
                        } while (task != null);
                    }));
                localThread.SetApartmentState(ApartmentState.STA);
                localThread.Start();

                return localThread;
            }
        }
    }
}
