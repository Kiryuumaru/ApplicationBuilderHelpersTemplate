namespace Application.Configuration.Interfaces;

public interface IApplicationConstants
{
    string AppNameSnakeCase { get; }

    string AppTag { get; }

    public string BuildAppBanner(string appVersion)
    {
        return
            $"   ██╗   ██╗██╗ █████╗ ███╗   ██╗ █████╗  \n" +
            $"   ██║   ██║██║██╔══██╗████╗  ██║██╔══██╗ \n" +
            $"   ██║   ██║██║███████║██╔██╗ ██║███████║ \n" +
            $"   ╚██╗ ██╔╝██║██╔══██║██║╚██╗██║██╔══██║ \n" +
            $"    ╚████╔╝ ██║██║  ██║██║ ╚████║██║  ██║ \n" +
            $"     ╚═══╝  ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝╚═╝  ╚═╝ \n" +
            $"                                by meldCX \n" +
            $"\n" +
            $"   {AppNameSnakeCase}\n" +
            $"   v{appVersion}";
    }
}
