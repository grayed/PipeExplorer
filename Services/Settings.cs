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
using System.IO;
using System.Security;
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

        void Load();
        void Save();
    }

    class Settings : ReactiveValidationObject<Settings>, ISettings
    {
        private const string regPath = @"SOFTWARE\PipeExplorer";

        private bool isLoading;

        [Reactive] public TimeSpan HighlightDuration { get; set; } = TimeSpan.FromSeconds(4);       // sec
        [Reactive] public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(1);         // sec
        [Reactive] public bool StartImmediately { get; set; } = true;

        public Settings()
        {
            this.ValidationRule(x => x.HighlightDuration, duration => duration.Ticks >= 0, Properties.Resources.ErrorMustBeNonNegative);
            this.ValidationRule(x => x.RefreshInterval, interval => interval.Ticks > 0, Properties.Resources.ErrorMustBePositive);

            PropertyChanged += delegate { if (!isLoading) Save(); };
        }

        public void Load()
        {
            isLoading = true;
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
                }
            }
            catch (SecurityException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            finally
            {
                isLoading = false;
            }
        }

        public async void Save()
        {
            try
            {
                var regKey = Registry.CurrentUser.CreateSubKey(regPath);
                if (regKey == null)
                {
                    await ((MetroWindow)App.Current.MainWindow).ShowMessageAsync(Properties.Resources.ErrorAccessingRegistry, Properties.Resources.ErrorCouldNotSaveSettings);
                    return;
                }
                using (regKey)
                {
                    regKey.SetValue("Highlight duration", (int)HighlightDuration.TotalSeconds, RegistryValueKind.DWord);
                    regKey.SetValue("Refresh interval", (int)RefreshInterval.TotalSeconds, RegistryValueKind.DWord);
                    regKey.SetValue("Start immediately", StartImmediately, RegistryValueKind.DWord);
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
