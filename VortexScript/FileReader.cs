namespace Vortex
{
    class FileReader
    {
        public static string[] ReadFile(string filePath){
            filePath = "../../../../script.vort";
            string file = File.ReadAllText(filePath);
            return file.Split("\n"); //TODO: examine
        }
    }
}