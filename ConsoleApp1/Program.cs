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
        static CompilerHook hook = new CompilerHook();
        public static ConsoleApp5.FileMonInterface Interface;
        LocalHook CreateFileHook;
        LocalHook GetJitHook;
        public static LocalHook CompileMethodHook;
        Stack<String> Queue = new Stack<String>();
        private static IntPtr pVTable;
        public static IntPtr defaultCompileMethodPtr;
        public static CompileMethodDel defaultCompileMethod;
        public static CorJitCompilerNative compiler;
        public static CompileMethodDel originCompile = null;
        public static CompileMethodDel mCompileMethodDel = null;
        public static IntPtr mCompileMethodPtr ;
        public static IntPtr originCompilePtr;
        LocalHook TestHook;
        public getC_D originGet_C_D_Del;
        IntPtr originGetC_D = IntPtr.Zero;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public unsafe delegate void MProcessShutdownWorkDel(IntPtr thisPtr, [Out] IntPtr corStaticInfo);

        [UnmanagedFunctionPointer(CallingConvention.StdCall,
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        public delegate CorJitCompiler.CorJitResult DCompileMethodDel(IntPtr thisPtr, [In] IntPtr corJitInfoPtr, [In] CorInfo* methodInfo,
                CorJitFlag flags, [Out] IntPtr nativeEntry, [Out] IntPtr nativeSizeOfCode);


        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public unsafe delegate Byte MisCacheCleanupRequiredDel();

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public unsafe delegate UInt32 MgetMethodAttribs(IntPtr methodHandle);


        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public unsafe delegate int getC_D(IntPtr thisPtr, int test1, int test2);

        public struct Test
        {
            public getC_D testfunc;
        }

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

        public static unsafe void NativeDump(IntPtr corJitInfoPtr, CorInfo* methodInfo,
            CorJitFlag flags, IntPtr nativeEntry, IntPtr nativeSizeOfCode) => CorJitCompiler.DumpMethodInfo(corJitInfoPtr, methodInfo, flags, nativeEntry, nativeSizeOfCode);

        internal static CorJitCompiler.CorJitResult compileMethodDel(IntPtr thisPtr, [In] IntPtr corJitInfoPtr, [In] CorInfo* methodInfo,
            CorJitFlag flags, [Out] IntPtr nativeEntry, [Out] IntPtr nativeSizeOfCode)
        {
            try
            {
                Interface.Info("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    RemoteHooking.GetCurrentThreadId() + "]: \"" + " ############# My This compile Method called ##############" + "\"");
                Interface.Info("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    RemoteHooking.GetCurrentThreadId() + "]: \"" + "############# orginCompile method is ########## " + Marshal.GetFunctionPointerForDelegate(originCompile).ToString() + "\"");
                Interface.Info("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    RemoteHooking.GetCurrentThreadId() + "]: \"" + "############# ilcodes is ########## " + methodInfo->ILCodeSize + "\"");

                return originCompile(thisPtr, corJitInfoPtr, methodInfo, flags, nativeEntry, nativeSizeOfCode);
            }
            catch (Exception execError)
            {
                Interface.ReportException(execError);
                return originCompile(thisPtr, corJitInfoPtr, methodInfo, flags, nativeEntry, nativeSizeOfCode);
            }

        }

        public int getC_D_Del(IntPtr thisPtr, int test1, int test2) {
            Main This = (Main)HookRuntimeInfo.Callback;
            lock (This.Queue)
            {
                This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    RemoteHooking.GetCurrentThreadId() + "]: \"" + " ############# getC_D_Del called ##############" + "\"");
            }
            test1 = 10;
            test2 = 5;
            return originGet_C_D_Del(thisPtr, test1, test2);
        }

        public unsafe void Run(
            RemoteHooking.IContext InContext,
            String InChannelName)
        {
            // install hook...
            try
            {
                //CreateFileHook = LocalHook.Create(
                //    LocalHook.GetProcAddress("kernel32.dll", "CreateFileW"),
                //    new DCreateFile(CreateFile_Hooked),
                //    this);

                //CreateFileHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });

                //originGetC_D = LocalHook.GetProcAddress("ConsoleApplication1.exe", "?get1_2@MyClass@@QEAAHHH@Z");
                //originGet_C_D_Del = (getC_D)Marshal.GetDelegateForFunctionPointer(originGetC_D, typeof(getC_D));

                //TestHook = LocalHook.Create(
                //    LocalHook.GetProcAddress("ConsoleApplication1.exe", "?get1_2@MyClass@@QEAAHHH@Z"),
                //    new getC_D(getC_D_Del),
                //    this
                //    );
                //TestHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });

                IntPtr originCompilePtr = GetCompileMethodPtr();
                if (originCompilePtr == null)
                {
                    RemoteHooking.WakeUpProcess();
                    return;
                }

                //CompileMethodDel testCompile = (CompileMethodDel)Marshal.GetDelegateForFunctionPointer(originCompilePtr, typeof(CompileMethodDel));
                mCompileMethodDel = new CompileMethodDel(compileMethodDel);
                Interface.Info("origiCompilePtr is " + originCompilePtr.ToString());

                RuntimeHelpers.PrepareDelegate(mCompileMethodDel);
                RuntimeHelpers.PrepareDelegate(originCompile);

                CompileMethodHook = LocalHook.Create(
                    originCompilePtr,
                    mCompileMethodDel,
                    this);
                CompileMethodHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });

                //GetJitHook = LocalHook.Create(
                //    LocalHook.GetProcAddress("Clrjit.dll", "getJit"),
                //    new DGetJit(getJit_Hooked),
                //    this);
                //GetJitHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });
                IntPtr test = GetJit();
                Interface.Info("Jit hook finish");
            }
            catch (Exception ExtInfo)
            {
                Interface.ReportException(ExtInfo);
                RemoteHooking.WakeUpProcess();
                return;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Interface.Info("There is " + assemblies.Length + " in dll");
            foreach (Assembly reference in assemblies)
            {
                Interface.Info(reference.GetName() +  " asm is loaed");
            }

            IntPtr intPtr = GetJit();
            Interface.Info(" get jit result is " + intPtr.ToString());

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
        delegate IntPtr DGetJit();

        static int hooked = 0;
        static IntPtr originCompilePtrBefore = IntPtr.Zero;
        public unsafe IntPtr getJit_Hooked()
        {

            IntPtr pJit;
            try
            {

                Main This = (Main)HookRuntimeInfo.Callback;

                //lock (This.Queue)
                //{
                //    This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                //        RemoteHooking.GetCurrentThreadId() + "]: \"" + "############ Hello getJit called ###############" + "\"");
                //}

                pJit = GetJit();
                //lock (This.Queue)
                //{
                //    This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                //        RemoteHooking.GetCurrentThreadId() + "]: \"" + "############ Inhook pJit addr is " + pJit.ToString() + " ###############" + "\"");
                //}

                compiler = Marshal.PtrToStructure<CorJitCompilerNative>(Marshal.ReadIntPtr(pJit));
                pVTable = Marshal.ReadIntPtr(pJit);
                //lock (This.Queue)
                //{
                //    This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                //        RemoteHooking.GetCurrentThreadId() + "]: \"" + "############ pVTable addr is " + pVTable.ToString() + " ###############" + "\"");
                //}

                //uint lpflOldProtect;
                //if (!Win32MemoryUtils.VirtualProtect(pVTable, sizeof(UInt64), Win32MemoryUtils.MemoryProtectionConstants.PAGE_READWRITE, out lpflOldProtect))
                //{
                //    goto FuncEnd;
                //}

                if (originCompile == null)
                {
                    originCompile = compiler.CompileMethod;
                    lock (This.Queue)
                    {
                        This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                            RemoteHooking.GetCurrentThreadId() + "]: \"" + "############ originCompile addr is " + originCompilePtr.ToString() + " ###############" + "\"");
                    }
                }

                originCompilePtr = Marshal.GetFunctionPointerForDelegate(originCompile);
                if (originCompilePtrBefore != originCompilePtr)
                {
                    lock (This.Queue)
                    {
                        This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                            RemoteHooking.GetCurrentThreadId() + "]: \"" + "############ originCompile addr is " + originCompilePtr.ToString() + " origin is " + originCompilePtrBefore.ToString() + " ###############" + "\"");
                    }
                    originCompilePtrBefore = originCompilePtr;
                }

                mCompileMethodDel = new CompileMethodDel(compileMethodDel);
                mCompileMethodPtr = Marshal.GetFunctionPointerForDelegate(mCompileMethodDel);
                //lock (This.Queue)
                //{
                //    This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                //        RemoteHooking.GetCurrentThreadId() + "]: \"" + "############ My compile method addr is " + mCompileMethodPtr.ToString() + " ###############" + "\"");
                //}

                //if (hooked == 0)
                //{
                //    lock (This.Queue)
                //    {
                //        This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                //            RemoteHooking.GetCurrentThreadId() + "]: \"" + "############ CompileMethodHook called  " + " ###############" + "\"");
                //    }
                //    hooked = 1;

                //    RuntimeHelpers.PrepareDelegate(mCompileMethodDel);
                //    RuntimeHelpers.PrepareDelegate(compiler.CompileMethod);
                //    CompileMethodHook = LocalHook.Create(
                //        originCompilePtr,
                //        mCompileMethodDel,
                //        this);
                //    CompileMethodHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });
                //}


            }
            catch (Exception error)
            {
                Main This = (Main)HookRuntimeInfo.Callback;

                lock (This.Queue)
                {
                    This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                        RemoteHooking.GetCurrentThreadId() + "]: \"" + error.ToString() + "\"");
                }
                return GetJit();
            }
        //FuncEnd:
            //// call original API...
            return pJit;
        }

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
        static int flag = 0;
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
                    //This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    //    RemoteHooking.GetCurrentThreadId() + "]: \"" + InFileName + "\"");
                }

                if (flag == 0)
                {
                    flag = 1;
                    //IntPtr pJit = GetJit();
                    //    if (pJit != null)
                    //    {
                    //pVTable = Marshal.ReadIntPtr(pJit);
                    //        lock (This.Queue)
                    //        {
                    //            This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    //                RemoteHooking.GetCurrentThreadId() + "]: \"" + "################## get jit success #############" + "\"");
                    //        }

                    //        //uint lpflOldProtect;
                    //        //Win32MemoryUtils.VirtualProtect(pVTable, (uint)IntPtr.Size, Win32MemoryUtils.MemoryProtectionConstants.PAGE_EXECUTE_READWRITE, out lpflOldProtect);
                    //        //defaultCompileMethodPtr = Marshal.ReadIntPtr(pVTable);
                    //        //if (defaultCompileMethodPtr == null)
                    //        //{
                    //        //    goto FuncEnd;
                    //        //}
                    //        //lock (This.Queue)
                    //        //{
                    //        //    This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    //        //        RemoteHooking.GetCurrentThreadId() + "]: \"" + "compileMehod addr is " + defaultCompileMethodPtr.ToString() + "\"");
                    //        //}

                    //        //uint lpflOldProtect;
                    //        //Win32MemoryUtils.VirtualProtect(pVTable, (uint)IntPtr.Size, Win32MemoryUtils.MemoryProtectionConstants.PAGE_EXECUTE_READWRITE, out lpflOldProtect);
                    //        //= Marshal.PtrToStructure<MCorJitCompilerNative>(Marshal.ReadIntPtr(pJit))
                    //        //if (defaultCompileMethod == null)
                    //        //{
                    //        //    goto FuncEnd;
                    //        //}
                    //        //lock (This.Queue)
                    //        //{
                    //        //    This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    //        //        RemoteHooking.GetCurrentThreadId() + "]: \"" + "################## get defaultCompileMethod Name is " + defaultCompileMethod.Method.Name + " #############" + "\"");
                    //        //}
                    //        //if (compiler.CompileMethod == null)
                    //        //{
                    //        //    lock (This.Queue)
                    //        //    {
                    //        //        This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    //        //            RemoteHooking.GetCurrentThreadId() + "]: \"" + "xxxxxxxxxxxxxxxx get compileMethod failed xxxxxxxxxxxxxxxx" + "\"");
                    //        //    }
                    //        //}
                    //        //else
                    //        //{
                    //        //    lock (This.Queue)
                    //        //    {
                    //        //        This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    //        //            RemoteHooking.GetCurrentThreadId() + "]: \"" + "################ get compileMethod success #########" + "\"" + compiler.CompileMethod.GetMethodInfo().Name);
                    //        //    }
                    //        //}
                    //        //}
                    //        //else {
                    //        //    lock (This.Queue)
                    //        //    {
                    //        //        This.Queue.Push("[" + RemoteHooking.GetCurrentProcessId() + ":" +
                    //        //            RemoteHooking.GetCurrentThreadId() + "]: \"" + "xxxxxxxxxxxxxxxx get jit called failed xxxxxxx" + "\"");
                    //        //    }
                    //    }
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
    }
}
