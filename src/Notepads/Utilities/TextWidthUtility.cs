// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    /// <summary>
    /// How much of the available horizontal space the text editor occupies.
    /// The editor column is centered and the remaining space is split evenly on both sides.
    /// </summary>
    public enum TextWidth
    {
        Full,
        ThreeQuarters,
        Half
    }

    public static class TextWidthUtility
    {
        /// <summary>
        /// Returns the fraction (0, 1] of the available width that the editor column occupies.
        /// </summary>
        public static double GetWidthFraction(TextWidth textWidth)
        {
            switch (textWidth)
            {
                case TextWidth.ThreeQuarters:
                    return 0.75;
                case TextWidth.Half:
                    return 0.5;
                case TextWidth.Full:
                default:
                    return 1.0;
            }
        }
    }
}
