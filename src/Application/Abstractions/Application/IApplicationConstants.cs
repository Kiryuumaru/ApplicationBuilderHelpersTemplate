namespace Application.Abstractions.Application;

public interface IApplicationConstants
{
    string AppName { get; }

    string AppTitle { get; }

    string AppDescription { get; }

    string Version { get; }
}