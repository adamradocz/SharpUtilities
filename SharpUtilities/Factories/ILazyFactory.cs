namespace SharpUtilities.Factories;

public interface ILazyFactory<T>
{
    T Value { get; }
}