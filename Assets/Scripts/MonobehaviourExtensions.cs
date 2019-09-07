using UnityEngine;
public static class MonobehaviourExtensions
{
    public static void EnsureCoroutineStopped(this MonoBehaviour value, ref Coroutine routine)
    {
        if (routine != null)
        {
            value.StopCoroutine(routine);
            routine = null;
        }
    }
}
