using Random = System.Random;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CaptureImages : MonoBehaviour
{
    public EnvSetup.targets[] capturedTargets;
    private Transform _target;

    public GameObject targetCamera;
    private Transform currentCamera;

    [Tooltip("Where you want to save your images")]
    public string trainImageSavePath;
    public string testImageSavePath;

    
    int[] trainAngles = new int[30];
    int[] testAngles = new int[30];

    void Start()
    {
        Random rnd = new Random();
        int[] anglesList = Enumerable.Range(0,60).ToArray();
        anglesList = anglesList.ToList().OrderBy(x => rnd.Next()).ToArray();
        Array.Copy(anglesList,0,trainAngles,0,30);
        Array.Copy(anglesList,30,testAngles,0,30);
    }

    public void GenerateTargetImages()
    {
        foreach(EnvSetup.targets target in capturedTargets)
        {
            _target = GameObject.Find($"Targets/{target.ToString()}").transform;
            foreach(Transform child in _target)
            {
                // Activate each child for the parent target
                child.gameObject.SetActive(true);

                // Initialized camera used for image capturing
                GameObject cameraObj = Instantiate(targetCamera, child) as GameObject;
                currentCamera = cameraObj.transform;
                Vector3 extent = Vector3.zero;
                if(child.GetComponent<Renderer>()!=null)
                {
                    extent = Vector3.Scale(child.GetComponent<Renderer>().bounds.extents, new Vector3(1f/child.localScale.x, 1f/child.localScale.y, 1f/child.localScale.z));
                }
                else if(child.GetComponent<Collider>()!=null)
                {
                    extent = Vector3.Scale(child.GetComponent<Collider>().bounds.extents, new Vector3(1f/child.localScale.x, 1f/child.localScale.y, 1f/child.localScale.z));
                }
                else
                {
                    Debug.LogWarning($"{target.ToString()}{child.GetSiblingIndex()} has no renderer or collider component!");
                }

                for(int i=0;i<3;i++)
                {
                    switch(target.ToString())
                    {
                        case "Cup": 
                            currentCamera.localPosition = new Vector3(0, 0, (1f+1f*i)*extent.z);
                            currentCamera.localRotation = Quaternion.Euler(90f, 0, 0);
                            currentCamera.Rotate(currentCamera.up, -90f);

                            for(int j=0;j<60;j++)
                            {
                                float r = Mathf.Max(extent.x, extent.y);
                                currentCamera.localPosition = new Vector3(5f*r*Mathf.Cos(6f*j*Mathf.Deg2Rad), 5f*r*Mathf.Sin(6f*j*Mathf.Deg2Rad), currentCamera.localPosition.z);
                                // currentCamera.Rotate(currentCamera.right, 10f*i);

                                if(trainAngles.Contains(j))
                                {
                                    string path = $"{trainImageSavePath}{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                    CaptureCameraView(currentCamera.GetComponent<Camera>(), path);
                                }
                                else if(testAngles.Contains(j))
                                {
                                    string path = $"{testImageSavePath}{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                    CaptureCameraView(currentCamera.GetComponent<Camera>(), path);
                                }

                                // currentCamera.Rotate(currentCamera.right, -10f*i);
                                currentCamera.Rotate(currentCamera.up, 6f);
                            }
                            break;
                        case "Chair":
                            currentCamera.localPosition = new Vector3(0, 0, (1f+1f*i)*extent.z);
                            currentCamera.localRotation = Quaternion.Euler(0, -90f, 0);

                            for(int j=0;j<60;j++)
                            {
                                float r = Mathf.Max(extent.x, extent.z);
                                currentCamera.localPosition = new Vector3(2f*r*Mathf.Cos(6f*j*Mathf.Deg2Rad), currentCamera.localPosition.z, 2f*r*Mathf.Sin(6f*j*Mathf.Deg2Rad));
                                // currentCamera.Rotate(currentCamera.right, 10f*i);

                                if(trainAngles.Contains(j))
                                {
                                    string path = $"{trainImageSavePath}{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                    CaptureCameraView(currentCamera.GetComponent<Camera>(), path);
                                }
                                else if(testAngles.Contains(j))
                                {
                                    string path = $"{testImageSavePath}{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                    CaptureCameraView(currentCamera.GetComponent<Camera>(), path);
                                }

                                // currentCamera.Rotate(currentCamera.right, -10f*i);
                                currentCamera.Rotate(currentCamera.up, -6f);
                            }
                            break;
                        case "Rack":
                            currentCamera.localPosition = new Vector3(0, 0, (1f+1f*i)*extent.z);
                            currentCamera.localRotation = Quaternion.Euler(0, -90f, 0);

                            for(int j=0;j<60;j++)
                            {
                                float r = Mathf.Max(extent.x, extent.z);
                                currentCamera.localPosition = new Vector3(3f*r*Mathf.Cos(6f*j*Mathf.Deg2Rad), currentCamera.localPosition.z, 3f*r*Mathf.Sin(6f*j*Mathf.Deg2Rad));
                                // currentCamera.Rotate(currentCamera.right, 10f*i);

                                if(trainAngles.Contains(j))
                                {
                                    string path = $"{trainImageSavePath}{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                    CaptureCameraView(currentCamera.GetComponent<Camera>(), path);
                                }
                                else if(testAngles.Contains(j))
                                {
                                    string path = $"{testImageSavePath}{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                    CaptureCameraView(currentCamera.GetComponent<Camera>(), path);
                                }

                                // currentCamera.Rotate(currentCamera.right, -10f*i);
                                currentCamera.Rotate(currentCamera.up, -6f);
                            }
                            break;
                        case "Sofa": 
                            currentCamera.localPosition = new Vector3(0, 0, (1f+1f*i)*extent.z);
                            currentCamera.localRotation = Quaternion.Euler(90f, 0, 0);
                            currentCamera.Rotate(currentCamera.up, -90f);

                            for(int j=0;j<60;j++)
                            {
                                float r = Mathf.Max(extent.x, extent.y);
                                currentCamera.localPosition = new Vector3(2f*r*Mathf.Cos(6f*j*Mathf.Deg2Rad), 2f*r*Mathf.Sin(6f*j*Mathf.Deg2Rad), currentCamera.localPosition.z);
                                // currentCamera.Rotate(currentCamera.right, 10f*i);

                                if(trainAngles.Contains(j))
                                {
                                    string path = $"{trainImageSavePath}{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                    CaptureCameraView(currentCamera.GetComponent<Camera>(), path);
                                }
                                else if(testAngles.Contains(j))
                                {
                                    string path = $"{testImageSavePath}{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                    CaptureCameraView(currentCamera.GetComponent<Camera>(), path);
                                }

                                // currentCamera.Rotate(currentCamera.right, -10f*i);
                                currentCamera.Rotate(currentCamera.up, 6f);
                            }
                            break;
                        // case "Pen": // currently used to save scene images without any targets
                    }
                }

                child.gameObject.SetActive(false);
                Destroy(cameraObj);
            }
        }
    }

    public void GenerateEnvImages()
    {
        // Initialized camera used for image capturing
        GameObject cameraObj = Instantiate(targetCamera, Vector3.zero, Quaternion.identity) as GameObject;

        // Lay camera in 3 height: 0.6, 1.2, and 1.8
        // Lay camera in 60 angels as before
        for(int i=1;i<4;i++)
        {
            cameraObj.transform.position = new Vector3(0f, 0.6f*i, 0f);

            for(int j=0;j<60;j++)
            {
                cameraObj.transform.Rotate(cameraObj.transform.up, 6f);

                if(trainAngles.Contains(j))
                {
                    string path = $"{trainImageSavePath}Env/Env_{i}_{j}.png"; // file save path
                    CaptureCameraView(cameraObj.transform.GetComponent<Camera>(), path);
                }
                else if(testAngles.Contains(j))
                {
                    string path = $"{testImageSavePath}Env/Env_{i}_{j}.png"; // file save path
                    CaptureCameraView(cameraObj.transform.GetComponent<Camera>(), path);
                }
            }
        }

        Destroy(cameraObj);
    }

    // Capture current camera view and write it into a png file
    public void CaptureCameraView(Camera m_camera, string path)
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
