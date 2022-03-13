using System;
using System.Threading;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using EasyHook;
using ClrAnalyzer;
using ClrAnalyzer.Core.Hooks;
using ClrAnalyzer.Core.Compiler;
using static ClrAnalyzer.Core.Compiler.CorJitFlags;
using static ClrAnalyzer.Core.Compiler.CorJitCompiler;
using ClrAnalyzer.Core.Utils;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConsoleApp1
{
    public unsafe class Main : EasyHook.IEntryPoint
    {
        public static ConsoleApp5.FileMonInterface Interface;
        LocalHook CreateFileHook;
        public static LocalHook CompileMethodHook;
        Stack<String> Queue = new Stack<String>();

        public static CorJitCompilerNative compiler;
        public static CompileMethodDel originCompile = null;
        public static CompileMethodDel mCompileMethodDel = null;
        public static IntPtr originCompilePtr;

        //Test test1l;
        public static IntPtr GetCompileMethodPtr() {
            IntPtr pJit = GetJit();
            if (pJit == null) return pJit;
            compiler = Marshal.PtrToStructure<CorJitCompilerNative>(Marshal.ReadIntPtr(pJit));

            originCompile = compiler.CompileMethod;
            IntPtr LocalOriginCompilePtr = Marshal.GetFunctionPointerForDelegate(originCompile);
            originCompilePtr = LocalOriginCompilePtr;
            return LocalOriginCompilePtr;
        }

        public Main(
            RemoteHooking.IContext InContext,
            String InChannelName)
        {
            // connect to host...
            Interface = RemoteHooking.IpcConnectClient<ConsoleApp5.FileMonInterface>(InChannelName);

            Interface.Ping();
        }

        public unsafe void Run(
            RemoteHooking.IContext InContext,
            String InChannelName)
        {
            // install hook...
            try
            {
                CreateFileHook = LocalHook.Create(
                    LocalHook.GetProcAddress("kernel32.dll", "CreateFileW"),
                    new DCreateFile(CreateFile_Hooked),
                    this);

                CreateFileHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });

                IntPtr originCompilePtr = GetCompileMethodPtr();
                if (originCompilePtr == null)
                {
                    RemoteHooking.WakeUpProcess();
                    return;
                }

                mCompileMethodDel = new CompileMethodDel(compileMethodDel);
                Interface.Info("origiCompilePtr is " + originCompilePtr.ToString());

                CompileMethodHook = LocalHook.Create(
                    originCompilePtr,
                    mCompileMethodDel,
                    this);
                CompileMethodHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });
            }
            catch (Exception ExtInfo)
            {
                Interface.ReportException(ExtInfo);
                RemoteHooking.WakeUpProcess();
                return;
            }

            Interface.IsInstalled(RemoteHooking.GetCurrentProcessId());

            RemoteHooking.WakeUpProcess();

            // wait for host process termination...
            try
            {
                while (true)
                {
                    Thread.Sleep(100);

                    // transmit newly monitored file accesses...
                    if (Queue.Count > 0)
                    {
                        String[] Package = null;

                        lock (Queue)
                        {
                            Package = Queue.ToArray();

                            Queue.Clear();
                        }

                        Interface.OnCreateFile(RemoteHooking.GetCurrentProcessId(), Package);
                    }
                    else
                        Interface.Ping();
                }
            }
            catch
            {
            }
        }

        [DllImport(
            "Clrjit.dll",
            CallingConvention = CallingConvention.StdCall,
            SetLastError = true,
            EntryPoint = "getJit",
            BestFitMapping = true)]
        public static extern IntPtr GetJit();

        [UnmanagedFunctionPointer(CallingConvention.StdCall,
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        delegate IntPtr DCreateFile(
            String InFileName,
            UInt32 InDesiredAccess,
            UInt32 InShareMode,
            IntPtr InSecurityAttributes,
            UInt32 InCreationDisposition,
            UInt32 InFlagsAndAttributes,
            IntPtr InTemplateFile);

        // just use a P-Invoke implementation to get native API access from C# (this step is not necessary for C++.NET)
        [DllImport("kernel32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        static extern IntPtr CreateFile(
            String InFileName,
            UInt32 InDesiredAccess,
            UInt32 InShareMode,
            IntPtr InSecurityAttributes,
            UInt32 InCreationDisposition,
            UInt32 InFlagsAndAttributes,
            IntPtr InTemplateFile);

        // this is where we are intercepting all file accesses!

        static IntPtr CreateFile_Hooked(
            String InFileName,
            UInt32 InDesiredAccess,
            UInt32 InShareMode,
            IntPtr InSecurityAttributes,
            UInt32 InCreationDisposition,
            UInt32 InFlagsAndAttributes,
            IntPtr InTemplateFile)
        {

            try
            {
                Main This = (Main)HookRuntimeInfo.Callback;

                lock (This.Queue)
                {
                    This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                        RemoteHooking.GetCurrentThreadId() + "]: \"" + InFileName + "\"");
                }
            }
            catch(Exception e)
            {
            }
            // call original API...
            return CreateFile(
                InFileName,
                InDesiredAccess,
                InShareMode,
                InSecurityAttributes,
                InCreationDisposition,
                InFlagsAndAttributes,
                InTemplateFile);
        }

        internal static CorJitCompiler.CorJitResult compileMethodDel(IntPtr thisPtr, [In] IntPtr corJitInfoPtr, [In] CorInfo* methodInfo,
            CorJitFlag flags, [Out] IntPtr nativeEntry, [Out] IntPtr nativeSizeOfCode)
        {
            Interface.Info("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                RemoteHooking.GetCurrentThreadId() + "]: \"" + " ############# My This compile Method called ##############" + "\"");

            return originCompile(thisPtr, corJitInfoPtr, methodInfo, flags, nativeEntry, nativeSizeOfCode);
        }
    }
}
