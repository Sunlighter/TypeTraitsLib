<!-- -*- coding: utf-8; fill-column: 118 -*- -->

# TypeTraitsLib

**Sunlighter.TypeTraitsLib** is a type traits library that allows for comparison, hashing, and binary serialization of
a wide variety of C# types, including immutable collections. These type traits are *composable*, meaning that you can
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
existing union cases.) It is not sensitive to the *names* of classes, though (unless the class names are used as union
case names), so it is safe to rename a class or move it to another namespace (or assembly). It is also the same
regardless of what version of .NET you are using, so it can be used to communicate between different versions of .NET.

Type traits can also be used to generate &ldquo;debug strings&rdquo; so that the values of complex data structures can
be easily seen or logged. However, there is no corresponding parser.

## The Type Traits Builder

New in Version 1.1 is a `Builder` class which can automatically construct traits and adapters for a wide variery of
given types, including user-defined types with certain attributes. The `Builder` handles recursive types properly and
makes the library much easier to use.

Just call `Builder.Instance.GetTypeTraits<T>()` or `Builder.Instance.GetAdapter<T>()` like this:

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
constructor parameter itself (in case you want to bind under a name different from the parameter&rsquo;s name).  If
there is more than one constructor, the Builder will favor the constructor that has a `[Bind(...)]` attribute on any
parameter.

The builder can construct &ldquo;setters&rdquo; for records with immutable properties. Just call
`Builder.Instance.GetSetter<T, U>(string bindingVariable)` where `T` is the record type, `U` is the field type, and
`bindingVariable` is the field name, which must match a constructor parameter name or a `[Bind(...)]`
attribute. Setters also work with tuples (where the binding names are `item1` and so forth). The &ldquo;setter&rdquo;
constructs a new record or tuple, where all the fields are the same except for the one that was written.

A `[Singleton(...)]` attribute can be used to serialize singletons using the `UnitTypeTraits<T>` class. The attribute
should be given a somewhat unique random `uint` value which will be fed into the `HashBuilder` when the value is
encountered.

A `[UnionOfDescendants]` attribute can be put on an abstract class; this will cause the Builder to create a
`UnionTypeTraits<T>` instance. All public or nested public classes in the same assembly which inherit from this class
will be made into union cases. The union case name will be the same as the type name, unless the
`[UnionCaseName("...")]` attribute is used. If the abstract class is generic, it is only possible to construct traits
for &ldquo;closed&rdquo; generics (in other words you must specify types for the generic parameters). The builder
ignores descendants that cannot be constructed due to constraints. It also ignores descendants that introduce
additional generic parameters (because those would lead to an open-ended number of union cases, one for each value of
the generic parameter).

A `[ProvidesOwnTypeTraits]` attribute can be put on a class (including an abstract class) indicating that it provides
its own type traits. The class should have a static property called `TypeTraits` which returns an instance of
`ITypeTraits<T>` where `T` is the class itself.

A `[ProvidesOwnAdapter]` attribute can be used if the class already provides its own `Adapter<T>`. It should not be
necessary for new classes to do this, but I have some classes with static properties that lazily create adapters from
their own type traits. The static property should be called `Adapter` and should return an instance of `Adapter<T>`
where `T` is the type of the class.

A `[GensymInt32]` attribute can be used to indicate that a class is used &ldquo;like a `gensym` in Lisp.&rdquo; This
will cause the Builder to create a `GensymTypeTraits<T, TId, int>` instance. A class bearing the `[GensymInt32]`
attribute must have a default constructor and a property called <code>ID</code> (case-sensitive). The <code>ID</code>
does not have to be an `int` but can be any type `TId` for which the `Builder` can construct traits. A dictionary will
be used during serialization to map `ID` values to `int` values which will be serialized.

The Builder can also construct traits and adapters for tuples and value tuples with up to seven items.

**Note:** Type traits and adapters do not support `null` values.

## Strong Box Traits

Although generally designed for immutable types, the library also supports `StrongBox<T>` and user-defined mutable box
types. Mutable boxes can be used to create circular references, which are serialized correctly but verbosely: the
library may serialize a second copy of the data before detecting a circular reference, because it has to reach the
same mutable box again, and does not assign identities to other data. (Note that deserialization creates a new box,
and since boxes are compared by identity and not by content, the new box will not be equal to the old box.)

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

New in Version 2.0 is a static function `AssemblyTypeTraits.IsDuplicate` which takes an assembly and checks to see if
it is a duplicate. There is also `TypeTypeTraits.IsDuplicateAssembly`. Note that if you dynamically load an assembly,
this might change whether an assembly is a duplicate, but the Assembly Type Traits will not detect this until it
receives the new Assembly object.

## A Note About Immutable Collections

With the exception of mutable boxes, this library makes the assumption that data is immutable, and that it does not
have an &ldquo;identity&rdquo; separate from its value. (In other words, you cannot change the value of `3`, and it
doesn&rsquo;t matter which `3` it is.)

With the Immutable Collections, changing a collection returns a new collection, while the old one is untouched.  For
efficiency reasons, the new collection and the old collection may share storage. (For example, if you change the first
half of a list, the second half does not change, and can be shared. Since the list is represented as a balanced tree,
this applies recursively down the tree &mdash; there is always a half that did not change &mdash; and this is why you
can change a list in logarithmic time.)

This library does not have access to the internal nodes of the immutable collections, and cannot tell if they are
shared. This has the effect that, if you serialize several lists that share storage, and then deserialize them, the
deserialized versions will *not* share storage and will take up more memory than the original lists.

# Reference

Here is a complete description of the classes and functions provided by the library.

## Type Traits Classes

These are the type traits classes provided in the `Sunlighter.TypeTraitsLib` namespace.

Below are the classes for the traits of primitive types. Each of these classes has a static `Value` property that
returns an appropriate instance of `ITypeTraits<T>`, except `FixedLengthByteArrayTypeTraits` which has a constructor
and expects the number of bytes as a constructor parameter. Some of these `Value` properties return instances of the
class they are defined in, but others return instances of other classes.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`StringTypeTraits`|`string`||
|`CharTypeTraits`|`char`||
|`ByteTypeTraits`|`byte`||
|`SByteTypeTraits`|`sbyte`||
|`Int16TypeTraits`|`short`||
|`UInt16TypeTraits`|`ushort`||
|`Int32TypeTraits`|`int`||
|`UInt32TypeTraits`|`uint`||
|`Int64TypeTraits`|`long`||
|`UInt64TypeTraits`|`ulong`||
|`SingleTypeTraits`|`float`|converts to `int`|
|`DoubleTypeTraits`|`double`|converts to `long`|
|`BigIntegerTypeTraits`|`System.Numerics.BigInteger`||
|`ByteArrayTypeTraits`|`byte[]`|immutable[^1]|
|`FixedLengthByteArrayTypeTraits`|`byte[]`|immutable[^1], fixed-length[^2]|
|`ImmutableByteArrayTypeTraits`|`ImmutableArray<byte>`[^3]||
|`BooleanTypeTraits`|`bool`|serializes as one byte|
|`DateTimeTypeTraits`|`System.DateTime`|converts to `long`|
|`GuidTypeTraits`|`System.Guid`|converts to fixed-length `byte[]`|

Note that because `SingleTypeTraits`, `DoubleTypeTraits`, and `DateTimeTypeTraits` work by converting to other types,
the `Compare` function will not work as it normally does for these types, because the type will be converted first and
then compared. This creates situations, e.g., where positive and negative zero can be distinct keys in dictionaries.

The following are compound type traits that take other type traits as arguments. This makes it possible to use `new`
to manually build traits for complex types.

These tuple traits are provided, but they are not used by the `Builder`, which has its own way of dealing with tuples.
The `Builder` can build traits for tuples with up to seven items.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`TupleTypeTraits<T, U>`|`Tuple<T, U>`||
|`ValueTupleTypeTraits<T, U>`|`(T, U)`||
|`TupleTypeTraits<T, U, V>`|`Tuple<T, U, V>`||
|`ValueTupleTypeTraits<T, U, V>`|`(T, U, V)`||

The following traits classes are for containers. There are currently no plans to support hash-based sets and
dictionaries.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`OptionTypeTraits<T>`|`Sunlighter.OptionLib.Option<T>`||
|`ListTypeTraits<T>`|`ImmutableList<T>`[^3]||
|`SetTypeTraits<T>`|`ImmutableSortedSet<T>`[^3]||
|`DictionaryTypeTraits<T>`|`ImmutableSortedDictionary<T>`[^3]||

Some types can be handled by converting to other types. For example, records with only two or three fields can be
converted into tuples.

Sometimes it is helpful to write a debug string that is aware of the original type instead of simply writing the debug
string of the conversion.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`ConvertTypeTraits<T, U>`|`T`|converts to `U` and back|
|`ConvertTypeTraitsDebugOverride<T, U>`|`T`|allows overriding `ToDebugString`|

The `GuardedTypeTraits` class allows you to set a condition which must always be met, and throws a `GuardException` if
any function is called with an object that does not meet the condition. (This is useful for types where some values
are serializable and some are not.)

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`GuardedTypeTraits<T>`|`T`||

A *unit type* is a type that can have only one value, such as `DBNull`. There are two classes for unit types. One
assumes that the unit type is a singleton, i.e., that only one value exists at runtime. The other allows that there
might be multiple instances of the unit type at distinct addresses, but it refuses to distinguish them. The serialized
form takes zero bytes, but the constructor requires a `uint` which is passed to the `HashBuilder`. The non-singleton
trait requires a function to create a new instance.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`UnitTypeTraits<T>`|`T`|singleton|
|`UnitTypeTraitsNonSingleton<T>`|`T`||

The `RecursiveTypeTraits<T>` class allows traits for recursive and mutually-recursive types to be defined. The way
to do this is:

* Create a `RecursiveTypeTraits<T>` instance for each type, to act as a stand-in
* Create the type traits using the stand-in
* Call the `Set` function to set the stand-in to refer back to the created traits

The `Set` function must be called exactly once.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`RecursiveTypeTraits<T>`|`T`||

The `MutableBoxTypeTraits` class allows creating type traits for various kinds of mutable boxes.

A mutable box is required to have a key of type `K` which uniquely identifies the box. Comparison and hashing look
*only* at the key. A mutable box also has a value of type `V`, which must not be null.

The function `TypeTraitsUtility.GetStrongBoxTraits<T>` returns a type traits object for `StrongBox<T>`, which is an
instance of `MutableBoxTypeTraits<StrongBox<T>, long, T>`.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`MutableBoxTypeTraits<T, K, V>`|`T`||

The `GensymTypeTraits` class is named after the Lisp `gensym` function. It is designed for objects that act like
unique symbols. The `gensym` function is supposed to generate a new symbol which is guaranteed to be distinct from any
other symbol in the runtime. This means that deserialization must also generate new symbols.

Every symbol of type `T` is expected to have a runtime id of type `TId`. This is converted to a serialized ID of type
`TSerializedID`, so that when you serialize copies of the same `gensym` symbol, you get back copies of the same
`gensym` symbol (although it is not the same as the one that was serialized), and when you serialize distinct `gensym`
symbols, you get back distinct `gensym` symbols. The mapping between the old symbols and the new symbols will be
one-for-one.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`GensymTypeTraits<T, TId, TSerializedId>`|`T`|since 2.0|

The `UnionTypeTraits<TTag, T>` class is designed for types which can be defined as a union of other types. The most
common case in C# is an abstract class with a set of descendants, but it is sometimes possible to regard other types
as unions.1 The `TTag` type is used in serialization to identify the union case.

A `UnionTypeTraits<TTag, T>` instance is constructed with (among other things) a list of union cases. Each case must
include a tag and a case trait. A case trait is an instance of `IUnionCaseTypeTraits<TTag, T>`, which can be either of
two descendants. (The descendants also take an additional type parameter.)

The `UnionCaseTypeTraits2<TTag, T, U>` class is designed for the common situation where `U` is a subclass of `T`.

The `UnionCaseTypeTraits<TTag, T, U>` class is designed for other situations and requires (among other things) a
function to identify whether a given instance of `T` can be converted to `U`. In this case, `U` does not have to be a
descendant of `T`.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`UnionTypeTraits<TTag, T>`|`T`||

The `RecordTypeTraits<TRecord, TBuilder>` class is designed for immutable record types. This class is not used by the
`Builder`, which has its own way of dealing with immutable record types.

`TRecord` is the type of the immutable record but `TBuilder` is meant to be a mutable type that can be converted into
a `TRecord`. It is possible for `TBuilder` to be a mutable dictionary or an array of objects.

The constructor requires (among other things) a list of `AbstractFieldTypeTraits<TRecord, TBuilder>` objects. There
is only one concrete descendant, `FieldTypeTraits<TRecord, TBuilder, T>`, where `T` is the type of the field.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`RecordTypeTraits<TRecord, TBuilder>`|`TRecord`||

There are traits for types and assemblies, which allow the use of types as keys in dictionaries. (Reference and
pointer types are not currently supported.) The traits for assemblies is used by the traits for types.

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`TypeTypeTraits`|`Type`||
|`AssemblyTypeTraits`|`System.Reflection.Assembly`||

Finally there is a trait class that uses delegates to &ldquo;outsource&rdquo; the computation. This class is used
by the `Builder`:

| Class Name | Type Traits For | Notes |
|:----|:--------|:----|
|`DelegateTypeTraits<T>`|`T`||

## Functions

The following functions and extension methods are provided. They are documented here as if they were public instance
members of the type traits class, but they are all extension methods[^4] except for `Compare`.

* `int Compare(T a, T b)` returns -1, 0, or 1. This function considers only the keys of mutable boxes and is therefore
  non-recursive.

* `bool IsAnalogous(T a, T b)` [since 2.0] checks to see if `a` and `b` are analogous (as would result if one was a
  clone of the other, or a serialized and deserialized copy of the other).

* `bool CanSerialize(T a)`[^4] checks to see if `a` is serializable. This function detects fixed-length arrays of the
  wrong length and unrecognized union cases, without throwing an exception. However, if it returns `false` it does not
  tell you *why* `a` cannot be serialized...

* `byte[] SerializeToBytes(T a)` serializes `a` to bytes.

* `T DeserializeFromBytes(byte[] b)` deserializes a value from bytes and returns it.

* `long MeasureAllBytes(T item)` returns the number of bytes that would be required to serialize `item`.

* `void SerializeToFile(string filePath, T a)` creates or overwrites a file given by `filePath` with the bytes
  produced by serializing `a`.

* `T DeserializeFromFile(string filePath)` reads a file given by `filePath` and deserializes a value from it, which is
  then returned.

* `T LoadOrGenerate(string filePath, Func<T> generate)` tries to read a value from the file given by `filePath`, and
  if that fails, generates the value by calling the `generate` function, and then tries to create the file given by
  `filePath`.

* `int GetBasicHashCode(T a)` returns an `int` hash of `a` suitable for use as if from `Object.GetHashCode`.

* `byte[] GetSHA256Hash(T a)` returns the SHA-256 hash of `a`. (32 bytes)

* `T Clone(T a)` [since 2.0] creates a clone of `a`, which means any mutable boxes and gensyms are replaced according
  to a one-for-one mapping. The result `IsAnalogous` to `a`. This is similar to serializing and then deserializing `a`
  but without the intermediate byte array.

* `string ToDebugString(T a)` returns a string describing the contents of `a`.

## Internal Functions

It is often not necessary to write a new implementation of `ITypeTraits<T>`. To handle new primitive types it is
recommended to use `ConvertTypeTraits<T, U>` to convert the new types to integral types, tuples, or byte arrays, and
then use the type traits for those.

If it is necessary to write a new implementation of `ITypeTraits<T>`, the following functions have to be provided.

* `int Compare(T a, T b)` returns -1, 0, or 1.

* `void AddToHash(HashBuilder b, T a)`. Passes important bytes in `a` to `b`. (The keys of mutable boxes are hashed,
  but the values are not. This makes the function non-recursive.)

There are two descendants of `HashBuilder`, which are `BasicHashBuilder` and `SHA256HashBuilder`.

The remaining functions are recursive and each one receives a specialized descendant of an abstract class called
`SerializerStateManager`. It is not always required for an implementation of `ITypeTraits<T>` to use this object, but
if it is required, each instance should, in its constructor, call the static function `SerializerStateID.Next()` to
get a `SerializerStateID`. A `SerializerStateManager` keeps serializer states for all the type traits classes it
encounters. Type traits classes can use these state objects to detect recursion and deal with it appropriately. A
`SerializerStateManager` also keeps a queue which is used to delay recursive activities (such as the reading and
writing of mutable box values) so that they occur only once.

Public instance members of the `SerializerStateManager` are:

* `T GetSerializerState<T>(SerializerStateID ssid, Func<T> create)` returns the serializer state for the given `ssid`
  if it exists, and creates, retains, and returns it if it does not exist.

* `void Enqueue(Action a)` enqueues a recursive action for later. (Note that the *caller* must ensure that the action
  has not already been enqueued.)

* `void RunQueue()` should be called after all the recursive calls return, to run any delayed actions.

The recursive functions of `ITypeTraits<T>` are:

* `void CheckAnalogous(AnalogyTracker tracker, T a, T b)`. The `AnalogyTracker` keeps a `bool` (exposed as the
  property `IsAnalogous`) which starts out `true` and then is set to `false` (by the method `SetNonAnalogous`) when
  the first proof is found that `a` and `b` are non-analogous. So unless the `IsAnalogous` flag is already `false`,
  `CheckAnalogous` simply recursively calls the `CheckAnalogous` function for the corresponding parts of `a` and `b`.
  (If `a` and `b` don&rsquo;t have corresponding parts, they are not analogous.)

* `void CheckSerializability(SerializabilityTracker tracker, T a)`. The `SerializabilityTracker` keeps a `bool`
  (exposed as the property `IsSerializable` which starts out `true` and is then set to `false` (by the method
  `SetNonSerializable`) when the first non-serializable value is found. So unless this flag is already `false`,
  `CheckSerializability` recursively checks the serializability of the parts of `a`.

* `void Serialize(Serializer dest, T a)`. Writes `a` to `dest`. `dest` contains a `System.IO.BinaryWriter`. The first
  time a mutable box is encountered an action is enqueued to serialize its value.

* `T Deserialize(Deserializer src)`. Reads a value from `src`. `src` contains a `System.IO.BinaryReader`. The first
  time a mutable box is encountered an action is enqueued to deserialize and set its value. Importantly, this means
  that deserialization actions are queued and run in the same order as serialization actions.

* `void MeasureBytes(ByteMeasurer measurer, T a)`. Calculates the number of bytes in the serialized form of `a`.
  The `ByteMeasurer` acts as an accumulator.

* `T Clone(CloneTracker tracker, T a)`. Clones `a` and returns the clone. `CloneTracker` allows that if `a` is a
  mutable box or a gensym which has already been cloned, the same clone is returned.

* `void AppendDebugString(DebugStringBuilder sb, T a)`. Appends a debug string to the `System.Text.StringBuilder`
  enclosed in the `DebugStringBuilder`. Box contents are queued as with serialization.


[^1]: Comparison and hashing look at the current contents of the byte array. If the byte array is placed in a sorted
      collection and then changed, it may appear to be in the wrong place in the collection, and this will violate the
      collection&rsquo;s assumptions, likely leading to erroneous behavior. Cloning, however, allocates a new byte
      array.

[^2]: Serializes bytes without a length because the length is known. Byte arrays of the wrong length cause exceptions.

[^3]: From `System.Collections.Immutable`.

[^4]: `CanSerialize` became an extension method in version 2.0 to fix a stack overflow bug with self-referential data.
      In previous versions it was a direct method of `ITypeTraits<T>`.
