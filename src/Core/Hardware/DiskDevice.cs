using System;

namespace org.amimchik.MyFileSystem.src.Core.Hardware;

public class DiskDevice(string name, long length)
{
    private FileStream? stream;
    public string Name { get; set; } = name;
    public long Length { get; set; } = length;
    public string Path { get; set; } = string.Empty;

    public void Open(string path)
    {
        stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        Path = path;
    }
    public void Close() => stream?.Close();

    public void Write(byte[] buffer, long offset)
    {
        if (stream is null)
        {
            Console.WriteLine("hello");
        }
        stream!.Seek(offset, SeekOrigin.Begin);
        stream.Write(buffer, 0, buffer.Length);
        stream.Flush(true);
        Console.WriteLine("WRITING TO FILE " + stream.Name);
        /*Console.WriteLine(
            string.Join("-",
                buffer
                .Select(b => b.ToString("x2"))
                .Chunk(4)
                .Select(bytes => string.Join("", bytes)))
        );*/
    }

    public void Read(byte[] buffer, long offset)
    {
        stream!.Seek(offset, SeekOrigin.Begin);
        _ = stream.Read(buffer, 0, buffer.Length);
    }
}
