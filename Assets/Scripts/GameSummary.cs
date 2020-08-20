using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

public class PublishedGameSummary
{
    public string GameID { get; private set; }
    public string Title { get; private set; }

    const string GameIDKey = "i";
    const string TitleKey = "t";

    public PublishedGameSummary(string gameID, string title)
    {
        GameID = gameID;
        Title = title;
    }

    public StringBuilder ToJson(bool includeID=false)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        if (includeID)
        {
            sb.Append("\"");
            sb.Append(GameIDKey);
            sb.Append("\":\"");
            sb.Append(GameID);
            sb.Append("\",");
        }

        sb.Append("\"");
        sb.Append(TitleKey);
        sb.Append("\":\"");
        sb.Append(Title);
        sb.Append("\"}");
        return sb;
    }
    public static PublishedGameSummary FromJson(string json)
    {
        JObject jObject = JObject.Parse(json);

        string gameID = jObject.Properties().First().Name;
        Debug.Log("id: " + gameID);
        string title = jObject[gameID][TitleKey].Value<string>();
        //Debug.Log("title: " + title);
        PublishedGameSummary gameSummary = new PublishedGameSummary(gameID, title);
        return gameSummary;
    }
}
