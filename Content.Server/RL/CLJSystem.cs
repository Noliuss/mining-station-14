using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

using clojure.clr.api;

public class CLJSystem : EntitySystem
{
    public override void Initialize()
    {
        Load();
    }

    public void Load()
    {
        var load = clojure.clr.api.Clojure.var("clojure.core", "load");
        load.invoke("startup");
    }
}

[AdminCommand(AdminFlags.Host)]
sealed class CLJEvalCommand : IConsoleCommand
{
    public string Command => "ceval";
    public string Description => "Evaluate CLJ expression";
    public string Help => "ceval <expression>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var eval = clojure.clr.api.Clojure.var("clojure.core", "eval");
        var read_string = clojure.clr.api.Clojure.var("clojure.core", "read-string");
        var str = clojure.clr.api.Clojure.var("clojure.core", "str");
        var result = eval.invoke(read_string.invoke(args[0]));
        shell.WriteLine((string)str.invoke(result));
    }
}

[AdminCommand(AdminFlags.Host)]
sealed class CLJReloadCommand : IConsoleCommand
{
    public string Command => "creload";
    public string Description => "Reload CLJ startup file";
    public string Help => "creload";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sysMan = IoCManager.Resolve<IEntitySystemManager>();
        var clj = sysMan.GetEntitySystem<CLJSystem>();
        clj.Load();
    }
}
