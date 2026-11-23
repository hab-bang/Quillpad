// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;

    /// <summary>
    /// Typewriter mode keeps the line with the caret in the middle of the editor.
    /// </summary>
    public static class TypewriterUtility
    {
        /// <summary>
        /// The scroll offset that puts a line in the middle of the view. Never below 0: the first lines
        /// stay at the top. The view cannot scroll past the end either, so the last lines stay lower.
        /// </summary>
        /// <param name="lineTop">Top of the caret line in the scrolled content.</param>
        public static double GetCenteredVerticalOffset(double lineTop, double lineHeight, double viewportHeight)
        {
            return Math.Max(0, lineTop + lineHeight / 2 - viewportHeight / 2);
        }
    }
}
