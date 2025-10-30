using System;
using System.Collections.Generic;
using UnityEngine;

public class CompositeKey
{
    private MemoryObject bruh;
    private int id;
}

[Serializable]
public class CreatureMemory {
    private readonly double MEMORY_DURATION_IN_SECONDS = 600;

    // MemoryObject category -> InstanceID -> MemoryDetails
    private Dictionary<MemoryObject, Dictionary<int, MemoryDetails>> memories;
    private Dictionary<ObjectName, MemoryObject> creatureInteractionMap;

    [SerializeField] public MemoryDetails SavedShelter;

    // Set target behavior
    [SerializeField] public MemoryDetails Target;
    private bool ActivelySearching;
    private MemoryObject searchObject;

    public CreatureMemory(ObjectName creatureType)
    {
        memories = new Dictionary<MemoryObject, Dictionary<int, MemoryDetails>>();
        creatureInteractionMap = CreatureInteractionTemplates.Templates[creatureType];
    }

    /// <summary>
    /// Adds or updates a memory from a seen collider.
    /// </summary>
    public void HandleMemory(Collider2D collider)
    {
        if (collider == null) return;

        // Decide what kind of memory this collider is
        MemoryObject memoryType = ClassifyMemoryObject(collider);

        // get/create memory dictionary for this type
        if (!memories.TryGetValue(memoryType, out var memoryDict))
        {
            memoryDict = new Dictionary<int, MemoryDetails>();
            memories[memoryType] = memoryDict;
        }

        int id = collider.GetInstanceID();
        double now = Time.timeAsDouble;
        Transform tr = collider.transform;

        if (memoryDict.TryGetValue(id, out var existing))
        {
            // Update existing memory
            existing.Update(tr.position, now);

            if (ActivelySearching) SetTarget(existing, memoryType);
        }
        else
        {
            // Add new memory
            memoryDict[id] = new MemoryDetails(tr, now, id);

            if (ActivelySearching) SetTarget(memoryDict[id], memoryType);
        }
        
        
        
        
    }

    private void SetTarget(MemoryDetails details, MemoryObject mo)
    {
        if (mo == searchObject)
        {
            Target = details;
            ActivelySearching = false;
        }
    }

    public void SetSearchBehavior(MemoryObject searchObject)
    {
        Target = null;
        this.searchObject = searchObject;
        ActivelySearching = true;
    }

    /// <summary>
    /// Gets the most recent memory of a given type, or null if none exist.
    /// </summary>
    public MemoryDetails GetLatest(MemoryObject type)
    {
        if (!memories.TryGetValue(type, out var memoryDict) || memoryDict.Count == 0)
            return null;

        MemoryDetails latest = null;
        foreach (var detail in memoryDict.Values)
        {
            if (latest == null || detail.TimeStamp > latest.TimeStamp)
            {
                latest = detail;
            }
        }
        return latest;
    }

    /// <summary>
    /// Gets all current memories of a type.
    /// </summary>
    public IEnumerable<MemoryDetails> GetAll(MemoryObject type)
    {
        if (memories.TryGetValue(type, out var memoryDict))
            return memoryDict.Values;
        return Array.Empty<MemoryDetails>();
    }

    /// <summary>
    /// Classifies the collider into a MemoryObject type.
    /// Replace this with your own logic (tags, layers, components).
    /// </summary>
    private MemoryObject ClassifyMemoryObject(Collider2D collider)
    {
        ObjectType objectType = collider.GetComponent<ObjectType>();
        if (objectType != null)
        {
            ObjectName objectName = objectType.GetName;
            if (creatureInteractionMap.TryGetValue(objectName, out MemoryObject value))
            {
                return value;
            }
        }
        return MemoryObject.Nothing; // default fallback
    }

    public void FlushOldMemories(double currentTime)
    {
        foreach (var memoryList in memories.Values)
        {
            // Collect old keys first to avoid modifying while iterating
            List<int> toRemove = new List<int>();

            foreach (var kvp in memoryList)
            {
                if (kvp.Value.TimeStamp + MEMORY_DURATION_IN_SECONDS < currentTime)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            // Remove expired memories
            foreach (var key in toRemove)
            {
                memoryList.Remove(key);
            }
        }
    }

    public bool TargetValid()
    {
        return Target != null && Target.InstanceID != 0;
    }

    public bool ShelterValid()
    {
        return SavedShelter != null && SavedShelter.InstanceID != 0;
    }
}

public enum MemoryObject {
    Shelter,            // A shelter object            
    Predator,           // A creature that wishes to hunt present creature
    DynamicCreature,    // Not prey or predator. Present creature must use brain and logic to determine safety around them.
    Food,               // An object determined as valid food
    DangerObject,       // Something that isn't a creature but is still dangerous.
    DynamicObject,      // Present creature must use brain and logic to determine safety around this non-creature obect.
    Nothing             // Default and shouldn't be used.
}

[Serializable]
public class MemoryDetails {
    [SerializeField] private Vector3 position;
    [SerializeField] private Transform transform;
    [SerializeField] private double timestamp;
    [SerializeField] private double instanceID;

    public static float RECENTLY_SEEN_TIME = 3f;

    public MemoryDetails(Transform transform, double timestamp, double instanceID)
    {
        position = transform.position;
        this.transform = transform;
        this.timestamp = timestamp;
        this.instanceID = instanceID;
    }

    public Vector3 Position => position;

    public Transform Transform => transform;

    public double TimeStamp => timestamp;

    public double InstanceID => instanceID;

    public void Update(Vector3 position, double time)
    {
        this.position = position;
        this.timestamp = time;
    }

    public bool RecentlySeen()
    {
        return Time.timeAsDouble - timestamp < RECENTLY_SEEN_TIME;
    }
}