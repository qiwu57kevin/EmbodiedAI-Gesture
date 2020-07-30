using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaptureImages : MonoBehaviour
{
    public Academy_Agent.targets[] capturedTargets;
    private Transform _target;

    public GameObject targetCamera;
    private Transform currentCamera;

    public void GenerateTargetImages()
    {
        foreach(Academy_Agent.targets target in capturedTargets)
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

                            for(int j=0;j<30;j++)
                            {
                                float r = Mathf.Max(extent.x, extent.y);
                                currentCamera.localPosition = new Vector3(5f*r*Mathf.Cos(12f*j*Mathf.Deg2Rad), 5f*r*Mathf.Sin(12f*j*Mathf.Deg2Rad), currentCamera.localPosition.z);
                                // currentCamera.Rotate(currentCamera.right, 10f*i);

                                string path = $"D:/ML-Agents/image-pretrain/{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                CaptureCameraView(currentCamera.GetComponent<Camera>(), path);

                                // currentCamera.Rotate(currentCamera.right, -10f*i);
                                currentCamera.Rotate(currentCamera.up, 12f);
                            }
                            break;
                        case "Chair":
                            currentCamera.localPosition = new Vector3(0, 0, (1f+1f*i)*extent.z);
                            currentCamera.localRotation = Quaternion.Euler(0, -90f, 0);

                            for(int j=0;j<30;j++)
                            {
                                float r = Mathf.Max(extent.x, extent.z);
                                currentCamera.localPosition = new Vector3(2f*r*Mathf.Cos(12f*j*Mathf.Deg2Rad), currentCamera.localPosition.z, 2f*r*Mathf.Sin(12f*j*Mathf.Deg2Rad));
                                // currentCamera.Rotate(currentCamera.right, 10f*i);

                                string path = $"D:/ML-Agents/image-pretrain/{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                CaptureCameraView(currentCamera.GetComponent<Camera>(), path);

                                // currentCamera.Rotate(currentCamera.right, -10f*i);
                                currentCamera.Rotate(currentCamera.up, -12f);
                            }
                            break;
                        case "Rack":
                            currentCamera.localPosition = new Vector3(0, 0, (1f+1f*i)*extent.z);
                            currentCamera.localRotation = Quaternion.Euler(0, -90f, 0);

                            for(int j=0;j<30;j++)
                            {
                                float r = Mathf.Max(extent.x, extent.z);
                                currentCamera.localPosition = new Vector3(3f*r*Mathf.Cos(12f*j*Mathf.Deg2Rad), currentCamera.localPosition.z, 3f*r*Mathf.Sin(12f*j*Mathf.Deg2Rad));
                                // currentCamera.Rotate(currentCamera.right, 10f*i);

                                string path = $"D:/ML-Agents/image-pretrain/{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                CaptureCameraView(currentCamera.GetComponent<Camera>(), path);

                                // currentCamera.Rotate(currentCamera.right, -10f*i);
                                currentCamera.Rotate(currentCamera.up, -12f);
                            }
                            break;
                        case "Sofa": 
                            currentCamera.localPosition = new Vector3(0, 0, (1f+1f*i)*extent.z);
                            currentCamera.localRotation = Quaternion.Euler(90f, 0, 0);
                            currentCamera.Rotate(currentCamera.up, -90f);

                            for(int j=0;j<30;j++)
                            {
                                float r = Mathf.Max(extent.x, extent.y);
                                currentCamera.localPosition = new Vector3(2f*r*Mathf.Cos(12f*j*Mathf.Deg2Rad), 2f*r*Mathf.Sin(12f*j*Mathf.Deg2Rad), currentCamera.localPosition.z);
                                // currentCamera.Rotate(currentCamera.right, 10f*i);

                                string path = $"D:/ML-Agents/image-pretrain/{target.ToString()}/{target.ToString()}{child.GetSiblingIndex()}_{i}_{j}.png"; // fiel save path
                                CaptureCameraView(currentCamera.GetComponent<Camera>(), path);

                                // currentCamera.Rotate(currentCamera.right, -10f*i);
                                currentCamera.Rotate(currentCamera.up, 12f);
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
