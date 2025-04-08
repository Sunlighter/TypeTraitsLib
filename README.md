<!-- -*- coding: utf-8; fill-column: 118 -*- -->

# TypeTraitsLib

This is a type traits library that allows for comparison, hashing, and serialization of a wide variety of C# types,
including immutable collections. These type traits are *composeable*, meaning that you can construct new type traits
using combinators and primitive type traits. The central interface is `ITypeTraits<T>`. There are many extension
methods, singleton classes which implement the traits for various values of `T`, and some classes for tuples and
immutable collections. There are also classes for union types, record types, and types which might be self-referential
(see `RecursiveTypeTraits<T>`.) It is also possible to implement `ITypeTraits<T>` directly.

If you want to see how they are used, take a look at the Sunlighter/MacroProtocol project (which is a separate project
on GitHub). The MacroProtocol defines several objects that are meant to be sent back and forth over TCP, and it
accomplishes this by constructing type traits for the serializable objects. The type traits then provide the
serialization and deserialization capabilities.

Although generally designed for immutable types, the library also supports `StrongBox<T>` and user-defined mutable box
types. Mutable boxes can be used to create circular references, which are serialized correctly but verbosely: the
library may serialize a second copy before detecting a circular reference.

There is also code to send serialized objects as blobs over TCP, and to set up servers and clients.

This implementation of the traits library has traits for `System.Type` and `System.Assembly` which are generally
intended to allow types to be used, for example, as keys in immutable sorted dictionaries.  However, the traits would
allow instances of these types to be serialized and deserialized, and it might be dangerous to deserialize a file
where an attacker could have specified an arbitrary type or assembly to deserialize. It should be noted that I do
*not* have any code that loads assemblies as a result of deserialization. You can only deserialize an assembly if it
is already loaded. If the deserialized data specifies an assembly which is not loaded, an exception will be thrown
during deserialization. But I do not know if this is sufficient to ensure security.

I have seen some cases where the same assembly seems to be loaded twice, but types from one loading do not seem to be
equivalent to types from the other loading. (This seems to only happen when running tests, and it only affects certain
test-related assemblies which are probably used internally by the test framework but would probably not be used by
normal tests or by the code being tested &mdash; but even if they are *used*, you probably wouldn&rsquo;t want to
create traits for these types and try to serialize instances of these types, etc.) The traits classes, as currently
written, will not be able to work with types where the assembly cannot be uniquely identified.
