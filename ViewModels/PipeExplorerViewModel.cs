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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using System.Windows.Media;
using DynamicData;
using DynamicData.Binding;
using PipeExplorer.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Helpers;
using Splat;

namespace PipeExplorer.ViewModels
{
    class PipeExplorerViewModel : ReactiveValidationObject<PipeExplorerViewModel>
    {
        public ReadOnlyObservableCollection<PipeViewModel> Pipes { get; }
        [Reactive] public string QuickFilter { get; set; }

        private readonly ISettings settings = Locator.Current.GetService<ISettings>();

        public bool StartImmediately
        {
            get => settings.StartImmediately;
            set
            {
                if (value != settings.StartImmediately)
                {
                    this.RaisePropertyChanging();
                    settings.StartImmediately = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public int RefreshInterval
        {
            get => (int)settings.RefreshInterval.TotalSeconds;
            set
            {
                var t = TimeSpan.FromSeconds(value);
                if (t != settings.RefreshInterval)
                {
                    this.RaisePropertyChanging();
                    settings.RefreshInterval = t;
                    this.RaisePropertyChanged();
                }
            }
        }

        public int HighlightDuration
        {
            get => (int)settings.HighlightDuration.TotalSeconds;
            set
            {
                var t = TimeSpan.FromSeconds(value);
                if (t != settings.HighlightDuration)
                {
                    this.RaisePropertyChanging();
                    settings.HighlightDuration = t;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool IsRunning
        {
            get => pipeWatcher.IsRunning;
            set
            {
                if (pipeWatcher.IsRunning != value)
                {
                    this.RaisePropertyChanging();
                    pipeWatcher.IsRunning = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        [Reactive] public ImageSource AppIcon { get; private set; }

        private Func<PipeViewModel, bool> BuildFilter(string filterString)
        {
            if (string.IsNullOrEmpty(filterString))
                return _ => true;
            else
                return p => p.Name.Contains(filterString);
        }

        public ICommand ClearFilterCmd { get; }
        public ICommand StartCmd { get; }
        public ICommand StopCmd { get; }

        static PipeExplorerViewModel()
        {
            Locator.CurrentMutable.Register<IPipeWatcher>(() => new PipeWatcher());
            Locator.CurrentMutable.RegisterConstant<ISettings>(new Settings());
        }

        public PipeExplorerViewModel()
        {
            pipeWatcher = Locator.Current.GetService<IPipeWatcher>();
            pipeWatcher.RefreshInterval = settings.RefreshInterval;
            pipeWatcher.Created += Pipe_Created;
            pipeWatcher.Updated += Pipe_Updated;
            pipeWatcher.Deleted += Pipe_Deleted;

            // delay removal of models marked for removal
            pipesCache
                .Connect()
                .AutoRefresh(p => p.BeingRemoved)
                .Filter(p => p.BeingRemoved)
                .OnItemAdded(p => Debug.WriteLine($"{DateTime.Now:T} BEFORE {p.Name}"))
                .ExpireAfter(p => settings.HighlightDuration)
                .OnItemRemoved(p =>
                {
                    Debug.WriteLine($"{DateTime.Now:T} AFTER {p.Name}");
                    var opt = pipesCache.Lookup(p.Path);
                    // the BeingRemoved flag may be reset if pipe is re-added
                    if (opt.HasValue && opt.Value == p && p.BeingRemoved)
                        pipesCache.RemoveKey(p.Path);
                })
                .Subscribe();

            pipesCache
                .Connect()
                .Batch(TimeSpan.FromMilliseconds(300))
                .Filter(this.WhenValueChanged(x => x.QuickFilter).Select(BuildFilter))
                .AutoRefresh(p => p.Pinned)
                .Sort(SortExpressionComparer<PipeViewModel>.Ascending(p => p))
                .ObserveOnDispatcher()
                .Bind(out var pipesCol)
                .Subscribe();
            Pipes = pipesCol;

            StartCmd = ReactiveCommand.Create(() => IsRunning = true);
            StopCmd = ReactiveCommand.Create(() => IsRunning = false);
            ClearFilterCmd = ReactiveCommand.Create(() => QuickFilter = "",
                this.WhenValueChanged(x => x.QuickFilter).Select(s => !string.IsNullOrEmpty(s)));

            this.WhenValueChanged(x => x.IsRunning)
                .Select(v => (ImageSource)App.Current.FindResource(v ? "AppIconActive" : "AppIcon"))
                .Subscribe(v => AppIcon = v);

            pipesCache.AddOrUpdate(Native.GetPipes().Select(p => new PipeViewModel(p, false)));
            IsRunning = settings.StartImmediately;
        }

        private void Pipe_Created(object sender, PipeWatcherEventArgs e)
        {
            var opt = pipesCache.Lookup(e.Pipe.Path);
            if (opt.HasValue && opt.Value.BeingRemoved)
            {
                pipesCache.RemoveKey(e.Pipe.Path);
                pipesCache.AddOrUpdate(new PipeViewModel(e.Pipe));
            }
            else if (!opt.HasValue)     // avoid re-creation of view models on startup
            {
                pipesCache.AddOrUpdate(new PipeViewModel(e.Pipe));
            }
        }

        private void Pipe_Updated(object sender, PipeWatcherEventArgs e)
        {
            var opt = pipesCache.Lookup(e.Pipe.Path);
            if (opt.HasValue)
                opt.Value.Update(e.Pipe);
        }

        private void Pipe_Deleted(object sender, PipeWatcherEventArgs e)
        {
            var opt = pipesCache.Lookup(e.Pipe.Path);
            if (opt.HasValue)
                opt.Value.MarkForRemoval();
        }

        private readonly IPipeWatcher pipeWatcher;
        private readonly SourceCache<PipeViewModel, string> pipesCache = new SourceCache<PipeViewModel, string>(p => p.Path);
    }
}
