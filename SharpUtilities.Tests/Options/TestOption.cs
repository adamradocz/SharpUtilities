namespace SharpUtilities.Tests.Options;

internal class TestOption
{
    public bool TestBool { get; set; }
    public byte TestByte { get; set; }
    public short TestShort { get; set; }
    public int TestInt { get; set; }
    public long TestLong { get; set; }
    public float TestFloat { get; set; }
    public double TestDouble { get; set; }
    public decimal TestDecimal { get; set; }
    public string TestString { get; set; } = string.Empty;
    public char TestChar { get; set; }
    public TestOption? TestSubClass { get; set; }
}
