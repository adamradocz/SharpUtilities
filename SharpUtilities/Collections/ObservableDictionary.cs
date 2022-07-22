using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

#if NETCOREAPP3_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace SharpUtilities.ObservableCollections;

// Based on: https://gist.github.com/kzu/cfe3cb6e4fe3efea6d24

/// <summary>
/// Provides a dictionary for use with data binding.
/// </summary>
/// <typeparam name="TKey">Specifies the type of the keys in this collection.</typeparam>
/// <typeparam name="TValue">Specifies the type of the values in this collection.</typeparam>
public class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged
{
    readonly IDictionary<TKey, TValue> _dictionary;

    /// <inheritdoc cref="ICollection.Count"/>
    public bool IsReadOnly => _dictionary.IsReadOnly;

    /// <inheritdoc cref="ICollection.Count"/>
    public int Count => _dictionary.Count;

    /// <inheritdoc cref="IDictionary.Keys"/>
    public ICollection<TKey> Keys => _dictionary.Keys;

    /// <inheritdoc cref="IDictionary.Values"/>
    public ICollection<TValue> Values => _dictionary.Values;

    /// <summary>
    /// Gets or sets the element with the specified key.
    /// </summary>
    /// <param name="key">The elem with the specified key.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public TValue this[TKey key]
    {
        get => _dictionary[key];
        set
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (_dictionary.TryGetValue(key, out var existing))
            {
                _dictionary[key] = value;

                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
                    new KeyValuePair<TKey, TValue>(key, value),
                    new KeyValuePair<TKey, TValue>(key, existing)));
                PropertyChanged(this, new PropertyChangedEventArgs("Values"));
            }
            else
            {
                Add(key, value);
            }
        }
    }

    /// <summary>Event raised when the collection changes.</summary>
    public event NotifyCollectionChangedEventHandler CollectionChanged = (sender, args) => { };

    /// <summary>Event raised when a property on the collection changes.</summary>
    public event PropertyChangedEventHandler PropertyChanged = (sender, args) => { };

    /// <summary>
    /// Initializes an instance of the class.
    /// </summary>
    public ObservableDictionary() : this(new Dictionary<TKey, TValue>())
    { }

    /// <summary>
    /// Initializes an instance of the class using another dictionary as the key/value store.
    /// </summary>
    public ObservableDictionary(IDictionary<TKey, TValue> dictionary)
    {
        _dictionary = dictionary;
    }

    /// <summary>
    /// Allows derived classes to raise custom property changed events.
    /// </summary>
    protected void RaisePropertyChanged(PropertyChangedEventArgs args) => PropertyChanged(this, args);

    #region IDictionary<TKey,TValue> Members
    /// <inheritdoc cref="IDictionary.Add(object, object)"/>
    public void Add(TKey key, TValue value)
    {
        _dictionary.Add(key, value);

        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
            new KeyValuePair<TKey, TValue>(key, value)));
        PropertyChanged(this, new PropertyChangedEventArgs("Count"));
        PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
        PropertyChanged(this, new PropertyChangedEventArgs("Values"));
    }

#if NETCOREAPP2_0_OR_GREATER
    /// <inheritdoc cref="Dictionary{TKey, TValue}.TryAdd(TKey, TValue)"/>
    public bool TryAdd(TKey key, TValue value)
    {
        if (!_dictionary.TryAdd(key, value))
        {
            return false;
        }

        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
                new KeyValuePair<TKey, TValue>(key, value)));
        PropertyChanged(this, new PropertyChangedEventArgs("Count"));
        PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
        PropertyChanged(this, new PropertyChangedEventArgs("Values"));

        return true;
    }
#else
    public bool TryAdd(TKey key, TValue value)
    {
        if (!_dictionary.ContainsKey(key))
        {
            Add(key, value);
            return true;
        }
        return false;
    }
#endif

    /// <inheritdoc cref="IDictionary.Clear()"/>
    public void Clear()
    {
        _dictionary.Clear();
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        PropertyChanged(this, new PropertyChangedEventArgs("Count"));
        PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
        PropertyChanged(this, new PropertyChangedEventArgs("Values"));
    }

    /// <inheritdoc cref="IDictionary.Contains(object)"/>
    public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

    /// <inheritdoc cref="IDictionary.Remove(object)"/>
    public bool Remove(TKey key)
    {
        if (_dictionary.Remove(key))
        {
            // We specify the change as Reset, because the Remove action was throwing invalid index exception.
            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            PropertyChanged(this, new PropertyChangedEventArgs("Count"));
            PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
            PropertyChanged(this, new PropertyChangedEventArgs("Values"));

            return true;
        }

        return false;
    }

#if NETCOREAPP3_0_OR_GREATER
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _dictionary.TryGetValue(key, out value);
#else
    /// <inheritdoc cref="IDictionary.TryGetValue(TKey, out TValue)"/>
    public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value);
#endif
    #endregion

    #region ICollection<KeyValuePair<TKey,TValue>> Members
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    public bool Contains(KeyValuePair<TKey, TValue> item) => _dictionary.Contains(item);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => _dictionary.CopyTo(array, arrayIndex);

    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
    #endregion

    #region IEnumerable<KeyValuePair<TKey,TValue>> Members
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();
    #endregion
}
