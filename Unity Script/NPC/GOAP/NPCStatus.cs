// https://github.com/gotzawal/GOALLM_v7

using System;

[System.Serializable]
public class NPCStatus
{
    public string Location;
    public string Inventory;
    public string Pose;
    public string Holding;

    public NPCStatus(
        string location,
        string inventory,
        string pose,
        string holding
    )
    {
        Location = location;
        Inventory = inventory;
        Pose = pose;
        Holding = holding;
    }
}
