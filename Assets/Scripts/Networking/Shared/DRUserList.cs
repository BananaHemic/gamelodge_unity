using System.Collections;
using System.Collections.Generic;
using DarkRift;

/// <summary>
/// Just a simple wrapper around a list of users
/// </summary>
public class DRUserList : IDarkRiftSerializable
{
    public List<DRUser> Users { get; private set; }

    public DRUserList() { }
    public DRUserList(int capacity) {
        Users = new List<DRUser>(capacity);
    }
    public void AddUser(DRUser user)
    {
        if (Users == null)
            Users = new List<DRUser>();
        Users.Add(user);
    }
    public static DRUserList DeserializeWithVersion(DarkRiftReader reader, int version, DRUserList existing = null)
    {
        if (existing == null)
            existing = new DRUserList();

        int numUsers = reader.DecodeInt32();
        if (existing.Users == null)
            existing.Users = new List<DRUser>(numUsers);
        for (int i = 0; i < numUsers; i++)
            existing.Users.Add(DRUser.DeserializeWithVersion(reader, version));

        return existing;
    }
    public void Deserialize(DeserializeEvent e)
    {
        DeserializeWithVersion(e.Reader, DRGameState.ApplicationVersion, this);
    }
    public void Serialize(SerializeEvent e)
    {
        if(Users == null)
        {
            e.Writer.EncodeInt32(0);
            return;
        }

        e.Writer.EncodeInt32(Users.Count);
        for (int i = 0; i < Users.Count; i++)
            e.Writer.Write(Users[i]);
    }
}
