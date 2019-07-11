#if ENABLE_BURST_AOT
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Burst.LowLevel;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEditor.Scripting;
using UnityEditor.Scripting.Compilers;
using UnityEditor.Utils;
using UnityEngine;
using CompilerMessageType = UnityEditor.Scripting.Compilers.CompilerMessageType;
using Debug = UnityEngine.Debug;

namespace Unity.Burst.Editor
{
    using static BurstCompilerOptions;

    internal class StaticPostProcessor
    {
        private const string TempSourceLibrary = @"Temp/StagingArea/StaticLibraries";
        [PostProcessBuildAttribute(1)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            // We only support AOT compilation for ios from a macos host (we require xcrun and the apple tool chains)
            //for other hosts, we simply act as if burst is not being used (an error will be generated by the build aot step)
            //this keeps the behaviour consistent with how it was before static linkage was introduced
#if UNITY_EDITOR_OSX
            if (target == BuildTarget.iOS)
            {
                PostAddStaticLibraries(path);
            }
#endif
        }

        private static void PostAddStaticLibraries(string path)
        {
            var assm = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly =>
                assembly.GetName().Name == "UnityEditor.iOS.Extensions.Xcode");
            Type PBXType = assm?.GetType("UnityEditor.iOS.Xcode.PBXProject");
            Type PBXSourceTree = assm?.GetType("UnityEditor.iOS.Xcode.PBXSourceTree");
            if (PBXType != null && PBXSourceTree!=null)
            {
                var project = Activator.CreateInstance(PBXType, null);

                var _sGetPBXProjectPath = PBXType.GetMethod("GetPBXProjectPath");
                var _ReadFromFile = PBXType.GetMethod("ReadFromFile");
                var _sGetUnityTargetName = PBXType.GetMethod("GetUnityTargetName");
                var _TargetGuidByName = PBXType.GetMethod("TargetGuidByName");
                var _AddFileToBuild = PBXType.GetMethod("AddFileToBuild");
                var _AddFile = PBXType.GetMethod("AddFile");
                var _WriteToString = PBXType.GetMethod("WriteToString");

                var sourcetree = new EnumConverter(PBXSourceTree).ConvertFromString("Source");

                string sPath = (string)_sGetPBXProjectPath?.Invoke(null, new object[] {path});
                _ReadFromFile?.Invoke(project, new object[] {sPath});

                string tn = (string) _sGetUnityTargetName?.Invoke(null, null);
                string g = (string) _TargetGuidByName?.Invoke(project, new object[] {tn});

                string srcPath = TempSourceLibrary;
                string dstPath = "Libraries";
                string dstCopyPath = Path.Combine(path, dstPath);

                string burstCppLinkFile = "lib_burst_generated.cpp";
                string libName = DefaultLibraryName + "32.a";
                if (File.Exists(Path.Combine(srcPath, libName)))
                {
                    File.Copy(Path.Combine(srcPath, libName), Path.Combine(dstCopyPath, libName));
                    string fg = (string) _AddFile?.Invoke(project,
                        new object[] {Path.Combine(dstPath, libName), Path.Combine(dstPath, libName), sourcetree});
                    _AddFileToBuild?.Invoke(project, new object[] {g, fg});
                }

                libName = DefaultLibraryName + "64.a";
                if (File.Exists(Path.Combine(srcPath, libName)))
                {
                    File.Copy(Path.Combine(srcPath, libName), Path.Combine(dstCopyPath, libName));
                    string fg = (string) _AddFile?.Invoke(project,
                        new object[] {Path.Combine(dstPath, libName), Path.Combine(dstPath, libName), sourcetree});
                    _AddFileToBuild?.Invoke(project, new object[] {g, fg});
                }

                // Additionally we need a small cpp file (weak symbols won't unfortunately override directly from the libs
                //presumably due to link order?
                string cppPath = Path.Combine(dstCopyPath, burstCppLinkFile);
                File.WriteAllText(cppPath, @"
extern ""C""
{
    void Staticburst_initialize(void* );
    void* StaticBurstStaticMethodLookup(void* );

    int burst_enable_static_linkage = 1;
    void burst_initialize(void* i) { Staticburst_initialize(i); }
    void* BurstStaticMethodLookup(void* i) { return StaticBurstStaticMethodLookup(i); }
}
");
                cppPath = Path.Combine(dstPath, burstCppLinkFile);
                string fileg = (string) _AddFile?.Invoke(project, new object[] {cppPath,cppPath,sourcetree});
                _AddFileToBuild?.Invoke(project, new object[] {g, fileg});

                string pstring = (string) _WriteToString?.Invoke(project, null);
                File.WriteAllText(sPath, pstring);
            }
        }
    }
    
    internal class BurstAotCompiler : IPostBuildPlayerScriptDLLs
    {
        private const string BurstAotCompilerExecutable = "bcl.exe";
        private const string TempStaging = @"Temp/StagingArea/";
        private const string TempStagingManaged = TempStaging + @"Data/Managed/";
        private const string TempStagingPlugins = @"Data/Plugins/";

        int IOrderedCallback.callbackOrder => 0;

        public void OnPostBuildPlayerScriptDLLs(BuildReport report)
        {
            var step = report.BeginBuildStep("burst");
            try
            {
                OnPostBuildPlayerScriptDLLsImpl(report);
            }
            finally
            {
                report.EndBuildStep(step);
            }
        }

        private void OnPostBuildPlayerScriptDLLsImpl(BuildReport report)
        {
            BurstPlatformAotSettings aotSettingsForTarget = BurstPlatformAotSettings.GetOrCreateSettings(report.summary.platform);

            // Early exit if burst is not activated or the platform is not supported
            if (aotSettingsForTarget.DisableBurstCompilation || !IsSupportedPlatform(report.summary.platform))
            {
                return;
            }

            // Collect all method signatures
            var methodsToCompile = BurstReflection.FindExecuteMethods(AssembliesType.Player);

            if (methodsToCompile.Count == 0)
            {
                return; // Nothing to do
            }

            // Prepare options

            // We are grouping methods per their compiler options (float precision...etc)
            var methodGroups = new Dictionary<string, List<string>>();
            for (var i = 0; i < methodsToCompile.Count; i++)
            {
                var burstCompileTarget = methodsToCompile[i];
                if (!burstCompileTarget.IsSupported)
                {
                    continue;
                }

                var methodStr = BurstCompilerService.GetMethodSignature(burstCompileTarget.Method);
                var methodFullSignature = methodStr + "--" + Hash128.Compute(methodStr);

                if (aotSettingsForTarget.DisableOptimisations)
                    burstCompileTarget.Options.DisableOptimizations = true;

                burstCompileTarget.Options.EnableBurstSafetyChecks = !aotSettingsForTarget.DisableSafetyChecks;

                string optionsAsStr;
                if (burstCompileTarget.TryGetOptionsAsString(false, out optionsAsStr))
                {
                    List<string> methodOptions;
                    if (!methodGroups.TryGetValue(optionsAsStr, out methodOptions))
                    {
                        methodOptions = new List<string>();
                        methodGroups.Add(optionsAsStr, methodOptions);
                    }
                    methodOptions.Add(GetOption(OptionAotMethod, methodFullSignature));
                }
            }

            var methodGroupOptions = new List<string>();

            // We should have something like this in the end:
            //
            // --group                1st group of method with the following shared options
            // --float-mode=xxx
            // --method=...
            // --method=...
            //
            // --group                2nd group of methods with the different shared options
            // --float-mode=yyy
            // --method=...
            // --method=...
            if (methodGroups.Count == 1)
            {
                var methodGroup = methodGroups.FirstOrDefault();
                // No need to create a group if we don't have multiple
                methodGroupOptions.Add(methodGroup.Key);
                foreach (var methodOption in methodGroup.Value)
                {
                    methodGroupOptions.Add(methodOption);
                }
            }
            else
            {
                foreach (var methodGroup in methodGroups)
                {
                    methodGroupOptions.Add(GetOption(OptionGroup));
                    methodGroupOptions.Add(methodGroup.Key);
                    foreach (var methodOption in methodGroup.Value)
                    {
                        methodGroupOptions.Add(methodOption);
                    }
                }
            }

            var commonOptions = new List<string>();
            var targetCpu = TargetCpu.Auto;
            var targetPlatform = GetTargetPlatformAndDefaultCpu(report.summary.platform, out targetCpu);
            commonOptions.Add(GetOption(OptionPlatform, targetPlatform));

            // TODO: Add support for configuring the optimizations/CPU
            // TODO: Add support for per method options

            var stagingFolder = Path.GetFullPath(TempStagingManaged);
            //Debug.Log($"Burst CompileAot - To Folder {stagingFolder}");

            // Prepare assembly folder list
            var assemblyFolders = new List<string>();
            assemblyFolders.Add(stagingFolder);

            var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            foreach (var assembly in playerAssemblies)
            {
                foreach (var assemblyRef in assembly.compiledAssemblyReferences)
                {
                    // Exclude folders with assemblies already compiled in the `folder`
                    var assemblyName = Path.GetFileName(assemblyRef);
                    if (assemblyName != null && File.Exists(Path.Combine(stagingFolder, assemblyName)))
                    {
                        continue;
                    }

                    var directory = Path.GetDirectoryName(assemblyRef);
                    if (directory != null)
                    {
                        var fullPath = Path.GetFullPath(directory);
                        if (IsMonoReferenceAssemblyDirectory(fullPath) || IsDotNetStandardAssemblyDirectory(fullPath))
                        {
                            // Don't pass reference assemblies to burst because they contain methods without implementation
                            // If burst accidentally resolves them, it will emit calls to burst_abort.
                            fullPath = Path.Combine(EditorApplication.applicationContentsPath, "MonoBleedingEdge/lib/mono/unityaot");
                            fullPath = Path.GetFullPath(fullPath); // GetFullPath will normalize path separators to OS native format
                            if (!assemblyFolders.Contains(fullPath))
                                assemblyFolders.Add(fullPath);

                            fullPath = Path.Combine(fullPath, "Facades");
                            if (!assemblyFolders.Contains(fullPath))
                                assemblyFolders.Add(fullPath);
                        }
                        else if (!assemblyFolders.Contains(fullPath))
                        {
                            assemblyFolders.Add(fullPath);
                        }
                    }
                }
            }

            commonOptions.AddRange(assemblyFolders.Select(folder => GetOption(OptionAotAssemblyFolder, folder)));

            var combinations = new List<BurstOutputCombination>();

            if (targetPlatform == TargetPlatform.macOS)
            {
                // NOTE: OSX has a special folder for the plugin
                // Declared in GetStagingAreaPluginsFolder
                // PlatformDependent\OSXPlayer\Extensions\Managed\OSXDesktopStandalonePostProcessor.cs
                combinations.Add(new BurstOutputCombination("UnityPlayer.app/Contents/Plugins", targetCpu));
            }
            else if (targetPlatform == TargetPlatform.iOS)
            {
                if (Application.platform != RuntimePlatform.OSXEditor)
                {
                    Debug.LogWarning("Burst Cross Compilation to iOS for standalone player, is only supported on OSX Editor at this time, burst is disabled for this build.");
                }
                else
                {
                    var targetArchitecture = (IOSArchitecture)UnityEditor.PlayerSettings.GetArchitecture(report.summary.platformGroup);
                    if (targetArchitecture == IOSArchitecture.ARMv7 || targetArchitecture == IOSArchitecture.Universal)
                    {
                        // PlatformDependent\iPhonePlayer\Extensions\Common\BuildPostProcessor.cs
                        combinations.Add(new BurstOutputCombination("StaticLibraries", TargetCpu.ARMV7A_NEON32, DefaultLibraryName + "32"));
                    }
                    if (targetArchitecture == IOSArchitecture.ARM64 || targetArchitecture == IOSArchitecture.Universal)
                    {
                        // PlatformDependent\iPhonePlayer\Extensions\Common\BuildPostProcessor.cs
                        combinations.Add(new BurstOutputCombination("StaticLibraries", TargetCpu.ARMV8A_AARCH64, DefaultLibraryName + "64"));
                    }
                }
            }
            else if (targetPlatform == TargetPlatform.Android)
            {
                //TODO: would be better to query AndroidNdkRoot (but thats not exposed from unity)
                string ndkRoot = null;

                // 2019.1 now has an embedded ndk
#if UNITY_2019_1_OR_NEWER
                if (EditorPrefs.HasKey("NdkUseEmbedded"))
                {
                    if (EditorPrefs.GetBool("NdkUseEmbedded"))
                    {
                        ndkRoot = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "NDK");
                    }
                    else
                    {
                        ndkRoot = EditorPrefs.GetString("AndroidNdkRootR16b");
                    }
                }
#endif

                // If we still don't have a valid root, try the old key
                if (string.IsNullOrEmpty(ndkRoot))
                {
                    ndkRoot = EditorPrefs.GetString("AndroidNdkRoot");
                }
                
                
                // Verify the directory at least exists, if not we fall back to ANDROID_NDK_ROOT current setting
                if (!string.IsNullOrEmpty(ndkRoot) && !Directory.Exists(ndkRoot))
                {
                    ndkRoot = null;
                }

                // Always set the ANDROID_NDK_ROOT (if we got a valid result from above), so BCL knows where to find the Android toolchain and its the one the user expects
                if (!string.IsNullOrEmpty(ndkRoot))
                {
                    Environment.SetEnvironmentVariable("ANDROID_NDK_ROOT", ndkRoot);
                }

                var androidTargetArch = UnityEditor.PlayerSettings.Android.targetArchitectures;
                if ((androidTargetArch & AndroidArchitecture.ARMv7) != 0)
                {
                    combinations.Add(new BurstOutputCombination("libs/armeabi-v7a", TargetCpu.ARMV7A_NEON32));
                }
                if ((androidTargetArch & AndroidArchitecture.ARM64) != 0)
                {
                    combinations.Add(new BurstOutputCombination("libs/arm64-v8a", TargetCpu.ARMV8A_AARCH64));
                }
#if !UNITY_2019_2_OR_NEWER
                if ((androidTargetArch & AndroidArchitecture.X86) != 0)
                {
                    combinations.Add(new BurstOutputCombination("libs/x86", TargetCpu.X86_SSE2));
                }
#endif
            }
            else if (targetPlatform == TargetPlatform.UWP)
            {
                // TODO: Make it configurable for x86 (sse2, sse4)
                combinations.Add(new BurstOutputCombination("Plugins/x64", TargetCpu.X64_SSE4));
                combinations.Add(new BurstOutputCombination("Plugins/x86", TargetCpu.X86_SSE2));
                combinations.Add(new BurstOutputCombination("Plugins/ARM", TargetCpu.THUMB2_NEON32));
                combinations.Add(new BurstOutputCombination("Plugins/ARM64", TargetCpu.ARMV8A_AARCH64));
            }
            else
            {
                combinations.Add(new BurstOutputCombination("Data/Plugins/", targetCpu));
            }

            foreach (var combination in combinations)
            {
                // Gets the output folder
                var stagingOutputFolder = Path.GetFullPath(Path.Combine(TempStaging, combination.OutputPath));
                var outputFilePrefix = Path.Combine(stagingOutputFolder, combination.LibraryName);

                var options = new List<string>(commonOptions);
                options.Add(GetOption(OptionAotOutputPath, outputFilePrefix));
                options.Add(GetOption(OptionTarget, combination.TargetCpu));

                if (targetPlatform == TargetPlatform.iOS)
                {
                    options.Add(GetOption(OptionStaticLinkage));
                }

                // finally add method group options
                options.AddRange(methodGroupOptions);

                var responseFile = Path.GetTempFileName();
                File.WriteAllLines(responseFile, options);

                //Debug.Log("Burst compile with response file: " + responseFile);

                try
                {
                    string generatedDebugInformationInOutput = "";
                    if ((report.summary.options & BuildOptions.Development) != 0)
                    {
                        // Workaround for clang >6 development issue (due to latest being 7.0.0) - IOS & newer PS4 SDKS are affected because its a source level IR compatability issue
                        if ((targetPlatform != TargetPlatform.iOS) && (targetPlatform != TargetPlatform.PS4))
                        {
                            generatedDebugInformationInOutput = GetOption(OptionDebug);
                        }
                    }

                    BclRunner.RunManagedProgram(Path.Combine(BurstLoader.RuntimePath, BurstAotCompilerExecutable),
                        $"{generatedDebugInformationInOutput} @{responseFile}",
                        new BclOutputErrorParser(),
                        report);
                }
                catch (Exception e)
                {
                    // We don't expect any error, but in case we have one, we identify that it is burst and we print the details here
	                Debug.LogError("Unexpected error while running the burst compiler: " + e);
                }
            }
        }

        private static bool IsMonoReferenceAssemblyDirectory(string path)
        {
            var editorDir = Path.GetFullPath(EditorApplication.applicationContentsPath);
            return path.IndexOf(editorDir, StringComparison.OrdinalIgnoreCase) != -1 && path.IndexOf("MonoBleedingEdge", StringComparison.OrdinalIgnoreCase) != -1 && path.IndexOf("-api", StringComparison.OrdinalIgnoreCase) != -1;
        }

        private static bool IsDotNetStandardAssemblyDirectory(string path)
        {
            var editorDir = Path.GetFullPath(EditorApplication.applicationContentsPath);
            return path.IndexOf(editorDir, StringComparison.OrdinalIgnoreCase) != -1 && path.IndexOf("netstandard", StringComparison.OrdinalIgnoreCase) != -1 && path.IndexOf("shims", StringComparison.OrdinalIgnoreCase) != -1;
        }

        public static TargetPlatform GetTargetPlatformAndDefaultCpu(BuildTarget target, out TargetCpu targetCpu)
        {
            var platform = TryGetTargetPlatform(target, out targetCpu);
            if (!platform.HasValue)
            {
                throw new NotSupportedException("The target platform " + target + " is not supported by the burst compiler");
            }
            return platform.Value;
        }

        public static bool IsSupportedPlatform(BuildTarget target)
        {
            TargetCpu cpu;
            return TryGetTargetPlatform(target, out cpu).HasValue;
        }

        public static TargetPlatform? TryGetTargetPlatform(BuildTarget target, out TargetCpu targetCpu)
        {
            // TODO: Add support for multi-CPU switch
            targetCpu = TargetCpu.Auto;
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                    targetCpu = TargetCpu.X86_SSE4;
                    return TargetPlatform.Windows;
                case BuildTarget.StandaloneWindows64:
                    targetCpu = TargetCpu.X64_SSE4;
                    return TargetPlatform.Windows;
                case BuildTarget.StandaloneOSX:
                    targetCpu = TargetCpu.X64_SSE4;
                    return TargetPlatform.macOS;
#if !UNITY_2019_2_OR_NEWER
                //32 bit linux support was deprecated
                case BuildTarget.StandaloneLinux:
                    targetCpu = TargetCpu.X86_SSE4;
                    return TargetPlatform.Linux;
#endif
                case BuildTarget.StandaloneLinux64:
                    targetCpu = TargetCpu.X64_SSE4;
                    return TargetPlatform.Linux;
                case BuildTarget.WSAPlayer:
                    targetCpu = TargetCpu.X64_SSE4;
                    return TargetPlatform.UWP;
                case BuildTarget.XboxOne:
                    targetCpu = TargetCpu.X64_SSE4;
                    return TargetPlatform.XboxOne;
                case BuildTarget.PS4:
                    targetCpu = TargetCpu.X64_SSE4;
                    return TargetPlatform.PS4;
                case BuildTarget.Android:
                    targetCpu = TargetCpu.ARMV7A_NEON32;
                    return TargetPlatform.Android;
                case BuildTarget.iOS:
                    targetCpu = TargetCpu.ARMV7A_NEON32;
                    return TargetPlatform.iOS;
            }
            return null;
        }

        /// <summary>
        /// Not exposed by Unity Editor today.
        /// This is a copy of the Architecture enum from `PlatformDependent\iPhonePlayer\Extensions\Common\BuildPostProcessor.cs` 
        /// </summary>
        private enum IOSArchitecture
        {
            ARMv7,
            ARM64,
            Universal
        }

        /// <summary>
        /// Defines an output path (for the generated code) and the target CPU 
        /// </summary>
        private struct BurstOutputCombination
        {
            public readonly TargetCpu TargetCpu;
            public readonly string OutputPath;
            public readonly string LibraryName;

            public BurstOutputCombination(string outputPath, TargetCpu targetCpu, string libraryName = DefaultLibraryName)
            {
                TargetCpu = targetCpu;
                OutputPath = outputPath;
                LibraryName = libraryName;
            }
        }


        private class BclRunner
        {
            private static readonly Regex MatchVersion = new Regex(@"com.unity.burst@(\d+.*?)[\\/]");

            public static void RunManagedProgram(string exe, string args, CompilerOutputParserBase parser, BuildReport report)
            {
                RunManagedProgram(exe, args, Application.dataPath + "/..", parser, report);
            }

            private static void RunManagedProgram(
              string exe,
              string args,
              string workingDirectory,
              CompilerOutputParserBase parser,
              BuildReport report)
            {
                Program p;
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    ProcessStartInfo si = new ProcessStartInfo()
                    {
                        Arguments = args,
                        CreateNoWindow = true,
                        FileName = exe
                    };
                    p = new Program(si);
                }
                else
                {
                    p = (Program) new ManagedProgram(MonoInstallationFinder.GetMonoInstallation("MonoBleedingEdge"), (string) null, exe, args, false, null);
                }

                RunProgram(p, exe, args, workingDirectory, parser, report);
            }

            private static void RunProgram(
              Program p,
              string exe,
              string args,
              string workingDirectory,
              CompilerOutputParserBase parser,
              BuildReport report)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                using (p)
                {
                    p.GetProcessStartInfo().WorkingDirectory = workingDirectory;
                    p.Start();
                    p.WaitForExit();
                    stopwatch.Stop();

                    Console.WriteLine("{0} exited after {1} ms.", (object)exe, (object)stopwatch.ElapsedMilliseconds);
                    IEnumerable<UnityEditor.Scripting.Compilers.CompilerMessage> compilerMessages = null;
                    string[] errorOutput = p.GetErrorOutput();
                    string[] standardOutput = p.GetStandardOutput();
                    if (parser != null)
                    {
                        compilerMessages = parser.Parse(errorOutput, standardOutput, true, "n/a (burst)");
                    }

                    var errorMessageBuilder = new StringBuilder();
                    if (p.ExitCode != 0)
                    {
                        if (compilerMessages != null)
                        {
                            foreach (UnityEditor.Scripting.Compilers.CompilerMessage compilerMessage in compilerMessages)
                            {
                                Debug.LogPlayerBuildError(compilerMessage.message, compilerMessage.file, compilerMessage.line, compilerMessage.column);
                            }
                        }

                        // We try to output the version in the heading error if we can
                        var matchVersion = MatchVersion.Match(exe);
                        errorMessageBuilder.Append(matchVersion.Success ?
                            "Burst compiler (" + matchVersion.Groups[1].Value + ") failed running" :
                            "Burst compiler failed running");
                        errorMessageBuilder.AppendLine();
                        errorMessageBuilder.AppendLine();
                        // Don't output the path if we are not burst-debugging or the exe exist
                        if (BurstLoader.IsDebugging || !File.Exists(exe))
                        {
                            errorMessageBuilder.Append(exe).Append(" ").Append(args);
                            errorMessageBuilder.AppendLine();
                            errorMessageBuilder.AppendLine();
                        }

                        errorMessageBuilder.AppendLine("stdout:");
                        foreach (string str in standardOutput)
                            errorMessageBuilder.AppendLine(str);
                        errorMessageBuilder.AppendLine("stderr:");
                        foreach (string str in errorOutput)
                            errorMessageBuilder.AppendLine(str);

                        // We don't use Debug.LogError as it displays a stacktrace that we really don't want to pollute our message output
                        //report.AddMessage(LogType.Error, errorMessageBuilder.ToString());
                        Debug.LogPlayerBuildError(errorMessageBuilder.ToString(), "unknown", 0, 0);

                        // We report the error to the build player but it seems that it is not correctly reported.
                        // In Editor\Src\BuildPipeline\BuildPlayer.cpp
                        //    if (report.GetSuccess())
                        //    {
                        //        DisplayProgressNotification("Build Successful", "");
                        //    }
                        //
                        //    report.UnregisterForLogMessages();
                        //    report.EndBuildStep(buildStepToken);
                        //    report.UpdateSummary();
                        //
                        // Actually report.GetSuccess() should go after the last UpdateSummary()
                        //
                        // We should probably have a method report.SetBuildStatus(error)
                        report.AddMessage(LogType.Error, errorMessageBuilder.ToString());
                    }
                    Console.WriteLine(p.GetAllOutput());
                }
            }
        }

        /// <summary>
        /// Internal class used to parse bcl output errors
        /// </summary>
        private class BclOutputErrorParser : CompilerOutputParserBase
        {
            // Format of an error message:
            //
            //C:\work\burst\src\Burst.Compiler.IL.Tests\Program.cs(17,9): error: Loading a managed string literal is not supported by burst
            // at Buggy.FuckBug() (at C:\work\burst\src\Burst.Compiler.IL.Tests\Program.cs:17)
            //
            //
            //                                                                [1]    [2]         [3]        [4]         [5]
            //                                                                path   line        col        type        message
            private static readonly Regex MatchLocation = new Regex(@"^(.*?)\((\d+)\s*,\s*(\d+)\):\s*(\w+)\s*:\s*(.*)");

            // Matches " at "
            private static readonly Regex MatchAt = new Regex(@"^\s+at\s+");

            public override IEnumerable<UnityEditor.Scripting.Compilers.CompilerMessage> Parse(
                string[] errorOutput,
                string[] standardOutput,
                bool compilationHadFailure,
                string assemblyName)
            {
                var messages = new List<UnityEditor.Scripting.Compilers.CompilerMessage>();
                var textBuilder = new StringBuilder();
                for (var i = 0; i < errorOutput.Length; i++)
                {
                    string line = errorOutput[i];

                    var message = new UnityEditor.Scripting.Compilers.CompilerMessage {assemblyName = assemblyName};

                    // If we are able to match a location, we can decode it including the following attached " at " lines
                    textBuilder.Clear();

                    var match = MatchLocation.Match(line);
                    if (match.Success)
                    {
                        var path = match.Groups[1].Value;
                        int.TryParse(match.Groups[2].Value, out message.line);
                        int.TryParse(match.Groups[3].Value, out message.column);
                        if (match.Groups[4].Value == "error")
                        {
                            message.type = CompilerMessageType.Error;
                        }
                        else
                        {
                            message.type = CompilerMessageType.Warning;
                        }
                        message.file = !string.IsNullOrEmpty(path) ? path : "unknown";
                        // Replace '\' with '/' to let the editor open the file
                        message.file = message.file.Replace('\\', '/');

                        // Make path relative to project path path
                        var projectPath = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');
                        if (projectPath != null && message.file.StartsWith(projectPath))
                        {
                            message.file = message.file.Substring(projectPath.EndsWith("/") ? projectPath.Length : projectPath.Length + 1);
                        }

                        // debug
                        // textBuilder.AppendLine("line: " + message.line + " column: " + message.column + " error: " + message.type + " file: " + message.file);
                        textBuilder.Append(match.Groups[5].Value);
                    }
                    else
                    {
                        // Don't output any blank line
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }
                        // Otherwise we output an error, but without source location information
                        // so that at least the user can see it directly in the log errors
                        message.type = CompilerMessageType.Error;
                        message.line = 0;
                        message.column = 0;
                        message.file = "unknown";


                        textBuilder.Append(line);
                    }

                    // Collect attached location call context information ("at ...")
                    // we do it for both case (as if we have an exception in bcl we want to print this in a single line)
                    bool isFirstAt = true;
                    for (int j = i + 1; j < errorOutput.Length; j++)
                    {
                        var nextLine = errorOutput[j];
                        if (MatchAt.Match(nextLine).Success)
                        {
                            i++;
                            if (isFirstAt)
                            {
                                textBuilder.AppendLine();
                                isFirstAt = false;
                            }
                            textBuilder.AppendLine(nextLine);
                        }
                        else
                        {
                            break;
                        }
                    }
                    message.message = textBuilder.ToString();

                    messages.Add(message);
                }
                return messages;
            }

            protected override string GetErrorIdentifier()
            {
                throw new NotImplementedException(); // as we overriding the method Parse()
            }

            protected override Regex GetOutputRegex()
            {
                throw new NotImplementedException(); // as we overriding the method Parse()
            }
        }
    }
}
#endif
