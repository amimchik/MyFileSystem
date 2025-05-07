using System;
using org.amimchik.MyFileSystem.src.Core.Hardware;

namespace org.amimchik.MyFileSystem.src.Core;

public struct MountInfo(DiskDevice dev, string path)
{
    public DiskDevice Device { get; set; } = dev;
    public FileSystem.Path Path { get; set; } = FileSystem.Path.Parse(path);
}
