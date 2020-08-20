using UnityEngine;
using UnityEngine.UI;

namespace Crosstales.FB.Demo
{
    /// <summary>Examples for all methods.</summary>
    [HelpURL("https://www.crosstales.com/media/data/assets/FileBrowser/api/class_crosstales_1_1_f_b_1_1_demo_1_1_examples.html")]
    public class Examples : MonoBehaviour
    {
        #region Variables

        public GameObject TextPrefab;

        public GameObject ScrollView;

        public Button OpenFilesBtn;
        public Button OpenFoldersBtn;

        public Text Error;

        //private string testPath = @"D:\slaubenberger\git";
        //private string testPath = @"C:\tmp";
        //private string testPath = null;
        //private string testPath = @"C:";

        #endregion


        #region MonoBehaviour methods

        public void Start()
        {
            //Util.Config.DEBUG = true;

            if (OpenFilesBtn != null)
                OpenFilesBtn.interactable = FileBrowser.canOpenMultipleFiles;

            if (OpenFoldersBtn != null)
                OpenFoldersBtn.interactable = FileBrowser.canOpenMultipleFolders;

            //Debug.Log("GetDirectories: " + FileBrowser.GetDirectories(testPath, true).CTDump());
            //Debug.Log("GetFiles: " + FileBrowser.GetFiles(testPath, true, "*").CTDump());
        }

        #endregion


        #region Public methods

        public void OpenSingleFile()
        {
            //string path = FileBrowser.OpenSingleFile("Open single file", testPath, new ExtensionFilter("Image Files", "png", "jpg", "jpeg"), new ExtensionFilter("Sound Files", "mp3", "wav"), new ExtensionFilter(Util.Constants.TEXT_ALL_FILES, "*"));
            //string path = FileBrowser.OpenSingleFile("Open single file", testPath, "txt", "jpg", "pdf");
            //string path = FileBrowser.OpenSingleFile("txt");
            string path = FileBrowser.OpenSingleFile();

            rebuildList(path);
        }

        public void OpenFiles()
        {
            //string[] paths = FileBrowser.OpenFiles("Open files", testPath, new ExtensionFilter("Image Files", "png", "jpg", "jpeg"), new ExtensionFilter("Sound Files", "mp3", "wav"), new ExtensionFilter(Util.Constants.TEXT_ALL_FILES, "*"));
            //string[] paths = FileBrowser.OpenFiles("txt", "jpg", "pdf");
            //string[] paths = FileBrowser.OpenFiles("txt");
            string[] paths = FileBrowser.OpenFiles();

            rebuildList(paths);
        }

        public void OpenSingleFolder()
        {
            //string path = FileBrowser.OpenSingleFolder("Open folder", testPath);
            string path = FileBrowser.OpenSingleFolder();

            rebuildList(path);
        }

        public void OpenFolders()
        {
            //string[] paths = FileBrowser.OpenFolders("Open folders", testPath);
            string[] paths = FileBrowser.OpenFolders();

            rebuildList(paths);
        }

        public void SaveFile()
        {
            //string path = FileBrowser.SaveFile("Save file", testPath, "MySaveFile", new ExtensionFilter("Binary", "bin"), new ExtensionFilter("Text", "txt", "md"), new ExtensionFilter("C#", "cs"));
            //string path = FileBrowser.SaveFile("Save file", testPath, "MySaveFile", "bin", "txt", "cs");
            //string path = FileBrowser.SaveFile("MySaveFile", "txt");
            string path = FileBrowser.SaveFile();

            rebuildList(path);
        }

        public void OpenFilesAsync()
        {
            //FileBrowser.OpenFilesAsync((string[] paths) => { writePaths(paths); }, "Open files", testPath, true, new ExtensionFilter("Image Files", "png", "jpg", "jpeg"), new ExtensionFilter("Sound Files", "mp3", "wav"), new ExtensionFilter(Util.Constants.TEXT_ALL_FILES, "*"));
            //FileBrowser.OpenFilesAsync((string[] paths) => { writePaths(paths); }, "Open files", testPath, true, "txt", "png");
            //FileBrowser.OpenFilesAsync((string[] paths) => { writePaths(paths); }, true, "txt");
            FileBrowser.OpenFilesAsync((string[] paths) => { writePaths(paths); }, true);

            //new System.Threading.Thread(() => FileBrowser.OpenFilesAsync((string[] paths) => { writePaths(paths); }, true, "txt")).Start();

        }

        public void OpenFoldersAsync()
        {
            //FileBrowser.OpenFoldersAsync((string[] paths) => { writePaths(paths); }, "Open folders", testPath, true);
            FileBrowser.OpenFoldersAsync((string[] paths) => { writePaths(paths); });

            //new System.Threading.Thread(() => FileBrowser.OpenFoldersAsync((string[] paths) => { writePaths(paths); })).Start();
        }

        public void SaveFileAsync()
        {
            //FileBrowser.SaveFileAsync((string paths) => { writePaths(paths); }, "Save File", testPath, "MySaveFile", new ExtensionFilter("Binary", "bin"), new ExtensionFilter("Text", "txt"), new ExtensionFilter("C#", "cs"));
            //FileBrowser.SaveFileAsync((string paths) => { writePaths(paths); }, "Save file", testPath, "MySaveFile", "txt", "cs");
            FileBrowser.SaveFileAsync((string paths) => { writePaths(paths); }, "MySaveFile", "txt");

            //new System.Threading.Thread(() => FileBrowser.SaveFileAsync((string paths) => { writePaths(paths); }, "MySaveFile", "txt")).Start();
        }

        private void writePaths(params string[] paths)
        {
            rebuildList(paths);
        }

        #endregion


        #region Private methods

        private void rebuildList(params string[] e)
        {
            for (int ii = ScrollView.transform.childCount - 1; ii >= 0; ii--)
            {
                Transform child = ScrollView.transform.GetChild(ii);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            ScrollView.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80 * e.Length);

            for (int ii = 0; ii < e.Length; ii++)
            {
                GameObject go = Instantiate(TextPrefab);

                go.transform.SetParent(ScrollView.transform);
                go.transform.localScale = Vector3.one;
                go.transform.localPosition = new Vector3(10, -80 * ii, 0);
                go.GetComponent<Text>().text = e[ii].ToString();
            }
        }

        #endregion
    }
}
// © 2017-2019 crosstales LLC (https://www.crosstales.com)