using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace MythicAscension.Util
{
    /// Прямые вызовы il2cpp-методов.
    /// Нужны для «небиттабельных» структур (ItemStat, RarityTooltip): обёртка Il2CppInterop при передаче их
    /// в generic-методы (List<T>.Add, Dictionary<K,V>.Add) отдаёт указатель на упакованный объект с заголовком,
    /// из-за чего поля сдвигаются на 16 байт. Здесь мы передаём распакованный указатель на данные.
    public static unsafe class Il2CppRaw
    {
        /// Указатель на данные значимого типа внутри обёртки (без заголовка объекта).
        public static IntPtr Unbox(Il2CppObjectBase boxed) => IL2CPP.il2cpp_object_unbox(boxed.Pointer);

        public static void Invoke(Il2CppObjectBase target, string method, params IntPtr[] args)
        {
            IntPtr obj = target.Pointer;
            IntPtr klass = IL2CPP.il2cpp_object_get_class(obj);
            IntPtr m = IL2CPP.il2cpp_class_get_method_from_name(klass, method, args.Length);
            if (m == IntPtr.Zero) throw new MissingMethodException($"il2cpp method {method}/{args.Length} not found");

            IntPtr* argv = stackalloc IntPtr[Math.Max(1, args.Length)];
            for (int i = 0; i < args.Length; i++) argv[i] = args[i];
            IntPtr exc = IntPtr.Zero;
            IL2CPP.il2cpp_runtime_invoke(m, obj, (void**)argv, ref exc);
            if (exc != IntPtr.Zero)
                throw new Exception($"il2cpp exception in {method}: " + new Il2CppSystem.Object(exc).ToString());
        }

        /// list.Add(item) для List<T>, где T — небиттабельная структура-обёртка.
        public static void ListAddStruct(Il2CppObjectBase list, Il2CppObjectBase boxedItem)
            => Invoke(list, "Add", Unbox(boxedItem));

        /// dict.Add(intEnumKey, value) для Dictionary<Enum, T>, где T — небиттабельная структура-обёртка.
        public static void DictAddEnumStruct(Il2CppObjectBase dict, int key, Il2CppObjectBase boxedValue)
        {
            int k = key;
            Invoke(dict, "Add", (IntPtr)(&k), Unbox(boxedValue));
        }

        /// dict.Add(intEnumKey, value) для Dictionary<Enum, T>, где T — обычная (C#) структура.
        public static void DictAddEnumValue<T>(Il2CppObjectBase dict, int key, T value) where T : unmanaged
        {
            int k = key;
            T v = value;
            Invoke(dict, "Add", (IntPtr)(&k), (IntPtr)(&v));
        }

        /// list.Add(value) для List<T>, где T — обычная (C#) структура.
        public static void ListAddValue<T>(Il2CppObjectBase list, T value) where T : unmanaged
        {
            T v = value;
            Invoke(list, "Add", (IntPtr)(&v));
        }
    }
}
