public interface ICommandHandler
{
    string CommandType { get; }

    void Handle(string json);
}
