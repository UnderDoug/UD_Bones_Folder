using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod
{
    /// <summary>
    /// Defines a generalized coalescence method that a value type or class implements to create a type-specific coalescense method for reducing two its instances into a single one.
    /// </summary>
    /// <typeparam name="T">The type of object to coalesce.</typeparam>
    public interface ICoalescible<T>
    {
        /// <summary>
        /// Coalesces the current instance with another object of the same type and returns the result.
        /// </summary>
        /// <remarks>
        /// Coalescence might look like multiple different things. It could be concatenating two <see cref="string"/>s together, performing addition on two <see cref="int"/>s, or outputting boolean logic.
        /// </remarks>
        /// <param name="Other">An object to coalesce with this istance.</param>
        /// <returns>A single instance representing the result of reducing two instances into one.</returns>
        /// <example>
        /// Examples of Coalsce:
        ///     strings:
        ///         string string1 = "first";
        ///         string string2 = "second"
        ///         string1.Coalesce(string2) returns "firstsecond" (concatenation) reducing them from two instances to one.
        ///         string1.Coalesce(string2) returns "second" (returning the longest one) reducing them from two instances to one.
        ///         string1.Coalesce(string2) returns <see cref="null"/> (only returning an object if they match) reducing them from two instances to one. 
        ///     integers:
        ///         int int1 = 10;
        ///         int int2 = 20;
        ///         int1.Coalesce(int2) returns 30 (additon) reducing them from two instances to one.
        ///         int1.Coalesce(int2) returns 15 (average) reducing them from two instances to one.
        ///         int1.Coalesce(int2) returns -10 (subtraction) reducing them from two instances to one.
        ///         int1.Coalesce(int2) returns 10 (minimum) reducing them from two instances to one.
        ///     boolean:
        ///         bool bool1 = true;
        ///         bool bool2 = false;
        ///         bool1.Coalesce(bool2) returns true (OR) reducing them from two instances to one.
        ///         bool1.Coalesce(bool2) returns false (AND) reducing them from two instances to one.
        ///         bool1.Coalesce(bool2) returns false (XOR) reducing them from two instances to one.
        ///         bool1.Coalesce(bool2) returns true (NAND) reducing them from two instances to one.
        /// </example>
        T Coalesce(T Other);
    }
}
