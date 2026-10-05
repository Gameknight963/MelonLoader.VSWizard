using System;

namespace MelonLoader.ProjectGeneration
{
    /// <summary>An immutable Unity version, including its release suffix when parsed.</summary>
    public readonly struct UnityVersion : IComparable<UnityVersion>, IEquatable<UnityVersion>
    {
        private readonly AssetRipper.Primitives.UnityVersion value;

        public UnityVersion(ushort major, ushort minor, ushort patch)
            : this(new AssetRipper.Primitives.UnityVersion(major, minor, patch)) { }

        private UnityVersion(AssetRipper.Primitives.UnityVersion value) => this.value = value;

        public ushort Major => value.Major;
        public ushort Minor => value.Minor;
        public ushort Patch => value.Build;
        public static UnityVersion Unknown => default;

        public static UnityVersion Parse(string text)
            => new(AssetRipper.Primitives.UnityVersion.Parse(text));

        public int CompareTo(UnityVersion other) => value.CompareTo(other.value);
        public bool Equals(UnityVersion other) => value.Equals(other.value);
        public override bool Equals(object obj) => obj is UnityVersion other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => value.ToString();

        public static bool operator ==(UnityVersion left, UnityVersion right) => left.Equals(right);
        public static bool operator !=(UnityVersion left, UnityVersion right) => !left.Equals(right);
        public static bool operator <(UnityVersion left, UnityVersion right) => left.CompareTo(right) < 0;
        public static bool operator >(UnityVersion left, UnityVersion right) => left.CompareTo(right) > 0;
        public static bool operator <=(UnityVersion left, UnityVersion right) => left.CompareTo(right) <= 0;
        public static bool operator >=(UnityVersion left, UnityVersion right) => left.CompareTo(right) >= 0;
    }
}
