namespace System.Runtime.CompilerServices
{
    /// Нужен компилятору для ограничения `where T : unmanaged` (в наборе interop-ссылок отсутствует).
    [AttributeUsage(AttributeTargets.All)]
    internal sealed class IsUnmanagedAttribute : Attribute { }
}
