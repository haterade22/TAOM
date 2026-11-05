using System;
using System.Threading;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>Counts the shots of one company and the distinct soldiers who loosed them, from any thread, with no lock and no allocation.</summary>
internal sealed class ShotWatch<T> where T : class
{
    internal const int Capacity = 128;

    private readonly T?[] _shooters = new T?[Capacity];
    private int _shots;
    private int _overflow;

    internal int Shots => Volatile.Read(ref _shots);

    /// <summary>Shots by soldiers that came after the table of distinct shooters was full.</summary>
    internal int Overflow => Volatile.Read(ref _overflow);

    internal int Shooters
    {
        get
        {
            var count = 0;
            for (var i = 0; i < _shooters.Length && Volatile.Read(ref _shooters[i]) != null; i++)
                count++;
            return count;
        }
    }

    internal void Reset()
    {
        Array.Clear(_shooters, 0, _shooters.Length);
        Volatile.Write(ref _shots, 0);
        Volatile.Write(ref _overflow, 0);
    }

    internal void Note(T shooter)
    {
        Interlocked.Increment(ref _shots);
        for (var i = 0; i < _shooters.Length; i++)
        {
            var seen = Interlocked.CompareExchange(ref _shooters[i], shooter, null);
            if (seen == null || ReferenceEquals(seen, shooter))
                return;
        }

        Interlocked.Increment(ref _overflow);
    }
}
