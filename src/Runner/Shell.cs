using System;

namespace org.amimchik.MyFileSystem.src.Runner;

public class Shell(Core.FileSystem fs)
{
    private Core.FileSystem fs = fs;
    private string GetPrompt()
    {
        return $"{fs.GetWorkingDirectory()} # ";
    }
    public bool Run()
    {
        if (Console.CursorLeft != 0)
        {
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write("%\n");
            Console.ResetColor();
        }

        Console.Write(GetPrompt());

        string? input = Console.ReadLine();

        if (input is null)
        {
            return false;
        }

        string[] parts = input.Split(' ');

        if (parts.Length == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(input))
            return true;

        if (parts[0] == "exit")
        {
            return false;
        }
        if (parts[0] == "ls")
        {
            foreach (var entry in fs.ListDirectory())
            {
                if (Console.CursorLeft <= Console.WindowLeft - 10)
                {
                    Console.WriteLine();
                }
                Console.Write(entry.Name + "\t");
            }
            Console.WriteLine();
            return true;
        }
        if (parts[0] == "clear")
        {
            Console.Clear();
            return true;
        }
        if (parts[0] == "cd")
        {
            if (parts.Length < 2)
            {
                Console.WriteLine("cd: to less arguments");
            }
            Console.Write(fs.ChangeDirectory(parts[1]) ? "" : "Error!\n");
            return true;
        }

        Console.WriteLine("Unknown command: " + input);
        return true;
    }
}
