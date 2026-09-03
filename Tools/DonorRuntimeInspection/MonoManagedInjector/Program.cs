using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
    private const uint ProcessCreateThread = 0x0002;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessVmWrite = 0x0020;
    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint PageExecuteReadWrite = 0x40;
    private const uint DontResolveDllReferences = 0x00000001;
    private const uint WaitObject0 = 0;

    private static int Main(string[] args)
    {
        if (args.Length != 6)
        {
            Console.Error.WriteLine(
                "Usage: MonoManagedInjector <pid> <mono.dll> <payload.dll> " +
                "<namespace> <class> <method>");
            return 2;
        }

        int pid = int.Parse(args[0]);
        string monoPath = Path.GetFullPath(args[1]);
        string payloadPath = Path.GetFullPath(args[2]);
        string typeNamespace = args[3];
        string typeName = args[4];
        string methodName = args[5];

        Process process = Process.GetProcessById(pid);
        ProcessModule remoteMono = process.Modules.Cast<ProcessModule>()
            .Single(module => string.Equals(
                module.ModuleName,
                "mono.dll",
                StringComparison.OrdinalIgnoreCase));

        IntPtr localMono = LoadLibraryEx(
            monoPath,
            IntPtr.Zero,
            DontResolveDllReferences);
        if (localMono == IntPtr.Zero)
        {
            ThrowLastWin32("LoadLibraryEx(mono.dll)");
        }

        IntPtr processHandle = IntPtr.Zero;
        IntPtr remoteBlock = IntPtr.Zero;
        IntPtr threadHandle = IntPtr.Zero;
        try
        {
            var functions = new Dictionary<string, ulong>();
            foreach (string name in new[]
            {
                "mono_get_root_domain",
                "mono_thread_attach",
                "mono_domain_assembly_open",
                "mono_assembly_get_image",
                "mono_class_from_name",
                "mono_class_get_method_from_name",
                "mono_runtime_invoke",
                "mono_thread_detach",
            })
            {
                IntPtr localAddress = GetProcAddress(localMono, name);
                if (localAddress == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        "mono.dll export not found: " + name);
                }

                long rva = localAddress.ToInt64() - localMono.ToInt64();
                functions.Add(
                    name,
                    unchecked((ulong)(remoteMono.BaseAddress.ToInt64() + rva)));
            }

            processHandle = OpenProcess(
                ProcessCreateThread |
                ProcessQueryInformation |
                ProcessVmOperation |
                ProcessVmRead |
                ProcessVmWrite,
                false,
                pid);
            if (processHandle == IntPtr.Zero)
            {
                ThrowLastWin32("OpenProcess");
            }

            remoteBlock = VirtualAllocEx(
                processHandle,
                IntPtr.Zero,
                (nuint)0x10000,
                MemCommit | MemReserve,
                PageExecuteReadWrite);
            if (remoteBlock == IntPtr.Zero)
            {
                ThrowLastWin32("VirtualAllocEx");
            }

            ulong block = unchecked((ulong)remoteBlock.ToInt64());
            ulong codeAddress = block;
            ulong slotDomain = block + 0x4000;
            ulong slotThread = block + 0x4008;
            ulong slotAssembly = block + 0x4010;
            ulong slotImage = block + 0x4018;
            ulong slotClass = block + 0x4020;
            ulong slotMethod = block + 0x4028;
            ulong slotException = block + 0x4030;
            ulong stringCursor = block + 0x5000;

            var strings = new List<(ulong Address, byte[] Bytes)>();
            ulong payloadString = AddString(payloadPath, ref stringCursor, strings);
            ulong namespaceString = AddString(typeNamespace, ref stringCursor, strings);
            ulong classString = AddString(typeName, ref stringCursor, strings);
            ulong methodString = AddString(methodName, ref stringCursor, strings);

            byte[] code = BuildShellcode(
                functions,
                payloadString,
                namespaceString,
                classString,
                methodString,
                slotDomain,
                slotThread,
                slotAssembly,
                slotImage,
                slotClass,
                slotMethod,
                slotException);
            Write(processHandle, (IntPtr)(long)codeAddress, code);
            foreach ((ulong address, byte[] bytes) in strings)
            {
                Write(processHandle, (IntPtr)(long)address, bytes);
            }

            Write(processHandle, (IntPtr)(long)slotException, new byte[8]);
            FlushInstructionCache(processHandle, remoteBlock, (nuint)code.Length);
            threadHandle = CreateRemoteThread(
                processHandle,
                IntPtr.Zero,
                0,
                remoteBlock,
                IntPtr.Zero,
                0,
                out uint threadId);
            if (threadHandle == IntPtr.Zero)
            {
                ThrowLastWin32("CreateRemoteThread");
            }

            uint wait = WaitForSingleObject(threadHandle, 15000);
            if (wait != WaitObject0)
            {
                throw new TimeoutException(
                    "Remote Mono bootstrap did not finish within 15 seconds.");
            }

            if (!GetExitCodeThread(threadHandle, out uint exitCode))
            {
                ThrowLastWin32("GetExitCodeThread");
            }

            ulong domain = ReadUInt64(processHandle, slotDomain);
            ulong thread = ReadUInt64(processHandle, slotThread);
            ulong assembly = ReadUInt64(processHandle, slotAssembly);
            ulong image = ReadUInt64(processHandle, slotImage);
            ulong klass = ReadUInt64(processHandle, slotClass);
            ulong method = ReadUInt64(processHandle, slotMethod);
            ulong exception = ReadUInt64(processHandle, slotException);
            Console.WriteLine(
                $"remoteThread={threadId} exit={exitCode} " +
                $"domain=0x{domain:X} thread=0x{thread:X} " +
                $"assembly=0x{assembly:X} image=0x{image:X} " +
                $"class=0x{klass:X} method=0x{method:X} " +
                $"exception=0x{exception:X}");

            if (exitCode != 0 || exception != 0 ||
                domain == 0 || thread == 0 || assembly == 0 ||
                image == 0 || klass == 0 || method == 0)
            {
                return 10;
            }

            return 0;
        }
        finally
        {
            if (threadHandle != IntPtr.Zero)
            {
                CloseHandle(threadHandle);
            }

            if (remoteBlock != IntPtr.Zero && processHandle != IntPtr.Zero)
            {
                VirtualFreeEx(processHandle, remoteBlock, 0, MemRelease);
            }

            if (processHandle != IntPtr.Zero)
            {
                CloseHandle(processHandle);
            }

            if (localMono != IntPtr.Zero)
            {
                FreeLibrary(localMono);
            }
        }
    }

    private static byte[] BuildShellcode(
        IReadOnlyDictionary<string, ulong> functions,
        ulong payload,
        ulong typeNamespace,
        ulong typeName,
        ulong methodName,
        ulong slotDomain,
        ulong slotThread,
        ulong slotAssembly,
        ulong slotImage,
        ulong slotClass,
        ulong slotMethod,
        ulong slotException)
    {
        var code = new List<byte>(512);
        Emit(code, 0x48, 0x83, 0xEC, 0x28); // sub rsp, 40

        Call0(code, functions["mono_get_root_domain"]);
        StoreRax(code, slotDomain);
        RequireRax(code, 1);

        LoadRcx(code, slotDomain);
        Call(code, functions["mono_thread_attach"]);
        StoreRax(code, slotThread);
        RequireRax(code, 2);

        LoadRcx(code, slotDomain);
        MovRdx(code, payload);
        Call(code, functions["mono_domain_assembly_open"]);
        StoreRax(code, slotAssembly);
        RequireRax(code, 3);

        LoadRcx(code, slotAssembly);
        Call(code, functions["mono_assembly_get_image"]);
        StoreRax(code, slotImage);
        RequireRax(code, 4);

        LoadRcx(code, slotImage);
        MovRdx(code, typeNamespace);
        MovR8(code, typeName);
        Call(code, functions["mono_class_from_name"]);
        StoreRax(code, slotClass);
        RequireRax(code, 5);

        LoadRcx(code, slotClass);
        MovRdx(code, methodName);
        Emit(code, 0x45, 0x31, 0xC0); // xor r8d, r8d
        Call(code, functions["mono_class_get_method_from_name"]);
        StoreRax(code, slotMethod);
        RequireRax(code, 6);

        LoadRcx(code, slotMethod);
        Emit(code, 0x31, 0xD2); // xor edx, edx
        Emit(code, 0x45, 0x31, 0xC0); // xor r8d, r8d
        MovR9(code, slotException);
        Call(code, functions["mono_runtime_invoke"]);

        LoadRcx(code, slotThread);
        Call(code, functions["mono_thread_detach"]);
        Emit(code, 0x31, 0xC0); // xor eax, eax
        Emit(code, 0x48, 0x83, 0xC4, 0x28); // add rsp, 40
        Emit(code, 0xC3); // ret
        return code.ToArray();
    }

    private static void RequireRax(List<byte> code, uint failureCode)
    {
        Emit(code, 0x48, 0x85, 0xC0); // test rax, rax
        Emit(code, 0x75, 0x0A); // jne +10
        Emit(code, 0xB8);
        EmitUInt32(code, failureCode);
        Emit(code, 0x48, 0x83, 0xC4, 0x28);
        Emit(code, 0xC3);
    }

    private static void Call0(List<byte> code, ulong address)
    {
        Call(code, address);
    }

    private static void Call(List<byte> code, ulong address)
    {
        Emit(code, 0x48, 0xB8);
        EmitUInt64(code, address);
        Emit(code, 0xFF, 0xD0);
    }

    private static void LoadRcx(List<byte> code, ulong address)
    {
        Emit(code, 0x48, 0xA1);
        EmitUInt64(code, address);
        Emit(code, 0x48, 0x89, 0xC1);
    }

    private static void StoreRax(List<byte> code, ulong address)
    {
        Emit(code, 0x48, 0xA3);
        EmitUInt64(code, address);
    }

    private static void MovRdx(List<byte> code, ulong value)
    {
        Emit(code, 0x48, 0xBA);
        EmitUInt64(code, value);
    }

    private static void MovR8(List<byte> code, ulong value)
    {
        Emit(code, 0x49, 0xB8);
        EmitUInt64(code, value);
    }

    private static void MovR9(List<byte> code, ulong value)
    {
        Emit(code, 0x49, 0xB9);
        EmitUInt64(code, value);
    }

    private static ulong AddString(
        string value,
        ref ulong cursor,
        ICollection<(ulong Address, byte[] Bytes)> strings)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
        ulong address = cursor;
        strings.Add((address, bytes));
        cursor += (ulong)bytes.Length;
        cursor = (cursor + 7UL) & ~7UL;
        return address;
    }

    private static void Write(IntPtr process, IntPtr address, byte[] bytes)
    {
        if (!WriteProcessMemory(
                process,
                address,
                bytes,
                (nuint)bytes.Length,
                out nuint written) ||
            written != (nuint)bytes.Length)
        {
            ThrowLastWin32("WriteProcessMemory");
        }
    }

    private static ulong ReadUInt64(IntPtr process, ulong address)
    {
        byte[] bytes = new byte[8];
        if (!ReadProcessMemory(
                process,
                (IntPtr)(long)address,
                bytes,
                (nuint)bytes.Length,
                out nuint read) ||
            read != 8)
        {
            ThrowLastWin32("ReadProcessMemory");
        }

        return BitConverter.ToUInt64(bytes, 0);
    }

    private static void Emit(List<byte> code, params byte[] bytes)
    {
        code.AddRange(bytes);
    }

    private static void EmitUInt32(List<byte> code, uint value)
    {
        code.AddRange(BitConverter.GetBytes(value));
    }

    private static void EmitUInt64(List<byte> code, ulong value)
    {
        code.AddRange(BitConverter.GetBytes(value));
    }

    private static void ThrowLastWin32(string operation)
    {
        throw new Win32Exception(
            Marshal.GetLastWin32Error(),
            operation + " failed");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint desiredAccess,
        bool inheritHandle,
        int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(
        IntPtr process,
        IntPtr address,
        nuint size,
        uint allocationType,
        uint protection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFreeEx(
        IntPtr process,
        IntPtr address,
        nuint size,
        uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(
        IntPtr process,
        IntPtr baseAddress,
        byte[] buffer,
        nuint size,
        out nuint written);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        IntPtr process,
        IntPtr baseAddress,
        byte[] buffer,
        nuint size,
        out nuint read);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(
        IntPtr process,
        IntPtr threadAttributes,
        nuint stackSize,
        IntPtr startAddress,
        IntPtr parameter,
        uint creationFlags,
        out uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(
        IntPtr handle,
        uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeThread(
        IntPtr thread,
        out uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(
        string fileName,
        IntPtr file,
        uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(
        IntPtr module,
        string procedureName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushInstructionCache(
        IntPtr process,
        IntPtr baseAddress,
        nuint size);
}
