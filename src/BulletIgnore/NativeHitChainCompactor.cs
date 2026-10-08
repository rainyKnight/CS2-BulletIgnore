namespace BulletIgnore;

/// <summary>
/// In-place compaction of a version-specific, already-built FireBullet hit chain.
/// Bounds checks reject obvious corruption, but do NOT establish memory accessibility
/// or ABI compatibility. Call only at the validated Linux x64 hook location.
/// </summary>
internal static unsafe class NativeHitChainCompactor
{
    internal const int SurfaceCountOffset = 0x00;
    internal const int SurfaceArrayOffset = 0x08;
    internal const int HitCountOffset = 0x1C20;
    internal const int HitArrayOffset = 0x1C28;
    internal const int HitCapacityOffset = 0x1C30;
    internal const int HitElementSize = 0x18;
    internal const int HitStartSurfaceOffset = 0x10;
    internal const int HitEndSurfaceOffset = 0x12;
    internal const int SurfaceElementSize = 0x38;
    internal const int SurfaceEntityHandleOffset = 0x2C;
    internal const int InvalidSurfaceIndex = 0x7FFF;
    internal const int MaximumPlausibleHitCount = 1024;
    internal const int MaximumPlausibleSurfaceCount = 4096;

    // True means the header passed plausibility checks, not that a hit was removed.
    // A retained hit's start surface can be repaired even when removed == 0.
    internal static bool TryCompact(byte* trace, Func<int, bool> shouldIgnoreEntity, out int removed)
    {
        removed = 0;
        if (!IsPlausiblePointer(trace))
        {
            return false;
        }

        var surfaceCount = *(int*)(trace + SurfaceCountOffset);
        var hitCount = *(int*)(trace + HitCountOffset);
        var hitCapacity = *(int*)(trace + HitCapacityOffset);
        if (surfaceCount <= 0 || surfaceCount > MaximumPlausibleSurfaceCount
            || hitCount <= 0 || hitCount > MaximumPlausibleHitCount
            || hitCapacity < hitCount || hitCapacity > MaximumPlausibleHitCount)
        {
            return false;
        }

        var surfaces = *(byte**)(trace + SurfaceArrayOffset);
        var hits = *(byte**)(trace + HitArrayOffset);
        if (!IsPlausiblePointer(surfaces) || !IsPlausiblePointer(hits))
        {
            return false;
        }

        var writeIndex = 0;
        for (var readIndex = 0; readIndex < hitCount; readIndex++)
        {
            var hit = hits + readIndex * HitElementSize;
            var endSurface = *(ushort*)(hit + HitEndSurfaceOffset);
            if (IsIgnoredSurface(surfaces, surfaceCount, endSurface, shouldIgnoreEntity))
            {
                removed++;
                continue;
            }

            var startSurface = *(ushort*)(hit + HitStartSurfaceOffset);
            if (startSurface != endSurface
                && IsIgnoredSurface(surfaces, surfaceCount, startSurface, shouldIgnoreEntity))
            {
                // Do not consume the ignored human's flesh material as a segment start.
                *(ushort*)(hit + HitStartSurfaceOffset) = endSurface;
            }

            if (writeIndex != readIndex)
            {
                var destination = hits + writeIndex * HitElementSize;
                *(ulong*)destination = *(ulong*)hit;
                *(ulong*)(destination + sizeof(ulong)) = *(ulong*)(hit + sizeof(ulong));
                *(ulong*)(destination + 2 * sizeof(ulong)) = *(ulong*)(hit + 2 * sizeof(ulong));
            }
            writeIndex++;
        }

        if (removed > 0)
        {
            *(int*)(trace + HitCountOffset) = writeIndex;
        }
        return true;
    }

    private static bool IsIgnoredSurface(
        byte* surfaces, int count, ushort encodedIndex, Func<int, bool> shouldIgnoreEntity)
    {
        var index = encodedIndex & InvalidSurfaceIndex;
        if (index == InvalidSurfaceIndex || index >= count)
        {
            return false;
        }

        var packedHandle = *(int*)(surfaces + index * SurfaceElementSize + SurfaceEntityHandleOffset);
        if (packedHandle is -1 or -2)
        {
            return false;
        }

        // Resolve failures must not terminate a partially compacted chain while its
        // original count is still exposed. Treat that single surface as native.
        try
        {
            return shouldIgnoreEntity(packedHandle);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPlausiblePointer(void* pointer)
    {
        var value = (ulong)pointer;
        return value >= 0x10000 && value <= 0x00007FFFFFFFFFFF;
    }
}
