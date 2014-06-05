using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;

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

    public class ConcurrentObservableCollection<t> : ObservableCollection<t>
    {
        // Override the event so this class can access it
        public override event NotifyCollectionChangedEventHandler CollectionChanged;

        public ConcurrentObservableCollection(IEnumerable<t> collection) : base(collection) { }
        public ConcurrentObservableCollection(List<t> collection) : base(collection) { }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            // Be nice - use BlockReentrancy like MSDN said
            using (BlockReentrancy())
            {
                var eventHandler = CollectionChanged;
                if (eventHandler != null)
                {
                    Delegate[] delegates = eventHandler.GetInvocationList();
                    // Walk thru invocation list
                    foreach (NotifyCollectionChangedEventHandler handler in delegates)
                    {
                        handler(this, e);
                    }
                }
            }
        }
    }
}
