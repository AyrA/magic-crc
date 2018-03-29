using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace magic.crc
{
    public static class Unicode
    {
        private static Dictionary<UnicodeCategory, List<char>> Categories;

        static Unicode()
        {
            Init();
        }

        private static void Init()
        {
            Categories = new Dictionary<UnicodeCategory, List<char>>();
            foreach (var Cat in Enum.GetValues(typeof(UnicodeCategory)).OfType<UnicodeCategory>())
            {
                Categories.Add(Cat, new List<char>());
            }
            for (int u = ushort.MinValue; u <= ushort.MaxValue; u++)
            {
                var c = (char)u;
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                Categories[cat].Add(c);
            }
        }

        public static char[] Get(UnicodeCategory Cat)
        {
            if (Enum.IsDefined(Cat.GetType(), Cat))
            {
                return Categories[Cat].ToArray();
            }
            return null;
        }
    }
}
