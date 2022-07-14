namespace SharpUtilities.Factories;

public interface IFactory<T> where T : class
{
    T CreateObject();
}
