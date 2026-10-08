// SPDX-License-Identifier: AGPL-3.0-only
// See docs/PROVENANCE.md and LICENSES for code origins and retained notices.
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Hooks;
using Sharp.Shared.Managers;
using Sharp.Shared.Types;

[assembly: DisableRuntimeMarshalling]

namespace BulletIgnore;

/// <summary>
/// Removes CT player pawns from CS2's native bullet hit chain before the
/// penetration filter consumes damage and penetration power.
/// </summary>
internal sealed class BulletPassThroughRuntime : IDisposable
{
    // CS2 Linux 1.41.7.8: common continuation immediately after BuildOutput
    // in the FireBullet-only hit-filter helper. At this point r15 is the trace
    // structure whose surface and output arrays are described below.
    private static readonly byte[] LinuxPostBuildOutputSignature =
    [
        0xF3, 0x41, 0x0F, 0x10, 0x9F, 0x08, 0x1D, 0x00, 0x00,
        0x66, 0x0F, 0xEF, 0xE4,
        0xF3, 0x41, 0x0F, 0x10, 0x87, 0x0C, 0x1D, 0x00, 0x00,
        0xF3, 0x0F, 0x59, 0xDB,
        0x49, 0x63, 0x97, 0x20, 0x1C, 0x00, 0x00,
        0xF3, 0x0F, 0x59, 0xC0,
        0x49, 0x8B, 0x87, 0x28, 0x1C, 0x00, 0x00
    ];

    // The byte sequence above stays the single source of truth; derive the
    // IDA-style text the scanner wants so the two cannot drift apart.
    private static readonly string LinuxPostBuildOutputPattern = string.Join(
        ' ',
        LinuxPostBuildOutputSignature.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture)));

    private static readonly object s_hookGate = new();
    private static BulletPassThroughRuntime? s_active;

    private readonly ISharedSystem _shared;
    private readonly IEntityManager _entities;
    private readonly Func<bool> _isModeActive;
    private readonly ILogger _logger;
    private readonly Func<int, bool> _shouldIgnoreEntity;
    private IMidFuncHook? _hook;
    private bool _active;
    private long _callbacks;
    private long _compactedChains;
    private long _removedHits;
    private long _resolvedPlayerSurfaces;
    private long _rejectedStructures;
    private long _errors;

    public BulletPassThroughRuntime(
        ISharedSystem shared,
        IEntityManager entities,
        Func<bool> isModeActive,
        ILogger logger)
    {
        _shared = shared;
        _entities = entities;
        _isModeActive = isModeActive;
        _logger = logger;
        _shouldIgnoreEntity = ShouldIgnoreEntity;
    }

    public bool Activate()
    {
        if (_active)
        {
            return true;
        }

        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            _logger.LogWarning("Human bullet pass-through is currently supported only on Linux x64.");
            return false;
        }

        try
        {
            var target = FindUniqueGameServerPattern();
            if (target == 0)
            {
                _logger.LogError(
                    "Human bullet pass-through signature did not match this CS2 server build; leaving native bullet behavior unchanged.");
                return false;
            }

            lock (s_hookGate)
            {
                if (Volatile.Read(ref s_active) is not null)
                {
                    _logger.LogError("Another human bullet pass-through hook is already active.");
                    return false;
                }

                unsafe
                {
                    _hook = _shared.GetHookManager().CreateMidFuncHook();
                    _hook.Prepare(
                        target,
                        (nint)(delegate* unmanaged<MidHookContext*, void>)&OnPostBuildOutput);
                    Volatile.Write(ref s_active, this);
                    _active = true;
                    if (!_hook.Install())
                    {
                        _active = false;
                        Volatile.Write(ref s_active, null);
                        ReleaseUninstalledHook();
                        _logger.LogError(
                            "Failed to install the human bullet pass-through MidHook at 0x{Address:X}.",
                            target);
                        return false;
                    }
                }
            }

            _logger.LogInformation(
                "Human bullet pass-through enabled at server address 0x{Address:X}; CT player pawn hits will be removed from FireBullet chains.",
                target);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to enable human bullet pass-through.");
            lock (s_hookGate)
            {
                _active = false;
                if (ReferenceEquals(Volatile.Read(ref s_active), this))
                {
                    Volatile.Write(ref s_active, null);
                }

                ReleaseUninstalledHook();
            }
            return false;
        }
    }

    public void Dispose()
    {
        var retainedForSafety = false;
        lock (s_hookGate)
        {
            _active = false;
            var hook = _hook;
            if (hook is not null)
            {
                try
                {
                    hook.Uninstall();
                    _hook = null;
                    hook.Dispose();
                }
                catch (Exception exception)
                {
                    retainedForSafety = true;
                    _logger.LogCritical(
                        exception,
                        "Human bullet pass-through hook could not be uninstalled; retaining its inactive runtime until process restart.");
                }
            }

            if (!retainedForSafety && ReferenceEquals(Volatile.Read(ref s_active), this))
            {
                Volatile.Write(ref s_active, null);
            }
        }

        if (retainedForSafety)
        {
            return;
        }

        _logger.LogInformation(
            "Human bullet pass-through disabled. Callbacks {Callbacks}, resolved player surfaces {ResolvedPlayerSurfaces}, compacted chains {CompactedChains}, removed hits {RemovedHits}, rejected structures {RejectedStructures}, errors {Errors}.",
            Interlocked.Read(ref _callbacks),
            Interlocked.Read(ref _resolvedPlayerSurfaces),
            Interlocked.Read(ref _compactedChains),
            Interlocked.Read(ref _removedHits),
            Interlocked.Read(ref _rejectedStructures),
            Interlocked.Read(ref _errors));
    }

    private void ReleaseUninstalledHook()
    {
        var hook = Interlocked.Exchange(ref _hook, null);
        if (hook is null)
        {
            return;
        }

        try
        {
            hook.Dispose();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to dispose an uninstalled human bullet pass-through hook.");
        }
    }

    private nint FindUniqueGameServerPattern()
    {
        // FindPatternExactly reports 0 both when the signature is absent and
        // when it matches more than once, which is the safety property this
        // hook needs before it rewrites a mid-function location.
        var target = _shared.GetLibraryModuleManager().Server.FindPatternExactly(LinuxPostBuildOutputPattern);
        if (target == 0)
        {
            _logger.LogError(
                "Human bullet pass-through signature did not match the CS2 server module exactly once; refusing to hook.");
            return 0;
        }

        return target;
    }

    [UnmanagedCallersOnly]
    private static unsafe void OnPostBuildOutput(MidHookContext* context)
    {
        var runtime = Volatile.Read(ref s_active);
        if (runtime is null || !runtime._active || context is null)
        {
            return;
        }

        try
        {
            if (!runtime._isModeActive())
            {
                return;
            }

            Interlocked.Increment(ref runtime._callbacks);
            var removed = runtime.CompactHumanHits((byte*)context->r15);
            if (removed > 0)
            {
                Interlocked.Increment(ref runtime._compactedChains);
                Interlocked.Add(ref runtime._removedHits, removed);
                if (Interlocked.Read(ref runtime._compactedChains) == 1)
                {
                    runtime._logger.LogInformation(
                        "Human bullet pass-through removed its first CT player hit before penetration and damage processing.");
                }
            }
        }
        catch
        {
            // Exceptions must never escape an UnmanagedCallersOnly callback.
            // Pointer plausibility/bounds checks do NOT prove mapped memory or
            // ABI compatibility. Native access faults are not safely caught here.
            // Managed lookup failures leave the corresponding hit unchanged.
            Interlocked.Increment(ref runtime._errors);
        }
    }

    private unsafe int CompactHumanHits(byte* trace)
    {
        if (!NativeHitChainCompactor.TryCompact(trace, _shouldIgnoreEntity, out var removed))
        {
            Interlocked.Increment(ref _rejectedStructures);
        }
        return removed;
    }

    private bool ShouldIgnoreEntity(int packedHandle)
    {
        try
        {
            // Native bullet surfaces store a generic entity handle. Resolving
            // it as IBasePlayerPawn makes ModSharp reject otherwise-valid
            // player surfaces, so no element ever reaches the compaction path.
            // Resolve the native handle first, then identify player pawns by
            // class and team before the penetration filter and downstream
            // damage loop consume the array.
            var handle = CEntityHandle<IBaseEntity>.FromPackedValue(unchecked((uint)packedHandle));
            if (_entities.FindEntityByHandle(handle) is not { IsValidEntity: true } entity
                || !HumanSurfacePolicy.IsPlayerPawnClass(entity.Classname))
            {
                return false;
            }

            Interlocked.Increment(ref _resolvedPlayerSurfaces);
            return HumanSurfacePolicy.IsHumanTeam(entity.Team);
        }
        catch
        {
            Interlocked.Increment(ref _errors);
            return false;
        }
    }

}
