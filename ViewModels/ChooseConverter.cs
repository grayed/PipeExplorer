using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PipeExplorer.ViewModels
{
    /// <summary>
    /// Chooses an element from the given sequence based on the value supplied.
    /// </summary>
    class ChooseConverter : IValueConverter
    {
        /// <summary>
        /// Chooses an element from <see cref="IEnumerable"/> in <paramref name="parameter"/> based on <paramref name="value"/>.
        /// </summary>
        /// <param name="value">A <see cref="bool"/>, a <c>null</c> or an <see cref="int"/>.</param>
        /// <param name="targetType">Ignored.</param>
        /// <param name="parameter">An <see cref="IEnumerable"/> containing sequence to choose from.</param>
        /// <param name="culture">Ignored</param>
        /// <returns>For <c>true</c>, <c>false</c> or <c>null</c> returns first, second or third element of the sequence in <paramref name="parameter"/>, respectively.
        /// For integer <paramref name="value"/>, returns corresponding element from the sequence in <paramref name="parameter"/>.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(parameter is IEnumerable enumerable))
                throw new ArgumentException("an IEnumerable parameter must be supplied");

            var enumerator = enumerable.GetEnumerator();
            enumerator.MoveNext();
            switch (value)
            {
                case true:
                    break;
                case false:
                    enumerator.MoveNext();
                    break;
                case null:
                    enumerator.MoveNext();
                    enumerator.MoveNext();
                    break;

                case int idx:
                    if (idx < 0)
                        return DependencyProperty.UnsetValue;
                    while (idx > 0)
                        enumerator.MoveNext();
                    break;

                default:
                    return DependencyProperty.UnsetValue;
            }
            return enumerator.Current;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
