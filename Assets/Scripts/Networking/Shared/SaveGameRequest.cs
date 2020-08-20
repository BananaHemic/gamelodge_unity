using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DarkRift;

/// <summary>
/// Message sent from client->server requesting that
/// the server save the current game state
/// </summary>
public class SaveGameRequest : IDarkRiftSerializable
{
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string ImageKey { get; private set; }
    public string Tags { get; private set; }

    public SaveGameRequest() { }
    public SaveGameRequest(string title, string description, string imageKey, string tags)
    {
        Title = title;
        Description = description;
        ImageKey = imageKey;
        Tags = tags;
    }
    public void Deserialize(DeserializeEvent e)
    {
        Title = e.Reader.ReadString();
        Description = e.Reader.ReadString();
        ImageKey = e.Reader.ReadString();
        Tags = e.Reader.ReadString();
    }
    public void Serialize(SerializeEvent e)
    {
        e.Writer.Write(Title);
        e.Writer.Write(Description);
        e.Writer.Write(ImageKey);
        e.Writer.Write(Tags);
    }
}
