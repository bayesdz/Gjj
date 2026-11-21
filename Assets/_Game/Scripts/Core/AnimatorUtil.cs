using System.Collections.Generic;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Безопасная установка параметров аниматора: если параметра нет в контроллере,
    /// вызов просто игнорируется. Удобно, пока модели и контроллеры меняются.
    /// </summary>
    public static class AnimatorUtil
    {
        static readonly Dictionary<Animator, HashSet<int>> cache = new Dictionary<Animator, HashSet<int>>();

        static bool Has(Animator a, int hash)
        {
            if (a == null || a.runtimeAnimatorController == null || !a.isActiveAndEnabled) return false;
            if (!cache.TryGetValue(a, out var set))
            {
                set = new HashSet<int>();
                foreach (var p in a.parameters) set.Add(p.nameHash);
                cache[a] = set;
            }
            return set.Contains(hash);
        }

        public static void SetTrigger(Animator a, int hash) { if (Has(a, hash)) a.SetTrigger(hash); }
        public static void SetBool(Animator a, int hash, bool v) { if (Has(a, hash)) a.SetBool(hash, v); }
        public static void SetFloat(Animator a, int hash, float v) { if (Has(a, hash)) a.SetFloat(hash, v); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();
    }
}
