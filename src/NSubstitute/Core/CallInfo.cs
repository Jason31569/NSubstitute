using System.Diagnostics.CodeAnalysis;
using NSubstitute.Exceptions;

namespace NSubstitute.Core;

public class CallInfo(Argument[] callArguments, Type[] genericArguments)
{

    /// <summary>
    /// Gets the nth argument to this call.
    /// </summary>
    /// <param name="index">Index of argument</param>
    /// <returns>The value of the argument at the given index</returns>
    public object? this[int index]
    {
        get => callArguments[index].Value;
        set
        {
            var argument = callArguments[index];
            EnsureArgIsSettable(argument, index, value);
            argument.Value = value;
        }
    }

    private void EnsureArgIsSettable(Argument argument, int index, object? value)
    {
        if (!argument.IsByRef)
        {
            throw new ArgumentIsNotOutOrRefException(index, argument.DeclaredType);
        }

        if (value != null && !argument.CanSetValueWithInstanceOf(value.GetType()))
        {
            throw new ArgumentSetWithIncompatibleValueException(index, argument.DeclaredType, value.GetType());
        }
    }

    /// <summary>
    /// Gets the generic type arguments in case of a generic method call or an empty array otherwise.
    /// </summary>
    /// <returns>Array of types of the generic type arguments for this method call</returns>
    public Type[] GenericArgs() => genericArguments;

    /// <summary>
    /// Get the arguments passed to this call.
    /// </summary>
    /// <returns>Array of all arguments passed to this call</returns>
    public object?[] Args() => callArguments.Select(x => x.Value).ToArray();

    /// <summary>
    /// Gets the types of all the arguments passed to this call.
    /// </summary>
    /// <returns>Array of types of all arguments passed to this call</returns>
    public Type[] ArgTypes() => callArguments.Select(x => x.DeclaredType).ToArray();

    /// <summary>
    /// Gets the argument of type `T` passed to this call. This will throw if there are no arguments
    /// of this type, or if there is more than one matching argument.
    /// </summary>
    /// <typeparam name="T">The type of the argument to retrieve</typeparam>
    /// <returns>The argument passed to the call, or throws if there is not exactly one argument of this type</returns>
    public T? Arg<T>()
    {
        T? arg;
        if (TryGetArg(x => x.IsDeclaredTypeEqualToOrByRefVersionOf(typeof(T)), out arg)) return arg;
        if (TryGetArg(x => x.IsValueAssignableTo(typeof(T)), out arg)) return arg;
        throw new ArgumentNotFoundException("Can not find an argument of type " + typeof(T).FullName + " to this call.");
    }

    private bool TryGetArg<T>(Func<Argument, bool> condition, [MaybeNullWhen(false)] out T? value)
    {
        value = default;

        var matchingArgs = callArguments.Where(condition);
        if (!matchingArgs.Any()) return false;
        ThrowIfMoreThanOne<T>(matchingArgs);

        value = (T?)matchingArgs.First().Value!;
        return true;
    }

    private void ThrowIfMoreThanOne<T>(IEnumerable<Argument> arguments)
    {
        if (arguments.Skip(1).Any())
        {
            throw new AmbiguousArgumentsException(
                "There is more than one argument of type " + typeof(T).FullName + " to this call.\n" +
                "The call signature is (" + DisplayTypes(ArgTypes()) + ")\n" +
                "  and was called with (" + DisplayTypes(callArguments.Select(x => x.ActualType)) + ")"
                );
        }
    }

    /// <summary>
    /// Gets the argument passed to this call at the specified zero-based position, converted to type `T`.
    /// This will throw if there are no arguments, if the argument is out of range or if it
    /// cannot be converted to the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the argument to retrieve</typeparam>
    /// <param name="position">The zero-based position of the argument to retrieve</param>
    /// <returns>The argument passed to the call, or throws if there is not exactly one argument of this type</returns>
    public T ArgAt<T>(int position)
    {
        if (position >= callArguments.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position), $"There is no argument at position {position}");
        }

        try
        {
            return (T)callArguments[position].Value!;
        }
        catch (InvalidCastException)
        {
            throw new InvalidCastException(
                $"Couldn't convert parameter at position {position} to type {typeof(T).FullName}");
        }
    }

    private static string DisplayTypes(IEnumerable<Type> types) =>
        string.Join(", ", types.Select(x => x.Name).ToArray());
}
