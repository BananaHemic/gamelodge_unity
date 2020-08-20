using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class BundleDatabase : IDisposable
{
    const string DatabaseFileName = "bundles.txt";
    private readonly StreamWriter _writer;
    private readonly FileStream _fileStream;
    private readonly List<Bundle> _allBundles = new List<Bundle>();
    private readonly Dictionary<string, Bundle> _bundleDict = new Dictionary<string, Bundle>();
    private bool _disposed = false;

    public BundleDatabase(string dbLocationFormat)
    {
        // Read the existing database
        string path = string.Format(dbLocationFormat, DatabaseFileName);

        bool dbExists = File.Exists(path);
        if (!dbExists)
            Debug.Log("No existing db, will build one at: " + path);
        //else
            //Debug.Log("Loading DB is at: " + path);
        _fileStream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        //Debug.Log(_fileStream.Position);
        if (dbExists)
        {
            //Debug.Log("can write: " + _fileStream.CanWrite);
            // We don't Dispose reader, because we want to keep the underlying stream open
            StreamReader reader = new StreamReader(_fileStream);
            using (JsonReader jsonReader = new JsonTextReader(reader))
            {
                JsonSerializer serializer = new JsonSerializer();
                jsonReader.CloseInput = false;
                jsonReader.SupportMultipleContent = true;
                while (jsonReader.Read())
                {
                    JObject json = serializer.Deserialize<JObject>(jsonReader);
                    //Debug.Log("read name: " + json.Value<string>("name"));
                    Bundle modelBundle = Bundle.FromJson(json);
                    _allBundles.Add(modelBundle);
                    _bundleDict.Add(modelBundle.ID, modelBundle);
                }
            }
            //using (StreamReader reader = new StreamReader(_fileStream))
            //{
            //    while(reader.Peek() > 0)
            //    {
            //        string modelStr = reader.ReadLine();
            //        //Debug.Log(modelStr);
            //        JObject json = JObject.Parse(modelStr);
            //        Debug.Log("read name: " + json.Value<string>("name"));
            //        Bundle modelBundle = Bundle.FromJson(json);
            //        _allBundles.Add(modelBundle);
            //        _bundleDict.Add(modelBundle.ID, modelBundle);
            //    }
            //}
            //Debug.Log("can write: " + _fileStream.CanWrite);
        }
        _writer = new StreamWriter(_fileStream);
    }
    public void AddModelToDatabase(Bundle modelBundle, string modelBundleJsonStr=null)
    {
        _allBundles.Add(modelBundle);
        _bundleDict.Add(modelBundle.ID, modelBundle);
        if (modelBundleJsonStr == null)
            modelBundleJsonStr = modelBundle.ToJson(true).ToString();
        Debug.Log("Adding to db: " + modelBundleJsonStr);
        if (modelBundleJsonStr.Contains("\n"))
            Debug.LogError("Model in database contains a newline!!! ID: " + modelBundle.ID);
        //_writer.WriteLineAsync(modelDataJsonStr);
        _writer.WriteLine(modelBundleJsonStr);
        _writer.Flush();
    }

    public bool Contains(string bundleID)
    {
        return _bundleDict.ContainsKey(bundleID);
    }

    public bool TryGetBundle(string bundleID, out Bundle modelBundle)
    {
        return _bundleDict.TryGetValue(bundleID, out modelBundle);
    }
    public List<Bundle> GetAllBundles()
    {
        return _allBundles;
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _writer.Dispose();
    }

    public Bundle GetBundle(string bundleID)
    {
        return _bundleDict[bundleID];
    }
}
