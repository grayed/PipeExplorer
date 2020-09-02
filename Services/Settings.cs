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
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Windows;
using DynamicData;
using DynamicData.Binding;
using IX.Observable;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace PipeExplorer.Services
{
    interface ISettings : IReactiveObject
    {
        TimeSpan HighlightDuration { get; set; }
        TimeSpan RefreshInterval { get; set; }
        bool StartImmediately { get; set; }
        bool ReadAcls { get; set; }
        WindowState WindowState { get; set; }
        Rect WindowPosition { get; set; }
        IDictionary<string, int> ColumnWidths { get; }
        ISet<string> PinnedNames { get; }

        void Save();
    }

    class Settings : ReactiveValidationObject<Settings>, ISettings
    {
        private const string regPath = @"SOFTWARE\PipeExplorer";

        [Reactive] public TimeSpan HighlightDuration { get; set; } = TimeSpan.FromSeconds(4);       // sec
        [Reactive] public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(1);         // sec
        [Reactive] public bool StartImmediately { get; set; } = true;
        [Reactive] public bool ReadAcls { get; set; } = false;
        [Reactive] public WindowState WindowState { get; set; } = WindowState.Normal;
        [Reactive] public Rect WindowPosition { get; set; }
        public ObservableDictionary<string, int> ColumnWidths { get; } = new ObservableDictionary<string, int>();
        public ObservableSet<string> PinnedNames { get; } = new ObservableSet<string>();

        IDictionary<string, int> ISettings.ColumnWidths => ColumnWidths;
        ISet<string> ISettings.PinnedNames => PinnedNames;

        public Settings()
        {
            this.ValidationRule(x => x.HighlightDuration, duration => duration.Ticks >= 0, Properties.Resources.ErrorMustBeNonNegative);
            this.ValidationRule(x => x.RefreshInterval, interval => interval.Ticks > 0, Properties.Resources.ErrorMustBePositive);

            Load();

            ColumnWidths.ToObservableChangeSet<ObservableDictionary<string, int>, KeyValuePair<string, int>>()
                .CastToObject()
                .Concat(this.WhenAnyPropertyChanged().ToObservableChangeSet().CastToObject())
                .Concat(PinnedNames.ToObservableChangeSet<ObservableSet<string>, string>().CastToObject())
                .Throttle(TimeSpan.FromSeconds(1))
                .Subscribe(_ => Save());
        }

        private static readonly Regex windowPosRegex = new Regex(@"^\s*([0-9]+)\s+([0-9]+)\s+([0-9]+)\s+([0-9]+)\s*$");
        private static readonly Regex colSpecRegex = new Regex(@"^(\w+)=(\d+)$");

        private void Load()
        {
            try
            {
                var regKey = Registry.CurrentUser.OpenSubKey(regPath) ?? Registry.LocalMachine.OpenSubKey(regPath);
                if (regKey is null)
                    return;
                using (regKey)
                {
                    if (regKey is null)
                        return;
                    if (regKey.GetValue("Highlight duration", 2) is int duration && duration >= 0)
                        HighlightDuration = TimeSpan.FromSeconds(duration);
                    if (regKey.GetValue("Refresh interval", 3) is int interval && interval >= 0)
                        RefreshInterval = TimeSpan.FromSeconds(interval);
                    if (regKey.GetValue("Start immediately", 1) is int n)
                        StartImmediately = n != 0;
                    if (regKey.GetValue("Read ACLs", 0) is int n2)
                        ReadAcls = n2 != 0;
                    if (regKey.GetValue("Window state") is string stateStr && Enum.TryParse<WindowState>(stateStr, true, out var state))
                        WindowState = state;

                    if (WindowState == WindowState.Normal && regKey.GetValue("Window position") is string posStr)
                    {
                        var match = windowPosRegex.Match(posStr);
                        if (match.Success)
                        {
                            var leftStr = match.Groups[1].Value;
                            var topStr = match.Groups[2].Value;
                            var widthStr = match.Groups[3].Value;
                            var heightStr = match.Groups[4].Value;
                            if (int.TryParse(leftStr, out var left) &&
                                int.TryParse(topStr, out var top) &&
                                int.TryParse(widthStr, out var width) && width > 0 &&
                                int.TryParse(heightStr, out var height) && height > 0)
                            {
                                WindowPosition = new Rect(left, top, width, height);
                            }
                        }
                    }

                    ColumnWidths.Clear();
                    if (regKey.GetValue("Columns") is string colStr && !string.IsNullOrWhiteSpace(colStr))
                    {
                        var colSpecs = colStr.Split(" \t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        foreach (var spec in colSpecs)
                        {
                            var match = colSpecRegex.Match(spec);
                            if (match.Success && int.TryParse(match.Groups[2].Value, out var width))
                                ColumnWidths.Add(match.Groups[1].Value, width);
                        }
                    }

                    var pinnedKey = regKey.OpenSubKey("Pinned");
                    if (pinnedKey is null)
                        return;
                    using (pinnedKey)
                    {
                        PinnedNames.AddRange(pinnedKey.GetValueNames());
                    }
                }
            }
            catch (SecurityException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        public async void Save()
        {
            try
            {
                var regKey = Registry.CurrentUser.CreateSubKey(regPath);
                if (regKey is null)
                {
                    await ((MetroWindow)App.Current.MainWindow).ShowMessageAsync(Properties.Resources.ErrorAccessingRegistry, Properties.Resources.ErrorCouldNotSaveSettings);
                    return;
                }
                using (regKey)
                {
                    regKey.SetValue("Highlight duration", (int)HighlightDuration.TotalSeconds, RegistryValueKind.DWord);
                    regKey.SetValue("Refresh interval", (int)RefreshInterval.TotalSeconds, RegistryValueKind.DWord);
                    regKey.SetValue("Start immediately", StartImmediately, RegistryValueKind.DWord);
                    regKey.SetValue("Read ACLs", ReadAcls, RegistryValueKind.DWord);
                    regKey.SetValue("Window state", WindowState.ToString(), RegistryValueKind.String);

                    if (WindowState == WindowState.Normal)
                        regKey.SetValue("Window position", $"{(int)WindowPosition.Left} {(int)WindowPosition.Top} {(int)WindowPosition.Width} {(int)WindowPosition.Height}", RegistryValueKind.String);
                    else
                        regKey.DeleteValue("Window position", false);

                    if (ColumnWidths.Count > 0)
                        regKey.SetValue("Columns", string.Join(" ", ColumnWidths.Select(kv => $"{kv.Key}={kv.Value}")), RegistryValueKind.String);
                    else
                        regKey.DeleteValue("Columns", false);

                    using (var pinnedKey = regKey.CreateSubKey("Pinned"))
                    {
                        if (pinnedKey is null)
                        {
                            await ((MetroWindow)App.Current.MainWindow).ShowMessageAsync(Properties.Resources.ErrorAccessingRegistry, Properties.Resources.ErrorCouldNotSaveSettings);
                            return;
                        }
                        var existingValues = pinnedKey.GetValueNames();
                        
                        var extra = existingValues.ToHashSet();
                        extra.ExceptWith(PinnedNames);
                        foreach (var name in extra)
                            pinnedKey.DeleteValue(name, false);
                        
                        var newNames = PinnedNames.ToHashSet();
                        newNames.ExceptWith(existingValues);
                        foreach (var name in newNames)
                            pinnedKey.SetValue(name, 1, RegistryValueKind.DWord);

                    }
                }
            }
            catch (SecurityException sex)
            {
                await ((MetroWindow)App.Current.MainWindow).ShowMessageAsync(Properties.Resources.ErrorAccessingRegistry, string.Format(Properties.Resources.ErrorCouldNotSaveSettingsFormatted, sex.Message));
            }
            catch (UnauthorizedAccessException uaex)
            {
                await ((MetroWindow)App.Current.MainWindow).ShowMessageAsync(Properties.Resources.ErrorAccessingRegistry, string.Format(Properties.Resources.ErrorCouldNotSaveSettingsFormatted, uaex.Message));
            }
            catch (IOException ioex)
            {
                await ((MetroWindow)App.Current.MainWindow).ShowMessageAsync(Properties.Resources.ErrorAccessingRegistry, string.Format(Properties.Resources.ErrorCouldNotSaveSettingsFormatted, ioex.Message));
            }
        }
    }
}
