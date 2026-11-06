using UnityEngine;

public static class ObjectFactory
{
    public static System.Action<GameObject> Created;
    private static T Style<T>(T instance) where T : Component { Created?.Invoke(instance.gameObject); return instance; }
    public static T Instantiate<T>(T prefab) where T : Component
    {
        return Style((T) Object.Instantiate(prefab));
    }

    public static GameObject Instantiate(GameObject prefab)
    {
        var instance = (GameObject) Object.Instantiate(prefab); Created?.Invoke(instance); return instance;
    }

    public static T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        return Style((T) Object.Instantiate(prefab, position, rotation));
    }
}
