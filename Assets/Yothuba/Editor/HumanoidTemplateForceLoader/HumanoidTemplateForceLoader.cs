using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;


public class HumanoidTemplateForceLoader : EditorWindow
{
    [SerializeField] Object souceObject;
    [SerializeField] private List<Object> targetList = new List<Object>();
    
    [MenuItem("Tools/Yothuba/HumanoidTemplateForceLoader")]
    public static void ShowWindow()
    {
        HumanoidTemplateForceLoader wnd = GetWindow<HumanoidTemplateForceLoader>();
        wnd.titleContent = new GUIContent("HumanoidTemplateForceLoader");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;
        
        root.Add(new HelpBox("htファイルをロード済みなFBX又はその他形式のモデルを利用し、他のモデルにhtファイルを適用します(破壊的変更)",
            HelpBoxMessageType.Info));
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Yothuba/Editor/HumanoidTemplateForceLoader/HumanoidTemplateForceLoader.uxml");
        VisualElement uxml = visualTree.Instantiate();
        root.Add(uxml);
        
        var serializedObject = new SerializedObject(this);

        var souceObjectField = root.Q<PropertyField>("SouceObject");
        var souceObjectProperty = serializedObject.FindProperty("souceObject");
        souceObjectField.BindProperty(souceObjectProperty);
        
        var objectListField = root.Q<ListView>("TargetList");
        objectListField.RegisterCallback<DragPerformEvent>(AddToTargetList);
        objectListField.RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
        var targetListProperty = serializedObject.FindProperty("targetList");
        objectListField.BindProperty(targetListProperty);
        serializedObject.ApplyModifiedProperties();

        var helpBox = new HelpBox();
        helpBox.text = "metaファイルを直接書き換えており、実行後のUndoはできないため注意してください";
        helpBox.messageType = HelpBoxMessageType.Warning;
        
        var loadButton = new Button();
        loadButton.text = "Force Load";
        loadButton.style.color = Color.red;
        loadButton.clicked += ForceLoad;
        
        root.Add(helpBox);
        root.Add(loadButton);
    }

    private void AddToTargetList(DragPerformEvent e)
    {
        var ddData = DragAndDrop.objectReferences;
        foreach (var data in ddData)
        {
            targetList.Add(data);
        }
        DragAndDrop.AcceptDrag();
    }
    
    
    void OnDragUpdatedEvent(DragUpdatedEvent evt)
    {
        if (DragAndDrop.objectReferences.Length >= 1)//&& DragAndDrop.objectReferences[0] is VisualTreeAsset )
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            return;
        }
        DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
    }
    
    
    private string GetAbsoluteMetaPathFromAsset(Object asset)
    {
        var baseAssetPath = AssetDatabase.GetAssetPath(asset);
        var baseMetaPath = baseAssetPath + ".meta";
        baseMetaPath = baseMetaPath.Substring(new string("Assets").Length);
        var baseAbsoluteMetaPath = string.Concat(Application.dataPath, baseMetaPath);
        return baseAbsoluteMetaPath;
    }
    

    private void ForceLoad()
    {
        if (souceObject == null || targetList == null)
        {
            Debug.LogError("コピー元、又はコピー先オブジェクトが１つ以上選択されていません");
            return;
        }
        var baseAbsoluteMetaPath = GetAbsoluteMetaPathFromAsset(souceObject);
        var rewriteObjectAbsoluteMetaPaths = 
            targetList.Select(x => GetAbsoluteMetaPathFromAsset(x)).ToList();

        //コピー元データ処理
        string copyText;
        using (var fs = File.OpenRead(baseAbsoluteMetaPath))
        {
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                var allText = sr.ReadToEnd();
                copyText = allText.Remove(0, allText.IndexOf("human:"));
                copyText = copyText.Remove(copyText.IndexOf("skeleton:"));
            }
        }

        var pathListLength = rewriteObjectAbsoluteMetaPaths.Count;
        //コピー先データ処理
        foreach (var (path,loopIndex) in rewriteObjectAbsoluteMetaPaths.Select((value, index) =>(value,index)))
        {
            var progress = (float)(loopIndex+1.0f)/(float)(pathListLength);
            EditorUtility.DisplayProgressBar("コピー処理",path, progress);
            string allText;
            using (var fs = File.Open(path, FileMode.Open))
            {
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    allText = sr.ReadToEnd();
                    var index = allText.IndexOf("human:");
                    var lastIndex = allText.IndexOf("skeleton:");
                    allText = allText.Remove(index, lastIndex-index);
                    allText = allText.Insert(index, copyText);
                }
            }
            
            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                sw.Write(allText);
            }
        }
        EditorUtility.ClearProgressBar();
    }
}