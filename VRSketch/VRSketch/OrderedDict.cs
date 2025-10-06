using System.Collections;
using System.Collections.Generic;


namespace VRSketch
{
    public class OrderedDict<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        readonly Dictionary<TKey, TValue> dict = new Dictionary<TKey, TValue>();
        readonly List<TKey> keys_in_order = new List<TKey>();


        public IReadOnlyList<TKey> Keys => keys_in_order;
        public bool TryGetValue(TKey key, out TValue value) => dict.TryGetValue(key, out value);
        public bool ContainsKey(TKey key) => dict.ContainsKey(key);
        public int Count => keys_in_order.Count;

        public TValue this[TKey key]
        {
            get => dict[key];
            set
            {
                bool is_old = dict.ContainsKey(key);
                dict[key] = value;
                if (!is_old)
                    keys_in_order.Add(key);
            }
        }

        public void Add(TKey key, TValue value)
        {
            dict.Add(key, value);   /* raises here if key already present */
            keys_in_order.Add(key);
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (var key in keys_in_order)
                    yield return dict[key];
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var key in keys_in_order)
                yield return new KeyValuePair<TKey, TValue>(key, dict[key]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Clear()
        {
            dict.Clear();
            keys_in_order.Clear();
        }
    }
}
