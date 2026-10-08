using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Launcher.Infrastructure;

/// <summary>
/// Aucun processus orphelin (INTEGRATION_CONTRACT.md §5.2) :
/// - Windows : Job Object avec JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE ;
/// - Linux : groupe de processus Unix, chaque fils placé dans le groupe du Launcher.
/// Un composant lancé sans Launcher ignore ce mécanisme : le confinement ne crée aucune dépendance.
/// </summary>
public static class ProcessTreeKiller
{
    /// <summary>
    /// Confinement à la création, côté parent (best effort) : le fils reçoit sa propre unité —
    /// son groupe de processus Unix, ou le Job Object Windows du Launcher
    /// (JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE) — pour que rien ne survive à la fermeture du
    /// Launcher et que l'arrêt forcé d'un composant ne touche jamais aux autres.
    /// </summary>
    public static void MakeOwnProcessGroup(int childPid)
    {
        if (OperatingSystem.IsWindows())
        {
            AssignToJob(childPid);
            return;
        }

        try
        {
            setpgid(childPid, childPid);
        }
        catch (DllNotFoundException)
        {
        }
    }

    /// <summary>Windows : ajoute le fils au Job Object du Launcher (best effort).</summary>
    [SupportedOSPlatform("windows")]
    private static void AssignToJob(int childPid)
    {
        try
        {
            using var child = Process.GetProcessById(childPid);
            JobObjectAssigner.TryAssign(child);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Processus déjà terminé ou accès refusé : le confinement reste best effort,
            // l'arrêt forcé conserve son repli sur l'arbre de processus.
        }
    }

    /// <summary>Tue l'arbre d'un processus démarré par ce Launcher.</summary>
    public static void KillTree(Process process)
    {
        if (OperatingSystem.IsWindows())
        {
            KillTreeWindows(process);
        }
        else
        {
            KillTreeUnix(process);
        }
    }

    /// <summary>Tue un processus par PID, sans handle gardé.</summary>
    public static void KillByPid(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Déjà terminé : condition normale après une fin spontanée.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void KillTreeWindows(Process process)
    {
        // L'arrêt forcé vise l'arbre du SEUL composant. Le Job Object commun (assigné au
        // démarrage) est le garde-fou « aucun orphelin à la fermeture du Launcher » : il ne
        // doit être ni terminé ni fermé ici — terminer le job tuerait aussi les autres composants
        // encore vivants (le job les contient tous) et fermer son handle retirerait le
        // garde-fou pour toute la fin de la session.
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Processus terminé entre-temps : condition normale, aucun orphelin à tuer.
        }
    }

    private static void KillTreeUnix(Process process)
    {
        // Le fils a son propre groupe (MakeOwnProcessGroup au démarrage) : le signal vise
        // ce groupe uniquement, jamais le groupe du Launcher lui-même.
        try
        {
            var childPgid = getpgid(process.Id);
            var ownPgid = getpgid(0);
            if (childPgid > 0 && childPgid != ownPgid)
            {
                kill(-childPgid, SIGKILL);
                return;
            }
        }
        catch (DllNotFoundException)
        {
        }

        process.Kill(entireProcessTree: true);
    }

    private const int SIGKILL = 9;

    [DllImport("libc", SetLastError = true)]
    private static extern int setpgid(int pid, int pgid);

    [DllImport("libc", SetLastError = true)]
    private static extern int getpgid(int pid);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signal);
}

/// <summary>
/// Job Object Windows : assigne le fils à un job du Launcher avec terminaison à la fermeture,
/// puis permet de terminer le job pour un arrêt forcé propre (spike S3, ROADMAP.md).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class JobObjectAssigner
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpInfo, int cbInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private static readonly Lazy<IntPtr> JobHandle = new(CreateJob, LazyThreadSafetyMode.ExecutionAndPublication);

    private static IntPtr CreateJob()
    {
        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, ref info, Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
        {
            CloseHandle(handle);
            return IntPtr.Zero;
        }

        return handle;
    }

    /// <summary>Assigne un processus au job du Launcher. Faux si l'assignation est impossible.</summary>
    public static bool TryAssign(Process process)
    {
        var handle = JobHandle.Value;
        return handle != IntPtr.Zero && AssignProcessToJobObject(handle, process.Handle);
    }
}
