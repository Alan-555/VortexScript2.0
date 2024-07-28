namespace VortexScript;

class FileReader
{
    public static string[] ReadFile(string filePath)
    {
        filePath = "../../../../" + filePath;
        string file = File.ReadAllText(filePath);
        return file.Split("\n");
    }
    public static bool FileExists(string filePath)
    {
        filePath = "../../../../" + filePath;
        return File.Exists(filePath);
    }
}