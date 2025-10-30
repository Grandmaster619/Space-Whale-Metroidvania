using System.Collections.Generic;
using UnityEngine;

public class ObjectType : MonoBehaviour
{
    [SerializeField] private ObjectName Name;

    public ObjectName GetName => Name;
}

public enum ObjectName
{
    // None
    None,

    // Shelter
    Shelter,

    // Plants and Objects
    Apple,

    // Creatures
    Predator_1,
    Player
}

public enum ShelterType
{
    SmallShelter,
    MediumShelter,
    LargeShelter,
    GinormousShelter
}

public static class CreatureInteractionTemplates
{
    // Each creature type has a predefined interaction map
    public static readonly Dictionary<ObjectName, Dictionary<ObjectName, MemoryObject>> Templates =
        new()
        {
            {
                ObjectName.Player, new Dictionary<ObjectName, MemoryObject>
                {
                    { ObjectName.Shelter, MemoryObject.Shelter },
                    { ObjectName.Apple, MemoryObject.Food },
                    { ObjectName.Predator_1, MemoryObject.Predator },
                    { ObjectName.Player, MemoryObject.DynamicCreature }
                }
            },
            {
                ObjectName.Predator_1, new Dictionary<ObjectName, MemoryObject>
                {
                    { ObjectName.Shelter, MemoryObject.Shelter },
                    { ObjectName.Apple, MemoryObject.Food },
                    { ObjectName.Predator_1, MemoryObject.DynamicCreature },
                    { ObjectName.Player, MemoryObject.Food }
                }
            }
        };
}
