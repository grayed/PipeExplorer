#region Licensing information
/*
 * Copyright(c) 2020 Vadim Zhukov<zhuk@openbsd.org>
 * 
 * Permission to use, copy, modify, and distribute this software for any
 * purpose with or without fee is hereby granted, provided that the above
 * copyright notice and this permission notice appear in all copies.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
 * WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS.IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
 * ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
 * WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
 * ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
 * OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using PipeExplorer.Models;
using ReactiveUI;

namespace PipeExplorer.Services
{
    class PipeWatcherEventArgs : EventArgs
    {
        public CollectionChangeAction Action { get; }
        public PipeModel Pipe { get; }

        public PipeWatcherEventArgs(CollectionChangeAction action, PipeModel pipe)
        {
            Action = action;
            Pipe = pipe;
        }
    }

    interface IPipeWatcher
    {
        string Host { get; set; }
        bool IsRunning { get; set; }
        TimeSpan RefreshInterval { get; set; }

        event EventHandler<PipeWatcherEventArgs> Created;
        event EventHandler<PipeWatcherEventArgs> Deleted;
        event EventHandler<PipeWatcherEventArgs> Updated;
    }

    class PipeWatcher : ReactiveObject, IDisposable, IPipeWatcher
    {
        private bool running;
        private bool disposedValue;
        private Timer timer;
        private TimeSpan refreshInterval = TimeSpan.FromSeconds(3);
        private string host = ".";
        private Dictionary<string, PipeModel> prevPipes = new Dictionary<string, PipeModel>();

        public event EventHandler<PipeWatcherEventArgs> Created;
        public event EventHandler<PipeWatcherEventArgs> Updated;
        public event EventHandler<PipeWatcherEventArgs> Deleted;

        public string Host
        {
            get => host;
            set
            {
                if (disposedValue)
                    throw new ObjectDisposedException(ToString());
                if (string.IsNullOrEmpty(value))
                    value = ".";
                if (value != host)
                {
                    if (IsRunning)
                    {
                        IsRunning = false;
                        host = value;
                        IsRunning = true;
                    }
                    else
                    {
                        host = value;
                    }
                }
            }
        }

        public TimeSpan RefreshInterval
        {
            get => refreshInterval;
            set
            {
                if (disposedValue)
                    throw new ObjectDisposedException(ToString());
                if (value == TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "must be non-zero");
                if (value != refreshInterval)
                {
                    refreshInterval = value;
                    if (IsRunning)
                    {
                        timer.Dispose();
                        timer = new Timer(TimerTick, this, new TimeSpan(), RefreshInterval);
                    }
                }
            }
        }

        public bool IsRunning
        {
            get => running;
            set
            {
                if (value && disposedValue)
                    throw new ObjectDisposedException(ToString());
                if (value != running)
                {
                    if (value)
                    {
                        timer = new Timer(TimerTick, this, new TimeSpan(), RefreshInterval);
                    }
                    else
                    {
                        timer?.Dispose();
                        timer = null;
                        prevPipes.Clear();
                    }
                    running = value;
                }
            }
        }

        public PipeWatcher()
        {
        }

        public override string ToString()
        {
            return $"{nameof(PipeWatcher)},{(IsRunning ? "" : " not")} running on {nameof(Host)}=\"{Host}\" each {RefreshInterval}";
        }

        private void TimerTick(object state)
        {
            var newPipes = new Dictionary<string, PipeModel>();
            foreach (var newp in Native.GetPipes(Host))
            {
                // do not blindly call newPipes.Add(): GetPipes() could return duplicated entries (and it's not its fault)
                if (prevPipes.TryGetValue(newp.Name, out var oldp))
                {
                    newPipes[newp.Name] = newp;
                    prevPipes.Remove(newp.Name);
                    // TODO: ACL check/update
                    if (oldp != newp)
                    {
                        Updated?.Invoke(this, new PipeWatcherEventArgs(CollectionChangeAction.Refresh, newp));
                    }
                }
                else if (newPipes.TryGetValue(newp.Name, out var tmp))
                {
                    if (tmp != newp)
                    {
                        newPipes[newp.Name] = newp;
                        Updated?.Invoke(this, new PipeWatcherEventArgs(CollectionChangeAction.Refresh, newp));
                    }
                }
                else
                {
                    newPipes.Add(newp.Name, newp);
                    Created?.Invoke(this, new PipeWatcherEventArgs(CollectionChangeAction.Add, newp));
                }
            }

            foreach (var oldp in prevPipes.Values)
                Deleted?.Invoke(this, new PipeWatcherEventArgs(CollectionChangeAction.Remove, oldp));
            prevPipes = newPipes;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    IsRunning = false;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~PipeWatcher()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
