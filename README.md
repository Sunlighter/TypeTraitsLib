<!-- -*- coding: utf-8; fill-column: 118 -*- -->

# TypeTraitsLib

**Sunlighter.TypeTraitsLib** is a type traits library that allows for comparison, hashing, and binary serialization of
a wide variety of C# types, including immutable collections. These type traits are *composeable*, meaning that you can
construct new type traits using combinators and primitive type traits. The central interface is
`ITypeTraits<T>`. There are many extension methods, singleton classes which implement the traits for various values of
`T`, and some classes for tuples and immutable collections. There are also classes for union types, record types, and
types defined recursively (see `RecursiveTypeTraits<T>`.) It is also possible to implement `ITypeTraits<T>` directly.

The library works with the .NET Framework and with the most recent versions of .NET Core.

Hashing is very adaptable and uses an abstract `HashBuilder` class, which basically receives bytes and mixes them into
the hash-in-progress. It is possible to produce an `int` hash like that returned by `Object.GetHashCode`, or an SHA256
hash, just by using different `HashBuilder` instances. For convenience, extension methods are available to easily
compute hashes. **Sunlighter.ShelfLib** uses this library&rsquo;s SHA256 hashes to quickly locate serialized objects
in databases.

There is also an `Adapter<T>` class which implements `IComparer<T>` and `IEqualityComparer<T>`. This adapter is
suitable to pass to the `WithComparer` or `WithComparers` functions of immutable collections, so that any type that
has traits can be used as a key in a dictionary.

The serialization format is &ldquo;just a bunch of bytes&rdquo; and is sensitive to the structure of the data and to
the names of union cases. (A new union case with a new name can be added without disturbing the deserialization of
existing union cases.) It is not sensitive to the *names* of classes, though, so it is safe to rename a class or move
it to another namespace (or assembly). It is also the same regardless of what version of .NET you are using, so it can
be used to communicate between different versions of .NET.

Type traits can also be used to generate &ldquo;debug strings&rdquo; so that the values of complex data structures can
be easily seen or logged. However, there is no corresponding parser.

## The Type Traits Builder

New in Version 1.1 is a `Builder` class which can automatically construct traits and adapters for a wide variery of
given types, including user-defined types with certain attributes. The `Builder` handles recursive types properly and
makes the library much easier to use.

It can be used like this:

```csharp
using Sunlighter.TypeTraitsLib;
using Sunlighter.TypeTraitsLib.Building;

namespace Example
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ITypeTraits<ImmutableSortedDictionary<(string, ImmutableList<string>), long>> dictionaryTraits =
                Builder.Instance.GetTypeTraits<ImmutableSortedDictionary<(string, ImmutableList<string>), long>>();

            Adapter<(string, ImmutableList<string>)> keyAdapter =
                Builder.Instance.GetAdapter<(string, ImmutableList<string>)>();

            ImmutableSortedDictionary<(string, ImmutableList<string>), long> dict =
                ImmutableSortedDictionary<(string, ImmutableList<string>), long>.Empty.WithComparers(keyAdapter);

            dict = dict.Add(("one", []), 100);
            dict = dict.Add(("two", [ "some", "key", "words" ]), 102);
            dict = dict.Add(("one", [ "other" ]), 104);

            Console.WriteLine(dict.ContainsKey(("one", [ "other" ]))); // True

            // serialization example

            byte[] serializedDict = dictionaryTraits.SerializeToBytes(dict);
            ImmutableSortedDictionary<(string, ImmutableList<string>), long> dict2 =
                dictionaryTraits.DeserializeFromBytes(serializedDict);

            Console.WriteLine(dictionaryTraits.Compare(dict, dict2)); // 0
        }
    }
}
```

User-defined types must have certain attributes on them in order for traits to be created automatically.

A `[Record]` attribute marks user-defined records (which are expected to be immutable). There should be a single
constructor that takes an argument for every property, and each property should have a `[Bind(...)]` attribute to
indicate which constructor parameter it goes with. It is also possible to use a `[Bind(...)]` attribute on the
constructor parameter itself (in case you want to bind under a name different from the parameter&rsquo;s name).

The builder can construct &ldquo;setters&rdquo; for records with immutable properties. (The field name is given by the
constructor parameter name or the `[Bind(...)]` attribute.) It also works with tuples (where the binding names are
`item1` and so forth). The &ldquo;setter&rdquo; constructs a new record or tuple, where all the fields are the same
except for the one that was written.

A `[Singleton(...)]` attribute can be used to serialize singletons using the `UnitTypeTraits<T>` class. The attribute
should be given a somewhat unique random `uint` value which will be fed into the `HashBuilder` when the value is
encountered.

A `[UnionOfDescendants]` attribute can be put on an abstract class; this will cause the Builder to create a
`UnionTypeTraits<T>` instance. All public or nested public classes in the same assembly which inherit from this class
will be made into union cases. If the abstract class is generic, it is only possible to construct traits for
&ldquo;closed&rdquo; generics (in other words you must specify types for the generic parameters). The builder ignores
descendants that cannot be constructed due to constraints. It also ignores descendants that introduce additional
generic parameters (because those would lead to an open-ended number of union cases, one for each value of the generic
parameter).

The Builder can also construct traits and adapters for tuples and value tuples with up to seven items.

## Strong Box Traits

Although generally designed for immutable types, the library also supports `StrongBox<T>` and user-defined mutable box
types. Mutable boxes can be used to create circular references, which are serialized correctly but verbosely: the
library may serialize a second copy of the data before detecting a circular reference, because it has to reach the
same mutable box again, and does not assign identities to other data. (Note that deserialization creates a new box,
and since boxes are compared by identity and not by content, the data with the new box will not be equal to the data
with the old box.)

## Networking

There is also code to send serialized objects as blobs over TCP, and to set up servers and clients.

If you want to see how type traits can be used to make an ad-hoc networking protocol, take a look at the
Sunlighter/MacroProtocol project (which is a separate project on GitHub). The MacroProtocol defines several objects
that are meant to be sent back and forth over TCP, and it accomplishes this by constructing type traits for the
serializable objects. The type traits then provide the serialization and deserialization capabilities.

## Types and Assemblies

This implementation of the traits library has traits for `System.Type` and `System.Assembly` which are generally
intended to allow types to be used, for example, as keys in dictionaries. (**Sunlighter.LrParserGenLib** uses them to
allow types to be used as terminals and nonterminals in a grammar.) However, the traits would allow instances of these
types to be serialized and deserialized (this only serializes the *identity* of the type or assembly, and not the
*code* in it). It might be dangerous to deserialize a file where an attacker could have specified an arbitrary type or
assembly to deserialize. It should be noted that I do *not* have any code that loads assemblies as a result of
deserialization. You can only deserialize an assembly if it is already loaded. If the deserialized data specifies an
assembly which is not loaded, an exception will be thrown during deserialization. But I do not know if this is
sufficient to ensure security.

## A Note About Duplicate Assemblies

I have seen some cases (in dot-Net Core) where the same assembly seems to be loaded twice, but types from one instance
of the assembly do not seem to be equivalent to types from the other instance. This seems to only happen when running
tests, and it only affects certain test-related assemblies which are probably used internally by the test framework
but would probably not be used by normal tests or by the code being tested.

The traits classes, as currently written, will not be able to work with types where the assembly cannot be uniquely
identified. This means not only that you can&rsquo;t create traits for these types, but that you can&rsquo;t use the
`Type` objects as keys in dictionaries.
