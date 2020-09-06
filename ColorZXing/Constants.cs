using System;
using System.Collections.Generic;
using System.Text;

namespace ColorZXing
{
    public class Constants
    {
        public static readonly int PixelSize = 4;
        public static readonly int RGB24PixelSize = 3;
        public static readonly int Gray8PixelSize = 1;

        public static readonly string NoDarkColorError = "No dark color selected. You need to make sure the gray scale value of darker color is smaller than 128.";
        public static readonly string NoLightColorError = "No light color selected. You need to make sure the gray scale value of lighter color is greater than 128.";
    }
}
