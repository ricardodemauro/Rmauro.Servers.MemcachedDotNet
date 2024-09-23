namespace Rmauro.Servers.Memcached.Servers.Commands;

public enum CommandType
{
    Add,
    Update,
    Get,
    Clean,
    Unknow,
    Set,
    FlushAll
}

public ref struct Command
{
    public CommandType CommandType;

    public ReadOnlySpan<byte> Key;

    public ReadOnlySpan<byte> Value;

    public Command(CommandType commandType, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        CommandType = commandType;
        Key = key;
        Value = value;
    }
}