using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
public class RunYOLO : MonoBehaviour
{
    public ModelAsset modelAsset;

    public TextAsset labelsAsset;

    public Sprite borderSprite;

    public Texture2D borderTexture;

    public Font font;

    private Model model;

    private IWorker engine;

    private string[] labels;

    private RenderTexture targetRT;

    private const int imageWidth = 640;

    private const int imageHeight = 640;

    private List<GameObject> boxPool = new List<GameObject>();

    public float mindist = float.MaxValue;

    int minIndex = -1;

    Color boxColor = Color.red;

    public int Alarm = 1;

    private string filePath;

    private StreamWriter writer;

    public struct BoundingBox
    {
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public string label;
        public float confidence;
    }

    void Start()
    {
        Application.targetFrameRate = 60;

        labels = labelsAsset.text.Split('\n');

        model = ModelLoader.Load(modelAsset);

        targetRT = new RenderTexture(2560, 1600, 0);

        engine = WorkerFactory.CreateWorker(BackendType.GPUCompute, model);

        if (borderSprite == null)
        {
            borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, borderTexture.width, borderTexture.height), new Vector2(borderTexture.width / 2, borderTexture.height / 2));
        }
    }

    private void Update()
    {
        ExecuteML();

        /*if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }*/
    }

    public void ExecuteML()
    {
        ClearAnnotations();

        // Camera���� �ǽð� ȭ���� �ؽ�ó�� ��ȯ�Ͽ� �� �Է����� ���
        Camera mainCamera = Camera.main;

        if (mainCamera == null) return;

        mainCamera.targetTexture = targetRT;

        mainCamera.Render();

        mainCamera.targetTexture = null;

        // �ؽ�ó�� �� �Է����� ��ȯ
        using var input = TextureConverter.ToTensor(targetRT, imageWidth, imageHeight, 3);

        engine.Execute(input);

        var output = engine.PeekOutput() as TensorFloat;

        output.CompleteOperationsAndDownload();

        float displayWidth = Screen.width;

        float displayHeight = Screen.height;

        float scaleX = displayWidth / imageWidth;

        float scaleY = displayHeight / imageHeight;

        int foundBoxes = output.shape[0];

        for (int n = 0; n < foundBoxes; n++)
        {
            var box = new BoundingBox
            {
                centerX = ((output[n, 1] + output[n, 3]) * 0.5f * scaleX),

                centerY = displayHeight + ((output[n, 2] + output[n, 4]) * 0.5f * -scaleY),

                width = (output[n, 3] - output[n, 1]) * scaleX * 1.5f,

                height = (output[n, 4] - output[n, 2]) * scaleY * 1.5f,

                label = labels[(int)output[n, 5]],

                confidence = Mathf.FloorToInt(output[n, 6] * 100 + 0.5f)
            };
            DrawBox(box, n);
        }
    }

    public void DrawBox(BoundingBox box, int id)
    {
        GameObject panel;

        GameObject[] person = GameObject.FindGameObjectsWithTag("Person");

        if (person.Length > 0)
        {
            float[] distance = new float[person.Length];

            for (int i = 0; i < person.Length; i++)
            {
                distance[i] = Vector3.Distance(this.gameObject.transform.position, person[i].transform.position);

                mindist = Mathf.Min(mindist, distance[i]);
            }
            print(mindist);
        }

        if (id < boxPool.Count)
        {
            panel = boxPool[id];

            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(boxColor);
        }

        panel.transform.position = new Vector3(box.centerX, box.centerY);

        RectTransform rt = panel.GetComponent<RectTransform>();

        rt.sizeDelta = new Vector2(box.width, box.height);

        var label = panel.GetComponentInChildren<Text>();

        label.text = box.label + " (" + box.confidence + "%)";
    }

    public GameObject CreateNewBox(Color color)
    {
        var panel = new GameObject("ObjectBox");

        panel.AddComponent<CanvasRenderer>();

        Image img = panel.AddComponent<Image>();

        img.color = color;

        img.sprite = borderSprite;

        img.type = Image.Type.Sliced;

        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas != null)
        {
            panel.transform.SetParent(canvas.transform, false);
        }
        else
        {
            Debug.LogError("Canvas�� ���� �����ϴ�. Canvas�� �߰��� �ּ���.");

            return null;
        }

        
        var text = new GameObject("ObjectLabel");

        text.AddComponent<CanvasRenderer>();

        text.transform.SetParent(panel.transform, false);

        Text txt = text.AddComponent<Text>();

        txt.font = font;

        txt.color = color;

        txt.fontSize = 40;

        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform rt2 = text.GetComponent<RectTransform>();

        rt2.offsetMin = new Vector2(20, 0);

        rt2.offsetMax = new Vector2(-20, 30);

        rt2.anchorMin = new Vector2(0, 0);

        rt2.anchorMax = new Vector2(1, 1);

        boxPool.Add(panel);

        return panel;
    }

    public void ClearAnnotations()
    {
        foreach (var box in boxPool)
        {
            box.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        engine?.Dispose();
    }
    
}