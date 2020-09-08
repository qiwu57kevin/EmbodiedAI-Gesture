using Random = System.Random;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

public class CaptureImages : MonoBehaviour
{
    public Transform posReceptacles;
    public Transform roomRootT;

    public Object objPrefab;
    public NavObj.ObjCategory objCat;

    private List<NavObj> navObjects = new List<NavObj>();

    public GameObject targetCamera;
    private Transform currentCamera;

    [Tooltip("Where you want to save your images")]
    public string trainImageSavePath;
    public string testImageSavePath;

    
    int[] trainAngles = new int[2];
    int[] testAngles = new int[2];

    void Start()
    {
        Random rnd = new Random();
        int[] anglesList = Enumerable.Range(0,4).ToArray();
        anglesList = anglesList.ToList().OrderBy(x => rnd.Next()).ToArray();
        Array.Copy(anglesList,0,trainAngles,0,2);
        Array.Copy(anglesList,2,testAngles,0,2);

        foreach(string objCat in Enum.GetNames(typeof(NavObj.ObjCategory)))
        {
            foreach(string filePath in Directory.GetFiles(Application.dataPath+"/Resources/NavObj/"+objCat+"/","*.prefab"))
            {
                string shortPath = filePath.Replace(Application.dataPath, "Assets");
                Object objLoaded = AssetDatabase.LoadAssetAtPath(shortPath, typeof(GameObject));
                // Add the loaded object prefab to the list
                NavObj navObj = new NavObj(objLoaded, Enum.GetNames(typeof(NavObj.ObjCategory)).ToList().IndexOf(objCat));
                navObjects.Add(navObj);
            }
        }
    }

    public void GenerateTargetImages()
    {
        for(int i=0;i<navObjects.Count;i++)
        {
            CaptureNavObjImage(navObjects[i]);
        }
    }

    public void GenerateOneImage()
    {
        NavObj navObj = new NavObj(objPrefab, Enum.GetNames(typeof(NavObj.ObjCategory)).ToList().IndexOf(objCat.ToString()));
        CaptureNavObjImage(navObj);
    }

    public void GenerateEnvImages()
    {
        // Initialized camera used for image capturing
        GameObject cameraObj = Instantiate(targetCamera, Vector3.zero, Quaternion.identity) as GameObject;

        // Lay camera in 3 height: 0.6, 1.2, and 1.8
        // Lay camera in 60 angels as before
        for(int k=0;k<7;k++)
        {
            for(int m=0;m<7;m++)
            {
                roomRootT.GetChild(m).gameObject.SetActive(m==k? true:false);
            }

            for(int i=1;i<4;i++)
            {
                cameraObj.transform.position = new Vector3(0f, 0.6f*i, 0f);

                for(int j=0;j<30;j++)
                {
                    cameraObj.transform.localRotation = Quaternion.Euler(0f, -12f*(j+1), 0f);

                    if(j<15)
                    {
                        string path = $"{trainImageSavePath}env"; // file save path
                        Directory.CreateDirectory(path);
                        CaptureCameraView(cameraObj.transform.GetComponent<Camera>(), path + $"/env_{k}_{i}_{j}.png");
                    }
                    else
                    {
                        string path = $"{testImageSavePath}env"; // file save path
                        Directory.CreateDirectory(path);
                        CaptureCameraView(cameraObj.transform.GetComponent<Camera>(), path + $"/env_{k}_{i}_{j}.png");
                    }
                }
            }
        }

        Destroy(cameraObj);
    }

    private void CaptureNavObjImage(NavObj navObj)
    {
        // Initiate the gameobject
        Transform locReceptable = GameObject.Find($"PosReceptacles/{navObj.objCat.ToString()}").transform;
        GameObject objInst = GameObject.Instantiate(navObj.objInstance,locReceptable) as GameObject;
        objInst.transform.localPosition = Vector3.zero;
        objInst.transform.localRotation = Quaternion.identity;

        // Initialized camera used for image capturing
        GameObject cameraObj = Instantiate(targetCamera, locReceptable) as GameObject;
        currentCamera = cameraObj.transform;

        // Select location
        for(int h=0;h<locReceptable.childCount;h++)
        {
            objInst.transform.SetParent(locReceptable.GetChild(h), false);
            cameraObj.transform.SetParent(locReceptable.GetChild(h), false);
            objInst.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f,180f),0f);
            
            // Get current object bounds
            Bounds targetObjBounds = new Bounds(Vector3.zero, Vector3.zero);
            // Debug.Log(targetObjBounds.extents);
            // Find target object Bounds
            foreach(Collider col in objInst.GetComponentsInChildren<Collider>())
            {
                // Debug.Log(col.bounds.extents);
                targetObjBounds.Encapsulate(col.bounds);
            }

            // Debug.Log(targetObjBounds.extents);

            // Select room
            for(int k=0;k<7;k++)
            {
                for(int m=0;m<7;m++)
                {
                    roomRootT.GetChild(m).gameObject.SetActive(m==k? true:false);
                }

                // Select height
                for(int i=0;i<1;i++)
                {
                    currentCamera.position = targetObjBounds.center;

                    // Select angle
                    for(int j=0;j<4;j++)
                    {
                        currentCamera.localRotation = Quaternion.Euler(0f, -90f*(j+1), 0f);

                        float r = Mathf.Max(targetObjBounds.extents.x, targetObjBounds.extents.z);
                        currentCamera.localPosition = new Vector3(5f*r*Mathf.Cos(90f*j*Mathf.Deg2Rad), (i+1.25f)*targetObjBounds.extents.y, 5f*r*Mathf.Sin(90f*j*Mathf.Deg2Rad));
                        // currentCamera.Rotate(currentCamera.right, 10f*i);

                        if(trainAngles.Contains(j))
                        {
                            Directory.CreateDirectory($"{trainImageSavePath}{navObj.objType.ToString()}");
                            string path = $"{trainImageSavePath}{navObj.objType.ToString()}/{navObj.objName}_{k}_{h}_{i}_{j}.png"; // fiel save path
                            CaptureCameraView(currentCamera.GetComponent<Camera>(), path);
                        }
                        else if(testAngles.Contains(j))
                        {
                            Directory.CreateDirectory($"{testImageSavePath}{navObj.objType.ToString()}");
                            string path = $"{testImageSavePath}{navObj.objType.ToString()}/{navObj.objName}_{k}_{h}_{i}_{j}.png"; // fiel save path
                            CaptureCameraView(currentCamera.GetComponent<Camera>(), path);
                        }
                    }
                }
            }
        }
        DestroyImmediate(objInst);
        DestroyImmediate(cameraObj);
    }  

    // Capture current camera view and write it into a png file
    private void CaptureCameraView(Camera m_camera, string path)
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture rt = new RenderTexture(150, 150, 24);
        m_camera.targetTexture = rt;
        RenderTexture.active = m_camera.targetTexture;
 
        m_camera.Render();
 
        Texture2D Image = new Texture2D(150, 150);
        Image.ReadPixels(new Rect(0, 0, m_camera.targetTexture.width, m_camera.targetTexture.height), 0, 0);
        Image.Apply();
        RenderTexture.active = currentRT;
 
        var Bytes = ImageConversion.EncodeToPNG(Image);
        DestroyImmediate(Image);
 
        File.WriteAllBytes(path, Bytes);
    }
}
