using System;
using System.Collections;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using org.amimchik.MyFileSystem.src.Core.Hardware;

namespace org.amimchik.MyFileSystem.src.Core;

public class FileSystem
{
    private const int HEADER_SIZE = 512;
    public const long MAGIC_NUMB = 0x0f0fcc0f00ffcc00;
    private NameGenerator nameGenerator = new();
    public List<DiskDevice> devices = [];
    private List<(DeviceBlock parent, DeviceBlock child)> mountsEntrys = [];
    private Dictionary<string, DeviceInfo> infos = [];
    private List<MountInfo> mounts = [];
    private Path workingDir = Path.Parse("/");
    private DeviceBlock currentBlock = new() { Name = "", Block = 0 };
    public bool ChangeDirectory(string relP)
    {
        Path relPP = Path.Parse(workingDir.ToString(), relP);

        if (!relPP.Relative)
        {
            workingDir = Path.Parse("/");
            DiskDevice? dev = GetDeviceByMountPoint("/");
            if (dev is null)
            {
                currentBlock = new();
                return true;
            }
            currentBlock.Name = dev.Name;
            currentBlock.Block = 1;
        }

        for (int i = 0; i < relPP.Parts.Count; i++)
        {
            string cP = relPP.Parts[i];
            bool success = true;

            if (cP == ".")
            {
                continue;
            }
            Entry currentDir = GetEntry(currentBlock.Block);
            List<Entry> content = GetDirectoryContent();
            if (cP == "..")
            {
                if (workingDir.Parts.Count == 0)
                {
                    continue;
                }
                workingDir.Parts.RemoveAt(workingDir.Parts.Count - 1);
                if (mountsEntrys.Count != 0 && currentBlock == mountsEntrys[^1].child)
                {
                    currentBlock = mountsEntrys[^1].parent;
                }
                else
                {
                    currentBlock.Block = currentDir.Parent;
                }
                continue;
            }
            DiskDevice? devM = GetDeviceByMountPoint((workingDir + Path.Parse(cP)).ToString());
            if (devM is not null)
            {
                workingDir += Path.Parse(cP);
                mountsEntrys.Add(new() { parent = currentBlock });
                DiskDevice newDev = GetDeviceByMountPoint(workingDir.ToString())!;
                currentBlock.Name = newDev.Name;
                currentBlock.Block = 1;
                continue;
            }
            for (int z = 0; z < content.Count; z++)
            {
                /*Console.WriteLine("FROM CD");
                Console.WriteLine(content[z].Name);
                Console.WriteLine(content[z].Name.Length);
                Console.WriteLine(cP);
                Console.WriteLine(cP.Length);
                foreach (byte b in content[z].Name.ToCharArray().Select(v => (byte)v))
                {
                    Console.Write(b + " ");
                }*/
                if (cP == content[z].Name)
                {
                    workingDir += Path.Parse(cP);
                    currentBlock.Block = content[z].Address;
                    success = true;
                    break;
                }
            }
            if (!success)
            {
                return false;
            }
        }

        return true;
    }
    public List<Entry> ListDirectory() => GetDirectoryContent();
    private static bool CmpStrs(string left, byte[] right)
    {
        for (int i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            byte leftC = (byte)(i < left.Length ? left[i] : 0);
            byte rightC = i < right.Length ? right[i] : (byte)0;

            if (leftC == 0 || rightC == 0)
            {
                return leftC == rightC;
            }

            if (leftC != rightC)
            {
                return false;
            }
        }
        return true;
    }
    public string GetWorkingDirectory()
    {
        return workingDir.ToString();
    }
    public string? Connect(DiskDevice dev)
    {
        if (!devices.Contains(dev))
        {
            devices.Add(dev);
            dev.Name = nameGenerator.GenerateNewName()!;
            ReadDevInfo(dev.Name);
            return dev.Name;
        }
        return null;
    }
    public List<string> GetDevices()
    {
        List<string> devs = [];
        foreach (var dev in devices)
        {
            devs.Add(dev.Name);
        }

        return devs;
    }
    public void Disconnect(string name)
    {
        for (int i = 0; i < devices.Count; i++)
        {
            if (devices[i].Name == name)
            {
                devices.RemoveAt(i);
                nameGenerator.DeleteName(name);
            }
        }
    }
    public bool Mount(string name, string path)
    {
        DiskDevice? dev = GetDevice(name);

        if (dev is null)
        {
            return false;
        }
        if (!infos[name].Correct)
        {
            return false;
        }
        mounts.Add(new(dev, path));
        return true;
    }
    private DiskDevice? GetDevice(string name)
    {
        foreach (var dev in devices)
        {
            if (dev.Name == name)
            {
                return dev;
            }
        }
        return null;
    }
    private bool ReadDevInfo(string name)
    {
        DiskDevice? dev = GetDevice(name);

        if (dev is null)
        {
            return false;
        }

        byte[] buffer = new byte[512];

        do
        {
            DeviceBlock old = currentBlock;

            currentBlock.Name = name;
            currentBlock.Block = 0;

            ReadBlock(buffer);

            currentBlock = old;

        } while (false);

        infos[name] = DeviceInfo.Parse(buffer)!.Value;

        return true;
    }
    private void ReadBlock(byte[] buffer)
    {
        GetDevice(currentBlock.Name)!.Read(buffer, currentBlock.Block * HEADER_SIZE);
    }
    private void WriteBlock(byte[] buffer)
    {
        Console.WriteLine("WRITEBLOCK" + currentBlock.Name);
        var dev = GetDevice(currentBlock.Name);
        dev!.Write(buffer, currentBlock.Block * HEADER_SIZE);
    }
    private DiskDevice? GetDeviceByMountPoint(string path)
    {
        Path pathP = Path.Parse(path);

        return mounts.FirstOrDefault(info => info.Path == pathP).Device;
    }
    private List<Entry> GetDirectoryContent()
    {
        byte[] buffer = new byte[HEADER_SIZE];

        ReadBlock(buffer);

        DeviceBlock old = currentBlock;

        Entry dir = GetEntry(currentBlock.Block);

        List<Entry> entries = [];

        currentBlock.Block = dir.ContentAddress;

        while (dir.Length > 0)
        {
            entries.Add(GetEntry(currentBlock.Block));

            dir.Length -= HEADER_SIZE;
            currentBlock.Block++;
        }

        currentBlock = old;

        return entries;
    }
    private Entry GetEntry(int block)
    {
        byte[] buffer = new byte[HEADER_SIZE];

        DeviceBlock old = currentBlock;

        currentBlock.Block = block;

        ReadBlock(buffer);

        int address = currentBlock.Block;

        currentBlock = old;

        return Entry.Parse(buffer, address)!.Value;
    }
    private int DAlloc(int size)
    {
        List<AllocationInfo> knownAllocations = [];

        DeviceBlock old = currentBlock;

        currentBlock.Block = 1;

        byte[] buffer = new byte[HEADER_SIZE];

        for (; currentBlock.Block < infos[currentBlock.Name].FATsCount; currentBlock.Block++)
        {
            ReadBlock(buffer);

            Entry curFAT = Entry.Parse(buffer, currentBlock.Block)!.Value;

            knownAllocations.Add(new()
            {
                Start = curFAT.ContentAddress,
                Length = curFAT.ReservedBlocks
            });
            MergeAllocs(knownAllocations);
        }

        int devBlocksC = (int)(GetDevice(currentBlock.Name)!.Length / HEADER_SIZE);

        int allocatedBlock = -1;

        DeviceInfo info = infos[currentBlock.Name];

        int sectorsCount = devBlocksC - dev

        currentBlock = old;

        return allocatedBlock;
    }
    private static void MergeAllocs(List<AllocationInfo> allocs)
    {
        Dictionary<int, int> starts = [];

        for (int i = 0; i < allocs.Count; i++)
        {
            starts.Add(allocs[i].Start, i);
        }

        for (int i = 0; i < allocs.Count; i++)
        {
            var alloc = allocs[i];

            int end = alloc.Start + alloc.Length;
            if (starts.ContainsKey(end + 1))
            {
                allocs[starts[end]].Start = allocs[i].Start;
                starts.Remove(end + 1);
            }
        }
    }
    public void Format(string name)
    {
        DeviceBlock old = currentBlock;

        currentBlock.Name = name;
        currentBlock.Block = 0;

        byte[] buffer = new byte[HEADER_SIZE];
        int i = 0;

        do
        {
            byte[] magicNum = BitConverter.GetBytes(MAGIC_NUMB);

            for (int z = 0; z < 8; z++)
            {
                buffer[i++] = magicNum[z];
            }
        } while (false);

        for (int z = 0; z < 4; z++)
        {
            buffer[i++] = 0;
        }

        WriteBlock(buffer);

        currentBlock = old;
    }
    private class Allocator(List<AllocationInfo> allocs, int start, int end, bool allocFromEnd)
    {
        private readonly List<AllocationInfo> allocations = allocs;
        public List<AllocationInfo> Allocations { get => [.. allocs]; }
        public int Start { get; set; } = start;
        public int End { get; set; } = end;
        public bool AllocateFromEnd { get; set; } = allocFromEnd;
        private bool merged = false;
        private const int CHUNK_BLOCKS_COUNT = 4096;

        // TODO: add a AllocateFromEnd flag processing
        public int Allocate(int size)
        {
            int result = -1;

            int lastBlockFreeSectors = 0;

            int chunksCount = DivideAndRoundUp(End - Start, CHUNK_BLOCKS_COUNT);

            for (int i = 0; i < chunksCount; i++)
            {
                Chunk chunk = new
                (
                    AllocateFromEnd
                        ? End - (i + 1) * CHUNK_BLOCKS_COUNT
                        : Start + i * CHUNK_BLOCKS_COUNT,
                    CHUNK_BLOCKS_COUNT
                );

                foreach (var alloc in allocations)
                {
                    chunk.ApplyAllocation(alloc);
                }


            }

            return result;
        }
        private int DivideAndRoundUp(int a, int b) =>
            (a / b) + a % b == 0 ? 0 : 1;
        public void AddAllocation(AllocationInfo info)
        {
            foreach (var alloc in allocations)
            {
                if (alloc.ConflictsWith(info))
                {
                    var oldC = (Console.BackgroundColor, Console.ForegroundColor);
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Error.Write($"""
                    ===> ALLOCATION CONFLICT:
                        Type: Allocation error
                        Source: Allocator
                        Message: Attempted to add an allocation, but it conflicts with an existing one.
                        Details:
                            Existing allocation: {alloc}
                            New allocation     : {info}
                        Actions: Add, Return, Exit
                        Choose an action [are]: 
                    """);
                    string? action = Console.ReadLine()!;
                    (Console.BackgroundColor, Console.ForegroundColor) = oldC;
                    if (action is null || action.StartsWith("a"))
                    {
                        allocations.Add(alloc);
                        merged = false;
                        return;
                    }
                    if (action.StartsWith("r"))
                    {
                        return;
                    }
                    if (action.StartsWith("e"))
                    {
                        Environment.Exit(1);
                    }
                    return;
                }
            }
        }
        private class Chunk(int start, int length)
        {
            public int StartBlock { get; } = start;
            public BitArray Sectors { get; } = new(length);
            public int Length { get; } = length;

            public void ApplyAllocation(AllocationInfo alloc)
            {
                if (alloc.Start + alloc.Length < StartBlock ||
                    StartBlock + Length < alloc.Start)
                {
                    return;
                }
                for (int i = 0; i < alloc.Length; i++)
                {
                    if (i + alloc.Start >= StartBlock &&
                        i + alloc.Start < StartBlock + Length)
                    {
                        Sectors[i] = true;
                    }
                }
            }
            public ChunkInfo Allocate(int size)
            {
                int freeBlocksStart = 0;
                int address = -1;
                int FreeBlocksEnd = 0;

                int state = 0;

                int freeBlocks = 0;

                for (int i = 0; i < Length; i++)
                {
                    if (Sectors[i])
                    {
                        if (freeBlocks == 0)
                        {
                            address = i;
                        }
                        if (state == 0)
                        {
                            freeBlocksStart++;
                        }
                        freeBlocks++;
                    }
                }
            }
        }
        private class ChunkInfo(int fbis, int addr, int fbie)
        {
            public int FreeBlocksInStart { get; set; } = fbis;
            public int FreeBlocksInEnd { get; set; } = fbie;
            public int AllocatedAddress { get; set; } = addr;
        }
    }
    private class AllocationInfo
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public bool ConflictsWith(AllocationInfo other)
        {
            return other.Start < this.Start + this.Length &&
                   this.Start < other.Start + other.Length;
        }
    }
    private struct DeviceInfo
    {
        public int FATsCount { get; set; }
        public bool Correct { get; set; }
        public int FirstAvailBlock { get; set; }

        public static DeviceInfo? Parse(byte[] buffer)
        {
            if (buffer.Length != 512)
            {
                return null;
            }

            DeviceInfo info = new();

            int i = 0;

            long magicNum = BitConverter.ToInt64(buffer, i);

            i += 8;

            if (magicNum != MAGIC_NUMB)
            {
                return new()
                {
                    FATsCount = 0,
                    Correct = false
                };
            }
            info.Correct = true;

            info.FATsCount = BitConverter.ToInt32(buffer, i);
            i += 4;

            info.FirstAvailBlock = info.FATsCount + 1;

            return info;
        }
        public readonly void Write(byte[] buffer)
        {
            if (buffer.Length != 512)
            {
                return;
            }

            int i = 0;

            byte[] wBuff = BitConverter.GetBytes(MAGIC_NUMB);

            foreach (var b in wBuff)
            {
                buffer[i++] = b;
            }

            wBuff = BitConverter.GetBytes(FATsCount);

            foreach (var b in wBuff)
            {
                buffer[i++] = b;
            }
        }
    }
    private struct DeviceBlock
    {
        public string Name { get; set; }
        public int Block { get; set; }

        public static bool operator ==(DeviceBlock left, DeviceBlock right)
        {
            return left.Name == right.Name && left.Block == right.Block;
        }
        public static bool operator !=(DeviceBlock left, DeviceBlock right)
        {
            return !(left == right);
        }
        public override readonly string ToString()
        {
            return $"{Name}:[{Block.ToString("%h")}]";
        }
        public override readonly bool Equals(object? obj)
        {
            return obj is not null && obj is DeviceBlock devBlock && this == devBlock;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Name, Block);
        }
    }
    public struct Entry
    {
        public string Name { get; set; }
        public EntryType Type { get; set; }
        public int ContentAddress { get; set; }
        public int Length { get; set; }
        public int ReservedBlocks { get; set; }
        public int Parent { get; set; }
        public int Address { get; set; }
        public static Entry? Parse(byte[] bytes, int address)
        {
            if (bytes.Length != HEADER_SIZE)
            {
                return null;
            }
            Entry entry = new();

            entry.Address = address;

            int i = 0;

            entry.Type = bytes[i++] == 1 ? EntryType.Directory : EntryType.File;

            entry.Name = "";

            for (int z = 0; z < 31; z++, i++)
            {
                if (bytes[i] == 109)
                {
                    /*Console.WriteLine(string.Join("\n",
                        bytes
                        .Select(b => b.ToString("x2"))
                        .Chunk(20)
                        .Select(bytes => string.Join("", bytes))));*/
                }
                if (bytes[i] == 0)
                {
                    continue;
                }
                entry.Name += (char)bytes[i];
            }

            entry.ContentAddress = BitConverter.ToInt32(bytes, i);
            i += 4;

            entry.Length = BitConverter.ToInt32(bytes, i);
            i += 4;

            entry.Parent = BitConverter.ToInt32(bytes, i);
            i += 4;

            entry.ReservedBlocks = entry.Length / HEADER_SIZE +
                entry.Length % HEADER_SIZE == 0 ? 0 : 1;

            entry.Name = entry.Name.Trim();

            return entry;
        }
        public void Write(byte[] buffer)
        {
            if (buffer.Length != HEADER_SIZE)
            {
                return;
            }

            int i = 0;

            buffer[i++] = (byte)(Type == EntryType.Directory ? 1 : 0);

            for (int z = 0; z < 31; z++)
            {
                buffer[i++] = z < Name.Length ? (byte)Name[z] : (byte)0;
            }

            byte[] address = BitConverter.GetBytes(ContentAddress);

            for (int z = 0; z < 4; z++)
            {
                buffer[i++] = address[z];
            }

            byte[] length = BitConverter.GetBytes(Length);

            for (int z = 0; z < 4; z++)
            {
                buffer[i++] = length[z];
            }

            byte[] parent = BitConverter.GetBytes(Parent);

            for (int z = 0; z < 4; z++)
            {
                buffer[i++] = parent[z];
            }
        }
        public override string ToString()
        {
            return $"""
            Name: {Name}; Address: {ContentAddress}
            Type: {Type}; Length:  {Length}
            """;
        }
    }
    public enum EntryType
    {
        Directory,
        File
    }
    public class Path
    {
        public List<string> Parts { get; private set; } = [];
        public bool Relative { get; private set; }

        public static Path Parse(string s)
        {
            Path path = new();
            List<string> parts = [];
            StringBuilder current = new();

            if (s.Length == 0)
            {
                throw new Exception($"Invalid path {s}, parsing error!");
            }

            path.Relative = true;

            if (s[0] == '/')
            {
                path.Relative = false;
            }

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (c == '/')
                {
                    string trimmed = current.ToString().Trim();
                    if (trimmed != string.Empty)
                    {
                        parts.Add(trimmed);
                    }

                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.ToString().Trim() != string.Empty)
            {
                parts.Add(current.ToString());
                current.Clear();
            }

            path.Parts = parts;

            return path;
        }
        public static Path Parse(string curDir, string relPath)
        {
            if (relPath.StartsWith('/'))
            {
                return Parse(relPath);
            }

            return Parse(curDir) + Parse(relPath);
        }
        public Path Concat(Path other)
        {
            return new Path { Parts = [.. Parts, .. other.Parts] };
        }
        public static Path operator +(Path left, Path right) => left.Concat(right);
        public static bool operator !=(Path? left, Path? right) => !(left == right);
        public static bool operator ==(Path? left, Path? right)
        {
            if (left is null || right is null)
            {
                return false;
            }

            if (left.Parts.Count != right.Parts.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Parts.Count; i++)
            {
                if (left.Parts[i] != right.Parts[i])
                {
                    return false;
                }
            }

            return true;
        }
        public override bool Equals(object? obj)
        {
            return obj is not null && obj is Path path && this == path;
        }
        public override string ToString()
        {
            return $"/{string.Join('/', Parts)}";
        }
        public override int GetHashCode()
        {
            return Parts.GetHashCode();
        }
    }
    private class NameGenerator
    {
        private int max = 0;
        private readonly List<int> avail = [];
        public string? GenerateNewName()
        {
            if (max == 'z' - 'a')
            {
                return null;
            }
            if (avail.Count != 0)
            {
                return $"sd{(char)avail[^1]}";
            }
            return $"sd{(char)(max++ + 'a')}";
        }
        public void DeleteName(string name)
        {
            name = name.Trim();

            if (!name.StartsWith("sd") || name.Length != 3)
            {
                return;
            }
            if (name[3] - 'a' > max)
            {
                return;
            }
            if (name[3] - 'a' == max)
            {
                max--;
                return;
            }

            avail.Add(name[3] - 'a');
        }
    }
}