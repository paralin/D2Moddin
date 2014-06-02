using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D2MPMaster
{
    public static class Utils
    {
        private static Random random = new Random((int)DateTime.Now.Ticks);//thanks to McAden
        public static string RandomString(int size)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < size; i++)
            {
                var ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                builder.Append(ch);
            }

            return builder.ToString();
        }

        public static int FindFirstNull(this string[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == null)
                {
                    return i;
                }
            }
            return -1;
        }

        public static string[] CompressToBeginning(this string[] arr)
        {
            int firstNull = FindFirstNull(arr);
            if (firstNull == -1) return arr;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null)
                {
                    var val = arr[i];
                    arr[firstNull] = val;
                    arr[i] = null;
                    firstNull = FindFirstNull(arr);
                }
            }
            return arr;
        }
    }
}
