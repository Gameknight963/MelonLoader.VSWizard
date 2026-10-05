using System;

namespace MelonLoader.ProjectGeneration
{
    /// <summary>An immutable Unity version, including its release suffix when parsed.</summary>
    public readonly struct UnityVersion : IComparable<UnityVersion>, IEquatable<UnityVersion>
    {
        private readonly AssetRipper.Primitives.UnityVersion value;

        /// <summary>Creates a Unity version from numeric components without a parsed release suffix.</summary>
        /// <param name="major">The major version, such as 2021.</param>
        /// <param name="minor">The minor version, such as 2.</param>
        /// <param name="patch">The patch version, such as 0.</param>
        public UnityVersion(ushort major, ushort minor, ushort patch)
            : this(new AssetRipper.Primitives.UnityVersion(major, minor, patch)) { }

        private UnityVersion(AssetRipper.Primitives.UnityVersion value) => this.value = value;

        /// <summary>Gets the major version component.</summary>
        public ushort Major => value.Major;
        /// <summary>Gets the minor version component.</summary>
        public ushort Minor => value.Minor;
        /// <summary>Gets the patch version component.</summary>
        public ushort Patch => value.Build;
        /// <summary>Gets the default zero-valued version used when Unity metadata is unavailable.</summary>
        public static UnityVersion Unknown => default;

        /// <summary>Parses a Unity version string while preserving its release suffix.</summary>
        /// <param name="text">A Unity version string, such as 2021.2.0f1.</param>
        /// <returns>The parsed immutable version.</returns>
        /// <remarks>Malformed input propagates the underlying parser's exception.</remarks>
        public static UnityVersion Parse(string text)
            => new(AssetRipper.Primitives.UnityVersion.Parse(text));

        /// <summary>Compares numeric components and release suffix ordering.</summary>
        /// <param name="other">The version to compare with.</param>
        /// <returns>A negative value when earlier, zero when equal, or a positive value when later.</returns>
        public int CompareTo(UnityVersion other) => value.CompareTo(other.value);
        /// <summary>Tests whether another version has the same version components and suffix.</summary>
        /// <param name="other">The version to compare with.</param>
        /// <returns><see langword="true"/> when the versions are equal.</returns>
        public bool Equals(UnityVersion other) => value.Equals(other.value);
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is UnityVersion other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => value.GetHashCode();
        /// <inheritdoc />
        public override string ToString() => value.ToString();

        /// <summary>Tests whether two Unity versions are equal.</summary>
        /// <param name="left">The <paramref name="left"/> version.</param>
        /// <param name="right">The <paramref name="right"/> version.</param>
        /// <returns><see langword="true"/> when the comparison holds.</returns>
        public static bool operator ==(UnityVersion left, UnityVersion right) => left.Equals(right);
        /// <summary>Tests whether two Unity versions are different.</summary>
        /// <param name="left">The <paramref name="left"/> version.</param>
        /// <param name="right">The <paramref name="right"/> version.</param>
        /// <returns><see langword="true"/> when the comparison holds.</returns>
        public static bool operator !=(UnityVersion left, UnityVersion right) => !left.Equals(right);
        /// <summary>Tests whether the <paramref name="left"/> version is earlier than the <paramref name="right"/> version.</summary>
        /// <param name="left">The <paramref name="left"/> version.</param>
        /// <param name="right">The <paramref name="right"/> version.</param>
        /// <returns><see langword="true"/> when the comparison holds.</returns>
        public static bool operator <(UnityVersion left, UnityVersion right) => left.CompareTo(right) < 0;
        /// <summary>Tests whether the <paramref name="left"/> version is later than the <paramref name="right"/> version.</summary>
        /// <param name="left">The <paramref name="left"/> version.</param>
        /// <param name="right">The <paramref name="right"/> version.</param>
        /// <returns><see langword="true"/> when the comparison holds.</returns>
        public static bool operator >(UnityVersion left, UnityVersion right) => left.CompareTo(right) > 0;
        /// <summary>Tests whether the <paramref name="left"/> version is earlier than or equal to the <paramref name="right"/> version.</summary>
        /// <param name="left">The <paramref name="left"/> version.</param>
        /// <param name="right">The <paramref name="right"/> version.</param>
        /// <returns><see langword="true"/> when the comparison holds.</returns>
        public static bool operator <=(UnityVersion left, UnityVersion right) => left.CompareTo(right) <= 0;
        /// <summary>Tests whether the <paramref name="left"/> version is later than or equal to the <paramref name="right"/> version.</summary>
        /// <param name="left">The <paramref name="left"/> version.</param>
        /// <param name="right">The <paramref name="right"/> version.</param>
        /// <returns><see langword="true"/> when the comparison holds.</returns>
        public static bool operator >=(UnityVersion left, UnityVersion right) => left.CompareTo(right) >= 0;
    }
}
