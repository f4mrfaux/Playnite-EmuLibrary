using System;
using System.Collections.Generic;

namespace EmuLibrary.PlayniteCommon
{
    /// <summary>
    /// Extension methods for collections
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Performs the specified action on each element of the IEnumerable.
        /// </summary>
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (action == null)
                throw new ArgumentNullException(nameof(action));
                
            foreach (T item in source)
            {
                action(item);
            }
        }
    }
}