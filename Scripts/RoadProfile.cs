using UnityEngine;

public enum RoadHierarchyLevel
{
    Alley,
    LocalStreet,
    MainRoad,
    Highway
}

[CreateAssetMenu(fileName = "RoadProfile", menuName = "City Builder/Road Profile")]
public class RoadProfile : ScriptableObject
{
    public string displayName = "New Road Profile";
    public RoadHierarchyLevel hierarchyLevel = RoadHierarchyLevel.LocalStreet;
    [Min(0.5f)] public float roadWidth = 3.0f;
    public Color debugColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Min(0f)] public float intersectionClearanceRadius = 2.5f;
    [Min(0f)] public float blockInset = 0f;
    [TextArea] public string description;

    public string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }
}