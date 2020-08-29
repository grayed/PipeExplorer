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
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PipeExplorer.ViewModels
{
    /// <summary>
    /// Convert between <see cref="bool"/> and <see cref="Visibility"/>, tunable.
    /// </summary>
    [Localizability(LocalizationCategory.NeverLocalize)]
    public sealed class AdvancedBooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// If this property is <c>true</c>, then the <c>true</c> converted value is treated as
        /// <see cref="Visibility.Collapsed"/> or <see cref="Visibility.Hidden"/>, and
        /// <c>false</c> converted value is treated as <see cref="Visibility.Visible"/>.
        /// </summary>
        public bool InvertedLogic { get; set; }

        /// <summary>
        /// Convert <see cref="bool"/> or <see cref="Nullable{Boolean}"/> to <see cref="Visibility"/>.
        /// </summary>
        /// <param name="value">A <see cref="bool"/> or <see cref="Nullable{Boolean}"/> to convert</param>
        /// <param name="targetType">Must be <see cref="Visibility"/>.</param>
        /// <param name="parameter">Optional. May be <see cref="Visibility.Collapsed"/> or <see cref="Visibility.Hidden"/>;
        /// <c>null</c> (i.e., default) maps to <see cref="Visibility.Collapsed"/>.</param>
        /// <param name="culture">Ignored.</param>
        /// <returns>
        /// <see cref="Visibility.Visible"/> if <paramref name="value"/> is <c>true</c> and <see cref="InvertedLogic"/> is <c>false</c>,
        /// or if <paramref name="value"/> is <c>false</c> and <see cref="InvertedLogic"/> is <c>true</c>.
        /// Otherwise, the <paramref name="parameter"/> value is returned, or <see cref="Visibility.Collapsed"/> if <paramref name="parameter"/> is <c>null</c>.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(Visibility))
                return DependencyProperty.UnsetValue;

            Visibility hideMode = Visibility.Collapsed;
            if (parameter is Visibility vis)
            {
                if (vis == Visibility.Visible)
                    throw new ArgumentOutOfRangeException(nameof(parameter), "may not be Visibility.Visible");
                hideMode = vis;
            }

            bool convValue = InvertedLogic;
            if (value is bool bValue)
                convValue = bValue;
            if (InvertedLogic)
                convValue = !convValue;
            return convValue ? Visibility.Visible : hideMode;
        }

        /// <summary>
        /// Convert <see cref="Visibility"/> to <see cref="bool"/>.
        /// </summary>
        /// <param name="value">A <see cref="Visibility"/> value.</param>
        /// <param name="targetType">Must be <see cref="bool"/> or <see cref="Nullable{Boolean}"/>.</param>
        /// <param name="parameter">Ignored.</param>
        /// <param name="culture">Ignored.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is <see cref="Visibility.Visible"/>, <c>false</c> otherwise.
        /// If <see cref="InvertedLogic"/> is set, though, the resulting value gets inverted.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(bool) && targetType != typeof(bool?))
                return DependencyProperty.UnsetValue;

            bool retValue = InvertedLogic;
            if (value is Visibility vis)
                retValue = vis == Visibility.Visible;
            if (InvertedLogic)
                retValue = !retValue;
            return retValue;
        }
    }
}
