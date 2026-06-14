using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod
{
    /// <summary>Represents the method that coalesces two object instances into a single one.</summary>
    /// <typeparam name="T">The type of objects to coalesce.</typeparam>
    /// <param name="x">The first object to coalesce.</param>
    /// <param name="y">The other object to coalesce.</param>
    /// <returns>The result of coalescing the <paramref name="x"/> with the <paramref name="y"/>.</returns>
    /// <example>
    /// Examples of Coalsce:
    ///     strings:
    ///         string string1 = "first";
    ///         string string2 = "second"
    ///         Coalesce(string1, string2) returns "firstsecond" (concatenation) reducing them from two instances to one.
    ///         Coalesce(string1, string2) returns "second" (returning the longest one) reducing them from two instances to one.
    ///         Coalesce(string1, string2) returns <see cref="null"/> (only returning an object if they match) reducing them from two instances to one. 
    ///     integers:
    ///         int int1 = 10;
    ///         int int2 = 20;
    ///         Coalesce(int1, int2) returns 30 (additon) reducing them from two instances to one.
    ///         Coalesce(int1, int2) returns 15 (average) reducing them from two instances to one.
    ///         Coalesce(int1, int2) returns -10 (subtraction) reducing them from two instances to one.
    ///         Coalesce(int1, int2) returns 10 (minimum) reducing them from two instances to one.
    ///     boolean:
    ///         bool bool1 = true;
    ///         bool bool2 = false;
    ///         Coalesce(bool1, bool2) returns true (OR) reducing them from two instances to one.
    ///         Coalesce(bool1, bool2) returns false (AND) reducing them from two instances to one.
    ///         Coalesce(bool1, bool2) returns false (XOR) reducing them from two instances to one.
    ///         Coalesce(bool1, bool2) returns true (NAND) reducing them from two instances to one.
    /// </example>
    public delegate T Coalesce<T>(T x, T y);
}
