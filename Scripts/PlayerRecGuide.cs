using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerRecGuide : MonoBehaviour
{
    public GameObject onFloorObjs;
    public GameObject onFurnitureTopObjs;
    public GameObject onWallObjs;
    public Material hightlightMat;

    private Text playerInstruction;
    private Dropdown targetDropdown;
    private Dropdown targetNumDropdown;

    private GameObject _objSelected;
    private Material _objOriMat;

    void Start()
    {
        playerInstruction = GameObject.Find("playerInstruction").GetComponent<Text>();
        targetDropdown = GameObject.Find("RecordingPanel/target").GetComponent<Dropdown>();
        targetNumDropdown = GameObject.Find("RecordingPanel/targetNum").GetComponent<Dropdown>();

        DisplayPlayerInstruction(); HighlightObjSelected();
    }

    // Give player instruction on screen
    public void DisplayPlayerInstruction()
    {
        string target = targetDropdown.captionText.text;
        switch(target)
        {
            case "OnFloor":
                playerInstruction.text = "Point to the red sofa on the floor with your right hand";
                break;
            case "OnFurnitureTop":
                playerInstruction.text = "Point to the red laptop on furniture top with your right hand";
                break;
            case "OnWall":
                playerInstruction.text = "Point to the red clock on the wall with your right hand";
                break;
            default:
                break;
        }
    }

    // Make object selected emissive
    public void HighlightObjSelected()
    {
        // Reset previous object material to normal
        if(_objSelected)
        {
            // for(int i=0;i<_objOriMat.Count;i++)
            // {
            //     _objSelected.GetComponentsInChildren<Renderer>()[i].material = _objOriMat[i];
            // }
            // _objOriMat.Clear();
            _objSelected.GetComponentInChildren<Renderer>().material = _objOriMat;
        }

        string target = targetDropdown.captionText.text;
        int targetNum = targetNumDropdown.value;
        
        // Activate selected target group
        switch(target)
        {
            case "OnFloor":
                onFloorObjs.SetActive(true); onFurnitureTopObjs.SetActive(false); onWallObjs.SetActive(false);
                _objSelected = onFloorObjs.transform.GetChild(targetNum).GetChild(0).gameObject;
                break;
            case "OnFurnitureTop":
                onFloorObjs.SetActive(false); onFurnitureTopObjs.SetActive(true); onWallObjs.SetActive(false);
                _objSelected = onFurnitureTopObjs.transform.GetChild(targetNum).GetChild(0).gameObject;
                break;
            case "OnWall":
                onFloorObjs.SetActive(false); onFurnitureTopObjs.SetActive(false); onWallObjs.SetActive(true);
                _objSelected = onWallObjs.transform.GetChild(targetNum).GetChild(0).gameObject;
                break;
            default:
                break;
        }
        
        // Change material of selected object
        // foreach(Renderer renderer in _objSelected.GetComponentsInChildren<Renderer>())
        // {
        //     _objOriMat.Add(renderer.material);
        //     renderer.material = hightlightMat;
        // }
       _objOriMat = _objSelected.GetComponentInChildren<Renderer>().material;
       _objSelected.GetComponentInChildren<Renderer>().material = hightlightMat;
    }
}
