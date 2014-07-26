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

        public static string TimeFromNow(DateTime dt, bool useUtc)
        {
            if (dt < DateTime.Now)
                return "about sometime ago";
            TimeSpan span = dt - (useUtc ? DateTime.UtcNow : DateTime.Now);
            if (span.Days > 365)
            {
                int years = (span.Days / 365);
                return String.Format("about {0} {1} from now", years, years == 1 ? "year" : "years");
            }
            if (span.Days > 30)
            {
                int months = (span.Days / 30);
                return String.Format("about {0} {1} from now", months, months == 1 ? "month" : "months");
            }
            if (span.Days > 0)
                return String.Format("about {0} {1} from now", span.Days, span.Days == 1 ? "day" : "days");
            if (span.Hours > 0)
                return String.Format("about {0} {1} from now", span.Hours, span.Hours == 1 ? "hour" : "hours");
            if (span.Minutes > 0)
                return String.Format("about {0} {1} from now", span.Minutes, span.Minutes == 1 ? "minute" : "minutes");
            if (span.Seconds > 5)
                return String.Format("about {0} seconds from now", span.Seconds);
            if (span.Seconds == 0)
                return "just now";
            return string.Empty;
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

        public static string ToSteamID64(this int accountid)
        {
            return (accountid + 76561197960265728) + "";
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
