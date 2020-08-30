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
using System.Windows;
using PipeExplorer.Services;
using Splat;

namespace PipeExplorer
{
    /// <summary>
    /// Interaction logic for PipeExplorerMainWindow.xaml
    /// </summary>
    public partial class PipeExplorerMainWindow
    {
        public PipeExplorerMainWindow()
        {
            InitializeComponent();

            var settings = Locator.Current.GetService<ISettings>();
            // settings are loaded by our viewmodel already
            this.WindowState = settings.WindowState;
            if (WindowState == WindowState.Normal)
            {
                var rect = settings.WindowPosition;
                if (rect.Width > 0 && rect.Height > 0)
                {
                    if (rect.Width > SystemParameters.VirtualScreenWidth)
                        rect.Width = SystemParameters.VirtualScreenWidth;
                    if (rect.Height > SystemParameters.VirtualScreenHeight)
                        rect.Height = SystemParameters.VirtualScreenHeight;
                
                    if (rect.Right > SystemParameters.VirtualScreenWidth)
                        rect.Offset(SystemParameters.VirtualScreenWidth - rect.Right, 0);
                    else if (rect.Left < SystemParameters.VirtualScreenLeft)
                        rect.Offset(SystemParameters.VirtualScreenLeft - rect.Left, 0);

                    if (rect.Bottom > SystemParameters.VirtualScreenHeight)
                        rect.Offset(0, SystemParameters.VirtualScreenWidth - rect.Bottom);
                    else if (rect.Top < SystemParameters.VirtualScreenTop)
                        rect.Offset(0, SystemParameters.VirtualScreenTop - rect.Top);

                    Width = rect.Width;
                    Height = rect.Height;
                    Left = rect.Left;
                    Top = rect.Top;
                }
            }

            int width;
            if (settings.ColumnWidths.TryGetValue("Name", out width) && width >= 0)
                ColumnName.Width = width;
            if (settings.ColumnWidths.TryGetValue("Connections", out width) && width >= 0)
                ColumnConnections.Width = width;
            if (settings.ColumnWidths.TryGetValue("Created", out width) && width >= 0)
                ColumnCreated.Width = width;
            if (settings.ColumnWidths.TryGetValue("Hint", out width) && width >= 0)
                ColumnHint.Width = width;
            if (settings.ColumnWidths.TryGetValue("ACL", out width) && width >= 0)
                ColumnAcl.Width = width;
        }

        protected override void OnClosed(EventArgs e)
        {
            var settings = Locator.Current.GetService<ISettings>();

            settings.WindowState = WindowState;
            if (WindowState == WindowState.Normal)
                settings.WindowPosition = new Rect(Left, Top, Width, Height);

            settings.ColumnWidths.Clear();
            settings.ColumnWidths["Name"] = (int)ColumnName.ActualWidth;
            settings.ColumnWidths["Connections"] = (int)ColumnConnections.ActualWidth;
            settings.ColumnWidths["Created"] = (int)ColumnCreated.ActualWidth;
            settings.ColumnWidths["Hint"] = (int)ColumnHint.ActualWidth;
            settings.ColumnWidths["ACL"] = (int)ColumnAcl.ActualWidth;

            settings.Save();
            base.OnClosed(e);
        }
    }
}
