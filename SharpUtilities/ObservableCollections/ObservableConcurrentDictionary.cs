using System.Collections;
using System.Collections.Concurrent;
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
public class ObservableConcurrentDictionary<TKey, TValue> : IDictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly ConcurrentDictionary<TKey, TValue> _dictionary;

    /// <inheritdoc cref="ICollection.Count"/>
    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).IsReadOnly;

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
                _ = TryAdd(key, value);
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
    public ObservableConcurrentDictionary() : this(new ConcurrentDictionary<TKey, TValue>())
    { }

    /// <summary>
    /// Initializes an instance of the class using another dictionary as the key/value store.
    /// </summary>
    public ObservableConcurrentDictionary(ConcurrentDictionary<TKey, TValue> dictionary)
    {
        _dictionary = dictionary;
    }

    /// <summary>
    /// Allows derived classes to raise custom property changed events.
    /// </summary>
    protected void RaisePropertyChanged(PropertyChangedEventArgs args) => PropertyChanged(this, args);

    #region ConcurrentDictionary<TKey,TValue> Members
    /// <inheritdoc cref="ConcurrentDictionary{TKey, TValue}.TryAdd(TKey, TValue)"/>
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

    /// <inheritdoc cref="ConcurrentDictionary{TKey, TValue}.TryUpdate(TKey, TValue, TValue)"/>
    public bool TryUpdate(TKey key, TValue newValue, TValue comparisonValue)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (!_dictionary.TryGetValue(key, out var value) && value.Equals(comparisonValue))
        {
            return false;
        }

        this[key] = newValue;
        return true;
    }

    /// <inheritdoc cref="ConcurrentDictionary{TKey, TValue}.TryRemove(TKey, out TValue)"/>
    public bool TryRemove(TKey key, out TValue value)
    {
        if (!_dictionary.TryRemove(key, out value))
        {
            return false;
        }

        // We specify the change as Reset, because the Remove action was throwing invalid index exception.
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        PropertyChanged(this, new PropertyChangedEventArgs("Count"));
        PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
        PropertyChanged(this, new PropertyChangedEventArgs("Values"));

        return true;
    }

    /// <inheritdoc cref="ConcurrentDictionary{TKey, TValue}.Clear"/>
    public void Clear()
    {
        _dictionary.Clear();
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        PropertyChanged(this, new PropertyChangedEventArgs("Count"));
        PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
        PropertyChanged(this, new PropertyChangedEventArgs("Values"));
    }
    #endregion

    #region IDictionary<TKey,TValue> Members
    /// <inheritdoc cref="IDictionary.Add(object, object)"/>
    void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => TryAdd(key, value);

    /// <inheritdoc cref="IDictionary.Remove(object)"/>
    /// 
    bool IDictionary<TKey, TValue>.Remove(TKey key) => TryRemove(key, out _);

    /// <inheritdoc cref="IDictionary.Contains(object)"/>
    public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

#if NETCOREAPP3_0_OR_GREATER
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _dictionary.TryGetValue(key, out value);
#else
    /// <inheritdoc cref="IDictionary.TryGetValue(TKey, out TValue)"/>
    public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value);
#endif
    #endregion

    #region ICollection<KeyValuePair<TKey,TValue>> Members
    public bool Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Contains(item);

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => TryAdd(item.Key, item.Value);

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).CopyTo(array, arrayIndex);

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => TryRemove(item.Key, out _);
    #endregion

    #region IEnumerable<KeyValuePair<TKey,TValue>> Members
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();
    #endregion
}
