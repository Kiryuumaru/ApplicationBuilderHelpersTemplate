namespace Application.Configuration.Interfaces;

public interface IApplicationConstants
{
    string AppName { get; }

    string AppTitle { get; }

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
            $"   {AppName}\n" +
            $"   v{appVersion}";
    }
}
