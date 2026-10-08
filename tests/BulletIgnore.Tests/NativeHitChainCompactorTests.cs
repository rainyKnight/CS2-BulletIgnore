using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Xunit;

namespace BulletIgnore.Tests;

public sealed unsafe class NativeHitChainCompactorTests
{
    private const ushort Invalid = 0x7FFF;

    [Fact]
    public void RemovesHumanAndRepairsFollowingStartWithoutChangingEnemyRecordData()
    {
        var enemy = Hit(0, 1, 0x42);
        using var f = new Fixture([100, 200], [Hit(Invalid, 0, 0x21), enemy]);
        Assert.True(f.Compact(h => h == 100, out var removed));
        Assert.Equal(1, removed);
        Assert.Equal(1, f.Count);
        BinaryPrimitives.WriteUInt16LittleEndian(enemy.AsSpan(0x10), 1);
        Assert.Equal(enemy, f.Record(0));
        Assert.Equal(2, f.Capacity);
        f.AssertGuards();
    }

    [Fact]
    public void PreservesWallAndEnemyOrderAndUnrelatedPayloadBytes()
    {
        var wall = Hit(Invalid, 1, 0x31);
        var enemy = Hit(0, 2, 0x67);
        using var f = new Fixture([100, -1, 200], [wall, Hit(1, 0, 0x22), enemy]);
        Assert.True(f.Compact(h => h == 100, out var removed));
        Assert.Equal(1, removed);
        Assert.Equal(2, f.Count);
        Assert.Equal(wall, f.Record(0));
        BinaryPrimitives.WriteUInt16LittleEndian(enemy.AsSpan(0x10), 2);
        Assert.Equal(enemy, f.Record(1));
        f.AssertGuards();
    }

    [Fact]
    public void ConsecutiveHumansAreAllRemoved()
    {
        using var f = new Fixture([100, 101, 200], [Hit(Invalid, 0), Hit(0, 1), Hit(1, 2)]);
        Assert.True(f.Compact(h => h is 100 or 101, out var removed));
        Assert.Equal(2, removed);
        Assert.Equal(1, f.Count);
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(f.Record(0).AsSpan(0x10)));
        f.AssertGuards();
    }

    [Fact]
    public void AllIgnoredHitsResultInAnEmptyChain()
    {
        using var f = new Fixture([100], [Hit(Invalid, 0), Hit(0, 0)]);
        Assert.True(f.Compact(_ => true, out var removed));
        Assert.Equal(2, removed);
        Assert.Equal(0, f.Count);
        Assert.Equal(2, f.Capacity);
        f.AssertGuards();
    }

    [Fact]
    public void NoHumansLeavesEveryRecordUnchanged()
    {
        var records = new[] { Hit(Invalid, 0, 1), Hit(0, 1, 2), Hit(1, 2, 3) };
        using var f = new Fixture([200, 201, 202], records);
        Assert.True(f.Compact(_ => false, out var removed));
        Assert.Equal(0, removed);
        Assert.Equal(3, f.Count);
        for (var i = 0; i < records.Length; i++) Assert.Equal(records[i], f.Record(i));
        f.AssertGuards();
    }

    [Fact]
    public void RepairsHumanStartEvenWhenNoEndHitIsRemoved()
    {
        using var f = new Fixture([100, 200], [Hit(0, 1)]);
        Assert.True(f.Compact(h => h == 100, out var removed));
        Assert.Equal(0, removed);
        Assert.Equal(1, f.Count);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(f.Record(0).AsSpan(0x10)));
        f.AssertGuards();
    }

    [Theory]
    [InlineData(0x7FFF)]
    [InlineData(0xFFFF)]
    [InlineData(9)]
    [InlineData(0x8009)]
    public void InvalidOrOutOfRangeSurfaceIndexIsNotResolved(int index)
    {
        var record = Hit((ushort)index, (ushort)index);
        using var f = new Fixture([100], [record]);
        var calls = 0;
        Assert.True(f.Compact(_ => { calls++; return true; }, out var removed));
        Assert.Equal(0, calls);
        Assert.Equal(0, removed);
        Assert.Equal(record, f.Record(0));
        f.AssertGuards();
    }

    [Fact]
    public void HighBitOfEncodedIndexIsMaskedForLookup()
    {
        using var f = new Fixture([100], [Hit(Invalid, 0x8000)]);
        Assert.True(f.Compact(h => h == 100, out var removed));
        Assert.Equal(1, removed);
        Assert.Equal(0, f.Count);
        f.AssertGuards();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-2)]
    public void InvalidEntityHandlesAreNotResolved(int handle)
    {
        using var f = new Fixture([handle], [Hit(0, 0)]);
        var calls = 0;
        Assert.True(f.Compact(_ => { calls++; return true; }, out var removed));
        Assert.Equal(0, removed);
        Assert.Equal(0, calls);
        f.AssertGuards();
    }

    [Fact]
    public void ResolverFailureLeavesThatHitNativeAndStillCommitsPreviousRemovals()
    {
        using var f = new Fixture([100, 200, 300], [Hit(Invalid, 0), Hit(1, 1), Hit(2, 2)]);
        Assert.True(f.Compact(h => h == 200 ? throw new InvalidOperationException() : h == 100, out var removed));
        Assert.Equal(1, removed);
        Assert.Equal(2, f.Count);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(f.Record(0).AsSpan(0x12)));
        f.AssertGuards();
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(-1, 1, 1)]
    [InlineData(4097, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1025, 1025)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, -1)]
    [InlineData(1, 1, 1025)]
    public void RejectsInvalidHeaderBeforeAccessingArrays(int surfaces, int hits, int capacity)
    {
        using var f = new Fixture([100], [Hit(0, 0)]);
        *(int*)f.Trace = surfaces;
        *(int*)(f.Trace + 0x1C20) = hits;
        *(int*)(f.Trace + 0x1C30) = capacity;
        var before = f.Record(0);
        var calls = 0;
        Assert.False(f.Compact(_ => { calls++; return true; }, out var removed));
        Assert.Equal(0, removed);
        Assert.Equal(0, calls);
        Assert.Equal(before, f.Record(0));
        Assert.Equal(hits, f.Count);
        f.AssertGuards();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(65535)]
    [InlineData(0x0000800000000000L)]
    public void RejectsObviouslyInvalidTracePointers(long pointer)
    {
        Assert.False(NativeHitChainCompactor.TryCompact((byte*)pointer, _ => true, out var removed));
        Assert.Equal(0, removed);
    }

    [Theory]
    [InlineData(0x08)]
    [InlineData(0x1C28)]
    public void RejectsNullArrayPointers(int offset)
    {
        using var f = new Fixture([100], [Hit(0, 0)]);
        *(nint*)(f.Trace + offset) = 0;
        Assert.False(f.Compact(_ => true, out var removed));
        Assert.Equal(0, removed);
        f.AssertGuards();
    }

    [Fact]
    public void AcceptsMaximumSupportedCountsWithoutWritingPastArrays()
    {
        var handles = Enumerable.Range(0, 4096).ToArray();
        var records = Enumerable.Range(0, 1024).Select(i => Hit(Invalid, (ushort)(i * 4))).ToArray();
        using var f = new Fixture(handles, records);
        Assert.True(f.Compact(h => h % 8 == 0, out var removed));
        Assert.Equal(512, removed);
        Assert.Equal(512, f.Count);
        f.AssertGuards();
    }

    [Fact]
    public void DeterministicRandomChainsMatchManagedReference()
    {
        var random = new Random(57113);
        for (var trial = 0; trial < 250; trial++)
        {
            var handles = Enumerable.Range(100, 12).ToArray();
            var ignored = handles.Where(_ => random.Next(2) == 0).ToHashSet();
            var records = Enumerable.Range(0, random.Next(1, 40))
                .Select(_ => Hit(Index(), Index(), (byte)random.Next(256))).ToArray();
            var expected = new List<byte[]>();
            foreach (var record in records)
            {
                var start = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(0x10));
                var end = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(0x12));
                if (Ignored(end)) continue;
                var copy = record.ToArray();
                if (start != end && Ignored(start)) BinaryPrimitives.WriteUInt16LittleEndian(copy.AsSpan(0x10), end);
                expected.Add(copy);
            }
            using var f = new Fixture(handles, records);
            Assert.True(f.Compact(ignored.Contains, out var removed));
            Assert.Equal(records.Length - expected.Count, removed);
            Assert.Equal(expected.Count, f.Count);
            for (var i = 0; i < expected.Count; i++) Assert.Equal(expected[i], f.Record(i));
            f.AssertGuards();

            ushort Index() => random.Next(7) == 0 ? Invalid : (ushort)(random.Next(15) | (random.Next(2) * 0x8000));
            bool Ignored(ushort encoded) => (encoded & 0x7FFF) < handles.Length && ignored.Contains(handles[encoded & 0x7FFF]);
        }
    }

    private static byte[] Hit(ushort start, ushort end, byte marker = 0x35)
    {
        var bytes = Enumerable.Repeat(marker, 0x18).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x10), start);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x12), end);
        return bytes;
    }

    private sealed class Fixture : IDisposable
    {
        private const int Guard = 16;
        private readonly byte* _trace;
        private readonly byte* _surfaces;
        private readonly byte* _hits;
        private readonly int _surfaceSize;
        private readonly int _hitSize;
        public byte* Trace => _trace + Guard;
        private byte* Hits => _hits + Guard;
        public int Count => *(int*)(Trace + 0x1C20);
        public int Capacity => *(int*)(Trace + 0x1C30);

        public Fixture(int[] handles, byte[][] records)
        {
            // Intentionally literal ABI values in fixtures: an accidental production
            // offset change must fail tests rather than silently move test data too.
            _surfaceSize = Math.Max(1, handles.Length) * 0x38;
            _hitSize = Math.Max(1, records.Length) * 0x18;
            _trace = Allocate(0x1C40);
            _surfaces = Allocate(_surfaceSize);
            _hits = Allocate(_hitSize);
            *(int*)Trace = handles.Length;
            *(byte**)(Trace + 0x08) = _surfaces + Guard;
            *(int*)(Trace + 0x1C20) = records.Length;
            *(byte**)(Trace + 0x1C28) = Hits;
            *(int*)(Trace + 0x1C30) = records.Length;
            for (var i = 0; i < handles.Length; i++) *(int*)(_surfaces + Guard + i * 0x38 + 0x2C) = handles[i];
            for (var i = 0; i < records.Length; i++) records[i].CopyTo(new Span<byte>(Hits + i * 0x18, 0x18));
        }

        public bool Compact(Func<int, bool> predicate, out int removed)
            => NativeHitChainCompactor.TryCompact(Trace, predicate, out removed);
        public byte[] Record(int index) => new ReadOnlySpan<byte>(Hits + index * 0x18, 0x18).ToArray();

        private static byte* Allocate(int size)
        {
            var p = (byte*)NativeMemory.AllocZeroed((nuint)(size + Guard * 2));
            new Span<byte>(p, Guard).Fill(0xAD);
            new Span<byte>(p + Guard + size, Guard).Fill(0xAD);
            return p;
        }
        public void AssertGuards()
        {
            AssertGuard(_trace, 0x1C40);
            AssertGuard(_surfaces, _surfaceSize);
            AssertGuard(_hits, _hitSize);
        }
        private static void AssertGuard(byte* p, int size)
        {
            Assert.All(new ReadOnlySpan<byte>(p, Guard).ToArray(), b => Assert.Equal((byte)0xAD, b));
            Assert.All(new ReadOnlySpan<byte>(p + Guard + size, Guard).ToArray(), b => Assert.Equal((byte)0xAD, b));
        }
        public void Dispose()
        {
            NativeMemory.Free(_trace);
            NativeMemory.Free(_surfaces);
            NativeMemory.Free(_hits);
        }
    }
}
