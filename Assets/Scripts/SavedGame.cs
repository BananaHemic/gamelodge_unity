using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SavedGameMetadata
{
    public int ID { get; private set; }
    public string S3_ID { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string ImageID { get; private set; }
    public string Tags { get; private set; }
    public string Creators { get; private set; }
    public uint Version { get; private set; }
    public string Parents { get; private set; }

    const string ID_Key = "id";
    const string S3_Key = "key";
    const string Creators_Key = "creators";
    const string Title_Key = "name";
    const string Description_Key = "description";
    const string Tags_Key = "tags";
    const string Image_Key = "imgKey";
    const string Parents_Key = "parents";

    public SavedGameMetadata(JObject json)
    {
        ID = json.Value<int>(ID_Key);
        S3_ID = json.Value<string>(S3_Key);
        Title = json.Value<string>(Title_Key);
        Creators = json.Value<string>(Creators_Key);
        Description = json.Value<string>(Description_Key);
        Tags = json.Value<string>(Tags_Key);
        ImageID = json.Value<string>(Image_Key);
        Parents = json.Value<string>(Parents_Key);
    }
}
