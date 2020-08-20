using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using UnityEngine.Networking;

public enum ModelPermission
{
    Open,
    OpenWithAttribution,
    Private
}

public class BundleMetaData
{
    public string ID;
    public string Name;
    public string Description;
    public ModelPermission Permission;
    public int NumLikes;
    public int NumDislikes;
    public bool SexualContent;
    public bool GorePresent;
    public string Credit;
    public string CreatedDate;
    public string Tags;

    const string IDKey = "id";
    const string NameKey = "name";
    const string DescriptionKey = "description";
    const string PermissionKey = "permission";
    const string LikesKey = "likes";
    const string DislikesKey = "dislikes";
    const string SexualContentKey = "sexual_content";
    const string GoreKey = "gore";
    const string CreditKey = "credit";
    const string TagsKey = "tags";
    const string CreatedDateKey = "created";
    //const string ModelUrlKey = "u";

    public BundleMetaData(string id, string friendlyName, string description, ModelPermission permission,
        int numLikes, int numDislikes, bool sexual_content, bool gore, string credit, string tags, string createdDate)
    {
        ID = id;
        Name = friendlyName;
        Description = description;
        Permission = permission;
        NumLikes = numLikes;
        NumDislikes = numDislikes;
        SexualContent = sexual_content;
        GorePresent = gore;
        Credit = credit;
        Tags = tags;
        CreatedDate = createdDate;
    }
    public void ToJson(StringBuilder sb, bool includeSquiglyBrackets)
    {
        if (includeSquiglyBrackets)
            sb.Append("{");

        // I know this is super ugly, and that there are easier / more robust ways to
        // do this. It's just that in the past I've been burned using those methods
        // so I just do the stupid way now
        sb.Append("\"");
        sb.Append(IDKey);
        sb.Append("\":\"");
        sb.Append(ID);
        sb.Append("\",\"");
        sb.Append(NameKey);
        sb.Append("\":");
        sb.Append(JsonConvert.ToString(Name));
        sb.Append(",\"");
        sb.Append(DescriptionKey);
        sb.Append("\":");
        sb.Append(JsonConvert.ToString(Description));
        sb.Append(",\"");
        sb.Append(PermissionKey);
        sb.Append("\":");
        sb.Append((int)Permission);
        sb.Append(",\"");
        sb.Append(LikesKey);
        sb.Append("\":");
        sb.Append(NumLikes);
        sb.Append(",\"");
        sb.Append(DislikesKey);
        sb.Append("\":");
        sb.Append(NumDislikes);
        sb.Append(",\"");
        sb.Append(SexualContentKey);
        sb.Append("\":");
        sb.Append(SexualContent ? 1 : 0);
        sb.Append(",\"");
        sb.Append(GoreKey);
        sb.Append("\":");
        sb.Append(GorePresent ? 1 : 0);
        sb.Append(",\"");
        sb.Append(CreditKey);
        sb.Append("\":");
        sb.Append(JsonConvert.ToString(Credit));
        sb.Append(",\"");
        sb.Append(TagsKey);
        sb.Append("\":");
        sb.Append(JsonConvert.ToString(Tags));
        sb.Append(",\"");
        sb.Append(CreatedDateKey);
        sb.Append("\":");
        sb.Append(JsonConvert.ToString(CreatedDate));


        if (includeSquiglyBrackets)
            sb.Append("}");
    }
    public void ApplyToForm(List<IMultipartFormSection> sections)
    {
        sections.Add(new MultipartFormDataSection(IDKey, ID));
        // TODO we really should json encode the name... This needs server support
        //sections.Add(new MultipartFormDataSection(NameKey, JsonConvert.ToString(Name)));
        sections.Add(new MultipartFormDataSection(NameKey, Name));
        string desc = JsonConvert.ToString(Description);
        desc = desc.Substring(1, desc.Length - 2);// TODO
        sections.Add(new MultipartFormDataSection(DescriptionKey, desc));
        sections.Add(new MultipartFormDataSection(PermissionKey,  ((int)Permission).ToString()));
        sections.Add(new MultipartFormDataSection(LikesKey, NumLikes.ToString()));
        sections.Add(new MultipartFormDataSection(DislikesKey, NumDislikes.ToString()));
        sections.Add(new MultipartFormDataSection(SexualContentKey, (SexualContent ? 1 : 0).ToString()));
        sections.Add(new MultipartFormDataSection(GoreKey, (GorePresent ? 1 : 0).ToString()));
        if (!string.IsNullOrEmpty(Credit))
            sections.Add(new MultipartFormDataSection(CreditKey, Credit));
            //sections.Add(new MultipartFormDataSection(CreditKey, JsonConvert.ToString(Credit)));
        if (!string.IsNullOrEmpty(Tags))
            sections.Add(new MultipartFormDataSection(TagsKey, Tags));
            //sections.Add(new MultipartFormDataSection(TagsKey, JsonConvert.ToString(Tags)));
        sections.Add(new MultipartFormDataSection(CreatedDateKey, CreatedDate));
    }
    public void ApplyToForm(WWWForm form)
    {
        form.AddField(IDKey, ID);
        form.AddField(NameKey, Name);
        form.AddField(DescriptionKey, Description);
        form.AddField(PermissionKey, (int)Permission);
        form.AddField(LikesKey, NumLikes);
        form.AddField(DislikesKey, NumDislikes);
        form.AddField(SexualContentKey, SexualContent ? 1 : 0);
        form.AddField(GoreKey, GorePresent ? 1 : 0);
        form.AddField(CreditKey, Credit);
        form.AddField(TagsKey, Tags);
        form.AddField(CreatedDateKey, CreatedDate);
    }
    public static BundleMetaData FromJson(JObject json)
    {
        BundleMetaData metaData = new BundleMetaData(
            json.Value<string>(IDKey),
            json.Value<string>(NameKey),
            json.Value<string>(DescriptionKey),
            (ModelPermission)json.Value<int>(PermissionKey),
            json.Value<int>(LikesKey),
            json.Value<int>(DislikesKey),
            json.Value<int>(SexualContentKey) == 1,
            json.Value<int>(GoreKey) == 1,
            json.Value<string>(CreditKey),
            json.Value<string>(TagsKey),
            json.Value<string>(CreatedDateKey)
            );
        return metaData;
    }
}
