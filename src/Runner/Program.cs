using System;
using org.amimchik.MyFileSystem.src.Core;
using org.amimchik.MyFileSystem.src.Core.Hardware;

namespace org.amimchik.MyFileSystem.src.Runner;

public class Program
{
    public static void Main(string[] args)
    {
        DiskDevice dev = new("myDev", 50000);

        dev.Open("myDev");

        FileSystem fs = new();

        string assignedName = fs.Connect(dev)!;

        fs.Format(assignedName);

        do
        {
            FileSystem.Entry root = new();
            root.Parent = 1;
            root.Address = 2;
            root.ReservedBlocks = 1;
            root.Length = 512;
            root.Name = "";
            root.Type = FileSystem.EntryType.Directory;

            byte[] buffer = new byte[512];
            root.Write(buffer);
            dev.Write(buffer, 512);
        } while (false);

        do
        {
            FileSystem.Entry dir = new();
            dir.Parent = 1;
            dir.Address = 3;
            dir.ReservedBlocks = 1;
            dir.Length = 512;
            dir.Name = "myDir";
            dir.Type = FileSystem.EntryType.Directory;

            byte[] buffer = new byte[512];
            dir.Write(buffer);
            dev.Write(buffer, 1024);
        } while (false);

        do
        {
            FileSystem.Entry dir = new();
            dir.Parent = 2;
            dir.Address = 4;
            dir.ReservedBlocks = 0;
            dir.Length = 0;
            dir.Name = "myDir2";
            dir.Type = FileSystem.EntryType.Directory;

            byte[] buffer = new byte[512];
            dir.Write(buffer);
            dev.Write(buffer, 512 + 1024);
        } while (false);

        fs.Mount(assignedName, "/");

        fs.ChangeDirectory("/");

        Shell shell = new(fs);

        while (shell.Run()) ;
    }
}
