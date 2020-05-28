using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Extensibility;
using SpiderEye;
using SpiderEye.Linux;
using SpiderEye.Mac;
using SpiderEye.Windows;
using Microsoft.Identity.Client.Extensions.Browsers.Experimental.DefaultOSBrowser;
using System.Diagnostics;

namespace Microsoft.Identity.Client.Extensions.Browsers.Experimental
{
    /// <summary>
    /// 
    /// </summary>
    public class SpiderEyeWebView : ICustomWebUi
    {

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
            Window window = null;


            Action authenticateAction = () =>
            {
                Init();
                window = new Window();
                window.Navigating += (sender, e) =>
                {
                    if (e.Uri.IsLoopback) // todo: better check
                    {
                        result = e.Uri;
                        window.Close();
                    }
                };

                window.Closing += (sender, e) =>
                {
                    if (result == null)
                    {
                        throw new OperationCanceledException();
                    }
                };
                window.UseBrowserTitle = true;
               
                Application.Run(window, authorizationUri.AbsoluteUri);
            };


            Action<object> authenticateActionWithTcs =
              new Action<object>(delegate (object tcs)
              {
                  try
                  {
                      authenticateAction();
                       ((TaskCompletionSource<object>)tcs).TrySetResult(null);
                  }
                  catch (Exception e)
                  {
                      // Need to catch the exception here and put on the TCS which is the task we are waiting on so that
                      // the exception comming out of Authenticate is correctly thrown.
                      ((TaskCompletionSource<object>)tcs).TrySetException(e);
                  }
              });

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
            {
                TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();

                if (SynchronizationContext.Current != null)
                {
                    SynchronizationContext.Current.Post(new SendOrPostCallback(authenticateActionWithTcs), tcs);
                    await tcs.Task.ConfigureAwait(false);
                }
                else
                {
                    using (StaTaskScheduler taskScheduler = new StaTaskScheduler())
                    {
                        await Task.Factory.StartNew(authenticateAction, cancellationToken, TaskCreationOptions.None, taskScheduler)
                            .ConfigureAwait(false);

                        //todo: catch ex
                    }
                }
            }
            else
            {
                authenticateAction();
            }

          


            if (result != null)
            {
                return result;
            }

            throw new OperationCanceledException();
        }




      

        private static void Init()
        {
            if (OperatingSystem.IsWindows())
            {
                WindowsApplication.Init();
            }
            if (OperatingSystem.IsMacOS())
            {
                MacApplication.Init();
            }
            if (OperatingSystem.IsLinux())
            {
                LinuxApplication.Init();
            }
        }
    }

    internal static class OperatingSystem
    {
        public static bool IsWindows() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static bool IsMacOS() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static bool IsLinux() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
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
