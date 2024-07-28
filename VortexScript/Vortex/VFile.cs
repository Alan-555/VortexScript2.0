using VortexScript.Definitions;

namespace VortexScript.Vortex;

public class VFile
{
    public string Path { get; private set; }
    string[] bufferedFile = [];
    public VFrame FileFrame { set; get; }
    public VContext? TopLevelContext { set; get; }
    public VFile(string path)
    {
        Path = path;
        FileFrame = new(this, 0, "Entrypoint(" + Path + ")");
        if (Interpreter.InternalModules.ContainsKey(GetFileName()))
        {
            throw new UseOfAReservedNameError(GetFileName());
        }
    }
    public void InterpretThisFile(bool entrypoint = false)
    {
        if (entrypoint)
        {
            FileFrame = new(this, 0, "Entrypoint(" + Path + ")");
        }
        else
        {
            FileFrame = new(this, 0, "Acquired(" + Path + ")");
        }
        Interpreter.CallStack.Push(FileFrame);
        TopLevelContext = new VContext([], this, 0, ScopeTypeEnum.topLevel)
        {
            IsMain = entrypoint,
            Name = GetFileName(),

        };
        if (!Interpreter.ActiveModules.TryAdd(GetFileName()[0].ToString().ToUpper() + GetFileName()[1..], TopLevelContext))
        {
            throw new ModuleAlreadyLoadedError(GetFileName());
        }
        FileFrame.ScopeStack.Push(TopLevelContext);
        new Interpreter(this).ExecuteFile();
    }
    public string[] ReadFile()
    {
        if (Path == Program.InteractiveTermMode)
        {

        }
        if (bufferedFile.Length == 0)
        {
            bufferedFile = FileReader.ReadFile(Path);
        }
        return bufferedFile;
    }
    public bool Exists()
    {
        return FileReader.FileExists(Path);
    }

    public string GetFileName()
    {
        return Path.Split(".")[0];
    }
    public string GetFileNameUpper()
    {
        var v = GetFileName();
        return v[0].ToString().ToUpper() + v[1..];
    }
}